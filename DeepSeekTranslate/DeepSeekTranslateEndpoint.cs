using System.Collections.Generic;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;

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
        private string _model;
        private double _temperature;
        private int _maxTokens;
        private bool _useDict;
        private string _dictMode;
        private Dictionary<string, List<string>> _dict;
        private bool _splitByLine;
        private int _maxConcurrency;
        private bool _batchTranslate;
        private int _maxTranslationsPerRequest;
        private int _coroutineWaitCountBeforeRead;
        private bool _useThreadPool;
        private int _minThreadCount;
        private int _maxThreadCount;

        private string _fullDictStr;

        public string Id => "DeepSeekTranslate";

        public string FriendlyName => Id;

        public int MaxConcurrency => _maxConcurrency;

        public int MaxTranslationsPerRequest => _maxTranslationsPerRequest;
    }
}
