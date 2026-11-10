using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DocumentGenerator;

/// <summary>
/// Однорядковий текст, який сам підбирає, що показати за доступною шириною:
/// 1) повний текст; 2) прибирає слова з кінця; 3) коли лишилося одне слово — прибирає літери й додає "...";
/// 4) якщо не вміщається навіть "..." — нічого не малює.
/// Розмір шрифту не змінює (його зменшує вікно через ресурси FsXxx).
/// </summary>
public sealed class FitTextBlock : FrameworkElement
{
    private const FrameworkPropertyMetadataOptions TextFlags =
        FrameworkPropertyMetadataOptions.Inherits |
        FrameworkPropertyMetadataOptions.AffectsMeasure |
        FrameworkPropertyMetadataOptions.AffectsRender;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(FitTextBlock),
        new FrameworkPropertyMetadata(string.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CenteredProperty = DependencyProperty.Register(
        nameof(Centered), typeof(bool), typeof(FitTextBlock),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
        typeof(FitTextBlock), new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, TextFlags));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
        typeof(FitTextBlock), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, TextFlags));

    public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(
        typeof(FitTextBlock), new FrameworkPropertyMetadata(FontWeights.Normal, TextFlags));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(FitTextBlock), new FrameworkPropertyMetadata(SystemColors.ControlTextBrush,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value ?? string.Empty); }
    public bool Centered { get => (bool)GetValue(CenteredProperty); set => SetValue(CenteredProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public FitTextBlock() { ClipToBounds = true; }

    private FormattedText Format(string text, Brush? brush = null) => new(
        text.Length == 0 ? "A" : text,
        CultureInfo.CurrentUICulture,
        FlowDirection,
        new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal),
        Math.Max(1, FontSize),
        brush ?? Foreground ?? Brushes.Black,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private double WidthOf(string text) => text.Length == 0 ? 0 : Format(text, Brushes.Black).WidthIncludingTrailingWhitespace;

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Text ?? string.Empty;
        var full = Format(text, Brushes.Black);
        var width = text.Length == 0 ? 0 : full.WidthIncludingTrailingWhitespace;
        if (!double.IsInfinity(availableSize.Width)) width = Math.Min(width, availableSize.Width);
        return new Size(Math.Max(0, width), full.Height);
    }

    private string Fit(string text, double width)
    {
        if (text.Length == 0 || WidthOf(text) <= width) return text;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // Спочатку прибираємо слова з кінця.
        for (var n = words.Length - 1; n >= 1; n--)
        {
            var candidate = string.Join(' ', words, 0, n);
            if (WidthOf(candidate) <= width) return candidate;
        }

        // Лишилося одне слово — прибираємо літери й додаємо три крапки.
        var first = words.Length > 0 ? words[0] : text.Trim();
        for (var len = first.Length - 1; len >= 1; len--)
        {
            var candidate = first[..len] + "...";
            if (WidthOf(candidate) <= width) return candidate;
        }
        return WidthOf("...") <= width ? "..." : string.Empty;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        if (width <= 0) return;
        var text = Text ?? string.Empty;
        var fitted = Fit(text, width);
        ToolTip = fitted.Length != text.Length && text.Length > 0 ? text : null;
        if (fitted.Length == 0) return;

        var ft = Format(fitted);
        var x = Centered ? Math.Max(0, (width - ft.WidthIncludingTrailingWhitespace) / 2) : 0;
        var y = Math.Max(0, (ActualHeight - ft.Height) / 2);
        dc.DrawText(ft, new Point(x, y));
    }
}

/// <summary>Кнопка з текстом, що скорочується за доступною шириною (див. FitTextBlock).</summary>
public class FitButton : Button
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(FitButton),
        new PropertyMetadata(string.Empty, (d, e) => ((FitButton)d)._label.Text = e.NewValue as string ?? string.Empty));

    private readonly FitTextBlock _label = new()
    {
        Centered = true,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Center
    };

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public FitButton() { Content = _label; }
}
