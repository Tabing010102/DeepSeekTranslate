using XUnity.AutoTranslator.Plugin.Core.Endpoints;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
        private string GetSysPromptStr()
        {
            if (!_useDict || string.IsNullOrEmpty(_fullDictStr))
            {
                return _sysPromptStr;
            }
            else
            {
                return _sysPromptStr + _fullDictStr;
            }
        }
    }
}
