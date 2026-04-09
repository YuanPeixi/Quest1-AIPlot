using AIPlot.Models;

namespace AIPlot.Services;

/// <summary>故事管理服务接口</summary>
public interface IStoryService
{
    // ─── 故事 ───
    Task<List<Story>> GetStoriesAsync(CancellationToken ct = default);
    Task<Story?> GetStoryAsync(int id, CancellationToken ct = default);
    Task<Story> CreateStoryAsync(string title, string description, CancellationToken ct = default);
    Task<Story> UpdateStoryAsync(int id, string title, string description, CancellationToken ct = default);
    Task DeleteStoryAsync(int id, CancellationToken ct = default);

    // ─── 角色 ───
    Task<Character> AddCharacterAsync(int storyId, Character character, CancellationToken ct = default);
    Task<Character> UpdateCharacterAsync(Character character, CancellationToken ct = default);
    Task DeleteCharacterAsync(int characterId, CancellationToken ct = default);

    // ─── 消息 ───
    Task<StoryMessage> AddMessageAsync(StoryMessage message, CancellationToken ct = default);
    Task DeleteMessagesFromSequenceAsync(int storyId, int fromSequence, CancellationToken ct = default);

    // ─── 检查点 ───
    Task<Checkpoint> CreateCheckpointAsync(int storyId, string name, int messageSequence, string? description, CancellationToken ct = default);
    Task RestoreCheckpointAsync(int checkpointId, CancellationToken ct = default);
    Task DeleteCheckpointAsync(int checkpointId, CancellationToken ct = default);

    // ─── 世界书 ───
    Task<WorldBook> GetOrCreateWorldBookAsync(int storyId, CancellationToken ct = default);
    Task<WorldBook> UpdateWorldBookAsync(int storyId, string background, CancellationToken ct = default);
    Task<WorldEntry> AddWorldEntryAsync(int worldBookId, WorldEntry entry, CancellationToken ct = default);
    Task<WorldEntry> UpdateWorldEntryAsync(WorldEntry entry, CancellationToken ct = default);
    Task DeleteWorldEntryAsync(int entryId, CancellationToken ct = default);
}
