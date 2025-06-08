using DeepSeekTranslate.Models;
using DeepSeekTranslate.Modules.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.Common.Logging;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
        private IEnumerator TranslateBatch(string trJsonStr, int lineCount, int totalLineCount, Dictionary<int, int> lineNumberDict,
            Dictionary<int, int> textLineDict, StringBuilder[] translatedTextBuilders)
        {
            int retryCount = 0;

            while (retryCount <= _maxRetries)
            {
                if (_debug)
                {
                    var lineNumberDictStr = string.Join(", ", lineNumberDict.Select(kv => $"{kv.Key}->{kv.Value}").ToArray());
                    var textLineDictStr = string.Join(", ", textLineDict.Select(kv => $"{kv.Key}->{kv.Value}").ToArray());
                    XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.TranslateBatch: trJsonStr={{{trJsonStr}}}, lineCount={{{lineCount}}}, totalLineCount={{{totalLineCount}}}, " +
                        $"lineNumberDict={{{lineNumberDictStr}}}, textLineDict={{{textLineDictStr}}}");
                }
                // create prompt
                var userTrPrompt = $"###这是你接下来的翻译任务，原文文本如下\n" +
                    $"```json\n" +
                    $"{{{trJsonStr}}}\n" +
                    $"```";
                var prompt = MakeRequestStr(new List<PromptMessage>
                {
                    new PromptMessage("system", GetSysPromptStr(trJsonStr)),
                    new PromptMessage("user", _trUserExampleStr),
                    new PromptMessage("assistant", _trAssistantExampleStr),
                    new PromptMessage("user", userTrPrompt)
                }, GetMaxTokens(trJsonStr));
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
                if (_debug) { XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.TranslateBatch: request filled"); }

                bool isCompleted = false;
                Exception catchedException = null;
                ThreadPool.QueueUserWorkItem((state) =>
                {
                    try
                    {
                        // get response
                        string responseText;
                        if (_debug) { XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.TranslateBatch: sending request"); }
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
                        if (_debug) { XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.TranslateBatch: responseText={{{responseText}}}"); }
                        var contents = JsonResponseHelper.ParseJsonResponse(responseText, _debug);
                        if (_debug) { XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.TranslateBatch: contents.Count={{{contents.Count}}}, lineCount={{{lineCount}}}"); }
                        if (contents.Count != lineCount)
                        {
                            throw new Exception("The number of translated lines does not match the number of lines to be translated.");
                        }
                        int textPos = 0;
                        for (int i = 0; i < totalLineCount; i++)
                        {
                            if (textLineDict.ContainsKey(i)) { textPos = textLineDict[i]; }
                            if (_debug) { XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.TranslateBatch: i={{{i}}}, textPos={{{textPos}}}"); }
                            if (_splitByLine)
                            {
                                if (!lineNumberDict.ContainsKey(i))
                                {
                                    translatedTextBuilders[textPos].AppendLine();
                                }
                                else
                                {
                                    if (_debug) { XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.TranslateBatch: i={{{i}}}, lineNumberDict[i]={{{lineNumberDict[i]}}}, contents[lineNumberDict[i]]={{{contents[lineNumberDict[i].ToString()].ToString().Trim('\"')}}}"); }
                                    translatedTextBuilders[textPos].AppendLine(contents[lineNumberDict[i].ToString()].ToString().Trim('\"'));
                                    if (_debug) { XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.TranslateBatch: i={{{i}}}, textPos={{{textPos}}}, translatedTextBuilders[textPos]={{{translatedTextBuilders[textPos]}}}"); }
                                }
                            }
                            else
                            {
                                if (lineNumberDict.ContainsKey(i))
                                {
                                    if (_debug) { XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.TranslateBatch: i={{{i}}}, lineNumberDict[i]={{{lineNumberDict[i]}}}, contents[lineNumberDict[i].ToString()]={{{contents[lineNumberDict[i].ToString()].ToString().Trim('\"')}}}"); }
                                    translatedTextBuilders[textPos].Append(contents[lineNumberDict[i].ToString()].ToString().Trim('\"'));
                                    if (_debug) { XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.TranslateBatch: i={{{i}}}, textPos={{{textPos}}}, translatedTextBuilders[textPos]={{{translatedTextBuilders[textPos]}}}"); }
                                }
                            }
                        }

                        isCompleted = true;
                        if (_debug) { XuaLogger.AutoTranslator.Debug($"DeepSeekTranslate.TranslateBatch: translatedTexts={{{string.Join(", ", translatedTextBuilders.Select(tb => tb.ToString()).ToArray())}}}"); }
                    }
                    catch (Exception e)
                    {
                        catchedException = e;
                    }
                });

                while (!isCompleted)
                {
                    if (catchedException != null)
                    {
                        // check if it's 429 or 503
                        if (catchedException is WebException webEx && webEx.Response is HttpWebResponse httpResponse &&
                            (httpResponse.StatusCode == (HttpStatusCode)429 || httpResponse.StatusCode == (HttpStatusCode)503))
                        {
                            XuaLogger.AutoTranslator.Warn($"TranslateBatch: Got code {httpResponse.StatusCode}, stopping retries");
                            throw catchedException;
                        }

                        if (retryCount >= _maxRetries)
                        {
                            XuaLogger.AutoTranslator.Warn($"TranslateBatch: Maximum retry attempts ({_maxRetries}) exceeded, rethrowing exception");
                            throw catchedException;
                        }

                        XuaLogger.AutoTranslator.Warn(catchedException, $"TranslateBatch: Attempt {retryCount + 1} / {_maxRetries} failed, retrying...");
                        retryCount++;
                        break; // break out of while loop to retry
                    }
                    yield return null;
                }

                if (isCompleted)
                {
                    break; // success, exit retry loop
                }
            }
        }
    }
}
