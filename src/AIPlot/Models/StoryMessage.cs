namespace AIPlot.Models;

/// <summary>故事消息/对话</summary>
public class StoryMessage
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public Story? Story { get; set; }

    public int? CharacterId { get; set; }
    public Character? Character { get; set; }

    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; } = MessageType.Dialogue;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>消息在故事中的顺序编号</summary>
    public int Sequence { get; set; } = 0;

    /// <summary>AI生成使用的模型</summary>
    public string? UsedModel { get; set; }

    /// <summary>生成指令（用于记录）</summary>
    public string? GenerationHint { get; set; }
}

public enum MessageType
{
    /// <summary>角色对话</summary>
    Dialogue,
    /// <summary>旁白/叙事</summary>
    Narration,
    /// <summary>AI审核评估</summary>
    Review,
    /// <summary>系统消息</summary>
    System
}
