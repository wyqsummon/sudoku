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

        var hintCapOptions = AppSettings.HintLevelCapOptions.ToList();
        var hintCapPicker = new Picker
        {
            WidthRequest = 168,
            ItemsSource = hintCapOptions.Select(AppSettings.HintLevelCapText).ToList(),
            SelectedIndex = Math.Max(0, hintCapOptions.IndexOf(settings.HintLevelCap)),
        };
        hintCapPicker.SelectedIndexChanged += (_, _) =>
        {
            int index = Math.Clamp(hintCapPicker.SelectedIndex, 0, hintCapOptions.Count - 1);
            AppState.UpdateSettings(s => s.HintLevelCap = hintCapOptions[index]);
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
                        Ui.SettingRow("高阶提示", "开启后列出可用技巧（含带鳍鱼、唯一矩形、BUG+1 等）并逐步讲解推导；关闭只用最简单的提示：直接告诉你某格该填什么", Toggle(settings.AdvancedHints, v => AppState.UpdateSettings(s => s.AdvancedHints = v))),
                        Ui.SettingRow("应用后清除提示绘制", "点「应用这一步」之后，自动擦掉棋盘上为提示画的箭头、链与删除标记", Toggle(settings.ClearHintDrawingOnApply, v => AppState.UpdateSettings(s => s.ClearHintDrawingOnApply = v))),
                        Ui.SettingRow("显示手绘链", "显示你在候选数之间画的强链（实线）与弱链（虚线）", Toggle(settings.ShowLinks, v => AppState.UpdateSettings(s => s.ShowLinks = v))),
                        Ui.SettingRow("画链用弧线", "把链画成略带弧度的曲线，绕开直线路径上的候选数（同一格内的短链仍画直线）；关掉则一律画直线", Toggle(settings.CurvedLinks, v => AppState.UpdateSettings(s => s.CurvedLinks = v))),
                        Ui.SettingRow("提示技法上限", "高阶技巧较慢，可限制提示最多用到哪一档（11 起含 ALS-XZ、Sue de Coq、连续环）", hintCapPicker),
                    },
                }),

                Ui.SectionHeader("输入与交互"),
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 14,
                    Children =
                    {
                        Ui.SettingRow("填完的数字键自动隐藏", "某个数字 9 个都填完后，隐藏下方对应的数字键（数字锁定模式下同样生效）", Toggle(settings.HideCompletedDigits, v => AppState.UpdateSettings(s => s.HideCompletedDigits = v))),
                        Ui.SettingRow("高亮对应数字键", "选中格子后，用该格候选数高亮下方对应的数字键", Toggle(settings.HighlightDigitButtons, v => AppState.UpdateSettings(s => s.HighlightDigitButtons = v))),
                        Ui.SettingRow("高亮候选数位置", "选中或锁定数字时，在盘面上高亮它的候选数位置", Toggle(settings.HighlightCandidateNotes, v => AppState.UpdateSettings(s => s.HighlightCandidateNotes = v))),
                        Ui.SettingRow("双击填入唯一候选数", "Windows 上双击只有一个候选数的格子即直接填入", Toggle(settings.DoubleClickQuickFill, v => AppState.UpdateSettings(s => s.DoubleClickQuickFill = v))),
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
                        Ui.Body("· 点击格子选中，再按下方数字键填数；重复按同一数字可清除（没有橡皮按钮，退格 / Delete / 0 也能清除）。", 13),
                        Ui.Body("· 打开「笔记」后，数字键切换该格候选数。", 13),
                        Ui.Body("· 「自动标记」是开关：打开=按当前盘面补全候选数，关闭=清空候选数。", 13),
                        Ui.Body("· 「数字锁定」是开关：开启后点下方数字即锁定该数字（盘面上它的位置与候选数一起高亮），此时点格子就是把该数字填进去；关闭后点格子，会用该格的候选数高亮下方对应的数字键。数字键下方那行小灰字是该数字还剩几个没填，填满 9 个后自动跳到下一个还没填完的数字（锁定状态跟着走）并清掉原来那格的高亮，全部填完则取消锁定。", 13),
                        Ui.Body("· Windows 端在只有一个候选数的格子上双击，可直接填入那个候选数。", 13),
                        Ui.Body("· 「提示」分两种：设置里打开「高阶提示」后，先列出可用技巧和条数 → 选技巧 → 多路径可挑 → 逐步看推导，最后可选「应用这一步」，同时在棋盘上画出这一步的箭头、强弱链、涉及的格子（如带鳍鱼的鳍格、唯一矩形的四角）与要删除的候选数；关闭「高阶提示」则退化为最简单的提示——直接告诉你某格该填什么。提示面板就在底部（原来数字键的位置），打开时数字键与工具按钮会先收起让位。", 13),
                        Ui.Body("· 棋盘上的提示绘制：想让它在点「应用这一步」后自动消失，就保持「应用后清除提示绘制」开启；关掉则会留在盘面上供你照着填，下次点「提示」或关掉提示框时才擦掉。", 13),
                        Ui.Body("· 提示面板固定在底部：打开提示时数字键盘与工具按钮整块隐藏让位，手机竖屏也不会挡住九宫格；面板高度按屏幕自适应（最多占屏高四成多），内容多时列表自己滚动，点「收起」只留标题与按钮，关闭提示后按钮自动回来。", 13),
                        Ui.Body("· 提示按「当前生效的候选数」计算：规则推导的结果再扣掉你自己删掉的候选数——你手动排除过候选数之后，提示会跟着变（可能因此给出唯一候选数，也可能暂时没有技巧可用）。如果填错了数字，推导会失真，提示此时不可靠。", 13),
                        Ui.Body("· 「绘制」（或按 L）进入绘制模式：下方换成颜色与画笔；涂色点格子刷色，「画链」依次点两个候选数连线——按落笔顺序画成带箭头的实线（强链）/ 虚线（弱链），箭头指向第二个候选数；按 K 切强弱、按 T 切涂色/画链。数字键与长按锁定在绘制模式下依然可用，便于对照候选项。", 13),
                        Ui.Body("· 「清除绘制」一次清掉全部涂色与连线，可撤销。", 13),
                        Ui.Body("· 「撤销」在界面上方（也可用 Z）；「重做」没有按钮（和「重开」太像容易点错），只用快捷键 Y。", 13),
                        Ui.Body("· Windows 端支持键盘：1-9 填数、退格清除、方向键移动、N 笔记、L 绘制模式、T 切涂色/画链、K 切换强弱、Z 撤销、Y 重做、H 提示、P 暂停。", 13),
                    },
                }),

                Ui.Body("阶段二（技法实验室）：链级技法引擎、分段式提示、候选数强弱链画线、SER 风格难度评分与「专家」档、SDK 文本导入导出——五项均已完成。", 12),
                Ui.Body("阶段三（棋盘交互）：去掉橡皮、可拖动提示框、数字锁定与候选数高亮、双击快速填入（仅 Windows）、自动标记改为开关、对局管理操作上移、绘制模式（涂色 + 带箭头强弱链）、连线改细避免遮挡候选数——均已完成。", 12),
                Ui.Body("阶段四（高阶技巧）：带鳍 X 翼 / 带鳍剑鱼、唯一矩形、BUG+1、远程数对，提示时在盘面上画出对应的箭头与链，并新增「高阶提示」（默认关闭）与「应用后清除提示绘制」两项设置。", 12),
                Ui.Body("阶段五（大师档 + 手机提示布局）：新增「大师」难度——题目必须靠阶段四的高阶技巧才解得下去（专家档则不再需要这些技巧）；「提示」面板从浮层挪到底部按钮的位置，打开提示时数字键盘与工具按钮隐藏让位，面板整体压扁（约屏高两成）；开关类按钮改用整块高亮取代「✓」；数字键下方新增「该数字还剩几个」的小灰字，数字锁定模式下填满一个数字会自动锁定到下一个（并清掉选中格高亮）。", 12),
                Ui.Body("阶段六（提示高亮分色）：提示画在盘面上的标记按角色分色——结构一种色、结构里的各个候选数各一种色、鱼鳍单独一种色、待删候选数再用一种色（浅色主题也用深色，不用浅蓝这种在白色棋盘上会糊掉的颜色）；每个标记还带「第几条推导才出现」，棋盘跟着「下一步」逐段亮起；关掉提示或点「应用这一步」后立即清除。提示面板同时改成固定高度，推导行数变多不再把棋盘挤小。", 12),
                Ui.Body("阶段七（棋盘尺寸稳定 + XY 双值 + 绘图不遮涂色）：① 状态栏、工具行、绘制面板的高度全部定死，「点格子 / 点按钮 / 切绘制模式」棋盘尺寸一点不变（只有打开提示面板是有意的例外）；② 数字键最右侧新增「XY」按钮，按下即用青绿描边圈出盘面上只剩两个候选数的格子，它只与「当前锁定的那个数字键」互斥，不会关掉数字锁定；③ 绘图模式不再显示选中格，且涂好的颜色优先级最高——选中格与行列宫高亮都不会再盖住已涂色的格子。", 12),
                Ui.Body("阶段八（手机竖屏把空间留给棋盘）：绘制模式下收起数字键盘、对局页不再显示导航栏、窄屏把上下两排工具按钮与数字键盘压矮并把棋盘内边距收到 2 px——400×800 窗口下棋盘从约 340 涨到 400×400（几乎满宽），两排按钮之间的空当从 49 px 收到 6 px，切绘制模式棋盘尺寸依旧不变。", 12),
                Ui.Body("阶段九（十七数题型 + 高阶技巧 + 教程练习）：① 主菜单新增「十七数」难度——题面恰好 17 个提示数（数独理论下限、唯一解），而且内置题库只收录「必须用到高阶技巧」的题目（从公开的 17 提示数全目录里筛出：49157 道里约 1900 道纯逻辑可解且必须用高阶技巧），难度系数 6.4~9.2，不低于大师档；出题走内置母题 + 等价变换；② 高阶技巧新增 ALS-XZ、Sue de Coq、ALS 链、连续环（等级 11 / 12 / 13），提示时照常画结构与删除标记；③ 主菜单新增「技巧教程与专项练习」——按基础 / 进阶 / 高阶列出每种技巧的原理、怎么找、怎么做与真实例题，点「专项练习」把盘面直接停在只剩这一招的卡点上（练习局不覆盖「继续上一局」的存档，点「提示」会直奔这一招逐步讲解）。", 12),
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
