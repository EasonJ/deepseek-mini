$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.Xml.dll','System.Drawing.dll','System.Windows.Forms.dll','Microsoft.CSharp.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
$references += '/r:' + (Join-Path $root 'Microsoft.Web.WebView2.Core.dll')
$references += '/r:' + (Join-Path $root 'Microsoft.Web.WebView2.Wpf.dll')
$references += '/r:' + (Join-Path $framework 'System.Web.Extensions.dll')
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ ('/out:' + (Join-Path $root 'DeepSeekMini.exe')) ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')) ('/win32icon:' + (Join-Path $root 'assets/whale.ico')) ('/resource:' + (Join-Path $root 'assets/whale.ico') + ',Whale.ico') ('/resource:' + (Join-Path $root 'assets/whale.png') + ',Whale.png') ('/resource:' + (Join-Path $PSScriptRoot 'FillDraft.js') + ',FillDraft.js') @references (Join-Path $PSScriptRoot 'DeepSeekMini.cs') (Join-Path $PSScriptRoot 'Features.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

