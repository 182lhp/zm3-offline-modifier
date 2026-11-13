namespace GmStudio;

public sealed record SkillHint(string Name, string Description, string Condition = "", string Code = "", string Acquisition = "");

internal static class SkillHelp
{
    private static readonly Dictionary<string,string> PetBasics=GameRules.Texts("skillHelp","basic");
    internal static readonly Dictionary<string,string> Heroes=GameRules.Texts("skillHelp","hero");

    internal static string PetDescription(GameFolder game, string code) => PetBasics.GetValueOrDefault(code)
        ?? (GameJson.Text(game.PetSkillDescriptions[code]) is { Length: > 0 } description ? description
            : "当前游戏目录未收录这项技能的效果说明。");

    internal static SkillHint Pet(GameFolder game, string code, string name)
    {
        var definition=game.PetNames.Select(pair=>PetData.Family(pair.Key)).Distinct()
            .SelectMany(family=>new PetRules(game,family+"1").SkillDefinitions()).FirstOrDefault(skill=>skill.Code==code);
        string condition=definition is null ? "历史技能，是否可重新学习取决于当前宠物。" :
            definition.Category=="基础" ? "各品种、各形态通用；技能数量受悟性限制。" :
            definition.Category=="奥义" ? definition.AcquisitionDescription :
            $"从第{definition.FirstStage}进化阶起可学习；技能数量受悟性限制。";
        return new(name,PetDescription(game,code),condition,code,definition?.Acquisition ?? "");
    }
}
