using System.Net.Http;
using System.Windows;

namespace GmStudio;

public partial class MainWindow
{
    private EditableFile? pendingSaveSync,pendingVipSync;
    private async void SaveClick(object sender,RoutedEventArgs e) => await SaveAsync();

    private async Task SaveAsync()
    {
        if(game is null || busy || save is null && vip is null) return;
        bool localSaved=false,checkingServer=false;
        string requestId=Guid.NewGuid().ToString("N");
        busy=true;IsEnabled=false;SaveButton.Content="保存中…";
        try
        {
            // Include the current file even if already clean, so Save can retry a failed push.
            var (sendSave,sendVip)=SaveDrafts(true,requestId);
            localSaved=true;
            if(sendSave) pendingSaveSync=save;
            if(sendVip) pendingVipSync=vip;
            sendSave|=save is not null && ReferenceEquals(pendingSaveSync,save);
            sendVip|=vip is not null && ReferenceEquals(pendingVipSync,vip);
            DiagnosticsLog.Write(game.Root,"save.local.completed",requestId,new {slot=slot?.Number,save=sendSave,vip=sendVip});
            Report("已保存到本地，正在确认游戏状态…");
            checkingServer=true;
            await game.CheckServerAsync(requestId);
            checkingServer=false;
            if(sendSave) save!.CheckCurrent();
            if(sendVip) vip!.CheckCurrent();
            var files=new List<EditableFile>();if(sendSave)files.Add(save!);if(sendVip)files.Add(vip!);
            var state=files.Count>0?await game.NotifySavedAsync(files,requestId):await game.RuntimeStatusAsync(requestId);
            if(sendSave)pendingSaveSync=null;if(sendVip)pendingVipSync=null;
            ShowRuntimeState(state);
            DiagnosticsLog.Write(game.Root,"save.confirmed",requestId,new {gameRunning=state.GameRunning,service=state.ServiceSession,game=state.GameSession});
            Report(state.GameRunning switch
            {
                false=>"已保存到本地。游戏未运行，下次启动后读取存档即可。",
                null=>"已保存到本地，服务已连接；游戏运行状态暂时无法确认。",
                true when sendSave=>"已保存并同步到游戏服务。游戏正在运行，请重新读取当前存档。",
                _ when sendVip=>"已保存。游戏正在运行，会员与点券将在下次轮询时刷新。",
                _=>"已保存到本地。游戏正在运行。"
            });
        }
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException)
        {
            Report(checkingServer
                ? "已保存到本地。未连接到游戏服务，下次启动游戏后读取存档即可。"
                : "已保存到本地，但服务确认未完成。请稍后再次点击「保存」，或重新启动游戏后读档。",
                error:!checkingServer);
            DiagnosticsLog.Write(game.Root,"save.service.unavailable",requestId,new {phase=checkingServer?"detection":"confirmation",error=ex.GetType().Name},"WARNING");
        }
        catch(Exception ex) when(Handled(ex))
        {
            if(System.IO.File.Exists(StorageTransaction.Journal(game.Root)))ShowError("保存中断，恢复事务已保留。请刷新或重新启动以完成恢复。"+ex.Message);
            else if(localSaved) Report("已保存到本地，自动推送已停止。"+ex.Message,error:true);
            else ShowError(ex.Message);
            DiagnosticsLog.Write(game.Root,"save.failed",requestId,new {localSaved,error=ex.GetType().Name,message=ex.Message},"ERROR");
        }
        finally
        {
            busy=false;IsEnabled=true;SaveButton.Content="保存";UpdateStatus();
        }
    }
}
