param(
 [string]$SourcePath = (Join-Path $PSScriptRoot 'compile_patch.py'),
 [string]$WorkingDirectory = $PSScriptRoot,
 [string]$RuntimeDirectory = $WorkingDirectory
)
$ErrorActionPreference = 'Stop'
$taskDir = $RuntimeDirectory
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Python38Compiler {
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void SetPath(IntPtr path);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Initialize(int signals);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Run([MarshalAs(UnmanagedType.LPUTF8Str)] string code);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int FinalizePython();
 public static int Compile(string root, string source) {
  var dll = NativeLibrary.Load(System.IO.Path.Combine(root, "python38.dll"));
  Marshal.WriteInt32(NativeLibrary.GetExport(dll,"Py_NoSiteFlag"), 1);
  Marshal.WriteInt32(NativeLibrary.GetExport(dll,"Py_IgnoreEnvironmentFlag"), 1);
  var path = Marshal.StringToHGlobalUni(System.IO.Path.Combine(root,"base_library.zip") + ";" + root);
  Marshal.GetDelegateForFunctionPointer<SetPath>(NativeLibrary.GetExport(dll,"Py_SetPath"))(path);
  Marshal.GetDelegateForFunctionPointer<Initialize>(NativeLibrary.GetExport(dll,"Py_InitializeEx"))(0);
  int result = Marshal.GetDelegateForFunctionPointer<Run>(NativeLibrary.GetExport(dll,"PyRun_SimpleString"))(source);
  Marshal.GetDelegateForFunctionPointer<FinalizePython>(NativeLibrary.GetExport(dll,"Py_FinalizeEx"))();
  Marshal.FreeHGlobal(path);
  return result;
 }
}
'@
Push-Location $WorkingDirectory
$oldWorkingDirectory = [Environment]::CurrentDirectory
[Environment]::CurrentDirectory = $WorkingDirectory
try {
 $code = Get-Content -LiteralPath $SourcePath -Raw -Encoding UTF8
 $result = [Python38Compiler]::Compile($taskDir, $code)
 if ($result -ne 0) { throw "Python 3.8 compiler failed: $result" }
} finally { [Environment]::CurrentDirectory = $oldWorkingDirectory; Pop-Location }
