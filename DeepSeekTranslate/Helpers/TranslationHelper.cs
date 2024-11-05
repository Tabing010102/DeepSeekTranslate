using System.Text;

namespace DeepSeekTranslate.Helpers
{
    public static class TranslationHelper
    {
        public static StringBuilder UnescapeTranslation(this StringBuilder translationSb, string original)
        {
            if (!original.Contains("\\r"))
            {
                translationSb.Replace("\\r", "\r");
            }
            if (!original.Contains("\\n"))
            {
                translationSb.Replace("\\n", "\n");
            }
            if (!original.Contains("\\t"))
            {
                translationSb.Replace("\\t", "\t");
            }

            return translationSb;
        }
    }
}
