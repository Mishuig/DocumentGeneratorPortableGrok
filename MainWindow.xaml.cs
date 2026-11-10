using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using DocumentGenerator.Domain;
using DocumentGenerator.Infrastructure;
using DocumentGenerator.Services;

namespace DocumentGenerator;

public partial class MainWindow : Window
{
    private readonly TemplateService _templates = new();
    private readonly DocxTemplateEngine _engine = new();
    private readonly VariableStore _variableStore = new();

    private readonly Dictionary<string, TextBox> _inputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _labels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FitTextBlock> _labelBlocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _fieldOrder = new();
    private readonly Dictionary<string, Border> _fieldCards = new(StringComparer.OrdinalIgnoreCase);

    private TemplateRecord? _selected;
    private bool _loading;
    private Point _dragStart;
    private Border? _dragCard;
    private string? _dragKey;

    public MainWindow()
    {
        InitializeComponent();
        UpdateResponsiveUi(Width, Height);
        Loaded += async (_, _) => await LoadTemplatesAsync();
    }

    private async Task LoadTemplatesAsync(int? selectId = null)
    {
        try
        {
            _loading = true;
            var items = await _templates.GetAllAsync(SearchBox.Text);
            TemplatesList.ItemsSource = items;
            var selected = items.FirstOrDefault(x => x.Id == (selectId ?? _selected?.Id));
            TemplatesList.SelectedItem = selected ?? (items.Count > 0 ? items[0] : null);
            if (TemplatesList.SelectedItem is null) ShowNoSelection();
        }
        catch (Exception ex) { ShowError("Не вдалося завантажити шаблони", ex); }
        finally { _loading = false; }
    }

