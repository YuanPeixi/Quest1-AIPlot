namespace AIPlot.Models;

/// <summary>故事角色（支持AI扮演）</summary>
public class Character
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public Story? Story { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string SpeakingStyle { get; set; } = string.Empty;
    public string Goals { get; set; } = string.Empty;

    /// <summary>使用的AI模型ID（OpenRouter格式，如 openai/gpt-4o）</summary>
    public string ModelId { get; set; } = "openai/gpt-4o-mini";
    public string AvatarColor { get; set; } = "#2196F3";
    public string AvatarEmoji { get; set; } = "🎭";

    /// <summary>是否由AI扮演（false表示人工输入）</summary>
    public bool IsAI { get; set; } = true;

    /// <summary>是否为故事审核员角色</summary>
    public bool IsReviewer { get; set; } = false;

    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
