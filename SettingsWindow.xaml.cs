using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace GmStudio;

public partial class SettingsWindow : Window
{
    private readonly MainWindow mainWindow;
    private readonly string? originalDirectory;
    internal string? SelectedDirectory { get; private set; }
    internal SettingsWindow(MainWindow owner,string? currentDirectory)
    {
        mainWindow=owner;InitializeComponent();Owner=owner;
        MaxHeight=SystemParameters.WorkArea.Height;
        originalDirectory=SelectedDirectory=currentDirectory;
        DirectoryText.Text=currentDirectory ?? "尚未选择游戏目录";
        RefreshPreferenceSummary();
        GiftSettingsButton.IsEnabled=currentDirectory is not null;
    }
    private void RefreshPreferenceSummary()
    {
        var choice=((App)Application.Current).Appearance.Current;
        string chinese=FontPreferences.ChineseOptions.First(option=>option.Id==choice.Chinese).Label;
        string latin=FontPreferences.LatinOptions.First(option=>option.Id==choice.Latin).Label;
        FontSummary.Text=$"当前：{chinese} · {latin}";
        SidebarSummary.Text=$"{mainWindow.SidebarPageCount} 个栏目 · 名称与顺序自动保留";
    }
    private void FontSettingsClick(object sender,RoutedEventArgs e)
    {
        new FontSettingsWindow(this).ShowDialog();RefreshPreferenceSummary();
    }
    private void GiftSettingsClick(object sender,RoutedEventArgs e)
    {
        if(originalDirectory is null)return;
        try {new LocalGiftWindow(this,new GameFolder(originalDirectory)).ShowDialog();}
        catch(Exception ex) when(ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {PromptDialog.Alert(this,"无法打开兑换码管理",ex.Message);}
    }
    private void SidebarSettingsClick(object sender,RoutedEventArgs e)
    {
        mainWindow.EditSidebar(this);RefreshPreferenceSummary();
    }
    private void ChooseDirectoryClick(object sender,RoutedEventArgs e)
    {
        var picker=new OpenFolderDialog {Title="选择造梦西游 3 单机版目录"};
        if(SelectedDirectory is { } directory && Directory.Exists(directory)) picker.InitialDirectory=directory;
        if(picker.ShowDialog(this)==true) SelectDirectory(picker.FolderName);
    }
    internal bool SelectDirectory(string path)
    {
        try
        {
            string fullPath=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            if(!GameFolder.IsGameFolder(fullPath)) throw new InvalidDataException("请选择同时包含 game、saves 和 schema 的游戏主文件夹。");
            SelectedDirectory=fullPath;DirectoryText.Text=fullPath;DirectoryError.Visibility=Visibility.Collapsed;
            ApplyDirectoryButton.IsEnabled=!string.Equals(Path.TrimEndingDirectorySeparator(originalDirectory ?? ""),fullPath,StringComparison.OrdinalIgnoreCase);
            return true;
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {DirectoryError.Text=ex.Message;DirectoryError.Visibility=Visibility.Visible;return false;}
    }
    private void ApplyClick(object sender,RoutedEventArgs e)
    {
        if(ApplyDirectoryButton.IsEnabled && SelectedDirectory is not null) DialogResult=true;
    }
    private void CancelClick(object sender,RoutedEventArgs e)=>DialogResult=false;
}
