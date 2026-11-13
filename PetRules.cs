using System.Globalization;
using System.Text.Json.Nodes;

namespace GmStudio;

internal sealed record PetSkillRule(string Code, string Category, string? Locked = null, string Acquisition = "");
internal sealed record PetSkillSpirit(string Name, string[] EquipmentCodes);
internal sealed record PetSkillDefinition(string Code, string Category, int FirstStage = 0,
    bool NeedsFullPotential = false, PetSkillSpirit? Spirit = null)
{
    internal string Acquisition => Spirit is not null ? "穿戴精灵直接获得" : NeedsFullPotential ? "进化后领悟" : "";
    internal string AcquisitionDescription => Spirit is {} spirit
        ? $"穿戴{spirit.Name}直接获得。第一形态即可获得，无需超进化或培养潜力极限；需要空余技能格。"
        : NeedsFullPotential ? "进化至第四阶完全体，超进化后潜力极限再次达到100%，再领悟奥义。无需精灵；需要空余技能格。" : "";
}

internal sealed class PetRules(GameFolder game, string code)
{
    internal static readonly string[] BasicSkills=GameRules.Strings("pet","basicSkills");
    internal static readonly (int Base,int Bonus,int Progress,string Key,int MaxBase)[] Abilities=GameRules.Rows("pet","abilities").Select(row=>(GameJson.Number(row!["base"]),GameJson.Number(row["bonus"]),GameJson.Number(row["progress"]),GameJson.Text(row["key"]),GameJson.Number(row["maxBase"]))).ToArray();
    internal static readonly int QualityExperienceBudget=GameRules.Int("pet","qualityExperienceBudget");
    private static readonly string[] QualityKeys=["hpquality","mpquality","atkquality","defquality"];
    private static readonly int[] ProgressThresholds=GameRules.Integers("pet","progressThresholds");
    private readonly string family = PetData.Family(code);
    internal int Stage { get; } = StageOf(code);
    private JsonNode? Reference => (game.Official["petFamilyRef"] as JsonObject)?[family];

    internal static int StageOf(string code) => code.Length > 0 && char.IsAsciiDigit(code[^1]) ? code[^1] - '0' : 0;

    internal static int Value(IReadOnlyList<string> fields, int index) =>
        int.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

    internal int QualityMaximum(int index)
    {
        int reference = GameJson.Number(Reference?[QualityKeys[index - 9]], GameRules.Int("pet","qualityDefault"));
        return (int)Math.Clamp((long)reference + GameRules.Int("pet","qualityMargin"), 0, GameRules.Int("pet","qualityMax"));
    }

    internal int BaseMaximum(string key, int maxBase) => Math.Clamp(GameJson.Number(Reference?[key], maxBase), 0, maxBase);

    internal int TotalMaximum(string key) => Math.Clamp(GameJson.Number(Reference?[key + "all"], GameRules.Int("pet","totalAbilityMax")), 0, GameRules.Int("pet","totalAbilityMax"));

    internal int Capacity(IReadOnlyList<string> fields) =>
        (int)Math.Clamp((long)Value(fields, 15) + Value(fields, 25), 0, TotalMaximum("perception"));

    internal static int ProgressTarget(int total) => total > 0 && total < ProgressThresholds.Length ? ProgressThresholds[total] : 0;

    internal static int NextExperience(int level) => level <= 10 ? Math.Max(1, level) * 50 :
        (int)Math.Min(int.MaxValue, (long)(level + 1) * (level + 1) * (5 + (level - 10) * 2));

    internal (int Min, int? Max) Range(int index, IReadOnlyList<string> fields, int levelBonus)
    {
        foreach (var (basis, bonus, progress, key, maxBase) in Abilities)
        {
            if (index == basis)
                return (0, BaseMaximum(key, maxBase));
            if (index == bonus)
                return (0, Math.Max(0, TotalMaximum(key) - Math.Clamp(Value(fields, basis), 0, 8)));
            if (index == progress)
            {
                long total = (long)Value(fields, basis) + Value(fields, bonus);
                return (0, total >= TotalMaximum(key) || total <= 0 ? 0 : ProgressThresholds[(int)total] - 1);
            }
        }
        return index switch
        {
            5 => (1, GameRules.Int("pet","maxLevel")),
            6 => (0, ExperienceMaximum(Value(fields, 5), levelBonus)),
            >= 9 and <= 12 => (0, QualityMaximum(index)),
            >= 20 and <= 23 => (0, QualityExperienceMaximum(index, fields)),
            31 => (0, GameRules.Int("pet","potentialMax")),
            _ => (0, null)
        };
    }

