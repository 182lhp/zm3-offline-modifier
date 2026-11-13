#if QA
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

public partial class MainWindow
{
    private static void VerifyMaximumPetEquipment(GameFolder local,PetData pet)
    {
        var spirits=new Dictionary<string,string>
        {
            ["phoenix"]="p_sscylhjl",["tigress"]="p_sscybxjl",["nian"]="p_sscynsjl",
            ["turtle"]="p_sscygsjl",["dragon"]="p_sscykljl",["mouse"]="p_sscyzsjl",
            ["neat"]="p_sscycnjl",["roomhorse"]="p_sscyymjl",["terribletiger"]="p_sscyyhjl"
        };
        string spirit=spirits.GetValueOrDefault(PetData.Family(pet.Fields[0]),"p_sscysx");
        Require(pet.Equipment.Count==5 && pet.Equipment.Select(raw=>raw.Split('|')[0]).ToHashSet()
            .SetEquals(["p_sscztk","p_ssczst","p_ssczkj","p_ssczzx",spirit]),
            "Every final species wears the complete epic suit and its specific spirit, with generic fallback only when no dedicated spirit exists.");
        foreach(string raw in pet.Equipment)
        {
            var data=EquipmentData.Parse(raw,true);
            Require(data[11]=="360","Every gear slot has maximum strengthening progress.");
            foreach(var field in EquipmentData.Pet)
            {
                decimal reference=EquipmentData.Read(GameJson.Text(local.PetEquipmentReference[data[0]]?[field.Key]));
                Require(EquipmentData.Read(data[field.Index])==reference+(field.Margin?10:0),
                    $"{data[0]} {field.Key}: raw attribute reaches its own reference cap, independently of strengthening.");
            }
        }
    }

    private void VerifyPetRelease(string folder)
    {
        var local=game!;
        foreach(string prefix in new[]{"p_cy","p_jlcy","p_sscy"})
        foreach(string spirit in new[]{"kljl","zsjl","cnjl","yhjl"})
        {
            var row=local.PetEquipmentReference[prefix+spirit];
            Require(row is JsonObject && EquipmentData.Read(GameJson.Text(row["ehp"]))==(spirit=="kljl"?500:1000) &&
                EquipmentData.Pet.Where(f=>f.Key!="ehp").All(f=>EquipmentData.Read(GameJson.Text(row[f.Key]))==0),
                "Missing spirit references use explicit constructor HP values, with no fabricated foreign attributes.");
        }
        var max=PetData.Create(local,"neat1",true);
        max.Equipment[0]+="|owned-extra";
        string worn=max.Equipment[0];
        var full=Enumerable.Repeat("existing-bag-record",15).ToList();
        max.MaximizeToFinal(local,full);
        Require(max.Equipment[0]==worn && full.Count==15,"Matching maximum gear preserves extra fields and does not require free bag slots.");
        var baby=PetData.New("mouse1");
        baby.Equipment.Add("p_cyst|1|0|2|0|0|0|0|0|0|0|0|0|owned");
        string before=baby.Encode();
        try {baby.MaximizeToFinal(local,full);throw new Exception("Full bag unexpectedly accepted displaced gear.");}
        catch(InvalidDataException) {Require(baby.Encode()==before && full.Count==15,"Capacity failure cancels the entire maximization.");}

        string a=PetData.New("mouse1").Encode();
        max.Fields[18]="1";string target=max.Encode();
        var holder=new JsonObject{[Inventory.Pets]=string.Join('}',a,target,a),[Inventory.PetGear]="old",["extra"]=42};
        var expected=(JsonObject)holder.DeepClone();
        expected[Inventory.Pets]=string.Join('}',a,a);
        expected[Inventory.PetGear]=string.Join('}',new[]{"old"}.Concat(max.Equipment));
        Require(PetData.Release(holder,1)==5 && JsonNode.DeepEquals(holder,expected),
            "Release removes only the chosen index, preserves duplicate species and every other field, and returns all worn records unchanged.");
        var blocked=new JsonObject{[Inventory.Pets]=target,[Inventory.PetGear]=string.Join('}',full)};
        string blockedBefore=blocked.ToJsonString();
        try {PetData.Release(blocked,0);throw new Exception("Full bag unexpectedly accepted release.");}
        catch(InvalidDataException) {Require(blocked.ToJsonString()==blockedBefore,"Release capacity failure is atomic.");}
        PetData.Release(new JsonObject{[Inventory.Pets]=a,[Inventory.PetGear]=string.Join('}',full)},0);

        var snapshot=(JsonObject)save!.Draft.DeepClone();
        string disk=File.ReadAllText(save.Path),key=player!.Key;
        int selected=((PetRow)PetTable.SelectedItem).Index;
        try
        {
            var current=Character!;
            current[Inventory.Pets]=string.Join('}',a,target,a);
            current[Inventory.PetGear]="";
            ShowPets();PetTable.SelectedItem=PetTable.Items.Cast<PetRow>().Single(p=>p.Index==1);
            PetTabs.SelectedItem=PetEquipmentTab;UpdateLayout();
            Require(ReleasePetButton.IsVisible && ReleasePetButton.IsEnabled &&
                ReleasePetButton.TranslatePoint(new Point(),PetLeftCard).Y>=PetMetricsScroll.TranslatePoint(new Point(),PetLeftCard).Y+PetMetricsScroll.ActualHeight &&
                ReleasePetButton.TranslatePoint(new Point(),PetLeftCard).Y<PetActions.TranslatePoint(new Point(),PetLeftCard).Y,
                "Release control stays at the right below stats, separate from fight/rest actions.");
            Capture(folder,"pet-release-location.png");
            var pending=save.Draft.DeepClone();
            ProbeSettingsDialog<PromptDialog>(ReleasePetButton,dialog=>
            {
                Require(dialog.MessageBody.Text.Contains("丑牛大元帅") && dialog.MessageBody.Text.Contains("5 件"),"Release confirmation identifies the specific pet and retained gear.");
                Capture(folder,"pet-release-confirm.png",dialog);ClickDialogButton(dialog,"取消");
            });
            Require(JsonNode.DeepEquals(save.Draft,pending),"Cancel release preserves the complete save.");
            ProbeSettingsDialog<PromptDialog>(ReleasePetButton,dialog=>ClickDialogButton(dialog,"确认放生"));
            Require(Inventory.Records(current,Inventory.Pets).SequenceEqual([a,a]) &&
                Inventory.Records(current,Inventory.PetGear).SequenceEqual(max.Equipment) &&
                PetCount.Text=="2 只宠物" && PetTable.SelectedIndex==1 && Dirty &&
                File.ReadAllText(save.Path)==disk,"Real release refreshes the selection/count, preserves gear, marks pending changes and never writes disk automatically.");
            for(int index=0;index<2;index++)
                ProbeSettingsDialog<PromptDialog>(ReleasePetButton,dialog=>ClickDialogButton(dialog,"确认放生"));
            Require(PetTable.Items.Count==0 && PetTable.SelectedItem is null && !ReleasePetButton.IsEnabled &&
                !PetActions.IsEnabled && !PetTabs.IsEnabled,"Releasing the final pet clears and disables pet-specific controls.");
        }
        finally
        {
            save.Draft=snapshot;ShowSlot(key);
            PetTable.SelectedItem=PetTable.Items.Cast<PetRow>().Single(p=>p.Index==selected);
        }
    }
}
#endif
