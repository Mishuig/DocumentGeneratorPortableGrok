using System.Windows;

namespace DocumentGenerator;

public partial class FieldLabelWindow : Window
{
    public string LabelResult { get; private set; } = "";

    public FieldLabelWindow(string currentLabel, string variableKey)
    {
        InitializeComponent();
        LabelBox.Text = currentLabel;
        LabelBox.Focus();
        LabelBox.SelectAll();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var text = (LabelBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("Вкажіть відображувану назву поля.", "Увага",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        LabelResult = text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
