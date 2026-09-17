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
        easy.Clicked += async (_, _) => await StartNewGameAsync(DifficultyLevel.Easy);
        medium.Clicked += async (_, _) => await StartNewGameAsync(DifficultyLevel.Medium);
        hard.Clicked += async (_, _) => await StartNewGameAsync(DifficultyLevel.Hard);

        var difficultyRow = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
        };
        difficultyRow.Add(easy, 0, 0);
        difficultyRow.Add(medium, 1, 0);
        difficultyRow.Add(hard, 2, 0);

        var settingsButton = Ui.Tool("设置");
        settingsButton.Clicked += async (_, _) => await Navigation.PushAsync(new SettingsPage());

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
                        Ui.Body("简单：只需唯一候选数即可完成\n中等：需要区块摒除与数对\n困难：需要三数组或更高级技巧", 13),
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
        _statusLabel.Text = $"上一局：{Difficulty.Name(snapshot.Level)} · 已填 {progress} · 用时 {TimeSpan.FromSeconds(snapshot.ElapsedSeconds):mm\\:ss}";
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
        SetBusy(true, $"正在生成{Difficulty.Name(level)}题目…");

        try
        {
            Puzzle puzzle = await Task.Run(() => new Generator().Generate(level));
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
