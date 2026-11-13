using System.IO;

namespace GmStudio;

internal sealed record SidebarDefinition(string Id, string Name, string? Glyph);
internal sealed record SidebarEntry(string Id, string Name);

internal sealed class SidebarPreferences
{
    internal const int NameLimit = 24;
    internal string FilePath { get; }
    internal IReadOnlyList<SidebarDefinition> Defaults { get; }
    internal IReadOnlyList<SidebarEntry> Current { get; private set; }

    internal SidebarPreferences(string path, IReadOnlyList<SidebarDefinition> defaults)
    {
        FilePath = path;
        Defaults = defaults;
        var saved = UserPreferencesFile.Read<SidebarEntry?[]>(path) ?? [];
        Current = Normalize(saved);
        if (saved.Any(entry => entry is not null && !Defaults.Any(item => item.Id == entry.Id)))
        {
            try { UserPreferencesFile.Write(path, Current); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    internal static bool IsNameValid(string name) =>
        name.Length is > 0 and <= NameLimit && !name.Any(char.IsControl);

    private SidebarEntry[] Normalize(IEnumerable<SidebarEntry?> saved)
    {
        var known = Defaults.ToDictionary(item => item.Id);
        var included = new HashSet<string>();
        var entries = new List<SidebarEntry>();
        foreach (var entry in saved)
        {
            if (entry?.Id is not string id || !known.TryGetValue(id, out var definition) || !included.Add(id))
                continue;
            string name = entry.Name?.Trim() ?? "";
            entries.Add(new SidebarEntry(id, IsNameValid(name) ? name : definition.Name));
        }
        entries.AddRange(Defaults.Where(item => !included.Contains(item.Id)).Select(item => new SidebarEntry(item.Id, item.Name)));
        return entries.ToArray();
    }

    internal void Save(IEnumerable<SidebarEntry> entries)
    {
        var layout = Normalize(entries);
        UserPreferencesFile.Write(FilePath, layout);
        Current = layout;
    }
}
