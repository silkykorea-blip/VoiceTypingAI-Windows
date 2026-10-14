$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root 'bin'
$dist = Join-Path $root 'dist'
$exe = Join-Path $out 'VoiceTypingAI.exe'
$setup = Join-Path $dist 'VoiceTypingAISetup.exe'
$src = Join-Path $root 'Program.cs'
$wakeSrc = Join-Path $root 'WakeAndConversation.cs'
$realtimeConversationSrc = Join-Path $root 'RealtimeConversation.cs'
$featureLocalizationSrc = Join-Path $root 'FeatureLocalization.cs'
$ico = Join-Path $root 'app.ico'
$installerSrc = Join-Path $root 'installer\Installer.cs'
$retiredWakeHost = Join-Path $out 'WakeWordHost.exe'
$uiAutomationClient = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll'
$uiAutomationTypes = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll'
New-Item -ItemType Directory -Force -Path $out | Out-Null
New-Item -ItemType Directory -Force -Path $dist | Out-Null
if (Test-Path -LiteralPath $retiredWakeHost) {
  Remove-Item -LiteralPath $retiredWakeHost -Force
}
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

& $csc `
  /nologo `
  /target:winexe `
  /platform:x64 `
  /optimize+ `
  /warn:4 `
  /codepage:65001 `
  "/out:$exe" `
  "/win32icon:$ico" `
  "/resource:$ico,app.ico" `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  /reference:System.Web.Extensions.dll `
  /reference:System.Security.dll `
  "/reference:$uiAutomationClient" `
  "/reference:$uiAutomationTypes" `
  "$featureLocalizationSrc" `
  "$realtimeConversationSrc" `
  "$wakeSrc" `
  "$src"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Copy-Item -LiteralPath $ico -Destination (Join-Path $out 'app.ico') -Force
Write-Host "Built $exe"

& $csc `
  /nologo `
  /target:winexe `
  /platform:x64 `
  /optimize+ `
  /warn:4 `
  /codepage:65001 `
  "/out:$setup" `
  "/win32icon:$ico" `
  "/resource:$exe,VoiceTypingAI.exe" `
  "/resource:$(Join-Path $out 'app.ico'),app.ico" `
  /reference:System.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  "$installerSrc"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Built $setup"
