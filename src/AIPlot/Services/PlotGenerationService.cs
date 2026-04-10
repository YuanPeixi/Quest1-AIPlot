using AIPlot.Models;
using System.Text;
using System.Text.Json;

namespace AIPlot.Services;

/// <summary>
/// AI 剧情生成服务
/// 负责构建提示词、调用 OpenRouter API、处理流式输出
/// </summary>
public class PlotGenerationService
{
    private readonly IOpenRouterService _ai;
    private readonly AppSettingsService _settings;
    private readonly ILogger<PlotGenerationService> _logger;

    public PlotGenerationService(
        IOpenRouterService ai,
        AppSettingsService settings,
        ILogger<PlotGenerationService> logger)
    {
        _ai = ai;
        _settings = settings;
        _logger = logger;
    }

    // ─── 角色对话生成 ─────────────────────────────────────

    /// <summary>
    /// 流式生成角色的下一段对话/动作
    /// </summary>
    public IAsyncEnumerable<string> GenerateCharacterMessageAsync(
        Story story,
        Character character,
        string? direction = null,
        CancellationToken ct = default)
    {
        var model = string.IsNullOrWhiteSpace(character.ModelId) ? _settings.DefaultModel : character.ModelId;
        var messages = BuildCharacterMessages(story, character, direction);
        return _ai.ChatStreamAsync(model, messages, ct);
    }

    /// <summary>
    /// 生成旁白/叙事段落
    /// </summary>
    public IAsyncEnumerable<string> GenerateNarrationAsync(
        Story story,
        string? direction = null,
        CancellationToken ct = default)
    {
        var model = _settings.DefaultModel;
        var messages = BuildNarrationMessages(story, direction);
        return _ai.ChatStreamAsync(model, messages, ct);
    }

    // ─── 故事审核 ─────────────────────────────────────────

    /// <summary>
    /// 对整个故事进行 AI 审核评估
    /// </summary>
    public async Task<string> ReviewStoryAsync(
        Story story,
        string? instructions = null,
        string? modelId = null,
        CancellationToken ct = default)
    {
        var model = modelId ?? _settings.DefaultModel;
        var messages = BuildReviewMessages(story, instructions);
        return await _ai.ChatAsync(model, messages, ct);
    }

    // ─── 角色卡生成 ───────────────────────────────────────

