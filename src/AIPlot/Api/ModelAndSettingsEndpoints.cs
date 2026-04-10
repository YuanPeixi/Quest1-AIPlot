using AIPlot.DTOs;
using AIPlot.Services;

namespace AIPlot.Api;

/// <summary>模型列表 & 设置端点</summary>
public static class ModelAndSettingsEndpoints
{
    public static void MapModelAndSettingsEndpoints(this WebApplication app)
    {
        // ─── 模型列表 ──────────────────────────────────────
        app.MapGet("/api/models", async (IOpenRouterService ai, CancellationToken ct) =>
        {
            var models = await ai.GetModelsAsync(ct);
            return Results.Ok(models);
        })
        .WithTags("Models")
        .WithSummary("获取可用 AI 模型列表");

        // ─── 设置 ──────────────────────────────────────────
        app.MapGet("/api/settings", (AppSettingsService settings) =>
            Results.Ok(new
            {
                HasApiKey = !string.IsNullOrWhiteSpace(settings.ApiKey),
                settings.DefaultModel,
                settings.SiteUrl,
                settings.SiteName,
                AgentModel = AppSettingsService.AgentModel
            }))
        .WithTags("Settings")
        .WithSummary("获取当前配置");

        app.MapPost("/api/settings", (SaveSettingsRequest req, AppSettingsService settings) =>
        {
            settings.UpdateSettings(req.ApiKey, req.DefaultModel, req.SiteUrl ?? "", req.SiteName ?? "");
            return Results.Ok(new { message = "Settings saved" });
        })
        .WithTags("Settings")
        .WithSummary("保存 OpenRouter 设置");

        app.MapPost("/api/settings/validate", async (
            ValidateKeyRequest req, IOpenRouterService ai, CancellationToken ct) =>
        {
            var valid = await ai.ValidateApiKeyAsync(req.ApiKey, ct);
            return Results.Ok(new { valid });
        })
        .WithTags("Settings")
        .WithSummary("验证 API Key 是否有效");
    }
}

internal record ValidateKeyRequest(string ApiKey);
