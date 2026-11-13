using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GmStudio;

public partial class MainWindow
{
    private void ShowVip()
    {
        if (VipLevelBox is null) return;
        bool previousLoading = loading;
        loading = true;
        try
        {
            VipPage.IsEnabled = vip is not null;
            int total = GameJson.Number(vip?.Draft["allMoney"]);
            int level = VipData.Level(Math.Max(0, total));
            VipLevelBox.ItemsSource = Enumerable.Range(0, 6).Select(i => new ComboBoxItem { Content = $"VIP {i}", IsEnabled = i >= level }).ToArray();
            VipLevelBox.SelectedIndex = level;
            VipLevelBox.IsEnabled = vip is not null && level < 5;
            VipLevelBox.ToolTip = level == 5 ? "已达到最高 VIP 等级，等级已锁定。" : "会员等级仅可向上升级。";
            VipTitle.Text = $"VIP {level}";
            VipBalance.Text = GameJson.Number(vip?.Draft["lastMoney"]).ToString("N0", CultureInfo.InvariantCulture);
            VipAll.Text = total.ToString("N0", CultureInfo.InvariantCulture);
            VipPaid.Text = GameJson.Number(vip?.Draft["paiedMoney"]).ToString("N0", CultureInfo.InvariantCulture);
            VipProgress.Text = level == 5 ? "最高会员等级 · 等级已锁定" : $"仅可升级 · 再累计 {Math.Max(0, VipData.Thresholds[level + 1] - total):N0} 点券可达 VIP {level + 1}";
            if (save is null || vip is null || game is null)
            {
                VipGiftList.ItemsSource = null;
                VipGiftSummary.Text = "选择已有存档后可查看礼包。";
                VipClaimButton.IsEnabled = false;
                return;
            }
            var cards = VipData.Gifts.Select(gift =>
            {
                bool claimed = VipData.Claimed(save.Draft, vip.Draft, gift);
                bool unlocked = level >= gift.Level;
                string state = !unlocked ? "未解锁" : claimed ? "已领取" : "可领取";
                var color = (Brush)FindResource(unlocked && !claimed ? "Green" : "Muted");
                ImageSource? icon = gift.Code is { } code ? game.CatalogByCode.GetValueOrDefault(code)?.Icon : (ImageSource)FindResource("VipExpIcon");
                icon ??= (ImageSource)FindResource("VipGiftIcon");
                return new VipGiftCard(gift.Name, gift.Detail, $"VIP {gift.Level} · {(gift.DailyFlag >= 0 ? "每日" : "一次性")}", state, color, icon, gift.Name[..1]);
            }).ToArray();
            VipGiftList.ItemsSource = cards;
            int available = VipData.Available(save.Draft, vip.Draft).Length;
            bool hasRole = GameFolder.Players(save.Draft).Count > 0;
            VipClaimButton.IsEnabled = available > 0 && hasRole;
            VipGiftSummary.Text = !hasRole ? "当前存档尚未创建角色，请先在游戏中创建角色。" :
                level == 0 ? "VIP 1 起可领取礼包，选择等级后查看对应奖励。" :
                available > 0 ? $"{available} 份可领取 · 自动跳过已领取和未解锁的礼包" : "当前等级的礼包已领取 · 已领取记录会保留";
        }
        finally { loading = previousLoading; }
    }
    private void VipLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || vip is null || VipLevelBox.SelectedIndex < 0) return;
        if (VipLevelBox.SelectedIndex < VipData.Level(Math.Max(0,GameJson.Number(vip.Draft["allMoney"]))))
        {
            ShowVip();Report("VIP 等级只能升级，不能降级。",error:true);return;
        }
        Guard(() =>
        {
            VipData.SetLevel(vip.Draft, VipLevelBox.SelectedIndex);
            ShowVip();
            Report($"已设为 {VipTitle.Text}，等待保存。");
        });
    }
    private void RechargeVip(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (vip is null) return;
        var dialog = new VipRechargeWindow(this, vip.Draft);
        if (dialog.ShowDialog() != true) return;
        VipData.Recharge(vip.Draft, dialog.Amount, dialog.AddToTotal);
        ShowVip();
        Report($"已充值 {dialog.Amount:N0} 点券，等待保存。");
    });
    private void ClaimVipGifts(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (save is null || vip is null || game is null) return;
        ApplyForms();
        int count = VipData.Claim(save.Draft, vip.Draft, game);
        ShowSlot(player?.Key);
        ShowVip();
        Report(count > 0 ? $"已领取 {count} 份 VIP 礼包，等待保存。" : "当前没有可领取的 VIP 礼包。");
    });
    private void VipMore(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var menu = new ContextMenu { PlacementTarget = button };
        var rights = new MenuItem { Header = "权益与使用次数…" };
        rights.Click += VipRights;
        menu.Items.Add(rights);
        var reset = new MenuItem { Header = "重置等级礼包领取记录…", IsEnabled = save is not null };
        reset.Click += ResetVipGifts;
        menu.Items.Add(reset);
        menu.IsOpen = true;
    }
}