    /// <summary>
    /// 从文本快速生成角色卡（JSON 结构）
    /// </summary>
    public async Task<Character?> GenerateCharacterCardAsync(
        string promptText,
        string? modelId = null,
        CancellationToken ct = default)
    {
        var model = modelId ?? _settings.DefaultModel;
        var messages = new[]
        {
            new ChatMessage("system", @"你是一个专业的角色卡设计助手。用户会提供对角色的描述，你需要将其整理成结构化的角色卡JSON。
输出格式（仅输出JSON，不要其他文字）：
{
  ""name"": ""角色名"",
  ""description"": ""简短外貌/形象描述（1-3句）"",
  ""personality"": ""性格特点描述（3-5句）"",
  ""background"": ""背景故事（3-5句）"",
  ""speakingStyle"": ""说话风格描述（1-2句）"",
  ""goals"": ""目标和动机（1-3句）"",
  ""avatarEmoji"": ""一个代表该角色的emoji""
}"),
            new ChatMessage("user", promptText)
        };

        var json = await _ai.ChatAsync(model, messages, ct);

        // Extract JSON block if wrapped in markdown
        json = ExtractJsonBlock(json);

        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var card = JsonSerializer.Deserialize<CharacterCardDto>(json, opts);
            if (card == null) return null;

            return new Character
            {
                Name = card.Name ?? "",
                Description = card.Description ?? "",
                Personality = card.Personality ?? "",
                Background = card.Background ?? "",
                SpeakingStyle = card.SpeakingStyle ?? "",
                Goals = card.Goals ?? "",
                AvatarEmoji = card.AvatarEmoji ?? "🎭",
                IsAI = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse character card JSON: {Json}", json);
            return null;
        }
    }

    // ─── 世界书生成 ───────────────────────────────────────

    /// <summary>
    /// 从文本生成世界书词条列表
    /// </summary>
    public async Task<List<WorldEntry>> GenerateWorldBookAsync(
        string promptText,
        string? modelId = null,
        CancellationToken ct = default)
    {
        var model = modelId ?? _settings.DefaultModel;
        var messages = new[]
        {
            new ChatMessage("system", @"你是一个世界观构建专家。根据用户提供的故事世界描述，整理出结构化的世界书词条。
输出格式（仅输出JSON数组，不要其他文字）：
[
  {
    ""title"": ""词条标题"",
    ""category"": ""地点/人物/规则/历史/其他"",
    ""content"": ""详细内容描述"",
    ""keywords"": ""触发关键词1,触发关键词2""
  }
]
提取5-15个重要词条，涵盖地点、重要人物、世界规则、历史事件等。"),
            new ChatMessage("user", promptText)
        };

        var json = await _ai.ChatAsync(model, messages, ct);
        json = ExtractJsonBlock(json);

        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var entries = JsonSerializer.Deserialize<List<WorldEntryDto>>(json, opts);
            if (entries == null) return [];

            return entries.Select((e, i) => new WorldEntry
            {
                Title = e.Title ?? "",
                Category = e.Category ?? "其他",
                Content = e.Content ?? "",
                Keywords = e.Keywords ?? "",
                SortOrder = i
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse world book JSON: {Json}", json);
            return [];
        }
    }

    // ─── 提示词构建 ───────────────────────────────────────

    private IEnumerable<ChatMessage> BuildCharacterMessages(
        Story story, Character character, string? direction)
    {
        var sb = new StringBuilder();

        // World book context
        if (story.WorldBook != null)
        {
            sb.AppendLine("=== 世界观设定 ===");
            if (!string.IsNullOrWhiteSpace(story.WorldBook.Background))
                sb.AppendLine(story.WorldBook.Background);

            foreach (var entry in story.WorldBook.Entries.OrderBy(e => e.SortOrder))
            {
                sb.AppendLine($"\n【{entry.Title}】({entry.Category})");
                sb.AppendLine(entry.Content);
            }
            sb.AppendLine();
        }

        // Story overview
        sb.AppendLine($"=== 故事：{story.Title} ===");
        if (!string.IsNullOrWhiteSpace(story.Description))
            sb.AppendLine(story.Description);
        sb.AppendLine();

        // Other characters brief
        var others = story.Characters.Where(c => c.Id != character.Id).ToList();
        if (others.Any())
        {
            sb.AppendLine("=== 其他角色 ===");
            foreach (var oc in others)
            {
                sb.AppendLine($"- {oc.Name}：{oc.Description}");
            }
            sb.AppendLine();
        }

        // Character card (self)
        sb.AppendLine("=== 你的角色设定 ===");
        sb.AppendLine($"姓名：{character.Name}");
        if (!string.IsNullOrWhiteSpace(character.Description))
            sb.AppendLine($"外貌：{character.Description}");
        if (!string.IsNullOrWhiteSpace(character.Personality))
            sb.AppendLine($"性格：{character.Personality}");
        if (!string.IsNullOrWhiteSpace(character.Background))
            sb.AppendLine($"背景：{character.Background}");
        if (!string.IsNullOrWhiteSpace(character.SpeakingStyle))
            sb.AppendLine($"说话风格：{character.SpeakingStyle}");
        if (!string.IsNullOrWhiteSpace(character.Goals))
            sb.AppendLine($"目标：{character.Goals}");
        sb.AppendLine();
        sb.AppendLine($"你现在扮演 {character.Name}，请完全沉浸在角色中，以第一人称角度进行对话和行动描写。");
        sb.AppendLine("回复格式：直接输出角色的对话或动作，不需要前缀标签。");
        if (!string.IsNullOrWhiteSpace(direction))
        {
            sb.AppendLine($"\n本次剧情方向提示：{direction}");
        }

        var systemPrompt = sb.ToString();
        var chatHistory = BuildChatHistory(story, character);

        yield return new ChatMessage("system", systemPrompt);
        foreach (var m in chatHistory)
            yield return m;

        // Final user turn with direction
        if (!string.IsNullOrWhiteSpace(direction))
            yield return new ChatMessage("user", $"（剧情方向：{direction}）请继续扮演 {character.Name} 进行下一段对话或行动。");
        else
            yield return new ChatMessage("user", $"请继续扮演 {character.Name} 进行下一段对话或行动。");
    }

    private IEnumerable<ChatMessage> BuildNarrationMessages(Story story, string? direction)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"你是故事《{story.Title}》的叙事者。");
        sb.AppendLine(story.Description);
        sb.AppendLine("\n请以第三人称视角写一段简短的过渡叙述，描述场景、氛围或事件，为下一幕铺垫。");
        if (!string.IsNullOrWhiteSpace(direction))
            sb.AppendLine($"叙事方向：{direction}");

        yield return new ChatMessage("system", sb.ToString());
        foreach (var m in BuildChatHistory(story, null))
            yield return m;
        yield return new ChatMessage("user", "请写一段过渡叙述。");
    }

    private IEnumerable<ChatMessage> BuildReviewMessages(Story story, string? instructions)
    {
        var storyText = new StringBuilder();
        storyText.AppendLine($"故事标题：{story.Title}");
        storyText.AppendLine($"故事简介：{story.Description}");
        storyText.AppendLine();
        storyText.AppendLine("=== 故事内容 ===");

        foreach (var msg in story.Messages.OrderBy(m => m.Sequence))
        {
            if (msg.Type == MessageType.Review || msg.Type == MessageType.System) continue;
            var speaker = msg.Character?.Name ?? "旁白";
            var typeLabel = msg.Type == MessageType.Narration ? "[旁白]" : $"[{speaker}]";
            storyText.AppendLine($"{typeLabel} {msg.Content}");
        }

        var systemPrompt = @"你是一位专业的故事审核员和剧情评估专家。请对提供的故事进行全面分析，包括：
1. **整体评分**（1-10分）：剧情流畅度、角色一致性、世界观合理性
2. **优点**：故事中做得好的地方
3. **问题**：剧情漏洞、角色矛盾、逻辑问题
4. **改进建议**：具体可操作的改进方向
5. **后续发展预测**：根据当前走向，可能的3个故事发展方向

请以 Markdown 格式输出，条理清晰。";

        yield return new ChatMessage("system", systemPrompt);
        yield return new ChatMessage("user",
            storyText.ToString() + (string.IsNullOrWhiteSpace(instructions)
                ? "\n请进行审核评估。"
                : $"\n特别关注：{instructions}"));
    }

    private static List<ChatMessage> BuildChatHistory(Story story, Character? currentCharacter)
    {
        // Use last N messages as context (avoid token overflow)
        const int MaxContextMessages = 30;
        var msgs = story.Messages
            .Where(m => m.Type != MessageType.Review && m.Type != MessageType.System)
            .OrderBy(m => m.Sequence)
            .TakeLast(MaxContextMessages)
            .ToList();

        var result = new List<ChatMessage>();
        foreach (var msg in msgs)
        {
            var speaker = msg.Character?.Name ?? "旁白";
            var content = msg.Type == MessageType.Narration
                ? $"[旁白] {msg.Content}"
                : msg.Content;

            // If from current character, mark as assistant; else as user
            var role = (currentCharacter != null && msg.CharacterId == currentCharacter.Id)
                ? "assistant"
                : "user";

            result.Add(new ChatMessage(role, $"[{speaker}]: {content}"));
        }
        return result;
    }

    private static string ExtractJsonBlock(string text)
    {
        // Extract JSON from markdown code blocks
        var start = text.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            start = text.IndexOf('\n', start) + 1;
            var end = text.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                return text[start..end].Trim();
        }
        // Try raw JSON extraction
        var firstBrace = text.IndexOfAny(['{', '[']);
        var lastBrace = text.LastIndexOfAny(['}', ']']);
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return text[firstBrace..(lastBrace + 1)].Trim();
        return text.Trim();
    }

    // ─── Internal DTOs ────────────────────────────────────

    private class CharacterCardDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Personality { get; set; }
        public string? Background { get; set; }
        public string? SpeakingStyle { get; set; }
        public string? Goals { get; set; }
        public string? AvatarEmoji { get; set; }
    }

    private class WorldEntryDto
    {
        public string? Title { get; set; }
        public string? Category { get; set; }
        public string? Content { get; set; }
        public string? Keywords { get; set; }
    }
}
