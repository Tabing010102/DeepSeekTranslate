using System.Collections.Generic;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
        private static readonly HashSet<string> s_supportedSrcLangs = new HashSet<string> { "en", "ja", "ko", "ru", "zh", "zh-TW" };
        private static readonly HashSet<string> s_supportedDstLangs = new HashSet<string> { "en", "ja", "ko", "ru", "zh", "zh-TW" };
        private static readonly Dictionary<string, string> s_langZhDict = new Dictionary<string, string>
        {
            { "en", "英语" },
            { "ja", "日语" },
            { "ko", "韩语" },
            { "ru", "俄语" },
            { "zh", "简中" },
            { "zh-TW", "繁中" }
        };
        private static readonly Dictionary<string, string> s_langShortZhDict = new Dictionary<string, string>
        {
            { "en", "英" },
            { "ja", "日" },
            { "ko", "韩" },
            { "ru", "俄" },
            { "zh", "中" },
            { "zh-TW", "中" }
        };
        private static readonly Dictionary<string, string> s_trExampleDict = new Dictionary<string, string>
        {
            { "en", "Love is the flame in the depth of the soul, warm and never extinguished." },
            { "ja", "愛は魂の深淵にある炎で、暖かくて永遠に消えない。" },
            { "ko", "사랑은 영혼 깊숙이 타오르는 불꽃이며, 따뜻하고 영원히 꺼지지 않는다." },
            { "ru", "Любовь - это пламя в глубине души, тёплое и никогда не угасающее." },
            { "zh", "爱情是灵魂深处的火焰，温暖且永不熄灭。" },
            { "zh-TW", "愛情是靈魂深處的火焰，溫暖且永不熄滅。" }
        };
    }
}
