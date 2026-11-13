using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;

namespace GmStudio;

public sealed record SkillSchool(int Index, string Name, string[] Codes, string[] Names, int[] Costs)
{
    public override string ToString() => Name;
}

public sealed record SkillRow(string School, string Code, string Name, int Cost, int Level)
{
    public BitmapSource? Icon { get; init; }
    public string Initial => Name.Length == 0 ? "技" : Name[..1];
    public string LevelLabel => Level == 0 ? "未学习" : $"{Level} 级";
    public string Caption => Code;
    public SkillHint Hint => new(Name,SkillHelp.Heroes.GetValueOrDefault(Code,"当前游戏目录未收录这项技能的效果说明。"),
        $"{School} · {LevelLabel} · 基础魔法消耗 {Cost}",Code);
}
public sealed record GardenRow(int Index, int Id, int Type, int Level, string Date)
{
    public string Status => Type switch { 1 => "可种植", 2 or 3 or 5 => "可收获", 4 => "待翻新", _ => "未知状态" };
}

public sealed record BuffKind(int Id, string Name, int Default, int? Limit)
{
    public string Display => $"{Name}  ·  #{Id}";
}

public sealed record BuffRow(int Type, string Name, int Value, int Frames)
{
    public string Duration => $"{Frames / (double)GameRules.Int("buff","framesPerSecond"):0.#} 秒";
}


internal static class SkillData
{
    internal static readonly string[] PassiveNames = GameRules.Strings("skill","passiveNames");

    internal static IReadOnlyList<SkillSchool> Schools(JsonArray reference, int role) =>
        reference.OfType<JsonObject>().Where(row => GameJson.Number(row["roleid"]) == role)
            .Select(row => new SkillSchool(GameJson.Number(row["xfIndex"]), GameJson.Text(row["xf"]),
                Texts(row["codes"]), Texts(row["names"]),
                (row["needMmp"] as JsonArray)?.Select(value => GameJson.Number(value)).ToArray() ?? []))
            .Where(row => row.Index is 0 or 1 && row.Codes.Length == 5)
            .OrderBy(row => row.Index).ToArray();

    private static string[] Texts(JsonNode? node) => (node as JsonArray)?.Select(GameJson.Text).ToArray() ?? [];

    private static JsonObject? SchoolRecord(JsonObject player, int index) =>
        player["isstudyskill"] is JsonArray list && list.Count > index ? list[index] as JsonObject : null;

