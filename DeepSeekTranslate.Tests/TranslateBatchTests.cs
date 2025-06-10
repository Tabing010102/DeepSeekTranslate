using HttpWebRequestWrapper;
using System;
using System.Collections;
using System.Net;
using System.Threading;
using Xunit;

namespace DeepSeekTranslate.Tests
{
    public class TranslateBatchTests : BaseErrorHandlingTests
    {
        [Fact]
        public void TranslateBatch_InvalidJsonResponse_ThrowsException()
        {
            var fakeResponseBody = "这不是有效的JSON";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                    request.HttpWebResponseCreator.Create(fakeResponseBody, HttpStatusCode.OK))))
            {
                var endpoint = CreateEndpoint(batchTranslate: true, useThreadPool: true);
                var context = new TestTranslationContext(new[] { "Hello", "World", "Test" });

                var enumerator = endpoint.Translate(context);

                var ex = Assert.ThrowsAny<Exception>(() =>
                {
                    while (enumerator.MoveNext()) { }
                });
            }
        }

        [Fact]
        public void TranslateBatch_JsonResponseWithError_ThrowsException()
        {
            var fakeResponseBody = @"{""error"": {""message"": ""无效的API密钥"", ""type"": ""invalid_request_error""}}";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                    request.HttpWebResponseCreator.Create(fakeResponseBody, HttpStatusCode.OK))))
            {
                var endpoint = CreateEndpoint(batchTranslate: true, useThreadPool: true);
                var context = new TestTranslationContext(new[] { "Hello", "World" });

                var enumerator = endpoint.Translate(context);

                var ex = Assert.Throws<Exception>(() =>
                {
                    while (enumerator.MoveNext()) { }
                });

                Assert.Contains("Failed to parse JSON from response", ex.Message);
            }
        }

        [Theory]
        [InlineData((HttpStatusCode)429)] // 429 Too Many Requests
        [InlineData((HttpStatusCode)503)] // 503 Service Unavailable  
        public void TranslateBatch_HttpError429Or503_StopsRetriesAndThrows(HttpStatusCode statusCode)
        {
            var attempts = 0;
            var fakeGoodResponseBody = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""content"": ""{\""0\"": \""你好\"", \""1\"": \""世界\"", \""2\"": \""测试\""}""
                        }
                    }
                ]
            }";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                {
                    Interlocked.Increment(ref attempts);
                    if (attempts == 1)
                    {
                        var response = request.HttpWebResponseCreator.Create("", statusCode);
                        throw new WebException("模拟服务器错误", null, WebExceptionStatus.ProtocolError, response);
                    }
                    return request.HttpWebResponseCreator.Create(fakeGoodResponseBody, HttpStatusCode.OK);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2, batchTranslate: true, useThreadPool: true);
                var context = new TestTranslationContext(new[] { "Hello", "World", "Test" });

                var enumerator = endpoint.Translate(context);

                var ex = Assert.Throws<WebException>(() =>
                {
                    while (enumerator.MoveNext()) { }
                });

                Assert.Equal(WebExceptionStatus.ProtocolError, ex.Status);
                Assert.Equal(statusCode, ((HttpWebResponse)ex.Response).StatusCode);
                Assert.Equal(1, attempts); // 应该不重试429和503错误
            }
        }

        [Fact]
        public void TranslateBatch_TransientError_RetriesAndSucceeds()
        {
            var attempts = 0;
            var fakeGoodResponseBody = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""content"": ""{\""0\"": \""你好\"", \""1\"": \""世界\""}""
                        }
                    }
                ]
            }";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                {
                    Interlocked.Increment(ref attempts);
                    if (attempts == 1)
                    {
                        throw new WebException("模拟临时错误", WebExceptionStatus.Timeout);
                    }
                    return request.HttpWebResponseCreator.Create(fakeGoodResponseBody, HttpStatusCode.OK);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2, batchTranslate: true, useThreadPool: true);
                var context = new TestTranslationContext(new[] { "Hello", "World" });

                var enumerator = endpoint.Translate(context);

                while (enumerator.MoveNext()) { }

                Assert.True(context.IsDone);
                Assert.NotNull(context.TranslatedTexts);
                Assert.Equal(2, context.TranslatedTexts.Length);
                Assert.Equal("你好", context.TranslatedTexts[0]);
                Assert.Equal("世界", context.TranslatedTexts[1]);
                Assert.Equal(2, attempts); // 第一次失败，第二次成功
            }
        }

        [Fact]
        public void TranslateBatch_MaxRetriesExceeded_ThrowsException()
        {
            var attempts = 0;

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                {
                    Interlocked.Increment(ref attempts);
                    throw new WebException("持续错误", WebExceptionStatus.Timeout);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2, batchTranslate: true, useThreadPool: true);
                var context = new TestTranslationContext(new[] { "Hello", "World", "Test" });

                var enumerator = endpoint.Translate(context);

                var ex = Assert.Throws<WebException>(() =>
                {
                    while (enumerator.MoveNext()) { }
                });

                Assert.Equal(WebExceptionStatus.Timeout, ex.Status);
                Assert.Equal(3, attempts); // 原始尝试 + 2次重试
            }
        }

        [Fact]
        public void TranslateBatch_MismatchedLineCount_ThrowsException()
        {
            // 返回的翻译行数与请求的不匹配
            var fakeResponseBody = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""content"": ""{\""0\"": \""你好\""}""
                        }
                    }
                ]
            }";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                    request.HttpWebResponseCreator.Create(fakeResponseBody, HttpStatusCode.OK))))
            {
                var endpoint = CreateEndpoint(batchTranslate: true, useThreadPool: true);
                var context = new TestTranslationContext(new[] { "Hello", "World", "Test" }); // 3个文本

                var enumerator = endpoint.Translate(context);

                var ex = Assert.Throws<Exception>(() =>
                {
                    while (enumerator.MoveNext()) { }
                });

                Assert.Contains("The number of translated lines does not match", ex.Message);
            }
        }

        [Fact]
        public void TranslateBatch_Http500Error_RetriesAndSucceeds()
        {
            var attempts = 0;
            var fakeGoodResponseBody = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""content"": ""{\""0\"": \""项目\"", \""1\"": \""测试\"", \""2\"": \""批处理\""}""
                        }
                    }
                ]
            }";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                {
                    Interlocked.Increment(ref attempts);
                    if (attempts == 1)
                    {
                        var response = request.HttpWebResponseCreator.Create("Internal Server Error", HttpStatusCode.InternalServerError);
                        throw new WebException("服务器内部错误", null, WebExceptionStatus.ProtocolError, response);
                    }
                    return request.HttpWebResponseCreator.Create(fakeGoodResponseBody, HttpStatusCode.OK);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2, batchTranslate: true, useThreadPool: true);
                var context = new TestTranslationContext(new[] { "Project", "Test", "Batch" });

                var enumerator = endpoint.Translate(context);

                while (enumerator.MoveNext()) { }

                Assert.True(context.IsDone);
                Assert.NotNull(context.TranslatedTexts);
                Assert.Equal(3, context.TranslatedTexts.Length);
                Assert.Equal("项目", context.TranslatedTexts[0]);
                Assert.Equal("测试", context.TranslatedTexts[1]);
                Assert.Equal("批处理", context.TranslatedTexts[2]);
                Assert.Equal(2, attempts);
            }
        }

        [Fact]
        public void TranslateBatch_EmptyStringsInBatch_HandlesCorrectly()
        {
            var fakeResponseBody = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""content"": ""{\""0\"": \""你好\"", \""1\"": \""世界\""}""
                        }
                    }
                ]
            }";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                    request.HttpWebResponseCreator.Create(fakeResponseBody, HttpStatusCode.OK))))
            {
                var endpoint = CreateEndpoint(batchTranslate: true, useThreadPool: true);
                // 包含空字符串的批量翻译
                var context = new TestTranslationContext(new[] { "Hello", "", "World", "" });

                var enumerator = endpoint.Translate(context);

                while (enumerator.MoveNext()) { }

                Assert.True(context.IsDone);
                Assert.NotNull(context.TranslatedTexts);
                Assert.Equal(4, context.TranslatedTexts.Length);
                // 第一个和第三个应该被翻译，第二个和第四个应该保持空
                Assert.Equal("你好", context.TranslatedTexts[0]);
                Assert.Equal("", context.TranslatedTexts[1]);
                Assert.Equal("世界", context.TranslatedTexts[2]);
                Assert.Equal("", context.TranslatedTexts[3]);
            }
        }

        [Fact]
        public void TranslateBatch_LargeTextBatch_HandlesCorrectly()
        {
            var fakeResponseBody = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""content"": ""{\""0\"": \""很长的文本1\"", \""1\"": \""很长的文本2\"", \""2\"": \""很长的文本3\"", \""3\"": \""很长的文本4\"", \""4\"": \""很长的文本5\""}""
                        }
                    }
                ]
            }";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                    request.HttpWebResponseCreator.Create(fakeResponseBody, HttpStatusCode.OK))))
            {
                var endpoint = CreateEndpoint(batchTranslate: true, useThreadPool: true);
                // 包含长文本的批量翻译测试
                var longTexts = new[]
                {
                    "This is a very long text that needs to be translated with multiple sentences and complex content.",
                    "Another long text with different content that also needs careful translation handling.",
                    "Yet another lengthy piece of text to test the batch translation capabilities thoroughly.",
                    "Even more text to ensure that the batch processing can handle multiple large items efficiently.",
                    "The final long text in this batch to verify complete functionality of the translation system."
                };
                var context = new TestTranslationContext(longTexts);

                var enumerator = endpoint.Translate(context);

                while (enumerator.MoveNext()) { }

                Assert.True(context.IsDone);
                Assert.NotNull(context.TranslatedTexts);
                Assert.Equal(5, context.TranslatedTexts.Length);
                for (int i = 0; i < 5; i++)
                {
                    Assert.Equal($"很长的文本{i + 1}", context.TranslatedTexts[i]);
                }
            }
        }
    }
} 