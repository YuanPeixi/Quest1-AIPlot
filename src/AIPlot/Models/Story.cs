namespace AIPlot.Models;

/// <summary>剧情故事实体</summary>
public class Story
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Character> Characters { get; set; } = new();
    public List<StoryMessage> Messages { get; set; } = new();
    public List<Checkpoint> Checkpoints { get; set; } = new();
    public WorldBook? WorldBook { get; set; }
}
