# 🎭 AIPlot — 多智能体剧情编写软件

> **当前代理模型：claude-sonnet-4-5**

基于 **ASP.NET Core 8 Minimal API + Blazor Server** 构建的多智能体 AI 剧情编写软件，支持 OpenRouter API 接入。

## ✨ 功能特性

| 功能 | 说明 |
|------|------|
| 🎭 **多角色 AI 扮演** | 为每个角色分配独立的 AI 模型，多 AI 协同演绎故事 |
| 📋 **AI 审核评估** | 引入专业审核 AI，评估剧情逻辑、角色一致性、给出改进建议 |
| 🔖 **检查点 / 重新生成** | 随时保存故事进度，从任意位置回滚或重新生成 |
| 🌍 **角色卡 & 世界书生成** | 从自然语言描述一键生成结构化角色卡和世界书词条 |
| 🌐 **OpenRouter API 接入** | 支持 GPT-4o、Claude 3.5、Gemini 等数百种模型 |
| 📡 **完整 REST API** | 所有功能均通过 Minimal API 暴露，支持第三方集成 |
| 🌊 **流式输出** | AI 生成内容实时流式展示（SSE） |

## 🚀 快速部署

### 方式一：Docker Compose（推荐）

```bash
# 克隆仓库
git clone https://github.com/YuanPeixi/Quest1-AIPlot.git
cd Quest1-AIPlot

# 启动服务（可选：通过环境变量传入 API Key）
OPENROUTER_API_KEY=sk-or-xxx docker compose up -d

# 访问
open http://localhost:8080
```

### 方式二：直接运行（需要 .NET 8 SDK）

```bash
cd src/AIPlot
dotnet run --framework net8.0
# 访问 http://localhost:5000
```

### 方式三：发布后运行

```bash
cd src/AIPlot
dotnet publish -c Release -o ./publish
cd publish
dotnet AIPlot.dll
```

## ⚙️ 配置

### API Key 配置（三种方式）

1. **Web 界面**：启动后访问 `/settings` 页面直接配置（推荐）
2. **环境变量**：`OPENROUTER_API_KEY=sk-or-xxx`
3. **appsettings.json**：

```json
{
  "OpenRouter": {
    "ApiKey": "sk-or-xxx",
    "DefaultModel": "openai/gpt-4o-mini"
  }
}
```

配置文件保存于 `user-settings.json`，重启后保留。

## 📡 API 文档

所有端点均在 `/api` 下，访问 `/api-docs` 查看完整文档。

### 核心端点

```
# 故事管理
GET    /api/stories              获取故事列表
POST   /api/stories              创建故事
GET    /api/stories/{id}         获取故事详情

# AI 生成（流式 SSE）
POST   /api/stories/{id}/generate    生成角色对话
POST   /api/stories/{id}/narrate     生成旁白
POST   /api/stories/{id}/regenerate  从指定位置重新生成

# AI 审核
POST   /api/stories/{id}/review      审核故事剧情

# 检查点
POST   /api/stories/{id}/checkpoints              创建检查点
POST   /api/stories/{id}/checkpoints/{cpId}/restore  恢复检查点

# 角色卡生成
POST   /api/characters/generate-card   从文本生成角色卡

# 世界书
POST   /api/stories/{id}/worldbook/generate  AI生成世界书词条

# 模型与设置
GET    /api/models               获取可用模型列表
POST   /api/settings             保存配置
```

## 🏗️ 技术架构

```
AIPlot/
├── Program.cs                 # 主入口（Minimal API + Blazor 配置）
├── Models/                    # 数据模型
│   ├── Story.cs               # 故事
│   ├── Character.cs           # 角色
│   ├── StoryMessage.cs        # 消息
│   ├── Checkpoint.cs          # 检查点
│   └── WorldBook.cs           # 世界书
├── Data/
│   └── AppDbContext.cs        # EF Core + SQLite
├── Services/
│   ├── IOpenRouterService.cs  # OpenRouter 接口
│   ├── OpenRouterService.cs   # HTTP 流式调用实现
│   ├── PlotGenerationService.cs  # 提示词构建 + AI 生成
│   ├── StoryService.cs        # 故事/角色/检查点 CRUD
│   └── AppSettingsService.cs  # 配置管理
├── Api/                       # Minimal API 端点
│   ├── StoryEndpoints.cs
│   ├── CharacterEndpoints.cs
│   └── ModelAndSettingsEndpoints.cs
└── Components/                # Blazor Server UI
    ├── Pages/
    │   ├── Home.razor          # 首页
    │   ├── Stories.razor       # 故事列表
    │   ├── StoryEditor.razor   # 故事编辑器（主功能页面）
    │   ├── CharacterGenerator.razor  # 角色卡生成
    │   ├── SettingsPage.razor  # 设置
    │   └── ApiDocs.razor       # API 文档
    └── Shared/
        ├── MessageBubble.razor # 消息气泡组件
        └── MessageActions.razor
```

### 技术栈

| 层次 | 技术 |
|------|------|
| 前端框架 | Blazor Server (.NET 8) |
| UI 组件库 | MudBlazor 7 |
| 后端 API | ASP.NET Core Minimal API |
| 数据库 | SQLite + Entity Framework Core 8 |
| AI 接口 | OpenRouter API (OpenAI 兼容格式) |
| 流式输出 | Server-Sent Events (SSE) |
| 容器化 | Docker + Docker Compose |

## 📝 使用流程

1. **配置 API Key** → `/settings`
2. **创建故事** → `/stories` → 点击「新建故事」
3. **添加角色** → 在故事编辑器右侧添加角色，设置性格、背景、AI 模型
4. **编写世界书** → 在「世界书」标签页输入世界设定，或让 AI 自动生成
5. **开始创作** → 选择角色，点击「AI生成」，AI 将扮演该角色进行对话
6. **创建检查点** → 保存当前进度，以便后续回滚
7. **AI 审核** → 点击「审核」按钮，获取 AI 对剧情的评价和建议

## 🤝 贡献

欢迎提交 Issue 和 PR！

## 📄 许可

MIT License
