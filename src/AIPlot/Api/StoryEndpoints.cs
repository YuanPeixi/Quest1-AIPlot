using AIPlot.DTOs;
using AIPlot.Models;
using AIPlot.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIPlot.Api;

/// <summary>故事相关的 Minimal API 端点</summary>
public static class StoryEndpoints
{
    public static void MapStoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stories").WithTags("Stories");

        // ─── CRUD ─────────────────────────────────────────

        group.MapGet("/", async (IStoryService svc, CancellationToken ct) =>
        {
            var stories = await svc.GetStoriesAsync(ct);
            return Results.Ok(stories.Select(s => new
            {
                s.Id, s.Title, s.Description, s.CreatedAt, s.UpdatedAt,
                CharacterCount = s.Characters.Count
            }));
        }).WithSummary("获取所有故事列表");

        group.MapGet("/{id:int}", async (int id, IStoryService svc, CancellationToken ct) =>
        {
            var story = await svc.GetStoryAsync(id, ct);
            return story is null ? Results.NotFound() : Results.Ok(story);
        }).WithSummary("获取故事详情");

        group.MapPost("/", async (CreateStoryRequest req, IStoryService svc, CancellationToken ct) =>
        {
            var story = await svc.CreateStoryAsync(req.Title, req.Description, ct);
            return Results.Created($"/api/stories/{story.Id}", story);
        }).WithSummary("创建新故事");

