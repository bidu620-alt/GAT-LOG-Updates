from pathlib import Path

ui=Path('server-dotnet/GatLogServer/MainForm.cs')
proj=Path('server-dotnet/GatLogServer/GatLogServer.csproj')
installer=Path('server-dotnet/GatLogInstaller/Program.cs')
installer_proj=Path('server-dotnet/GatLogInstaller/GatTelemetryInstaller.csproj') if False else Path('server-dotnet/GatLogInstaller/GatLogInstaller.csproj')

s=ui.read_text(encoding='utf-8')
s=s.replace('CurrentVersion = "1.0.0"','CurrentVersion = "1.0.31"')
s=s.replace('GAT-LOG SERVER 1.0 | ETS2 + Telemetria','GAT-LOG SERVER 1.0.31 | ETS2 + Telemetria')
s=s.replace('C# WinForms 1.0.0','C# WinForms 1.0.31')
s=s.replace('Interval = 3000','Interval = 1000')
if 'ExtractAssociatedIcon' not in s:
    s=s.replace('DoubleBuffered = true;','DoubleBuffered = true;\n            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);',1)
ui.write_text(s,encoding='utf-8')

p=proj.read_text(encoding='utf-8')
p=p.replace('<Version>1.0.0.0</Version>','<Version>1.0.31.0</Version>')
p=p.replace('<FileVersion>1.0.0.0</FileVersion>','<FileVersion>1.0.31.0</FileVersion>')
p=p.replace('<AssemblyVersion>1.0.0.0</AssemblyVersion>','<AssemblyVersion>1.0.31.0</AssemblyVersion>')
if '<ApplicationIcon>' not in p:
    p=p.replace('<ApplicationManifest>app.manifest</ApplicationManifest>','<ApplicationManifest>app.manifest</ApplicationManifest>\n    <ApplicationIcon>assets\\GAT_SERVER.ico</ApplicationIcon>',1)
proj.write_text(p,encoding='utf-8')

x=installer.read_text(encoding='utf-8')
if 'using System.Runtime.InteropServices;' not in x:
    x=x.replace('using System.Reflection;','using System.Reflection;\nusing System.Runtime.InteropServices;',1)
x=x.replace('Instalar GAT-LOG Server C# 1.0.0 TESTE?','Atualizar GAT-LOG Server C# para 1.0.31?')
x=x.replace('GAT-LOG Server C# 1.0.0 TESTE instalado.','GAT-LOG Server C# 1.0.31 atualizado.')
marker='internal static class Program\n    {'
if 'SHChangeNotify' not in x:
    x=x.replace(marker,marker+'\n        [System.Runtime.InteropServices.DllImport("shell32.dll")]\n        private static extern void SHChangeNotify(uint wEventId,uint uFlags,IntPtr dwItem1,IntPtr dwItem2);',1)
if 'private static string Startup' not in x:
    x=x.replace('private static string StartMenu => Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);','private static string StartMenu => Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);\n        private static string Startup => Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);',1)
x=x.replace('private static void CreateShortcut(string shortcutPath, string targetPath)','private static void CreateShortcut(string shortcutPath, string targetPath, string arguments = "")')
if 'shortcut.Arguments = arguments;' not in x:
    x=x.replace('shortcut.TargetPath = targetPath;','shortcut.TargetPath = targetPath;\n            shortcut.Arguments = arguments;',1)
if 'shortcut.IconLocation' not in x:
    x=x.replace('shortcut.Description = "GAT-LOG Server";','shortcut.Description = "GAT-LOG Server";\n            shortcut.IconLocation = targetPath + ",0";',1)
call='CreateShortcut(Path.Combine(StartMenu, "GAT-LOG Server.lnk"), Path.Combine(InstallDir, "GAT_LOG_SERVER.exe"));'
startup='CreateShortcut(Path.Combine(Startup, "GAT Central.lnk"), Path.Combine(InstallDir, "GAT_LOG_AGENT.exe"), "--background");'
if startup not in x:
    x=x.replace(call,call+'\n                '+startup,1)
if 'SHChangeNotify(0x08000000' not in x:
    x=x.replace(startup,startup+'\n                SHChangeNotify(0x08000000,0x0000,IntPtr.Zero,IntPtr.Zero);',1)
installer.write_text(x,encoding='utf-8')

q=installer_proj.read_text(encoding='utf-8')
q=q.replace('GAT_LOG_SERVER_DOTNET_SETUP_1.0.0_TESTE','GAT_LOG_SERVER_DOTNET_UPDATE_1.0.31_TESTE')
if '<ApplicationIcon>' not in q:
    q=q.replace('<ApplicationManifest>app.manifest</ApplicationManifest>','<ApplicationManifest>app.manifest</ApplicationManifest>\n    <ApplicationIcon>assets\\GAT_SERVER.ico</ApplicationIcon>',1)
installer_proj.write_text(q,encoding='utf-8')
print('UI/updater 1.0.31 preparado')
