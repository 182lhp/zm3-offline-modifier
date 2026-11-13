using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;

namespace GmStudio;

internal sealed class PetData
{
    internal static readonly int BackpackCapacity = GameRules.Int("inventory","petGear");
    internal static readonly string[] FieldNames =
    [
        "品种编号", "当前生命", "最大生命", "当前魔法", "最大魔法", "等级", "当前经验", "攻击", "防御",
        "生命资质", "魔法资质", "攻击资质", "防御资质", "寿命", "品质", "悟性", "根骨", "灵力",
        "出战", "技能编号（用 ~ 分隔）", "生命经验", "魔法经验", "攻击经验", "防御经验", "潜力点",
        "悟性加成", "灵力加成", "根骨加成", "悟性进度", "灵力进度", "根骨进度", "潜力极限"
    ];
    internal string[] Fields { get; }
    internal List<string> Equipment { get; private set; }

    internal PetData(string record)
    {
        var parts = record.Split('&', 2);
        Fields = parts[0].Split('|');
        if (Fields.Length < 32)
            throw new InvalidDataException("宠物记录缺少 32 个基础字段。");
        Equipment = parts.Length > 1 ? parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries).ToList() : [];
    }

    internal static PetData New(string code)
    {
        var fields = Enumerable.Repeat("0", 32).ToArray();
        fields[0] = code;
        fields[1] = fields[2] = "1000";
        fields[3] = fields[4] = "800";
        fields[5] = fields[14] = "1";
        fields[7] = "60";
        fields[8] = "10";
        fields[13] = "100";
        fields[19] = "";
        return new PetData(string.Join('|', fields));
    }

    internal static string Family(string code) => code == "nea1" ? "neat" : code.TrimEnd("0123456789".ToCharArray());
    internal string Encode() => string.Join('|', Fields) + (Equipment.Count > 0 ? "&" + string.Join(';', Equipment) + ";" : "");

    internal static int RepairPotentialLimits(JsonObject save)
    {
        int repaired = 0;
        var containers = GameFolder.Players(save).Select(player => (JsonObject)save[player.Key]!).Prepend(save);
        foreach (var container in containers)
        foreach (string key in new[] { Inventory.Pets, "petWareHouseSaveString" })
        {
            if (container[key] is null) continue;
            var records = GameJson.Text(container[key]).Split('}');
            bool changed = false;
            for (int index = 0; index < records.Length; index++)
            {
                var parts = records[index].Split('&', 2);
                var fields = parts[0].Split('|');
                if (fields.Length < 32 || !long.TryParse(fields[31], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out long value) || value is >= 0 and <= 100) continue;
                fields[31] = Math.Clamp(value, 0, GameRules.Int("pet","potentialMax")).ToString(CultureInfo.InvariantCulture);
                records[index] = string.Join('|', fields) + (parts.Length == 2 ? "&" + parts[1] : "");
                repaired++;
                changed = true;
            }
            if (changed) container[key] = string.Join('}', records);
        }
        return repaired;
    }

    internal void UseReference(GameFolder game)
    {
        var rows = (game.Extreme["pets"] as JsonArray ?? new()).OfType<JsonObject>();
        var row = rows.FirstOrDefault(item => GameJson.Text(item["petName"]) == Fields[0]);
        if (row is null)
            throw new InvalidDataException("这只宠物没有同品种、同进化阶段的现成极品模板，可使用「一键极品」按参考表计算。");
        var exact = new PetData(GameJson.Text(row["raw"]));
        exact.Fields[18] = Fields[18];
        Array.Copy(exact.Fields, Fields, 32);
        Fields[31] = Math.Clamp(PetRules.Value(Fields, 31), 0, GameRules.Int("pet","potentialMax")).ToString(CultureInfo.InvariantCulture);
        Equipment = exact.Equipment;
    }

    internal void Maximize(GameFolder game)
    {
        MaximizeGrowth(game);
        FillSkills(game);
    }

    internal static string FinalForm(GameFolder game, string code) => game.PetNames
        .Where(pair => Family(pair.Key) == Family(code))
        .OrderByDescending(pair => PetRules.StageOf(pair.Key))
        .Select(pair => pair.Key).FirstOrDefault()
        ?? throw new InvalidDataException("当前游戏目录没有这只宠物的进化形态资料。");

    internal void MaximizeToFinal(GameFolder game, List<string>? backpack = null)
    {
        var result = new PetData(Encode());
        result.MaximizeFinalGrowthAndSkills(game);
        result.Dress(game, 1, 2, false);
        var previous = Equipment.ToList();
        for (int index = 0; index < result.Equipment.Count; index++)
        {
            string code = result.Equipment[index].Split('|', 2)[0];
            int owned = previous.FindIndex(raw => raw.Split('|', 2)[0] == code);
            if (owned < 0) continue;
            var gear = EquipmentData.Parse(previous[owned], true);
            EquipmentData.Maximize(game, gear, true);
            result.Equipment[index] = string.Join('|', gear);
            previous.RemoveAt(owned);
        }
        if (previous.Count > 0 && (backpack is null || backpack.Count + previous.Count > BackpackCapacity))
            throw new InvalidDataException($"宠物装备背包空间不足，需腾出 {previous.Count} 个位置（最多 {BackpackCapacity} 件）。本次未作修改。");
        // Commit only after all equipment and capacity checks pass. Matching gear is
        // upgraded in place; displaced records are kept byte-for-byte in the bag.
        backpack?.AddRange(previous);
        Array.Copy(result.Fields, Fields, Fields.Length);
        Equipment = result.Equipment;
    }

    private void MaximizeFinalGrowthAndSkills(GameFolder game)
    {
        string final = FinalForm(game, Fields[0]);
        // This explicit GM action replaces the complete skill list. Manual learning
        // and new-pet creation continue to use their original eligibility rules.
        Fields[19] = "";
        Fields[0] = final;
        MaximizeGrowth(game);
        var rules = new PetRules(game, final);
        var definitions = rules.SkillDefinitions().Where(rules.MatchesForm).ToArray();
        var skills = definitions.Where(skill => skill.Category == "专属").OrderBy(skill => skill.FirstStage)
            .Concat(definitions.Where(skill => skill.Category == "奥义"))
            .Select(skill => skill.Code).Distinct(StringComparer.Ordinal).ToArray();
        if (skills.Length > rules.Capacity(Fields))
            throw new InvalidDataException("当前品种的技能容量不足以容纳全部专属技能和奥义。");
        Fields[19] = string.Join('~', skills);
    }

    internal void MaximizeGrowth(GameFolder game)
    {
        int potential = Math.Max(GameRules.Int("pet","initialPotential"), PetRules.Value(Fields, 24));
        int[] experience = Enumerable.Range(20, 4).Select(index => Math.Max(0, PetRules.Value(Fields, index))).ToArray();
        FillGrowth(game);
        Fields[24] = potential.ToString(CultureInfo.InvariantCulture);
        int remaining = PetRules.QualityExperienceBudget;
        for (int index = 0; index < experience.Length; index++)
        {
            int value = Math.Min(experience[index], remaining);
            Fields[20 + index] = value.ToString(CultureInfo.InvariantCulture);
            remaining -= value;
        }
        Fields[5] = GameRules.Int("pet","maxLevel").ToString(CultureInfo.InvariantCulture);
        Fields[6] = "0";
        var curves=GameRules.Rows("pet","statCurves");
        string StatFor(int index)=>Stat(Fields[9+index],EquipmentData.Read(GameJson.Text(curves[index]!["multiplier"])),GameJson.Number(curves[index]!["initial"]));
        Fields[1]=Fields[2]=StatFor(0);Fields[3]=Fields[4]=StatFor(1);Fields[7]=StatFor(2);Fields[8]=StatFor(3);
    }

    internal static PetData Create(GameFolder game, string code, bool extreme)
    {
        if (!game.PetNames.ContainsKey(code))
            throw new InvalidDataException("请选择当前游戏目录中的宠物形态。");
        string first=game.PetNames.Where(pair=>Family(pair.Key)==Family(code))
            .OrderBy(pair=>PetRules.StageOf(pair.Key)).First().Key;
        var pet = New(first);
        if (extreme)
        {
            pet.MaximizeToFinal(game);
        }
        else
        {
            pet.FillGrowth(game);
            // Level-one babies start without any learned skills, including family talents.
            pet.Fields[19] = "";
        }
        pet.Fields[18] = "0";
        return pet;
    }

    private void FillGrowth(GameFolder game)
    {
        var rules = new PetRules(game, Fields[0]);
        for (int index = 9; index <= 12; index++)
            Fields[index] = rules.QualityMaximum(index).ToString(CultureInfo.InvariantCulture);
        foreach (var (index, bonus, progress, key, maxBase) in PetRules.Abilities)
        {
            int total = rules.TotalMaximum(key);
            int current = Math.Min(rules.BaseMaximum(key, maxBase), total);
            Fields[index] = current.ToString(CultureInfo.InvariantCulture);
            Fields[bonus] = (total - current).ToString(CultureInfo.InvariantCulture);
            Fields[progress] = "0";
        }
        Fields[13]=GameRules.Int("pet","lifeMax").ToString(CultureInfo.InvariantCulture);
        Fields[24]=GameRules.Int("pet","initialPotential").ToString(CultureInfo.InvariantCulture);Fields[31]=GameRules.Int("pet","potentialMax").ToString(CultureInfo.InvariantCulture);
        if (Fields[14] is "" or "0")
            Fields[14] = "1";
        for (int i = 20; i <= 23; i++)
            Fields[i] = "0";
    }

    private void FillSkills(GameFolder game)
    {
        var rules = new PetRules(game, Fields[0]);
        var available = rules.Skills(Fields, Equipment).Where(skill => skill.Locked is null).ToArray();
        var ultimate = available.FirstOrDefault(skill => skill.Category == "奥义");
        int capacity = rules.Capacity(Fields);
        var chosen = available.Where(skill => skill != ultimate).OrderBy(skill => skill.Category == "基础")
            .Take(Math.Max(0, capacity - (ultimate is null ? 0 : 1))).Select(skill => skill.Code);
        Fields[19] = string.Join('~', ultimate is null || capacity == 0 ? chosen : chosen.Append(ultimate.Code));
    }

    private static string Stat(string quality, decimal multiplier, int initial) =>
        ((int)(initial + (GameRules.Int("pet","maxLevel")-1) * EquipmentData.Read(quality) * multiplier)).ToString(CultureInfo.InvariantCulture);

    internal void Dress(GameFolder game, int set, int quality, bool keepSpirit)
    {
        var suits = game.Official["petSuits"]?["petSuit"]?["sets"] as JsonArray;
        int index = set * 3 + quality;
        if (suits is null || index >= suits.Count || suits[index] is not JsonArray suit)
            throw new InvalidDataException("游戏目录没有该套装的参考记录。");
        string family = Family(Fields[0]);
        string oldSpirit = Equipment.Select(raw => raw.Split('|', 2)[0]).FirstOrDefault(code => PetType(game, code) == "zbjn") ?? "";
        var spiritRow = (game.Official["jnToPet"] as JsonArray ?? new()).OfType<JsonObject>()
            .FirstOrDefault(row => GameJson.Text(row["petFamily"]) == family);
        string spirit = keepSpirit && oldSpirit.Length > 0 ? oldSpirit :
            spiritRow?["fills"] is JsonArray fills && fills.Count > quality ? GameJson.Text(fills[quality]) :
            GameRules.Strings("pet","defaultSpirits")[quality];
        var codes = suit.Select(GameJson.Text).Append(spirit).Where(code => code.Length > 0).ToArray();
        if (codes.Length != 5 || codes.Any(code => game.PetEquipmentReference[code] is not JsonObject) ||
            codes.Select(code => PetType(game, code)).Distinct().Count() != 5)
            throw new InvalidDataException("宠物套装或对应精灵的上限资料不完整，本次未作修改。");
        string[] order = ["zbjn", "zbst", "zbmz", "zbyf", "zbxz"];
        Equipment = codes.OrderBy(code => Array.IndexOf(order, PetType(game, code))).Select(code =>
        {
            if (keepSpirit && code == oldSpirit)
                return Equipment.First(raw => raw.Split('|', 2)[0] == oldSpirit);
            var gear = Enumerable.Repeat("0", 13).ToArray();
            gear[0] = code;
            EquipmentData.Maximize(game, gear, true);
            return string.Join('|', gear);
        }).ToList();
    }

    internal static string PetType(GameFolder game, string code)
    {
        var row = (game.Official["petEquipItems"] as JsonArray ?? new()).OfType<JsonObject>()
            .FirstOrDefault(item => GameJson.Text(item["fillName"]) == code);
        return row is null ? game.Catalog.FirstOrDefault(item => item.Code == code)?.Type ?? "" : GameJson.Text(row["type"]);
    }

    internal static int Release(JsonObject character, int index)
    {
        var pets = Inventory.Records(character, Inventory.Pets);
        if (index < 0 || index >= pets.Count)
            throw new InvalidDataException("请先选择要放生的宠物。");
        var pet = new PetData(pets[index]);
        var backpack = Inventory.Records(character, Inventory.PetGear);
        if (pet.Equipment.Count > 0 && backpack.Count + pet.Equipment.Count > BackpackCapacity)
            throw new InvalidDataException($"宠物装备背包空间不足，需腾出 {pet.Equipment.Count} 个位置存放已穿戴装备。本次未放生。");
        backpack.AddRange(pet.Equipment);
        pets.RemoveAt(index);
        if (pet.Equipment.Count > 0) character[Inventory.PetGear] = string.Join('}', backpack);
        character[Inventory.Pets] = string.Join('}', pets);
        return pet.Equipment.Count;
    }

    internal int Wear(GameFolder game, List<string> backpack, int index)
    {
        if (index < 0 || index >= backpack.Count) throw new InvalidDataException("请先选择一件宠物背包装备。");
        string raw = backpack[index];
        EquipmentData.Parse(raw, true);
        string type = PetType(game, raw.Split('|', 2)[0]);
        if (type is not ("zbst" or "zbmz" or "zbyf" or "zbxz" or "zbjn"))
            throw new InvalidDataException("这件装备没有可识别的宠物栏位。");
        int slot = Equipment.FindIndex(item => PetType(game, item.Split('|', 2)[0]) == type);
        if (slot >= 0) { backpack[index] = Equipment[slot]; Equipment[slot] = raw; }
        else { slot = Equipment.Count; Equipment.Add(raw); backpack.RemoveAt(index); }
        return slot;
    }

    internal void Return(List<string> backpack, int index)
    {
        if (index < 0 || index >= Equipment.Count) throw new InvalidDataException("请先选择一件已穿戴装备。");
        backpack.Add(Equipment[index]);
        Equipment.RemoveAt(index);
    }

    // A GM cultivation step is explicitly one potential point and one progress point.
    // The game has separate debug/release multipliers for potential-limit growth.
    internal bool Cultivate(GameFolder game, int basis)
    {
        var rules = new PetRules(game, Fields[0]);
        var ability = PetRules.Abilities.FirstOrDefault(item => item.Base == basis);
        int points = PetRules.Value(Fields, 24);
        if (points <= 0 || ability.Base == 0) return false;
        int total = PetRules.Value(Fields, basis) + PetRules.Value(Fields, ability.Bonus);
        int target = PetRules.ProgressTarget(total);
        if (total >= rules.TotalMaximum(ability.Key) || target == 0) return false;
        int progress = Math.Clamp(PetRules.Value(Fields, ability.Progress), 0, target - 1) + 1;
        if (progress >= target)
        {
            Fields[ability.Bonus] = (Math.Max(0, PetRules.Value(Fields, ability.Bonus)) + 1).ToString(CultureInfo.InvariantCulture);
            progress = 0;
        }
        Fields[ability.Progress] = progress.ToString(CultureInfo.InvariantCulture);
        Fields[24] = (points - 1).ToString(CultureInfo.InvariantCulture);
        Fields[31] = Math.Clamp((long)PetRules.Value(Fields, 31) + 1, 0, GameRules.Int("pet","potentialMax")).ToString(CultureInfo.InvariantCulture);
        return true;
    }
}
