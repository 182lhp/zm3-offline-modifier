using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace GmStudio;

internal sealed record RuntimeServiceState(bool? GameRunning,string ServiceSession,string GameSession);

internal sealed partial class GameFolder
{
    private static HttpRequestMessage ServiceRequest(HttpMethod method,string route,string? requestId)
    {
        var request=new HttpRequestMessage(method,route);
        request.Headers.Add("X-Zm3-Client","modifier");
        request.Headers.Add("X-Zm3-Request-Id",requestId??Guid.NewGuid().ToString("N"));
        return request;
    }
    private RuntimeServiceState ReadRuntime(JsonObject data)
    {
        string root=GameJson.Text(data["root"]);
        if(string.IsNullOrWhiteSpace(root)||!string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)),Path.TrimEndingDirectorySeparator(Root),StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("本地服务连接的是另一个游戏目录，已停止同步。");
        bool? running=data["gameRunning"] is JsonValue value&&value.TryGetValue<bool>(out bool result)?result:null;
        return new(running,GameJson.Text(data["serviceSession"]),GameJson.Text((data["games"] as JsonArray)?.FirstOrDefault()?["sessionId"]));
    }
    internal async Task<RuntimeServiceState> RuntimeStatusAsync(string? requestId=null)
    {
        using var request=ServiceRequest(HttpMethod.Get,"runtime/status",requestId);
        using var response=await http.SendAsync(request);
        if(response.StatusCode==HttpStatusCode.NotFound)
        {
            await CheckServerAsync(requestId);return new(null,"","");
        }
        response.EnsureSuccessStatusCode();return ReadRuntime(GameJson.Parse(await response.Content.ReadAsStringAsync()));
    }
    internal async Task<RuntimeServiceState> NotifySavedAsync(IReadOnlyList<EditableFile> files,string requestId)
    {
        var rows=new JsonArray();
        foreach(var file in files)
        {
            string path=Path.GetRelativePath(Root,file.Path).Replace('\\','/');
            rows.Add(new JsonObject{["path"]=path,["sha256"]=Convert.ToHexString(SHA256.HashData(file.PersistedBytes())).ToLowerInvariant()});
        }
        using var request=ServiceRequest(HttpMethod.Post,"storage/notify",requestId);
        request.Content=new StringContent(new JsonObject{["files"]=rows}.ToJsonString(),Encoding.UTF8,"application/json");
        using var response=await http.SendAsync(request);
        string text=await response.Content.ReadAsStringAsync();
        if(response.StatusCode==HttpStatusCode.Conflict)
            throw new InvalidOperationException(GameJson.Text(GameJson.Parse(text)["error"]));
        response.EnsureSuccessStatusCode();var data=GameJson.Parse(text);
        if(data["ok"]?.GetValue<bool>()!=true)throw new InvalidDataException("服务未确认本次保存，请稍后重试。");
        foreach(var file in files)file.CheckCurrent();
        return ReadRuntime(data);
    }
}
