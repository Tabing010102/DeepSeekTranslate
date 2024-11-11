using DeepSeekTranslate.Models;
using System.Collections.Generic;
using System.Text;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Utilities;
using XUnity.Common.Logging;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
        public string MakeRequestStr(List<PromptMessage> prompts, string model, double temperature,
            int maxTokens, double frequencyPenalty = 0)
        {
            var sb = new StringBuilder();
            prompts.ForEach(p => { sb.Append($"{{\"role\":\"{JsonHelper.Escape(p.Role)}\",\"content\":\"{JsonHelper.Escape(p.Content)}\"}},"); });
            sb.Remove(sb.Length - 1, 1);
            var retStr =
                $"{{\"messages\":[{sb}]," +
                $"\"model\":\"{model}\"," +
                $"\"frequency_penalty\":{frequencyPenalty}," +
                $"\"max_tokens\":{maxTokens}," +
                $"\"presence_penalty\":0," +
                $"\"response_format\":{{\"type\":\"json_object\"}}," +
                $"\"stop\":null," +
                $"\"stream\":false," +
                $"\"stream_options\":null," +
                $"\"temperature\":{temperature}," +
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
