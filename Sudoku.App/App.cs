using Sudoku.App.Game;
using Sudoku.App.Views;

namespace Sudoku.App;

/// <summary>应用入口：构建导航与主窗口。</summary>
public sealed class App : Application
{
    public App()
    {
        UserAppTheme = AppState.Settings.Theme;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var root = new NavigationPage(new MainPage());
        root.SetAppThemeColor(VisualElement.BackgroundColorProperty, Ui.LightBackground, Ui.DarkBackground);
        root.SetAppThemeColor(NavigationPage.BarBackgroundColorProperty, Ui.LightSurface, Ui.DarkSurface);
        root.SetAppThemeColor(NavigationPage.BarTextColorProperty, Ui.LightText, Ui.DarkText);

        return new Window(root)
        {
            Title = "数独",
            Width = 900,
            Height = 1000,
            MinimumWidth = 420,
            MinimumHeight = 620,
        };
    }
}
