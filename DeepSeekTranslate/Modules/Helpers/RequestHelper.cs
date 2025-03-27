using DeepSeekTranslate.Models;
using System;
using System.Collections.Generic;
using System.Text;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Utilities;
using XUnity.Common.Logging;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
        private string MakeRequestStr(List<PromptMessage> prompts, int maxTokens, double frequencyPenalty = 0)
        {
            var sb = new StringBuilder();
            if (_addEndingAssistantPrompt) { prompts.Add(new PromptMessage("assistant", "我完全理解了翻译的要求与原则，我将遵循您的指示进行翻译，以下是对原文的翻译：")); }
            prompts.ForEach(p => { sb.Append($"{{\"role\":\"{JsonHelper.Escape(p.Role)}\",\"content\":\"{JsonHelper.Escape(p.Content)}\"}},"); });
            sb.Remove(sb.Length - 1, 1);
            var retStr =
                $"{{\"messages\":[{sb}]," +
                $"\"model\":\"{_model}\"," +
                $"\"frequency_penalty\":{frequencyPenalty}," +
                $"\"max_tokens\":{maxTokens}," +
                $"\"presence_penalty\":0," +
                $"\"response_format\":{{\"type\":\"json_object\"}}," +
                $"\"stop\":null," +
                $"\"stream\":false," +
                $"\"stream_options\":null," +
                $"\"temperature\":{_temperature}," +
                $"\"top_p\":1," +
                $"\"tools\":null," +
                $"\"tool_choice\":\"none\"," +
                $"\"logprobs\":false," +
                $"\"top_logprobs\":null}}";
            if (_debug) { XuaLogger.AutoTranslator.Debug($"MakeRequestStr: retStr={{{retStr}}}"); }
            return retStr;
        }

        private int GetMaxTokens(string originalText)
        {
            if (_maxTokensMode == MaxTokensMode.Static)
            {
                return _staticMaxTokens;
            }
            else if (_maxTokensMode == MaxTokensMode.Dynamic)
            {
                return Math.Max((int)Math.Ceiling(originalText.Length * _dynamicMaxTokensMultiplier), 20);
            }
            else
            {
                throw new Exception("Invalid max tokens mode.");
            }
        }
    }
}
