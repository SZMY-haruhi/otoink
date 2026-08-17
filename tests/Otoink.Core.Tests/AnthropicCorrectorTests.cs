using System.Net;
using System.Text;
using Otoink.Core;
using Otoink.Core.Ai;

public class AnthropicCorrectorTests
{
    [Fact]
    public async Task CorrectAsync_posts_messages_and_reads_text()
    {
        HttpRequestMessage? seen = null;
        var handler = new StubHandler(async request =>
        {
            seen = request;
            var json = """{"content":[{"type":"text","text":"你好，世界。"}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        var http = new HttpClient(handler);
        var corrector = new AnthropicCorrector(
            http,
            () => new AppSettings
            {
                ApiBaseUrl = "https://api.anthropic.com",
                ApiKey = "sk-ant-test",
                ApiModel = "claude-sonnet-4-5",
                ApiProvider = ApiProvider.Anthropic
            });

        var text = await corrector.CorrectAsync("你好世界", CancellationToken.None);

        Assert.Equal("你好，世界。", text);
        Assert.NotNull(seen);
        Assert.Equal(HttpMethod.Post, seen!.Method);
        Assert.Equal("/v1/messages", seen.RequestUri!.AbsolutePath);
        Assert.Equal("sk-ant-test", seen.Headers.GetValues("x-api-key").Single());
        Assert.Equal(AnthropicCorrector.ApiVersion, seen.Headers.GetValues("anthropic-version").Single());
        var body = await seen.Content!.ReadAsStringAsync();
        Assert.Contains("claude-sonnet-4-5", body);
        Assert.Contains("你好世界", body);
        Assert.Contains(OpenAiCompatibleCorrector.SystemPrompt, body);
    }

    [Theory]
    [InlineData("https://api.anthropic.com", "/v1/messages")]
    [InlineData("https://api.anthropic.com/v1", "/v1/messages")]
    public void MessagesUri_appends_v1_messages(string root, string path)
    {
        Assert.Equal(path, AnthropicCorrector.MessagesUri(root).AbsolutePath);
    }

    [Fact]
    public async Task LlmCorrector_routes_to_anthropic()
    {
        var openAi = new CountingCorrector("openai");
        var anthropic = new CountingCorrector("anthropic");
        var llm = new LlmCorrector(openAi, anthropic, () => new AppSettings { ApiProvider = ApiProvider.Anthropic });
        var text = await llm.CorrectAsync("hi", CancellationToken.None);
        Assert.Equal("anthropic", text);
        Assert.Equal(0, openAi.Calls);
        Assert.Equal(1, anthropic.Calls);
    }

    [Fact]
    public async Task LlmCorrector_routes_probe_to_anthropic()
    {
        var openAi = new CountingCorrector("openai");
        var anthropic = new CountingCorrector("anthropic");
        var llm = new LlmCorrector(openAi, anthropic, () => new AppSettings { ApiProvider = ApiProvider.Anthropic });
        await llm.ProbeAsync(CancellationToken.None);
        Assert.Equal(0, openAi.Calls);
        Assert.Equal(1, anthropic.Calls);
    }

    [Fact]
    public async Task ProbeAsync_posts_tiny_message()
    {
        HttpRequestMessage? seen = null;
        var handler = new StubHandler(async request =>
        {
            seen = request;
            var json = """{"content":[{"type":"text","text":"OK"}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        var http = new HttpClient(handler);
        var corrector = new AnthropicCorrector(
            http,
            () => new AppSettings
            {
                ApiBaseUrl = "https://api.anthropic.com",
                ApiKey = "sk-ant-test",
                ApiModel = "claude-sonnet-4-5"
            });

        await corrector.ProbeAsync(CancellationToken.None);
        var body = await seen!.Content!.ReadAsStringAsync();
        Assert.Equal("/v1/messages", seen.RequestUri!.AbsolutePath);
        Assert.Contains("\"max_tokens\":8", body);
        Assert.Contains("ping", body);
        Assert.DoesNotContain(OpenAiCompatibleCorrector.SystemPrompt, body);
    }

    private sealed class CountingCorrector : IAiCorrector
    {
        private readonly string _name;
        public int Calls { get; private set; }
        public CountingCorrector(string name) => _name = name;
        public Task<string> CorrectAsync(string rawText, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_name);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _fn;
        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> fn) => _fn = fn;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _fn(request);
    }
}
