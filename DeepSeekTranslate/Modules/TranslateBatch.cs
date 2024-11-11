using DeepSeekTranslate.Helpers;
using DeepSeekTranslate.Models;
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

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
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
            var prompt = RequestHelper.MakeRequestStr(new List<PromptMessage>
            {
                new PromptMessage("system", GetSysPromptStr()),
                new PromptMessage("user", _trUserExampleStr),
                new PromptMessage("assistant", _trAssistantExampleStr),
                new PromptMessage("user", userTrPrompt)
            }, _model, _temperature, _maxTokens);
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
                    if (_splitByLine)
                    {
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
                    else
                    {
                        if (lineNumberDict.ContainsKey(i))
                        {
                            if (DEBUG) { Console.WriteLine($"TranslateBatch: i={{{i}}}, lineNumberDict[i]={{{lineNumberDict[i]}}}, contents[lineNumberDict[i].ToString()]={{{contents[lineNumberDict[i].ToString()].ToString().Trim('\"')}}}"); }
                            translatedTextBuilders[textPos].Append(contents[lineNumberDict[i].ToString()].ToString().Trim('\"'));
                            if (DEBUG) { Console.WriteLine($"TranslateBatch: i={{{i}}}, textPos={{{textPos}}}, translatedTextBuilders[textPos]={{{translatedTextBuilders[textPos].ToString()}}}"); }
                        }
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
