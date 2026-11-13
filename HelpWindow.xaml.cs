using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GmStudio;

public partial class HelpWindow : Window
{
    private sealed record ManualSource(string Title, string Path);
    private readonly string? gameRoot;
    private readonly List<ManualSource> sources = [];
    internal string ManualText { get; private set; } = "";

    internal HelpWindow(string? gameRoot)
    {
        this.gameRoot = gameRoot is null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameRoot));
        InitializeComponent();
        Version? version = typeof(HelpWindow).Assembly.GetName().Version;
        ReleaseLabel.Text = $"修改器 {version?.ToString(3)}  ·  启动修改器.exe";
        LoadSources();
    }

    private void LoadSources()
    {
        if (gameRoot is not null)
        {
            string main = Path.Combine(gameRoot, "使用说明.md");
            string legacy = Path.Combine(gameRoot, "使用说明.txt");
            if (File.Exists(main)) sources.Add(new("使用说明", main));
            else if (File.Exists(legacy)) sources.Add(new("使用说明（旧版文本）", legacy));
            foreach (string name in new[] { "修改器使用手册", "存档与备份", "常见问题", "版本与文件说明" })
            {
                string path = Path.Combine(gameRoot, "资料", name + ".md");
                if (File.Exists(path)) sources.Add(new(name, path));
            }
        }
        ManualSourceBox.ItemsSource = sources;
        ManualSourceBox.IsEnabled = sources.Count > 0;
        if (sources.Count > 0) ManualSourceBox.SelectedIndex = 0;
        else
        {
            ManualLocation.Text = gameRoot is null ? "尚未选择游戏目录" : "当前游戏目录中没有使用资料";
            ShowText(gameRoot is null
                ? "# 先选择游戏目录\n\n请先在修改器选择游戏目录，再打开说明书。操作步骤也可在「修改器指南」查看。"
                : "# 未找到使用资料\n\n请把使用说明.md和资料文件夹放回游戏根目录。操作步骤可在「修改器指南」查看。");
        }
    }

    private void ManualSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ManualSourceBox.SelectedItem is not ManualSource source) return;
        try
        {
            string text = File.ReadAllText(source.Path);
            ManualLocation.Text = gameRoot is null ? source.Title : Path.GetRelativePath(gameRoot, source.Path);
            ShowText(string.IsNullOrWhiteSpace(text) ? "# 暂无内容\n\n当前资料文件为空。" : text, source.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowText("# 无法读取资料\n\n请检查资料文件是否存在，以及当前文件夹的读取权限。");
        }
    }

    private void ShowText(string text, string? source = null)
    {
        ManualText = text;
        OriginalManual.Document = MarkdownGuide.Render(text, link => FollowLink(source, link));
        OriginalManual.ScrollToHome();
    }

    private void FollowLink(string? source, string link)
    {
        if (Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme is "https" or "http")
            {
                try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                { PromptDialog.Alert(this, "无法打开链接", "请检查默认浏览器设置后重试。"); }
            }
            return;
        }
        if (gameRoot is null || source is null) return;
        try
        {
            string relative = Uri.UnescapeDataString(link.Split('#')[0]);
            string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, relative));
            if (!path.StartsWith(gameRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase)) return;
            ManualSource? target = sources.FirstOrDefault(item => item.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (target is not null) ManualSourceBox.SelectedItem = target;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or UriFormatException) { }
    }
}
