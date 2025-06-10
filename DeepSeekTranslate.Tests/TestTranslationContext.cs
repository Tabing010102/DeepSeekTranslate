using System;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;

namespace DeepSeekTranslate.Tests
{
    public class TestTranslationContext : ITranslationContext
    {
        public string[] UntranslatedTexts { get; }
        public string UntranslatedText => UntranslatedTexts[0];
        public string SourceLanguage { get; }
        public string DestinationLanguage { get; }
        public bool IsDone { get; private set; }
        public string TranslatedText { get; private set; }
        public string[] TranslatedTexts { get; private set; }

        public TestTranslationContext(string text)
        {
            UntranslatedTexts = new[] { text };
        }

        public TestTranslationContext(string[] texts)
        {
            UntranslatedTexts = texts;
        }

        public void Complete(string translation)
        {
            TranslatedText = translation;
            IsDone = true;
        }

        public void Complete(string[] translations)
        {
            TranslatedTexts = translations;
            if (translations != null && translations.Length > 0)
            {
                TranslatedText = translations[0];
            }
            IsDone = true;
        }

        public void Fail(string reason, Exception error)
        {
            IsDone = true;
            throw new Exception($"Translation failed: {reason}", error);
        }

        public void Fail(string reason)
        {
            IsDone = true;
            throw new Exception($"Translation failed: {reason}");
        }
    }
} 