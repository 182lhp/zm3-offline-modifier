#define UNICODE
#define _UNICODE
#define _WIN32_WINNT 0x0A00
#include <windows.h>
#include <shellapi.h>
#include <stdio.h>
#include "geometry.h"
static DWORD wanted;static HWND target;
static BOOL CALLBACK locate(HWND hwnd,LPARAM ignored){
 DWORD pid=0;GetWindowThreadProcessId(hwnd,&pid);
 LONG_PTR style=GetWindowLongPtrW(hwnd,GWL_STYLE);
 if(pid==wanted&&IsWindowVisible(hwnd)&&!GetWindow(hwnd,GW_OWNER)&&(style&WS_CAPTION)==WS_CAPTION){target=hwnd;return FALSE;}return TRUE;
}
static int install(DWORD pid){
 wanted=pid;
 for(int i=0;i<200;i++){EnumWindows(locate,0);if(target)break;Sleep(50);}
 if(!target)return 2;
 WCHAR dll[MAX_PATH];GetModuleFileNameW(NULL,dll,MAX_PATH);WCHAR *slash=wcsrchr(dll,L'\\');if(!slash)return 3;
 wcscpy(slash+1,L"window-aspect.dll");
 HMODULE module=LoadLibraryW(dll);if(!module)return 4;
 HOOKPROC proc=(HOOKPROC)GetProcAddress(module,"AspectHook");if(!proc){FreeLibrary(module);return 5;}
 DWORD thread=GetWindowThreadProcessId(target,NULL);HHOOK hook=SetWindowsHookExW(WH_CALLWNDPROC,proc,module,thread);
 if(!hook){FreeLibrary(module);return 6;}
 DWORD_PTR result=0;
 SendMessageTimeoutW(target,RegisterWindowMessageW(L"ZM3.GameAspect.Attach.v1"),0x5A4D3301,0,SMTO_ABORTIFHUNG,5000,&result);
 int status=GetPropW(target,L"ZM3.GameAspect.Ready.v1")?0:7;
 UnhookWindowsHookEx(hook);FreeLibrary(module);
 return status;
}
#ifdef ASPECT_QA
static int selftest(void){
 for(int fw=0;fw<=40;fw+=8)for(int fh=24;fh<=80;fh+=8)for(int edge=1;edge<=8;edge++){
  RECT r={100,100,1100,800};LONG left=r.left,top=r.top,right=r.right,bottom=r.bottom;
  sizing_rect(&r,edge,fw,fh,940,590,1800,960);
  int w=r.right-r.left-fw,h=r.bottom-r.top-fh;
  if(abs(w*GAME_H-h*GAME_W)>GAME_W/2||w>1800||h>960)return 20;
  if((edge==WMSZ_LEFT||edge==WMSZ_TOPLEFT||edge==WMSZ_BOTTOMLEFT)?r.right!=right:r.left!=left)return 21;
  if((edge==WMSZ_TOP||edge==WMSZ_TOPLEFT||edge==WMSZ_TOPRIGHT)?r.bottom!=bottom:r.top!=top)return 22;
 }
 return 0;
}
static int verify(void){
 int sizes[][2]={{1200,590},{940,720},{846,531},{1400,900}};
 for(int i=0;i<4;i++){
  SetWindowPos(target,NULL,100,100,sizes[i][0],sizes[i][1],SWP_NOZORDER|SWP_NOACTIVATE);Sleep(80);
  RECT client;GetClientRect(target,&client);
  if(abs(client.right*GAME_H-client.bottom*GAME_W)>GAME_W)return 30+i;
 }
 ShowWindow(target,SW_MAXIMIZE);Sleep(100);
 RECT client;GetClientRect(target,&client);
 if(abs(client.right*GAME_H-client.bottom*GAME_W)>GAME_W)return 40;
 ShowWindow(target,SW_RESTORE);Sleep(100);GetClientRect(target,&client);
 if(abs(client.right*GAME_H-client.bottom*GAME_W)>GAME_W)return 41;
 return 0;
}
#endif
int WINAPI wWinMain(HINSTANCE instance,HINSTANCE prior,LPWSTR command,int show){
 SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
 int count=0;WCHAR **args=CommandLineToArgvW(GetCommandLineW(),&count);if(!args)return 1;
#ifdef ASPECT_QA
 if(count>1&&!wcscmp(args[1],L"--self-test")){LocalFree(args);return selftest();}
#endif
 if(count<2){LocalFree(args);return 1;}
 int status=install(wcstoul(args[1],NULL,10));
#ifdef ASPECT_QA
 if(!status&&count>2&&!wcscmp(args[2],L"--verify"))status=verify();
#endif
 if(count>3){FILE *log=_wfopen(args[3],L"a, ccs=UTF-8");if(log){fwprintf(log,L"window.aspect pid=%lu status=%d\n",wanted,status);fclose(log);}}
 LocalFree(args);return status;
}
