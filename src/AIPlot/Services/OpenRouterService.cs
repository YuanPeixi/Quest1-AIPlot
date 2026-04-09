using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIPlot.Services;

/// <summary>
/// OpenRouter API 服务实现
/// 文档：https://openrouter.ai/docs
/// 兼容 OpenAI Chat Completions 格式
/// </summary>
public class OpenRouterService : IOpenRouterService
{
    private const string BaseUrl = "https://openrouter.ai/api/v1";
    private readonly HttpClient _http;
    private readonly ILogger<OpenRouterService> _logger;
    private readonly AppSettingsService _settings;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public OpenRouterService(
        HttpClient http,
        ILogger<OpenRouterService> logger,
        AppSettingsService settings)
    {
        _http = http;
        _logger = logger;
        _settings = settings;
    }

    private void SetAuthHeader(HttpRequestMessage req)
    {
        var key = _settings.ApiKey;
        if (!string.IsNullOrWhiteSpace(key))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
        if (!string.IsNullOrWhiteSpace(_settings.SiteUrl))
            req.Headers.TryAddWithoutValidation("HTTP-Referer", _settings.SiteUrl);
        if (!string.IsNullOrWhiteSpace(_settings.SiteName))
            req.Headers.TryAddWithoutValidation("X-Title", _settings.SiteName);
    }

    public async Task<string> ChatAsync(
        string model,
        IEnumerable<ChatMessage> messages,
        CancellationToken ct = default)
    {
        var body = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = false
        };

        var json = JsonSerializer.Serialize(body, _jsonOpts);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        SetAuthHeader(req);

        var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("OpenRouter error {Status}: {Body}", resp.StatusCode, text);
            throw new InvalidOperationException($"OpenRouter API 错误 {resp.StatusCode}: {text}");
        }

        var doc = JsonDocument.Parse(text);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        string model,
        IEnumerable<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true
        };

        var json = JsonSerializer.Serialize(body, _jsonOpts);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        SetAuthHeader(req);

        // Cannot yield inside catch, so we capture the error first
        string? earlyError = null;
        HttpResponseMessage? resp = null;
        try
        {
            resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stream request failed");
            earlyError = $"[错误: {ex.Message}]";
        }

        if (earlyError != null)
        {
            yield return earlyError;
            yield break;
        }

        if (resp == null || !resp.IsSuccessStatusCode)
        {
            var errBody = resp != null ? await resp.Content.ReadAsStringAsync(ct) : "No response";
            _logger.LogError("OpenRouter stream error {Status}: {Body}", resp?.StatusCode, errBody);
            yield return $"[API错误 {resp?.StatusCode}: {errBody}]";
            yield break;
        }

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            string? token = null;
            try
            {
                var node = JsonNode.Parse(data);
                token = node?["choices"]?[0]?["delta"]?["content"]?.GetValue<string>();
            }
            catch (Exception ex)
            {
                _logger.LogDebug("SSE parse skip: {Err}", ex.Message);
            }

            if (token != null) yield return token;
        }
    }

    public async Task<List<ModelInfo>> GetModelsAsync(CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models");
        SetAuthHeader(req);

        try
        {
            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return GetFallbackModels();

            var text = await resp.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(text);

            var models = new List<ModelInfo>();
            foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                try
                {
                    var id = item.GetProperty("id").GetString() ?? "";
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? id : id;
                    var desc = item.TryGetProperty("description", out var d) ? d.GetString() : null;
                    long? ctx = null;
                    if (item.TryGetProperty("context_length", out var cl) && cl.ValueKind == JsonValueKind.Number)
                        ctx = cl.GetInt64();
                    decimal? pp = null, pc = null;
                    if (item.TryGetProperty("pricing", out var pricing))
                    {
                        if (pricing.TryGetProperty("prompt", out var pProp) && pProp.ValueKind == JsonValueKind.String)
                            decimal.TryParse(pProp.GetString(), out var ppv) ;
                        if (pricing.TryGetProperty("completion", out var cProp) && cProp.ValueKind == JsonValueKind.String)
                            decimal.TryParse(cProp.GetString(), out var pcv);
                    }
                    models.Add(new ModelInfo(id, name, desc, ctx, pp, pc));
                }
                catch { /* skip malformed entry */ }
            }

            return models.OrderBy(m => m.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch models, using fallback list");
            return GetFallbackModels();
        }
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var resp = await _http.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static List<ModelInfo> GetFallbackModels() =>
    [
        new("openai/gpt-4o", "GPT-4o", "OpenAI GPT-4o", 128000, null, null),
        new("openai/gpt-4o-mini", "GPT-4o Mini", "OpenAI GPT-4o Mini", 128000, null, null),
        new("openai/gpt-4-turbo", "GPT-4 Turbo", "OpenAI GPT-4 Turbo", 128000, null, null),
        new("anthropic/claude-3.5-sonnet", "Claude 3.5 Sonnet", "Anthropic Claude 3.5 Sonnet", 200000, null, null),
        new("anthropic/claude-3-haiku", "Claude 3 Haiku", "Anthropic Claude 3 Haiku", 200000, null, null),
        new("google/gemini-pro-1.5", "Gemini Pro 1.5", "Google Gemini Pro 1.5", 1000000, null, null),
        new("meta-llama/llama-3.1-8b-instruct", "Llama 3.1 8B", "Meta Llama 3.1 8B (Free)", 131072, null, null),
        new("meta-llama/llama-3.1-70b-instruct", "Llama 3.1 70B", "Meta Llama 3.1 70B", 131072, null, null),
    ];
}
