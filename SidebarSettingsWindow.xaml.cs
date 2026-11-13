using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

internal sealed class SidebarDraftRow(SidebarDefinition definition, string name, bool isActive) : INotifyPropertyChanged
{
    private string label = name;
    public string Id => definition.Id;
    public string OriginalName => definition.Name;
    public string? Glyph => definition.Glyph;
    public bool IsGroup => Glyph is null;
    public bool IsActive => isActive;
    public string Name
    {
        get => label;
        set
        {
            if (label == value) return;
            label = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class SidebarSettingsWindow : Window
{
    private readonly SidebarPreferences preferences;
    private readonly string activeId;
    private readonly Dictionary<string, SidebarDefinition> definitions;
    private readonly ObservableCollection<SidebarDraftRow> rows = [];

    internal SidebarSettingsWindow(Window owner, SidebarPreferences preferences, string activeId)
    {
        this.preferences = preferences;
        this.activeId = activeId;
        definitions = preferences.Defaults.ToDictionary(item => item.Id);
        InitializeComponent();
        Owner = owner;
        Width = Math.Clamp(SystemParameters.WorkArea.Width, MinWidth, Width);
        Height = Math.Clamp(SystemParameters.WorkArea.Height, MinHeight, Height);
        NameBox.MaxLength = SidebarPreferences.NameLimit;
        ColumnList.ItemsSource = rows;
        SidebarPreview.ItemsSource = rows;
        SetRows(preferences.Current, activeId);
    }

    private void SetRows(IEnumerable<SidebarEntry> entries, string? selectedId)
    {
        rows.Clear();
        foreach (var entry in entries)
            rows.Add(new SidebarDraftRow(definitions[entry.Id], entry.Name, entry.Id == activeId));
        ColumnList.SelectedItem = rows.FirstOrDefault(row => row.Id == selectedId) ?? rows[0];
        ColumnList.ScrollIntoView(ColumnList.SelectedItem);
        SaveError.Visibility = Visibility.Collapsed;
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateEditorState();

    private void UpdateEditorState()
    {
        int index = ColumnList.SelectedIndex;
        NameBox.IsEnabled = index >= 0;
        UpButton.IsEnabled = index > 0;
        DownButton.IsEnabled = index >= 0 && index < rows.Count - 1;
    }

    private void MoveUpClick(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDownClick(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int offset)
    {
        int index = ColumnList.SelectedIndex;
        int target = index + offset;
        if (index < 0 || target < 0 || target >= rows.Count) return;
        var selected = rows[index];
        rows.Move(index, target);
        ColumnList.SelectedItem = selected;
        ColumnList.ScrollIntoView(selected);
        UpdateEditorState();
    }

    private void ResetClick(object sender, RoutedEventArgs e) =>
        SetRows(preferences.Defaults.Select(item => new SidebarEntry(item.Id, item.Name)),
            (ColumnList.SelectedItem as SidebarDraftRow)?.Id);

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        var invalid = rows.FirstOrDefault(row => !SidebarPreferences.IsNameValid(row.Name.Trim()));
        if (invalid is not null)
        {
            ColumnList.SelectedItem = invalid;
            ColumnList.ScrollIntoView(invalid);
            NameBox.Focus();
            ShowError($"请输入 1 到 {SidebarPreferences.NameLimit} 个字符，名称不能包含控制字符。");
            return;
        }
        try
        {
            preferences.Save(rows.Select(row => new SidebarEntry(row.Id, row.Name)));
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError($"保存栏目设置失败。{ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        SaveError.Text = message;
        SaveError.Visibility = Visibility.Visible;
    }
}
