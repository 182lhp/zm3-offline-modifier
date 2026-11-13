using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

public partial class GardenPlotWindow : Window
{
    private readonly JsonObject source;
    private readonly string initialRaw;
    private readonly bool all;
    private bool ready;
    internal JsonObject? Result { get; private set; }
    internal GardenPlotWindow(Window owner, JsonObject source, bool all, int level)
    {
        this.source=(JsonObject)source.DeepClone(); this.all=all;
        InitializeComponent(); Owner=owner;
        Height=Math.Min(Height,SystemParameters.WorkArea.Height); MaxHeight=SystemParameters.WorkArea.Height;
        EditorTitle.Text=all ? "批量种植" : $"地块 {GameJson.Number(source["gid"])}";
        TargetHint.Text=all ? $"替换全部 {level} 个已开垦地块的植物" : GameJson.Number(source["gid"])>level ? "应用时开垦此地块，并补足药园等级。" : "选择植物与品级，取消不会修改地块。";
        var plants=GardenData.Plants.ToList(); int type=GameJson.Number(source["type"],2);
        if (!plants.Any(p=>p.Type==type)) plants.Add(GardenData.Plant(type));
        PlantBox.ItemsSource=plants; PlantBox.SelectedItem=plants.Single(p=>p.Type==type);
        GradeBox.ItemsSource=Enumerable.Range(1,5).Select(GardenData.Grade).ToArray();
        GradeBox.SelectedIndex=Math.Clamp(GameJson.Number(source["plantingLevel"],5),1,5)-1;
        DateBox.Text=GameJson.Text(source["date"]) is {Length:>0} date ? date : DateTime.Today.ToString("yyyy-MM-dd");
        initialRaw=source.ToJsonString(GameJson.Format); RawBox.Text=initialRaw;
        RawPanel.Visibility=all ? Visibility.Collapsed : Visibility.Visible;
        ready=true; ShowChoice();
    }
    private void ChoiceChanged(object sender, SelectionChangedEventArgs e) { if(ready) ShowChoice(); }
    private void ShowChoice()
    {
        if(PlantBox.SelectedItem is not GardenPlant plant) return;
        PlantPreview.Source=plant.Icon; PlantDescription.Text=plant.Description;
        GradeBox.IsEnabled=plant.Type is 2 or 3 or 5;
        ResultHint.Text=plant.Type is 2 or 3 or 5 ? $"{plant.Name} · {GardenData.Grade(GradeBox.SelectedIndex+1)}。当前单机包种下即可收获，回到游戏完成收获。" : plant.Type==1 ? "应用后清空当前植物，保留地块。" : plant.Description;
        ErrorText.Visibility=Visibility.Collapsed;
    }
    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if(PlantBox.SelectedItem is not GardenPlant plant) return;
            var result=!all && RawBox.Text!=initialRaw ? GameJson.Parse(RawBox.Text) : (JsonObject)source.DeepClone();
            if(all || RawBox.Text==initialRaw)
            {
                result["type"]=plant.Type; result["plantingLevel"]=plant.Type is 2 or 3 or 5 ? GradeBox.SelectedIndex+1 : 0;
                result["date"]=DateBox.Text.Trim();
            }
            var holder=new JsonObject {["gardenList"]=new JsonArray()};
            if(GameJson.Number(result["gid"])!=GameJson.Number(source["gid"]))
                throw new InvalidDataException("地块编号由当前选中的地块决定，请保留原编号。");
            GardenData.Validate(result,holder,null);
            Result=result; DialogResult=true;
        }
        catch(Exception ex) when(ex is InvalidDataException or System.Text.Json.JsonException)
        { ErrorText.Text=ex.Message; ErrorText.Visibility=Visibility.Visible; }
    }
    private void CancelClick(object sender,RoutedEventArgs e)=>DialogResult=false;
}
