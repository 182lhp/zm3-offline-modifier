using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GmStudio;

public partial class MainWindow
{
    private void ShowGarden()
    {
        GardenPage.IsEnabled=save is not null;
        int selected=(GardenTable.SelectedItem as GardenTile)?.Id ?? 1;
        var rows=GardenData.Rows(save?.Draft); int level=GardenData.Level(save?.Draft);
        GardenSoil.Source=UtilityArtwork.Image("garden-soil.png");
        GardenGrass.Source=UtilityArtwork.Image("garden-exp.png");
        int templeLevel=GardenData.TempleLevel(save?.Draft);
        TemplePortrait.Source=UtilityArtwork.Image($"temple-{templeLevel}.png");
        TempleLevelText.Text=$"土地庙 Lv. {templeLevel}";
        int templeExp=GameJson.Number(save?.Draft["landgodExp"]);
        TempleSummary.Text=templeLevel==5 ? $"土地庙经验 {templeExp:N0} · 已满级" : $"土地庙经验 {templeExp:N0} / {GardenData.TempleThresholds[templeLevel]:N0}";
        GardenLevelText.Text=$"药园 Lv. {level}";
        GardenSummary.Text=$"药园经验 {GameJson.Number(save?.Draft["gardenExp"]):N0} · {rows.Count(r=>r.Type is 2 or 3 or 5)} 块可收获";
        GardenTable.ItemsSource=Enumerable.Range(1,9).Select(id=>new GardenTile(id,rows.FirstOrDefault(r=>r.Id==id),id<=level)).ToArray();
        GardenTable.SelectedItem=GardenTable.Items.Cast<GardenTile>().Single(t=>t.Id==selected);
        ShowGardenSelection();
    }
    private void GardenSelected(object sender,SelectionChangedEventArgs e)=>ShowGardenSelection();
    private void ShowGardenSelection()
    {
        if(GardenPlantButton is null || GardenTable.SelectedItem is not GardenTile tile) return;
        var plant=GardenData.Plant(tile.Row?.Type ?? 1);
        GardenSelectionTitle.Text=tile.Title; GardenSelectionState.Text=tile.State;
        GardenSelectionName.Text=tile.Name; GardenSelectionImage.Source=tile.Crop ?? tile.Soil;
        GardenSelectionDescription.Text=tile.Unlocked || tile.Row is not null ? plant.Description : $"药园达到 {tile.Id} 级时开启。应用种植时会补足对应经验，并保留已有地块。";
        GardenSelectionDate.Text=tile.Row is {Type:2 or 3 or 5} row ? $"{GardenData.Grade(row.Level)} · 种植日期 {row.Date}" : "选择植物后设置一至五品。";
        GardenPlantButton.Content=tile.Unlocked || tile.Row is not null ? "种植 / 编辑" : "开垦并种植";
        GardenRenewButton.IsEnabled=tile.Row?.Type==4;
    }
    private void EditPlot(object sender,RoutedEventArgs e)=>Guard(()=>OpenPlantEditor(false));
    private void PlantAllPlots(object sender,RoutedEventArgs e)=>Guard(()=>OpenPlantEditor(true));
    private void OpenPlantEditor(bool all)
    {
        if(save is null || GardenTable.SelectedItem is not GardenTile tile) return;
        var rows=GardenData.Rows(save.Draft);
        int level=Math.Max(GardenData.Level(save.Draft),rows.Select(r=>r.Id).DefaultIfEmpty(1).Max());
        JsonObject source=all || tile.Row is null ? new JsonObject {["gid"]=all ? 1 : tile.Id,["type"]=2,["plantingLevel"]=5,["date"]=""}
            : (JsonObject)GardenData.Plots(save.Draft)[tile.Row.Index]!.DeepClone();
        var dialog=new GardenPlotWindow(this,source,all,level);
        if(dialog.ShowDialog()!=true || dialog.Result is null) return;
        GardenData.Apply(save.Draft,dialog.Result,all);
        ShowGarden(); Report(all ? "已批量种植，等待保存。" : $"{tile.Title}已更新，等待保存。");
    }
    private void PlotDoubleClick(object sender,MouseButtonEventArgs e)
    {
        if(e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(GardenTable,source) is ListBoxItem) EditPlot(sender,e);
    }
    private void RenewSelectedPlot(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(save is null || GardenTable.SelectedItem is not GardenTile tile) return;
        int count=GardenData.Renew(save.Draft,tile.Id); ShowGarden();
        Report(count>0 ? $"{tile.Title}已翻新，等待保存。" : "当前地块无需翻新。");
    });
    private void TopGradePlots(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(save is null) return;
        int count=GardenData.TopGrade(save.Draft);ShowGarden();
        Report(count>0 ? $"已将 {count} 株植物设为五品，等待保存。" : "当前没有需要提升的植物。");
    });
    private void RenewAllPlots(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(save is null) return;
        int count=GardenData.Renew(save.Draft);ShowGarden();
        Report(count>0 ? $"已翻新 {count} 块已用土地，等待保存。" : "当前没有需要翻新的土地。");
    });
    private void GardenMore(object sender,RoutedEventArgs e)
    {
        if(sender is not Button button) return;
        var menu=new ContextMenu {PlacementTarget=button};
        var grade=new MenuItem {Header="全部植物设为五品"};grade.Click+=TopGradePlots;menu.Items.Add(grade);
        var renew=new MenuItem {Header="翻新全部已用土地"};renew.Click+=RenewAllPlots;menu.Items.Add(renew);menu.IsOpen=true;
    }
    private void EditGardenExp(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(save is null) return;
        var dialog=new EditorDialog(this,"药园经验","按经验提升药园等级。已有地块所需的经验下限会保留。");
        var box=dialog.Text("药园经验",Value(save.Draft,"gardenExp"));
        if(dialog.Show(()=>
        {
            int highest=GardenData.Rows(save.Draft).Select(r=>r.Id).Where(id=>id is >=1 and <=9).DefaultIfEmpty(1).Max();
            save.Draft["gardenExp"]=Math.Max(Input(box,"药园经验",0),GardenData.Thresholds[highest-1]);
        })) {ShowGarden();Report("药园经验已更新，等待保存。");}
    });

    private void EditTemple(object sender,RoutedEventArgs e)=>Guard(()=>
    {
        if(save is null) return;
        var dialog=new EditorDialog(this,"土地庙等级与经验","土地庙与药园独立升级。这里仅修改土地庙经验，保留药园等级与地块。");
        var level=dialog.Choices("土地庙等级",Enumerable.Range(1,5).Select(i=>$"Lv. {i}"),GardenData.TempleLevel(save.Draft)-1);
        var experience=dialog.Text("土地庙经验",Value(save.Draft,"landgodExp"));
        bool changing=false;
        level.SelectionChanged+=(_,_)=>
        {
            if(changing || level.SelectedIndex<0) return;
            changing=true;
            experience.Text=GardenData.TempleThresholds[level.SelectedIndex].ToString();
            changing=false;
        };
        experience.TextChanged+=(_,_)=>
        {
            if(changing || !int.TryParse(experience.Text,out int amount) || amount<0) return;
            changing=true;level.SelectedIndex=GardenData.TempleLevel(amount)-1;changing=false;
        };
        if(dialog.Show(()=>save.Draft["landgodExp"]=Input(experience,"土地庙经验",0)))
        {ShowGarden();Report("土地庙经验已更新，药园与地块保持原值，等待保存。");}
    });
}
