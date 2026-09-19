using Sudoku.App.Game;
using Sudoku.Core;

namespace Sudoku.App.Views;

/// <summary>
/// 技巧教程：按「基础 / 进阶 / 高阶」列出全部技巧，点进去看原理、怎么找、怎么做，
/// 还能用真实例题开一局「专项练习」——盘面直接停在只剩这一招的卡点上。
/// </summary>
public sealed class TutorialPage : ContentPage
{
    private readonly VerticalStackLayout _list;
    private readonly ScrollView _scroll;
    private readonly Button _topButton;
    private readonly Label _title;

    private TechniqueLesson? _open;

    public TutorialPage()
    {
        Title = "技巧教程";
        Ui.ApplyBackground(this);

        _title = Ui.Title("技巧教程", 24);

        _topButton = Ui.Tool("返回目录");
        _topButton.IsVisible = false;
        _topButton.Clicked += (_, _) => ShowCatalog();

        var header = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
        };
        header.Add(_title, 0, 0);
        header.Add(_topButton, 1, 0);

        _list = new VerticalStackLayout { Spacing = 4 };

        _scroll = new ScrollView { Content = _list };

        var root = new Grid
        {
            RowSpacing = 8,
            Padding = new Thickness(16, 14, 16, 20),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        root.Add(header, 0, 0);
        root.Add(_scroll, 0, 1);

        Content = root;

        ShowCatalog();
    }

    /// <summary>技巧目录：按分组列出全部教程。</summary>
    private void ShowCatalog()
    {
        _open = null;
        _title.Text = "技巧教程";
        _topButton.IsVisible = false;
        _list.Clear();

        _list.Add(Ui.Body("每种技巧都有：一句话说明、原理、怎么找、怎么做，以及一道从真实题目里采来的例题。想练手就点「专项练习」，盘面会直接停在这一招能用的位置。", 13));

        foreach (IGrouping<string, TechniqueLesson> group in TechniqueLessons.Grouped())
        {
            _list.Add(Ui.SectionHeader($"{group.Key}（{group.Count()} 种）"));

            foreach (TechniqueLesson lesson in group)
            {
                TechniqueLesson captured = lesson;
                Button open = Ui.Secondary($"{lesson.Name} · 第 {lesson.Level} 级");
                open.HorizontalOptions = LayoutOptions.Fill;
                open.Clicked += (_, _) => ShowLesson(captured);

                _list.Add(Ui.Card(new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        open,
                        Ui.Body(lesson.Summary, 13),
                    },
                }, 12));
            }
        }
    }

    /// <summary>单个技巧的教程详情。</summary>
    private void ShowLesson(TechniqueLesson lesson)
    {
        _open = lesson;
        _title.Text = lesson.Name;
        _topButton.IsVisible = true;
        _list.Clear();

        _list.Add(Ui.Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                Ui.Body($"第 {lesson.Level} 级 · {lesson.Group}", 12),
                Ui.Body(lesson.Summary, 14),
            },
        }));

        _list.Add(Ui.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Ui.SectionHeader("原理"),
                Ui.Body(lesson.Idea, 13),
                Ui.SectionHeader("怎么找"),
                Ui.Body(lesson.HowToFind, 13),
                Ui.SectionHeader("怎么做"),
                Ui.Body(lesson.Action, 13),
            },
        }));

        PracticeSetup? setup = lesson.HasExample ? TechniqueLessons.CreatePractice(lesson.Technique) : null;

        _list.Add(Ui.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Ui.SectionHeader("例题"),
                Ui.Body(setup is null
                    ? "这一招在真实题目里出现得很稀少，暂时没有采到合适的例题。可以先看上面的原理，或者去「导入导出」粘贴自己的题目；实战里遇到它时，「提示」会照常讲解。"
                    : $"这道题推进到某一步时，低阶技巧全部用尽、只剩这一招。\n\n{setup.Note}\n\n（点下面的按钮，盘面会直接停在这一步，由你来找。）", 13),
                PracticeButton(lesson, setup),
            },
        }));
    }

    private View PracticeButton(TechniqueLesson lesson, PracticeSetup? setup)
    {
        if (setup is null)
        {
            return lesson.HasExample
                ? Ui.Body("这道例题现在开不了局（盘面走不到这一招），请稍后再试。", 13)
                : Ui.Body("暂无可练习的例题。", 13);
        }

        Button practice = Ui.Primary($"专项练习 · {lesson.Name}");
        practice.Clicked += async (_, _) => await StartPracticeAsync(setup);
        return practice;
    }

    private async Task StartPracticeAsync(PracticeSetup setup)
    {
        GameSession session = GameSession.NewPractice(setup, AppState.Settings);
        await Navigation.PushAsync(new GamePage(session));
    }
}
