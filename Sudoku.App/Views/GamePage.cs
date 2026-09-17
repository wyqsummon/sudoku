using Microsoft.Maui.Devices;
using Sudoku.App.Drawing;
using Sudoku.App.Game;
using Sudoku.Core;

namespace Sudoku.App.Views;

/// <summary>对局界面：棋盘、数字面板、工具条与各类浮层。</summary>
public sealed class GamePage : ContentPage
{
    private readonly GameSession _session;
    private readonly BoardDrawable _drawable = new();
    private readonly GraphicsView _boardView;
    private readonly Label _levelLabel;
    private readonly Label _timerLabel;
    private readonly Label _progressLabel;
    private readonly Label _mistakeLabel;
    private readonly Label _statusLabel;
    private readonly Button _undoButton;
    private readonly Button _redoButton;
    private readonly Button _noteButton;
    private readonly Button _pauseButton;
    private readonly Grid _pauseOverlay;
    private readonly Grid _resultOverlay;
    private readonly Label _resultTitle;
    private readonly Label _resultDetail;
    private readonly Button _resultPrimary;
    private readonly Button _resultSecondary;
    private readonly IDispatcherTimer _timer;
    private int _tickCounter;
    private bool _resultDismissed;

#if WINDOWS
    private bool _keyboardHooked;
#endif

