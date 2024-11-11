using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.AutoTranslator.Plugin.Core.Utilities;
using XUnity.Common.Logging;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
        public IEnumerator Translate(ITranslationContext context)
        {
            // batch translate force use thread pool
            if (_batchTranslate && _useThreadPool)
            {
                if (_debug)
                {
                    XuaLogger.AutoTranslator.Debug($"Translate: context.UntranslatedTexts={{{string.Join(", ", context.UntranslatedTexts)}}}");
                }
                var untranslatedTexts = context.UntranslatedTexts;
                var lines = new List<string>();
                var textLineDict = new Dictionary<int, int>(untranslatedTexts.Length);
                if (_splitByLine)
                {
                    // split text into lines
                    for (int i = 0; i < untranslatedTexts.Length; i++)
                    {
                        textLineDict.Add(lines.Count, i);
                        lines.AddRange(untranslatedTexts[i].Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));
                    }
                }
                else
                {
                    for (int i = 0; i < untranslatedTexts.Length; i++)
                    {
                        textLineDict.Add(lines.Count, i);
                        lines.Add(untranslatedTexts[i]);
                    }
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
                        trJsonStrBuilder.Append($"\"{validLineCount}\":\"{lines[i]}\",");
                        lineNumberDict.Add(i, validLineCount);
                        validLineCount++;
                    }
                }
                trJsonStrBuilder.Remove(trJsonStrBuilder.Length - 1, 1);
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
                    if (_splitByLine) { translatedTexts[i] = translatedTextBuilders[i].ToString().TrimEnd('\r', '\n'); }
                    else { translatedTexts[i] = JsonHelper.Unescape(translatedTextBuilders[i].ToString()); }
                }

                if (_debug)
                {
                    XuaLogger.AutoTranslator.Debug($"Translate: translatedTexts={{{string.Join(", ", translatedTexts)}}}");
                }
                context.Complete(translatedTexts);
            }
            // per line translate
            else
            {
                var untranslatedText = context.UntranslatedText;
                string[] lines;
                if (_splitByLine)
                {
                    // split text into lines
                    lines = untranslatedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                }
                else
                {
                    lines = new string[] { untranslatedText };
                }
                var translatedTextBuilder = new StringBuilder();

                foreach (var line in lines)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        var translateLineCoroutine = TranslateSingle(line, translatedTextBuilder);
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

                string translatedText;
                if (_splitByLine) { translatedText = translatedTextBuilder.ToString().TrimEnd('\r', '\n'); }
                else { translatedText = JsonHelper.Unescape(translatedTextBuilder.ToString()); }
                context.Complete(translatedText);
            }
        }
    }
}
