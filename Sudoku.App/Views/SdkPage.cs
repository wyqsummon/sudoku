using Sudoku.App.Game;
using Sudoku.Core;

namespace Sudoku.App.Views;

/// <summary>
/// SDK 文本导入 / 导出页：粘贴一段 81 字符的盘面即可开新局（自动校验唯一解并评分），
/// 也可把当前对局的题面 / 局面 / 答案导出成文本或复制到剪贴板。
/// </summary>
public sealed class SdkPage : ContentPage
{
    private readonly Editor _input;
    private readonly Editor _output;
    private readonly Label _status;
    private bool _busy;

    public SdkPage()
    {
        Title = "SDK 导入导出";
        Ui.ApplyBackground(this);

        _input = new Editor
        {
            Placeholder = "把 81 个字符的盘面粘到这里：数字 1-9 为已知数，. 或 0 表示空格。\n也支持 Hodoku 风格的多行 / 带宫线排版。",
            HeightRequest = 120,
            FontSize = 14,
            AutoSize = EditorAutoSizeOption.Disabled,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };

        Button pasteButton = Ui.Tool("粘贴");
        pasteButton.Clicked += async (_, _) => await PasteAsync();

        Button clearInputButton = Ui.Tool("清空");
        clearInputButton.Clicked += (_, _) =>
        {
            _input.Text = string.Empty;
            SetStatus(string.Empty, false);
        };

        Button importButton = Ui.Primary("导入并开始");
        importButton.Clicked += async (_, _) => await ImportAsync();

        var inputTools = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
        };
        inputTools.Add(pasteButton, 0, 0);
        inputTools.Add(clearInputButton, 1, 0);
        inputTools.Add(importButton, 2, 0);

        _status = Ui.Body(string.Empty, 13);
        _status.LineBreakMode = LineBreakMode.WordWrap;

        _output = new Editor
        {
            Placeholder = "导出的文本会显示在这里，同时自动复制到剪贴板。",
            HeightRequest = 120,
            FontSize = 13,
            IsReadOnly = true,
            AutoSize = EditorAutoSizeOption.Disabled,
        };

        Button exportPuzzle = MakeExportButton("复制题面", ExportTarget.Puzzle);
        Button exportState = MakeExportButton("复制当前局面", ExportTarget.State);
        Button exportSolution = MakeExportButton("复制答案", ExportTarget.Solution);
        Button exportPretty = MakeExportButton("复制可读排版", ExportTarget.Pretty);

        var exportGrid = new Grid
        {
            RowSpacing = 8,
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
        };
        exportGrid.Add(exportPuzzle, 0, 0);
        exportGrid.Add(exportState, 1, 0);
        exportGrid.Add(exportSolution, 0, 1);
        exportGrid.Add(exportPretty, 1, 1);

        var stack = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(16, 20, 16, 16),
            Children =
            {
                Ui.Title("SDK 盘面", 30),
                Ui.Body("一行 81 个字符的文本格式，兼容 Hodoku 的 .sdk 写法", 13, TextAlignment.Center),
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        Ui.SectionHeader("导入"),
                        _input,
                        inputTools,
                        _status,
                    },
                }),
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        Ui.SectionHeader("导出"),
                        Ui.Body("导出当前对局的题面 / 当前局面（含已填入数字）/ 答案。", 13),
                        exportGrid,
                        _output,
                    },
                }),
                Ui.Body("导入会替换当前存档中的对局；校验项：字符集、81 个格值、盘面合法性（重复数字）、是否有解、是否唯一解。", 12),
            },
        };

        Content = new ScrollView { Content = stack };
    }

    private enum ExportTarget
    {
        Puzzle,
        State,
        Solution,
        Pretty,
    }

    private Button MakeExportButton(string text, ExportTarget target)
    {
        Button button = Ui.Tool(text);
        button.Clicked += async (_, _) => await ExportAsync(target);
        return button;
    }

    private async Task PasteAsync()
    {
        try
        {
            if (!Clipboard.Default.HasText)
            {
                SetStatus("剪贴板里没有文本。", true);
                return;
            }

            string? text = await Clipboard.Default.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus("剪贴板里的文本是空的。", true);
                return;
            }

            _input.Text = text.Trim();
            SetStatus($"已粘贴 {_input.Text.Length} 个字符，点「导入并开始」校验并开局。", false);
        }
        catch (Exception ex)
        {
            SetStatus($"读取剪贴板失败：{ex.Message}", true);
        }
    }

    private async Task ImportAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        bool replaced = AppState.Session is not null;
        SetStatus("正在校验盘面…", false);

        try
        {
            string text = _input.Text ?? string.Empty;
            SdkImportResult result = await Task.Run(() => SdkFormat.Import(text));

            if (!result.Success || result.Solution is null)
            {
                SetStatus(result.Message, true);
                return;
            }

            RatingReport rating = await Task.Run(() => DifficultyRating.Rate(result.Board));
            var puzzle = new Puzzle(
                result.Board,
                result.Solution,
                rating.Level,
                TechniqueInfo.Level(rating.Hardest),
                result.ClueCount)
            {
                Score = rating.Score,
            };

            GameSession session = GameSession.New(puzzle, AppState.Settings);
            AppState.Session = session;
            GameStorage.Save(session.ToSnapshot());

            string suffix = replaced ? "（原对局已被替换）" : string.Empty;
            SetStatus($"{result.Message} 评分 {rating.Score:0.0}，档位 {Difficulty.Name(rating.Level)}{suffix}", false);

            await Navigation.PushAsync(new GamePage(session));
        }
        catch (Exception ex)
        {
            SetStatus($"导入失败：{ex.Message}", true);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ExportAsync(ExportTarget target)
    {
        GameSession? session = AppState.Session;

        if (session is null)
        {
            // 没有打开棋盘时，用存档恢复一份会话，方便直接导出上一局
            GameSnapshot? snapshot = GameStorage.Load();
            if (snapshot is not null)
            {
                try
                {
                    session = GameSession.Restore(snapshot, AppState.Settings);
                }
                catch
                {
                    session = null;
                }
            }
        }

        if (session is null)
        {
            SetStatus("当前没有进行中的对局，先开一局或导入一道题。", true);
            return;
        }

        string text;
        string label;

        switch (target)
        {
            case ExportTarget.Puzzle:
                text = SdkFormat.Format(session.Puzzle.Given);
                label = $"已导出题面（{session.Puzzle.ClueCount} 个已知数）";
                break;

            case ExportTarget.State:
                Board current = session.CurrentBoard();
                text = SdkFormat.Format(current);
                label = $"已导出当前局面（已填 {current.FilledCount}/81）";
                break;

            case ExportTarget.Solution:
                text = SdkFormat.Format(session.Puzzle.Solution);
                label = "已导出答案";
                break;

            default:
                text = SdkFormat.FormatPretty(session.CurrentBoard());
                label = "已导出可读排版（带宫线）";
                break;
        }

        _output.Text = text;

        try
        {
            await Clipboard.Default.SetTextAsync(text);
            SetStatus($"{label}，并已复制到剪贴板。", false);
        }
        catch (Exception ex)
        {
            SetStatus($"{label}，但复制到剪贴板失败：{ex.Message}", true);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        _status.Text = message;
        _status.SetAppThemeColor(Label.TextColorProperty, isError ? Color.FromArgb("#C62828") : Ui.LightMuted, isError ? Color.FromArgb("#EF9A9A") : Ui.DarkMuted);
    }
}
