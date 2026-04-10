using AIPlot.DTOs;
using AIPlot.Services;

namespace AIPlot.Api;

/// <summary>角色卡生成端点</summary>
public static class CharacterEndpoints
{
    public static void MapCharacterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/characters").WithTags("Characters");

        /// <summary>
        /// 从文本描述快速生成角色卡
        /// POST /api/characters/generate-card
        /// </summary>
        group.MapPost("/generate-card", async (
            GenerateCharacterCardRequest req,
            PlotGenerationService gen,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.PromptText))
                return Results.BadRequest("PromptText is required");

            var card = await gen.GenerateCharacterCardAsync(req.PromptText, req.ModelId, ct);
            return card is null
                ? Results.Problem("AI generation failed to produce a valid character card")
                : Results.Ok(card);
        }).WithSummary("从文本描述生成角色卡");
    }
}
