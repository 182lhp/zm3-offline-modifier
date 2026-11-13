using System.IO;
namespace GmStudio;

internal static class GamePaths
{
    internal static string Within(string root,string relative)
    {
        string full=Path.GetFullPath(root),path=Path.GetFullPath(Path.Combine(full,relative.Replace('/',Path.DirectorySeparatorChar)));
        if(!path.StartsWith(Path.TrimEndingDirectorySeparator(full)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("文件路径超出游戏目录。");
        for(string? at=path;at is not null && !string.Equals(at,full,StringComparison.OrdinalIgnoreCase);at=Path.GetDirectoryName(at))
            if((File.Exists(at)||Directory.Exists(at))&&(File.GetAttributes(at)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("资源路径不能经过链接目录。");
        return path;
    }
    internal static string Select(string root,string current,string legacy)
    {
        string a=Within(root,current),b=Within(root,legacy),marker=Within(root,"schema/layout.json");
        int version=File.Exists(marker)?GameJson.Number(GameJson.Read(marker)["layoutVersion"]):1;
        if(version is not (1 or 2))throw new InvalidDataException("不支持的文件布局版本，请保留现有文件。");
        if(version==2)
        {
            if(!File.Exists(a))throw new InvalidDataException("新版数据文件缺失："+current+"；请恢复原文件，不要重建领取记录。");
            return a;
        }
        if(File.Exists(a)&&File.Exists(b)&&!File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b)))throw new InvalidDataException("发现两份不同的数据，迁移尚未完成，请先核对："+current);
        return File.Exists(a)?a:b;
    }
    internal static string GiftState(string root)=>Select(root,"saves/gifts/local_gifts.json","tools/local_gifts.json");
}
