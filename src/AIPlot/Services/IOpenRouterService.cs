namespace AIPlot.Services;

/// <summary>OpenRouter AI API 服务接口</summary>
public interface IOpenRouterService
{
    /// <summary>单次补全（非流式）</summary>
    Task<string> ChatAsync(
        string model,
        IEnumerable<ChatMessage> messages,
        CancellationToken ct = default);

    /// <summary>流式补全（逐token返回）</summary>
    IAsyncEnumerable<string> ChatStreamAsync(
        string model,
        IEnumerable<ChatMessage> messages,
        CancellationToken ct = default);

    /// <summary>获取可用模型列表</summary>
    Task<List<ModelInfo>> GetModelsAsync(CancellationToken ct = default);

    /// <summary>验证API Key是否有效</summary>
    Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default);
}

public record ChatMessage(string Role, string Content);

public record ModelInfo(
    string Id,
    string Name,
    string? Description,
    long? ContextLength,
    decimal? PricePrompt,
    decimal? PriceCompletion);
