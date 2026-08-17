using System.Net;
using System.Text;
using Otoink.Core;
using Otoink.Core.Ai;

public class OpenAiCompatibleCorrectorTests
{
    [Fact]
    public async Task CorrectAsync_posts_chat_completions_and_reads_content()
    {
        HttpRequestMessage? seen = null;
        var handler = new StubHandler(async request =>
        {
            seen = request;
            var json = """{"choices":[{"message":{"content":"你好，世界。"}}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var corrector = new OpenAiCompatibleCorrector(
            http,
            () => new AppSettings
            {
                ApiBaseUrl = "https://api.deepseek.com/v1",
                ApiKey = "sk-test",
                ApiModel = "deepseek-chat"
            });

        var text = await corrector.CorrectAsync("你好世界", CancellationToken.None);

        Assert.Equal("你好，世界。", text);
        Assert.NotNull(seen);
        Assert.Equal(HttpMethod.Post, seen!.Method);
        Assert.Equal("/v1/chat/completions", seen.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer sk-test", seen.Headers.Authorization!.ToString());
        var body = await seen.Content!.ReadAsStringAsync();
        Assert.Contains("deepseek-chat", body);
        Assert.Contains("你好世界", body);
        Assert.Contains(OpenAiCompatibleCorrector.SystemPrompt, body);
    }

    [Fact]
    public async Task CorrectAsync_uses_custom_prompt_when_set()
    {
        HttpRequestMessage? seen = null;
        var handler = new StubHandler(async request =>
        {
            seen = request;
            var json = """{"choices":[{"message":{"content":"OK"}}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        var http = new HttpClient(handler);
        var corrector = new OpenAiCompatibleCorrector(
            http,
            () => new AppSettings
            {
                ApiBaseUrl = "https://api.deepseek.com/v1",
                ApiKey = "sk-test",
                ApiModel = "deepseek-chat",
                ApiPrompt = "改成书面语，不要解释。"
            });

        await corrector.CorrectAsync("喂你好", CancellationToken.None);
        var body = await seen!.Content!.ReadAsStringAsync();
        Assert.Contains("改成书面语，不要解释。", body);
        Assert.DoesNotContain(OpenAiCompatibleCorrector.SystemPrompt, body);
    }

    [Fact]
    public async Task ProbeAsync_posts_tiny_completion()
    {
        HttpRequestMessage? seen = null;
        var handler = new StubHandler(async request =>
        {
            seen = request;
            var json = """{"choices":[{"message":{"content":"OK"}}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        var http = new HttpClient(handler);
        var corrector = new OpenAiCompatibleCorrector(
            http,
            () => new AppSettings
            {
                ApiBaseUrl = "https://api.deepseek.com/v1",
                ApiKey = "sk-test",
                ApiModel = "deepseek-chat"
            });

        await corrector.ProbeAsync(CancellationToken.None);

        var body = await seen!.Content!.ReadAsStringAsync();
        Assert.Equal("/v1/chat/completions", seen.RequestUri!.AbsolutePath);
        Assert.Contains("\"max_tokens\":8", body);
        Assert.Contains("ping", body);
        Assert.DoesNotContain(OpenAiCompatibleCorrector.SystemPrompt, body);
    }

    [Fact]
    public async Task ProbeAsync_surfaces_api_error_message()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                """{"error":{"message":"Incorrect API key"}}""",
                Encoding.UTF8,
                "application/json")
        }));
        var http = new HttpClient(handler);
        var corrector = new OpenAiCompatibleCorrector(
            http,
            () => new AppSettings
            {
                ApiBaseUrl = "https://api.deepseek.com/v1",
                ApiKey = "bad",
                ApiModel = "deepseek-chat"
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            corrector.ProbeAsync(CancellationToken.None));
        Assert.Contains("401", ex.Message);
        Assert.Contains("Incorrect API key", ex.Message);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _fn;
        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _fn(request);
    }
}
