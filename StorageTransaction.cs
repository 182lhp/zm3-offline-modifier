using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GmStudio;

internal sealed record StorageWrite(string Relative, byte[]? Before, byte[] After);

internal static class StorageTransaction
{
    private static readonly Regex Managed = new(@"^(saves/(slot[0-5]|vip)\.json|(?:tools|saves/gifts)/local_gifts\.json)$");
    internal static string Journal(string root) => Path.Combine(root,"tools","local_gifts.transaction.json");
    internal static IDisposable Acquire(string root) => AcquireFile(Path.Combine(root,"tools","local_gifts.lock"),4000);
    internal static IDisposable AcquireFile(string path,int timeout)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var file=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);
        if(file.Length==0){file.WriteByte(0);file.Flush(true);}
        long until=Environment.TickCount64+timeout;
        while(true)
        {
            try{file.Lock(0,1);return new HeldLock(file);}
            catch(IOException) when(Environment.TickCount64<until){Thread.Sleep(20);}
            catch{file.Dispose();throw;}
        }
    }
    private sealed class HeldLock(FileStream file):IDisposable
    {
        public void Dispose(){try{file.Unlock(0,1);}finally{file.Dispose();}}
    }
    internal static string? RootFor(string path)
    {
        string full=Path.GetFullPath(path),directory=Path.GetDirectoryName(full)!;
        if(Path.GetFileName(directory)=="gifts" && Path.GetFileName(Path.GetDirectoryName(directory))=="saves")directory=Path.GetDirectoryName(directory)!;
        if(Path.GetFileName(directory) is not ("saves" or "tools"))return null;
        string root=Path.GetDirectoryName(directory)!;
        return Managed.IsMatch(Path.GetRelativePath(root,full).Replace('\\','/'))?root:null;
    }
    internal static byte[]? ReadForEdit(string path)
    {
        string? root=RootFor(path);
        if(root is null)return Read(path);
        using var held=Acquire(root);RecoverLocked(root);return Read(path);
    }
    private static byte[]? Read(string path)=>File.Exists(path)?File.ReadAllBytes(path):null;
    private static bool Same(byte[]? a,byte[]? b)=>a is null?b is null:b is not null && a.AsSpan().SequenceEqual(b);
    private static string Resolve(string root,string relative)
    {
        if(!Managed.IsMatch(relative))throw new InvalidDataException("事务文件路径无效。");
        string path=relative is "tools/local_gifts.json" or "saves/gifts/local_gifts.json"?GamePaths.GiftState(root):GamePaths.Within(root,relative);
        if(!path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("事务文件超出游戏目录。");
        return path;
    }
    private static void Atomic(string path,byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write)){output.Write(data);output.Flush(true);}
            if(File.Exists(path))File.Move(temporary,path,true);else File.Move(temporary,path);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    internal static void RecoverLocked(string root,bool replay=true)
    {
        string journal=Journal(root);if(!File.Exists(journal))return;
        var transaction=GameJson.Read(journal);int version=GameJson.Number(transaction["version"],1);
        if(version is not (1 or 2))throw new InvalidDataException("不支持的存档事务版本，请保留事务文件。");
        var rows=transaction["files"]?.DeepClone() as JsonArray ?? throw new InvalidDataException("事务文件列表无效。");
        if(version==1)
        {
            foreach(var row in rows.OfType<JsonObject>())
                if(GameJson.Text(row["before"])==""&&Read(Resolve(root,GameJson.Text(row["path"]))) is null)row["before"]=null;
            var state=transaction["state"] as JsonObject ?? throw new InvalidDataException("旧事务缺少领取记录。");
            byte[]? before=Read(Resolve(root,"tools/local_gifts.json"));
            rows.Add(new JsonObject{["path"]="tools/local_gifts.json",["before"]=before is null?null:Convert.ToHexString(before),["after"]=state.ToJsonString()});
        }
        if(rows.Count is <1 or >8)throw new InvalidDataException("事务文件数量无效。");
        var writes=new List<StorageWrite>();var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in rows.OfType<JsonObject>())
        {
            string name=GameJson.Text(row["path"]),after=GameJson.Text(row["after"]),hex=GameJson.Text(row["before"]);
            Resolve(root,name);
            if(!names.Add(Resolve(root,name).ToUpperInvariant())||after.Length>8*1024*1024||hex.Length>16*1024*1024)throw new InvalidDataException("事务记录无效。");
            _=GameJson.Parse(after.TrimStart('\uFEFF'));
            writes.Add(new(name,row["before"] is null?null:Convert.FromHexString(hex),Encoding.UTF8.GetBytes(after)));
        }
        if(writes.Count!=rows.Count)throw new InvalidDataException("事务记录格式无效。");
        foreach(var item in writes)
        {
            byte[]? current=Read(Resolve(root,item.Relative));
            if(!Same(current,item.Before)&&!Same(current,item.After))throw new InvalidOperationException("事务恢复发现外部修改："+item.Relative+"。请保留备份并刷新。");
        }
