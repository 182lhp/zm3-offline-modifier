using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace GmStudio;

public partial class FontSettingsWindow : Window
{
    private readonly App app;

    internal FontSettingsWindow(Window owner)
    {
        app = (App)Application.Current;
        InitializeComponent();
        Owner = owner;
        ChineseBox.ItemsSource = FontPreferences.ChineseOptions;
        LatinBox.ItemsSource = FontPreferences.LatinOptions;
        Select(app.Appearance.Current);
    }

    private FontSelection? Selected => ChineseBox?.SelectedItem is FontOption chinese && LatinBox?.SelectedItem is FontOption latin
        ? new FontSelection(chinese.Id, latin.Id) : null;

    private void Select(FontSelection choice)
    {
        ChineseBox.SelectedItem = FontPreferences.ChineseOptions.First(option => option.Id == choice.Chinese);
        LatinBox.SelectedItem = FontPreferences.LatinOptions.First(option => option.Id == choice.Latin);
    }

    private void PreviewChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Selected is FontSelection choice)
            TextElement.SetFontFamily(FontPreview, FontPreferences.Compose(choice));
    }

    private void ResetClick(object sender, RoutedEventArgs e) => Select(FontSelection.Default);

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not FontSelection choice)
            return;
        try
        {
            app.ApplyFonts(choice);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError.Text = "无法保存字体设置。请检查当前 Windows 用户的应用数据目录是否可写。";
            SaveError.Visibility = Visibility.Visible;
        }
    }
}
