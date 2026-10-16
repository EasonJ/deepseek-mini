$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = '1.0.3537.50'
$deps = Join-Path $root '.deps'
New-Item -ItemType Directory -Force $deps | Out-Null
$archive = Join-Path $deps 'webview2.zip'
$package = Join-Path $deps 'webview2'
Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg" -OutFile $archive
Expand-Archive -LiteralPath $archive -DestinationPath $package -Force
Copy-Item -LiteralPath (Join-Path $package 'lib/net462/Microsoft.Web.WebView2.Core.dll'),(Join-Path $package 'lib/net462/Microsoft.Web.WebView2.Wpf.dll'),(Join-Path $package 'runtimes/win-x64/native/WebView2Loader.dll') -Destination $root
Write-Output "Restored Microsoft.Web.WebView2 $version. Run source/build.ps1 next."
