using System;
using System.Text.RegularExpressions;
using SimpleJSON;
using XUnity.Common.Logging;

namespace DeepSeekTranslate.Modules.Helpers
{
    public static class JsonResponseHelper
    {
        private static readonly Regex s_jsonObjectRegex = new Regex(@"\{(?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!))\}", RegexOptions.Singleline | RegexOptions.Compiled);

        public static JSONNode ParseJsonResponse(string responseText, bool debug)
        {
            // Direct parsing
            try
            {
                var jsonObj = JSON.Parse(responseText);
                var respMsg = jsonObj.AsObject["choices"].AsArray[0]["message"];
                return JSON.Parse(respMsg["content"]);
            }
            catch (Exception ex)
            {
                if (debug) { XuaLogger.AutoTranslator.Debug($"JsonResponseHelper: Direct JSON parsing failed: {ex.Message}"); }
            }

            // Try to extract JSON content from the response
            try
            {
                string jsonContent = ExtractLastJsonContent(responseText);
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    var jsonObj = JSON.Parse(jsonContent);
                    var respMsg = jsonObj.AsObject["choices"].AsArray[0]["message"];
                    return JSON.Parse(respMsg["content"]);
                }
            }
            catch (Exception ex)
            {
                if (debug) { XuaLogger.AutoTranslator.Debug($"JsonResponseHelper: JSON extraction failed: {ex.Message}"); }
            }

            throw new Exception($"Failed to parse JSON from response: {responseText}");
        }

        private static string ExtractLastJsonContent(string text)
        {
            // Look for content enclosed in {} or []
            var objectMatches = s_jsonObjectRegex.Matches(text);
            // var arrayMatches = Regex.Matches(text, @"\[(?:[^\[\]]|(?<open>\[)|(?<-open>\]))+(?(open)(?!))\]", RegexOptions.Singleline);

            string lastJsonContent = null;

            // Find the last JSON object
            if (objectMatches.Count > 0)
            {
                lastJsonContent = objectMatches[objectMatches.Count - 1].Value;
            }

            return lastJsonContent;
        }
    }
}