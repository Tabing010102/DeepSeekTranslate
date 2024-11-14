# DeepSeekTranslate

������ XUnity.AutoTranslator �� DeepSeek API ������  
[README](README.md) | [��������˵��](README_zh_CN.md)  

## ����

��δ��ȫ���ԡ�ʹ��ʱ�����ге�����

## ע������

- �ڲ�ʹ���̳߳� (`UseThreadPool`) ʱ��ʹ�� DeepSeek ���� API ������������ ReadToEnd ��������ȡ��Ӧ��������ܵ���Ӧ�ó�����  
  Ϊ�˸������飬���Խ� `CoroutineWaitCountBeforeRead` ����Ϊ���� 0 ��ֵ���Եȴ�ָ��������Э�̣�����ÿ֡����һ�Σ����ڵȴ��ڼ�����������ܹ���ȫ������Ӧ
- ����Գ���ʹ�� Nginx ��Ϊ������������� DeepSeek ����Ӧ��Ȼ��һ���Է��͸��ͻ���  
  ����������£����Խ� `Endpoint` ����Ϊ Nginx ��������ַ������ `CoroutineWaitCountBeforeRead` ����Ϊ `0`
- ʹ���̳߳�ʱ����������������

## ����ʾ��

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
Dict={"��̫":["��̫","�����˹�"],"������":["����","Ů"]}
SplitByLine=False
MaxConcurrency=4
BatchTranslate=True
MaxTranslationsPerRequest=5
CoroutineWaitCountBeforeRead=150
UseThreadPool=True
MinThreadCount=
MaxThreadCount=
Debug=False
```

## ������

- **Endpoint**��API URL
  - Ĭ��ֵ��`https://api.deepseek.com/chat/completions`

- **ApiKey**��API ��Կ
  - Ĭ��ֵ��`YOUR_API_KEY_HERE`

- **Model**�����ݸ�API��`model`����
  - Ĭ��ֵ��`deepseek-chat`

- **Temperature**��
  - Ĭ��ֵ��`1.3`

- **MaxTokensMode**��
  - `Static`��Ĭ�ϣ�����̬`max_tokens`
  - `Dynamic`����̬`max_tokens`�����������ı����ȶ�̬����

- **StaticMaxTokens**��
  - Ĭ��ֵ��`1024`
  - ˵������ `MaxTokensMode` Ϊ `Static` ʱ�ķ��͵�`max_tokens`

- **DynamicMaxTokensMultiplier**��
  - Ĭ��ֵ��`1.5`
  - ˵������ `MaxTokensMode` Ϊ `Dynamic` ʱ��`max_tokens`����Ϊ������δ����json�ַ����ַ����ı���

- **DictMode**��
  - `None`��Ĭ�ϣ�����ʹ���ֵ�
  - `Full`��ʹ�������ֵ�
  - `MatchOriginalText`��ʹ��ƥ��ԭʼ�ı����ֵ�
  - ˵����
    - ʹ�ùٷ� API �������ֵ�ʱ���Ƽ�ʹ�� `Full` ģʽ������������û��棬���ͷ���

- **Dict**��
  - Ĭ��ֵ�����ַ���
  - ˵����
    - �����ֵ䣬����Ϊ�ջ�Ϸ���Json��ʽ������ʧ�ܽ�����Ϊ��
    - �ֵ��ʽ`{"src":["dst","info"]}`
    - ����`info`�����ڿ���д��`{"src":["dst"]}`����`{"src":"dst"}`

- **SplitByLine**��
  - `False`��Ĭ�ϣ��������зָ�ԭʼ�ı�
  - `True`�����зָ�ԭʼ�ı�

- **MaxConcurrency**��
  - Ĭ��ֵ��`1`
  - ˵������󲢷���

- **BatchTranslate**��
  - `False`��Ĭ�ϣ���������������
  - `True`��������������

- **MaxTranslationsPerRequest**��
  - Ĭ��ֵ��`1`
  - ˵����ÿ���������������������� `BatchTranslate` Ϊ `True` ʱ��Ч

- **CoroutineWaitCountBeforeRead**��
  - Ĭ��ֵ��`150`
  - ˵������ȡ��Ӧ��ǰ��Э�̵ȴ����������� `UseThreadPool` Ϊ `False` ʱ��Ч

- **UseThreadPool**��
  - `True`��Ĭ�ϣ���ʹ���̳߳�
  - `False`����ʹ���̳߳�

- **MinThreadCount**��
  - Ĭ��ֵ����
  - ˵�����̳߳ص���С�߳�����Ϊ�ջ����ʧ��ʱʹ�� `Environment.ProcessorCount * 2`

- **MaxThreadCount**��
  - Ĭ��ֵ����
  - ˵�����̳߳ص�����߳�����Ϊ�ջ����ʧ��ʱʹ�� `Environment.ProcessorCount * 4`

- **Debug**��
  - `False`��Ĭ�ϣ������õ���ģʽ
  - `True`�����õ���ģʽ
