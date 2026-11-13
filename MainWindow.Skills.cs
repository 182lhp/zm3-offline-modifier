using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GmStudio;

public partial class MainWindow
{
    private sealed record PassiveChoice(int Value, string Label);
    private sealed record SkillPalette(Brush Ink, Brush Tile);

    private sealed class PassiveRow(string name, string effect, Geometry glyph, SkillPalette palette, int level)
    {
        public string Name { get; } = name;
        public string Effect { get; } = effect;
        public SkillHint Hint => new(Name,$"常驻提高角色的{Effect}。",$"角色通用被动 · 当前 {Level} 级 · 可设为0–5级");
        public Geometry Glyph { get; } = glyph;
        public Brush Ink => palette.Ink;
        public Brush Tile => palette.Tile;
        public int Level { get; } = level;
        public int? DraftLevel { get; set; } = level>=0 && level<=GameRules.Int("skill","passiveMax") ? level : null;
        public IReadOnlyList<PassiveChoice> Options => PassiveChoices;
    }

    private static readonly PassiveChoice[] PassiveChoices = Enumerable.Range(0, GameRules.Int("skill","passiveMax")+1)
        .Select(level => new PassiveChoice(level, level == 0 ? "未学习" : $"{level} 级")).ToArray();
    private static readonly (string Effect, Geometry Glyph)[] PassiveBadges =
    [
        ("生命上限", Badge("M12,21 L4,13 C-2,7 5,1 9,4 L12,7 L15,4 C19,1 26,7 20,13 Z")),
        ("魔法上限", Badge("M12,1 C10,6 4,11 4,16 C4,21 8,24 12,24 C16,24 20,21 20,16 C20,11 14,6 12,1 Z")),
        ("暴击率", Badge("M13,0 L14,8 L22,5 L18,12 L24,15 L16,17 L16,24 L10,19 L3,22 L6,15 L0,11 L8,10 L7,3 L12,7 Z")),
        ("回血", Badge("M9,2 H15 V9 H22 V15 H15 V22 H9 V15 H2 V9 H9 Z")),
        ("回魔", Badge("M19,18 C11,21 4,13 8,5 C2,7 0,15 5,20 C9,24 16,23 19,18 Z M17,1 L18,5 L22,6 L18,7 L17,11 L16,7 L12,6 L16,5 Z"))
    ];
    private static readonly SkillPalette[] SkillPalettes =
    [
        Palette("#27604F", "#E5F0E8"),
        Palette("#8A4C24", "#F5E3CC"),
        Palette("#28577B", "#DFECF6"),
        Palette("#765027", "#F0E5C8"),
        Palette("#3E673D", "#E4EEDC")
    ];
    private JsonObject? passiveOwner;
    private int passiveRole;
    private PassiveRow[] passiveRows = [];

    private static Geometry Badge(string path)
    {
        var glyph = Geometry.Parse(path);
        glyph.Freeze();
        return glyph;
    }

