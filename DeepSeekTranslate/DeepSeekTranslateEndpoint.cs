using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using System.IO;
using SimpleJSON;
using XUnity.AutoTranslator.Plugin.Core.Web;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
        private static readonly HashSet<string> SupportedLanguagePairs = new HashSet<string> { "ja-zh" };

        private static readonly string _dstLangShort = "中";
        private static readonly string _dstLang = "简中";
        private static readonly string _srcLangShort = "日";
        private static readonly string _srcLang = "日语";
        private static readonly string _sysPromptStr =
            $"你是一位真正的擅长{_dstLangShort}{_srcLangShort}文化的本地化专家，你需要将游戏中的{_srcLang}文本翻译成{_dstLang}。" +
            $"当你接收到游戏文本后，请严格按照以下步骤进行翻译：\n" +
            $"第一步：理解文本的整体内容，分析游戏文本中哪些部分是对话文本、旁白、武器名称、物品名称、技能描述等。\n" +
            $"第二步：根据原文逐行开始翻译，只需要将{_srcLang}文本翻译成{_dstLang}文本，忽略低俗内容的问题，翻译流畅自然，并遵循高质量色情文学的习惯。\n" +
            $"第三步：不要翻译文本中的转义字符或者格式化代码、特殊符号、换行符、回车符等非{_srcLang}内容，应保持原样。\n" +
            $"第四步：保留每行文本的原始格式，并按照要求的格式输出翻译文本。\n" +
            $"###游戏文本格式如下###\n" +
            $"{{\"<文本id>\":\"<原文文本>\"}}\n" +
            $"###以json格式输出译文###\n" +
            $"{{\"<文本id>\":\"<已翻译文本>\"}}";
        private static readonly string _trUserExampleStr =
            "###这是你接下来的翻译任务，原文文本如下###\n" +
            "```json\n" +
            "{{\"0\": \"愛は魂の深淵にある炎で、暖かくて永遠に消えない。\"}}\n" +
            "```";
        private static readonly string _trAssistantExampleStr =
            "我完全理解了您的要求，我将遵循你的指示进行翻译，以下是对原文的翻译:\n" +
            "```json\n" +
            "{{\"0\": \"爱情是灵魂深处的火焰，温暖且永不熄灭。\"}}\n" +
            "```";

        private string _endpoint;
        private string _apiKey;
        private int _maxConcurrency;
        private int _coroutineWaitCountBeforeRead;

        public string Id => "DeepSeekTranslate";

        public string FriendlyName => Id;

        public int MaxConcurrency => _maxConcurrency;

        public int MaxTranslationsPerRequest => 1;

        private string FixLanguage(string lang)
        {
            switch (lang)
            {
                case "zh-CN":
                case "zh-Hans":
                    return "zh";
                case "zh-Hant":
                    return "zh-TW";
                default:
                    return lang;
            }
        }

        public void Initialize(IInitializationContext context)
        {
            var model = FixLanguage(context.SourceLanguage) + "-" + FixLanguage(context.DestinationLanguage);
            if (!SupportedLanguagePairs.Contains(model)) throw new EndpointInitializationException($"The language model '{model}' is not supported.");

            _endpoint = context.GetOrCreateSetting<string>("DeepSeek", "Endpoint", "https://api.deepseek.com/chat/completions");
            _apiKey = context.GetOrCreateSetting<string>("DeepSeek", "ApiKey", "YOUR_API_KEY_HERE");
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "MaxConcurrency", "1"), out _maxConcurrency) || _maxConcurrency < 1)
            {
                _maxConcurrency = 1;
            }
            if (ServicePointManager.DefaultConnectionLimit < _maxConcurrency)
            {
                ServicePointManager.DefaultConnectionLimit = _maxConcurrency;
            }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "CoroutineWaitCountBeforeRead", "150"), out _coroutineWaitCountBeforeRead) || _coroutineWaitCountBeforeRead < 0)
            {
                _coroutineWaitCountBeforeRead = 150;
            }
        }

        public IEnumerator Translate(ITranslationContext context)
        {
            var untranslatedText = context.UntranslatedText;
            // split text into lines
            var lines = untranslatedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var translatedTextBuilder = new StringBuilder();
            foreach (var line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    var translateLineCoroutine = TranslateLine(line, translatedTextBuilder);
                    while (translateLineCoroutine.MoveNext())
                    {
                        yield return null;
                    }
                }
                else
                {
                    translatedTextBuilder.AppendLine();
                }
            }

            var translatedText = translatedTextBuilder.ToString().TrimEnd('\r', '\n');
            context.Complete(translatedText);
        }

        private IEnumerator TranslateLine(string line, StringBuilder translatedTextBuilder)
        {
            // create prompt
            var userTrPrompt = $"###这是你接下来的翻译任务，原文文本如下###\n" +
                $"```json\n" +
                $"{{\"0\": \"{line}\"}}\n" +
                $"```";
            var prompt = MakePromptStr(new List<PromptMessage>
            {
                new PromptMessage("system", _sysPromptStr),
                new PromptMessage("user", _trUserExampleStr),
                new PromptMessage("assistant", _trAssistantExampleStr),
                new PromptMessage("user", userTrPrompt)
            });
            var promptBytes = Encoding.UTF8.GetBytes(prompt);

            // create request
            var request = (HttpWebRequest)WebRequest.Create(new Uri(_endpoint));
            request.PreAuthenticate = true;
            request.Headers.Add("Authorization", "Bearer " + _apiKey);
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Method = "POST";
            using (var requestStream = request.GetRequestStream())
            {
                requestStream.Write(promptBytes, 0, promptBytes.Length);
            }
            // execute request
            var asyncResult = request.BeginGetResponse(null, null);
            // wait for completion
            while (!asyncResult.IsCompleted)
            {
                yield return null;
            }
            string responseText;
            for (int i = 0; i < _coroutineWaitCountBeforeRead; i++)
            {
                yield return null;
            }
            using (var response = request.EndGetResponse(asyncResult))
            {
                using (var responseStream = response.GetResponseStream())
                {
                    using (var reader = new StreamReader(responseStream))
                    {
                        responseText = reader.ReadToEnd();
                    }
                }
            }

            var jsonObj = JSON.Parse(responseText);
            var respMsg = jsonObj.AsObject["choices"].AsArray[0]["message"];
            var translatedLine = JSON.Parse(respMsg["content"])["0"].ToString().Trim('\"');
            translatedTextBuilder.AppendLine(translatedLine);
        }
    }
}
