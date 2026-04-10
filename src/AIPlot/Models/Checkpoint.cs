namespace AIPlot.Models;

/// <summary>故事检查点（快照）</summary>
public class Checkpoint
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public Story? Story { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>该检查点对应的消息序列号（含）</summary>
    public int MessageSequence { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
