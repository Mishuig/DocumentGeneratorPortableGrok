using System.Windows;
using System.Windows.Controls;

namespace DocumentGenerator;

/// <summary>
/// Адаптивна верстка. Від розміру вікна залежать:
///  • масштаб: шрифти зменшуються до 10 (включно), кнопки/відступи — до мінімуму;
///  • режим розкладки: Wide (3 колонки), Medium (2 колонки: бібліотека+дії | форма),
///    Narrow (все в стовпчик), Compact (менше ~150 px — лише логотип);
///  • текст у кнопках і заголовках скорочується в FitTextBlock: спершу слова, потім літери + "...".
/// </summary>
public partial class MainWindow
{
    private enum LayoutMode { Wide, Medium, Narrow, Compact }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateResponsiveUi(e.NewSize.Width, e.NewSize.Height);

    private static double Cl(double v, double min, double max) => Math.Max(min, Math.Min(max, v));

    private void UpdateResponsiveUi(double w, double h)
    {
        if (w <= 0 || h <= 0) return;

        // 1.0 — повний розмір (>= 1100x700), далі лінійно зменшуємо.
        var f = Math.Min(1.0, Math.Min(w / 1100.0, h / 700.0));
        double Fs(double original) => Math.Min(original, Math.Max(10.0, original * f)); // шрифт не менше 10

        Resources["FsTitle"] = Fs(18);
        Resources["FsHead"] = Fs(15);
        Resources["FsBase"] = Fs(13);
        Resources["FsSmall"] = Fs(12);
        Resources["FsTiny"] = Fs(11);

        var btnGap = Cl(4 * f, 1, 4);
        var gap = Cl(16 * f, 4, 16);
        Resources["BtnH"] = Cl(40 * f, 24, 40);
        Resources["BtnPad"] = new Thickness(Cl(14 * f, 3, 14), Cl(8 * f, 1, 8), Cl(14 * f, 3, 14), Cl(8 * f, 1, 8));
        Resources["BtnGap"] = new Thickness(btnGap);
        Resources["BtnGridMargin"] = new Thickness(-btnGap, Cl(10 * f, 3, 10) - btnGap, -btnGap, -btnGap);
        Resources["IconSize"] = Cl(28 * f, 18, 28);
        Resources["InputPad"] = new Thickness(Cl(12 * f, 3, 12), Cl(10 * f, 2, 10), Cl(12 * f, 3, 12), Cl(10 * f, 2, 10));
        Resources["ItemPad"] = new Thickness(Cl(12 * f, 3, 12), Cl(10 * f, 2, 10), Cl(12 * f, 3, 12), Cl(10 * f, 2, 10));
        Resources["CardPad"] = new Thickness(Cl(16 * f, 3, 16));
        Resources["FormPad"] = new Thickness(Cl(20 * f, 3, 20));
        Resources["BodyMargin"] = new Thickness(Cl(18 * f, 3, 18));
        Resources["HeaderPad"] = new Thickness(Cl(20 * f, 4, 20), Cl(8 * f, 2, 8), Cl(20 * f, 4, 20), Cl(8 * f, 2, 8));
        Resources["LogoH"] = Cl(44 * f, 16, 44);
        Resources["FieldMargin"] = new Thickness(0, 0, 0, Cl(14 * f, 4, 14));
        Resources["InputMinH"] = Cl(40 * f, 24, 40);
        Resources["InputMinHMulti"] = Cl(72 * f, 36, 72);

        var compact = w < 150 || h < 150;
        var mode = compact ? LayoutMode.Compact
                 : w >= 760 ? LayoutMode.Wide
                 : w >= 400 ? LayoutMode.Medium
                 : LayoutMode.Narrow;

        CompactLogo.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        BodyGrid.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        HeaderBar.Visibility = compact || h < 300 ? Visibility.Collapsed : Visibility.Visible;
        if (compact) return;

        ApplyLayoutMode(mode, gap, h);
    }

    private void ApplyLayoutMode(LayoutMode mode, double gap, double height)
    {
        BodyGrid.ColumnDefinitions.Clear();
        BodyGrid.RowDefinitions.Clear();

        foreach (var card in new FrameworkElement[] { LibraryCard, FormCard, ActionsCard })
        {
            card.Margin = new Thickness(0);
            Grid.SetRow(card, 0);
            Grid.SetColumn(card, 0);
            Grid.SetRowSpan(card, 1);
            Grid.SetColumnSpan(card, 1);
        }

        ActionsTitle.Visibility = mode == LayoutMode.Wide ? Visibility.Visible : Visibility.Collapsed;

        switch (mode)
        {
            case LayoutMode.Wide:
                // Бібліотека | Форма | Дії
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star), MaxWidth = 380 });
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) });
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MaxWidth = 220 });
                BodyGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                Grid.SetColumn(FormCard, 2);
                Grid.SetColumn(ActionsCard, 4);
                ActionsGrid.Columns = 1;
                break;

            case LayoutMode.Medium:
                // Ліворуч: бібліотека + дії, праворуч: форма (підходить для половини екрана)
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star), MaxWidth = 340 });
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Star) });
                BodyGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                BodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(FormCard, 2);
                Grid.SetRowSpan(FormCard, 2);
                Grid.SetRow(ActionsCard, 1);
                ActionsCard.Margin = new Thickness(0, gap, 0, 0);
                ActionsGrid.Columns = height < 450 ? 3 : 1;
                break;

            default: // Narrow: усе в один стовпчик (вузька половина екрана, малі вікна)
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                BodyGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
                BodyGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
                BodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(FormCard, 1);
                FormCard.Margin = new Thickness(0, gap, 0, 0);
                Grid.SetRow(ActionsCard, 2);
                ActionsCard.Margin = new Thickness(0, gap, 0, 0);
                ActionsGrid.Columns = 3;
                break;
        }
    }
}
