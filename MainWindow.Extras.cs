using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GmStudio;

public partial class MainWindow
{
    private static readonly string[] VipBenefits =
    [
        "强化石礼包（VIP 1）", "已用免费传送次数", "双倍经验礼包（VIP 2）", "已用 Boss 刷新次数（VIP 4）",
        "神秘商店加次数礼包（VIP 2）", "法宝洗练次数（VIP 4）", "宠物属性重置次数（VIP 3）", "宠物技能重置次数（VIP 3）"
    ];

    private void VipRights(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (vip is null)
            return;
        var dialog = new EditorDialog(this, "VIP 权益与次数", "礼包项表示今天是否已领取。次数项填写已经使用的次数。", 530);
        var before = vip.Draft["vipsavelist"] as JsonArray ?? new();
        var values = new Control[8];
        for (int i = 0; i < values.Length; i++)
        {
            JsonNode? value = i < before.Count ? before[i] : null;
            values[i] = i is 0 or 2 or 4
                ? dialog.Choices(VipBenefits[i], ["未领取", "已领取"], GameJson.Text(value) is "true" or "True" or "1" ? 1 : 0)
                : dialog.Text(VipBenefits[i], GameJson.Number(value).ToString());
        }
        dialog.Action("恢复初始状态", () =>
        {
            foreach (Control control in values)
                if (control is ComboBox choice) choice.SelectedIndex = 0;
                else ((TextBox)control).Text = "0";
        });
        if (dialog.Show(() =>
        {
            var updated = (JsonArray)before.DeepClone();
            while (updated.Count < values.Length)
                updated.Add(null);
            for (int i = 0; i < values.Length; i++)
                updated[i] = values[i] is ComboBox choice
                    ? JsonValue.Create(choice.SelectedIndex == 1)
                    : JsonValue.Create(Input((TextBox)values[i], VipBenefits[i], 0));
            vip.Draft["vipsavelist"] = updated;
        }))
        {
            ShowVip();
            Report("VIP 权益与次数已应用到存档。");
        }
    });

    private void ResetVipGifts(object sender, RoutedEventArgs e)
    {
        if (save is null || !Confirm("重置 VIP 礼包", "重置当前存档的五个 VIP 礼包领取记录？", "重置领取记录", caution: true))
            return;
        save.Draft["getVIPPresent"] = "false_false_false_false_false";
        ShowVip();
        Report("VIP 礼包领取记录已重置，等待保存。");
    }

    private void AddPet(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (game is null || Character is not JsonObject current)
            return;
        var dialog = new PetCreateWindow(this, game);
        if (dialog.ShowDialog() != true || dialog.CreatedPet is not PetData pet)
            return;
        var records = Inventory.Records(current, Inventory.Pets);
        records.Add(pet.Encode());
        current[Inventory.Pets] = string.Join('}', records);
        PetUpdated("宠物已加入存档。");
        PetTable.SelectedItem = PetTable.Items.Cast<PetRow>().LastOrDefault();
    });

    private void PetDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(PetTable, source) is ListBoxItem item)
        {
            PetTable.SelectedItem = item.Content;
            e.Handled = true;
            EditPet(sender, e);
        }
    }

    private void EditPet(object sender, RoutedEventArgs e) => OpenPetEditor(PetEditSection.Growth);
    private void MaximizePet(object sender, RoutedEventArgs e) => Guard(() => UpdatePet((pet, selected) =>
    {
        string final = PetData.FinalForm(game!, pet.Fields[0]);
        string finalName = GameJson.Text(game!.PetNames[final]);
        if (!Confirm("宠物一键极品", $"将「{selected.Name}」进化为最终形态「{finalName}」，提升至 90 级，填满资质与成长上限，潜力极限设为 100%。\n\n先清空原技能，再按进化顺序学会各形态专属技能；有奥义的最后学会奥义。\n配齐史诗叱咤套装，优先匹配专属精灵；全部装备按本包上限拉满属性与强化。替换下来的旧装备放回宠物背包，空间不足则整次取消。\n保留出战状态、合法资质经验与已有更多潜力点。\n应用后点击主窗口「保存」生效。", "设为极品")) return false;
        string before = pet.Encode();
        var backpack = Inventory.Records(Character!, Inventory.PetGear);
        pet.MaximizeToFinal(game!, backpack);
        if (pet.Encode() == before) return false;
        Character![Inventory.PetGear] = string.Join('}', backpack);
        return pet.Encode() != before;
    }));

    private void EditPetSkills(object sender, RoutedEventArgs e) => OpenPetEditor(PetEditSection.Skills);

    private void OpenPetEditor(PetEditSection section) => Guard(() => UpdatePet((pet, selected) =>
    {
        if (game is null || Character is not JsonObject current)
            return false;
        string originalPet = pet.Encode();
        int bonus = Math.Max(GameJson.Number(save?.Draft["levelUpLimit"]), save is null ? 0 : RequiredCap(save.Draft));
        var dialog = new PetEditorWindow(this, game, pet, selected, bonus, section);
        if (dialog.ShowDialog() != true)
            return false;
        return pet.Encode() != originalPet;
    }));

    private void UpdatePet(Func<PetData, PetRow, bool> edit)
    {
        if (Character is not JsonObject current || game is null || PetTable.SelectedItem is not PetRow selected)
            return;
        var records = Inventory.Records(current, Inventory.Pets);
        var pet = new PetData(records[selected.Index]);
        if (!edit(pet, selected))
            return;
        records[selected.Index] = pet.Encode();
        current[Inventory.Pets] = string.Join('}', records);
        PetUpdated($"{selected.Name}已更新，等待保存。");
    }

    private void PetUpdated(string status)
    {
        if (save is not null)
            save.Draft["levelUpLimit"] = Math.Max(GameJson.Number(save.Draft["levelUpLimit"]), RequiredCap(save.Draft));
        ShowCharacter();
        Report(status);
    }

}
