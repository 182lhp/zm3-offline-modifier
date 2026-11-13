#if QA
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GmStudio;

public partial class MainWindow
{
    private async Task VerifyAutomaticSave(string folder)
    {
        var originalGame=game!;
        using var endpoint=new AutomaticSaveService(originalGame.Saves);
        using var client=new HttpClient(endpoint) {BaseAddress=new Uri("http://127.0.0.1:43999/")};
        game=new GameFolder(originalGame.Root,client);
        int Backups()=>Directory.GetFiles(Path.Combine(game.Saves,".gm-backups"),"*.json").Length;
        void Change(string marker)=>save!.Draft["automatic_save_probe"]=marker;
        async Task Run(string mode)
        {
            endpoint.Mode=mode;endpoint.Requests.Clear();
            await SaveAsync();
            Require(!busy && IsEnabled && SaveButton.IsEnabled && Equals(SaveButton.Content,"保存"),"Save restores the action after every outcome.");
        }
        try
        {
            SelectPage(NavCatalog);
            int before=Backups();Change("offline");
            endpoint.BeforePing=()=>Require(GameJson.Text(GameJson.Read(save!.Path)["automatic_save_probe"])=="offline" && !Dirty,
                "The local file is saved before attempting network detection.");
            await Run("offline");endpoint.BeforePing=null;
            Require(endpoint.Requests.SequenceEqual(["/ping"]) && Backups()==before+1 && Status.Text.Contains("已保存到本地") &&
                Status.Text.Contains("未连接"),"Offline save succeeds once with an exact backup and no POST.");
            await Run("offline");Require(Backups()==before+1,"A clean repeat does not create duplicate backups.");
            Change("timeout");await Run("timeout");
            Require(!Dirty && Status.Text.Contains("已保存到本地") && endpoint.Requests.Count==1,"Detection timeout retains the local save.");

            Change("wrong-directory");await Run("wrong-directory");
            Require(!Dirty && endpoint.Requests.SequenceEqual(["/ping"]) && Status.Text.Contains("另一个游戏目录"),
                "A different installation never receives a POST, while the local save is kept.");
            Change("malformed");await Run("malformed");
            Require(!Dirty && endpoint.Requests.Count==1 && Status.Text.Contains("自动推送已停止"),"An invalid service response cannot trigger a push.");

            Change("both-online");vip!.Draft["lastMoney"]=GameJson.Number(vip.Draft["lastMoney"])+1;
            await Run("online");
            Require(endpoint.Requests.SequenceEqual(["/ping","/storage/notify"]) && endpoint.Files.Count==2 && !Dirty &&
                Status.Text.Contains("同步到游戏服务") && pendingSaveSync is null && pendingVipSync is null,
                "One local batch commits both files, followed by one read-only checksum notification.");
            await CaptureWhenReady(folder,"save-online.png");
            Change("no-game");await Run("no-game");
            Require(Status.Text.Contains("游戏未运行") && RuntimeStatusText.Text=="游戏未运行","A live service does not imply a running game.");
            Change("unknown-game");await Run("unknown-game");
            Require(Status.Text.Contains("无法确认") && RuntimeStatusText.Text=="游戏状态待确认","Process inspection failure is not reported as online.");

            SelectPage(NavVip);vip.Draft["lastMoney"]=GameJson.Number(vip.Draft["lastMoney"])+1;
            await Run("disconnect");
            Require(Status.Text.Contains("确认未完成") && !Dirty && ReferenceEquals(pendingVipSync,vip),
                "Disconnecting after detection preserves saved data and records an unfinished VIP sync.");
            SelectPage(NavCatalog);before=Backups();await Run("online");
            Require(endpoint.Files.Contains("saves/vip.json") && pendingVipSync is null && Backups()==before,
                "A clean retry after changing pages still sends the unsynchronized VIP file without another backup.");
            SelectPage(NavVip);await Run("online");
            Require(endpoint.Files.SequenceEqual(["saves/vip.json"]) && Status.Text.Contains("轮询"),"Clean membership save targets only VIP.");
            Require(OpenRawPage(),"Open raw JSON after membership.");await Run("online");
            Require(endpoint.Files.SequenceEqual([$"saves/slot{slot!.Number}.json"]),"Advanced JSON save targets the slot after membership.");
            ReturnFromRawButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));SelectPage(NavCatalog);

