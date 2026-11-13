#if QA
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

public partial class MainWindow
{
    private void ProbePreferenceDialog<T>(Func<SettingsWindow,Button> opener,Action<T> interact) where T : Window
    {
        var originalPage=active;bool originalRaw=showingRaw;
        ProbeSettingsDialog<SettingsWindow>(SettingsButton,settings=>
        {
            ProbeSettingsDialog(opener(settings),interact,settings);
            Require(settings.IsVisible && settings.IsEnabled,"Closing a preference editor returns to the settings hub.");
            var choice=((App)Application.Current).Appearance.Current;
            Require(settings.FontSummary.Text.Contains(FontPreferences.ChineseOptions.Single(f=>f.Id==choice.Chinese).Label) &&
                settings.FontSummary.Text.Contains(FontPreferences.LatinOptions.Single(f=>f.Id==choice.Latin).Label),
                "The hub summarizes the saved Chinese and Latin font selections.");
            ClickDialogButton(settings,"关闭");
        });
        Require(active==originalPage && showingRaw==originalRaw,"Preferences preserve the current editing page.");
    }

    private async Task VerifySettingsAndTools(string folder)
    {
        VerifyUnifiedRules(folder);
        var originalGame=game!;var originalDraft=save!.Draft.DeepClone();
        string originalPath=save.Path;string originalDisk=File.ReadAllText(originalPath);
        Require(SidebarItems.Children.OfType<RadioButton>().Count()==7 && !sidebar.Defaults.Any(d=>d.Name=="原始 JSON"),
            "Common navigation excludes raw JSON while settings and tools remain separate.");
        Require(FindName("FontSettingsButton") is null && FindName("SidebarSettingsButton") is null,
            "Font and sidebar settings no longer have duplicate main-window entries.");
        Require(Equals(SaveButton.Content,"保存") && FindName("PushButton") is null &&
            SaveButton.ToolTip.ToString()!.Contains("自动推送"),"A single save action replaces the separate push button.");
        string alternate=Path.Combine(folder,"settings-alternate");
        Directory.CreateDirectory(Path.Combine(alternate,"game"));Directory.CreateDirectory(Path.Combine(alternate,"schema"));Directory.CreateDirectory(Path.Combine(alternate,"saves"));
        File.Copy(Path.Combine(originalGame.Root,"game","launcher.swf"),Path.Combine(alternate,"game","launcher.swf"));
        File.Copy(Path.Combine(originalGame.Root,"schema","library_final.json"),Path.Combine(alternate,"schema","library_final.json"));
        ProbeSettingsDialog<SettingsWindow>(SettingsButton,dialog=>
        {
            Require(dialog.DirectoryText.Text==originalGame.Root && !dialog.ApplyDirectoryButton.IsEnabled,"Settings show the complete current path without a pending switch.");
            Require(dialog.FontSettingsButton.IsVisible && dialog.SidebarSettingsButton.IsVisible && dialog.SidebarSummary.Text.Contains("7 个栏目"),
                "Fonts, sidebar and directory share one settings hub.");
            Require(!dialog.SelectDirectory(folder) && dialog.DirectoryError.IsVisible && dialog.SelectedDirectory==originalGame.Root,
                "Invalid directories receive inline feedback without changing the active selection.");
            Require(dialog.SelectDirectory(alternate) && dialog.ApplyDirectoryButton.IsEnabled,"A valid alternative can be reviewed before applying.");
            Require(dialog.SelectDirectory(originalGame.Root) && !dialog.ApplyDirectoryButton.IsEnabled,"Selecting the current directory is a no-op.");
            Capture(folder,"settings.png",dialog);
            double originalMaximum=dialog.MaxHeight;
            dialog.MaxHeight=590;dialog.UpdateLayout();
            Require(dialog.CloseSettingsButton.IsVisible && dialog.ApplyDirectoryButton.IsVisible &&
                dialog.CloseSettingsButton.TranslatePoint(new Point(),dialog).Y+dialog.CloseSettingsButton.ActualHeight<=dialog.ActualHeight,
                "The settings footer remains available in a short window.");
            Capture(folder,"settings-compact.png",dialog);
            dialog.MaxHeight=originalMaximum;dialog.UpdateLayout();
            dialog.SelectDirectory(alternate);ClickDialogButton(dialog,"关闭");
        });
        Require(ReferenceEquals(game,originalGame) && JsonNode.DeepEquals(save.Draft,originalDraft),"Canceling settings preserves both the directory and pending save.");
        ProbeSettingsDialog<SettingsWindow>(SettingsButton,dialog=>{dialog.SelectDirectory(alternate);ClickDialogButton(dialog,"应用目录");});
        Require(game!.Root==Path.GetFullPath(alternate) && save is null && !Directory.GetFiles(Path.Combine(alternate,"saves")).Any(),
            "Applying a directory loads it without creating or overwriting game saves.");
        Connect(originalGame.Root);
        Require(save!.Path==originalPath && File.ReadAllText(originalPath)==originalDisk && JsonNode.DeepEquals(save.Draft,originalDraft),
            "Reconnecting retains the original save data.");
        AdvancedToolsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menu=AdvancedToolsButton.ContextMenu;
        Require(menu?.Items.Count==3 && menu.Items[0] is MenuItem {Header:"原始 JSON",IsEnabled:true} &&
            menu.Items[1] is MenuItem {Header:"游戏日志",IsEnabled:true} && menu.Items[2] is MenuItem {Header:"资源版本",IsEnabled:true},"Advanced tools exposes original JSON and game logs.");
        string resourceRoot=Path.Combine(originalGame.Root,"resource-qa");
        var release=ResourceRelease.Read(resourceRoot);Require(release.Verify(resourceRoot).Count==0,"C# validates the resource fingerprint produced by the Python release builder.");
        var resourceDialog=new ResourceVersionWindow(this,resourceRoot);resourceDialog.Show();resourceDialog.UpdateLayout();
        Require(resourceDialog.VersionText.Text.Contains("2.33.0")&&resourceDialog.VerifyButton.IsEnabled,"Resource inspection is available offline from the tools menu.");
        Capture(folder,"resource-version.png",resourceDialog);resourceDialog.Close();
        File.WriteAllText(Path.Combine(resourceRoot,"game","probe.swf"),"changed");Require(release.Verify(resourceRoot).Count==1,"Resource inspection detects changes without modifying the file.");
        menu!.IsOpen=false;((MenuItem)menu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Require(showingRaw && RawPage.IsVisible && !SidebarItems.Children.OfType<RadioButton>().Any(b=>b.IsChecked==true),
            "Opening advanced JSON clears common-page selection without losing the last common page.");
        var draft=GameJson.Parse(RawText.Text);draft["settings_probe"]="retain";RawText.Text=draft.ToJsonString(GameJson.Format);
        ReturnFromRawButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(!showingRaw && active==NavCatalog && NavCatalog.IsChecked==true && GameJson.Text(save.Draft["settings_probe"])=="retain" &&
            File.ReadAllText(originalPath)==originalDisk,"Returning applies valid JSON to the pending copy without writing disk.");
        save.Draft=(JsonObject)originalDraft.DeepClone();ShowSlot();
        SelectPage(NavVip);Require(OpenRawPage(),"Open advanced JSON from membership.");
        Require(SaveDrafts(true)==(true,false),"Pushing while viewing advanced JSON targets the slot, even after membership was the last common page.");
        Require(File.ReadAllText(originalPath)==originalDisk,"A clean push preparation does not rewrite the save.");
        ReturnFromRawButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(active==NavVip && !showingRaw && NavVip.IsChecked==true,"Return restores the last common page.");
        SelectPage(NavCatalog);
        await CaptureWhenReady(folder,"settings-navigation.png");
    }
}
#endif