    public GamePage(GameSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _drawable.Session = _session;

        Title = $"{_session.LevelName} · 数独";
        Ui.ApplyBackground(this);

        // ---------- 顶部信息条 ----------
        _levelLabel = StatLabel(_session.LevelName);
        _timerLabel = StatLabel(_session.ElapsedText);
        _progressLabel = StatLabel(_session.ProgressText);
        _mistakeLabel = StatLabel(_session.MistakeText);

        _pauseButton = Ui.Tool("暂停");
        _pauseButton.WidthRequest = 74;
        _pauseButton.Clicked += (_, _) => OnPause();

        var headerGrid = new Grid
        {
            ColumnSpacing = 10,
            Padding = new Thickness(12, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
            },
        };
        headerGrid.Add(_pauseButton, 0, 0);
        headerGrid.Add(_timerLabel, 1, 0);
        headerGrid.Add(_progressLabel, 2, 0);
        headerGrid.Add(_mistakeLabel, 3, 0);
        headerGrid.Add(_levelLabel, 4, 0);

        // ---------- 棋盘 ----------
        _boardView = new GraphicsView { Drawable = _drawable };
        _boardView.StartInteraction += OnBoardTouch;

        _pauseOverlay = BuildOverlay("已暂停", "计时已停止，点继续回到棋盘", out _, out Button resumeButton, out Button pauseSecondary, out _);
        resumeButton.Clicked += (_, _) => OnPause();
        pauseSecondary.IsVisible = false;

        _resultOverlay = BuildOverlay("完成！", string.Empty, out _resultTitle, out _resultPrimary, out _resultSecondary, out _resultDetail);
        _resultPrimary.Clicked += (_, _) => OnResultPrimary();
        _resultSecondary.Clicked += async (_, _) => await Navigation.PopToRootAsync();

        var boardArea = new Grid
        {
            Padding = new Thickness(10, 4),
            Children = { _boardView, _pauseOverlay, _resultOverlay },
        };

        // ---------- 数字键盘 ----------
        var digitsGrid = new Grid
        {
            ColumnSpacing = 6,
            Padding = new Thickness(10, 2),
        };
        for (int i = 0; i < SudokuGrid.Size; i++)
        {
            digitsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (int digit = 1; digit <= SudokuGrid.Size; digit++)
        {
            Button button = Ui.Digit(digit.ToString());
            int captured = digit;
            button.Clicked += (_, _) => OnDigit(captured);
            digitsGrid.Add(button, digit - 1, 0);
        }

        // ---------- 工具条 ----------
        _undoButton = Ui.Tool("撤销");
        _undoButton.Clicked += (_, _) => OnUndo();

        _redoButton = Ui.Tool("重做");
        _redoButton.Clicked += (_, _) => OnRedo();

        _noteButton = Ui.Tool("笔记");
        _noteButton.Clicked += (_, _) => OnToggleNoteMode();

        Button eraseButton = Ui.Tool("橡皮");
        eraseButton.Clicked += (_, _) => OnErase();

        Button hintButton = Ui.Tool("提示");
        hintButton.Clicked += (_, _) => OnHint();

        var toolsGrid = new Grid
        {
            ColumnSpacing = 6,
            Padding = new Thickness(10, 2),
        };
        for (int i = 0; i < 5; i++)
        {
            toolsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        toolsGrid.Add(_undoButton, 0, 0);
        toolsGrid.Add(_redoButton, 1, 0);
        toolsGrid.Add(_noteButton, 2, 0);
        toolsGrid.Add(eraseButton, 3, 0);
        toolsGrid.Add(hintButton, 4, 0);

        // ---------- 更多操作 ----------
        Button autoButton = Ui.Tool("自动标记");
        autoButton.Clicked += (_, _) => OnAutoCandidates();

        Button clearButton = Ui.Tool("清除标记");
        clearButton.Clicked += (_, _) => OnClearCandidates();

        Button restartButton = Ui.Tool("重开");
        restartButton.Clicked += (_, _) => OnRestart();

        Button newGameButton = Ui.Tool("新对局");
        newGameButton.Clicked += async (_, _) => await Navigation.PopToRootAsync();

        Button settingsButton = Ui.Tool("设置");
        settingsButton.Clicked += async (_, _) => await Navigation.PushAsync(new SettingsPage());

        var actionsGrid = new Grid
        {
            ColumnSpacing = 6,
            Padding = new Thickness(10, 2),
        };
        for (int i = 0; i < 5; i++)
        {
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        actionsGrid.Add(autoButton, 0, 0);
        actionsGrid.Add(clearButton, 1, 0);
        actionsGrid.Add(restartButton, 2, 0);
        actionsGrid.Add(newGameButton, 3, 0);
        actionsGrid.Add(settingsButton, 4, 0);

        _statusLabel = Ui.Body(string.Empty, 13, TextAlignment.Center);
        _statusLabel.Margin = new Thickness(12, 2, 12, 8);

        var layout = new Grid
        {
            RowSpacing = 2,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
        };

        layout.Add(headerGrid, 0, 0);
        layout.Add(boardArea, 0, 1);
        layout.Add(digitsGrid, 0, 2);
        layout.Add(toolsGrid, 0, 3);
        layout.Add(actionsGrid, 0, 4);
        layout.Add(_statusLabel, 0, 5);

        Content = layout;

        _session.PropertyChanged += (_, e) => OnSessionPropertyChanged(e.PropertyName);
        _session.BoardChanged += (_, _) => OnSessionBoardChanged();

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => OnTimerTick();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _timer.Start();
        HookKeyboard();
        RefreshAll();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer.Stop();
        SaveGame();
    }

    // ---------- 输入处理 ----------

    private void OnBoardTouch(object? sender, TouchEventArgs e)
    {
        if (!_session.CanPlay || e.Touches.Length == 0)
        {
            return;
        }

        int cell = _drawable.HitTest(e.Touches[0]);
        if (cell >= 0)
        {
            _session.Select(cell);
        }
    }

    private void OnDigit(int digit)
    {
        if (!_session.CanPlay)
        {
            return;
        }

        if (_session.SelectedCell < 0)
        {
            SetStatus("先点选一个格子，再按数字键。");
            return;
        }

        int cell = _session.SelectedCell;
        InputOutcome outcome = _session.Input(digit);

        switch (outcome)
        {
            case InputOutcome.Placed:
                if (_session.IsWrongEntry(cell))
                {
                    SetStatus("这个数字与答案不符，可撤销后重试。");
                }
                else if (_session.HasConflict(cell))
                {
                    SetStatus("同行/列/宫出现重复，需要调整。");
                }
                else
                {
                    SetStatus(string.Empty);
                }

                Haptic();
                break;

            case InputOutcome.NoteChanged:
                SetStatus(string.Empty);
                break;

            case InputOutcome.Erased:
                SetStatus(string.Empty);
                break;
        }

        if (_session.IsCompleted)
        {
            SetStatus($"恭喜完成！用时 {_session.ElapsedText}，错误 {_session.MistakeCount} 次，提示 {_session.HintsUsed} 次。");
        }
        else if (_session.IsFailed)
        {
            SetStatus("错误次数已用尽，本局结束。");
        }

        SaveGame();
        RefreshHeader();
        RefreshOverlays();
    }

    private void OnErase()
    {
        if (!_session.CanPlay)
        {
            return;
        }

        if (_session.Erase() == InputOutcome.Ignored)
        {
            SetStatus("先用橡皮：选中一个已填的格子。");
            return;
        }

        SetStatus(string.Empty);
        SaveGame();
        RefreshHeader();
    }

    private void OnUndo()
    {
        _session.Undo();
        SetStatus(string.Empty);
        SaveGame();
    }

    private void OnRedo()
    {
        _session.Redo();
        SetStatus(string.Empty);
        SaveGame();
    }

    private void OnToggleNoteMode()
    {
        _session.ToggleNoteMode();
        SetStatus(_session.NoteMode ? "笔记模式：数字键切换候选数。" : "填数模式：数字键直接填入。");
    }

    private void OnAutoCandidates()
    {
        if (!_session.CanPlay)
        {
            return;
        }

        _session.FillAllCandidates();
        SetStatus("已按当前盘面标记全部候选数。");
        SaveGame();
    }

    private void OnClearCandidates()
    {
        _session.ClearAllCandidates();
        SetStatus("已清空全部候选数标记。");
        SaveGame();
    }

    private void OnHint()
    {
        HintInfo? hint = _session.RevealHint();
        if (hint is null)
        {
            SetStatus("当前没有可提示的空格。");
            return;
        }

        SetStatus($"提示：{hint.Text}（已用 {_session.HintsUsed} 次）");
        SaveGame();
    }

    private void OnPause()
    {
        _session.TogglePause();
        SetStatus(_session.IsPaused ? "已暂停。" : string.Empty);
        SaveGame();
        RefreshOverlays();
    }

    private void OnRestart()
    {
        _session.Restart();
        _resultDismissed = false;
        SetStatus("已重开本局。");
        SaveGame();
        RefreshAll();
    }

    private void OnResultPrimary()
    {
        if (_session.IsCompleted)
        {
            _resultDismissed = true;
            _resultOverlay.IsVisible = false;
            return;
        }

        if (_session.IsFailed)
        {
            OnRestart();
        }
    }

    private void OnTimerTick()
    {
        _session.Tick();

        _tickCounter++;
        if (_tickCounter % 5 == 0)
        {
            SaveGame();
        }
    }

    // ---------- 界面刷新 ----------

    private void OnSessionPropertyChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(GameSession.ElapsedText):
                _timerLabel.Text = _session.ElapsedText;
                break;

            case nameof(GameSession.MistakeText):
                _mistakeLabel.Text = _session.MistakeText;
                break;

            case nameof(GameSession.ProgressText):
                _progressLabel.Text = _session.ProgressText;
                break;

            case nameof(GameSession.NoteMode):
                _noteButton.Text = _session.NoteMode ? "笔记 ✓" : "笔记";
                break;

            case nameof(GameSession.CanPlay):
            case nameof(GameSession.IsCompleted):
            case nameof(GameSession.IsFailed):
            case nameof(GameSession.IsPaused):
                RefreshOverlays();
                break;

            case nameof(GameSession.HintDigit):
            case nameof(GameSession.HintCell):
                _boardView.Invalidate();
                break;
        }
    }

    private void OnSessionBoardChanged()
    {
        _boardView.Invalidate();
        RefreshHeader();
        RefreshOverlays();
    }

    private void RefreshAll()
    {
        _boardView.Invalidate();
        RefreshHeader();
        RefreshOverlays();
    }

    private void RefreshHeader()
    {
        _timerLabel.Text = _session.ElapsedText;
        _progressLabel.Text = _session.ProgressText;
        _mistakeLabel.Text = _session.MistakeText;
        _levelLabel.Text = _session.LevelName;
        _undoButton.IsEnabled = _session.CanUndo;
        _redoButton.IsEnabled = _session.CanRedo;
        _noteButton.Text = _session.NoteMode ? "笔记 ✓" : "笔记";
        _pauseButton.Text = _session.IsPaused ? "继续" : "暂停";
    }

    private void RefreshOverlays()
    {
        _pauseOverlay.IsVisible = _session.IsPaused;

        if (_session.IsCompleted || _session.IsFailed)
        {
            if (!_resultDismissed)
            {
                _resultTitle.Text = _session.IsCompleted ? "🎉 完成！" : "错误次数已用尽";
                _resultDetail.Text = _session.IsCompleted
                    ? $"用时 {_session.ElapsedText} · 错误 {_session.MistakeCount} 次 · 提示 {_session.HintsUsed} 次"
                    : $"已达上限 {AppState.Settings.MistakeLimit} 次错误，本局结束。";
                _resultPrimary.Text = _session.IsCompleted ? "查看棋盘" : "重开本局";
                _resultSecondary.Text = "返回主菜单";
                _resultOverlay.IsVisible = true;
            }
            else
            {
                _resultOverlay.IsVisible = false;
            }

            return;
        }

        _resultOverlay.IsVisible = false;
        _resultDismissed = false;
    }

    private void SetStatus(string message) => _statusLabel.Text = message;

    private void SaveGame() => GameStorage.Save(_session.ToSnapshot());

    private void Haptic()
    {
        if (!AppState.Settings.HapticsEnabled)
        {
            return;
        }

        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch (Exception)
        {
            // 部分平台不支持震动反馈，忽略即可
        }
    }

    private static Label StatLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 13,
            VerticalTextAlignment = TextAlignment.Center,
        };
        label.SetAppThemeColor(Label.TextColorProperty, Ui.LightMuted, Ui.DarkMuted);
        return label;
    }

    private static Grid BuildOverlay(string title, string detail, out Label titleLabel, out Button primary, out Button secondary, out Label detailLabel)
    {
        titleLabel = Ui.Title(title, 24);
        detailLabel = Ui.Body(detail, 14, TextAlignment.Center);
        primary = Ui.Primary("确定");
        secondary = Ui.Secondary("取消");

        var card = Ui.Card(new VerticalStackLayout
        {
            Spacing = 12,
            WidthRequest = 300,
            Children = { titleLabel, detailLabel, primary, secondary },
        }, 22);

        card.HorizontalOptions = LayoutOptions.Center;
        card.VerticalOptions = LayoutOptions.Center;

        return new Grid
        {
            IsVisible = false,
            BackgroundColor = Color.FromArgb("#99000000"),
            Children = { card },
        };
    }