    private static Dictionary<string, int> Levels(JsonObject? entry)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string value in GameJson.Text(entry?["skillName"]).Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = value.Split('~', 2);
            if (pair.Length == 2 && int.TryParse(pair[1], out int level))
                result[pair[0]] = level;
        }
        return result;
    }

    internal static int SchoolLevel(JsonObject player, SkillSchool school) => GameJson.Number(SchoolRecord(player, school.Index)?["xflevel"]);

    internal static IReadOnlyList<SkillRow> Rows(JsonObject player, SkillSchool school)
    {
        var levels = Levels(SchoolRecord(player, school.Index));
        return school.Codes.Select((code, index) => new SkillRow(school.Name, code,
            school.Names.ElementAtOrDefault(index) ?? code, school.Costs.ElementAtOrDefault(index),
            levels.GetValueOrDefault(code))).ToArray();
    }

    private static JsonObject EnsureSchool(JsonObject player, int index)
    {
        var list = player["isstudyskill"] as JsonArray;
        if (list is null)
            player["isstudyskill"] = list = new JsonArray();
        while (list.Count <= index)
            list.Add(new JsonObject { ["xflevel"] = 0, ["skillName"] = "" });
        if (list[index] is not JsonObject)
            list[index] = new JsonObject { ["xflevel"] = 0, ["skillName"] = "" };
        return (JsonObject)list[index]!;
    }

    internal static void SetSchool(JsonObject player, SkillSchool school, int? schoolLevel = null, int? all = null, string? code = null, int singleLevel = 0)
    {
        if (schoolLevel.HasValue && (schoolLevel<0 || schoolLevel>GameRules.Int("skill","schoolMax")) || all.HasValue && (all<0 || all>GameRules.Int("skill","activeMax")) || singleLevel<0 || singleLevel>GameRules.Int("skill","activeMax"))
            throw new InvalidDataException("技能等级应为 0–3，心法等级应为 0–5。");
        JsonObject record = EnsureSchool(player, school.Index);
        if (schoolLevel.HasValue)
            record["xflevel"] = schoolLevel.Value;
        if (all.HasValue || code is not null)
        {
            var levels = Levels(record);
            foreach (string name in school.Codes)
                if (all.HasValue || name == code)
                    levels[name] = all ?? singleLevel;
            record["skillName"] = string.Join('|', school.Codes.Where(name => levels.GetValueOrDefault(name) > 0)
                .Select(name => $"{name}~{levels[name]}"));
            if (player["skillbykey"] is JsonArray bindings)
            {
                for (int i = bindings.Count - 1; i >= 0; i--)
                {
                    if (bindings[i] is not JsonObject binding)
                        continue;
                    string bound = GameJson.Text(binding["skillName"]);
                    if (!school.Codes.Contains(bound) || !all.HasValue && bound != code)
                        continue;
                    int level = levels.GetValueOrDefault(bound);
                    if (level == 0)
                        bindings.RemoveAt(i);
                    else
                        binding["slev"] = level;
                }
            }
        }
        player["skillLimit"] ??= GameRules.Int("skill","defaultSlots");
    }

    internal static void SetPassive(JsonObject player, IReadOnlyList<int> levels)
    {
        if (levels.Count != PassiveNames.Length || levels.Any(level => level<0 || level>GameRules.Int("skill","passiveMax")))
            throw new InvalidDataException("五项被动技能的等级均应为 0–5。");
        player["ispassiveskill"] = new JsonArray(levels.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        player["biggestpassiveskilllevel"] = levels.Max();
        player["skillLimit"] ??= GameRules.Int("skill","defaultSlots");
    }
}

internal static class BuffData
{
    internal static readonly int MaxFrames=GameRules.Int("buff","maxFrames");
    internal static readonly IReadOnlyList<BuffKind> Kinds=GameRules.Rows("buff","kinds").Select(row=>new BuffKind(GameJson.Number(row!["id"]),GameJson.Text(row["name"]),GameJson.Number(row["default"]),row["limit"] is null?null:GameJson.Number(row["limit"]))).ToArray();

    internal static List<BuffRow> Rows(JsonObject? save)
    {
        var rows = new List<BuffRow>();
        foreach (string row in GameJson.Text(save?["savedBuffString"]).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = row.Split('|');
            if (fields.Length != 3 || !int.TryParse(fields[0], out int kind) ||
                !int.TryParse(fields[1], out int value) || !int.TryParse(fields[2], out int frames))
                continue;
            string name = Kinds.FirstOrDefault(entry => entry.Id == kind)?.Name ?? $"其他增益 #{kind}";
            rows.Add(new BuffRow(kind, name, value, frames));
        }
        return rows;
    }

    internal static void Write(JsonObject save, IEnumerable<BuffRow> rows) =>
        save["savedBuffString"] = string.Join(',', rows.Select(row => $"{row.Type}|{row.Value}|{row.Frames}"));

    internal static void Put(JsonObject save, BuffKind kind, int value, int frames)
    {
        if (!Kinds.Contains(kind) || value < 0 || (frames < 1 || frames > MaxFrames))
            throw new InvalidDataException("该 Buff 类型、数值或时长无效。");
        var rows = GameJson.Text(save["savedBuffString"]).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(row => row.Split('|')[0] != kind.Id.ToString(CultureInfo.InvariantCulture)).ToList();
        rows.Add($"{kind.Id}|{Math.Min(value, kind.Limit ?? int.MaxValue)}|{frames}");
        save["savedBuffString"] = string.Join(',', rows);
    }

    internal static void Remove(JsonObject save, int type) => save["savedBuffString"] = string.Join(',',
        GameJson.Text(save["savedBuffString"]).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(row => row.Split('|')[0] != type.ToString(CultureInfo.InvariantCulture)));
    internal static void AddAll(JsonObject save)
    {
        foreach (var kind in Kinds) Put(save, kind, kind.Default, MaxFrames);
    }

}