        group.MapPut("/{id:int}", async (int id, UpdateStoryRequest req, IStoryService svc, CancellationToken ct) =>
        {
            try
            {
                var story = await svc.UpdateStoryAsync(id, req.Title, req.Description, ct);
                return Results.Ok(story);
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).WithSummary("更新故事信息");

        group.MapDelete("/{id:int}", async (int id, IStoryService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.DeleteStoryAsync(id, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).WithSummary("删除故事");

        // ─── 角色管理 ──────────────────────────────────────

        group.MapPost("/{id:int}/characters", async (
            int id, CreateCharacterRequest req, IStoryService svc, CancellationToken ct) =>
        {
            var character = new Character
            {
                Name = req.Name,
                Description = req.Description,
                Personality = req.Personality,
                Background = req.Background,
                SpeakingStyle = req.SpeakingStyle,
                Goals = req.Goals,
                ModelId = req.ModelId,
                AvatarColor = req.AvatarColor,
                AvatarEmoji = req.AvatarEmoji,
                IsAI = req.IsAI,
                IsReviewer = req.IsReviewer
            };
            var result = await svc.AddCharacterAsync(id, character, ct);
            return Results.Created($"/api/stories/{id}/characters/{result.Id}", result);
        }).WithSummary("添加角色");

        group.MapPut("/{storyId:int}/characters/{charId:int}", async (
            int storyId, int charId, UpdateCharacterRequest req, IStoryService svc, CancellationToken ct) =>
        {
            var story = await svc.GetStoryAsync(storyId, ct);
            if (story is null) return Results.NotFound("Story not found");
            var character = story.Characters.FirstOrDefault(c => c.Id == charId);
            if (character is null) return Results.NotFound("Character not found");

            character.Name = req.Name;
            character.Description = req.Description;
            character.Personality = req.Personality;
            character.Background = req.Background;
            character.SpeakingStyle = req.SpeakingStyle;
            character.Goals = req.Goals;
            character.ModelId = req.ModelId;
            character.AvatarColor = req.AvatarColor;
            character.AvatarEmoji = req.AvatarEmoji;

            var updated = await svc.UpdateCharacterAsync(character, ct);
            return Results.Ok(updated);
        }).WithSummary("更新角色");

        group.MapDelete("/{storyId:int}/characters/{charId:int}", async (
            int storyId, int charId, IStoryService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.DeleteCharacterAsync(charId, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).WithSummary("删除角色");

        // ─── AI 生成对话（流式） ────────────────────────────

        group.MapPost("/{id:int}/generate", async (
            int id,
            GenerateMessageRequest req,
            IStoryService svc,
            PlotGenerationService gen,
            HttpContext http,
            CancellationToken ct) =>
        {
            var story = await svc.GetStoryAsync(id, ct);
            if (story is null) return Results.NotFound("Story not found");

            var character = story.Characters.FirstOrDefault(c => c.Id == req.CharacterId);
            if (character is null) return Results.NotFound("Character not found");

            // Stream SSE response
            http.Response.Headers.Append("Content-Type", "text/event-stream");
            http.Response.Headers.Append("Cache-Control", "no-cache");
            http.Response.Headers.Append("X-Accel-Buffering", "no");

            var fullContent = new System.Text.StringBuilder();
            await foreach (var token in gen.GenerateCharacterMessageAsync(story, character, req.Direction, ct))
            {
                fullContent.Append(token);
                var data = System.Text.Json.JsonSerializer.Serialize(new { token });
                await http.Response.WriteAsync($"data: {data}\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
            }

            // Save completed message
            if (fullContent.Length > 0)
            {
                var msg = new StoryMessage
                {
                    StoryId = id,
                    CharacterId = character.Id,
                    Content = fullContent.ToString(),
                    Type = MessageType.Dialogue,
                    UsedModel = character.ModelId,
                    GenerationHint = req.Direction
                };
                await svc.AddMessageAsync(msg, ct);
                var savedJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    done = true,
                    messageId = msg.Id,
                    sequence = msg.Sequence
                });
                await http.Response.WriteAsync($"data: {savedJson}\n\n", ct);
            }

            return Results.Empty;
        }).WithSummary("AI生成角色对话（流式SSE）");

        // ─── 旁白生成（流式） ──────────────────────────────

        group.MapPost("/{id:int}/narrate", async (
            int id,
            [FromBody] NarrateRequest? req,
            IStoryService svc,
            PlotGenerationService gen,
            HttpContext http,
            CancellationToken ct) =>
        {
            var story = await svc.GetStoryAsync(id, ct);
            if (story is null) return Results.NotFound();

            http.Response.Headers.Append("Content-Type", "text/event-stream");
            http.Response.Headers.Append("Cache-Control", "no-cache");

            var fullContent = new System.Text.StringBuilder();
            await foreach (var token in gen.GenerateNarrationAsync(story, req?.Direction, ct))
            {
                fullContent.Append(token);
                var data = System.Text.Json.JsonSerializer.Serialize(new { token });
                await http.Response.WriteAsync($"data: {data}\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
            }

            if (fullContent.Length > 0)
            {
                var msg = new StoryMessage
                {
                    StoryId = id,
                    Content = fullContent.ToString(),
                    Type = MessageType.Narration
                };
                await svc.AddMessageAsync(msg, ct);
                var savedJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    done = true,
                    messageId = msg.Id,
                    sequence = msg.Sequence
                });
                await http.Response.WriteAsync($"data: {savedJson}\n\n", ct);
            }

            return Results.Empty;
        }).WithSummary("生成旁白（流式SSE）");

        // ─── 手动添加消息 ──────────────────────────────────

        group.MapPost("/{id:int}/messages", async (
            int id, AddManualMessageRequest req, IStoryService svc, CancellationToken ct) =>
        {
            var type = Enum.TryParse<MessageType>(req.Type, out var t) ? t : MessageType.Dialogue;
            var msg = new StoryMessage
            {
                StoryId = id,
                CharacterId = req.CharacterId == 0 ? null : req.CharacterId,
                Content = req.Content,
                Type = type
            };
            var saved = await svc.AddMessageAsync(msg, ct);
            return Results.Created($"/api/stories/{id}/messages/{saved.Id}", saved);
        }).WithSummary("手动添加消息");

        group.MapDelete("/{id:int}/messages/from/{sequence:int}", async (
            int id, int sequence, IStoryService svc, CancellationToken ct) =>
        {
            await svc.DeleteMessagesFromSequenceAsync(id, sequence, ct);
            return Results.NoContent();
        }).WithSummary("删除指定序列号之后的所有消息");

        // ─── 重新生成 ──────────────────────────────────────

        group.MapPost("/{id:int}/regenerate", async (
            int id,
            RegenerateFromRequest req,
            IStoryService svc,
            PlotGenerationService gen,
            HttpContext http,
            CancellationToken ct) =>
        {
            var story = await svc.GetStoryAsync(id, ct);
            if (story is null) return Results.NotFound("Story not found");

            var character = story.Characters.FirstOrDefault(c => c.Id == req.CharacterId);
            if (character is null) return Results.NotFound("Character not found");

            // Delete messages from the given sequence
            await svc.DeleteMessagesFromSequenceAsync(id, req.FromSequence, ct);

            // Re-fetch story after deletion
            story = await svc.GetStoryAsync(id, ct) ?? story;

            http.Response.Headers.Append("Content-Type", "text/event-stream");
            http.Response.Headers.Append("Cache-Control", "no-cache");

            var fullContent = new System.Text.StringBuilder();
            await foreach (var token in gen.GenerateCharacterMessageAsync(story, character, req.Direction, ct))
            {
                fullContent.Append(token);
                var data = System.Text.Json.JsonSerializer.Serialize(new { token });
                await http.Response.WriteAsync($"data: {data}\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
            }

            if (fullContent.Length > 0)
            {
                var msg = new StoryMessage
                {
                    StoryId = id,
                    CharacterId = character.Id,
                    Content = fullContent.ToString(),
                    Type = MessageType.Dialogue,
                    UsedModel = character.ModelId,
                    GenerationHint = req.Direction
                };
                await svc.AddMessageAsync(msg, ct);
                var savedJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    done = true,
                    messageId = msg.Id,
                    sequence = msg.Sequence
                });
                await http.Response.WriteAsync($"data: {savedJson}\n\n", ct);
            }

            return Results.Empty;
        }).WithSummary("从指定位置重新生成");

        // ─── AI 审核 ───────────────────────────────────────

        group.MapPost("/{id:int}/review", async (
            int id, ReviewStoryRequest req, IStoryService svc, PlotGenerationService gen, CancellationToken ct) =>
        {
            var story = await svc.GetStoryAsync(id, ct);
            if (story is null) return Results.NotFound();

            var review = await gen.ReviewStoryAsync(story, req.Instructions, req.ModelId, ct);

            // Save review message
            var msg = new StoryMessage
            {
                StoryId = id,
                Content = review,
                Type = MessageType.Review
            };
            await svc.AddMessageAsync(msg, ct);

            return Results.Ok(new { review, messageId = msg.Id });
        }).WithSummary("AI审核故事剧情");

        // ─── 检查点 ────────────────────────────────────────

        group.MapGet("/{id:int}/checkpoints", async (int id, IStoryService svc, CancellationToken ct) =>
        {
            var story = await svc.GetStoryAsync(id, ct);
            return story is null ? Results.NotFound() : Results.Ok(story.Checkpoints);
        }).WithSummary("获取检查点列表");

        group.MapPost("/{id:int}/checkpoints", async (
            int id, CreateCheckpointRequest req, IStoryService svc, CancellationToken ct) =>
        {
            var cp = await svc.CreateCheckpointAsync(id, req.Name, req.MessageSequence, req.Description, ct);
            return Results.Created($"/api/stories/{id}/checkpoints/{cp.Id}", cp);
        }).WithSummary("创建检查点");

        group.MapPost("/{id:int}/checkpoints/{cpId:int}/restore", async (
            int id, int cpId, IStoryService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.RestoreCheckpointAsync(cpId, ct);
                return Results.Ok(new { message = "Checkpoint restored" });
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).WithSummary("恢复到检查点");

        group.MapDelete("/{id:int}/checkpoints/{cpId:int}", async (
            int id, int cpId, IStoryService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.DeleteCheckpointAsync(cpId, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).WithSummary("删除检查点");

        // ─── 世界书 ────────────────────────────────────────

        group.MapGet("/{id:int}/worldbook", async (int id, IStoryService svc, CancellationToken ct) =>
        {
            var wb = await svc.GetOrCreateWorldBookAsync(id, ct);
            return Results.Ok(wb);
        }).WithSummary("获取世界书");

        group.MapPut("/{id:int}/worldbook", async (
            int id, [FromBody] UpdateWorldBookRequest req, IStoryService svc, CancellationToken ct) =>
        {
            var wb = await svc.UpdateWorldBookAsync(id, req.Background, ct);
            return Results.Ok(wb);
        }).WithSummary("更新世界书背景");

        group.MapPost("/{id:int}/worldbook/entries", async (
            int id, CreateWorldEntryRequest req, IStoryService svc, CancellationToken ct) =>
        {
            var wb = await svc.GetOrCreateWorldBookAsync(id, ct);
            var entry = new WorldEntry
            {
                Title = req.Title,
                Content = req.Content,
                Category = req.Category,
                Keywords = req.Keywords
            };
            var saved = await svc.AddWorldEntryAsync(wb.Id, entry, ct);
            return Results.Created($"/api/stories/{id}/worldbook/entries/{saved.Id}", saved);
        }).WithSummary("添加世界书词条");

        group.MapPut("/{id:int}/worldbook/entries/{entryId:int}", async (
            int id, int entryId, UpdateWorldEntryRequest req, IStoryService svc, CancellationToken ct) =>
        {
            var story = await svc.GetStoryAsync(id, ct);
            if (story?.WorldBook is null) return Results.NotFound();
            var entry = story.WorldBook.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry is null) return Results.NotFound();

            entry.Title = req.Title;
            entry.Content = req.Content;
            entry.Category = req.Category;
            entry.Keywords = req.Keywords;

            var updated = await svc.UpdateWorldEntryAsync(entry, ct);
            return Results.Ok(updated);
        }).WithSummary("更新世界书词条");

        group.MapDelete("/{id:int}/worldbook/entries/{entryId:int}", async (
            int id, int entryId, IStoryService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.DeleteWorldEntryAsync(entryId, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        }).WithSummary("删除世界书词条");

        // ─── AI 生成世界书 ─────────────────────────────────

        group.MapPost("/{id:int}/worldbook/generate", async (
            int id,
            GenerateWorldBookRequest req,
            IStoryService svc,
            PlotGenerationService gen,
            CancellationToken ct) =>
        {
            var entries = await gen.GenerateWorldBookAsync(req.PromptText, req.ModelId, ct);
            var wb = await svc.GetOrCreateWorldBookAsync(id, ct);

            var saved = new List<WorldEntry>();
            foreach (var entry in entries)
            {
                var e = await svc.AddWorldEntryAsync(wb.Id, entry, ct);
                saved.Add(e);
            }
            return Results.Ok(saved);
        }).WithSummary("AI生成世界书词条");
    }
}

// Extra request types used only in this file
internal record NarrateRequest(string? Direction = null);
internal record UpdateWorldBookRequest(string Background);
