using Microsoft.Maui.Controls.Shapes;

namespace Sudoku.App.Views;

/// <summary>界面控件工厂：统一深浅双主题的配色与风格。</summary>
internal static class Ui
{
    public static readonly Color LightBackground = Color.FromArgb("#F4F6F9");
    public static readonly Color DarkBackground = Color.FromArgb("#15181D");
    public static readonly Color LightSurface = Color.FromArgb("#FFFFFF");
    public static readonly Color DarkSurface = Color.FromArgb("#1E2229");
    public static readonly Color LightText = Color.FromArgb("#1F2430");
    public static readonly Color DarkText = Color.FromArgb("#ECEFF4");
    public static readonly Color LightMuted = Color.FromArgb("#6B7280");
    public static readonly Color DarkMuted = Color.FromArgb("#9AA4B2");
    public static readonly Color LightBorder = Color.FromArgb("#E1E6EE");
    public static readonly Color DarkBorder = Color.FromArgb("#2B323C");
    public static readonly Color LightAccent = Color.FromArgb("#1565C0");
    public static readonly Color DarkAccent = Color.FromArgb("#1E4976");
    public static readonly Color LightAccentSoft = Color.FromArgb("#E3EDF9");
    public static readonly Color DarkAccentSoft = Color.FromArgb("#24303D");

    /// <summary>页面背景（跟随主题）。</summary>
    public static void ApplyBackground(VisualElement element) =>
        element.SetAppThemeColor(VisualElement.BackgroundColorProperty, LightBackground, DarkBackground);

    /// <summary>标题文本。</summary>
    public static Label Title(string text, double size = 24)
    {
        var label = new Label
        {
            Text = text,
            FontSize = size,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
        };
        label.SetAppThemeColor(Label.TextColorProperty, LightText, DarkText);
        return label;
    }

    /// <summary>正文文本。</summary>
    public static Label Body(string text, double size = 14, TextAlignment alignment = TextAlignment.Start)
    {
        var label = new Label
        {
            Text = text,
            FontSize = size,
            HorizontalTextAlignment = alignment,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        label.SetAppThemeColor(Label.TextColorProperty, LightMuted, DarkMuted);
        return label;
    }

    /// <summary>主按钮。</summary>
    public static Button Primary(string text)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 16,
            HeightRequest = 52,
            CornerRadius = 12,
        };
        button.SetAppThemeColor(Button.BackgroundColorProperty, LightAccent, DarkAccent);
        button.SetAppThemeColor(Button.TextColorProperty, Colors.White, Color.FromArgb("#EAF3FF"));
        return button;
    }

    /// <summary>次按钮（描边风格）。</summary>
    public static Button Secondary(string text)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 15,
            HeightRequest = 50,
            CornerRadius = 12,
        };
        button.SetAppThemeColor(Button.BackgroundColorProperty, LightAccentSoft, DarkAccentSoft);
        button.SetAppThemeColor(Button.TextColorProperty, LightAccent, Color.FromArgb("#8FC2F5"));
        return button;
    }

    /// <summary>小按钮（工具条用）。</summary>
    public static Button Tool(string text)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 13,
            HeightRequest = 42,
            CornerRadius = 10,
            Padding = new Thickness(4, 0),
        };
        button.SetAppThemeColor(Button.BackgroundColorProperty, LightSurface, DarkSurface);
        button.SetAppThemeColor(Button.TextColorProperty, LightText, DarkText);
        return button;
    }

    /// <summary>数字键盘按钮。</summary>
    public static Button Digit(string text)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 54,
            CornerRadius = 10,
            Padding = 0,
        };
        button.SetAppThemeColor(Button.BackgroundColorProperty, LightSurface, DarkSurface);
        button.SetAppThemeColor(Button.TextColorProperty, LightAccent, Color.FromArgb("#8FC2F5"));
        return button;
    }

    /// <summary>卡片容器。</summary>
    public static Border Card(View content, double padding = 16)
    {
        var border = new Border
        {
            Content = content,
            Padding = padding,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            Margin = new Thickness(0, 6),
        };
        border.SetAppThemeColor(VisualElement.BackgroundColorProperty, LightSurface, DarkSurface);
        return border;
    }

    /// <summary>设置行：左标题右控件。</summary>
    public static Grid SettingRow(string title, string? subtitle, View control)
    {
        var left = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        var titleLabel = new Label { Text = title, FontSize = 15 };
        titleLabel.SetAppThemeColor(Label.TextColorProperty, LightText, DarkText);
        left.Add(titleLabel);

        if (!string.IsNullOrEmpty(subtitle))
        {
            left.Add(Body(subtitle, 12));
        }

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 12,
        };

        grid.Add(left, 0, 0);
        grid.Add(control, 1, 0);
        return grid;
    }

    /// <summary>分隔块（设置页分组标题）。</summary>
    public static Label SectionHeader(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(4, 14, 0, 0),
        };
        label.SetAppThemeColor(Label.TextColorProperty, LightMuted, DarkMuted);
        return label;
    }
}
