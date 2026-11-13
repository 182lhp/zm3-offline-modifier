using System.Text.Json.Nodes;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

public partial class MainWindow
{
    private void ShowPetDetails()
    {
        var row = PetTable.SelectedItem as PetRow;
        PetTitle.Text = row?.Name ?? "选择宠物";
        PetSummary.Text = row is null ? "" : $"Lv. {row.Level} · {row.Status}";
        PetCount.Text = $"{PetTable.Items.Count} 只宠物";
        PetPortraitImage.Source = row?.Icon;
        PetPortraitInitial.Text = row?.Initial ?? "宠";
        PetPortraitInitial.Visibility = row?.Icon is null ? Visibility.Visible : Visibility.Collapsed;
        PetSource.Text = row is null ? "" : AcquisitionGuide.ForPet(row.Code, row.Name);
        PetSourceCard.Visibility = row is not null ? Visibility.Visible : Visibility.Collapsed;
        PetActions.IsEnabled = PetTabs.IsEnabled = false;
        ReleasePetButton.IsEnabled = false;
        PetMetrics.ItemsSource = PetAbilities.ItemsSource = PetQualityMeters.ItemsSource = PetGrowthRows.ItemsSource = null;
        PetSkillList.ItemsSource = PetGearList.ItemsSource = PetBackpackList.ItemsSource = null;
        PetSkillHint.Text = "还没有学习技能";
        PetGearHint.Text = "—";
        PetSkillHint.Visibility = Visibility.Visible;
        PetHealthText.Text = PetManaText.Text = PetExperienceText.Text = PetPotentialText.Text = PetPotentialPoints.Text = PetLifetime.Text = "—";
        PetHealthBar.Value = PetManaBar.Value = PetExperienceBar.Value = PetPotentialBar.Value = 0;
        PetBackpackCount.Text = "";
        PetBackpackHint.Visibility = Visibility.Visible;
        PetPotentialWarning.Visibility = Visibility.Collapsed;
        PetMetricsScroll.ScrollToTop();
        if (game is null || Character is not JsonObject character || row is null) return;
        var records = Inventory.Records(character, Inventory.Pets);
        if (row.Index >= records.Count) return;
        PetData pet;
        try { pet = new PetData(records[row.Index]); }
        catch (System.IO.InvalidDataException) { PetSummary.Text += " · 记录字段不足"; return; }
        var profile = new PetProfile(game, pet);
        PetActions.IsEnabled = PetTabs.IsEnabled = true;
        ReleasePetButton.IsEnabled = true;
        FightButton.IsEnabled = row.Status != "出战";
        RestButton.IsEnabled = row.Status == "出战";
        PetMetrics.ItemsSource = profile.Metrics;
        PetAbilities.ItemsSource = PetGrowthRows.ItemsSource = profile.Growth;
        PetQualityMeters.ItemsSource = profile.Qualities;
        PetHealthText.Text = profile.HealthText; PetManaText.Text = profile.ManaText;
        PetHealthBar.Value = profile.HealthPercent; PetManaBar.Value = profile.ManaPercent;
        int next = PetRules.NextExperience(Math.Clamp(PetRules.Value(pet.Fields, 5), 1, 90));
        PetExperienceBar.Maximum = next; PetExperienceBar.Value = Math.Max(0, PetRules.Value(pet.Fields, 6));
        PetExperienceText.Text = $"{PetRules.Value(pet.Fields, 6).ToString("N0", CultureInfo.InvariantCulture)} / {next.ToString("N0", CultureInfo.InvariantCulture)}";
        PetPotentialPoints.Text = pet.Fields[24];
        int potential = PetRules.Value(pet.Fields, 31);
        PetPotentialText.Text = $"{Math.Clamp(potential, 0, 100)} / 100%";
        PetPotentialWarning.Text = "旧存档的宠物潜力极限已按 0–100% 修正，保存后生效。";
        PetPotentialWarning.Visibility = save is { PetLimitRepairs: > 0, Dirty: true } ? Visibility.Visible : Visibility.Collapsed;
        PetPotentialBar.Value = Math.Clamp(PetRules.Value(pet.Fields, 31), 0, 100);
        PetLifetime.Text = $"{pet.Fields[13]} / 100";
        var skills = pet.Fields[19].Split('~', StringSplitOptions.RemoveEmptyEntries).Select(code => PetEntry.Skill(game, code)).ToArray();
        PetSkillList.ItemsSource = skills;
        PetSkillHint.Visibility = skills.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        PetGearList.ItemsSource = profile.Worn;
        PetGearHint.Text = profile.Effects;
        var backpack = Inventory.Records(character, Inventory.PetGear).Select((raw, index) =>
            new PetTile(index, "背包装备", PetEntry.Equipment(game, raw), raw)).ToArray();
        PetBackpackList.ItemsSource = backpack;
        PetBackpackCount.Text = $"{backpack.Length} 件";
        PetBackpackHint.Visibility = backpack.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdatePetEquipmentActions();
    }

