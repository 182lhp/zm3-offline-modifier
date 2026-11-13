using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

internal sealed record PetGuideForm(PetEntry Entry, int Number, string[] SaveCodes)
{
    public string Code => Entry.Code;
    public string Label => $"第 {Number} 形态 · {Entry.Name}";
    public bool IncludesStage(int stage) => SaveCodes.Any(code => PetRules.StageOf(code) == stage);
}

internal sealed record PetGuideFamily(string Code, string Label, PetGuideForm[] Forms)
{
    public PetEntry Entry => Forms[0].Entry;
}

internal static class PetGuideCatalog
{
    internal static PetGuideFamily[] Families(GameFolder game)
    {
        return game.PetNames.GroupBy(pair => PetData.Family(pair.Key)).Select(group =>
        {
            // Several save stages can share the same in-game form name.
            var forms = group.OrderBy(pair => PetRules.StageOf(pair.Key))
                .GroupBy(pair => GameJson.Text(pair.Value), StringComparer.Ordinal).Select((shape, index) =>
                {
                    var codes = shape.Select(pair => pair.Key).ToArray();
                    string code = codes[0];
                    return new PetGuideForm(new PetEntry(code, shape.Key, game.Portrait("pets", code)), index + 1, codes);
                }).ToArray();
            string first = forms[0].Entry.Name, last = forms[^1].Entry.Name;
            return new PetGuideFamily(group.Key, first == last ? first : $"{first} → {last}", forms);
        }).ToArray();
    }
}
internal sealed record PetGuideSkill(PetEntry Art, string Condition, string Description, string Status, bool IsFuture)
{
    public SkillHint? Hint => Art.Hint is {} hint ? hint with { Condition=Condition } : null;
}
internal sealed record PetGuideSection(string Title, string Intro, string EmptyText, PetGuideSkill[] Rows)
{
    public string Heading => $"{Title} · {Rows.Length}";
    public bool IsEmpty => Rows.Length == 0;
}

public partial class PetGuideWindow : Window
{
    private readonly GameFolder game;
    private string? initialCode;

    internal PetGuideWindow(Window owner, GameFolder game, string? selectedCode)
    {
        this.game = game;
        initialCode = selectedCode == "nea1" ? "neat1" : selectedCode;
        InitializeComponent();
        Owner = owner;
        Width = Math.Clamp(SystemParameters.WorkArea.Width, MinWidth, Width);
        Height = Math.Clamp(SystemParameters.WorkArea.Height, MinHeight, Height);

        var families = PetGuideCatalog.Families(game);
        FamilyBox.ItemsSource = families;
        var selectedFamily = initialCode is string requested
            ? families.FirstOrDefault(family => family.Forms.Any(form => form.SaveCodes.Contains(requested))) : null;
        FamilyBox.SelectedItem = selectedFamily ?? families.FirstOrDefault();
    }

    private void FamilyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FamilyBox.SelectedItem is not PetGuideFamily family)
            return;
        FormBox.ItemsSource = family.Forms;
        var selectedForm = initialCode is string requested
            ? family.Forms.FirstOrDefault(form => form.SaveCodes.Contains(requested)) : null;
        FormBox.SelectedItem = selectedForm ?? family.Forms[0];
        initialCode = null;
    }

    private void FormChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FormBox.SelectedItem is not PetGuideForm form || FamilyBox.SelectedItem is not PetGuideFamily family)
            return;
        GuidePortrait.Content = form.Entry;
        GuideName.Text = form.Entry.Name;
        GuideForm.Text = $"第 {form.Number} 形态 / 共 {family.Forms.Length} 种";
        GuideSource.Text = AcquisitionGuide.ForPet(form.Code, form.Entry.Name);
        var rules = new PetRules(game, form.Code);
        var definitions = rules.SkillDefinitions();

        PetGuideSection Section(string category, string title, string intro, string empty) =>
            new(title, intro, empty, definitions.Where(skill => skill.Category == category)
                .Select(skill => SkillRow(rules, family, skill)).ToArray());

        int tab = Math.Max(0, SkillTabs.SelectedIndex);
        SkillTabs.ItemsSource = new[]
        {
            Section("专属", "专属技能", "按首次可领悟的形态排列，后续形态的技能也可预览。", "此品种的专属技能资料为空。"),
            Section("奥义", "奥义", "进化后领悟：完全体培养后领悟。穿戴精灵直接获得：穿戴对应精灵即可，第一形态也适用。", "此品种在当前单机包中的奥义记录为空。"),
            Section("基础", "基础技能", "各品种通用，升级时随机领悟。", "基础技能资料为空。")
        };
        SkillTabs.SelectedIndex = tab;
    }

    private PetGuideSkill SkillRow(PetRules rules, PetGuideFamily family, PetSkillDefinition skill)
    {
        var firstForm = family.Forms.FirstOrDefault(form => form.IncludesStage(skill.FirstStage));
        string stage = skill.FirstStage == 0 ? "全形态" :
            firstForm is null ? "对应进化形态" : $"第 {firstForm.Number} 形态（{firstForm.Entry.Name}）";
        bool future = !rules.MatchesForm(skill);
        string condition = skill.Spirit is not null ? $"{skill.Acquisition}：{skill.AcquisitionDescription}" :
            skill.NeedsFullPotential ? $"{skill.Acquisition} · {stage}\n{skill.AcquisitionDescription}" :
            $"{stage}{(skill.FirstStage == 0 ? "" : "起")} · 升级领悟 · 无需精灵";
        string status = skill.Acquisition.Length>0 ? skill.Acquisition : future ? "后续形态" :
            skill.FirstStage == 0 ? "" : "当前形态可学";
        return new PetGuideSkill(PetEntry.Skill(game, skill.Code), condition,
            SkillHelp.PetDescription(game,skill.Code), status, future);
    }

    private void CloseClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
