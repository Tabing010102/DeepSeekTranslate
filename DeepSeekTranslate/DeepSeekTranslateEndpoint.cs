using DeepSeekTranslate.Models;
using System.Collections.Generic;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
        private string _srcLangShort;
        private string _srcLang;
        private string _dstLangShort;
        private string _dstLang;
        private string _sysPromptStr;
        private string _trUserExampleStr;
        private string _trAssistantExampleStr;
        private string _fullDictStr;

        private string _endpoint;
        private string _apiKey;
        private string _model;
        private double _temperature;
        private MaxTokensMode _maxTokensMode;
        private int _staticMaxTokens;
        private double _dynamicMaxTokensMultiplier;
        private DictMode _dictMode;
        private Dictionary<string, List<string>> _dict;
        private bool _addEndingAssistantPrompt;
        private bool _splitByLine;
        private int _maxConcurrency;
        private bool _batchTranslate;
        private int _maxTranslationsPerRequest;
        private int _coroutineWaitCountBeforeRead;
        private int _maxRetries;
        private bool _useThreadPool;
        private int _minThreadCount;
        private int _maxThreadCount;
        private bool _debug;

        public string Id => "DeepSeekTranslate";

        public string FriendlyName => "DeepSeek API Translator";

        public int MaxConcurrency => _maxConcurrency;

        public int MaxTranslationsPerRequest => _maxTranslationsPerRequest;
    }
}
