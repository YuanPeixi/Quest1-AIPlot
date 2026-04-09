namespace AIPlot.Services;

/// <summary>
/// 应用全局设置服务（OpenRouter API Key 等）
/// 在内存中保存，同时持久化到 appsettings 或本地文件
/// </summary>
public class AppSettingsService
{
    private readonly IConfiguration _config;
    private readonly string _settingsFile;
    private readonly ILogger<AppSettingsService> _logger;

    public string ApiKey { get; private set; } = string.Empty;
    public string DefaultModel { get; private set; } = "openai/gpt-4o-mini";
    public string SiteUrl { get; private set; } = "https://github.com/YuanPeixi/Quest1-AIPlot";
    public string SiteName { get; private set; } = "AIPlot Story Writer";

    /// <summary>当前使用的模型名称（用于UI展示）</summary>
    public const string AgentModel = "claude-sonnet-4-5";

    public event Action? OnChange;

    public AppSettingsService(IConfiguration config, ILogger<AppSettingsService> logger)
    {
        _config = config;
        _logger = logger;
        _settingsFile = Path.Combine(AppContext.BaseDirectory, "user-settings.json");
        LoadSettings();
    }

    private void LoadSettings()
    {
        // 优先从本地文件加载
        if (File.Exists(_settingsFile))
        {
            try
            {
                var json = File.ReadAllText(_settingsFile);
                var data = System.Text.Json.JsonSerializer.Deserialize<SettingsData>(json);
                if (data != null)
                {
                    ApiKey = data.ApiKey ?? string.Empty;
                    DefaultModel = data.DefaultModel ?? DefaultModel;
                    SiteUrl = data.SiteUrl ?? SiteUrl;
                    SiteName = data.SiteName ?? SiteName;
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load user-settings.json");
            }
        }

        // 从 appsettings / 环境变量加载
        ApiKey = _config["OpenRouter:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? string.Empty;
        DefaultModel = _config["OpenRouter:DefaultModel"] ?? DefaultModel;
    }

    public void UpdateSettings(string apiKey, string defaultModel, string siteUrl = "", string siteName = "")
    {
        ApiKey = apiKey;
        DefaultModel = defaultModel;
        if (!string.IsNullOrWhiteSpace(siteUrl)) SiteUrl = siteUrl;
        if (!string.IsNullOrWhiteSpace(siteName)) SiteName = siteName;

        SaveToFile();
        OnChange?.Invoke();
    }

    private void SaveToFile()
    {
        try
        {
            var data = new SettingsData(ApiKey, DefaultModel, SiteUrl, SiteName);
            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    private record SettingsData(string? ApiKey, string? DefaultModel, string? SiteUrl, string? SiteName);
}
