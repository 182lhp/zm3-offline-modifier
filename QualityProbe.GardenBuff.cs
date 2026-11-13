#if QA
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

public partial class MainWindow
{
    private async Task VerifyGardenAndBuffs(string folder)
    {
        var snapshot=(JsonObject)save!.Draft.DeepClone();
        string disk=File.ReadAllText(save.Path), key=player!.Key;
        static JsonObject Plot(int id,int type,int grade)=>new() {["gid"]=id,["type"]=type,["plantingLevel"]=grade,["date"]="2026-11-10",["retained"]=$"plot-{id}"};
        var sample=new JsonObject {["gardenExp"]=0,["gardenList"]=new JsonArray(Plot(1,1,0))};
        foreach(var (experience,level) in new[]{(0,1),(59,1),(60,2),(119,2),(120,3),(179,3),(180,4),(239,4),(240,5),(int.MaxValue,5)})
            Require(GardenData.TempleLevel(experience)==level,"Temple thresholds use its independent 0/60/120/180/240 experience scale.");
        Require(Enumerable.Range(1,5).All(level=>UtilityArtwork.Image($"temple-{level}.png") is not null),"Temple artwork corresponds to all five levels.");
        Require(GardenData.Level(sample)==1 && new GardenRow(0,1,1,4,"").Status=="可种植" &&
            new GardenRow(0,1,2,1,"").Status=="可收获" && new GardenRow(0,1,4,1,"").Status=="待翻新",
            "Garden type controls state; plantingLevel is grade, not maturity.");
        GardenData.Apply(sample,Plot(4,2,5),false);
        Require(GardenData.Level(sample)==4 && GardenData.Rows(sample).Select(r=>r.Id).SequenceEqual(new[]{1,2,3,4}) &&
            GameJson.Text(sample["gardenList"]![0]!["retained"])=="plot-1", "Opening a later plot fills earlier plots and preserves existing fields.");
        var batch=Plot(1,5,5);GardenData.Apply(sample,batch,true);
        Require(GardenData.Rows(sample).All(r=>r.Type==5 && r.Level==5) && GameJson.Text(sample["gardenList"]![3]!["retained"])=="plot-4",
            "Batch planting preserves each plot's unrelated fields and supports grade five.");
        ((JsonObject)sample["gardenList"]![1]!)["type"]=4;
        Require(GardenData.Renew(sample,2)==1 && GameJson.Number(sample["gardenList"]![1]!["plantingLevel"])==0 &&
            GameJson.Text(sample["gardenList"]![1]!["date"])=="2026-11-10", "Renewing used soil changes only type and grade.");
        ((JsonObject)sample["gardenList"]![0]!)["plantingLevel"]=1;
        var emptyBefore=sample["gardenList"]![1]!.DeepClone();
        Require(GardenData.TopGrade(sample)==1 && JsonNode.DeepEquals(emptyBefore,sample["gardenList"]![1]), "Top-grade action skips empty and used plots.");
        bool invalid=false;
        try { var bad=Plot(1,2,6);GardenData.Validate(bad,new JsonObject(),null); } catch(InvalidDataException){invalid=true;}
        Require(invalid,"Grade six is rejected.");
        var duplicate=new JsonObject {["gardenList"]=new JsonArray(Plot(1,1,0),Plot(1,1,0))};var beforeDuplicate=duplicate.DeepClone();
        invalid=false;try {GardenData.Apply(duplicate,Plot(2,2,5),false);}catch(InvalidDataException){invalid=true;}
        Require(invalid && JsonNode.DeepEquals(duplicate,beforeDuplicate),"Duplicate plots are rejected without partial changes.");
        Require(BuffData.Kinds.Select(k=>k.Id).SequenceEqual(new[]{0,1,3,4,5,6,8}) && BuffData.Kinds.All(k=>UtilityArtwork.Image($"buff-{k.Id}.jpg") is not null),
            "Seven supported effect cards have original icons.");
        var buffs=new JsonObject {["savedBuffString"]="7|450|78460,opaque|keep|x,0|433|26493"};
        BuffData.AddAll(buffs);
        Require(GameJson.Text(buffs["savedBuffString"]).StartsWith("7|450|78460,opaque|keep|x,") &&
            BuffData.Kinds.All(k=>BuffData.Rows(buffs).Single(r=>r.Type==k.Id).Frames==86400), "Recommended effects preserve every other raw record.");
        var buffSnapshot=buffs.DeepClone();invalid=false;
        try {BuffData.Put(buffs,BuffData.Kinds[0],100,0);}catch(InvalidDataException){invalid=true;}
        Require(invalid && JsonNode.DeepEquals(buffs,buffSnapshot),"Invalid duration never changes records.");
        try
        {
            Require(SelectPage(NavGarden),"Garden navigation.");
            save.Draft["gardenExp"]=5400;save.Draft["landgodExp"]=9;ShowGarden();
            Require(GardenLevelText.Text=="药园 Lv. 9" && TempleLevelText.Text=="土地庙 Lv. 1" && TempleSummary.Text.Contains("9 / 60") &&
                ReferenceEquals(TemplePortrait.Source,UtilityArtwork.Image("temple-1.png")),"Level-nine garden and level-one temple are displayed independently with the correct temple portrait.");
            var templeBefore=(JsonObject)save.Draft.DeepClone();
            ProbeSettingsDialog<Window>(TempleEditButton,dialog=>
            {
                var choice=VisualDescendants(dialog).OfType<ComboBox>().Single();
                var experience=VisualDescendants(dialog).OfType<TextBox>().Single();
                choice.SelectedIndex=4;
                Require(experience.Text=="240","Choosing a temple level fills only the temple experience threshold.");
                Capture(folder,"temple-editor.png",dialog);ClickDialogButton(dialog,"取消");
            });
            Require(JsonNode.DeepEquals(save.Draft,templeBefore),"Canceling temple edits leaves the complete save untouched.");
            ProbeSettingsDialog<Window>(TempleEditButton,dialog=>
            {
                var experience=VisualDescendants(dialog).OfType<TextBox>().Single();
                experience.Text="120";
                Require(VisualDescendants(dialog).OfType<ComboBox>().Single().SelectedIndex==2,"Temple experience updates the previewed temple level.");
                ClickDialogButton(dialog,"应用到存档");
            });
            var expectedTemple=(JsonObject)templeBefore.DeepClone();expectedTemple["landgodExp"]=120;
            Require(JsonNode.DeepEquals(save.Draft,expectedTemple) && GardenLevelText.Text=="药园 Lv. 9" && TempleLevelText.Text=="土地庙 Lv. 3",
                "Applying temple edits changes exactly landgodExp and preserves garden experience, all plots, blessings, pets and other data.");
            save.Draft=templeBefore;ShowGarden();
            await CaptureWhenReady(folder,"garden-temple-levels.png");
            save.Draft["gardenExp"]=0;save.Draft["gardenList"]=null;
            var emptyView=save.Draft.DeepClone();ShowGarden();
            Require(GardenTable.Items.Count==9 && JsonNode.DeepEquals(save.Draft,emptyView),"Nine plot placeholders do not create save records while browsing.");
            GardenTable.SelectedItem=GardenTable.Items.Cast<GardenTile>().Single(t=>t.Id==4);
            ProbeSettingsDialog<GardenPlotWindow>(GardenPlantButton,dialog=>
            {
                dialog.PlantBox.SelectedItem=GardenData.Plants.Single(p=>p.Type==5);dialog.GradeBox.SelectedIndex=4;
                Capture(folder,"garden-planting.png",dialog);ClickDialogButton(dialog,"取消");
            });
            Require(JsonNode.DeepEquals(save.Draft,emptyView),"Canceling plot planting retains the whole save.");
            ProbeSettingsDialog<GardenPlotWindow>(GardenPlantButton,dialog=>
            {
                dialog.PlantBox.SelectedItem=GardenData.Plants.Single(p=>p.Type==5);dialog.GradeBox.SelectedIndex=4;
                ClickDialogButton(dialog,"应用到存档");
            });
            Require(GardenData.Level(save.Draft)==4 && GardenData.Rows(save.Draft).Single(r=>r.Id==4) is {Type:5,Level:5},"Planting applies the selected named crop and full grade.");
            var cancelBatch=save.Draft.DeepClone();
            ProbeSettingsDialog<GardenPlotWindow>(GardenBatchButton,dialog=>{dialog.PlantBox.SelectedIndex=2;ClickDialogButton(dialog,"取消");});
            Require(JsonNode.DeepEquals(save.Draft,cancelBatch),"Canceling batch planting is inert.");
            ProbeSettingsDialog<GardenPlotWindow>(GardenBatchButton,dialog=>
            {dialog.PlantBox.SelectedItem=GardenData.Plants.Single(p=>p.Type==3);dialog.GradeBox.SelectedIndex=4;ClickDialogButton(dialog,"应用到存档");});
            Require(GardenData.Rows(save.Draft).All(r=>r.Type==3 && r.Level==5),"Batch applies to all unlocked plots.");
            save.Draft["gardenExp"]=1100;save.Draft["gardenList"]=new JsonArray(Plot(1,1,0),Plot(2,2,5),Plot(3,5,4),Plot(4,3,3),Plot(5,4,0));
            ShowGarden();GardenTable.SelectedIndex=1;await CaptureWhenReady(folder,"garden.png");
            Require(SelectPage(NavBuff),"Effect card navigation.");
            save.Draft["savedBuffString"]="7|450|78460,opaque|keep|x,1|2|49395";ShowBuffs();
            BuffTable.SelectedItem=BuffTable.Items.Cast<BuffTile>().Single(t=>t.Type==1);
            Require(BuffFramesBox.Text=="49395","Existing exact remaining duration is shown without rounding the saved value.");
            await VerifyCombo(BuffDurationBox,"effect duration");
            BuffValueBox.Text="400";BuffDurationBox.SelectedItem=BuffDurationBox.Items.Cast<BuffDuration>().Single(d=>d.Frames==21600);
            BuffApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(BuffData.Rows(save.Draft).Single(r=>r.Type==1) is {Value:3,Frames:21600} &&
                GameJson.Text(save.Draft["savedBuffString"]).StartsWith("7|450|78460,opaque|keep|x,"),"Effect card application enforces limits and chosen minutes without altering unrelated records.");
            BuffDurationBox.SelectedItem=BuffDurationBox.Items.Cast<BuffDuration>().Single(d=>d.Frames==0);BuffFramesBox.Text="24";
            BuffApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(BuffData.Rows(save.Draft).Single(r=>r.Type==1).Frames==24,"Custom precise duration applies.");
            BuffTable.SelectedItem=BuffTable.Items.Cast<BuffTile>().Single(t=>t.Type==7);
            Require(!BuffApplyButton.IsEnabled && BuffRemoveButton.IsEnabled,"Other old effects remain read-only and removable.");
            BuffRemoveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(!BuffData.Rows(save.Draft).Any(r=>r.Type==7) && GameJson.Text(save.Draft["savedBuffString"]).Contains("opaque|keep|x"),"Removing one card preserves unrelated raw data.");
            MaxBuffs(BuffPresetButton,new RoutedEventArgs());
            Require(BuffData.Kinds.All(k=>BuffData.Rows(save.Draft).Single(r=>r.Type==k.Id).Frames==86400),"Recommended effects use one hour.");
            BuffTable.SelectedIndex=1;await CaptureWhenReady(folder,"buffs.png");
            Require(File.ReadAllText(save.Path)==disk,"Garden and effect actions only edit the pending save.");
        }
        finally {save.Draft=snapshot;ShowSlot(key);}
    }
}
#endif
