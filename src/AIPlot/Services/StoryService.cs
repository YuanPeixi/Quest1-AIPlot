using AIPlot.Data;
using AIPlot.Models;
using Microsoft.EntityFrameworkCore;

namespace AIPlot.Services;

/// <summary>故事管理服务实现（EF Core + SQLite）</summary>
public class StoryService : IStoryService
{
    private readonly AppDbContext _db;

    public StoryService(AppDbContext db) => _db = db;

    // ─── 故事 ─────────────────────────────────────────────

    public Task<List<Story>> GetStoriesAsync(CancellationToken ct = default) =>
        _db.Stories
           .OrderByDescending(s => s.UpdatedAt)
           .Include(s => s.Characters)
           .ToListAsync(ct);

    public Task<Story?> GetStoryAsync(int id, CancellationToken ct = default) =>
        _db.Stories
           .Include(s => s.Characters.OrderBy(c => c.SortOrder))
           .Include(s => s.Messages.OrderBy(m => m.Sequence))
               .ThenInclude(m => m.Character)
           .Include(s => s.Checkpoints.OrderBy(cp => cp.MessageSequence))
           .Include(s => s.WorldBook!.Entries.OrderBy(e => e.SortOrder))
           .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Story> CreateStoryAsync(string title, string description, CancellationToken ct = default)
    {
        var story = new Story { Title = title, Description = description };
        _db.Stories.Add(story);
        await _db.SaveChangesAsync(ct);
        return story;
    }

    public async Task<Story> UpdateStoryAsync(int id, string title, string description, CancellationToken ct = default)
    {
        var story = await _db.Stories.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Story {id} not found");
        story.Title = title;
        story.Description = description;
        story.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return story;
    }

    public async Task DeleteStoryAsync(int id, CancellationToken ct = default)
    {
        var story = await _db.Stories.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Story {id} not found");
        _db.Stories.Remove(story);
        await _db.SaveChangesAsync(ct);
    }

    // ─── 角色 ─────────────────────────────────────────────

    public async Task<Character> AddCharacterAsync(int storyId, Character character, CancellationToken ct = default)
    {
        character.StoryId = storyId;
        var count = await _db.Characters.CountAsync(c => c.StoryId == storyId, ct);
        character.SortOrder = count;
        _db.Characters.Add(character);
        await _db.SaveChangesAsync(ct);
        return character;
    }

    public async Task<Character> UpdateCharacterAsync(Character character, CancellationToken ct = default)
    {
        _db.Characters.Update(character);
        await _db.SaveChangesAsync(ct);
        return character;
    }

    public async Task DeleteCharacterAsync(int characterId, CancellationToken ct = default)
    {
        var c = await _db.Characters.FindAsync([characterId], ct)
            ?? throw new KeyNotFoundException($"Character {characterId} not found");
        _db.Characters.Remove(c);
        await _db.SaveChangesAsync(ct);
    }

    // ─── 消息 ─────────────────────────────────────────────

    public async Task<StoryMessage> AddMessageAsync(StoryMessage message, CancellationToken ct = default)
    {
        var maxSeq = await _db.Messages
            .Where(m => m.StoryId == message.StoryId)
            .Select(m => (int?)m.Sequence)
            .MaxAsync(ct) ?? -1;
        message.Sequence = maxSeq + 1;
        _db.Messages.Add(message);

        // Update story timestamp
        var story = await _db.Stories.FindAsync([message.StoryId], ct);
        if (story != null) story.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return message;
    }

    public async Task DeleteMessagesFromSequenceAsync(int storyId, int fromSequence, CancellationToken ct = default)
    {
        var toDelete = await _db.Messages
            .Where(m => m.StoryId == storyId && m.Sequence >= fromSequence)
            .ToListAsync(ct);
        _db.Messages.RemoveRange(toDelete);
        await _db.SaveChangesAsync(ct);
    }

    // ─── 检查点 ─────────────────────────────────────────────

    public async Task<Checkpoint> CreateCheckpointAsync(
        int storyId, string name, int messageSequence, string? description,
        CancellationToken ct = default)
    {
        var cp = new Checkpoint
        {
            StoryId = storyId,
            Name = name,
            MessageSequence = messageSequence,
            Description = description
        };
        _db.Checkpoints.Add(cp);
        await _db.SaveChangesAsync(ct);
        return cp;
    }

    public async Task RestoreCheckpointAsync(int checkpointId, CancellationToken ct = default)
    {
        var cp = await _db.Checkpoints.FindAsync([checkpointId], ct)
            ?? throw new KeyNotFoundException($"Checkpoint {checkpointId} not found");

        // Remove all messages after the checkpoint sequence
        await DeleteMessagesFromSequenceAsync(cp.StoryId, cp.MessageSequence + 1, ct);
    }

    public async Task DeleteCheckpointAsync(int checkpointId, CancellationToken ct = default)
    {
        var cp = await _db.Checkpoints.FindAsync([checkpointId], ct)
            ?? throw new KeyNotFoundException($"Checkpoint {checkpointId} not found");
        _db.Checkpoints.Remove(cp);
        await _db.SaveChangesAsync(ct);
    }

    // ─── 世界书 ─────────────────────────────────────────────

    public async Task<WorldBook> GetOrCreateWorldBookAsync(int storyId, CancellationToken ct = default)
    {
        var wb = await _db.WorldBooks
            .Include(w => w.Entries.OrderBy(e => e.SortOrder))
            .FirstOrDefaultAsync(w => w.StoryId == storyId, ct);
        if (wb != null) return wb;

        wb = new WorldBook { StoryId = storyId };
        _db.WorldBooks.Add(wb);
        await _db.SaveChangesAsync(ct);
        return wb;
    }

    public async Task<WorldBook> UpdateWorldBookAsync(int storyId, string background, CancellationToken ct = default)
    {
        var wb = await GetOrCreateWorldBookAsync(storyId, ct);
        wb.Background = background;
        await _db.SaveChangesAsync(ct);
        return wb;
    }

    public async Task<WorldEntry> AddWorldEntryAsync(int worldBookId, WorldEntry entry, CancellationToken ct = default)
    {
        entry.WorldBookId = worldBookId;
        var count = await _db.WorldEntries.CountAsync(e => e.WorldBookId == worldBookId, ct);
        entry.SortOrder = count;
        _db.WorldEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<WorldEntry> UpdateWorldEntryAsync(WorldEntry entry, CancellationToken ct = default)
    {
        _db.WorldEntries.Update(entry);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task DeleteWorldEntryAsync(int entryId, CancellationToken ct = default)
    {
        var e = await _db.WorldEntries.FindAsync([entryId], ct)
            ?? throw new KeyNotFoundException($"WorldEntry {entryId} not found");
        _db.WorldEntries.Remove(e);
        await _db.SaveChangesAsync(ct);
    }
}
