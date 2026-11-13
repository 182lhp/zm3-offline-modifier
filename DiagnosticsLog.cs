using System.IO;
using System.Text;
using System.Text.Json;

namespace GmStudio;

internal static class DiagnosticsLog
{
    private static readonly string Session=Guid.NewGuid().ToString("N");
    internal static void Write(string root,string action,string request,object? details=null,string level="INFO")
    {
        try
        {
            string directory=Path.Combine(root,"logs"),path=Path.Combine(directory,"game-events.log");
            Directory.CreateDirectory(directory);
            using var held=StorageTransaction.AcquireFile(Path.Combine(directory,"game-events.lock"),200);
            if(File.Exists(path)&&new FileInfo(path).Length>=2*1024*1024)
                for(int n=3;n>=1;n--){string source=path+(n>1?"."+(n-1):"");if(File.Exists(source))File.Move(source,path+"."+n,true);}
            string message=details is null?"":JsonSerializer.Serialize(details);
            string line=$"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {level} service=- game=- req={request} component=modifier editor={Session} {action} {message}\n";
            using var output=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.ReadWrite);
            output.Write(Encoding.UTF8.GetBytes(line));output.Flush();
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            System.Diagnostics.Trace.WriteLine("Diagnostic log unavailable: "+error.GetType().Name);
        }
    }
}
