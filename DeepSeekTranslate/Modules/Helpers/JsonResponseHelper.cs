using SimpleJSON;
using System;
using XUnity.Common.Logging;

namespace DeepSeekTranslate.Modules.Helpers
{
    public static class JsonResponseHelper
    {
        public static JSONNode ParseJsonResponse(string responseText, bool debug)
        {
            var content = JSON.Parse(responseText)["choices"].AsArray[0]["message"]["content"];

            // Direct parsing
            try
            {
                return JSON.Parse(content);
            }
            catch (Exception ex)
            {
                if (debug) { XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.JsonResponseHelper: Direct JSON parsing failed: {ex.Message}"); }
            }

            // Try to extract JSON content from the response
            try
            {
                string jsonContent = ExtractJsonContent(content);
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    return JSON.Parse(jsonContent);
                }
            }
            catch (Exception ex)
            {
                if (debug) { XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.JsonResponseHelper: JSON extraction failed: {ex.Message}"); }
            }

            throw new Exception($"Failed to parse JSON from response: {responseText}");
        }

        private static string ExtractJsonContent(string text)
        {
            int firstBrace = text.IndexOf('{');
            int lastBrace = text.LastIndexOf('}');

            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return text.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            return null;
        }
    }
}