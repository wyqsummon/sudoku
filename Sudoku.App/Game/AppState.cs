using Sudoku.App.Views;

namespace Sudoku.App.Game;

/// <summary>应用级共享状态（当前设置与当前对局）。</summary>
public static class AppState
{
    /// <summary>当前设置。</summary>
    public static AppSettings Settings { get; private set; } = AppSettings.Load();

    /// <summary>当前对局（可为空）。</summary>
    public static GameSession? Session { get; set; }

    /// <summary>设置变化事件。</summary>
    public static event EventHandler? SettingsChanged;

    /// <summary>修改设置并立即生效（持久化 + 应用到主题与当前对局）。</summary>
    public static void UpdateSettings(Action<AppSettings> mutate)
    {
        mutate(Settings);
        Settings.Save();
        Settings.ApplyTheme();
        Session?.ApplySettings(Settings);
        SettingsChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>界面根导航。</summary>
    public static INavigation? Navigation => Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
}
