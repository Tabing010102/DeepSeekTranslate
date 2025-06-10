using HttpWebRequestWrapper;
using System;
using System.Net;
using System.Reflection;
using Xunit;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;

namespace DeepSeekTranslate.Tests
{
    public class ErrorHandlingTests
    {
        private class TestTranslationContext : ITranslationContext
        {
            public string[] UntranslatedTexts { get; }
            public string UntranslatedText => UntranslatedTexts[0];
            public string SourceLanguage { get; }
            public string DestinationLanguage { get; }
            public bool IsDone { get; private set; }
            public string TranslatedText { get; private set; }

            public TestTranslationContext(string text)
            {
                UntranslatedTexts = new[] { text };
            }

            public void Complete(string translation)
            {
                TranslatedText = translation;
                IsDone = true;
            }

            public void Complete(string[] translations)
            {
                TranslatedText = translations[0];
                IsDone = true;
            }

            public void Fail(string reason, Exception error)
            {
                IsDone = true;
                throw new Exception($"Translation failed: {reason}", error);
            }

            public void Fail(string reason)
            {
                IsDone = true;
                throw new Exception($"Translation failed: {reason}");
            }
        }

        private DeepSeekTranslateEndpoint CreateEndpoint(bool splitByLine = false, int maxRetries = 1)
        {
            var endpoint = new DeepSeekTranslateEndpoint();
            SetPrivateField(endpoint, "_endpoint", "https://api.deepseek.com/chat/completions");
            SetPrivateField(endpoint, "_apiKey", "test-api-key");
            SetPrivateField(endpoint, "_maxRetries", maxRetries);
            SetPrivateField(endpoint, "_splitByLine", splitByLine);
            SetPrivateField(endpoint, "_debug", true);
            SetPrivateField(endpoint, "_useThreadPool", false);
            SetPrivateField(endpoint, "_coroutineWaitCountBeforeRead", 0);
            return endpoint;
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = typeof(DeepSeekTranslateEndpoint).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(obj, value);
        }

        [Fact]
        public void Translate_InvalidJsonResponse_ThrowsException()
        {
            var fakeResponseBody = "this is not json";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                    request.HttpWebResponseCreator.Create(fakeResponseBody, HttpStatusCode.OK))))
            {
                var endpoint = CreateEndpoint();
                var context = new TestTranslationContext("Hello");

                var enumerator = endpoint.Translate(context);

                var ex = Assert.ThrowsAny<Exception>(() =>
                {
                    while (enumerator.MoveNext()) { }
                });
            }
        }

        [Fact]
        public void Translate_JsonResponseWithError_ThrowsException()
        {
            var fakeResponseBody = @"{""error"": {""message"": ""Invalid API key"", ""type"": ""invalid_request_error""}}";

            using (new HttpWebRequestWrapperSession(
                new HttpWebRequestWrapperInterceptorCreator(request =>
                    request.HttpWebResponseCreator.Create(fakeResponseBody, HttpStatusCode.OK))))
            {
                var endpoint = CreateEndpoint();
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
        [InlineData((HttpStatusCode)429)] // 429
        [InlineData((HttpStatusCode)503)] // 503
        public void Translate_HttpError429Or503_StopsRetriesAndThrows(HttpStatusCode statusCode)
        {
            var attempts = 0;
            var fakeGoodResponseBody = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""content"": ""{\""0\"": \""Hola\""}""
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
                        throw new WebException("Simulated server error", null, WebExceptionStatus.ProtocolError, response);
                    }
                    return request.HttpWebResponseCreator.Create(fakeGoodResponseBody, HttpStatusCode.OK);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2);
                var context = new TestTranslationContext("Hello");

                var enumerator = endpoint.Translate(context);

                var ex = Assert.Throws<WebException>(() =>
                {
                    while (enumerator.MoveNext()) { }
                });

                Assert.Equal(WebExceptionStatus.ProtocolError, ex.Status);
                Assert.Equal(statusCode, ((HttpWebResponse)ex.Response).StatusCode);
                Assert.Equal(1, attempts);
            }
        }

        [Fact]
        public void Translate_TransientError_RetriesAndSucceeds()
        {
            var attempts = 0;
            var fakeGoodResponseBody = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""content"": ""{\""0\"": \""Hola\""}""
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
                        throw new WebException("Simulated transient error", WebExceptionStatus.Timeout);
                    }
                    return request.HttpWebResponseCreator.Create(fakeGoodResponseBody, HttpStatusCode.OK);
                })))
            {
                var endpoint = CreateEndpoint(maxRetries: 2);
                var context = new TestTranslationContext("Hello");

                var enumerator = endpoint.Translate(context);

                while (enumerator.MoveNext()) { }

                Assert.True(context.IsDone);
                Assert.Equal("Hola", context.TranslatedText);
                Assert.Equal(2, attempts);
            }
        }
    }
}