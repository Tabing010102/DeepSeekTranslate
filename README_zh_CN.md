# DeepSeekTranslate

适用于 XUnity.AutoTranslator 的 DeepSeek API 翻译插件  
[README](README.md) | [简体中文说明](README_zh_CN.md)  

## 警告

尚未完全测试。使用时请自行承担风险

## 注意事项

- 在不使用线程池 (`UseThreadPool`) 时，使用 DeepSeek 在线 API 将调用阻塞的 ReadToEnd 方法来读取响应流，这可能导致应用程序卡死  
  为了改善体验，可以将 `CoroutineWaitCountBeforeRead` 设置为大于 0 的值，以等待指定数量的协程（可能每帧调用一次），在等待期间服务器可能能够完全生成响应
- 或可以尝试使用 Nginx 作为反向代理来缓冲 DeepSeek 的响应，然后一次性发送给客户端  
  在这种情况下，可以将 `Endpoint` 设置为 Nginx 服务器地址，并将 `CoroutineWaitCountBeforeRead` 设置为 `0`
- 使用线程池时，不存在以上问题

## 配置示例

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
MaxRetries=1
UseThreadPool=True
MinThreadCount=
MaxThreadCount=
Debug=False
```

## 配置项

- **Endpoint**：API URL
  - 默认值：`https://api.deepseek.com/chat/completions`

- **ApiKey**：API 密钥
  - 默认值：`YOUR_API_KEY_HERE`

- **Model**：传递给API的`model`参数
  - 默认值：`deepseek-chat`

- **Temperature**：
  - 默认值：`1.3`

- **MaxTokensMode**：
  - `Static`（默认）：静态`max_tokens`
  - `Dynamic`：动态`max_tokens`，根据输入文本长度动态调整

- **StaticMaxTokens**：
  - 默认值：`1024`
  - 说明：当 `MaxTokensMode` 为 `Static` 时的发送的`max_tokens`

- **DynamicMaxTokensMultiplier**：
  - 默认值：`1.5`
  - 说明：当 `MaxTokensMode` 为 `Dynamic` 时，`max_tokens`设置为构建的未翻译json字符串字符数的倍数

- **DictMode**：
  - `None`（默认）：不使用字典
  - `Full`：使用完整字典
  - `MatchOriginalText`：使用匹配原始文本的字典
  - 说明：
    - 使用官方 API 且启用字典时，推荐使用 `Full` 模式，可以最大化利用缓存，降低费用

- **Dict**：
  - 默认值：空字符串
  - 说明：
    - 翻译字典，必须为空或合法的Json格式，解析失败将会视为空
    - 字典格式`{"src":["dst","info"]}`
    - 其中`info`不存在可以写成`{"src":["dst"]}`或者`{"src":"dst"}`

- **AddEndingAssistantPrompt**：
  - `True`（默认）：添加结束助手提示，可能降低模型不回答的概率，但会增加未命中缓存的token数
  - `False`：不添加结束助手提示，节省token

- **SplitByLine**：
  - `False`（默认）：不按行分割原始文本
  - `True`：按行分割原始文本

- **MaxConcurrency**：
  - 默认值：`1`
  - 说明：最大并发数

- **BatchTranslate**：
  - `False`（默认）：禁用批量翻译
  - `True`：启用批量翻译

- **MaxTranslationsPerRequest**：
  - 默认值：`1`
  - 说明：每次请求的最大翻译数量，仅在 `BatchTranslate` 为 `True` 时有效

- **CoroutineWaitCountBeforeRead**：
  - 默认值：`150`
  - 说明：读取响应流前的协程等待计数，仅在 `UseThreadPool` 为 `False` 时有效

- **MaxRetries**：
  - 默认值：`1`
  - 说明：请求失败时的最大重试次数，为空或解析失败时使用 `0`。遇到429/503错误时立即停止重试

- **UseThreadPool**：
  - `True`（默认）：使用线程池
  - `False`：不使用线程池

- **MinThreadCount**：
  - 默认值：空
  - 说明：线程池的最小线程数，为空或解析失败时使用 `Environment.ProcessorCount * 2`

- **MaxThreadCount**：
  - 默认值：空
  - 说明：线程池的最大线程数，为空或解析失败时使用 `Environment.ProcessorCount * 4`

- **Debug**：
  - `False`（默认）：禁用调试模式
  - `True`：启用调试模式