internal sealed record AttributeField(int Index, string Key, string Label, string? Coefficient = null, bool Margin = false);

internal static class EquipmentData
{
    private static AttributeField[] Fields(string name)=>GameRules.Rows("equipment",name).Select(row=>new AttributeField(GameJson.Number(row!["index"]),GameJson.Text(row["key"]),GameJson.Text(row["label"]),row["coefficient"] is null?null:GameJson.Text(row["coefficient"]),row["margin"]!.GetValue<bool>())).ToArray();
    internal static readonly AttributeField[] Hero=Fields("heroFields");
    internal static readonly AttributeField[] Pet=Fields("petFields");

    internal static bool Strengthen(string type) => type is "zbwq" or "zbfj" or "zbsp" or "zbsz";
    internal static bool Fashion(string type) => type is "zbsz" or "zbcb";
    internal static bool Wearable(string type) => type is "zbwq" or "zbfj" or "zbsp" or "zbtx" or "zbfb" or "zbsz" or "zbcb";
    internal static string Number(decimal value) => value.ToString("0.############", CultureInfo.InvariantCulture);
    internal static decimal Read(string? value) => decimal.TryParse(value, NumberStyles.Float,
        CultureInfo.InvariantCulture, out decimal number) ? number : 0;
    internal static bool IsPet(string field) => field == Inventory.PetGear;
    internal static string Code(string[] data, bool pet) => data[pet ? 0 : 2];
    internal static int StrengthLevel(string value) =>
        int.TryParse(value, out int strength) ? Math.Clamp(strength, 0, GameRules.Int("equipment","petStrengthMax")) : 0;

    internal static string[] Parse(string raw, bool pet)
    {
        var data = raw.Split('|');
        if (pet ? data.Length < 13 : data.Length < 28)
            throw new InvalidDataException("装备记录缺少字段，无法安全调整。");
        if (!pet && Fashion(data[3]) && data.Length == 28)
        {
            string extra = data[27];
            Array.Resize(ref data, 29);
            data[27] = "never";
            data[28] = extra;
        }
        return data;
    }

    internal static decimal? Limit(GameFolder game, string[] data, bool pet, AttributeField field, int strength)
    {
        string code = Code(data, pet);
        JsonObject refs = pet ? game.PetEquipmentReference : game.EquipmentReference;
        if (refs[code] is not JsonObject row)
            return null;
        if (!pet && field.Key == "eatblood")
            return 0;
        if (row[field.Key] is not JsonNode value || !pet && field.Key == "deephit")
            return null;
        decimal bonus = !pet && field.Coefficient is string key
            ? strength * Read(GameJson.Text(game.EquipmentStrength[code]?[key])) : 0;
        return Math.Max(0, Read(GameJson.Text(value)) + (field.Margin ? GameRules.Int("equipment","margin") : 0) - bonus);
    }

    internal static void Maximize(GameFolder game, string[] data, bool pet)
    {
        string code = Code(data, pet);
        int strength = 0;
        if (pet)
            data[11] = GameRules.Int("equipment","petStrengthMax").ToString(CultureInfo.InvariantCulture);
        else
        {
            if (Strengthen(data[3]))
                data[23] = GameRules.Int("equipment","heroStrengthMax").ToString(CultureInfo.InvariantCulture);
            strength = (int)Read(data[23]);
            if (data[3] == "zbfb")
                data[20] = code == "tjbg" ? "9" : "15";
            if (Fashion(data[3]))
                data[27] = "never";
            for (int i = 15; i <= 19; i++)
                data[i] = "true";
        }
        bool known = (pet ? game.PetEquipmentReference : game.EquipmentReference)[code] is JsonObject;
        foreach (var field in pet ? Pet : Hero)
        {
            decimal? value = Limit(game, data, pet, field, strength);
            if (!value.HasValue)
            {
                value = known ? 0 : !pet
                    ? field.Key == "eatblood" ? 0 : GameRules.Int("equipment","unknownHeroFallback")
                    : game.PetEquipmentReference.Select(pair => Read(GameJson.Text(pair.Value?[field.Key]))).DefaultIfEmpty().Max();
            }
            data[field.Index] = Number(value.Value);
        }
    }
}
