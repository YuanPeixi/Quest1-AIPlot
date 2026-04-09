namespace AIPlot.DTOs;

// ───── Story DTOs ─────

public record CreateStoryRequest(string Title, string Description);
public record UpdateStoryRequest(string Title, string Description);

// ───── Character DTOs ─────

public record CreateCharacterRequest(
    string Name,
    string Description,
    string Personality,
    string Background,
    string SpeakingStyle,
    string Goals,
    string ModelId,
    string AvatarColor,
    string AvatarEmoji,
    bool IsAI,
    bool IsReviewer);

public record UpdateCharacterRequest(
    string Name,
    string Description,
    string Personality,
    string Background,
    string SpeakingStyle,
    string Goals,
    string ModelId,
    string AvatarColor,
    string AvatarEmoji);

// ───── Message / Generation DTOs ─────

public record GenerateMessageRequest(
    int CharacterId,
    /// <summary>用户给AI的剧情方向提示</summary>
    string? Direction = null);

public record AddManualMessageRequest(
    int CharacterId,
    string Content,
    string Type = "Dialogue");

public record RegenerateFromRequest(
    int FromSequence,
    int CharacterId,
    string? Direction = null);

// ───── Checkpoint DTOs ─────

public record CreateCheckpointRequest(string Name, int MessageSequence, string? Description = null);
public record RestoreCheckpointRequest(int CheckpointId);

// ───── AI Review DTOs ─────

public record ReviewStoryRequest(
    string? Instructions = null,
    string? ModelId = null);

// ───── AI Generation (Character Card / World Book) DTOs ─────

public record GenerateCharacterCardRequest(
    string PromptText,
    string? ModelId = null);

public record GenerateWorldBookRequest(
    string PromptText,
    string? ModelId = null,
    int StoryId = 0);

// ───── WorldBook DTOs ─────

public record CreateWorldEntryRequest(
    string Title,
    string Content,
    string Category,
    string Keywords);

public record UpdateWorldEntryRequest(
    string Title,
    string Content,
    string Category,
    string Keywords);

// ───── OpenRouter / Settings DTOs ─────

public record SaveSettingsRequest(
    string ApiKey,
    string DefaultModel,
    string? SiteUrl = null,
    string? SiteName = null);
