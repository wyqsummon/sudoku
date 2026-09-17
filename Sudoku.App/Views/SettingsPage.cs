using Sudoku.App.Game;

namespace Sudoku.App.Views;

/// <summary>设置页：主题、候选数、高亮、错误次数上限、震动反馈。</summary>
public sealed class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        Title = "设置";
        Ui.ApplyBackground(this);

        AppSettings settings = AppState.Settings;

        var themePicker = new Picker
        {
            WidthRequest = 140,
            ItemsSource = new List<string> { "跟随系统", "浅色", "深色" },
            SelectedIndex = settings.Theme switch
            {
                AppTheme.Light => 1,
                AppTheme.Dark => 2,
                _ => 0,
            },
        };
        themePicker.SelectedIndexChanged += (_, _) => AppState.UpdateSettings(s => s.Theme = themePicker.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        });

        var mistakeOptions = AppSettings.MistakeLimitOptions.ToList();
        var mistakePicker = new Picker
        {
            WidthRequest = 140,
            ItemsSource = mistakeOptions.Select(AppSettings.MistakeLimitText).ToList(),
            SelectedIndex = Math.Max(0, mistakeOptions.IndexOf(settings.MistakeLimit)),
        };
        mistakePicker.SelectedIndexChanged += (_, _) =>
        {
            int index = Math.Clamp(mistakePicker.SelectedIndex, 0, mistakeOptions.Count - 1);
            AppState.UpdateSettings(s => s.MistakeLimit = mistakeOptions[index]);
        };

        var stack = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(18, 16, 18, 24),
            Children =
            {
                Ui.SectionHeader("外观"),
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 14,
                    Children =
                    {
                        Ui.SettingRow("主题", "跟随系统或手动指定", themePicker),
                    },
                }),

                Ui.SectionHeader("候选数"),
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 14,
                    Children =
                    {
                        Ui.SettingRow("新局自动标记候选数", "开局即按盘面填好全部候选数", Toggle(settings.AutoCandidatesOnNewGame, v => AppState.UpdateSettings(s => s.AutoCandidatesOnNewGame = v))),
                        Ui.SettingRow("落子后自动清理候选数", "填入正确数字后，同步删去同行列宫的该候选数", Toggle(settings.AutoRemoveNotes, v => AppState.UpdateSettings(s => s.AutoRemoveNotes = v))),
                    },
                }),

                Ui.SectionHeader("高亮与提示"),
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 14,
                    Children =
                    {
                        Ui.SettingRow("同类数字高亮", "点击数字或格子时高亮全盘相同数字", Toggle(settings.HighlightSameNumber, v => AppState.UpdateSettings(s => s.HighlightSameNumber = v))),
                        Ui.SettingRow("相关行/列/宫高亮", "高亮当前格所在的行、列、宫", Toggle(settings.HighlightUnit, v => AppState.UpdateSettings(s => s.HighlightUnit = v))),
                        Ui.SettingRow("冲突高亮", "重复或填错的数字以红色显示", Toggle(settings.HighlightConflict, v => AppState.UpdateSettings(s => s.HighlightConflict = v))),
                    },
                }),

                Ui.SectionHeader("规则"),
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 14,
                    Children =
                    {
                        Ui.SettingRow("错误次数上限", "达到上限即判负；默认不限（教学向）", mistakePicker),
                        Ui.SettingRow("震动反馈", "落子时轻微震动（部分设备支持）", Toggle(settings.HapticsEnabled, v => AppState.UpdateSettings(s => s.HapticsEnabled = v))),
                    },
                }),

                Ui.SectionHeader("操作说明"),
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        Ui.Body("· 点击格子选中，再按下方数字键填数；重复按同一数字可清除。", 13),
                        Ui.Body("· 打开「笔记」后，数字键切换该格候选数。", 13),
                        Ui.Body("· 「自动标记」按当前盘面一键填好全部候选数，「清除标记」一键清空。", 13),
                        Ui.Body("· 「提示」会高亮一格并给出正确答案，不自动填入。", 13),
                        Ui.Body("· Windows 端支持键盘：1-9 填数、退格清除、方向键移动、N 笔记、Z 撤销、Y 重做、H 提示、P 暂停。", 13),
                    },
                }),

                Ui.Body("阶段二将加入：候选数强弱链画线、分段式提示（多路径可选）、技法驱动的难度评分与盘面导入导出。", 12),
            },
        };

        Content = new ScrollView { Content = stack };
    }

    private static Switch Toggle(bool value, Action<bool> onChange)
    {
        var toggle = new Switch
        {
            IsToggled = value,
            VerticalOptions = LayoutOptions.Center,
        };
        toggle.Toggled += (_, e) => onChange(e.Value);
        return toggle;
    }
}
