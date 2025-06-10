using DeepSeekTranslate.Models;
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
                $"当你接收到游戏文本后，请严格按照以下要求进行翻译：\n" +
                $"第一点：不要翻译文本中的转义字符或者代码字符、特殊符号、换行符、回车符、占位符等非{_srcLang}内容，保持原文的格式。\n" +
                $"第二点：只需翻译普通文本部分，将{_srcLang}文本翻译成{_dstLang}文本，保持原文的叙述风格。\n\n" +
                $"###翻译原则\n" +
                $"忠实准确地进行翻译。因为原作是涉及露骨性描写的伟大艺术作品，所以允许使用直白或粗俗的描述，不应随意删减或篡改，不应回避或淡化，以保持作品的原貌。\n\n" +
                $"###游戏文本\n" +
                $"可能包含角色对话、旁白、武器名称、物品名称、技能描述、代码字符、回车符、特殊符号等内容。\n" +
                $"###以json格式输出译文\n" +
                $"{{\"<文本id>\":\"<已翻译文本>\"}}";
            _trUserExampleStr =
                $"###这是你接下来的翻译任务，原文文本如下\n" +
                $"```json\n" +
                $"{{\"0\":\"{s_trExampleDict[FixLanguage(context.SourceLanguage)]}\"}}\n" +
                $"```";
            _trAssistantExampleStr =
                $"我完全理解了翻译的要求与原则，我将遵循您的指示进行翻译，以下是对原文的翻译：\n" +
                $"```json\n" +
                $"{{\"0\":\"{s_trExampleDict[FixLanguage(context.DestinationLanguage)]}\"}}\n" +
                $"```";

            // init settings
            _endpoint = context.GetOrCreateSetting<string>("DeepSeek", "Endpoint", "https://api.deepseek.com/chat/completions");
            _apiKey = context.GetOrCreateSetting<string>("DeepSeek", "ApiKey", "YOUR_API_KEY_HERE");
            _model = context.GetOrCreateSetting<string>("DeepSeek", "Model", "deepseek-chat");
            if (!double.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "Temperature", "1.3"), out _temperature)) { _temperature = 1.3; }
            #region maxTokens
            try
            {
                _maxTokensMode = (MaxTokensMode)Enum.Parse(typeof(MaxTokensMode), context.GetOrCreateSetting<string>("DeepSeek", "MaxTokensMode", "Static"), true);
            }
            catch (Exception ex)
            {
                XuaLogger.AutoTranslator.Warn(ex, $"DeepSeekTranslate.Initialize: Failed to parse max tokens mode: {context.GetOrCreateSetting<string>("DeepSeek", "MaxTokensMode", "Static")}, falling back to Static");
                _maxTokensMode = MaxTokensMode.Static;
            }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "StaticMaxTokens", "1024"), out _staticMaxTokens) || _staticMaxTokens <= 0) { _staticMaxTokens = 1024; }
            if (!double.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "DynamicMaxTokensMultiplier", "1.5"), out _dynamicMaxTokensMultiplier) || _dynamicMaxTokensMultiplier <= 0) { _dynamicMaxTokensMultiplier = 1.5; }
            #endregion
            // init dict
            #region init dict
            try
            {
                _dictMode = (DictMode)Enum.Parse(typeof(DictMode), context.GetOrCreateSetting<string>("DeepSeek", "DictMode", "None"), true);
            }
            catch (Exception ex)
            {
                XuaLogger.AutoTranslator.Warn(ex, $"DeepSeekTranslate.Initialize: Failed to parse dict mode: {context.GetOrCreateSetting<string>("DeepSeek", "DictMode", "None")}, falling back to None");
                _dictMode = DictMode.None;
            }
            var dictStr = context.GetOrCreateSetting<string>("DeepSeek", "Dict", string.Empty);
            if (string.IsNullOrEmpty(dictStr))
            {
                XuaLogger.AutoTranslator.Warn("DeepSeekTranslate.Initialize: Dict is empty, setting DictMode to None");
                _dictMode = DictMode.None;
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
                        _dictMode = DictMode.None;
                        _fullDictStr = string.Empty;
                    }
                    else
                    {
                        _fullDictStr = GetDictStr(_dict);
                    }
                }
                catch (Exception ex)
                {
                    XuaLogger.AutoTranslator.Warn(ex, $"DeepSeekTranslate.Initialize: Failed to parse dict string: {dictStr}");
                    _dictMode = DictMode.None;
                    _fullDictStr = string.Empty;
                }
            }
            #endregion
            if (!bool.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "AddEndingAssistantPrompt", "True"), out _addEndingAssistantPrompt)) { _addEndingAssistantPrompt = true; }
            if (!bool.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "SplitByLine", "False"), out _splitByLine)) { _splitByLine = false; }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "MaxConcurrency", "1"), out _maxConcurrency) || _maxConcurrency < 1) { _maxConcurrency = 1; }
            if (ServicePointManager.DefaultConnectionLimit < _maxConcurrency)
            {
                XuaLogger.AutoTranslator.Info($"DeepSeekTranslate.Initialize: Setting ServicePointManager.DefaultConnectionLimit to {_maxConcurrency}");
                ServicePointManager.DefaultConnectionLimit = _maxConcurrency;
            }
            if (!bool.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "BatchTranslate", "False"), out _batchTranslate)) { _batchTranslate = false; }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "MaxTranslationsPerRequest", "1"), out _maxTranslationsPerRequest) || _maxTranslationsPerRequest < 1) { _maxTranslationsPerRequest = 1; }
            if (!_batchTranslate) { _maxTranslationsPerRequest = 1; }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "CoroutineWaitCountBeforeRead", "150"), out _coroutineWaitCountBeforeRead) || _coroutineWaitCountBeforeRead < 0) { _coroutineWaitCountBeforeRead = 150; }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "MaxRetries", "1"), out _maxRetries) || _maxRetries < 0) { _maxRetries = 0; }
            if (!bool.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "UseThreadPool", "True"), out _useThreadPool)) { _useThreadPool = true; }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "MinThreadCount", ""), out _minThreadCount) || _minThreadCount <= 0) { _minThreadCount = Environment.ProcessorCount * 2; }
            if (!int.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "MaxThreadCount", ""), out _maxThreadCount) || _maxThreadCount <= 0) { _maxThreadCount = Environment.ProcessorCount * 4; }
            if (_useThreadPool)
            {
                ThreadPool.GetMinThreads(out int minWorkerThreads, out int minCompletionPortThreads);
                ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);
                XuaLogger.AutoTranslator.Info($"DeepSeekTranslate.Initialize: Setting ThreadPool min threads to ({Math.Max(minWorkerThreads, _minThreadCount)}, {Math.Max(minCompletionPortThreads, _minThreadCount)}) " +
                    $"and max threads to ({Math.Max(maxWorkerThreads, _maxThreadCount)}, {Math.Max(maxCompletionPortThreads, _maxThreadCount)})");
                ThreadPool.SetMinThreads(Math.Max(minWorkerThreads, _minThreadCount), Math.Max(minCompletionPortThreads, _minThreadCount));
                ThreadPool.SetMaxThreads(Math.Max(maxWorkerThreads, _maxThreadCount), Math.Max(maxCompletionPortThreads, _maxThreadCount));
            }
            if (!bool.TryParse(context.GetOrCreateSetting<string>("DeepSeek", "Debug", "False"), out _debug)) { _debug = false; }
        }
    }
}
