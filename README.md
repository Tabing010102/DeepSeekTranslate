# DeepSeekTranslate

DeepSeek API Translator for XUnity.AutoTranslator  
[README](README.md) | [简体中文说明](README_zh_CN.md)  

## Warning

Not fully tested. Use at your own risk.

## Note

- When using DeepSeek online api without using ThreadPool, the blocking ReadToEnd method is used to read the response stream. This may cause the application to freeze.  
To improve the experience, you can set `CoroutineWaitCountBeforeRead` to a value greater than 0, which will wait for the specified number of coroutines (which may be called once per frame) before block-reading the response stream and hope that the server fully generates the response during the wait.
- Or you can try to use Nginx as a reverse proxy which may buffers the response from DeepSeek and then sends it to the client in one go.  
In this case, you can set `Endpoint` to your Nginx server address and set `CoroutineWaitCountBeforeRead` to `0`.
- There is no such problem when using ThreadPool.

## Config example

```ini
[DeepSeek]
Endpoint=https://api.deepseek.com/chat/completions
ApiKey=sk-xxxxxxx
Model=deepseek-chat
Temperature=1.3
MaxTokensMode=Dynamic
StaticMaxTokens=1024
DynamicMaxTokensMultiplier=1.5
DictMode=Full
Dict={"想太":["想太","男主人公"],"ダイヤ":["戴亚","女"]}
AddEndingAssistantPrompt=True
SplitByLine=False
MaxConcurrency=4
BatchTranslate=True
MaxTranslationsPerRequest=5
CoroutineWaitCountBeforeRead=150
MaxRetries=2
UseThreadPool=True
MinThreadCount=
MaxThreadCount=
Debug=False
```

## Config options

- **Endpoint**: API URL
  - Default: `https://api.deepseek.com/chat/completions`

- **ApiKey**: API key
  - Default: `YOUR_API_KEY_HERE`

- **Model**: The `model` parameter passed to the API
  - Default: `deepseek-chat`

- **Temperature**:
  - Default: `1.3`

- **MaxTokensMode**:
  - `Static` (default): Static `max_tokens`
  - `Dynamic`: Dynamic `max_tokens`, adjusted based on the length of the input text

- **StaticMaxTokens**:
  - Default: `1024`
  - Description: The `max_tokens` sent when `MaxTokensMode` is `Static`

- **DynamicMaxTokensMultiplier**:
  - Default: `1.5`
  - Description: When `MaxTokensMode` is `Dynamic`, `max_tokens` is set to a multiple of the character count of the constructed untranslated JSON string

- **DictMode**:
  - `None` (default): Do not use dictionary
  - `Full`: Use the full dictionary
  - `MatchOriginalText`: Use a dictionary that matches the original text
  - Description:
    - When using the official API and enabling the dictionary, it is recommended to use `Full` mode to maximize cache utilization and reduce costs

- **Dict**:
  - Default: Empty string
  - Description:
    - Translation dictionary, must be empty or in valid JSON format, parsing failure will fall back to empty
    - Dictionary format `{"src":["dst","info"]}`
    - If `info` does not exist, it can be written as `{"src":["dst"]}` or `{"src":"dst"}`

- **AddEndingAssistantPrompt**:
  - `True` (default): Add an ending assistant prompt, which may reduce the probability of the model refusing to respond, but will increase cache-missed tokens
  - `False`: Do not add an ending assistant prompt, saving tokens

- **SplitByLine**:
  - `False` (default): Do not split the original text by line
  - `True`: Split the original text by line

- **MaxConcurrency**:
  - Default: `1`
  - Description: Maximum concurrency

- **BatchTranslate**:
  - `False` (default): Disable batch translation
  - `True`: Enable batch translation

- **MaxTranslationsPerRequest**:
  - Default: `1`
  - Description: Maximum number of translations per request, only effective when `BatchTranslate` is `True`

- **CoroutineWaitCountBeforeRead**:
  - Default: `150`
  - Description: Coroutine wait count before reading the response stream, only effective when `UseThreadPool` is `False`

- **MaxRetries**:
  - Default: `2`
  - Description: Maximum retry attempts when request fails. Stops retrying immediately when encountering 429/503 error

- **UseThreadPool**:
  - `True` (default): Use thread pool
  - `False`: Do not use thread pool

- **MinThreadCount**:
  - Default: Empty
  - Description: Minimum thread count for the thread pool, if empty or parsing fails, `Environment.ProcessorCount * 2` is used

- **MaxThreadCount**:
  - Default: Empty
  - Description: Maximum thread count for the thread pool, if empty or parsing fails, `Environment.ProcessorCount * 4` is used

- **Debug**:
  - `False` (default): Disable debug mode
  - `True`: Enable debug mode
