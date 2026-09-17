using Sudoku.Core;

namespace Sudoku.App.Game;

/// <summary>用户设置（持久化在 Preferences 中）。</summary>
public sealed class AppSettings
{
    private const string ThemeKey = "settings.theme";
    private const string AutoCandidatesKey = "settings.auto_candidates_on_new";
    private const string AutoRemoveNotesKey = "settings.auto_remove_notes";
    private const string HighlightSameKey = "settings.highlight_same";
    private const string HighlightUnitKey = "settings.highlight_unit";
    private const string HighlightConflictKey = "settings.highlight_conflict";
    private const string MistakeLimitKey = "settings.mistake_limit";
    private const string HapticsKey = "settings.haptics";

    /// <summary>主题偏好：System / Light / Dark。</summary>
    public AppTheme Theme { get; set; } = AppTheme.Unspecified;

    /// <summary>新局是否自动标记全部候选数。</summary>
    public bool AutoCandidatesOnNewGame { get; set; }

    /// <summary>落子后是否自动清理相关格的该候选数。</summary>
    public bool AutoRemoveNotes { get; set; } = true;

    /// <summary>同类数字高亮。</summary>
    public bool HighlightSameNumber { get; set; } = true;

    /// <summary>当前格所在行/列/宫高亮。</summary>
    public bool HighlightUnit { get; set; } = true;

    /// <summary>冲突（重复）高亮。</summary>
    public bool HighlightConflict { get; set; } = true;

    /// <summary>错误次数上限：0 表示不限。</summary>
    public int MistakeLimit { get; set; }

    /// <summary>震动反馈。</summary>
    public bool HapticsEnabled { get; set; }

    /// <summary>错误次数上限的可选值。</summary>
    public static IReadOnlyList<int> MistakeLimitOptions { get; } = new[] { 0, 3, 5 };

    /// <summary>错误次数上限的显示文本。</summary>
    public static string MistakeLimitText(int limit) => limit <= 0 ? "不限" : $"{limit} 次";

    /// <summary>从 Preferences 读取设置。</summary>
    public static AppSettings Load()
    {
        var settings = new AppSettings
        {
            Theme = (AppTheme)Preferences.Default.Get(ThemeKey, (int)AppTheme.Unspecified),
            AutoCandidatesOnNewGame = Preferences.Default.Get(AutoCandidatesKey, false),
            AutoRemoveNotes = Preferences.Default.Get(AutoRemoveNotesKey, true),
            HighlightSameNumber = Preferences.Default.Get(HighlightSameKey, true),
            HighlightUnit = Preferences.Default.Get(HighlightUnitKey, true),
            HighlightConflict = Preferences.Default.Get(HighlightConflictKey, true),
            MistakeLimit = Preferences.Default.Get(MistakeLimitKey, 0),
            HapticsEnabled = Preferences.Default.Get(HapticsKey, false),
        };

        return settings;
    }

    /// <summary>写入 Preferences。</summary>
    public void Save()
    {
        Preferences.Default.Set(ThemeKey, (int)Theme);
        Preferences.Default.Set(AutoCandidatesKey, AutoCandidatesOnNewGame);
        Preferences.Default.Set(AutoRemoveNotesKey, AutoRemoveNotes);
        Preferences.Default.Set(HighlightSameKey, HighlightSameNumber);
        Preferences.Default.Set(HighlightUnitKey, HighlightUnit);
        Preferences.Default.Set(HighlightConflictKey, HighlightConflict);
        Preferences.Default.Set(MistakeLimitKey, MistakeLimit);
        Preferences.Default.Set(HapticsKey, HapticsEnabled);
    }

    /// <summary>应用主题到全局。</summary>
    public void ApplyTheme()
    {
        if (Application.Current is not null)
        {
            Application.Current.UserAppTheme = Theme;
        }
    }
}
