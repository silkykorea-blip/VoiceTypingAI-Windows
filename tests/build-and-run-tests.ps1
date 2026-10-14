$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $PSScriptRoot 'bin'
$exe = Join-Path $out 'DesktopRiskTests.exe'
$appSource = Join-Path $root 'Program.cs'
$wakeSource = Join-Path $root 'WakeAndConversation.cs'
$realtimeConversationSource = Join-Path $root 'RealtimeConversation.cs'
$featureLocalizationSource = Join-Path $root 'FeatureLocalization.cs'
$testSource = Join-Path $PSScriptRoot 'DesktopRiskTests.cs'
$providerSmokeSource = Join-Path $PSScriptRoot 'ProviderSmokeTests.cs'
$providerSmokeExe = Join-Path $out 'ProviderSmokeTests.exe'
$retiredWakeHost = Join-Path $out 'WakeWordHost.exe'
$appIcon = Join-Path $root 'app.ico'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$uiAutomationClient = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll'
$uiAutomationTypes = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll'

New-Item -ItemType Directory -Force -Path $out | Out-Null
if (Test-Path -LiteralPath $retiredWakeHost) {
  Remove-Item -LiteralPath $retiredWakeHost -Force
}

& $csc `
  /nologo `
  /target:exe `
  /platform:x64 `
  /optimize+ `
  /warn:4 `
  /codepage:65001 `
  /main:VoiceSecretaryDesktop.DesktopRiskTests `
  "/out:$exe" `
  "/win32icon:$appIcon" `
  "/resource:$appIcon,app.ico" `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  /reference:System.Web.Extensions.dll `
  /reference:System.Security.dll `
  "/reference:$uiAutomationClient" `
  "/reference:$uiAutomationTypes" `
  "$featureLocalizationSource" `
  "$realtimeConversationSource" `
  "$wakeSource" `
  "$appSource" `
  "$testSource"

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $exe
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Compile the live provider smoke tool without running it. To run it explicitly:
# tests\bin\ProviderSmokeTests.exe <path-to-your-non-sensitive-notes-speech.wav>
# It needs mono 16-bit PCM speech and uploads that audio to Gemini and OpenAI.
# Private recordings are not included; the offline suite above generates its own PCM.
& $csc `
  /nologo `
  /target:exe `
  /platform:x64 `
  /optimize+ `
  /warn:4 `
  /codepage:65001 `
  /main:VoiceSecretaryDesktop.ProviderSmokeTests `
  "/out:$providerSmokeExe" `
  "/win32icon:$appIcon" `
  "/resource:$appIcon,app.ico" `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  /reference:System.Web.Extensions.dll `
  /reference:System.Security.dll `
  "/reference:$uiAutomationClient" `
  "/reference:$uiAutomationTypes" `
  "$featureLocalizationSource" `
  "$realtimeConversationSource" `
  "$wakeSource" `
  "$appSource" `
  "$providerSmokeSource"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
