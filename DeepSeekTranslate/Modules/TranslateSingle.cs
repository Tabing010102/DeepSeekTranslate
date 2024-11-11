using DeepSeekTranslate.Helpers;
using DeepSeekTranslate.Models;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;

namespace DeepSeekTranslate
{
    public partial class DeepSeekTranslateEndpoint : ITranslateEndpoint
    {
        private IEnumerator TranslateLine(string line, StringBuilder translatedTextBuilder)
        {
            // create prompt
            var userTrPrompt = $"###这是你接下来的翻译任务，原文文本如下###\n" +
                $"```json\n" +
                $"{{\"0\": \"{line}\"}}\n" +
                $"```";
            var prompt = RequestHelper.MakeRequestStr(new List<PromptMessage>
            {
                new PromptMessage("system", GetSysPromptStr()),
                new PromptMessage("user", _trUserExampleStr),
                new PromptMessage("assistant", _trAssistantExampleStr),
                new PromptMessage("user", userTrPrompt)
            }, _model, _temperature, _maxTokens);
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

            if (!_useThreadPool)
            {
                // execute request
                var asyncResult = request.BeginGetResponse(null, null);
                // wait for completion
                while (!asyncResult.IsCompleted)
                {
                    yield return null;
                }
                string responseText;
                for (int i = 0; i < _coroutineWaitCountBeforeRead; i++)
                {
                    yield return null;
                }
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

                var jsonObj = JSON.Parse(responseText);
                var respMsg = jsonObj.AsObject["choices"].AsArray[0]["message"];
                var translatedLine = JSON.Parse(respMsg["content"])["0"].ToString().Trim('\"');
                if (_splitByLine) { translatedTextBuilder.AppendLine(translatedLine); }
                else { translatedTextBuilder.Append(translatedLine); }
            }
            else
            {
                bool isCompleted = false;
                ThreadPool.QueueUserWorkItem((state) =>
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
                    var jsonObj = JSON.Parse(responseText);
                    var respMsg = jsonObj.AsObject["choices"].AsArray[0]["message"];
                    var translatedLine = JSON.Parse(respMsg["content"])["0"].ToString().Trim('\"');
                    if (_splitByLine) { translatedTextBuilder.AppendLine(translatedLine); }
                    else { translatedTextBuilder.Append(translatedLine); }

                    isCompleted = true;
                });

                while (!isCompleted)
                {
                    yield return null;
                }
            }
        }
    }
}
