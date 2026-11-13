using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;

namespace GmStudio;

internal sealed record RoleMetric(string Label, string Value);
internal sealed record RoleGearSlot(string Label, string Name, BitmapImage? Icon, string Hint);

internal sealed class RoleProfile
{
    private static readonly (string Type, string Label)[] GearSlots =
    [
        ("zbwq", "武器"), ("zbfj", "防具"), ("zbsp", "饰品"),
        ("zbfb", "法宝"), ("zbtx", "头衔"), ("zbsz", "时装"), ("zbcb", "翅膀")
    ];
    private static readonly int[] PillHealth = GameRules.Integers("role","pillHealth");

    internal int Id { get; }
    internal int Level { get; }
    internal int Experience { get; }
    internal int NextExperience { get; }
    internal int Clothing { get; }
    internal int Weapon { get; }
    internal decimal Power { get; }
    internal RoleMetric[] Metrics { get; }
    internal RoleGearSlot[] Gear { get; }

    internal RoleProfile(GameFolder game, JsonObject save, JsonObject character)
    {
        Id = GameJson.Number(character["roleid"]);
        Level = Math.Clamp(GameJson.Number(character["curLevel"], 1), 1, GameRules.Int("role","maxLevel"));
        Experience = Math.Max(0, GameJson.Number(character["curExp"]));
        var experience=GameRules.Rows("role","experience").First(row=>Level<GameJson.Number(row!["below"]));
        NextExperience=GameJson.Number(experience!["base"])+GameJson.Number(experience["step"])*(Level-GameJson.Number(experience["start"]));

        var worn = Inventory.Records(character, Inventory.Worn)
            .Select(raw => raw.Split('|'))
            .Where(fields => fields.Length >= 28 && EquipmentData.Wearable(fields[3])).ToArray();
        var slots = worn.GroupBy(fields => fields[3]).ToDictionary(group => group.Key, group => group.First());
        int SpriteId(string type) => slots.TryGetValue(type, out var fields) &&
            int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id <= 32 ? id : 0;

        bool showFashion = GameJson.Text(character["isshowfashion"]) is "true" or "True" or "1";
        Clothing = showFashion && slots.ContainsKey("zbsz") ? SpriteId("zbsz") : SpriteId("zbfj");
        Weapon = SpriteId("zbwq");
        Gear = GearSlots.Where(slot => slot.Type != "zbcb" || slots.ContainsKey(slot.Type)).Select(slot =>
        {
            if (!slots.TryGetValue(slot.Type, out var fields))
                return new RoleGearSlot(slot.Label, "未穿戴", null, "未穿戴");
            var item = game.CatalogByCode.GetValueOrDefault(fields[2]);
            string name = item?.Name ?? fields[2];
            int strength = EquipmentData.StrengthLevel(fields[23]);
            return new RoleGearSlot(slot.Label, name, item?.Icon, strength > 0 ? $"{name} · 强化 +{strength}" : name);
        }).ToArray();

        // BaseRoleProperies.initByLevel and the single-player backpack use these level curves.
        int steps = Level - 1;
        var curve=GameRules.Node("rules","role","curves")[Id.ToString(CultureInfo.InvariantCulture)] as JsonArray;
        int Stat(int index)=>curve?[index] is JsonArray row?GameJson.Number(row[0])+GameJson.Number(row[1])*steps:0;
        int hp=Stat(0),mp=Stat(1),attack=Stat(2),defense=Stat(3);
        decimal Passive(int index, int factor) => character["ispassiveskill"] is JsonArray passives && passives.Count > index &&
            GameJson.Number(passives[index]) is > 0 and var value ? ((decimal)value + 1) * factor : 0;
        decimal hpPassive = Passive(0, 100), mpPassive = Passive(1, 100);
        decimal critPassive = Passive(2, 1), recoveryPassive = Passive(3, 1), manaPassive = Passive(4, 1);
        decimal pill = character["immortalitylist"] is JsonArray pills
            ? Math.Clamp(pills.Take(PillHealth.Length).Select((row, index) =>
                (decimal)Math.Max(0, GameJson.Number((row as JsonObject)?["usenum"])) * PillHealth[index]).Sum(), 0, GameRules.Int("role","pillHealthMax")) : 0;
        var buffs = BuffData.Rows(save).Where(row => row.Frames > 0).ToArray();
        int Buff(int id) => buffs.LastOrDefault(row => row.Type == id)?.Value ?? 0;
        int bloodthirst = Id == 1 && character["isstudyskill"] is JsonArray schools &&
            schools.OfType<JsonObject>().Any(school => GameJson.Text(school["skillName"])
                .Split('|').Any(skill => skill.StartsWith("sx~", StringComparison.Ordinal) &&
                    int.TryParse(skill.AsSpan(3), out int learned) && learned > 0)) ? 5 : 0;

        decimal GearTotal(int index, string coefficient)
        {
            decimal total = 0;
            foreach (string[] fields in worn)
            {
                decimal baseValue = Number(fields[index]);
                decimal reforge = Number(fields[EquipmentData.Fashion(fields[3]) && fields.Length > 28 ? 28 : 27]);
                decimal rate = game.EquipmentStrength[fields[2]] is JsonObject reference
                    ? Number(GameJson.Text(reference[coefficient])) : 0;
                decimal value = baseValue * (1 + reforge * .1m) + EquipmentData.StrengthLevel(fields[23]) * rate;
                total += index is 11 or 12 or 24 ? decimal.Round(value, 2, MidpointRounding.AwayFromZero) * 100 : value;
            }
            return total;
        }

        decimal gearAttack = GearTotal(9, "att"), gearCrit = GearTotal(12, "crit");
        decimal gearDodge = GearTotal(11, "miss"), gearResistance = GearTotal(24, "mdef");
        decimal gearHeal = GearTotal(13, "ehp"), gearMana = GearTotal(14, "emp");
        Power = decimal.Truncate(Level * 15 + (pill + hpPassive + mpPassive) * .1m +
            (critPassive + recoveryPassive + manaPassive) * 10 + gearAttack + (Id switch
            {
                1 => (gearCrit + bloodthirst) * 25,
                2 => (gearCrit + gearMana) * 30,
                3 => gearHeal * 8 + gearResistance * 8 + gearCrit * 10,
                4 => (gearCrit + gearDodge) * 20,
                _ => 0
            }));
        Metrics =
        [
            new("生命上限", Format(Math.Max(1, hp + hpPassive + pill + GearTotal(7, "hp") + Buff(5)))),
            new("魔法上限", Format(Math.Max(1, mp + mpPassive + GearTotal(8, "mp")))),
            new("攻击", Format(attack + gearAttack + (decimal)Buff(3) + Buff(7) + Buff(8))),
            new("防御", Format(decimal.Floor(defense + GearTotal(10, "def") + Buff(6) + .5m))),
            new("暴击", Format(gearCrit + critPassive + bloodthirst + Buff(2)) + "%"),
            new("闪避", Format(gearDodge) + "%"),
            new("回血", Format(gearHeal + recoveryPassive + Buff(4))),
            new("回魔", Format(gearMana + manaPassive)),
            new("魔抗", Format(gearResistance) + "%"),
            new("存档幸运", Format(GameJson.Number(character["luckdata"])))
        ];
    }

    private static decimal Number(string? value) => Math.Clamp(EquipmentData.Read(value), -int.MaxValue, int.MaxValue);
    internal static string Format(decimal value) => value.ToString("#,0.##", CultureInfo.CurrentCulture);
}