    private async void ImportTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Оберіть Word-шаблон",
            Filter = "Word documents (*.docx)|*.docx",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var record = await _templates.ImportAsync(dialog.FileName);
            await LoadTemplatesAsync(record.Id);
            MessageBox.Show($"Шаблон «{record.Name}» імпортовано.", "Готово",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError("Не вдалося імпортувати DOCX", ex); }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loading && IsLoaded) await LoadTemplatesAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadTemplatesAsync();

    private async void TemplateRenamePencil_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button btn || btn.Tag is not TemplateRecord record) return;

        var dialog = new TemplateEditWindow(record.Name, record.Category) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            await _templates.RenameAsync(record.Id, dialog.TemplateName, dialog.Category);
            await LoadTemplatesAsync(record.Id);
        }
        catch (Exception ex) { ShowError("Не вдалося зберегти налаштування шаблону", ex); }
    }

    private void TemplatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selected is not null && _inputs.Count > 0)
            SaveCurrentState();

        _selected = TemplatesList.SelectedItem as TemplateRecord;
        ClearFieldUi();

        if (_selected is null) { ShowNoSelection(); return; }

        try
        {
            SelectedTemplateTitle.Text = _selected.Name;
            var path = AppPaths.ResolveTemplate(_selected.RelativePath);
            var keys = _engine.FindPlaceholders(path);
            var savedValues = _variableStore.LoadValues(_selected.Id);
            var savedLabels = _variableStore.LoadLabels(_selected.Id);
            var savedOrder = _variableStore.LoadOrder(_selected.Id);

            // Порядок: збережений, потім нові ключі
            _fieldOrder.Clear();
            foreach (var k in savedOrder)
                if (keys.Contains(k, StringComparer.OrdinalIgnoreCase) &&
                    !_fieldOrder.Contains(k, StringComparer.OrdinalIgnoreCase))
                    _fieldOrder.Add(k);
            foreach (var k in keys)
                if (!_fieldOrder.Contains(k, StringComparer.OrdinalIgnoreCase))
                    _fieldOrder.Add(k);

            TemplateInfo.Text = $"Категорія: {_selected.Category} • Змінних: {_fieldOrder.Count}";

            if (_fieldOrder.Count == 0)
            {
                FieldsPanel.Children.Add(new TextBlock
                {
                    Text = "У шаблоні немає змінних.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 4, 0, 12)
                });
                return;
            }

            foreach (var key in _fieldOrder)
            {
                var display = savedLabels.TryGetValue(key, out var lbl) && !string.IsNullOrWhiteSpace(lbl)
                    ? lbl
                    : Humanize(key);
                _labels[key] = display;

                var value = savedValues.TryGetValue(key, out var v) ? v : "";
                var card = BuildFieldCard(key, display, value);
                _fieldCards[key] = card;
                FieldsPanel.Children.Add(card);
            }
        }
        catch (Exception ex) { ShowError("Не вдалося прочитати шаблон", ex); }
    }

    private Border BuildFieldCard(string key, string display, string value)
    {
        var card = new Border
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Tag = key,
            AllowDrop = true
        };
        card.SetResourceReference(FrameworkElement.MarginProperty, "FieldMargin");

        var root = new StackPanel();

        // Label row: text + pencil
        var labelRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

        var editBtn = MakeIconButton("✎", "Змінити назву поля");
        editBtn.Tag = key;
        editBtn.Click += EditLabel_Click;
        editBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x0D, 0x94, 0x88));
        DockPanel.SetDock(editBtn, Dock.Right);
        labelRow.Children.Add(editBtn);

        var labelBlock = new FitTextBlock
        {
            Text = display,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
            VerticalAlignment = VerticalAlignment.Center
        };
        _labelBlocks[key] = labelBlock;
        labelRow.Children.Add(labelBlock);
        root.Children.Add(labelRow);

        // Input row: drag handle + textbox + up/down
        var inputRow = new DockPanel();

        var handle = new TextBlock
        {
            Text = "⋮⋮",
            Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.SizeAll
        };
        handle.SetResourceReference(TextBlock.FontSizeProperty, "FsHead");
        DockPanel.SetDock(handle, Dock.Left);
        handle.PreviewMouseLeftButtonDown += FieldCard_PreviewMouseLeftButtonDown;
        handle.MouseMove += FieldCard_MouseMove;
        inputRow.Children.Add(handle);

        var arrows = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        DockPanel.SetDock(arrows, Dock.Right);

        var upBtn = MakeIconButton("▲", "Вгору");
        upBtn.Tag = key;
        upBtn.Click += MoveUp_Click;
        upBtn.FontSize = 10;
        upBtn.Padding = new Thickness(4, 0, 4, 0);
        upBtn.MinHeight = 18;

        var downBtn = MakeIconButton("▼", "Вниз");
        downBtn.Tag = key;
        downBtn.Click += MoveDown_Click;
        downBtn.FontSize = 10;
        downBtn.Padding = new Thickness(4, 0, 4, 0);
        downBtn.MinHeight = 18;

        arrows.Children.Add(upBtn);
        arrows.Children.Add(downBtn);
        inputRow.Children.Add(arrows);

        var input = new TextBox
        {
            Tag = key,
            AcceptsReturn = IsMultilineKey(key),
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = value,
            Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xFD, 0xFA)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x99, 0xF6, 0xE4))
        };
        input.SetResourceReference(FrameworkElement.MinHeightProperty, IsMultilineKey(key) ? "InputMinHMulti" : "InputMinH");
        _inputs[key] = input;
        inputRow.Children.Add(input);

        root.Children.Add(inputRow);
        card.Child = root;

        card.DragOver += FieldsPanel_DragOver;
        card.Drop += FieldsPanel_Drop;
        card.PreviewMouseLeftButtonDown += FieldCard_PreviewMouseLeftButtonDown;
        card.MouseMove += FieldCard_MouseMove;

        return card;
    }

    private static Button MakeIconButton(string content, string tip)
    {
        var button = new Button
        {
            Content = content,
            ToolTip = tip,
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(2, 0, 2, 0),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
        };
        button.SetResourceReference(FrameworkElement.StyleProperty, "IconButton");
        button.SetResourceReference(FrameworkElement.MinHeightProperty, "IconSize");
        button.SetResourceReference(FrameworkElement.MinWidthProperty, "IconSize");
        button.SetResourceReference(Control.FontSizeProperty, "FsSmall");
        return button;
    }

    private void FieldCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragCard = FindParentCard(e.OriginalSource as DependencyObject);
        _dragKey = _dragCard?.Tag as string;
    }

    private void FieldCard_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragCard is null || _dragKey is null)
            return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        // Не починати drag, якщо тягнуть з TextBox
        if (FindParent<TextBox>(e.OriginalSource as DependencyObject) is not null)
            return;

        DragDrop.DoDragDrop(_dragCard, new DataObject("FieldKey", _dragKey), DragDropEffects.Move);
        _dragCard = null;
        _dragKey = null;
    }

    private void FieldsPanel_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("FieldKey"))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        else e.Effects = DragDropEffects.None;
    }

    private void FieldsPanel_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("FieldKey")) return;
        if (e.Data.GetData("FieldKey") is not string sourceKey) return;

        string? targetKey = null;
        var card = FindParentCard(e.OriginalSource as DependencyObject);
        if (card?.Tag is string tk) targetKey = tk;

        if (targetKey is null || string.Equals(sourceKey, targetKey, StringComparison.OrdinalIgnoreCase))
            return;

        var from = _fieldOrder.FindIndex(k => string.Equals(k, sourceKey, StringComparison.OrdinalIgnoreCase));
        var to = _fieldOrder.FindIndex(k => string.Equals(k, targetKey, StringComparison.OrdinalIgnoreCase));
        if (from < 0 || to < 0) return;

        _fieldOrder.RemoveAt(from);
        // after removal, index may shift
        to = _fieldOrder.FindIndex(k => string.Equals(k, targetKey, StringComparison.OrdinalIgnoreCase));
        if (to < 0) _fieldOrder.Add(sourceKey);
        else _fieldOrder.Insert(to, sourceKey);

        RebuildFieldsPanelPreserveValues();
        PersistOrder();
        e.Handled = true;
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string key) return;
        var idx = _fieldOrder.FindIndex(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        if (idx <= 0) return;
        (_fieldOrder[idx - 1], _fieldOrder[idx]) = (_fieldOrder[idx], _fieldOrder[idx - 1]);
        RebuildFieldsPanelPreserveValues();
        PersistOrder();
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string key) return;
        var idx = _fieldOrder.FindIndex(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        if (idx < 0 || idx >= _fieldOrder.Count - 1) return;
        (_fieldOrder[idx + 1], _fieldOrder[idx]) = (_fieldOrder[idx], _fieldOrder[idx + 1]);
        RebuildFieldsPanelPreserveValues();
        PersistOrder();
    }

    private void RebuildFieldsPanelPreserveValues()
    {
        // snapshot values
        var values = _inputs.ToDictionary(x => x.Key, x => x.Value.Text ?? "", StringComparer.OrdinalIgnoreCase);
        var labels = new Dictionary<string, string>(_labels, StringComparer.OrdinalIgnoreCase);

        ClearFieldUi(keepOrder: true);

        foreach (var key in _fieldOrder)
        {
            var display = labels.TryGetValue(key, out var lbl) ? lbl : Humanize(key);
            _labels[key] = display;
            var value = values.TryGetValue(key, out var v) ? v : "";
            var card = BuildFieldCard(key, display, value);
            _fieldCards[key] = card;
            FieldsPanel.Children.Add(card);
        }
    }

    private void PersistOrder()
    {
        if (_selected is null) return;
        _variableStore.SaveOrder(_selected.Id, _fieldOrder);
    }

    private void EditLabel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string key) return;
        if (_selected is null) return;

        var current = _labels.TryGetValue(key, out var l) ? l : Humanize(key);
        var dialog = new FieldLabelWindow(current, key) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        _labels[key] = dialog.LabelResult;
        if (_labelBlocks.TryGetValue(key, out var block))
            block.Text = dialog.LabelResult;

        _variableStore.SaveLabels(_selected.Id, _labels);
    }

    private void ClearFieldUi(bool keepOrder = false)
    {
        _inputs.Clear();
        _labels.Clear();
        _labelBlocks.Clear();
        _fieldCards.Clear();
        if (!keepOrder) _fieldOrder.Clear();
        FieldsPanel.Children.Clear();
    }

    private void ShowNoSelection()
    {
        _selected = null;
        ClearFieldUi();
        SelectedTemplateTitle.Text = "Оберіть шаблон";
        TemplateInfo.Text = "";
    }

    private static string Humanize(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "(порожня змінна)";
        var spaced = System.Text.RegularExpressions.Regex.Replace(key, "([a-zа-яіїєґ])([A-ZА-ЯІЇЄҐ])", "$1 $2");
        return spaced.Replace('_', ' ');
    }

    private static bool IsMultilineKey(string key) =>
        key.Contains("address", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("адреса", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("comment", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("коментар", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("note", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("text", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("текст", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("description", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("опис", StringComparison.OrdinalIgnoreCase);

    private static Border? FindParentCard(DependencyObject? child)
    {
        while (child is not null)
        {
            if (child is Border b && b.Tag is string)
                return b;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T t) return t;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private void OpenTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(AppPaths.ResolveTemplate(_selected.RelativePath))
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) { ShowError("Не вдалося відкрити файл", ex); }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        if (MessageBox.Show(
                $"Видалити шаблон «{_selected.Name}» з бібліотеки? Файл буде переміщено до backups.",
                "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            await _templates.DeleteAsync(_selected.Id);
            await LoadTemplatesAsync();
        }
        catch (Exception ex) { ShowError("Не вдалося видалити шаблон", ex); }
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _templates.BackupAllAsync();
            MessageBox.Show($"Резервну копію створено у папці:\n{AppPaths.Backups}",
                "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError("Не вдалося створити резервну копію", ex); }
    }

    private void ClearFields_Click(object sender, RoutedEventArgs e)
    {
        foreach (var input in _inputs.Values) input.Clear();
        if (_selected is not null) SaveCurrentState();
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetValues(out var values)) return;

        try
        {
            SaveCurrentState();
            var templatePath = AppPaths.ResolveTemplate(_selected!.RelativePath);
            var previewDir = Path.Combine(AppPaths.Exports, "preview");
            Directory.CreateDirectory(previewDir);

            var previewName = $"preview_{SafeFileName(_selected.Name)}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
            var previewPath = Path.Combine(previewDir, previewName);

            _engine.Generate(templatePath, previewPath, values);
            Process.Start(new ProcessStartInfo(previewPath) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError("Помилка перегляду", ex); }
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetValues(out var values)) return;

        var suggested = $"{SafeFileName(_selected!.Name)}_{DateTime.Now:yyyyMMdd_HHmm}.docx";
        var choice = new SaveChoiceWindow(suggested) { Owner = this };
        if (choice.ShowDialog() != true || choice.Result == SaveChoiceWindow.SaveMode.Cancel)
            return;

        string targetPath;
        if (choice.Result == SaveChoiceWindow.SaveMode.AutoName)
        {
            Directory.CreateDirectory(AppPaths.Exports);
            targetPath = Path.Combine(AppPaths.Exports, suggested);
        }
        else
        {
            var dialog = new SaveFileDialog
            {
                Title = "Зберегти сформований документ",
                Filter = "Word document (*.docx)|*.docx",
                FileName = suggested,
                DefaultExt = ".docx",
                InitialDirectory = AppPaths.Exports
            };
            if (dialog.ShowDialog() != true) return;
            targetPath = dialog.FileName;
        }

        try
        {
            SaveCurrentState();
            var templatePath = AppPaths.ResolveTemplate(_selected.RelativePath);

            if (string.Equals(Path.GetFullPath(templatePath), Path.GetFullPath(targetPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Не можна зберігати результат поверх оригінального шаблону.");
            }

            await Task.Run(() => _engine.Generate(templatePath, targetPath, values));

            var open = MessageBox.Show(
                $"Документ збережено:\n{targetPath}\n\nВідкрити файл?",
                "Готово", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (open == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError("Помилка генерації документа", ex); }
    }

    private bool TryGetValues(out Dictionary<string, string> values)
    {
        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_selected is null)
        {
            MessageBox.Show("Спочатку оберіть шаблон.", "Увага",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (_inputs.Count == 0)
        {
            MessageBox.Show("У шаблоні немає змінних для заповнення.", "Увага",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        values = _inputs.ToDictionary(x => x.Key, x => x.Value.Text ?? "", StringComparer.OrdinalIgnoreCase);
        var missing = values.Where(x => string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Key).ToList();
        if (missing.Count > 0)
        {
            var names = missing.Select(k => _labels.TryGetValue(k, out var n) ? n : k);
            var result = MessageBox.Show(
                $"Не заповнені поля:\n{string.Join("\n", names)}\n\nПродовжити з порожніми значеннями?",
                "Перевірка даних", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return false;
        }
        return true;
    }

    private void SaveCurrentState()
    {
        if (_selected is null) return;
        if (_inputs.Count > 0)
        {
            var values = _inputs.ToDictionary(x => x.Key, x => x.Value.Text ?? "", StringComparer.OrdinalIgnoreCase);
            _variableStore.SaveValues(_selected.Id, values);
        }
        if (_labels.Count > 0)
            _variableStore.SaveLabels(_selected.Id, _labels);
        if (_fieldOrder.Count > 0)
            _variableStore.SaveOrder(_selected.Id, _fieldOrder);
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    private static void ShowError(string title, Exception ex)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppPaths.Logs, "application.log"),
                $"{DateTime.UtcNow:O} | {title}\n{ex}\n\n");
        }
        catch { /* ignore */ }

        MessageBox.Show(
            $"{title}.\n{ex.Message}\n\nДеталі записано в logs/application.log.",
            "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
