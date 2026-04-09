using AIPlot.Api;
using AIPlot.Components;
using AIPlot.Data;
using AIPlot.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  AIPlot — 多智能体剧情编写软件
//  基于 .NET 8 Minimal API + Blazor Server + OpenRouter API
//  当前代理模型：claude-sonnet-4-5
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// ─── 数据库（SQLite） ────────────────────────────────────────────────────────
var dbPath = Environment.GetEnvironmentVariable("AIPLOT_DB_PATH")
    ?? Path.Combine(builder.Environment.ContentRootPath, "aiplot.db");
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite($"Data Source={dbPath}"));

// ─── 应用服务 ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<AppSettingsService>();
builder.Services.AddHttpClient<IOpenRouterService, OpenRouterService>(c =>
{
    c.Timeout = TimeSpan.FromMinutes(5);
    c.DefaultRequestHeaders.Add("User-Agent", "AIPlot/1.0");
});
builder.Services.AddScoped<IStoryService, StoryService>();
builder.Services.AddScoped<PlotGenerationService>();

// ─── Blazor Server ───────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// ─── CORS（可选，供外部工具调用 API） ─────────────────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ─── OpenAPI ─────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ─── 数据库迁移 ──────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ─── HTTP Pipeline ───────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors();
app.UseAntiforgery();

// ─── Minimal API Endpoints ───────────────────────────────────────────────────
app.MapStoryEndpoints();
app.MapCharacterEndpoints();
app.MapModelAndSettingsEndpoints();

// ─── Blazor ──────────────────────────────────────────────────────────────────
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
