#if QA
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GmStudio;

public partial class MainWindow
{
    private async Task VerifyMembership(string folder)
    {
        var originalVip = (JsonObject)vip!.Draft.DeepClone();
        var originalSave = (JsonObject)save!.Draft.DeepClone();
        string diskVip = File.ReadAllText(vip.Path), diskSave = File.ReadAllText(save.Path);
        var money = new JsonObject { ["allMoney"] = 75000, ["lastMoney"] = 100, ["paiedMoney"] = 123, ["custom"] = "retain" };
        foreach (var (total, level) in new[] { (0,0), (999,0), (1000,1), (4999,1), (5000,2), (9999,2), (10000,3), (19999,3), (20000,4), (49999,4), (50000,5), (int.MaxValue,5) })
            Require(VipData.Level(total) == level, "VIP level boundaries match the installed game.");
        for(int current=1;current<=5;current++)
        {
            var test=new JsonObject {["allMoney"]=VipData.Thresholds[current],["lastMoney"]=42,["paiedMoney"]=0};
            for(int lower=0;lower<current;lower++)
            {
                var before=test.DeepClone();bool rejected=false;
                try{VipData.SetLevel(test,lower);}catch(InvalidOperationException){rejected=true;}
                Require(rejected && JsonNode.DeepEquals(test,before),"Every VIP tier rejects all lower tiers without mutating balances.");
            }
        }
        VipData.SetLevel(money, 5);
        Require(GameJson.Number(money["allMoney"]) == 75000, "Selecting the same VIP level preserves excess cumulative recharge.");
        var locked=money.DeepClone();bool downgrade=false;
        try {VipData.SetLevel(money,3);}catch(InvalidOperationException){downgrade=true;}
        Require(downgrade && JsonNode.DeepEquals(money,locked),"VIP levels cannot decrease and rejected downgrades mutate no fields.");
        money["allMoney"]=0;VipData.SetLevel(money, 3);
        Require(GameJson.Number(money["allMoney"]) == 10000 && GameJson.Number(money["lastMoney"]) == 100 && GameJson.Number(money["paiedMoney"]) == 123,
            "Level choice changes cumulative recharge only, without free balance top-ups.");
        VipData.Recharge(money, 5000, true);
        VipData.Recharge(money, 1000, false);
        Require(GameJson.Number(money["lastMoney"]) == 6100 && GameJson.Number(money["allMoney"]) == 15000 && GameJson.Number(money["paiedMoney"]) == 123,
            "Recharge adds balance, optionally adds cumulative recharge, and preserves consumption.");
        var moneyBefore = money.DeepClone();
        bool overflow = false;
        try { VipData.Recharge(money, int.MaxValue, true); } catch (InvalidDataException) { overflow = true; }
        Require(overflow && JsonNode.DeepEquals(money, moneyBefore), "Recharge overflow is rejected before either balance or total changes.");

        JsonObject SampleSlot() => new()
        {
            ["player1_obj"] = new JsonObject { ["roleid"] = 1, ["curLevel"] = 1 },
            ["player2_obj"] = new JsonObject { ["roleid"] = 2, ["curLevel"] = 1 },
            ["getVIPPresent"] = "false_false_false_false_false_keep",
            ["savedBuffString"] = "7|15|250,1|3|100000,custom|x|y",
            ["unknown"] = 99
        };
        JsonObject SampleVip(int total) => new()
        {
            ["allMoney"] = total, ["lastMoney"] = 42, ["paiedMoney"] = 37,
            ["vipsavelist"] = new JsonArray(false,3,false,2,true,4,5,6,987)
        };
        var data = SampleSlot(); var account = SampleVip(1000);
        Require(VipData.Claim(data, account, game!) == 2, "VIP1 claims only the login stone and first level gift.");
        var once = data.DeepClone(); var accountOnce = account.DeepClone();
        Require(VipData.Claim(data, account, game!) == 0 && JsonNode.DeepEquals(data, once) && JsonNode.DeepEquals(account, accountOnce),
            "Repeated claims do not duplicate rewards or alter state.");
        account["allMoney"] = 50000;
        Require(VipData.Claim(data, account, game!) == 5, "VIP5 claims remaining unlocked gifts.");
        Require(GameJson.Text(data["getVIPPresent"]) == "true_true_true_true_true_keep" &&
            GameJson.Text(data["savedBuffString"]) == "7|15|250,custom|x|y,1|3|100000" &&
            GameJson.Number(account["vipsavelist"]![1]) == 3 && GameJson.Number(account["vipsavelist"]![8]) == 987 &&
            GameJson.Number(account["lastMoney"]) == 42 && GameJson.Number(account["paiedMoney"]) == 37,
            "Claim preserves stronger buffs, unrelated data, extra flags, wallet and usage counters.");
        foreach (var role in new[] { "player1_obj", "player2_obj" })
        {
            var inventory = Inventory.List((JsonObject)data[role]!, game!).ToArray();
            Require(inventory.Count(r => r.Code == "wpqhs1") == 1 && inventory.Count(r => r.Code == "nianqld") == 1 &&
                inventory.Count(r => r.Code == "nianjhd") == 1 && inventory.Count(r => r.Code == "xhhhl") == 1 &&
                inventory.Count(r => r.Code == "_zmxyptxnkl2014") == 1 && inventory.Count(r => r.Code == "p_cynsjl" && r.Field == Inventory.PetGear) == 1,
                "Both existing roles receive each item in its correct bag.");
            var spirit = Inventory.Records((JsonObject)data[role]!, Inventory.PetGear).Single().Split('|');
            var treasure = Inventory.Records((JsonObject)data[role]!, Inventory.Gear).Single(r => r.Split('|')[2] == "xhhhl").Split('|');
            Require(spirit[12] == "0.03" && treasure[20] == "1" && treasure[13] == "6" && treasure[14] == "3" &&
                treasure.Skip(15).Take(5).Count(v => v == "true") == 1, "Gift gear uses original initial values rather than maxed references.");
        }
        var blocked = SampleSlot(); var blockedVip = SampleVip(50000);
        ((JsonObject)blocked["player2_obj"]!)[Inventory.Gear] = string.Join('}', Enumerable.Repeat("occupied",50));
        var blockedBefore = blocked.DeepClone(); var blockedVipBefore = blockedVip.DeepClone();
        bool full = false;
        try { VipData.Claim(blocked, blockedVip, game!); } catch (InvalidDataException) { full = true; }
        Require(full && JsonNode.DeepEquals(blocked, blockedBefore) && JsonNode.DeepEquals(blockedVip, blockedVipBefore),
            "A full second-role bag rolls back rewards and flags across both files.");
        var empty = new JsonObject(); var emptyAccount = SampleVip(50000);
        bool noRole = false;
        try { VipData.Claim(empty, emptyAccount, game!); } catch (InvalidDataException) { noRole = true; }
        Require(noRole && empty.Count == 0 && !VipData.Claimed(empty, emptyAccount, VipData.Gifts[0]), "Missing roles never consume gifts.");

        try
        {
            vip.Draft=SampleVip(0);vip.Draft["lastMoney"]=originalVip["lastMoney"]?.DeepClone();save.Draft=SampleSlot();ShowSlot();ShowVip();
            VipLevelBox.SelectedIndex = 0;
            Require(!VipClaimButton.IsEnabled && VipGiftSummary.Text.Contains("VIP 1") &&
                VipGiftList.Items.Cast<VipGiftCard>().All(c => c.State == "未解锁"), "VIP0 clearly shows locked gifts without a misleading claimed state.");
            VipLevelBox.SelectedIndex = 2;
            Require(GameJson.Number(vip.Draft["allMoney"]) == 5000 && VipTitle.Text == "VIP 2" && VipGiftList.Items.Count == 7,
                "The visible VIP selector applies directly and refreshes all seven gift cards.");
            Require(VipGiftList.Items.Cast<VipGiftCard>().All(c => c.Icon is not null) &&
                !VisualDescendants(VipPage).OfType<TextBox>().Any(), "Every gift has an icon and the main page has no raw money input fields.");
            await VerifyCombo(VipLevelBox, "VIP level");
            ProbeSettingsDialog<VipRechargeWindow>(VipRechargeButton, dialog =>
            {
                dialog.AmountBox.Text = "0";
                Require(!dialog.ConfirmButton.IsEnabled && dialog.ErrorText.IsVisible, "Recharge rejects zero with inline feedback.");
                dialog.AmountBox.Text = "2147483647";
                Require(!dialog.ConfirmButton.IsEnabled, "Recharge blocks overflow in its preview.");
                dialog.AmountBox.Text = "10000";
                Require(dialog.ConfirmButton.IsEnabled && dialog.LevelPreview.Text == "VIP 3", "Preview includes the resulting VIP level.");
                Capture(folder, "vip-recharge.png", dialog);
                ClickDialogButton(dialog, "取消");
            });
            Require(GameJson.Number(vip.Draft["allMoney"]) == 5000 && GameJson.Number(vip.Draft["lastMoney"]) == GameJson.Number(originalVip["lastMoney"]),
                "Canceling recharge leaves the account unchanged.");
            int balance = GameJson.Number(vip.Draft["lastMoney"]);
            ProbeSettingsDialog<VipRechargeWindow>(VipRechargeButton, dialog =>
            {
                dialog.AmountBox.Text = "1000";
                dialog.CountTotal.IsChecked = false;
                ClickDialogButton(dialog, "确认充值");
            });
            Require(GameJson.Number(vip.Draft["lastMoney"]) == balance+1000 && GameJson.Number(vip.Draft["allMoney"]) == 5000,
                "Recharge applies exactly once through the window.");
            VipLevelBox.SelectedIndex=5;
            Require(!VipLevelBox.IsEnabled && VipLevelBox.Items.Cast<ComboBoxItem>().Take(5).All(item=>!item.IsEnabled),
                "VIP5 locks its selector and all lower levels are unavailable.");
            var highest=vip.Draft.DeepClone();VipLevelBox.SelectedIndex=1;
            Require(VipLevelBox.SelectedIndex==5 && JsonNode.DeepEquals(vip.Draft,highest),"Programmatic selection cannot bypass VIP downgrade protection.");
            vip.Draft = SampleVip(50000); save.Draft = SampleSlot(); ShowSlot(); ShowVip();
            VipClaimButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(!VipClaimButton.IsEnabled && VipGiftList.Items.Cast<VipGiftCard>().All(c => c.State == "已领取") &&
                save.Dirty && vip.Dirty && File.ReadAllText(vip.Path) == diskVip && File.ReadAllText(save.Path) == diskSave,
                "One-click claim updates both pending files and all cards without touching disk.");
            await CaptureWhenReady(folder, "vip-claimed.png");
            save.Draft = SampleSlot(); ShowSlot(); ShowVip();
            await CaptureWhenReady(folder, "vip.png");
        }
        finally
        {
            save.Draft = originalSave; vip.Draft = originalVip;
            ShowSlot(); ShowVip();
        }
    }
}
#endif
