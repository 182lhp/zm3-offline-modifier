using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Media;

namespace GmStudio;

internal sealed record VipGift(string Id, string Name, string Detail, int Level, string? Code, int Flag = -1, int DailyFlag = -1);
internal sealed record VipGiftCard(string Name, string Detail, string Requirement, string State, Brush StateColor, ImageSource? Icon, string Initial);

internal static class VipData
{
    internal static readonly int[] Thresholds=GameRules.Integers("vip","thresholds");
    internal static readonly VipGift[] Gifts=GameRules.Rows("vip","gifts").Select(row=>new VipGift(GameJson.Text(row!["id"]),GameJson.Text(row["name"]),GameJson.Text(row["detail"]),GameJson.Number(row["level"]),row["code"] is null?null:GameJson.Text(row["code"]),GameJson.Number(row["flag"],-1),GameJson.Number(row["dailyFlag"],-1))).ToArray();
    internal static int Level(int total) => Enumerable.Range(0, Thresholds.Length).Last(i => total >= Thresholds[i]);
    internal static int Money(JsonObject vip, string key)
    {
        if (!int.TryParse(GameJson.Text(vip[key]), NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 0)
            throw new InvalidDataException("当前点券数据无效，请先核对 vip.json。");
        return value;
    }
    internal static void SetLevel(JsonObject vip, int level)
    {
        if (level is < 0 or > 5) throw new InvalidDataException("VIP 等级应为 0–5。");
        if (level < Level(Money(vip, "allMoney"))) throw new InvalidOperationException("VIP 等级只能升级，不能降级。");
        if (Level(Money(vip, "allMoney")) != level) vip["allMoney"] = Thresholds[level];
    }
    internal static (int Balance, int Total) RechargePreview(JsonObject vip, int amount, bool countTotal)
    {
        if (amount < 1) throw new InvalidDataException("请输入大于 0 的充值点券数。");
        long balance = (long)Money(vip, "lastMoney") + amount;
        long total = (long)Money(vip, "allMoney") + (countTotal ? amount : 0);
        if (balance > int.MaxValue || total > int.MaxValue) throw new InvalidDataException("充值后数值超出游戏支持范围，请减少点券数。");
        return ((int)balance, (int)total);
    }
    internal static void Recharge(JsonObject vip, int amount, bool countTotal)
    {
        var result = RechargePreview(vip, amount, countTotal);
        vip["lastMoney"] = result.Balance;
        if (countTotal) vip["allMoney"] = result.Total;
    }
    private static bool True(JsonNode? node) => GameJson.Text(node).ToLowerInvariant() is "true" or "1";
    internal static bool Claimed(JsonObject save, JsonObject vip, VipGift gift)
    {
        if (gift.DailyFlag >= 0)
            return vip["vipsavelist"] is JsonArray list && list.Count > gift.DailyFlag && True(list[gift.DailyFlag]);
        return GameJson.Text(save["getVIPPresent"]).Split('_').ElementAtOrDefault(gift.Flag)?.ToLowerInvariant() is "true" or "1";
    }
    internal static VipGift[] Available(JsonObject save, JsonObject vip) => Gifts
        .Where(g => g.Level <= Level(Money(vip, "allMoney")) && !Claimed(save, vip, g)).ToArray();

    internal static int Claim(JsonObject save, JsonObject vip, GameFolder game)
    {
        var gifts = Available(save, vip);
        if (gifts.Length == 0) return 0;
        // Prepare both files privately, then commit together. A missing item or full bag cannot leave claim flags behind.
        var slotCopy = (JsonObject)save.DeepClone();
        var vipCopy = (JsonObject)vip.DeepClone();
        var players = new[] { slotCopy["player1_obj"], slotCopy["player2_obj"] }.OfType<JsonObject>()
            .Where(p => GameJson.Number(p["roleid"]) is >= 1 and <= 4).ToArray();
        if (players.Length == 0) throw new InvalidDataException("当前存档没有已创建的角色，无法领取礼包。");
        foreach (var gift in gifts)
        {
            if (gift.Code is { } code)
            {
                if (!game.CatalogByCode.TryGetValue(code, out var item)) throw new InvalidDataException($"当前游戏包缺少{gift.Name}的数据，未领取任何礼包。");
                foreach (var player in players) Grant(player, item, game);
            }
            else
            {
                var rows = BuffData.Rows(slotCopy);
                int expType=GameJson.Number(GameRules.Node("rules","vip","expGift")["type"]);
                var existing = rows.Where(r => r.Type == expType).ToArray();
                // Preserve a stronger/longer existing experience bonus and all unrelated buff records verbatim.
                string[] records = GameJson.Text(slotCopy["savedBuffString"]).Split(',', StringSplitOptions.RemoveEmptyEntries);
                var retained = records.Where(r => r.Split('|')[0] != expType.ToString(CultureInfo.InvariantCulture)).ToList();
                retained.Add($"{expType}|{Math.Max(GameJson.Number(GameRules.Node("rules","vip","expGift")["value"]), existing.Select(r => r.Value).DefaultIfEmpty().Max())}|{Math.Max(GameJson.Number(GameRules.Node("rules","vip","expGift")["frames"]), existing.Select(r => r.Frames).DefaultIfEmpty().Max())}");
                slotCopy["savedBuffString"] = string.Join(',', retained);
            }
            if (gift.DailyFlag >= 0)
            {
                var list = vipCopy["vipsavelist"] as JsonArray ?? new JsonArray(false, 0, false, 0, false, 0, 0, 0);
                if (vipCopy["vipsavelist"] is not JsonArray) vipCopy["vipsavelist"] = list;
                while (list.Count <= gift.DailyFlag) list.Add(0);
                list[gift.DailyFlag] = true;
            }
            else
            {
                var flags = GameJson.Text(slotCopy["getVIPPresent"]).Split('_').ToList();
                while (flags.Count < 5) flags.Add("false");
                flags[gift.Flag] = "true";
                slotCopy["getVIPPresent"] = string.Join('_', flags);
            }
        }
        foreach (var pair in slotCopy.ToArray()) save[pair.Key] = pair.Value?.DeepClone();
        foreach (var pair in vipCopy.ToArray()) vip[pair.Key] = pair.Value?.DeepClone();
        return gifts.Length;
    }

    private static void Grant(JsonObject player, CatalogItem item, GameFolder game)
    {
        var records = Inventory.Records(player, item.Bag);
        int limit = item.PetEquipment ? GameRules.Int("inventory","petGear") : GameRules.BagCapacity(GameJson.Number(player["curLevel"]));
        int existing = item.Bag == Inventory.Consumables ? records.FindIndex(r => r.Split('|').ElementAtOrDefault(2) == item.Code) : -1;
        if (existing >= 0)
        {
            var fields = records[existing].Split('|');
            if (fields.Length <= 22 || !int.TryParse(fields[22], out int count) || count < 0 || count >= GameRules.Int("inventory","stack"))
                throw new InvalidDataException($"{item.Name}已有数量无效或已满，未领取任何礼包。");
            fields[22] = (count + 1).ToString(CultureInfo.InvariantCulture);
            records[existing] = string.Join('|', fields);
            player[item.Bag] = string.Join('}', records);
            return;
        }
        if (records.Count >= limit) throw new InvalidDataException($"{Inventory.Labels[item.Bag]}已满，请整理后再领取。此次未领取任何礼包。");
        Inventory.Add(player, item, 1, false, game);
        records = Inventory.Records(player, item.Bag);
        var raw = records[^1].Split('|');
        if (item.Code == GameRules.Text("vip","titlePermanent")) raw[20] = "0";
        if (item.Code == GameJson.Text(GameRules.Node("rules","vip","randomMagic")["code"]))
        {
            // Local MyAllEquip definition: level 1, HP/MP recovery 6/3, growth 2.0–2.4.
            var magic=GameRules.Node("rules","vip","randomMagic");
            raw[13]=GameJson.Text(magic["hpRecovery"]);raw[14]=GameJson.Text(magic["mpRecovery"]);
            raw[21] = (Random.Shared.Next(GameJson.Number(magic["growthTenths"]![0]), GameJson.Number(magic["growthTenths"]![1])+1) / 10d).ToString("0.0", CultureInfo.InvariantCulture);
            raw[Random.Shared.Next(15, 20)] = "true";
        }
        if (item.Code == GameJson.Text(GameRules.Node("rules","vip","spiritGift")["code"])) raw[12] = GameJson.Text(GameRules.Node("rules","vip","spiritGift")["crit"]);
        records[^1] = string.Join('|', raw);
        player[item.Bag] = string.Join('}', records);
    }
}
