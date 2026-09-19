using Microsoft.Maui.Devices;
using Microsoft.Maui.Layouts;
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
    private readonly Button _noteButton;
    private readonly Button _linkStyleButton;
    private readonly Button _autoMarkButton;
    private readonly Button _lockButton;
    private readonly Button _paintToolButton;
    private readonly Button _drawButton;
    private readonly Button _pauseButton;
    private readonly View _bottomPanel;
    private readonly View _drawPanel;
    private Grid? _bottomSwap;
    private readonly List<Button> _digitButtons = new();
    private readonly List<Label> _digitCountLabels = new();
    private readonly List<Button> _colorButtons = new();

    /// <summary>窄屏（手机竖屏）时要压矮的普通工具按钮。</summary>
    private readonly List<Button> _compactButtons = new();
    private Grid? _digitsGrid;
    private Grid? _boardArea;
    private readonly Grid _pauseOverlay;
    private readonly Grid _resultOverlay;
    private readonly Label _resultTitle;
    private readonly Label _resultDetail;
    private readonly Button _resultPrimary;
    private readonly Button _resultSecondary;
    private readonly Grid _hintDock;
    private View? _bottomControls;
    private Border? _hintCard;
    private VerticalStackLayout? _hintStack;
    private readonly Label _hintTitle;
    private readonly Label _hintSubtitle;
    private readonly VerticalStackLayout _hintList;
    private readonly Button _hintBack;
    private readonly Button _hintPrimary;
    private ScrollView? _hintScroll;
    private Button? _hintCollapse;
    private bool _hintCollapsed;
    private DateTime _lastTapAt = DateTime.MinValue;
    private int _lastTapCell = -1;
    private HintPlan? _hintPlan;
    private HintGroup? _hintGroup;
    private HintOption? _hintOption;
    private int _hintLineIndex;
    private bool _hintLoading;
    private Button? _bivalueButton;
    private Label? _bivalueLabel;
    private readonly IDispatcherTimer _timer;
    private int _tickCounter;
    private bool _resultDismissed;

    /// <summary>状态栏按「最长的一条状态文字」预留高度（当前最长的一条是 45 字上下）。</summary>
    private const int StatusReserveChars = 52;
    private double _statusLayoutWidth = -1;
    private bool _compactApplied;

#if WINDOWS
    private bool _keyboardHooked;
