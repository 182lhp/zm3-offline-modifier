using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace GmStudio;

internal sealed record FontSelection(string Chinese, string Latin)
{
    internal static readonly FontSelection Default = new("Microsoft YaHei UI", "Segoe UI");
}

internal sealed record FontOption(string Id, string Label);

internal sealed class FontPreferences
{
    private static readonly XmlLanguage English = XmlLanguage.GetLanguage("en-US");
    private static readonly XmlLanguage Chinese = XmlLanguage.GetLanguage("zh-CN");
    private static readonly Lazy<(FontOption[] Chinese, FontOption[] Latin)> Options = new(InstalledOptions);

    internal static IReadOnlyList<FontOption> ChineseOptions => Options.Value.Chinese;
    internal static IReadOnlyList<FontOption> LatinOptions => Options.Value.Latin;

    internal string FilePath { get; }
    internal FontSelection Current { get; private set; }

    internal FontPreferences(string path)
    {
        FilePath = path;
        Current = Read();
    }

    private FontSelection Read()
    {
        var saved = UserPreferencesFile.Read<FontSelection>(FilePath);
        return saved is null ? FontSelection.Default : new FontSelection(
            ChineseOptions.FirstOrDefault(font => font.Id == saved.Chinese)?.Id ?? FontSelection.Default.Chinese,
            LatinOptions.FirstOrDefault(font => font.Id == saved.Latin)?.Id ?? FontSelection.Default.Latin);
    }

    internal void Save(FontSelection choice)
    {
        UserPreferencesFile.Write(FilePath, choice);
        Current = choice;
    }

    internal static FontFamily Compose(FontSelection choice)
    {
        var metrics = new FontFamily(FontSelection.Default.Chinese);
        var font = new FontFamily { Baseline = metrics.Baseline, LineSpacing = metrics.LineSpacing };
        font.FamilyNames.Add(English, "GM Workbench Interface");
        font.FamilyMaps.Add(new FontFamilyMap
        {
            Unicode = "0030-0039,0041-005A,0061-007A",
            Target = $"{choice.Latin}, Segoe UI"
        });
        font.FamilyMaps.Add(new FontFamilyMap
        {
            Target = $"{choice.Chinese}, Microsoft YaHei UI, Microsoft YaHei, Global User Interface"
        });
        return font;
    }

    private static (FontOption[] Chinese, FontOption[] Latin) InstalledOptions()
    {
        const string chineseSample = "简体中文修改器装备宠物等级";
        const string latinSample = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var chinese = new List<FontOption> { new(FontSelection.Default.Chinese, "微软雅黑 UI（默认）") };
        var latin = new List<FontOption> { new(FontSelection.Default.Latin, "Segoe UI（默认）") };
        var families = Fonts.SystemFontFamilies
            .Select(family => (Family: family, Label: family.FamilyNames.TryGetValue(Chinese, out string? cn) ? cn
                : family.FamilyNames.TryGetValue(English, out string? en) ? en : family.Source))
            .OrderBy(entry => entry.Label, StringComparer.CurrentCultureIgnoreCase);
        foreach (var (family, label) in families)
        {
            var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            if (!typeface.TryGetGlyphTypeface(out var glyph))
                continue;
            if (family.Source != FontSelection.Default.Chinese && chineseSample.All(ch => glyph.CharacterToGlyphMap.ContainsKey(ch)))
                chinese.Add(new FontOption(family.Source, label));
            if (family.Source != FontSelection.Default.Latin && latinSample.All(ch => glyph.CharacterToGlyphMap.ContainsKey(ch)))
                latin.Add(new FontOption(family.Source, label));
        }
        return (chinese.ToArray(), latin.ToArray());
    }
}
