using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GmStudio;

public partial class MainWindow
{

    private void InitAdvanced()
    {
        GardenPage.IsEnabled = false;
        SkillEditor.IsEnabled = false;
        BuffPage.IsEnabled = false;
    }

    private void EquipmentDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TuneButton.IsEnabled && e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(BagTable, source) is DataGridRow)
            EditEquipment(sender, e);
    }

    private void EditEquipment(object sender, RoutedEventArgs e) => Guard(() => UpdateEquipment((raw, pet, name) =>
        EquipmentDialog.Edit(this, game!, raw, pet, name)));

    private void MaxEquipment(object sender, RoutedEventArgs e) => Guard(() => UpdateEquipment((raw, pet, name) =>
    {
        var data = EquipmentData.Parse(raw, pet);
        EquipmentData.Maximize(game!, data, pet);
        return string.Join('|', data);
    }));

    private void UpdateEquipment(Func<string, bool, string, string?> edit)
    {
        if (game is null || Character is not JsonObject character || BagTable.SelectedItem is not InventoryRow row)
            return;
        var records = Inventory.Records(character, row.Field);
        bool pet = EquipmentData.IsPet(row.Field);
        if (!pet && !EquipmentData.Wearable(EquipmentData.Parse(records[row.Index], false)[3]))
            throw new InvalidDataException("选择一件角色装备、时装、法宝或宠物装备后再调整属性。");
        if (edit(records[row.Index], pet, row.Name) is not string replacement)
            return;
        records[row.Index] = replacement;
        character[row.Field] = string.Join('}', records);
        ShowBag();
        Report($"{row.Name}属性已更新，等待保存。");
    }

    private void MaxWorn(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (game is null || Character is not JsonObject character)
            return;
        var records = Inventory.Records(character, Inventory.Worn);
        if (records.Count == 0 || !Confirm("穿戴全极品", $"按参考上限调整当前穿戴的 {records.Count} 条装备记录？", "设为极品"))
            return;
        for (int i = 0; i < records.Count; i++)
        {
            var data = EquipmentData.Parse(records[i], false);
            if (!EquipmentData.Wearable(data[3]))
                continue;
            EquipmentData.Maximize(game, data, false);
            records[i] = string.Join('|', data);
        }
        character[Inventory.Worn] = string.Join('}', records);
        ShowBag();
        Report("穿戴装备已按极品方案调整，等待保存。");
    });

    private void MoveEquipment(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (game is null || Character is not JsonObject character || BagTable.SelectedItem is not InventoryRow row)
            return;
        if (EquipmentData.IsPet(row.Field))
            throw new InvalidDataException("宠物装备应分配给宠物，不能穿在角色身上。");
        var source = Inventory.Records(character, row.Field);
        string raw = source[row.Index];
        var fields = EquipmentData.Parse(raw, false);
        if (!EquipmentData.Wearable(fields[3]))
            throw new InvalidDataException("该物品不能穿戴。");
        string targetField = row.Field == Inventory.Worn
            ? EquipmentData.Fashion(fields[3]) ? Inventory.Fashion : Inventory.Gear : Inventory.Worn;
        var target = Inventory.Records(character, targetField);
        if (targetField == Inventory.Worn)
        {
            string user = game.Catalog.FirstOrDefault(item => item.Code == row.Code)?.User ?? "";
            string role = GameJson.Role(GameJson.Number(character["roleid"]));
            if (user.Length > 0 && !role.Contains(user, StringComparison.Ordinal) && !user.Contains(role, StringComparison.Ordinal))
                throw new InvalidDataException($"{row.Name}限 {user} 使用。");
            int other = target.FindIndex(entry => entry.Split('|', 5).ElementAtOrDefault(3) == fields[3]);
            if (other >= 0)
            {
                source[row.Index] = target[other];
                target[other] = raw;
            }
            else
            {
                source.RemoveAt(row.Index);
                target.Add(raw);
            }
        }
        else
        {
            source.RemoveAt(row.Index);
            target.Add(raw);
        }
        character[row.Field] = string.Join('}', source);
        character[targetField] = string.Join('}', target);
        ShowBag();
        Report($"{row.Name}已移到{Inventory.Labels[targetField]}，等待保存。");
    });

}
