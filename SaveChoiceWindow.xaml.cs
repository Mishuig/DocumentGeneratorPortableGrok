using System.Windows;

namespace DocumentGenerator;

public partial class SaveChoiceWindow : Window
{
    public enum SaveMode
    {
        Cancel,
        AutoName,
        CustomName
    }

    public SaveMode Result { get; private set; } = SaveMode.Cancel;
    public string SuggestedFileName { get; }

    public SaveChoiceWindow(string suggestedFileName)
    {
        InitializeComponent();
        SuggestedFileName = suggestedFileName;
        AutoNamePreview.Text = suggestedFileName;
    }

    private void AutoName_Click(object sender, RoutedEventArgs e)
    {
        Result = SaveMode.AutoName;
        DialogResult = true;
    }

    private void CustomName_Click(object sender, RoutedEventArgs e)
    {
        Result = SaveMode.CustomName;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = SaveMode.Cancel;
        DialogResult = false;
    }
}
