using System.Net.Http.Json;
using System.Text.Json;

namespace PaCheck.Cli.Ai;

/// <summary>Provider config resolved from CLI options / environment.</summary>
public sealed record AiOptions(
    string Provider,
    string? Endpoint,
    string Model,
    string ApiKeyEnv,
    bool Send)
{
    public string? ApiKey => Environment.GetEnvironmentVariable(ApiKeyEnv);
}

/// <summary>Abstraction over an LLM backend. No network call happens unless <c>Send</c> is set.</summary>
public interface ILlmProvider
{
    string Name { get; }
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}

/// <summary>Default provider: performs no network call, echoes what would be sent (dry-run).</summary>
public sealed class DryRunProvider : ILlmProvider
{
    public string Name => "dry-run";

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct) =>
        Task.FromResult(
            "— dry run: no data left this machine —\n" +
            "Pass --send with a configured provider to get an AI response.\n\n" +
            "The prompt that WOULD be sent:\n" +
            "----------------------------------------\n" +
            $"[system]\n{systemPrompt}\n\n[user]\n{userPrompt}\n" +
            "----------------------------------------");
}

/// <summary>OpenAI-compatible chat-completions provider (local endpoint or API).</summary>
public sealed class OpenAiCompatibleProvider : ILlmProvider
{
    private readonly AiOptions _opts;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };

    public OpenAiCompatibleProvider(AiOptions opts) => _opts = opts;
    public string Name => _opts.Provider;

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var endpoint = _opts.Endpoint
            ?? throw new InvalidOperationException("--endpoint is required to send (e.g. http://localhost:1234/v1/chat/completions).");

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                model = _opts.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt },
                },
                temperature = 0.2,
            }),
        };
        if (_opts.ApiKey is { Length: > 0 } key)
            req.Headers.Authorization = new("Bearer", key);

        using var resp = await Http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "(empty response)";
    }
}

public static class LlmProviderFactory
{
    public static ILlmProvider Create(AiOptions opts) =>
        opts.Send ? new OpenAiCompatibleProvider(opts) : new DryRunProvider();
}
