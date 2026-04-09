namespace AIPlot.Models;

/// <summary>世界书（世界观设定）</summary>
public class WorldBook
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public Story? Story { get; set; }

    public string Background { get; set; } = string.Empty;
    public List<WorldEntry> Entries { get; set; } = new();
}

/// <summary>世界书词条</summary>
public class WorldEntry
{
    public int Id { get; set; }
    public int WorldBookId { get; set; }
    public WorldBook? WorldBook { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>词条类别（地点/人物/规则/历史/其他）</summary>
    public string Category { get; set; } = "其他";

    /// <summary>触发关键词（逗号分隔），匹配时注入上下文</summary>
    public string Keywords { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 0;
}
