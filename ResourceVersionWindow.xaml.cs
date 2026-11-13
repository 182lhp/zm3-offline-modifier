using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
namespace GmStudio;

internal sealed record ResourceEntry(string Path,long Size,string Hash);
internal sealed record ResourceRelease(string Version,string BuildId,IReadOnlyList<ResourceEntry> Files)
{
    internal static ResourceRelease Read(string root)
    {
        var manifest=GameJson.Read(GamePaths.Within(root,"schema/resource_manifest.json"));
        if(GameJson.Number(manifest["format"])!=1 || manifest["files"] is not JsonArray rows)throw new InvalidDataException("资源版本清单格式无效。");
        var files=new List<ResourceEntry>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var row in rows)
        {
            string path=GameJson.Text(row?["path"]),hash=GameJson.Text(row?["sha256"]);
            if(!Regex.IsMatch(path,@"^(?:(?:game|icons|runtime|schema|tools|修改器)/.+|启动游戏\.exe)$")||path.Contains('\\')||path.Split('/').Contains("..")||!seen.Add(path)||!Regex.IsMatch(hash,"^[a-f0-9]{64}$")||path is "tools/local_gifts.json" or "schema/ref_from_game.xml")throw new InvalidDataException("资源版本清单含有无效路径或摘要。");
            GamePaths.Within(root,path);long size=row?["size"]?.GetValue<long>()??-1;if(size<0)throw new InvalidDataException("资源大小无效。");
            files.Add(new(path,size,hash));
        }
        string version=GameJson.Text(manifest["version"]),build=GameJson.Text(manifest["buildId"]);
        string canonical=version+"\n"+string.Join("\n",files.OrderBy(row=>row.Path,StringComparer.Ordinal).Select(row=>$"{row.Path}\t{row.Size}\t{row.Hash}"))+"\n";
        if(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant()!=build)throw new InvalidDataException("资源版本指纹不一致。");
        return new(version,build,files);
    }
    internal IReadOnlyList<string> Verify(string root)
    {
        var errors=new List<string>();
        foreach(var row in Files)
        {
            try
            {
                string path=GamePaths.Within(root,row.Path);using var stream=File.OpenRead(path);
                if(stream.Length!=row.Size || Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()!=row.Hash)errors.Add(row.Path+"：内容与发布版本不同");
            }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException){errors.Add(row.Path+"：文件缺失或无法读取");}
        }
        return errors;
    }
}
public partial class ResourceVersionWindow:Window
{
    private readonly string root;
    private readonly ResourceRelease? release;
    internal ResourceVersionWindow(Window owner,string root)
    {
        InitializeComponent();Owner=owner;this.root=root;MaxHeight=SystemParameters.WorkArea.Height;
        try
        {
            release=ResourceRelease.Read(root);VersionText.Text="版本 "+release.Version;BuildText.Text="版本指纹："+release.BuildId;CountText.Text=$"{release.Files.Count:N0} 个程序与资源文件 · 规则 {GameRules.Revision}\n规则指纹：{GameRules.ExpectedSha256}";
        }
        catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {VersionText.Text="暂无可用版本清单";ResultText.Text=error.Message;VerifyButton.IsEnabled=false;}
    }
    private async void VerifyClick(object sender,RoutedEventArgs e)
    {
        if(release is null)return;VerifyButton.IsEnabled=false;ResultText.Text="正在校验…";
        try
        {
            var errors=await Task.Run(()=>release.Verify(root));
            ResultText.Text=errors.Count==0?$"校验通过：{release.Files.Count:N0} 个文件完整。":$"发现 {errors.Count:N0} 项差异：\n\n"+string.Join("\n",errors.Take(100))+(errors.Count>100?"\n其余差异请查看日志。":"");
            DiagnosticsLog.Write(root,"resources.verified",Guid.NewGuid().ToString("N"),new {build=release.BuildId,checkedFiles=release.Files.Count,errors=errors.ToArray()});
        }
        finally{VerifyButton.IsEnabled=true;}
    }
    private void CloseClick(object sender,RoutedEventArgs e)=>Close();
}
