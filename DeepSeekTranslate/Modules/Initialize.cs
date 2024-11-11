using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Utilities;
using XUnity.Common.Logging;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
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

        private List<string> GetDictStringList(IEnumerable<KeyValuePair<string, List<string>>> kvPairs)
        {
            List<string> dictList = new List<string>();
            foreach (var entry in kvPairs)
            {
                var src = entry.Key;
                var dst = entry.Value[0];
                var info = entry.Value[1];
                dictList.Add($"|\t{src}\t|\t{dst}\t|\t{(string.IsNullOrEmpty(info) ? " " : info)}\t|");
            }

            return dictList;
        }

        public void Initialize(IInitializationContext context)
        {
            if (!s_supportedSrcLangs.Contains(context.SourceLanguage) || !s_supportedDstLangs.Contains(context.DestinationLanguage))
            {
                throw new EndpointInitializationException($"The language model '{context.SourceLanguage}-{context.DestinationLanguage}' is not supported.");
            }
            // init prompts
            _srcLangShort = s_langShortZhDict[FixLanguage(context.SourceLanguage)];
            _srcLang = s_langZhDict[FixLanguage(context.SourceLanguage)];
            _dstLangShort = s_langShortZhDict[FixLanguage(context.DestinationLanguage)];
            _dstLang = s_langZhDict[FixLanguage(context.DestinationLanguage)];
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
                $"{{\"0\":\"{s_trExampleDict[FixLanguage(context.SourceLanguage)]}\"}}\n" +
                $"```";
            _trAssistantExampleStr =
                $"我完全理解了您的要求，我将遵循你的指示进行翻译，以下是对原文的翻译:\n" +
                $"```json\n" +
                $"{{\"0\":\"{s_trExampleDict[FixLanguage(context.DestinationLanguage)]}\"}}\n" +
                $"```";

            // init settings
            _endpoint = context.GetOrCreateSetting<string>("DeepSeek", "Endpoint", "https://api.deepseek.com/chat/completions");
            _apiKey = context.GetOrCreateSetting<string>("DeepSeek", "ApiKey", "YOUR_API_KEY_HERE");
            _model = context.GetOrCreateSetting<string>("DeepSeek", "Model", "deepseek-chat");
            if (!double.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "Temperature", "1.3"), out _temperature) || _temperature <= 0) { _temperature = 1.3; }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "MaxTokens", "1024"), out _maxTokens) || _maxTokens <= 0) { _maxTokens = 1024; }
            // init dict
            #region init dict
            if (!bool.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "UseDict", "false"), out _useDict)) { _useDict = false; }
            _dictMode = context.GetOrCreateSetting<string>("DeepSeek", "DictMode", "full");
            var dictStr = context.GetOrCreateSetting<string>("DeepSeek", "Dict", string.Empty);
            if (string.IsNullOrEmpty(dictStr))
            {
                _useDict = false;
                _fullDictStr = string.Empty;
            }
            else
            {
                try
                {
                    _dict = new Dictionary<string, List<string>>();
                    var dictJObj = JSON.Parse(dictStr);
                    foreach (var item in dictJObj)
                    {
                        try
                        {
                            var vArr = JSON.Parse(item.Value.ToString()).AsArray;
                            List<string> vList;
                            if (vArr.Count <= 0)
                            {
                                throw new Exception();
                            }
                            else if (vArr.Count == 1)
                            {
                                vList = new List<string> { JsonHelper.Unescape(vArr[0].ToString().Trim('\"')), string.Empty };
                            }
                            else
                            {
                                vList = new List<string> { JsonHelper.Unescape(vArr[0].ToString().Trim('\"')),
                                    JsonHelper.Unescape(vArr[1].ToString().Trim('\"')) };
                            }
                            _dict.Add(JsonHelper.Unescape(item.Key.Trim('\"')), vList);
                        }
                        catch
                        {
                            _dict.Add(JsonHelper.Unescape(item.Key.Trim('\"')),
                                new List<string> { JsonHelper.Unescape(item.Value.ToString().Trim('\"')), string.Empty });
                        }
                    }
                    if (_dict.Count == 0)
                    {
                        _useDict = false;
                        _fullDictStr = string.Empty;
                    }
                    else
                    {
                        var dictStrings = GetDictStringList(_dict);
                        _fullDictStr = s_dictBaseStr + string.Join("\n", dictStrings.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    XuaLogger.AutoTranslator.Warn(ex, $"Failed to parse dict string: {dictStr}");
                    _useDict = false;
                    _fullDictStr = string.Empty;
                }
            }
            #endregion
            if (!bool.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "SplitByLine", "false"), out _splitByLine)) { _splitByLine = false; }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "MaxConcurrency", "1"), out _maxConcurrency) || _maxConcurrency < 1) { _maxConcurrency = 1; }
            if (ServicePointManager.DefaultConnectionLimit < _maxConcurrency)
            {
                XuaLogger.AutoTranslator.Info($"Setting ServicePointManager.DefaultConnectionLimit to {_maxConcurrency}");
                ServicePointManager.DefaultConnectionLimit = _maxConcurrency;
            }
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
                XuaLogger.AutoTranslator.Info($"Setting ThreadPool min threads to {Math.Max(minWorkerThreads, _minThreadCount)} " +
                    $"and max threads to {Math.Max(minCompletionPortThreads, _minThreadCount)}");
                ThreadPool.SetMinThreads(Math.Max(minWorkerThreads, _minThreadCount), Math.Max(minCompletionPortThreads, _minThreadCount));
                ThreadPool.SetMaxThreads(Math.Max(maxWorkerThreads, _maxThreadCount), Math.Max(maxCompletionPortThreads, _maxThreadCount));
            }
            if (!bool.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "Debug", "false"), out _debug)) { _debug = false; }
        }
    }
}
