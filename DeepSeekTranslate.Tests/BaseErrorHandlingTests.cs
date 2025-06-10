using System.Reflection;

namespace DeepSeekTranslate.Tests
{
    public abstract class BaseErrorHandlingTests
    {
        protected DeepSeekTranslateEndpoint CreateEndpoint(int maxRetries = 0, bool useThreadPool = true, bool batchTranslate = false)
        {
            var endpoint = new DeepSeekTranslateEndpoint();
            SetPrivateField(endpoint, "_endpoint", "https://api.deepseek.com/chat/completions");
            SetPrivateField(endpoint, "_apiKey", "test-api-key");
            SetPrivateField(endpoint, "_maxRetries", maxRetries);
            SetPrivateField(endpoint, "_splitByLine", false);
            SetPrivateField(endpoint, "_debug", true);
            SetPrivateField(endpoint, "_useThreadPool", useThreadPool);
            SetPrivateField(endpoint, "_batchTranslate", batchTranslate);
            SetPrivateField(endpoint, "_maxTranslationsPerRequest", batchTranslate ? 10 : 1);
            SetPrivateField(endpoint, "_coroutineWaitCountBeforeRead", 0);
            SetPrivateField(endpoint, "_model", "deepseek-chat");
            SetPrivateField(endpoint, "_temperature", 0.3);
            SetPrivateField(endpoint, "_addEndingAssistantPrompt", true);
            return endpoint;
        }

        protected void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = typeof(DeepSeekTranslateEndpoint).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(obj, value);
        }
    }
} 