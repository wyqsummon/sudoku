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
    private const string AdvancedHintsKey = "settings.advanced_hints";
    private const string ClearHintDrawingKey = "settings.clear_hint_drawing_on_apply";
    private const string ShowLinksKey = "settings.show_links";
    private const string CurvedLinksKey = "settings.curved_links";
    private const string HintLevelCapKey = "settings.hint_level_cap";
    private const string HideCompletedDigitsKey = "settings.hide_completed_digits";
    private const string HighlightDigitButtonsKey = "settings.highlight_digit_buttons";
    private const string HighlightCandidateNotesKey = "settings.highlight_candidate_notes";
    private const string DoubleClickQuickFillKey = "settings.double_click_quick_fill";

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

    /// <summary>高阶提示：按技巧分步讲解（含带鳍鱼、唯一矩形、BUG+1 等高阶技巧）；关闭时退化为最简单的提示。</summary>
    public bool AdvancedHints { get; set; }

    /// <summary>应用技巧后，自动擦掉棋盘上为提示画的箭头与链。</summary>
    public bool ClearHintDrawingOnApply { get; set; } = true;

    /// <summary>是否在棋盘上显示手绘的强弱链。</summary>
    public bool ShowLinks { get; set; } = true;

    /// <summary>
    /// 画链是否用带弧度的曲线：直线横跨几格时会从沿途格子里的候选数上压过去，
    /// 弧线绕开路径上的候选数（同一格内的短链仍然画直线）。
    /// </summary>
    public bool CurvedLinks { get; set; } = true;

    /// <summary>提示可用的最高技法等级（6~13，13 表示含连续环 / ALS 链）。</summary>
    public int HintLevelCap { get; set; } = 10;

    /// <summary>提示技法等级上限的可选值。</summary>
    public static IReadOnlyList<int> HintLevelCapOptions { get; } = new[] { 6, 7, 8, 9, 10, 11, 12, 13 };

    /// <summary>某个数字 1~9 已经全部填完时，隐藏下方对应的数字键（数字锁定模式除外）。</summary>
    public bool HideCompletedDigits { get; set; } = true;

    /// <summary>选中格子后，用该格候选数高亮下方对应的数字键。</summary>
    public bool HighlightDigitButtons { get; set; } = true;

    /// <summary>在棋盘上高亮选中/锁定数字的候选数位置。</summary>
    public bool HighlightCandidateNotes { get; set; } = true;

    /// <summary>Windows 上双击只有一个候选数的格子即直接填入。</summary>
    public bool DoubleClickQuickFill { get; set; } = true;

    /// <summary>提示技法等级上限的显示文本。</summary>
    public static string HintLevelCapText(int cap) => cap switch
    {
        <= 6 => "≤6 不含链",
        7 => "≤7 含 X 链",
        8 => "≤8 含 XY 链",
        9 => "≤9 含 AIC",
        10 => "≤10 含 BUG+1",
        11 => "≤11 含 ALS-XZ",
        12 => "≤12 含 Sue de Coq",
        _ => "≤13 含 ALS 链 / 连续环",
    };

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
            AdvancedHints = Preferences.Default.Get(AdvancedHintsKey, false),
            ClearHintDrawingOnApply = Preferences.Default.Get(ClearHintDrawingKey, true),
            ShowLinks = Preferences.Default.Get(ShowLinksKey, true),
            CurvedLinks = Preferences.Default.Get(CurvedLinksKey, true),
            HintLevelCap = Preferences.Default.Get(HintLevelCapKey, 10),
            HideCompletedDigits = Preferences.Default.Get(HideCompletedDigitsKey, true),
            HighlightDigitButtons = Preferences.Default.Get(HighlightDigitButtonsKey, true),
            HighlightCandidateNotes = Preferences.Default.Get(HighlightCandidateNotesKey, true),
            DoubleClickQuickFill = Preferences.Default.Get(DoubleClickQuickFillKey, true),
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
        Preferences.Default.Set(AdvancedHintsKey, AdvancedHints);
        Preferences.Default.Set(ClearHintDrawingKey, ClearHintDrawingOnApply);
        Preferences.Default.Set(ShowLinksKey, ShowLinks);
        Preferences.Default.Set(CurvedLinksKey, CurvedLinks);
        Preferences.Default.Set(HintLevelCapKey, HintLevelCap);
        Preferences.Default.Set(HideCompletedDigitsKey, HideCompletedDigits);
        Preferences.Default.Set(HighlightDigitButtonsKey, HighlightDigitButtons);
        Preferences.Default.Set(HighlightCandidateNotesKey, HighlightCandidateNotes);
        Preferences.Default.Set(DoubleClickQuickFillKey, DoubleClickQuickFill);
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