#if QA
        int index=0;
#endif
        foreach(var item in writes)
        {
            string path=Resolve(root,item.Relative);if(!Same(Read(path),item.After))Atomic(path,item.After);
#if QA
            FailureAfterWrite?.Invoke(++index);
#endif
        }
        File.Delete(journal);
        DiagnosticsLog.Write(root,replay?"storage.recovered":"storage.committed",GameJson.Text(transaction["id"]),new {origin=GameJson.Text(transaction["origin"]),files=writes.Count});
    }
#if QA
    internal static Action<int>? FailureAfterWrite;
#endif
    internal static Dictionary<string,string> CommitLocked(string root,IReadOnlyList<StorageWrite> writes,string origin,string id)
    {
        RecoverLocked(root);
        if(writes.Select(item=>Resolve(root,item.Relative).ToUpperInvariant()).Distinct().Count()!=writes.Count)throw new InvalidDataException("事务文件重复。");
        foreach(var item in writes)
        {
            _=GameJson.Parse(Encoding.UTF8.GetString(item.After).TrimStart('\uFEFF'));
            if(!Same(Read(Resolve(root,item.Relative)),item.Before))throw new InvalidOperationException("磁盘文件已被游戏或其他编辑器更新。请先刷新存档，再重新编辑。");
        }
        var changed=writes.Where(item=>!Same(item.Before,item.After)).ToArray();var backups=new Dictionary<string,string>();
        if(changed.Length==0)return backups;
        foreach(var item in changed)
        {
            if(item.Before is null)continue;
            string folder=item.Relative.StartsWith("saves/",StringComparison.Ordinal)?".gm-backups":"backups";
            string backup=Path.Combine(root,"saves",folder,$"{Path.GetFileNameWithoutExtension(item.Relative)}-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{id}.json");
            Atomic(backup,item.Before);backups[item.Relative]=backup;
        }
        var rows=new JsonArray(changed.Select(item=>(JsonNode)new JsonObject{["path"]=item.Relative,["before"]=item.Before is null?null:Convert.ToHexString(item.Before),["after"]=Encoding.UTF8.GetString(item.After)}).ToArray());
        var transaction=new JsonObject{["version"]=2,["id"]=id,["origin"]=origin,["files"]=rows};
        Atomic(Journal(root),Encoding.UTF8.GetBytes(transaction.ToJsonString()));
        DiagnosticsLog.Write(root,"storage.prepared",id,new {origin,files=changed.Length});
        RecoverLocked(root,false);return backups;
    }
    internal static int SaveFiles(IReadOnlyList<EditableFile> files,string id)
    {
        if(files.Count==0)return 0;
        string root=RootFor(files[0].Path)??throw new InvalidDataException("存档不在游戏目录中。");
        if(files.Any(file=>!string.Equals(root,RootFor(file.Path),StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("不能跨游戏目录提交事务。");
        using var held=Acquire(root);RecoverLocked(root);
        var prepared=files.Select(file=>(File:file,Write:file.PrepareWrite(root))).ToArray();
        var backups=CommitLocked(root,prepared.Select(p=>p.Write).ToArray(),"modifier",id);
        foreach(var item in prepared)item.File.AcceptPersisted(item.Write.After);
        return backups.Count;
    }
}