            Change("single-flight");endpoint.Gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            endpoint.Mode="online";endpoint.Requests.Clear();
            Task first=SaveAsync();
            Require(busy && !IsEnabled && Equals(SaveButton.Content,"保存中…"),"The save action has a busy state during detection.");
            await SaveAsync();Require(endpoint.Requests.Count==1,"A repeated action during save does not start another request.");
            endpoint.Gate.SetResult();await first;endpoint.Gate=null;

            string disk=File.ReadAllText(save!.Path);File.AppendAllText(save.Path,"\n");
            endpoint.Requests.Clear();Exception? promptFailure=null;
            _=Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,()=>
            {
                try {var prompt=OwnedWindows.OfType<PromptDialog>().Single();ClickDialogButton(prompt,"知道了");}
                catch(Exception ex) {promptFailure=ex;foreach(var window in OwnedWindows.OfType<Window>().ToArray()) window.Close();}
            });
            await SaveAsync();
            if(promptFailure is not null) throw promptFailure;
            Require(endpoint.Requests.Count==0 && File.ReadAllText(save.Path)==disk+"\n" && Status.Text.Contains("磁盘文件"),
                "A concurrent disk update blocks both overwrite and automatic push.");
            save=new EditableFile(save.Path,repairPets:true);ShowSlot();
            endpoint.Mode="offline";await SaveAsync();
            await CaptureWhenReady(folder,"save-offline.png");
            VerifyStorageRecovery(folder);
        }
        finally {game=originalGame;pendingSaveSync=null;pendingVipSync=null;}
    }

    private sealed class AutomaticSaveService(string saves) : HttpMessageHandler
    {
        internal string Mode {get;set;}="online";
        internal Action? BeforePing {get;set;}
        internal TaskCompletionSource? Gate {get;set;}
        internal List<string> Requests {get;}=[];
        internal List<string> Files {get;}=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            string route=request.RequestUri!.PathAndQuery;Requests.Add(route);
            if(route=="/ping")
            {
                Files.Clear();
                BeforePing?.Invoke();
                if(Gate is { } gate) await gate.Task;
                if(Mode=="offline") throw new HttpRequestException("Offline test service");
                if(Mode=="timeout") throw new TaskCanceledException("Detection timeout");
                string status=Mode=="malformed" ? "{" : new JsonObject { ["who"]="zm3",
                    ["saves"]=Mode=="wrong-directory" ? Path.Combine(saves,"another-copy") : saves}.ToJsonString();
                return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(status)};
            }
            if(Mode=="disconnect") throw new HttpRequestException("Disconnected after detection");
            Require(route=="/storage/notify","Save never writes slot or VIP data a second time.");
            Require(request.Headers.Contains("X-Zm3-Request-Id") && request.Headers.GetValues("X-Zm3-Client").Single()=="modifier","Request correlation is attached.");
            var payload=GameJson.Parse(await request.Content!.ReadAsStringAsync(token));
            foreach(var item in ((JsonArray)payload["files"]!).OfType<JsonObject>())
            {
                string name=GameJson.Text(item["path"]);Files.Add(name);
                string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(saves)!,name)))).ToLowerInvariant();
                Require(item.Count==2 && hash==GameJson.Text(item["sha256"]),"Notification contains only committed paths and checksums.");
            }
            return new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(new JsonObject {
                ["ok"]=true,["root"]=Path.GetDirectoryName(saves),["gameRunning"]=Mode=="unknown-game"?null:JsonValue.Create(Mode!="no-game"),
                ["serviceSession"]="service-control",["games"]=new JsonArray()}.ToJsonString())};
        }
    }
}
#endif