    private static int QualityExperienceMaximum(int index, IReadOnlyList<string> fields)
    {
        // Rebirth allocates one shared pool to all four quality experience fields.
        long used = Enumerable.Range(20, 4).Where(other => other != index)
            .Sum(other => (long)Math.Max(0, Value(fields, other)));
        return (int)Math.Max(0, QualityExperienceBudget - used);
    }

    private static int ExperienceMaximum(int value, int levelBonus)
    {
        int level = Math.Clamp(value, 1, GameRules.Int("pet","maxLevel"));
        if (level >= GameRules.Int("pet","baseLevelCap") + Math.Clamp(levelBonus, 0, GameRules.Int("pet","levelBonusMax")))
            return 0;
        return NextExperience(level) - 1;
    }

    internal static bool Affects(int field, int changed) =>
        field == changed || field is >= 20 and <= 23 && changed is >= 20 and <= 23 ||
        Abilities.Any(group => field == group.Bonus && changed == group.Base ||
            field == group.Progress && (changed == group.Base || changed == group.Bonus));

    internal IReadOnlyList<PetSkillDefinition> SkillDefinitions()
    {
        var result = BasicSkills.Select(skill => new PetSkillDefinition(skill, "基础")).ToList();
        var talents = game.Official["petTalent"] as JsonObject;
        for (int shape = 1; shape <= 5; shape++)
        {
            string skill = GameJson.Text(talents?[family + shape]?["add"]);
            if (skill.Length > 0 && !result.Any(item => item.Code == skill))
                result.Add(new PetSkillDefinition(skill, "专属", shape));
        }

        string esoteric = GameJson.Text((game.Official["petEsoteric"] as JsonObject)?[family]);
        if (esoteric.Length > 0)
            result.Add(new PetSkillDefinition(esoteric, "奥义", 4, NeedsFullPotential: true));

        var spiritRule=GameRules.Node("rules","pet","spiritUltimates")[family];
        string? spiritSkill=spiritRule is null?null:GameJson.Text(spiritRule["skill"]);
        string spiritName=GameJson.Text(spiritRule?["name"]);
        if (spiritSkill is not null)
        {
            var fills = (game.Official["jnToPet"] as JsonArray ?? []).OfType<JsonObject>()
                .FirstOrDefault(row => GameJson.Text(row["petFamily"]) == family)?["fills"] as JsonArray;
            var spirit = new PetSkillSpirit(spiritName, fills?.Select(GameJson.Text).ToArray() ?? []);
            result.Add(new PetSkillDefinition(spiritSkill, "奥义", Spirit: spirit));
        }
        return result;
    }

    internal bool MatchesForm(PetSkillDefinition skill) =>
        skill.NeedsFullPotential ? Stage == skill.FirstStage : Stage >= skill.FirstStage;

    internal IReadOnlyList<PetSkillRule> Skills(IReadOnlyList<string> fields, IReadOnlyList<string> equipment) =>
        SkillDefinitions().Where(MatchesForm).Select(skill =>
        {
            string? locked = null;
            if (skill.NeedsFullPotential && Value(fields, 31) < 100)
                locked = "进化至第四阶完全体，超进化后潜力极限再次达到100%，再领悟奥义。";
            if (skill.Spirit is { } spirit && !equipment.Any(raw => spirit.EquipmentCodes.Contains(raw.Split('|', 2)[0])))
                locked = $"尚未穿戴{spirit.Name}；穿戴后可直接获得，需要空余技能格。";
            return new PetSkillRule(skill.Code, skill.Category, locked, skill.Acquisition);
        }).ToArray();

    internal static string Aliases(string code)=>GameRules.Texts("pet","aliases").GetValueOrDefault(code,"");
}
