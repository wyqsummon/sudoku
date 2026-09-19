using Sudoku.App.Game;
using Sudoku.Core;

namespace Sudoku.App.Views;

/// <summary>主菜单：继续游戏 / 新游戏（选择难度）/ 设置。</summary>
public sealed class MainPage : ContentPage
{
    private readonly Button _continueButton;
    private readonly Label _statusLabel;
    private readonly ActivityIndicator _busyIndicator;
    private readonly Label _busyLabel;
    private readonly Grid _busyOverlay;
    private bool _busy;

    public MainPage()
    {
        Title = "数独";
        Ui.ApplyBackground(this);

        _continueButton = Ui.Primary("继续上一局");
        _continueButton.Clicked += async (_, _) => await ContinueAsync();

        var easy = Ui.Secondary("简单");
        var medium = Ui.Secondary("中等");
        var hard = Ui.Secondary("困难");
        var expert = Ui.Secondary("专家");
        var master = Ui.Secondary("大师");
        var seventeen = Ui.Secondary("十七数");
        easy.Clicked += async (_, _) => await StartNewGameAsync(DifficultyLevel.Easy);
        medium.Clicked += async (_, _) => await StartNewGameAsync(DifficultyLevel.Medium);
        hard.Clicked += async (_, _) => await StartNewGameAsync(DifficultyLevel.Hard);
        expert.Clicked += async (_, _) => await StartNewGameAsync(DifficultyLevel.Expert);
        master.Clicked += async (_, _) => await StartNewGameAsync(DifficultyLevel.Master);
        seventeen.Clicked += async (_, _) => await StartNewGameAsync(DifficultyLevel.Seventeen);

        // 六个档位分两行三列：上面三个基础档（简单 / 中等 / 困难），下面三个高难档（专家 / 大师 / 十七数）
        var difficultyRow = new Grid
        {
            ColumnSpacing = 6,
            RowSpacing = 6,
        };
        for (int i = 0; i < 3; i++)
        {
            difficultyRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        difficultyRow.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        difficultyRow.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        difficultyRow.Add(easy, 0, 0);
        difficultyRow.Add(medium, 1, 0);
        difficultyRow.Add(hard, 2, 0);
        difficultyRow.Add(expert, 0, 1);
        difficultyRow.Add(master, 1, 1);
        difficultyRow.Add(seventeen, 2, 1);

        var settingsButton = Ui.Tool("设置");
        settingsButton.Clicked += async (_, _) => await Navigation.PushAsync(new SettingsPage());

        Button tutorialButton = Ui.Secondary("技巧教程与专项练习");
        tutorialButton.Clicked += async (_, _) => await Navigation.PushAsync(new TutorialPage());

        Button importButton = Ui.Secondary("导入 SDK 盘面");
        importButton.Clicked += async (_, _) => await Navigation.PushAsync(new SdkPage());

        _statusLabel = Ui.Body(string.Empty, 13, TextAlignment.Center);

        var stack = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(20, 26, 20, 20),
            Children =
            {
                Ui.Title("数独", 36),
                Ui.Body("经典 9×9 · 唯一解出题 · 深浅双主题", 14, TextAlignment.Center),
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        Ui.SectionHeader("继续游戏"),
                        _continueButton,
                    },
                }),
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        Ui.SectionHeader("新游戏"),
                        difficultyRow,
                        Ui.Body(string.Join('\n', Difficulty.All.Select(l => $"{Difficulty.Name(l)}：{Difficulty.Description(l)}")), 13),
                    },
                }),
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        Ui.SectionHeader("学习 / 专项练习"),
                        tutorialButton,
                        Ui.Body("按「基础 / 进阶 / 高阶」列出全部技巧：原理、怎么找、怎么做，外加一道真实例题。「专项练习」会把盘面直接停在只剩这一招的卡点上，让你在实战位置练这一招；练废了也不影响「继续上一局」的存档。", 13),
                    },
                }),
                Ui.Card(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        Ui.SectionHeader("题库 / 导入"),
                        importButton,
                        Ui.Body("支持 SDK 文本（81 个字符，. 或 0 表示空格）与 Hodoku 的 .sdk 写法：多行题库、# 注释行、带宫线的可读排版都能直接粘贴。导入时自动校验合法性、唯一解并给出评分。", 13),
                    },
                }),
                settingsButton,
                _statusLabel,
                Ui.Body("第一版 v0.1 · 基于 .NET MAUI", 12, TextAlignment.Center),
            },
        };

        _busyIndicator = new ActivityIndicator { IsRunning = false, Color = Ui.LightAccent, HorizontalOptions = LayoutOptions.Center };
        _busyLabel = Ui.Body("正在生成题目…", 14, TextAlignment.Center);

        var busyCard = Ui.Card(new VerticalStackLayout
        {
            Spacing = 12,
            Children = { _busyIndicator, _busyLabel },
        }, 22);
        busyCard.HorizontalOptions = LayoutOptions.Center;
        busyCard.VerticalOptions = LayoutOptions.Center;

        _busyOverlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = Color.FromArgb("#88000000"),
            Children = { busyCard },
        };

        Content = new Grid { Children = { new ScrollView { Content = stack }, _busyOverlay } };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _continueButton.IsEnabled = GameStorage.HasSavedGame();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        GameSnapshot? snapshot = GameStorage.Load();
        if (snapshot is null)
        {
            _statusLabel.Text = "还没有进行中的对局，选一个难度开始吧。";
            return;
        }

        string progress = $"{81 - snapshot.Values.Count(v => v == 0)}/81";
        string rating = snapshot.DifficultyScore > 0 ? $" · 评分 {snapshot.DifficultyScore:0.0}" : string.Empty;
        _statusLabel.Text = $"上一局：{Difficulty.Name(snapshot.Level)}{rating} · 已填 {progress} · 用时 {TimeSpan.FromSeconds(snapshot.ElapsedSeconds):mm\\:ss}";
    }

    private async Task ContinueAsync()
    {
        GameSnapshot? snapshot = GameStorage.Load();
        if (snapshot is null)
        {
            _statusLabel.Text = "没有找到可继续的对局。";
            return;
        }

        try
        {
            GameSession session = GameSession.Restore(snapshot, AppState.Settings);
            AppState.Session = session;
            await Navigation.PushAsync(new GamePage(session));
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"读取存档失败：{ex.Message}";
        }
    }

    private async Task StartNewGameAsync(DifficultyLevel level)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        // 大师档要求「必须用到高阶技巧」，出题要反复挖洞+试解，所以给它更大的重试预算。
        // 十七数走内置母题（已按「必须用高阶技巧」筛过）+ 等价变换，代价很低，不需要放大预算。
        int attempts = level == DifficultyLevel.Master ? 200 : 40;
        SetBusy(true, level == DifficultyLevel.Master
            ? "正在生成大师题目…（这一档更慢，最多几秒）"
            : level == DifficultyLevel.Seventeen
                ? "正在生成十七数题目…（要用到 ALS / BUG+1 等高阶技巧）"
                : $"正在生成{Difficulty.Name(level)}题目…");

        try
        {
            Puzzle puzzle = await Task.Run(() => new Generator().Generate(level, attempts));
            GameSession session = GameSession.New(puzzle, AppState.Settings);
            AppState.Session = session;
            GameStorage.Save(session.ToSnapshot());
            SetBusy(false, string.Empty);
            await Navigation.PushAsync(new GamePage(session));
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"生成失败：{ex.Message}";
        }
        finally
        {
            _busy = false;
            SetBusy(false, string.Empty);
        }
    }

    private void SetBusy(bool busy, string message)
    {
        _busyOverlay.IsVisible = busy;
        _busyIndicator.IsRunning = busy;
        _busyLabel.Text = message;
    }
}
