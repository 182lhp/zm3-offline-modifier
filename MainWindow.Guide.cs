using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

public partial class MainWindow
{
    private sealed record StatPreview(string Name, string Current, string Limit);

    private void BagSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!loading)
            ShowBagDetails();
    }

    private void ShowBagDetails()
    {
        var row = BagTable.SelectedItem as InventoryRow;
        BagSource.Text = AcquisitionGuide.ForItem(row?.Code);
        BagSourceCard.Visibility = row is not null ? Visibility.Visible : Visibility.Collapsed;
        RemoveItemButton.IsEnabled = row is not null && Character is not null;
        WearButton.IsEnabled = TuneButton.IsEnabled = MaxItemButton.IsEnabled = false;
        WearButton.Content = row?.Field == Inventory.Worn ? "放回背包" : "穿戴";
        BagStatPanel.Visibility = Visibility.Collapsed;
        BagStats.ItemsSource = null;
        if (game is null || Character is not JsonObject character || row is null)
        {
            BagSummary.Text = Character is null ? "选择存档和角色后查看背包。" : "选择一项物品查看详情。";
            return;
        }

        BagSummary.Text = $"{row.Category} · 数量 {row.Quantity}";
        var records = Inventory.Records(character, row.Field);
        if (row.Index >= records.Count)
            return;
        string raw = records[row.Index];
        bool pet = EquipmentData.IsPet(row.Field);
        if (!pet && !EquipmentData.Wearable(raw.Split('|', 5).ElementAtOrDefault(3) ?? ""))
            return;

        try
        {
            var data = EquipmentData.Parse(raw, pet);
            int strength = EquipmentData.StrengthLevel(data[pet ? 11 : 23]);
            BagSummary.Text += pet || EquipmentData.Strengthen(data[3]) || strength > 0 ? $" · 强化 +{strength}" : " · 未强化";
            BagStats.ItemsSource = (pet ? EquipmentData.Pet : EquipmentData.Hero).Select(field =>
            {
                decimal? limit = EquipmentData.Limit(game, data, pet, field, strength);
                return new StatPreview(field.Label, data[field.Index].Length > 0 ? data[field.Index] : "—",
                    limit.HasValue ? EquipmentData.Number(limit.Value) : "—");
            }).ToArray();
            BagStatPanel.Visibility = Visibility.Visible;
            WearButton.IsEnabled = !pet;
            TuneButton.IsEnabled = MaxItemButton.IsEnabled = true;
        }
        catch (InvalidDataException)
        {
            BagSummary.Text += " · 记录字段不足";
        }
    }

    private void PetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!loading)
        {
            ShowPetDetails();
            if (PetTable.IsVisible && PetTable.SelectedItem is PetRow selected)
                PetTable.Dispatcher.BeginInvoke(() => PetTable.ScrollIntoView(selected));
        }
    }

    private void OpenPetGuide(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (game is null || game.PetNames.Count == 0)
        {
            ShowError("请选择含有宠物资料的游戏目录。");
            return;
        }
        new PetGuideWindow(this, game, (PetTable.SelectedItem as PetRow)?.Code).ShowDialog();
    });

    private void OpenGuide(object sender, RoutedEventArgs e)
    {
        string? address = ((sender as Button)?.Tag as string) switch
        {
            "equipment" => "https://news.4399.com/gonglue/zmxy3/zhuangbei/",
            "pets" => "https://news.4399.com/gonglue/zmxy3/chongwu/",
            "skills" => "https://news.4399.com/gonglue/zmxy3/jn/",
            _ => null
        };
        if (address is null)
            return;
        try { Process.Start(new ProcessStartInfo(address) { UseShellExecute = true })?.Dispose(); }
        catch (Win32Exception) { ShowError("无法打开默认浏览器。"); }
    }
}
