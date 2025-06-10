using HttpWebRequestWrapper;
using System;
using System.Net;
using Xunit;

namespace DeepSeekTranslate.Tests
{
    public class TranslateSingleWithoutThreadPoolTests : BaseErrorHandlingTests
    {
        [Fact]
        public void TranslateSingle_WithoutThreadPool_InvalidJsonResponse_ThrowsException()
        {
            var fakeResponseBody = "这不是有效的JSON";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                    request.HttpWebResponseCreator.Create(fakeResponseBody, HttpStatusCode.OK))))
            {
                var endpoint = CreateEndpoint(useThreadPool: false);
                var context = new TestTranslationContext("Hello");

                var enumerator = endpoint.Translate(context);

                var ex = Assert.ThrowsAny<Exception>(() =>
                {
                    while (enumerator.MoveNext()) { }
                });
            }
        }

        [Fact]
        public void TranslateSingle_WithoutThreadPool_JsonResponseWithError_ThrowsException()
        {
            var fakeResponseBody = @"{""error"": {""message"": ""无效的API密钥"", ""type"": ""invalid_request_error""}}";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                    request.HttpWebResponseCreator.Create(fakeResponseBody, HttpStatusCode.OK))))
            {
                var endpoint = CreateEndpoint(useThreadPool: false);
                var context = new TestTranslationContext("Hello");

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
        public void TranslateSingle_WithoutThreadPool_HttpError429Or503_StopsRetriesAndThrows(HttpStatusCode statusCode)
        {
            var attempts = 0;
            var fakeGoodResponseBody = @"{
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
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        var response = request.HttpWebResponseCreator.Create("", statusCode);
                        throw new WebException("模拟服务器错误", null, WebExceptionStatus.ProtocolError, response);
                    }
                    return request.HttpWebResponseCreator.Create(fakeGoodResponseBody, HttpStatusCode.OK);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2, useThreadPool: false);
                var context = new TestTranslationContext("Hello");

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
        public void TranslateSingle_WithoutThreadPool_TransientError_RetriesAndSucceeds()
        {
            var attempts = 0;
            var fakeGoodResponseBody = @"{
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
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        throw new WebException("模拟临时错误", WebExceptionStatus.Timeout);
                    }
                    return request.HttpWebResponseCreator.Create(fakeGoodResponseBody, HttpStatusCode.OK);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2, useThreadPool: false);
                var context = new TestTranslationContext("Hello");

                var enumerator = endpoint.Translate(context);

                while (enumerator.MoveNext()) { }

                Assert.True(context.IsDone);
                Assert.Equal("你好", context.TranslatedText);
                Assert.Equal(2, attempts); // 第一次失败，第二次成功
            }
        }

        [Fact]
        public void TranslateSingle_WithoutThreadPool_MaxRetriesExceeded_ThrowsException()
        {
            var attempts = 0;

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                {
                    attempts++;
                    throw new WebException("持续错误", WebExceptionStatus.Timeout);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2, useThreadPool: false);
                var context = new TestTranslationContext("Hello");

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
        public void TranslateSingle_WithoutThreadPool_Http500Error_RetriesAndSucceeds()
        {
            var attempts = 0;
            var fakeGoodResponseBody = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""content"": ""{\""0\"": \""世界\""}""
                        }
                    }
                ]
            }";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        var response = request.HttpWebResponseCreator.Create("Internal Server Error", HttpStatusCode.InternalServerError);
                        throw new WebException("服务器内部错误", null, WebExceptionStatus.ProtocolError, response);
                    }
                    return request.HttpWebResponseCreator.Create(fakeGoodResponseBody, HttpStatusCode.OK);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2, useThreadPool: false);
                var context = new TestTranslationContext("World");

                var enumerator = endpoint.Translate(context);

                while (enumerator.MoveNext()) { }

                Assert.True(context.IsDone);
                Assert.Equal("世界", context.TranslatedText);
                Assert.Equal(2, attempts);
            }
        }
    }
} 