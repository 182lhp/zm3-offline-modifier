using System.Text.Json.Nodes;
using System.Globalization;
using System.Windows.Media.Imaging;

namespace GmStudio;

internal sealed record PetMeter(string Name, string Text, double Value, double Maximum, string Color);
internal sealed record PetMetric(string Name, string Text);
internal sealed record PetGrowth(int Basis, string Name, string Total, string Progress, double Value, double Maximum, bool CanGrow);
internal sealed record PetTile(int Index, string Slot, PetEntry? Art, string Raw = "")
{
    public string Name => Art?.Name ?? "未穿戴";
    public string Initial => Art?.Initial ?? "＋";
    public BitmapImage? Icon => Art?.Icon;
}

// Saves contain unequipped base stats. Restore permanent passives, equipment and
// matching suit effects for a read-only preview; temporary combat buffs stay in game.
internal sealed class PetProfile
{
    internal decimal Hp { get; private set; }
    internal decimal Mp { get; private set; }
    internal decimal Attack { get; private set; }
    internal decimal Defense { get; private set; }
    internal decimal Speed { get; private set; } = 5;
    internal decimal MagicDefense { get; private set; }
    internal decimal Critical { get; private set; }
    internal decimal Dodge { get; private set; }
    internal decimal RegenHp { get; private set; }
    internal decimal RegenMp { get; private set; }
    internal PetMeter[] Qualities { get; }
    internal PetMetric[] Metrics { get; }
    internal PetGrowth[] Growth { get; }
    internal PetTile[] Worn { get; }
    internal string Effects { get; }
    internal string HealthText { get; }
    internal string ManaText { get; }
    internal double HealthPercent { get; }
    internal double ManaPercent { get; }