    private static SkillPalette Palette(string ink, string tile)
    {
        static Brush ColorBrush(string value)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze();
            return brush;
        }
        return new SkillPalette(ColorBrush(ink), ColorBrush(tile));
    }

    private static SkillPalette SkillColors(int role) => SkillPalettes[role is >= 1 and <= 4 ? role : 0];

    private void ShowSkills()
    {
        var character = Character;
        int role = GameJson.Number(character?["roleid"]);
        int oldSchool = (SchoolBox.SelectedItem as SkillSchool)?.Index ?? 0;
        var schools = game is not null && character is not null ? SkillData.Schools(game.HeroSkills, role) : [];
        bool wasLoading = loading;
        loading = true;
        try
        {
            SchoolBox.ItemsSource = schools;
            SchoolBox.SelectedItem = schools.FirstOrDefault(school => school.Index == oldSchool) ?? schools.FirstOrDefault();
            SkillEditor.IsEnabled = character is not null;
            ActiveSkillCard.IsEnabled = SkillBatchActions.IsEnabled = schools.Count > 0;
        }
        finally { loading = wasLoading; }

        SkillRole.Text = character is null ? "选择存档和角色以查看技能。" :
            $"{GameJson.Role(role)} · {schools.Count} 套心法 · {SkillData.PassiveNames.Length} 项被动";
        SkillSoulBox.Text = character is null ? "" : Value(character, "lhValue");
        if (SkillsPage.IsVisible)
            ShowSkillPortrait(role);
        ShowSkillRows();
        ShowPassiveRows();
    }

    private void SkillsVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (SkillsPage is { IsVisible: true })
            ShowSkillPortrait(GameJson.Number(Character?["roleid"]));
    }

    private void ShowSkillPortrait(int role)
    {
        var palette = SkillColors(role);
        SkillHeroTile.Background = palette.Tile;
        SkillHeroInitial.Foreground = palette.Ink;
        SkillHeroInitial.Text = role switch { 1 => "悟", 2 => "唐", 3 => "戒", 4 => "沙", _ => "技" };
        SkillHeroImage.Source = game?.RoleArtwork.Portrait(role, 0, 0, out _);
        SkillHeroInitial.Visibility = SkillHeroImage.Source is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowSkillRows()
    {
        if (game is null || Character is not JsonObject character || SchoolBox.SelectedItem is not SkillSchool school)
        {
            SkillsTable.ItemsSource = Array.Empty<SkillRow>();
            SkillLevelBox.Clear();
            SchoolLevelBox.Clear();
            SchoolTitle.Text = "心法等级（0–5）";
            return;
        }
        string? current = (SkillsTable.SelectedItem as SkillRow)?.Code;
        var rows = SkillData.Rows(character, school).Select(row => row with { Icon = game.SkillArtwork.Icon(row.Code) }).ToArray();
        SkillsTable.ItemsSource = rows;
        SkillsTable.SelectedItem = rows.FirstOrDefault(row => row.Code == current) ?? rows.FirstOrDefault();
        SchoolTitle.Text = $"{school.Name}（0–5）";
        SchoolLevelBox.Text = SkillData.SchoolLevel(character, school).ToString(CultureInfo.InvariantCulture);
    }

    private void ShowPassiveRows(bool refresh = false)
    {
        var character = Character;
        int role = GameJson.Number(character?["roleid"]);
        int[] levels = character is null ? [] : Enumerable.Range(0, SkillData.PassiveNames.Length)
            .Select(index => PassiveLevel(character, index)).ToArray();
        if (!refresh && ReferenceEquals(passiveOwner, character) && passiveRole == role &&
            passiveRows.Select(row => row.Level).SequenceEqual(levels))
            return;
        passiveOwner = character;
        passiveRole = role;
        var palette = SkillColors(role);
        passiveRows = levels.Select((level, index) => new PassiveRow(SkillData.PassiveNames[index],
            PassiveBadges[index].Effect, PassiveBadges[index].Glyph, palette, level)).ToArray();
        PassiveList.ItemsSource = passiveRows;
    }

    private static int PassiveLevel(JsonObject character, int index) =>
        character["ispassiveskill"] is JsonArray levels && levels.Count > index ? GameJson.Number(levels[index]) : 0;

    private void SchoolChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loading)
            ShowSkillRows();
    }

    private void SkillSelected(object sender, SelectionChangedEventArgs e)
    {
        var row = SkillsTable.SelectedItem as SkillRow;
        SkillTitle.Text = row is null ? "技能等级（0–3）" : $"{row.Name} · 等级（0–3）";
        SkillLevelBox.Text = row?.Level.ToString(CultureInfo.InvariantCulture) ?? "";
    }

    private void SetSkillLevel(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (Character is not JsonObject character || SchoolBox.SelectedItem is not SkillSchool school ||
            SkillsTable.SelectedItem is not SkillRow selected)
            return;
        int level = Input(SkillLevelBox, "技能等级", 0, GameRules.Int("skill","activeMax"));
        SkillData.SetSchool(character, school, code: selected.Code, singleLevel: level);
        ShowSkillRows();
        Report($"{selected.Name}已设为 {level} 级，等待保存。");
    });

    private void SetSchoolLevel(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (Character is not JsonObject character || SchoolBox.SelectedItem is not SkillSchool school)
            return;
        SkillData.SetSchool(character, school, schoolLevel: Input(SchoolLevelBox, "心法等级", 0, GameRules.Int("skill","schoolMax")));
        ShowSkillRows();
        Report($"{school.Name}已更新，等待保存。");
    });

    private void MaxSchoolSkills(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (Character is not JsonObject character || SchoolBox.SelectedItem is not SkillSchool school)
            return;
        SkillData.SetSchool(character, school, all: GameRules.Int("skill","activeMax"));
        ShowSkillRows();
        Report($"{school.Name}的五项主动技能已设为 3 级，等待保存。");
    });

    private void MaxAllSkills(object sender, RoutedEventArgs e) => Guard(() => SetAllSkills(GameRules.Int("skill","activeMax"), GameRules.Int("skill","schoolMax")));

    private void SetAllSkills(int level, int schoolLevel)
    {
        if (Character is not JsonObject character || game is null)
            return;
        foreach (SkillSchool school in SkillData.Schools(game.HeroSkills, GameJson.Number(character["roleid"])))
            SkillData.SetSchool(character, school, schoolLevel, level);
        SkillData.SetPassive(character, Enumerable.Repeat(schoolLevel, SkillData.PassiveNames.Length).ToArray());
        ShowSkillRows();
        ShowPassiveRows(refresh: true);
        Report(level == 0 ? "技能、心法和被动已清空，等待保存。" : "主动技能、心法和被动均已学满，等待保存。");
    }

    private void ClearSkills(object sender, RoutedEventArgs e)
    {
        if (Confirm("清空技能", "将当前角色的主动技能、心法和被动设为未学习？", "清空技能", caution: true))
            Guard(() => SetAllSkills(0, 0));
    }

    private void ApplyPassive(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (Character is not JsonObject character || !ReferenceEquals(character, passiveOwner))
            return;
        var levels = passiveRows.Select(row => row.DraftLevel ??
            throw new InvalidDataException($"请选择「{row.Name}」的等级。")).ToArray();
        SkillData.SetPassive(character, levels);
        ShowPassiveRows(refresh: true);
        Report("被动技能已更新，等待保存。");
    });

    private void SetSkillSoul(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (Character is not JsonObject character)
            return;
        character["lhValue"] = Input(SkillSoulBox, "灵魂", 0);
        ShowCharacter();
        Report("灵魂已更新，等待保存。");
    });
}
