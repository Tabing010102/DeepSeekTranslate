using DeepSeekTranslate.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Utilities;
using XUnity.Common.Logging;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
        public string MakeRequestStr(List<PromptMessage> prompts, double frequencyPenalty = 0)
        {
            var sb = new StringBuilder();
            prompts.ForEach(p => { sb.Append($"{{\"role\":\"{JsonHelper.Escape(p.Role)}\",\"content\":\"{JsonHelper.Escape(p.Content)}\"}},"); });
            sb.Remove(sb.Length - 1, 1);
            int maxTokens;
            if (_maxTokensMode == MaxTokensMode.Static) { maxTokens = _staticMaxTokens; }
            else if (_maxTokensMode == MaxTokensMode.Dynamic) { maxTokens = (int)Math.Ceiling(prompts.Last().Content.Length * _dynamicMaxTokensMultiplier); }
            else { throw new Exception("Invalid max tokens mode."); }
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
    }
}
