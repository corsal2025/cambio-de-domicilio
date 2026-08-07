' Launches abrir-dashboard-dev.ps1 with zero visible window (not even the brief
' black-frame flash that "powershell -WindowStyle Hidden" still shows for ~1-2s
' before it hides, since WScript.Shell.Run with style 0 never draws a window at all).
Set shell = CreateObject("WScript.Shell")
scriptDir = CreateObject("Scripting.FileSystemObject").GetParentFolderName(WScript.ScriptFullName)
psScript = scriptDir & "\abrir-dashboard-dev.ps1"
shell.Run "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File """ & psScript & """", 0, False