#if WINDOWS
    private void HookKeyboard()
    {
        if (_keyboardHooked)
        {
            return;
        }

        try
        {
            if (Window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window window)
            {
                return;
            }

            if (window.Content is Microsoft.UI.Xaml.UIElement root)
            {
                root.AddHandler(
                    Microsoft.UI.Xaml.UIElement.KeyDownEvent,
                    new Microsoft.UI.Xaml.Input.KeyEventHandler(OnKeyDown),
                    handledEventsToo: true);
                _keyboardHooked = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"键盘绑定失败：{ex.Message}");
        }
    }

    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Number1:
            case Windows.System.VirtualKey.NumberPad1:
                OnDigit(1);
                break;
            case Windows.System.VirtualKey.Number2:
            case Windows.System.VirtualKey.NumberPad2:
                OnDigit(2);
                break;
            case Windows.System.VirtualKey.Number3:
            case Windows.System.VirtualKey.NumberPad3:
                OnDigit(3);
                break;
            case Windows.System.VirtualKey.Number4:
            case Windows.System.VirtualKey.NumberPad4:
                OnDigit(4);
                break;
            case Windows.System.VirtualKey.Number5:
            case Windows.System.VirtualKey.NumberPad5:
                OnDigit(5);
                break;
            case Windows.System.VirtualKey.Number6:
            case Windows.System.VirtualKey.NumberPad6:
                OnDigit(6);
                break;
            case Windows.System.VirtualKey.Number7:
            case Windows.System.VirtualKey.NumberPad7:
                OnDigit(7);
                break;
            case Windows.System.VirtualKey.Number8:
            case Windows.System.VirtualKey.NumberPad8:
                OnDigit(8);
                break;
            case Windows.System.VirtualKey.Number9:
            case Windows.System.VirtualKey.NumberPad9:
                OnDigit(9);
                break;

            case Windows.System.VirtualKey.Back:
            case Windows.System.VirtualKey.Delete:
            case Windows.System.VirtualKey.Number0:
                OnErase();
                break;

            case Windows.System.VirtualKey.Left:
                _session.MoveSelection(0, -1);
                break;
            case Windows.System.VirtualKey.Right:
                _session.MoveSelection(0, 1);
                break;
            case Windows.System.VirtualKey.Up:
                _session.MoveSelection(-1, 0);
                break;
            case Windows.System.VirtualKey.Down:
                _session.MoveSelection(1, 0);
                break;

            case Windows.System.VirtualKey.N:
                OnToggleNoteMode();
                break;
            case Windows.System.VirtualKey.H:
                OnHint();
                break;
            case Windows.System.VirtualKey.Z:
                OnUndo();
                break;
            case Windows.System.VirtualKey.Y:
                OnRedo();
                break;
            case Windows.System.VirtualKey.P:
                OnPause();
                break;

            default:
                return;
        }

        e.Handled = true;
    }
#else
    private void HookKeyboard()
    {
        // 手机端只有触摸输入，无需键盘绑定
    }
#endif
}
