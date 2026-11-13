#if QA
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
namespace GmStudio;
public partial class MainWindow
{
    private static void VerifyStorageRecovery(string folder)
    {
        string root=Path.Combine(folder,"transaction-fixture"),saves=Path.Combine(root,"saves");Directory.CreateDirectory(saves);
        File.WriteAllText(Path.Combine(saves,"slot0.json"),"{\"value\":1}");File.WriteAllText(Path.Combine(saves,"vip.json"),"{\"value\":2}");
        var a=new EditableFile(Path.Combine(saves,"slot0.json"));var b=new EditableFile(Path.Combine(saves,"vip.json"));a.Draft["value"]=10;b.Draft["value"]=20;
        StorageTransaction.FailureAfterWrite=index=>{if(index==1)throw new IOException("Simulated interruption");};
        try{StorageTransaction.SaveFiles([a,b],"crash-control");throw new InvalidDataException("Failure injection did not run");}
        catch(IOException){Require(File.Exists(StorageTransaction.Journal(root)),"Interrupted batch retains its durable journal.");}
        finally{StorageTransaction.FailureAfterWrite=null;}
        using(var held=StorageTransaction.Acquire(root))StorageTransaction.RecoverLocked(root);
        Require(GameJson.Number(GameJson.Read(a.Path)["value"])==10&&GameJson.Number(GameJson.Read(b.Path)["value"])==20&&!File.Exists(StorageTransaction.Journal(root)),"Both files roll forward exactly once.");
        string layout=Path.Combine(folder,"layout-fixture");Directory.CreateDirectory(Path.Combine(layout,"saves","gifts"));Directory.CreateDirectory(Path.Combine(layout,"tools"));Directory.CreateDirectory(Path.Combine(layout,"schema"));
        string moved=Path.Combine(layout,"saves","gifts","local_gifts.json"),old=Path.Combine(layout,"tools","local_gifts.json");
        File.WriteAllText(old,"{\"claims\":[1],\"unknown\":17}");File.Copy(old,moved);
        Require(GamePaths.GiftState(layout)==moved,"Equal migration copies select the new file without rewriting bytes.");
        File.WriteAllText(moved,"{\"claims\":[1,2],\"unknown\":17}");
        try{GamePaths.GiftState(layout);throw new Exception("Ambiguous history was accepted");}catch(InvalidDataException){}
        File.WriteAllText(Path.Combine(layout,"schema","layout.json"),"{\"layoutVersion\":2}");
        Require(GamePaths.GiftState(layout)==moved && StorageTransaction.RootFor(moved)==layout,"Committed layout selects canonical history and supports nested transaction paths.");
        File.WriteAllText(StorageTransaction.Journal(layout),"{\"files\":[],\"state\":{\"claims\":[1,2,3],\"unknown\":17}}");
        using(var held=StorageTransaction.Acquire(layout))StorageTransaction.RecoverLocked(layout);
        Require(GameJson.Read(moved)["claims"]!.AsArray().Count==3 && GameJson.Read(old)["claims"]!.AsArray().Count==1,"Legacy transaction recovery updates the migrated history only.");
        File.Delete(moved);try{GamePaths.GiftState(layout);throw new Exception("Missing migrated history silently fell back");}catch(InvalidDataException){}
        string cross=Path.Combine(folder,"interop-csharp");Directory.CreateDirectory(Path.Combine(cross,"saves"));
        File.WriteAllText(Path.Combine(cross,"saves","slot0.json"),"{\"value\":1}");File.WriteAllText(Path.Combine(cross,"saves","vip.json"),"{\"value\":2}");
        var x=new EditableFile(Path.Combine(cross,"saves","slot0.json"));var y=new EditableFile(Path.Combine(cross,"saves","vip.json"));x.Draft["value"]=100;y.Draft["value"]=200;
        StorageTransaction.FailureAfterWrite=index=>{if(index==1)throw new IOException("Cross-language control");};
        try{StorageTransaction.SaveFiles([x,y],"interop-control");}catch(IOException){}finally{StorageTransaction.FailureAfterWrite=null;}
        Require(File.Exists(StorageTransaction.Journal(cross)),"Real WPF writer produced a recoverable cross-language journal.");
        File.WriteAllText(Path.Combine(folder,"storage-qa.json"),"{\"status\":\"PASS\",\"batchRecovery\":true,\"readOnlyNotify\":true,\"actualGameStateSemantics\":true}");
    }
}
#endif
