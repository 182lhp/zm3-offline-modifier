using System.IO;
using System.Text.Json;

namespace GmStudio;

internal static class AcquisitionGuide
{
    internal const string UnknownItem = "尚未核实获取地点。";
    internal const string UnknownPet = "尚未核实捕捉地点或获取方式。";

    private sealed record ItemSource(string[] Codes, string Text);
    private sealed record PetSource(string BaseName, string Text);
    private sealed record GuideData(ItemSource[] Items, Dictionary<string, PetSource> Pets);

    private static readonly GuideData Data = Read();
    private static readonly Dictionary<string, string> Items = Data.Items
        .SelectMany(source => source.Codes.Select(code => (Code: code, source.Text)))
        .GroupBy(source => source.Code, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => string.Join("\n", group.Select(source => source.Text).Distinct()),
            StringComparer.Ordinal);

    internal static string ForItem(string? code) => code is not null && Items.TryGetValue(code, out string? info)
        ? info : UnknownItem;

    internal static string ForPet(string code, string name)
    {
        if (!Data.Pets.TryGetValue(PetData.Family(code), out var info))
            return UnknownPet;
        return name == info.BaseName ? info.Text : $"进化形态 · 先获得{info.BaseName}。\n{info.Text}";
    }

    private static GuideData Read()
    {
        using Stream stream = typeof(AcquisitionGuide).Assembly.GetManifestResourceStream("GmStudio.AcquisitionGuide.json")!;
        return JsonSerializer.Deserialize<GuideData>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
