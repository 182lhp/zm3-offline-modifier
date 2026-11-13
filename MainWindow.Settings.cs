using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace GmStudio;

public partial class MainWindow
{
    private bool showingRaw;
    internal int SidebarPageCount => sidebar.Defaults.Count(item => item.Glyph is not null);
    private void ShowSettings(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        var dialog=new SettingsWindow(this,game?.Root);
        if(dialog.ShowDialog()==true && dialog.SelectedDirectory is { } directory && ConfirmDiscard()) Connect(directory);
    });
    private void OpenAdvancedTools(object sender,RoutedEventArgs e)
    {
        var menu=new ContextMenu {PlacementTarget=AdvancedToolsButton};
        var raw=new MenuItem {Header="原始 JSON",IsEnabled=save is not null};
        raw.Click+=(_,_)=>OpenRawPage();menu.Items.Add(raw);
        var logs=new MenuItem {Header="游戏日志",IsEnabled=game is not null,ToolTip="打开当前游戏的实时日志；服务未启动时打开本地日志文件夹。"};
        logs.Click+=OpenGameLog;menu.Items.Add(logs);
        var resources=new MenuItem {Header="资源版本",IsEnabled=game is not null};
        resources.Click+=(_,_)=>{if(game is not null)new ResourceVersionWindow(this,game.Root).ShowDialog();};menu.Items.Add(resources);
        AdvancedToolsButton.ContextMenu=menu;menu.IsOpen=true;
    }
    private async void OpenGameLog(object sender,RoutedEventArgs e)
    {
        var current=game;if(current is null)return;
        string address="http://127.0.0.1:43999/game-log";
        bool live=true;
        try {await current.CheckServerAsync();}
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException || Handled(ex))
        {
            live=false;address=Path.Combine(current.Root,"logs");
        }
        if(!ReferenceEquals(game,current))return;
        if(!live && !Directory.Exists(address)){ShowError("当前游戏目录还没有日志，请先运行一次游戏。");return;}
        try
        {
            Process.Start(new ProcessStartInfo(address){UseShellExecute=true})?.Dispose();
            Report(live?"已打开当前游戏的实时日志。":"已打开当前游戏的日志文件夹；启动游戏服务后可查看实时日志。");
        }
        catch(Exception ex) when(ex is Win32Exception or InvalidOperationException or IOException)
        {
            ShowError("无法打开游戏日志。"+ex.Message);
        }
    }
    private bool OpenRawPage()
    {
        try
        {
            ApplyForms();showingRaw=true;RefreshPageVisibility();PopulateRaw();return true;
        }
        catch(Exception ex) when(Handled(ex)) {RefreshNavigationState();ShowError(ex.Message);return false;}
    }
    private void ReturnFromRaw(object sender,RoutedEventArgs e)=>SelectPage(active);
    private void RefreshPageVisibility()
    {
        CatalogPage.Visibility=Show(NavCatalog);RolePage.Visibility=Show(NavRole);
        PetsPage.Visibility=Show(NavPets);SkillsPage.Visibility=Show(NavSkills);GardenPage.Visibility=Show(NavGarden);
        BuffPage.Visibility=Show(NavBuff);VipPage.Visibility=Show(NavVip);
        RawPage.Visibility=showingRaw ? Visibility.Visible : Visibility.Collapsed;
        RefreshNavigationState();
    }
    private void RefreshNavigationState()
    {
        foreach(var button in SidebarItems.Children.OfType<RadioButton>()) button.IsChecked=!showingRaw && button==active;
        if(showingRaw) AdvancedToolsButton.Background=(System.Windows.Media.Brush)FindResource("Green");
        else AdvancedToolsButton.ClearValue(Control.BackgroundProperty);
    }
}
