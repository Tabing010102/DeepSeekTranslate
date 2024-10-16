# DeepSeekTranslate
DeepSeek API Translator for XUnity.AutoTranslator

## Warning
Not fully tested. Use at your own risk.

## Note

- When using DeepSeek online api without using ThreadPool, the blocking ReadToEnd method is used to read the response stream. This may cause the application to freeze.  
To improve the experience, you can set `CoroutineWaitCountBeforeRead` to a value greater than 0, which will wait for the specified number of coroutines (which may be called once per frame) before block-reading the response stream and hope that the server fully generates the response during the wait.
- Or you can try to use Nginx as a reverse proxy which may buffers the response from DeepSeek and then sends it to the client in one go.  
In this case, you can set `Endpoint` to your Nginx server address and set `CoroutineWaitCountBeforeRead` to `0`.

## Configuration

- **Endpoint**:
   - Default Value: `https://api.deepseek.com/chat/completions`

- **ApiKey**:
   - Default Value: `YOUR_API_KEY_HERE`

- **Model**:
   - Default Value: `deepseek-chat`

- **MaxTokens**:
   - Default Value: `1024`

- **Temperature**:
   - Default Value: `1.3`

- **MaxConcurrency**:
   - Default Value: `1`
   - Description: The maximum concurrency. If parsing fails or the value is less than 1, it defaults to 1.

- **BatchTranslate**:
   - Default Value: `false`
   - Description: Whether to enable batch translation. If parsing fails, it defaults to `false`.

- **MaxTranslationsPerRequest**:
   - Default Value: `1`
   - Description: The maximum number of translations per request, only valid when `BatchTranslate` is `true`. If parsing fails or the value is less than 1, it defaults to 1.

- **CoroutineWaitCountBeforeRead**:
   - Default Value: `150`
   - Description: The coroutine wait count before reading response stream, only valid when `UseThreadPool` is `false`. If parsing fails or the value is less than 0, it defaults to 150.

- **UseThreadPool**:
   - Default Value: `true`
   - Description: Whether to use the thread pool. If parsing fails, it defaults to `true`.

- **MinThreadCount**:
   - Default Value: `Environment.ProcessorCount * 2`
   - Description: The minimum thread count. If parsing fails or the value is less than or equal to 0, it defaults to twice the number of processors.

- **MaxThreadCount**:
   - Default Value: `Environment.ProcessorCount * 4`
   - Description: The maximum thread count. If parsing fails or the value is less than or equal to 0, it defaults to four times the number of processors.

