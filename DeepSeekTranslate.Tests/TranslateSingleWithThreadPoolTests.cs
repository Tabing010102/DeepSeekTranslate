using HttpWebRequestWrapper;
using System;
using System.Collections;
using System.Net;
using System.Threading;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DeepSeekTranslate.Tests
{
    public class TranslateSingleWithThreadPoolTests : BaseErrorHandlingTests
    {
        [Fact]
        public void TranslateSingle_WithThreadPool_InvalidJsonResponse_ThrowsException()
        {
            var fakeResponseBody = "这不是有效的JSON";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                    request.HttpWebResponseCreator.Create(fakeResponseBody, HttpStatusCode.OK))))
            {
                var endpoint = CreateEndpoint(useThreadPool: true);
                var context = new TestTranslationContext("Hello");

                var enumerator = endpoint.Translate(context);

                var ex = Assert.ThrowsAny<Exception>(() =>
                {
                    while (enumerator.MoveNext()) { }
                });
            }
        }

        [Fact]
        public void TranslateSingle_WithThreadPool_JsonResponseWithError_ThrowsException()
        {
            var fakeResponseBody = @"{""error"": {""message"": ""无效的API密钥"", ""type"": ""invalid_request_error""}}";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                    request.HttpWebResponseCreator.Create(fakeResponseBody, HttpStatusCode.OK))))
            {
                var endpoint = CreateEndpoint(useThreadPool: true);
                var context = new TestTranslationContext("Hello");

                var enumerator = endpoint.Translate(context);

                var ex = Assert.ThrowsAny<Exception>(() =>
                {
                    while (enumerator.MoveNext()) { }
                });
            }
        }

        [Theory]
        [InlineData((HttpStatusCode)429)] // 429 Too Many Requests
        [InlineData((HttpStatusCode)503)] // 503 Service Unavailable
        public void TranslateSingle_WithThreadPool_HttpError429Or503_StopsRetriesAndThrows(HttpStatusCode statusCode)
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
                    Interlocked.Increment(ref attempts);
                    if (attempts == 1)
                    {
                        var response = request.HttpWebResponseCreator.Create("", statusCode);
                        throw new WebException("模拟服务器错误", null, WebExceptionStatus.ProtocolError, response);
                    }
                    return request.HttpWebResponseCreator.Create(fakeGoodResponseBody, HttpStatusCode.OK);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2, useThreadPool: true);
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
        public void TranslateSingle_WithThreadPool_TransientError_RetriesAndSucceeds()
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
                    Interlocked.Increment(ref attempts);
                    if (attempts == 1)
                    {
                        throw new WebException("模拟临时错误", WebExceptionStatus.Timeout);
                    }
                    return request.HttpWebResponseCreator.Create(fakeGoodResponseBody, HttpStatusCode.OK);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2, useThreadPool: true);
                var context = new TestTranslationContext("Hello");

                var enumerator = endpoint.Translate(context);

                while (enumerator.MoveNext()) { }

                Assert.True(context.IsDone);
                Assert.Equal("你好", context.TranslatedText);
                Assert.Equal(2, attempts); // 第一次失败，第二次成功
            }
        }

        [Fact]
        public void TranslateSingle_WithThreadPool_MaxRetriesExceeded_ThrowsException()
        {
            var attempts = 0;

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                {
                    Interlocked.Increment(ref attempts);
                    throw new WebException("持续错误", WebExceptionStatus.Timeout);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2, useThreadPool: true);
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
        public void TranslateSingle_WithThreadPool_Http500Error_RetriesAndSucceeds()
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
                    Interlocked.Increment(ref attempts);
                    if (attempts == 1)
                    {
                        var response = request.HttpWebResponseCreator.Create("Internal Server Error", HttpStatusCode.InternalServerError);
                        throw new WebException("服务器内部错误", null, WebExceptionStatus.ProtocolError, response);
                    }
                    return request.HttpWebResponseCreator.Create(fakeGoodResponseBody, HttpStatusCode.OK);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2, useThreadPool: true);
                var context = new TestTranslationContext("World");

                var enumerator = endpoint.Translate(context);

                while (enumerator.MoveNext()) { }

                Assert.True(context.IsDone);
                Assert.Equal("世界", context.TranslatedText);
                Assert.Equal(2, attempts);
            }
        }

        [Fact]
        public void TranslateSingle_WithThreadPool_MultipleSimultaneousRequests_HandlesErrors()
        {
            var attempts = 0;
            var successfulTranslations = 0;
            var failureCount = 0;

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                {
                    var currentAttempt = Interlocked.Increment(ref attempts);
                    
                    // 让第1和第3个请求失败，第2个请求成功
                    if (currentAttempt == 1 || currentAttempt == 3)
                    {
                        throw new WebException("模拟错误", WebExceptionStatus.Timeout);
                    }
                    
                    var fakeResponseBody = @"{
                        ""choices"": [
                            {
                                ""message"": {
                                    ""content"": ""{\""0\"": \""测试成功\""}""
                                }
                            }
                        ]
                    }";
                    return request.HttpWebResponseCreator.Create(fakeResponseBody, HttpStatusCode.OK);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 0, useThreadPool: true);
                
                // 启动3个翻译任务
                var tasks = new[]
                {
                    new { Context = new TestTranslationContext("Test1"), Enumerator = (IEnumerator)null },
                    new { Context = new TestTranslationContext("Test2"), Enumerator = (IEnumerator)null },
                    new { Context = new TestTranslationContext("Test3"), Enumerator = (IEnumerator)null }
                };

                // 初始化枚举器
                for (int i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = new { 
                        Context = tasks[i].Context, 
                        Enumerator = endpoint.Translate(tasks[i].Context) 
                    };
                }

                // 执行所有任务
                for (int i = 0; i < tasks.Length; i++)
                {
                    try
                    {
                        while (tasks[i].Enumerator.MoveNext()) { }
                        if (tasks[i].Context.IsDone && !string.IsNullOrEmpty(tasks[i].Context.TranslatedText))
                        {
                            Interlocked.Increment(ref successfulTranslations);
                        }
                    }
                    catch (WebException)
                    {
                        Interlocked.Increment(ref failureCount);
                    }
                }

                // 应该有1个成功，2个失败
                Assert.Equal(1, successfulTranslations);
                Assert.Equal(2, failureCount);
                Assert.Equal(3, attempts);
            }
        }
    }
} 