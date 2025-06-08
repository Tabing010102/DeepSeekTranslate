using DeepSeekTranslate.Models;
using DeepSeekTranslate.Modules.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;
using XUnity.Common.Logging;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
        private IEnumerator TranslateSingle(string line, StringBuilder translatedTextBuilder)
        {
            int retryCount = 0;

            while (retryCount <= _maxRetries)
            {
                // create prompt
                var userTrPrompt = $"###这是你接下来的翻译任务，原文文本如下\n" +
                    $"```json\n" +
                    $"{{\"0\": \"{line}\"}}\n" +
                    $"```";
                var prompt = MakeRequestStr(new List<PromptMessage>
                {
                    new PromptMessage("system", GetSysPromptStr(line)),
                    new PromptMessage("user", _trUserExampleStr),
                    new PromptMessage("assistant", _trAssistantExampleStr),
                    new PromptMessage("user", userTrPrompt)
                }, GetMaxTokens(line));
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

                Exception catchedException = null;
                bool isCompleted = false;

                if (!_useThreadPool)
                {
                    // execute request
                    var asyncResult = request.BeginGetResponse(null, null);
                    // wait for completion
                    while (!asyncResult.IsCompleted)
                    {
                        yield return null;
                    }

                    for (int i = 0; i < _coroutineWaitCountBeforeRead; i++)
                    {
                        yield return null;
                    }

                    try
                    {
                        string responseText;
                        using (var response = request.EndGetResponse(asyncResult))
                        {
                            using (var responseStream = response.GetResponseStream())
                            {
                                using (var reader = new StreamReader(responseStream))
                                {
                                    responseText = reader.ReadToEnd();
                                }
                            }
                        }

                        var translatedLine = JsonResponseHelper.ParseJsonResponse(responseText, _debug)["0"].ToString().Trim('\"');
                        if (_splitByLine) { translatedTextBuilder.AppendLine(translatedLine); }
                        else { translatedTextBuilder.Append(translatedLine); }

                        isCompleted = true;
                    }
                    catch (Exception ex)
                    {
                        catchedException = ex;
                    }
                }
                else
                {
                    ThreadPool.QueueUserWorkItem((state) =>
                    {
                        try
                        {
                            // get response
                            string responseText;
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
                            var translatedLine = JsonResponseHelper.ParseJsonResponse(responseText, _debug)["0"].ToString().Trim('\"');
                            if (_splitByLine) { translatedTextBuilder.AppendLine(translatedLine); }
                            else { translatedTextBuilder.Append(translatedLine); }

                            isCompleted = true;
                        }
                        catch (Exception ex)
                        {
                            catchedException = ex;
                        }
                    });

                    while (!isCompleted && catchedException == null)
                    {
                        yield return null;
                    }
                }

                if (isCompleted)
                {
                    break; // success, exit retry loop
                }

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
                        XuaLogger.AutoTranslator.Warn($"TranslateSingle: Maximum retry attempts ({_maxRetries}) exceeded, rethrowing exception");
                        throw catchedException;
                    }

                    XuaLogger.AutoTranslator.Warn(catchedException, $"TranslateBatch: Attempt {retryCount + 1} / {_maxRetries} failed, retrying...");
                    retryCount++;
                }
            }
        }
    }
}