    internal PetProfile(GameFolder game, PetData pet)
    {
        var fields = pet.Fields;
        var rules = new PetRules(game, fields[0]);
        decimal Number(int index) => EquipmentData.Read(fields[index]);
        Hp = Number(2); Mp = Number(4); Attack = Number(7); Defense = Number(8);
        var skills = fields[19].Split('~', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        int war = PetRules.Value(fields, 17) + PetRules.Value(fields, 26);
        int bone = PetRules.Value(fields, 16) + PetRules.Value(fields, 27);
        if (skills.Contains("tsml")) Attack += rules.Stage * GameJson.Number(GameRules.Node("rules","pet","permanentPassives")["tsml"]) * (decimal)war;
        if (skills.Contains("zrsh")) Defense += rules.Stage * GameJson.Number(GameRules.Node("rules","pet","permanentPassives")["zrsh"]) * (decimal)bone;
        if (skills.Contains("smzf")) Hp += rules.Stage * GameJson.Number(GameRules.Node("rules","pet","permanentPassives")["smzf"]) * (decimal)war;
        if (skills.Contains("mfby")) Mp += rules.Stage * GameJson.Number(GameRules.Node("rules","pet","permanentPassives")["mfby"]) * (decimal)bone;

        var codes = new HashSet<string>();
        foreach (string raw in pet.Equipment)
        {
            string[] gear = raw.Split('|');
            if (gear.Length < 13) continue;
            string code = gear[0];
            codes.Add(code);
            decimal Gear(int index) => EquipmentData.Read(gear[index]);
            int divisor=GameJson.Number(GameRules.Node("rules","pet","strengthDivisors")[game.CatalogByCode.GetValueOrDefault(code)?.Quality??""]);
            decimal strength = divisor > 0 ? Math.Floor(Math.Max(0, Gear(11)) / divisor) : 0;
            decimal Extra(string key) => strength * EquipmentData.Read(GameJson.Text(game.PetEquipmentStrength[code]?[key]));
            Hp += Gear(1) + Extra("hp"); Mp += Gear(2) + Extra("mp");
            Attack += Gear(3) + Extra("atk"); Defense += Gear(4) + Extra("def");
            Dodge += Gear(5); Critical += Gear(6); RegenHp += Gear(7); RegenMp += Gear(8);
            MagicDefense += Gear(9); Speed += Gear(10);
        }

        var effects = new List<string>();
        void ApplySuit(JsonNode? suit, bool spirit)
        {
            if (suit?["sets"] is not JsonArray sets) return;
            for (int i = 0; i < sets.Count; i++)
            {
                if (sets[i] is not JsonArray set || set.Count == 0) continue;
                var members = set.Select(GameJson.Text).ToArray();
                if (!members.All(codes.Contains)) continue;
                if (!spirit && pet.Equipment.Count(raw => PetData.PetType(game, raw.Split('|', 2)[0]) != "zbjn") != members.Length) continue;
                if (spirit && !(game.Official["jnToPet"] as JsonArray ?? []).OfType<JsonObject>().Any(row =>
                    GameJson.Text(row["petFamily"]) == PetData.Family(fields[0]) &&
                    (row["fills"] as JsonArray ?? []).Select(GameJson.Text).Intersect(members).Any())) continue;
                if (suit["effect"]?[i] is not JsonArray changes) continue;
                foreach (var change in changes)
                {
                    decimal value = EquipmentData.Read(GameJson.Text(change?["value"]));
                    switch (GameJson.Text(change?["type"]))
                    {
                        case "SUIT_ADD_HP": Hp += value; break;
                        case "SUIT_ADD_ATK": Attack += value; break;
                        case "SUIT_ADD_MDEF": MagicDefense += value; break;
                        case "SUIT_ADD_CRIT": Critical += value; break;
                    }
                }
                if (changes.Count > 0) effects.Add(GameJson.Text(suit["names"]?[i]));
            }
        }
        ApplySuit(game.Official["petSuits"]?["petSuit"], false);
        ApplySuit(game.Official["petSuits"]?["jnSuit"], true);
        if (Number(9) > 2000) Hp = 1;
        if (Number(10) > 2000) Mp = 1;
        if (Number(11) > 2000) Attack = 1;
        if (Number(12) > 2000) Defense = 1;
        Hp = Math.Max(1, decimal.Truncate(Hp)); Mp = Math.Max(1, decimal.Truncate(Mp));
        HealthPercent = Number(2) <= 0 ? 0 : (double)Math.Clamp(Number(1) / Number(2), 0, 1);
        ManaPercent = Number(4) <= 0 ? 0 : (double)Math.Clamp(Number(3) / Number(4), 0, 1);
        HealthText = $"{N(decimal.Truncate(Hp * (decimal)HealthPercent))} / {N(Hp)}";
        ManaText = $"{N(decimal.Truncate(Mp * (decimal)ManaPercent))} / {N(Mp)}";
        Metrics = [new("攻击", N(Attack)), new("防御", N(Defense)), new("移速", N(Speed)), new("魔抗", P(MagicDefense)),
            new("暴击", P(Critical)), new("闪避", P(Dodge)), new("回血", N(RegenHp)), new("回蓝", N(RegenMp))];
        string[] qualityNames = ["生命资质", "魔法资质", "攻击资质", "防御资质"];
        string[] colors = ["#B9604F", "#518AAB", "#4E9272", "#AD873B"];
        Qualities = Enumerable.Range(0, 4).Select(i => new PetMeter(qualityNames[i],
            $"{fields[9 + i]} + {fields[20 + i]} / {rules.QualityMaximum(9 + i)}", (double)Number(9 + i),
            Math.Max(1, rules.QualityMaximum(9 + i)), colors[i])).ToArray();
        string[] names = ["悟性", "根骨", "灵力"];
        Growth = PetRules.Abilities.Select((a, i) =>
        {
            int total = PetRules.Value(fields, a.Base) + PetRules.Value(fields, a.Bonus);
            int target = PetRules.ProgressTarget(total);
            bool maximum = total >= rules.TotalMaximum(a.Key);
            return new PetGrowth(a.Base, names[i], $"{total} / {rules.TotalMaximum(a.Key)}",
                maximum ? "已达上限" : target > 0 ? $"{fields[a.Progress]} / {target}" : "先设置基础能力",
                PetRules.Value(fields, a.Progress), Math.Max(1, target), !maximum && target > 0 && PetRules.Value(fields, 24) > 0);
        }).ToArray();
        string[] slots = ["手套", "头盔", "铠甲", "战靴", "精灵"];
        string[] types = ["zbst", "zbmz", "zbyf", "zbxz", "zbjn"];
        Worn = types.Select((type, i) =>
        {
            int index = pet.Equipment.FindIndex(raw => PetData.PetType(game, raw.Split('|', 2)[0]) == type);
            return new PetTile(index, slots[i], index < 0 ? null : PetEntry.Equipment(game, pet.Equipment[index]),
                index < 0 ? "" : pet.Equipment[index]);
        }).ToArray();
        Effects = effects.Count == 0 ? "未激活套装加成" : string.Join(" · ", effects.Distinct()) + "已激活";
    }

    private static string N(decimal value) => value.ToString("#,0.############", CultureInfo.InvariantCulture);
    private static string P(decimal value) => N(value * 100) + "%";
}
