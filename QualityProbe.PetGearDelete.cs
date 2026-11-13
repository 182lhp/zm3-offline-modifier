#if QA
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

public partial class MainWindow
{
    private void VerifyPetEquipmentDeletion(string folder)
    {
        var snapshot=(JsonObject)save!.Draft.DeepClone();
        string key=player!.Key,disk=File.ReadAllText(save.Path);
        int selected=((PetRow)PetTable.SelectedItem).Index;
        try
        {
            var current=Character!;
            var pet=new PetData(Inventory.Records(current,Inventory.Pets)[selected]);
            string raw=pet.Equipment[0];
            current[Inventory.PetGear]=string.Join('}',raw,raw);
            ShowPets();PetTabs.SelectedItem=PetEquipmentTab;UpdateLayout();
            Require(PetDeleteGearButton.IsVisible && !PetDeleteGearButton.IsEnabled,"Delete is visible in the equipment toolbar and disabled without a selected record.");
            PetBackpackList.SelectedItem=PetBackpackList.Items.Cast<PetTile>().Last();
            var before=save.Draft.DeepClone();
            ProbeSettingsDialog<PromptDialog>(PetDeleteGearButton,dialog=>
            {
                Require(dialog.MessageBody.Text.Contains("宠物背包") && dialog.MessageBody.Text.Contains("不放回背包"),"Delete confirmation identifies its source and direct removal.");
                Capture(folder,"pet-delete-confirm.png",dialog);ClickDialogButton(dialog,"取消");
            });
            Require(JsonNode.DeepEquals(save.Draft,before),"Canceling equipment deletion preserves every save field.");
            ProbeSettingsDialog<PromptDialog>(PetDeleteGearButton,dialog=>ClickDialogButton(dialog,"确认删除"));
            var expected=(JsonObject)before.DeepClone();((JsonObject)expected[key]!)[Inventory.PetGear]=raw;
            Require(JsonNode.DeepEquals(save.Draft,expected) && PetBackpackCount.Text=="1 件" && !PetDeleteGearButton.IsEnabled,
                "Deleting one of identical bag records removes exactly the selected record and resets selection.");
            PetGearList.SelectedItem=PetGearList.Items.Cast<PetTile>().First(tile=>tile.Index>=0);
            var worn=(PetTile)PetGearList.SelectedItem;
            before=save.Draft.DeepClone();
            var expectedPets=Inventory.Records((JsonObject)before[key]!,Inventory.Pets);
            var expectedPet=new PetData(expectedPets[selected]);expectedPet.Equipment.RemoveAt(worn.Index);
            expectedPets[selected]=expectedPet.Encode();
            expected=(JsonObject)before.DeepClone();((JsonObject)expected[key]!)[Inventory.Pets]=string.Join('}',expectedPets);
            ProbeSettingsDialog<PromptDialog>(PetDeleteGearButton,dialog=>
            {
                Require(dialog.MessageBody.Text.Contains("已穿戴"),"Worn gear deletion names the worn location.");
                ClickDialogButton(dialog,"确认删除");
            });
            Require(JsonNode.DeepEquals(save.Draft,expected) && PetGearList.Items.Cast<PetTile>().Count(t=>t.Index>=0)==4 &&
                Inventory.Records(current,Inventory.PetGear).SequenceEqual([raw]) && Dirty,
                "Deleting worn gear refreshes the empty slot/profile and leaves pet fields, bag and all other records unchanged.");
            PetBackpackList.SelectedItem=PetBackpackList.Items.Cast<PetTile>().Single();
            ProbeSettingsDialog<PromptDialog>(PetDeleteGearButton,dialog=>ClickDialogButton(dialog,"确认删除"));
            Require(PetBackpackList.Items.Count==0 && PetBackpackHint.IsVisible && !PetDeleteGearButton.IsEnabled &&
                File.ReadAllText(save.Path)==disk,"Deleting the last bag record shows the empty state without writing disk.");
            Capture(folder,"pet-delete-equipment.png");
        }
        finally
        {
            save.Draft=snapshot;ShowSlot(key);
            PetTable.SelectedItem=PetTable.Items.Cast<PetRow>().Single(p=>p.Index==selected);
            PetTabs.SelectedItem=PetEquipmentTab;
        }
    }
}
#endif