#endif

    public GamePage(GameSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _drawable.Session = _session;

        Title = $"{_session.LevelName} · 数独";
        Ui.ApplyBackground(this);

        // 对局页不要标题栏：手机竖屏下它要吃掉 50 上下像素，留给棋盘更划算
        // （难度、用时、进度都在页内第一行；回主菜单用上方「新对局」）
        NavigationPage.SetHasNavigationBar(this, false);

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

        _hintDock = BuildHintDock(out _hintTitle, out _hintSubtitle, out _hintList, out _hintBack, out _hintPrimary);
        _hintBack.Clicked += (_, _) => OnHintBack();
        _hintPrimary.Clicked += (_, _) => OnHintPrimary();

        // 提示框要随屏幕尺寸自适应（手机竖屏下高度受限，列表自己滚动）
        SizeChanged += (_, _) => ApplyHintLayout();

        _boardArea = new Grid
        {
            Padding = new Thickness(10, 4),
            Children = { _boardView, _pauseOverlay, _resultOverlay },
        };
        Grid boardArea = _boardArea;

        // ---------- 数字键盘（长按数字键可锁定该数字） ----------
        var digitsGrid = new Grid
        {
            ColumnSpacing = 6,
            Padding = new Thickness(10, 2),
        };
        _digitsGrid = digitsGrid;

        // 9 个数字 + 最右侧一个 XY（与数字键同级：高亮只剩两个候选数的格子）
        for (int i = 0; i <= SudokuGrid.Size; i++)
        {
            digitsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        // 两行：上面是数字键，下面一行灰字显示该数字还剩几个没填
        digitsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        digitsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int digit = 1; digit <= SudokuGrid.Size; digit++)
        {
            Button button = Ui.Digit(digit.ToString());
            int captured = digit;

            button.Clicked += (_, _) => OnDigit(captured);

            Label countLabel = Ui.Body(SudokuGrid.Size.ToString(), 11, TextAlignment.Center);
            countLabel.Margin = new Thickness(0, -3, 0, 0);

            _digitButtons.Add(button);
            _digitCountLabels.Add(countLabel);
            digitsGrid.Add(button, digit - 1, 0);
            digitsGrid.Add(countLabel, digit - 1, 1);
        }

        // 最右侧的 XY 按钮：与数字键同级（只和「锁定的数字键」互斥，不动「数字锁定」开关）
        _bivalueButton = Ui.Digit("XY");
        _bivalueButton.FontSize = 15;
        _bivalueButton.Clicked += (_, _) => OnToggleBivalue();
        _bivalueLabel = Ui.Body("双值", 11, TextAlignment.Center);
        _bivalueLabel.Margin = new Thickness(0, -3, 0, 0);
        digitsGrid.Add(_bivalueButton, SudokuGrid.Size, 0);
        digitsGrid.Add(_bivalueLabel, SudokuGrid.Size, 1);

        // ---------- 棋盘下方：只放与游戏内容相关的功能 ----------
        _undoButton = Ui.Tool("撤销");
        _undoButton.Clicked += (_, _) => OnUndo();

        _noteButton = Ui.Tool("笔记");
        _noteButton.Clicked += (_, _) => OnToggleNoteMode();

        _autoMarkButton = Ui.Tool("自动标记");
        _autoMarkButton.Clicked += (_, _) => OnToggleAutoMark();

        _lockButton = Ui.Tool("数字锁定");
        _lockButton.Clicked += (_, _) => OnToggleLockMode();

        Button hintButton = Ui.Tool("提示");
        hintButton.Clicked += (_, _) => OnHint();

        // 绘制模式是开关：激活时整块高亮（不再用「✓」后缀）
        _drawButton = Ui.Tool("绘制");
        _drawButton.Clicked += (_, _) => OnToggleDrawMode();

        _compactButtons.AddRange(new[] { _noteButton, hintButton, _autoMarkButton, _lockButton, _drawButton });

        var contentToolsGrid = new Grid
        {
            ColumnSpacing = 6,
            Padding = new Thickness(10, 2),
        };
        for (int i = 0; i < 5; i++)
        {
            contentToolsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        contentToolsGrid.Add(_noteButton, 0, 0);
        contentToolsGrid.Add(hintButton, 1, 0);
        contentToolsGrid.Add(_autoMarkButton, 2, 0);
        contentToolsGrid.Add(_lockButton, 3, 0);
        contentToolsGrid.Add(_drawButton, 4, 0);

        // 内容工具面板：绘制模式下整块让位给绘制面板（数字行独立在外，始终保留）
        _bottomPanel = contentToolsGrid;

        // ---------- 绘制面板：进入绘制模式后替换上面的内容工具 ----------
        _paintToolButton = CompactTool("涂色 ●");
        _paintToolButton.Clicked += (_, _) => OnTogglePaintTool();

        _linkStyleButton = CompactTool("强链 ━");
        _linkStyleButton.Clicked += (_, _) =>
        {
            _session.ToggleLinkStrength();
            RefreshDrawTools();
            SetStatus($"下一条连线将画成{_session.LinkStrengthText}（实线=强链，虚线=弱链）。");
        };

        Button clearDrawingButton = CompactTool("清除绘制");
        clearDrawingButton.Clicked += (_, _) => OnClearDrawing();

        Button exitDrawButton = CompactTool("退出绘制");
        exitDrawButton.Clicked += (_, _) => OnToggleDrawMode();

        // 绘制工具与色块放进同一个会换行的横排：宽屏一行放得下（和普通工具行一样高，
        // 于是「工具行 / 绘制面板」取较大者也不会让棋盘变小），手机窄屏自动折行。
        var drawTools = new FlexLayout
        {
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Start,
        };

        void AddDrawItem(View item)
        {
            FlexLayout.SetBasis(item, new FlexBasis(66));
            FlexLayout.SetGrow(item, 1);
            item.Margin = new Thickness(3, 2);
            drawTools.Add(item);
        }

        AddDrawItem(_paintToolButton);
        AddDrawItem(_linkStyleButton);
        AddDrawItem(clearDrawingButton);
        AddDrawItem(exitDrawButton);

        for (int color = 1; color <= CellColoring.MaxColor; color++)
        {
            Button swatch = CompactTool("●");
            int captured = color;
            swatch.Clicked += (_, _) => OnPickColor(captured);
            _colorButtons.Add(swatch);
            AddDrawItem(swatch);
        }

        Button eraserSwatch = CompactTool("无");
        eraserSwatch.Clicked += (_, _) => OnPickColor(0);
        _colorButtons.Add(eraserSwatch);
        AddDrawItem(eraserSwatch);

        _drawPanel = new VerticalStackLayout
        {
            IsVisible = false,
            Spacing = 2,
            Children = { drawTools },
        };

        // ---------- 界面上方：对局管理类操作 ----------
        Button restartButton = Ui.Tool("重开");
        restartButton.Clicked += (_, _) => OnRestart();

        Button newGameButton = Ui.Tool("新对局");
        newGameButton.Clicked += async (_, _) => await Navigation.PopToRootAsync();

        Button settingsButton = Ui.Tool("设置");
        settingsButton.Clicked += async (_, _) => await Navigation.PushAsync(new SettingsPage());

        Button sdkButton = Ui.Tool("导入导出");
        sdkButton.Clicked += async (_, _) => await Navigation.PushAsync(new SdkPage());

        var topActionsGrid = new Grid
        {
            ColumnSpacing = 6,
            Padding = new Thickness(10, 0),
        };
        for (int i = 0; i < 5; i++)
        {
            topActionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        // 上方只留撤销：重做与撤销长得太像（都和「重开」撞脸），改成只用快捷键 Y
        topActionsGrid.Add(_undoButton, 0, 0);
        topActionsGrid.Add(restartButton, 1, 0);
        topActionsGrid.Add(newGameButton, 2, 0);
        topActionsGrid.Add(sdkButton, 3, 0);
        topActionsGrid.Add(settingsButton, 4, 0);

        _compactButtons.AddRange(new[] { _undoButton, restartButton, newGameButton, sdkButton, settingsButton });

        _statusLabel = Ui.Body(string.Empty, 13, TextAlignment.Center);
        _statusLabel.Margin = new Thickness(12, 2, 12, 8);
        _statusLabel.VerticalTextAlignment = TextAlignment.Center;

        // 普通工具行与绘制面板叠在同一个格子里（切换只换可见性，不换高度）；
        // 容器高度在 ApplyHintLayout 里量成「两者较大者」并定死，这样切绘制模式棋盘不会变尺寸。
        _bottomSwap = new Grid();
        _bottomSwap.Add(_bottomPanel);
        _bottomSwap.Add(_drawPanel);

        // 底部区域：平时放数字键盘与工具按钮；点「提示」时这一整块让位给提示框
        _bottomControls = new VerticalStackLayout
        {
            Spacing = 2,
            Children = { digitsGrid, _bottomSwap },
        };

        _hintDock.IsVisible = false;
        var bottomArea = new Grid
        {
            Children = { _bottomControls, _hintDock },
        };

        var layout = new Grid
        {
            RowSpacing = 2,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            },
        };

        layout.Add(headerGrid, 0, 0);
        layout.Add(topActionsGrid, 0, 1);
        layout.Add(boardArea, 0, 2);
        layout.Add(bottomArea, 0, 3);
        layout.Add(_statusLabel, 0, 4);

        Content = layout;

        RefreshDigitButtons();
        RefreshDrawTools();

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

        // 专项练习：开局把目标写进状态栏，免得玩家不知道要找什么
        if (_session.IsPractice && !_session.IsCompleted && !_session.IsFailed)
        {
            SetStatus($"{_session.PracticeGoalText}（点「提示」看这一步怎么推）");
        }
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

        PointF point = e.Touches[0];

        // 绘制模式：涂色刷格子，或者按候选数画链
        if (_session.DrawMode)
        {
            if (_session.PaintMode)
            {
                int paintCell = _drawable.HitTest(point);
                if (paintCell >= 0)
                {
                    // 绘图模式下刻意不选中：选中高亮会盖住玩家刚涂的颜色
                    _session.ClearSelection();
                    OnPaintCell(paintCell);
                }

                return;
            }

            if (_drawable.HitTestCandidate(point) is { } target)
            {
                HandleLinkTap(target);
                return;
            }

            return;
        }

        int cell = _drawable.HitTest(point);
        if (cell < 0)
        {
            return;
        }

        // Windows 上双击：该格只有一个候选数时直接填入（数字锁定模式下单击即填入，不需要双击）
        DateTime now = DateTime.UtcNow;
        bool isDoubleTap = cell == _lastTapCell && (now - _lastTapAt).TotalMilliseconds <= 420;
        _lastTapCell = cell;
        _lastTapAt = now;

        if (isDoubleTap && !_session.LockMode && AppState.Settings.DoubleClickQuickFill && IsDoubleTapSupported())
        {
            _lastTapCell = -1;
            HandleQuickFill(cell);
            return;
        }

        _session.Select(cell);

        // 数字锁定模式：点格子就是把锁定的数字填进这一格
        if (_session.LockMode)
        {
            if (_session.LockedDigit != 0)
            {
                FillDigit(_session.LockedDigit);
            }
            else
            {
                SetStatus("数字锁定已开启：先点下方要锁定的数字，再点格子即可填入。");
            }

            return;
        }

        RefreshDigitButtons();
    }

    /// <summary>双击快速填入仅在 Windows（鼠标）上启用。</summary>
    private static bool IsDoubleTapSupported() => DeviceInfo.Platform == DevicePlatform.WinUI;

    private void HandleQuickFill(int cell)
    {
        switch (_session.QuickFill(cell))
        {
            case QuickFillOutcome.Filled:
                SetStatus($"{CellName(cell)} 只有一个候选数，已直接填入。");
                SaveGame();
                RefreshHeader();
                RefreshOverlays();
                break;

            case QuickFillOutcome.MultipleCandidates:
                SetStatus($"{CellName(cell)} 不止一个候选数，不能快速填入。");
                break;

            case QuickFillOutcome.NoCandidate:
                SetStatus($"{CellName(cell)} 已经没有可用候选数了，说明盘面有矛盾。");
                break;
        }
    }

    private static string CellName(int cell) => $"R{SudokuGrid.Row(cell) + 1}C{SudokuGrid.Col(cell) + 1}";

    private void HandleLinkTap(CandidateRef candidate)
    {
        switch (_session.TapCandidate(candidate.Cell, candidate.Digit))
        {
            case LinkOutcome.Started:
                SetStatus($"已选起点 {candidate}，再点另一个候选数即可连{_session.LinkStrengthText}（箭头指向终点）。");
                break;

            case LinkOutcome.Linked:
                SetStatus($"已画{_session.LinkStrengthText}：{candidate} → 终点带箭头（共 {_session.LinkCount} 条，可撤销）");
                SaveGame();
                break;

            case LinkOutcome.Duplicate:
                SetStatus("这条连线已经画过了。");
                break;

            case LinkOutcome.Cancelled:
                SetStatus("已取消起点选择。");
                break;
        }
    }

    // ---------- 绘制（涂色 / 画链） ----------

    private void OnToggleDrawMode()
    {
        _session.DrawMode = !_session.DrawMode;

        if (_session.DrawMode)
        {
            // 进绘图模式就清掉选中：否则行列宫与选中格的高亮会盖住已经涂好的颜色
            _session.ClearSelection();
        }

        RefreshDrawTools();

        SetStatus(_session.DrawMode
            ? $"绘制模式：下方换成颜色与画笔（当前 {_session.PaintToolText}）。涂色点格子，画链点两个候选数。"
            : string.Empty);

        // 两种模式的底部高度不同，重新量一次（整块高度是取两者较大者，所以棋盘尺寸不变）
        ApplyHintLayout();
    }

    private void OnTogglePaintTool()
    {
        _session.PaintMode = !_session.PaintMode;
        RefreshDrawTools();

        SetStatus(_session.PaintMode
            ? "绘制工具：涂色。选一个颜色后点格子即可刷色。"
            : $"绘制工具：画链。点一个候选数作为起点，再点另一个候选数连线（当前 {_session.LinkStrengthText}），箭头指向终点。");
    }

    private void OnPickColor(int colorIndex)
    {
        _session.SetDrawColor(colorIndex);
        RefreshDrawTools();

        SetStatus(colorIndex == 0
            ? "已选择「无」：点格子可擦掉该格颜色。"
            : $"已选择绘制颜色 {colorIndex}（涂色与连线共用这个颜色）。");
    }

    private void OnPaintCell(int cell)
    {
        if (!_session.PaintCell(cell))
        {
            SetStatus("这一格已经是该颜色（或无法涂色）。");
            return;
        }

        SaveGame();
        SetStatus($"{CellName(cell)} 已涂色，可用撤销回退。");
    }

    private void OnClearDrawing()
    {
        if (!_session.ClearDrawing())
        {
            SetStatus("还没有涂色或画线。");
            return;
        }

        SaveGame();
        SetStatus("已清除全部涂色与连线（可用撤销恢复）。");
    }

    private void RefreshDrawTools()
    {
        _paintToolButton.Text = _session.PaintToolText;
        _linkStyleButton.Text = _session.LinkStrengthText;
        _autoMarkButton.Text = _session.AutoMarkText;
        _lockButton.Text = _session.LockModeText;
        _noteButton.Text = _session.NoteModeText;
        _drawButton.Text = _session.DrawModeText;

        // 开关类按钮：激活时整块高亮，取代原来的「✓」后缀
        Ui.SetToolActive(_paintToolButton, _session.PaintMode);
        Ui.SetToolActive(_autoMarkButton, _session.AutoMark);
        Ui.SetToolActive(_lockButton, _session.LockMode);
        Ui.SetToolActive(_noteButton, _session.NoteMode);
        Ui.SetToolActive(_drawButton, _session.DrawMode);

        for (int i = 0; i < _colorButtons.Count; i++)
        {
            int color = i + 1;
            Button swatch = _colorButtons[i];
            bool isEraser = color > CellColoring.MaxColor;

            if (!isEraser)
            {
                // 选中的色块用描边高亮（色块本身已经填了颜色，改底色看不出来），文字保持 ●
                swatch.Text = "●";
                if (Palette.Current.DrawColor(color) is { } fill)
                {
                    swatch.BackgroundColor = fill;
                }

                swatch.TextColor = Ui.LightText;
                Ui.SetSwatchSelected(swatch, _session.DrawColorIndex == color);
            }
            else
            {
                // 最后一个色块是「无」（擦除）
                swatch.Text = "无";
                Ui.SetSwatchSelected(swatch, _session.DrawColorIndex == 0);
            }
        }

        RefreshBottomPanels();
        _boardView.Invalidate();
    }

    private void RefreshBottomPanels()
    {
        _bottomPanel.IsVisible = !_session.DrawMode;
        _drawPanel.IsVisible = _session.DrawMode;

        // 绘制模式下收起数字键盘：用不上，而且腾出的高度正好让绘制面板装进同一块区域
        if (_digitsGrid is not null)
        {
            _digitsGrid.IsVisible = !_session.DrawMode;
        }
    }

    private void OnDigit(int digit)
    {
        if (!_session.CanPlay)
        {
            return;
        }

        // 数字键与最右侧的 XY 互斥：按了数字就把「两个候选数」高亮放掉
        _session.ClearBivalueMode();

        // 数字锁定模式：点数字键 = 锁定该数字（点格子才是填入）
        if (_session.LockMode)
        {
            OnLockDigit(digit);
            return;
        }

        FillDigit(digit);
    }

    /// <summary>最右侧 XY 按钮：高亮盘面上只剩两个候选数的格子（与数字锁定互斥）。</summary>
    private void OnToggleBivalue()
    {
        if (!_session.CanPlay)
        {
            return;
        }

        bool on = _session.ToggleBivalueMode();
        RefreshDigitButtons();
        SaveGame();

        SetStatus(on
            ? $"已高亮 {_session.BivalueCount} 个只剩两个候选数的格子（青绿圈出）；再点一次 XY 或按数字键即取消。"
            : "已取消「两个候选数」高亮。");
    }

    /// <summary>把数字填进当前选中的格子（数字锁定模式下点格子也走这里）。</summary>
    private void FillDigit(int digit)
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
        int lockedBefore = _session.LockedDigit;
        InputOutcome outcome = _session.Input(digit);
        int lockedAfter = _session.LockedDigit;

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

        // 数字锁定模式下把某个数字填满了：会自动跳到下一个数字，这里提示一下
        if (outcome == InputOutcome.Placed && lockedBefore != 0 && lockedAfter != lockedBefore)
        {
            SetStatus(lockedAfter == 0
                ? $"数字 {lockedBefore} 已填满，9 个数字都齐了，已取消锁定。"
                : $"数字 {lockedBefore} 已填满，自动切换到 {lockedAfter}。");
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
        RefreshDigitButtons();
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
            SetStatus("先选中一个已填的格子，再用退格 / Delete / 0 清除。");
            return;
        }

        SetStatus(string.Empty);
        SaveGame();
        RefreshHeader();
    }

    private void OnUndo()
    {
        if (!_session.CanUndo)
        {
            SetStatus("没有可撤销的步骤。");
            RefreshHeader();
            return;
        }

        _session.Undo();
        SetStatus("已撤销上一步。");
        SaveGame();
        // 撤销会改动盘面：进度、数字键剩余数量、撤销键可用状态都要跟着刷新
        RefreshHeader();
        RefreshOverlays();
    }

    /// <summary>重做：界面上不再单独放按钮（与「重开」看着像），只保留 Y 快捷键。</summary>
    private void OnRedo()
    {
        if (!_session.CanRedo)
        {
            SetStatus("没有可重做的步骤。");
            RefreshHeader();
            return;
        }

        _session.Redo();
        SetStatus("已重做。");
        SaveGame();
        RefreshHeader();
        RefreshOverlays();
    }

    private void OnToggleNoteMode()
    {
        _session.ToggleNoteMode();
        SetStatus(_session.NoteMode ? "笔记模式：数字键切换候选数。" : "填数模式：数字键直接填入。");
    }

    /// <summary>自动标记开关：打开补全候选数，关闭清空候选数。</summary>
    private void OnToggleAutoMark()
    {
        if (!_session.CanPlay)
        {
            return;
        }

        bool on = _session.ToggleAutoMark();
        RefreshDrawTools();
        SetStatus(on ? "自动标记已开启：已按当前盘面补全候选数。" : "自动标记已关闭：候选数标记已清空。");
        SaveGame();
    }

    // ---------- 数字锁定模式 ----------

    private void OnToggleLockMode()
    {
        bool on = _session.ToggleLockMode();
        RefreshDigitButtons();
        RefreshDrawTools();
        SaveGame();

        SetStatus(on
            ? "数字锁定已开启：点下方数字即锁定该数字（它的位置与候选数会一起高亮），再点格子即可填入。"
            : "数字锁定已关闭：点格子会按该格候选数高亮下方对应的数字键。");
    }

    private void OnLockDigit(int digit)
    {
        int locked = _session.ToggleLockDigit(digit);
        RefreshDigitButtons();
        SaveGame();

        SetStatus(locked == 0
            ? $"已取消锁定 {digit}。"
            : $"已锁定数字 {locked}：点格子即可填入 {locked}（再点一次 {locked} 可解锁）。");
    }

    private void RefreshDigitButtons()
    {
        int selected = _session.SelectedCell;
        bool hasCell = selected >= 0 && _session.Values[selected] == 0;
        int notes = hasCell ? _session.Notes[selected] : 0;
        bool lockMode = _session.LockMode;
        AppSettings settings = AppState.Settings;

        // 填完的数字键一律隐藏（数字锁定模式下同样适用）
        bool hideCompleted = settings.HideCompletedDigits;

        for (int i = 0; i < _digitButtons.Count; i++)
        {
            int digit = i + 1;
            Button button = _digitButtons[i];

            bool locked = lockMode && _session.LockedDigit == digit;
            bool candidate = settings.HighlightDigitButtons && !lockMode && hasCell && (notes & (1 << (digit - 1))) != 0;

            int remaining = _session.RemainingOf(digit);
            bool complete = remaining == 0;

            button.IsVisible = !(hideCompleted && complete);
            button.Text = locked ? $"{digit} 🔒" : digit.ToString();
            // 填完的数字键淡出，配合下方「0」一眼看出这个数字不用再管了
            button.Opacity = complete ? 0.45 : 1;

            Label countLabel = _digitCountLabels[i];
            countLabel.Text = remaining.ToString();
            countLabel.IsVisible = button.IsVisible;
            countLabel.Opacity = complete ? 0.7 : 1;

            if (locked)
            {
                button.SetAppThemeColor(Button.BackgroundColorProperty, Ui.LightAccent, Color.FromArgb("#4A7FBF"));
                button.SetAppThemeColor(Button.TextColorProperty, Colors.White, Colors.White);
            }
            else if (candidate)
            {
                // 关闭锁定时：选中格的候选数在下方数字键上高亮，方便对照
                button.SetAppThemeColor(Button.BackgroundColorProperty, Color.FromArgb("#BBDEFB"), Color.FromArgb("#2E4A63"));
                button.SetAppThemeColor(Button.TextColorProperty, Ui.LightAccent, Colors.White);
            }
            else
            {
                button.SetAppThemeColor(Button.BackgroundColorProperty, Ui.LightSurface, Ui.DarkSurface);
                button.SetAppThemeColor(Button.TextColorProperty, Ui.LightAccent, Color.FromArgb("#8FC2F5"));
            }
        }

        RefreshBivalueButton();
        _boardView.Invalidate();
    }

    /// <summary>刷新最右侧 XY 按钮的状态：开启时整块高亮（和数字键一样只有一块是亮的）。</summary>
    private void RefreshBivalueButton()
    {
        if (_bivalueButton is null)
        {
            return;
        }

        bool on = _session.BivalueMode;
        Ui.SetToolActive(_bivalueButton, on);

        if (_bivalueLabel is not null)
        {
            _bivalueLabel.Text = on ? _session.BivalueCount.ToString() : "双值";
        }
    }

    /// <summary>提示入口：高阶提示（分技巧讲解）或最简单的单格提示。</summary>
    private async void OnHint()
    {
        if (!_session.CanPlay)
        {
            SetStatus("当前无法提示。");
            return;
        }

        // 上一轮提示留在盘面上的图示先擦掉，免得和新提示混在一起
        _session.ShowHintStep(null);

        if (!AppState.Settings.AdvancedHints)
        {
            HintInfo? direct = _session.RevealHint();
            SetStatus(direct is null ? "当前没有可提示的空格。" : $"提示：{direct.Text}（已用 {_session.HintsUsed} 次）");
            SaveGame();
            return;
        }

        if (_hintLoading)
        {
            return;
        }

        _hintLoading = true;
        SetStatus("正在分析当前盘面，寻找可用技巧…");

        try
        {
            int cap = AppState.Settings.HintLevelCap;

            // 专项练习要练的那一招可能比设置里的技法上限还高，练习模式下放宽到目标技巧的等级
            if (_session.Practice is { } practiceCap)
            {
                cap = Math.Max(cap, TechniqueInfo.Level(practiceCap.Technique));
            }

            // 十七数题目本身就要求高阶技巧（ALS 链 / BUG+1 / 唯一矩形 等），
            // 技法上限不放宽的话提示会一直是「没有找到可用技巧」
            if (_session.IsSeventeen)
            {
                cap = Math.Max(cap, SeventeenClues.MaxTechniqueLevel);
            }

            HintPlan plan = await Task.Run(() => _session.BuildHintPlan(cap));

            _hintPlan = plan;
            _hintGroup = null;
            _hintOption = null;
            _hintLineIndex = 0;

            // 专项练习：直接跳到目标技巧那一步，不用玩家自己在一堆技巧里找
            if (_session.Practice is { } practicePlan
                && plan.Groups.FirstOrDefault(g => g.Technique == practicePlan.Technique) is { } group)
            {
                _hintGroup = group;
                _hintOption = group.Options.FirstOrDefault(o => SameStep(o.Step, practicePlan.Target))
                    ?? group.Options.FirstOrDefault();
                if (_hintOption is not null)
                {
                    FocusHintStep(_hintOption.Step);
                }
            }

            if (plan.IsEmpty)
            {
                SetStatus($"当前盘面没有找到可用技巧（技法上限 {cap} 级）。");
                return;
            }

            SetStatus(string.Empty);
            ShowHintPanel(true);
            // 提示面板在底部按钮区，尺寸要等布局完成才准，这里前后补算两次
            ApplyHintLayout();
            Dispatcher.Dispatch(ApplyHintLayout);
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(80), ApplyHintLayout);
            RenderHint();
        }
        catch (Exception ex)
        {
            SetStatus($"提示分析失败：{ex.Message}");
        }
        finally
        {
            _hintLoading = false;
        }
    }

    // ---------- 分段式提示：技巧 → 路径 → 逐步推导 ----------

    private void RenderHint()
    {
        if (_hintPlan is null)
        {
            CloseHint();
            return;
        }

        _hintList.Clear();

        if (_hintGroup is not null && _hintOption is not null)
        {
            RenderHintDerivation(_hintGroup, _hintOption);
        }
        else if (_hintGroup is not null)
        {
            RenderHintPaths(_hintGroup);
        }
        else
        {
            RenderHintTechniques(_hintPlan);
        }
    }

    private void RenderHintTechniques(HintPlan plan)
    {
        _hintTitle.Text = "现在能用的技巧";
        _hintSubtitle.Text = $"{plan.Groups.Count} 类 · {plan.TotalOptions} 条路径。选一个，我带你一步步推。";
        _hintBack.Text = "关闭";
        _hintPrimary.IsVisible = false;

        foreach (HintGroup group in plan.Groups)
        {
            Button button = Ui.Secondary($"{group.Name}（{group.Count} 条）");
            button.FontSize = 13;
            button.HeightRequest = 34;
            button.CornerRadius = 9;
            HintGroup captured = group;
            button.Clicked += (_, _) =>
            {
                _hintGroup = captured;
                _hintOption = null;
                _hintLineIndex = 0;
                RenderHint();
            };
            _hintList.Add(button);
        }
    }

    private void RenderHintPaths(HintGroup group)
    {
        _hintTitle.Text = $"{group.Name} · 选路径";
        _hintSubtitle.Text = group.Summary;
        _hintBack.Text = "返回";
        _hintPrimary.IsVisible = false;

        foreach (HintOption option in group.Options)
        {
            Button button = Ui.Secondary(option.Label);
            button.FontSize = 13;
            button.HeightRequest = 34;
            button.CornerRadius = 9;
            HintOption captured = option;
            button.Clicked += (_, _) =>
            {
                _hintOption = captured;
                _hintLineIndex = 0;
                FocusHintStep(captured.Step);
                RenderHint();
            };            _hintList.Add(button);
        }
    }

    private void RenderHintDerivation(HintGroup group, HintOption option)
    {
        TechniqueStep step = option.Step;
        IReadOnlyList<string> lines = DerivationLines(step);

        _hintTitle.Text = group.Name;
        _hintSubtitle.Text = HintPlanner.Effect(step);
        _hintBack.Text = "返回";
        _hintPrimary.IsVisible = true;

        int shown = Math.Clamp(_hintLineIndex, 1, lines.Count);
        for (int i = 0; i < shown; i++)
        {
            _hintList.Add(Ui.Body($"· {lines[i]}", 12));
        }

        // 棋盘高亮跟着推导进度走：第 1 条只亮结构，走到最后一条才出现「要删的候选数」
        _session.ShowHintStep(step, shown - 1);

        bool finished = shown >= lines.Count;
        if (finished)
        {
            _hintList.Add(Ui.Body($"结论：{step.Description}", 12));
        }

        _hintPrimary.Text = finished ? "应用这一步" : $"下一步（{shown}/{lines.Count}）";
    }

    private void OnHintPrimary()
    {
        if (_hintOption is null)
        {
            return;
        }

        IReadOnlyList<string> lines = DerivationLines(_hintOption.Step);
        if (_hintLineIndex < lines.Count)
        {
            _hintLineIndex++;
            RenderHint();
            return;
        }

        HintInfo? info = _session.ApplyHintStep(_hintOption.Step);

        // 设置里勾了「应用后清除提示绘制」就顺手擦掉盘面图示，否则留着让玩家照着填
        bool keepDrawing = !AppState.Settings.ClearHintDrawingOnApply;
        TechniqueStep appliedStep = _hintOption.Step;
        CloseHint(keepDrawing: keepDrawing);

        if (keepDrawing)
        {
            // 落子类步骤内部会经「选中 / 输入」清掉提示高亮，这里按设置把图示重新挂回去
            _session.ShowHintStep(appliedStep);
            _boardView.Invalidate();
        }

        if (info is null)
        {
            SetStatus("这一步没能应用（盘面已变化或已暂停）。");
            return;
        }

        SetStatus($"已应用：{info.Text}（已用 {_session.HintsUsed} 次）");
        SaveGame();
        RefreshAll();
    }

    private void OnHintBack()
    {
        if (_hintOption is not null)
        {
            _hintOption = null;
            _hintLineIndex = 0;
            _session.ShowHintStep(null);
            RenderHint();
            return;
        }

        if (_hintGroup is not null)
        {
            _hintGroup = null;
            RenderHint();
            return;
        }

        CloseHint();
    }

    /// <summary>
    /// 显示 / 隐藏底部提示面板：显示时把数字键盘与工具按钮整块隐藏，把底部让给提示。
    /// </summary>
    private void ShowHintPanel(bool show)
    {
        if (show && !_hintDock.IsVisible)
        {
            // 每次重新打开都恢复成展开状态（收起状态留着会让人以为没反应）
            _hintCollapsed = false;
        }

        _hintDock.IsVisible = show;

        if (_bottomControls is not null)
        {
            _bottomControls.IsVisible = !show;
        }
    }

    private void CloseHint(bool keepDrawing = false)
    {
        ShowHintPanel(false);
        _hintPlan = null;
        _hintGroup = null;
        _hintOption = null;
        _hintLineIndex = 0;
        _hintList.Clear();

        if (!keepDrawing)
        {
            _session.ShowHintStep(null);
        }
    }

    private void FocusHintStep(TechniqueStep step)
    {
        // 让棋盘同步画出这一步的结构（第 1 条推导的进度），链/箭头与结论随面板步进再出现
        _session.ShowHintStep(step, 0);
    }

    private static IReadOnlyList<string> DerivationLines(TechniqueStep step) =>
        step.DerivationSteps.Count > 0 ? step.DerivationSteps : new[] { step.Description };

    /// <summary>两步是否作用相同（落子看格与数字，删除看删除集合）——用来在提示里认出练习目标那一步。</summary>
    private static bool SameStep(TechniqueStep a, TechniqueStep b)
    {
        if (a.IsPlacement != b.IsPlacement)
        {
            return false;
        }

        if (a.IsPlacement)
        {
            return a.PlaceIndex == b.PlaceIndex && a.PlaceDigit == b.PlaceDigit;
        }

        var left = a.Eliminations.Select(e => (e.Cell * 10) + e.Digit).OrderBy(v => v).ToArray();
        var right = b.Eliminations.Select(e => (e.Cell * 10) + e.Digit).OrderBy(v => v).ToArray();
        return left.SequenceEqual(right);
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
                _noteButton.Text = _session.NoteModeText;
                Ui.SetToolActive(_noteButton, _session.NoteMode);
                break;

            case nameof(GameSession.LinkMode):
            case nameof(GameSession.PaintMode):
            case nameof(GameSession.DrawMode):
            case nameof(GameSession.DrawColorIndex):
            case nameof(GameSession.NextLinkIsStrong):
            case nameof(GameSession.AutoMarkText):
                RefreshDrawTools();
                break;

            case nameof(GameSession.LockedDigit):
                RefreshDigitButtons();
                break;

            case nameof(GameSession.LockMode):
                RefreshDigitButtons();
                RefreshDrawTools();
                break;

            case nameof(GameSession.BivalueMode):
            case nameof(GameSession.BivalueText):
                RefreshBivalueButton();
                break;

            case nameof(GameSession.SelectedCell):
                RefreshDigitButtons();
                break;

            case nameof(GameSession.CanUndo):
            case nameof(GameSession.CanRedo):
                // 撤销键的可用状态（重做没有按钮，只走 Y 快捷键）
                _undoButton.IsEnabled = _session.CanUndo;
                break;

            case nameof(GameSession.PendingLink):
                _boardView.Invalidate();
                break;

            case nameof(GameSession.CanPlay):
            case nameof(GameSession.IsCompleted):
            case nameof(GameSession.IsFailed):
            case nameof(GameSession.IsPaused):
                RefreshOverlays();
                break;

            case nameof(GameSession.HintDigit):
            case nameof(GameSession.HintCell):
            case nameof(GameSession.ActiveHintStep):
                _boardView.Invalidate();
                break;
        }
    }

    private void OnSessionBoardChanged()
    {
        _boardView.Invalidate();
        RefreshHeader();
        RefreshDigitButtons();
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
        _levelLabel.Text = _session.IsPractice
            ? $"练习 · {_session.Practice!.Name}"
            : _session.LevelName;
        _undoButton.IsEnabled = _session.CanUndo;
        _noteButton.Text = _session.NoteModeText;
        Ui.SetToolActive(_noteButton, _session.NoteMode);
        _pauseButton.Text = _session.IsPaused ? "继续" : "暂停";
        _autoMarkButton.Text = _session.AutoMarkText;
        RefreshDigitButtons();
        RefreshDrawTools();
    }

    private void RefreshOverlays()
    {
        if (!_session.CanPlay && _hintDock.IsVisible)
        {
            CloseHint();
        }

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

    /// <summary>工具条 / 绘制面板用的小按钮（比普通工具按钮矮一点，省下的高度留给棋盘）。</summary>
    private static Button CompactTool(string text)
    {
        Button button = Ui.Tool(text);
        button.FontSize = 12;
        button.HeightRequest = 38;
        return button;
    }


    private void SaveGame()
    {
        // 专项练习是临时的教学局，不能覆盖「继续上一局」的存档
        if (_session.IsPractice)
        {
            return;
        }

        GameStorage.Save(_session.ToSnapshot());
    }


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

    /// <summary>
    /// 提示面板：固定在页面底部（也就是原来数字键盘 / 工具按钮的位置）。
    /// 打开提示时底部按钮整块隐藏让位，关闭后自动回来——手机上不会再挡住九宫格。
    /// </summary>
    private Grid BuildHintDock(out Label title, out Label subtitle, out VerticalStackLayout list, out Button back, out Button primary)
    {
        title = Ui.Title("现在能用的技巧", 16);
        subtitle = Ui.Body(string.Empty, 12, TextAlignment.Center);
        // 副标题只留一行：面板高度很紧张，说明文字溢出让列表滚动去消化
        subtitle.MaxLines = 1;
        subtitle.LineBreakMode = LineBreakMode.TailTruncation;
        list = new VerticalStackLayout { Spacing = 5 };
        back = Ui.Secondary("关闭");
        primary = Ui.Secondary("下一步");
        primary.IsVisible = false;

        // 面板按钮一律压扁（默认 50 高在手机竖屏下太占地方）
        foreach (Button compactButton in new[] { back, primary })
        {
            compactButton.FontSize = 14;
            compactButton.HeightRequest = 38;
            compactButton.CornerRadius = 10;
        }

        // 收起：只剩标题与按钮，把棋盘让出来
        _hintCollapse = Ui.Secondary("收起");
        _hintCollapse.FontSize = 11;
        _hintCollapse.HeightRequest = 30;
        _hintCollapse.Padding = new Thickness(6, 0);
        _hintCollapse.WidthRequest = 56;
        _hintCollapse.Clicked += (_, _) => OnToggleHintCollapse();

        var titleRow = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
        };
        titleRow.Add(title, 0, 0);
        titleRow.Add(_hintCollapse, 1, 0);

        var buttons = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
        };
        buttons.Add(back, 0, 0);
        buttons.Add(primary, 1, 0);

        _hintScroll = new ScrollView { Content = list };

        _hintStack = new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                titleRow,
                subtitle,
                _hintScroll,
                buttons,
            },
        };

        var card = Ui.Card(_hintStack, 10);

        // 贴着底部铺满宽度：高度由 ApplyHintLayout 按屏幕尺寸限高，列表自己滚动
        card.HorizontalOptions = LayoutOptions.Fill;
        card.VerticalOptions = LayoutOptions.End;
        card.Margin = new Thickness(8, 2, 8, 4);
        _hintCard = card;

        return new Grid
        {
            IsVisible = false,
            Children = { card },
        };
    }

    /// <summary>
    /// 提示面板固定在底部：宽度铺满，高度按屏幕尺寸限上限，列表内部自己滚动。
    /// 手机上底部最多占屏高约一半的三分之一，九宫格始终露得出来。
    /// </summary>
    private void ApplyHintLayout()
    {
        double width = Width > 1 ? Width : 400;
        double height = Height > 1 ? Height : 700;

        ApplyFixedRows(width);

        if (_hintCard is null)
        {
            return;
        }

        bool compact = width < 520;
        _hintCard.Padding = new Thickness(compact ? 9 : 12);
        _hintTitle.FontSize = compact ? 15 : 16;

        if (_hintStack is not null)
        {
            _hintStack.Spacing = 5;
        }

        if (_hintScroll is not null)
        {
            _hintScroll.IsVisible = !_hintCollapsed;
            if (_hintCollapsed)
            {
                _hintScroll.HeightRequest = -1;
            }
            else
            {
                // 固定高度（不是只限上限）：点「下一步」会多出一行推导，如果面板跟着长高，
                // 棋盘就会一次次缩水、格子位置乱跳。定死这一块高度，多出来的内容一律在面板内滚动。
                _hintScroll.HeightRequest = Math.Clamp(
                    height * (compact ? 0.13 : 0.19), 62, 168);
            }
        }

        if (_hintCollapse is not null)
        {
            _hintCollapse.Text = _hintCollapsed ? "展开" : "收起";
        }
    }

    private void OnToggleHintCollapse()
    {
        _hintCollapsed = !_hintCollapsed;
        ApplyHintLayout();
    }

    /// <summary>
    /// 把「状态栏」与「底部工具区」这两块的高度定死：点上方格子、点下方按钮、
    /// 切绘制模式时棋盘尺寸都不再变化（只有打开「提示」面板是有意的例外）。
    /// 底部整块取「普通模式（数字键盘 + 工具行）」与「绘制模式（只有绘制面板）」中较高者：
    /// 绘制模式本来就隐藏数字键盘，所以取较大者不会白白占掉棋盘的高度。
    /// </summary>
    private void ApplyFixedRows(double width)
    {
        ApplyCompactLayout(width);

        if (_bottomSwap is not null && _bottomControls is not null)
        {
            double panelWidth = Math.Max(200, width - 20);
            double digits = _digitsGrid is null ? 0 : MeasureHeight(_digitsGrid, panelWidth);
            double toolRow = MeasureHeight(_bottomPanel, panelWidth);
            double drawRow = MeasureHeight(_drawPanel, panelWidth);

            // 哪一块显示就用哪一块的高度（不然可见的那块会被裁掉）
            double swap = _session.DrawMode ? drawRow : toolRow;
            if (swap > 1 && Math.Abs(_bottomSwap.HeightRequest - swap) > 0.5)
            {
                _bottomSwap.HeightRequest = swap;
            }

            // 整块底部的高度固定成「普通模式（数字键盘 + 工具行）」与「绘制模式（绘制面板，
            // 此时数字键盘已收起）」中较高者：切来切去棋盘都不会变，普通模式下也没有多余空当。
            double reserved = Math.Max(digits + 2 + toolRow, drawRow);
            if (reserved > 1)
            {
                _bottomControls.HeightRequest = reserved;
                _bottomControls.VerticalOptions = LayoutOptions.End;
            }
        }

        ApplyStatusHeight(width);
    }

    /// <summary>窄屏（手机竖屏）压矮上下两排工具按钮、数字键盘与棋盘内边距，把高度尽量让给棋盘。</summary>
    private void ApplyCompactLayout(double width)
    {
        bool compact = width < 520;
        if (_compactApplied == compact)
        {
            return;
        }

        _compactApplied = compact;

        foreach (Button button in _compactButtons)
        {
            button.HeightRequest = compact ? 36 : 42;
        }

        foreach (Button button in _digitButtons)
        {
            button.HeightRequest = compact ? 46 : 54;
            button.FontSize = compact ? 19 : 22;
        }

        foreach (Label label in _digitCountLabels)
        {
            label.FontSize = compact ? 10 : 11;
        }

        if (_bivalueButton is not null)
        {
            _bivalueButton.HeightRequest = compact ? 46 : 54;
        }

        if (_bivalueLabel is not null)
        {
            _bivalueLabel.FontSize = compact ? 10 : 11;
        }

        _statusLabel.FontSize = compact ? 12 : 13;

        if (_boardArea is not null)
        {
            _boardArea.Padding = compact ? new Thickness(2, 0) : new Thickness(10, 4);
        }

        _statusLayoutWidth = -1;   // 字号变了，状态栏要重新量
    }

    /// <summary>量一个面板在指定宽度下想要多高（量的时候临时让它可见，否则会量到 0）。</summary>
    private static double MeasureHeight(View view, double width)
    {
        bool wasVisible = view.IsVisible;
        view.IsVisible = true;
        double height = view.Measure(width, double.PositiveInfinity).Height;
        view.IsVisible = wasVisible;
        return height;
    }

    /// <summary>
    /// 状态栏按「最长的一条状态文字在这个宽度下要几行」预留固定高度：文字长短不一时高度不变。
    /// 用最宽的汉字（中文按字号满宽）量，得到的就是上限，实际文字不会超出而被截断。
    /// </summary>
    private void ApplyStatusHeight(double width)
    {
        if (Math.Abs(_statusLayoutWidth - width) < 1 && _statusLabel.HeightRequest > 0)
        {
            return;
        }

        double available = Math.Max(140, width - 24);
        string keep = _statusLabel.Text;

        _statusLabel.Text = new string('测', StatusReserveChars);
        double measured = _statusLabel.Measure(available, double.PositiveInfinity).Height;
        _statusLabel.Text = keep;

        if (measured <= 1)
        {
            return;
        }

        _statusLayoutWidth = width;
        double reserved = Math.Ceiling(measured);
        if (Math.Abs(_statusLabel.HeightRequest - reserved) > 0.5)
        {
            _statusLabel.HeightRequest = reserved;
        }
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
            case Windows.System.VirtualKey.L:
                OnToggleDrawMode();
                break;
            case Windows.System.VirtualKey.K:
                _session.ToggleLinkStrength();
                RefreshDrawTools();
                break;
            case Windows.System.VirtualKey.T:
                OnTogglePaintTool();
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