    private void PetEquipmentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender == PetGearList && PetGearList.SelectedItem is not null) PetBackpackList.SelectedItem = null;
        else if (sender == PetBackpackList && PetBackpackList.SelectedItem is not null) PetGearList.SelectedItem = null;
        UpdatePetEquipmentActions();
    }

    private void UpdatePetEquipmentActions()
    {
        if (PetWearButton is null) return;
        PetWearButton.IsEnabled = PetBackpackList.SelectedItem is PetTile { Index: >= 0 } && Character is not null;
        PetReturnButton.IsEnabled = PetTuneButton.IsEnabled = PetGearList.SelectedItem is PetTile { Index: >= 0 } && Character is not null;
        PetDeleteGearButton.IsEnabled = Character is not null &&
            (PetBackpackList.SelectedItem is PetTile { Index: >= 0 } || PetGearList.SelectedItem is PetTile { Index: >= 0 });
    }

    private void WearPetEquipment(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (PetBackpackList.SelectedItem is not PetTile tile) return;
        UpdatePet((pet, _) =>
        {
            var backpack = Inventory.Records(Character!, Inventory.PetGear);
            if (tile.Index >= backpack.Count || backpack[tile.Index] != tile.Raw) return false;
            pet.Wear(game!, backpack, tile.Index);
            Character![Inventory.PetGear] = string.Join('}', backpack);
            return true;
        });
    });

    private void ReturnPetEquipment(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (PetGearList.SelectedItem is not PetTile { Index: >= 0 } tile) return;
        UpdatePet((pet, _) =>
        {
            if (tile.Index >= pet.Equipment.Count || pet.Equipment[tile.Index] != tile.Raw) return false;
            var backpack = Inventory.Records(Character!, Inventory.PetGear);
            pet.Return(backpack, tile.Index);
            Character![Inventory.PetGear] = string.Join('}', backpack);
            return true;
        });
    });

    private void TunePetEquipment(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (PetGearList.SelectedItem is not PetTile { Index: >= 0 } tile) return;
        UpdatePet((pet, _) =>
        {
            if (tile.Index >= pet.Equipment.Count || pet.Equipment[tile.Index] != tile.Raw) return false;
            string? edited = EquipmentDialog.Edit(this, game!, tile.Raw, true, tile.Name);
            if (edited is null || edited == tile.Raw) return false;
            pet.Equipment[tile.Index] = edited;
            return true;
        });
    });

    private void CultivatePet(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (sender is Button { Tag: int basis }) UpdatePet((pet, _) => pet.Cultivate(game!, basis));
    });

    private void DeletePetEquipment(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (Character is not JsonObject current || PetTable.SelectedItem is not PetRow selected) return;
        bool worn = PetGearList.SelectedItem is PetTile { Index: >= 0 };
        if ((worn ? PetGearList.SelectedItem : PetBackpackList.SelectedItem) is not PetTile { Index: >= 0 } tile) return;
        var pets = Inventory.Records(current, Inventory.Pets);
        if (selected.Index < 0 || selected.Index >= pets.Count) return;
        var pet = new PetData(pets[selected.Index]);
        var records = worn ? pet.Equipment : Inventory.Records(current, Inventory.PetGear);
        if (tile.Index >= records.Count || records[tile.Index] != tile.Raw) return;
        string location = worn ? $"「{selected.Name}」已穿戴" : "宠物背包";
        if (!Confirm("删除宠物装备", $"确定删除{location}中的「{tile.Name}」吗？\n\n只删除当前选中的这一件，直接移除，不放回背包。\n应用后点击主窗口「保存」生效。", "确认删除", caution: true)) return;
        records.RemoveAt(tile.Index);
        if (worn)
        {
            pets[selected.Index] = pet.Encode();
            current[Inventory.Pets] = string.Join('}', pets);
        }
        else current[Inventory.PetGear] = string.Join('}', records);
        ShowCharacter();
        Report($"已删除{tile.Name}，等待保存。");
    });

    private void ReleasePet(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (Character is not JsonObject current || PetTable.SelectedItem is not PetRow selected) return;
        var records = Inventory.Records(current, Inventory.Pets);
        if (selected.Index < 0 || selected.Index >= records.Count) return;
        var pet = new PetData(records[selected.Index]);
        string equipment = pet.Equipment.Count > 0
            ? $"穿戴的 {pet.Equipment.Count} 件装备会放回宠物背包；空间不足时取消放生。"
            : "这只宠物没有穿戴装备。";
        if (!Confirm("放生宠物", $"确定放生「{selected.Name}」（{selected.Level}级 · {selected.Status}）吗？\n\n{equipment}\n只放生当前选中的这一只，其余宠物保留。\n应用后点击主窗口「保存」生效，保存后无法撤销放生。", "确认放生", caution: true)) return;
        int returned = PetData.Release(current, selected.Index);
        ShowCharacter();
        var next = PetTable.Items.Cast<PetRow>().ToArray();
        PetTable.SelectedItem = next.ElementAtOrDefault(Math.Min(selected.Index, next.Length - 1));
        Report($"已放生{selected.Name}" + (returned > 0 ? $"，{returned} 件装备已放回宠物背包" : "") + "，等待保存。");
    });
}
