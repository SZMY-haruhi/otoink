# otoink

语音转文字桌面浮条。名字来自 oto（音）+ ink（墨）。

## 和 Win+H 的差别

- 切窗口不会消失
- 设置（默认 AI 录入、自动标点、API Key）会记住
- 识别在本地完成；纠正用你自己的 DeepSeek / OpenAI 兼容接口
- 关掉「默认 AI 录入」时不会自动请求 AI，只能在展开的记录里点「AI 优化」

## 开发

需要 .NET 8 SDK。

```bash
dotnet test
dotnet run --project src/Otoink.App/Otoink.App.csproj
```

第一次运行会下载 SenseVoice 模型到 `%LOCALAPPDATA%\otoink\models\`。
