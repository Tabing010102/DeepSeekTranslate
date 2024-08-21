using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.Common.Logging;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
        private const bool DEBUG = false;

        private string _srcLangShort;
        private string _srcLang;
        private string _dstLangShort;
        private string _dstLang;
        private string _sysPromptStr;
        private string _trUserExampleStr;
        private string _trAssistantExampleStr;

        private string _endpoint;
        private string _apiKey;
        private int _maxConcurrency;
        private bool _batchTranslate;
        private int _maxTranslationsPerRequest;
        private int _coroutineWaitCountBeforeRead;
        private bool _useThreadPool;
        private int _minThreadCount;
        private int _maxThreadCount;

        public string Id => "DeepSeekTranslate";

        public string FriendlyName => Id;

        public int MaxConcurrency => _maxConcurrency;

        public int MaxTranslationsPerRequest => _maxTranslationsPerRequest;

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
            if (!_supportedSrcLangs.Contains(context.SourceLanguage) || !_supportedDstLangs.Contains(context.DestinationLanguage))
            {
                throw new EndpointInitializationException($"The language model '{context.SourceLanguage}-{context.DestinationLanguage}' is not supported.");
            }
            // init prompts
            _srcLangShort = _langShortZhDict[FixLanguage(context.SourceLanguage)];
            _srcLang = _langZhDict[FixLanguage(context.SourceLanguage)];
            _dstLangShort = _langShortZhDict[FixLanguage(context.DestinationLanguage)];
            _dstLang = _langZhDict[FixLanguage(context.DestinationLanguage)];
            _sysPromptStr =
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
            _trUserExampleStr =
                $"###这是你接下来的翻译任务，原文文本如下###\n" +
                $"```json\n" +
                $"{{\"0\": \"{_trExampleDict[FixLanguage(context.SourceLanguage)]}\"}}\n" +
                $"```";
            _trAssistantExampleStr =
                $"我完全理解了您的要求，我将遵循你的指示进行翻译，以下是对原文的翻译:\n" +
                $"```json\n" +
                $"{{\"0\": \"{_trExampleDict[FixLanguage(context.DestinationLanguage)]}\"}}\n" +
                $"```";

            // init settings
            _endpoint = context.GetOrCreateSetting<string>("DeepSeek", "Endpoint", "https://api.deepseek.com/chat/completions");
            _apiKey = context.GetOrCreateSetting<string>("DeepSeek", "ApiKey", "YOUR_API_KEY_HERE");
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "MaxConcurrency", "1"), out _maxConcurrency) || _maxConcurrency < 1) { _maxConcurrency = 1; }
            if (ServicePointManager.DefaultConnectionLimit < _maxConcurrency) { ServicePointManager.DefaultConnectionLimit = _maxConcurrency; }
            if (!bool.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "BatchTranslate", "false"), out _batchTranslate)) { _batchTranslate = false; }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "MaxTranslationsPerRequest", "1"), out _maxTranslationsPerRequest) || _maxTranslationsPerRequest < 1) { _maxTranslationsPerRequest = 1; }
            if (!_batchTranslate) { _maxTranslationsPerRequest = 1; }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "CoroutineWaitCountBeforeRead", "150"), out _coroutineWaitCountBeforeRead) || _coroutineWaitCountBeforeRead < 0) { _coroutineWaitCountBeforeRead = 150; }
            if (!bool.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "UseThreadPool", "true"), out _useThreadPool)) { _useThreadPool = true; }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "MinThreadCount", ""), out _minThreadCount) || _minThreadCount <= 0) { _minThreadCount = Environment.ProcessorCount * 2; }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "MaxThreadCount", ""), out _maxThreadCount) || _maxThreadCount <= 0) { _maxThreadCount = Environment.ProcessorCount * 4; }
            if (_useThreadPool)
            {
                ThreadPool.GetMinThreads(out int minWorkerThreads, out int minCompletionPortThreads);
                ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
                ThreadPool.SetMinThreads(Math.Max(minWorkerThreads, _minThreadCount), Math.Max(minCompletionPortThreads, _minThreadCount));
                ThreadPool.SetMaxThreads(Math.Max(maxWorkerThreads, _maxThreadCount), Math.Max(maxCompletionPortThreads, _maxThreadCount));
            }
        }

        public IEnumerator Translate(ITranslationContext context)
        {
            // batch translate force use thread pool
            if (_batchTranslate && _useThreadPool)
            {
                if (DEBUG)
                {
                    Console.WriteLine($"Translate: context={{{string.Join(", ", context.UntranslatedTexts)}}}");
                }
                var untranslatedTexts = context.UntranslatedTexts;
                // split text into lines
                var lines = new List<string>();
                var textLineDict = new Dictionary<int, int>(untranslatedTexts.Length);
                for (int i = 0; i < untranslatedTexts.Length; i++)
                {
                    textLineDict.Add(lines.Count, i);
                    lines.AddRange(untranslatedTexts[i].Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));
                }

                var lineNumberDict = new Dictionary<int, int>(lines.Count);
                int validLineCount = 0;
                var trJsonStrBuilder = new StringBuilder();
                for (int i = 0; i < lines.Count; i++)
                {
                    if (string.IsNullOrEmpty(lines[i]))
                    {
                        continue;
                    }
                    else
                    {
                        trJsonStrBuilder.Append($"\"{validLineCount}\": \"{lines[i]}\",\n");
                        lineNumberDict.Add(i, validLineCount);
                        validLineCount++;
                    }
                }
                trJsonStrBuilder.Remove(trJsonStrBuilder.Length - 2, 2);
                var translatedTextBuilders = new StringBuilder[untranslatedTexts.Length];
                for (int i = 0; i < translatedTextBuilders.Length; i++)
                {
                    translatedTextBuilders[i] = new StringBuilder();
                }
                var translateBatchCoroutine = TranslateBatch(trJsonStrBuilder.ToString(), validLineCount, lines.Count, lineNumberDict, textLineDict, translatedTextBuilders);
                while (translateBatchCoroutine.MoveNext())
                {
                    yield return null;
                }
                var translatedTexts = new string[untranslatedTexts.Length];
                for (int i = 0; i < translatedTextBuilders.Length; i++)
                {
                    translatedTexts[i] = translatedTextBuilders[i].ToString().TrimEnd(Environment.NewLine.ToCharArray());
                }

                if (DEBUG)
                {
                    Console.WriteLine($"Translate: translatedTexts={{{string.Join(", ", translatedTexts)}}}");
                }
                context.Complete(translatedTexts);
            }
            // per line translate
            else
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

                var translatedText = translatedTextBuilder.ToString().TrimEnd(Environment.NewLine.ToCharArray());
                context.Complete(translatedText);
            }
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

            if (!_useThreadPool)
            {
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
            else
            {
                bool isCompleted = false;
                ThreadPool.QueueUserWorkItem((state) =>
                {
                    // get response
                    string responseText;
                    using (var response = request.GetResponse())
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

                    isCompleted = true;
                });

                while (!isCompleted)
                {
                    yield return null;
                }
            }
        }

        private IEnumerator TranslateBatch(string trJsonStr, int lineCount, int totalLineCount, Dictionary<int, int> lineNumberDict,
            Dictionary<int, int> textLineDict, StringBuilder[] translatedTextBuilders)
        {
            if (DEBUG)
            {
                var lineNumberDictStr = string.Join(", ", lineNumberDict.Select(kv => $"{kv.Key}->{kv.Value}").ToArray());
                var textLineDictStr = string.Join(", ", textLineDict.Select(kv => $"{kv.Key}->{kv.Value}").ToArray());
                Console.WriteLine($"TranslateBatch: trJsonStr={{{trJsonStr}}}, lineCount={{{lineCount}}}, totalLineCount={{{totalLineCount}}}, " +
                    $"lineNumberDict={{{lineNumberDictStr}}}, textLineDict={{{textLineDictStr}}}");
            }
            // create prompt
            var userTrPrompt = $"###这是你接下来的翻译任务，原文文本如下###\n" +
                $"```json\n" +
                $"{{{trJsonStr}}}\n" +
                $"```";
            var prompt = MakePromptStr(new List<PromptMessage>
            {
                new PromptMessage("system", _sysPromptStr),
                new PromptMessage("user", _trUserExampleStr),
                new PromptMessage("assistant", _trAssistantExampleStr),
                new PromptMessage("user", userTrPrompt)
            });
            if (DEBUG) { Console.WriteLine($"TranslateBatch: prompt={{{prompt}}}"); }
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
            if (DEBUG) { Console.WriteLine($"TranslateBatch: request filled"); }

            bool isCompleted = false;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                // get response
                string responseText;
                if (DEBUG) { Console.WriteLine($"TranslateBatch: sending request"); }
                using (var response = request.GetResponse())
                {
                    using (var responseStream = response.GetResponseStream())
                    {
                        using (var reader = new StreamReader(responseStream))
                        {
                            responseText = reader.ReadToEnd();
                        }
                    }
                }
                if (DEBUG) { Console.WriteLine($"TranslateBatch: responseText={{{responseText}}}"); }
                var jsonObj = JSON.Parse(responseText);
                var respMsg = jsonObj.AsObject["choices"].AsArray[0]["message"];
                var contents = JSON.Parse(respMsg["content"]);
                if (DEBUG) { Console.WriteLine($"TranslateBatch: contents.Count={{{contents.Count}}}, lineCount={{{lineCount}}}"); }
                if (contents.Count != lineCount)
                {
                    throw new Exception("The number of translated lines does not match the number of lines to be translated.");
                }
                int textPos = 0;
                for (int i = 0; i < totalLineCount; i++)
                {
                    if (textLineDict.ContainsKey(i)) { textPos = textLineDict[i]; }
                    if (DEBUG) { Console.WriteLine($"TranslateBatch: i={{{i}}}, textPos={{{textPos}}}"); }
                    if (!lineNumberDict.ContainsKey(i))
                    {
                        translatedTextBuilders[textPos].AppendLine();
                    }
                    else
                    {
                        if (DEBUG) { Console.WriteLine($"TranslateBatch: i={{{i}}}, lineNumberDict[i]={{{lineNumberDict[i]}}}, contents[lineNumberDict[i]]={{{contents[lineNumberDict[i].ToString()].ToString().Trim('\"')}}}"); }
                        translatedTextBuilders[textPos].AppendLine(contents[lineNumberDict[i].ToString()].ToString().Trim('\"'));
                        if (DEBUG) { Console.WriteLine($"TranslateBatch: i={{{i}}}, textPos={{{textPos}}}, translatedTextBuilders[textPos]={{{translatedTextBuilders[textPos].ToString()}}}"); }
                    }
                }

                isCompleted = true;
                if (DEBUG) { Console.WriteLine($"TranslateBatch: translatedTexts={{{string.Join(", ", translatedTextBuilders.Select(tb => tb.ToString()).ToArray())}}}"); }
            });

            while (!isCompleted)
            {
                yield return null;
            }
        }
    }
}
