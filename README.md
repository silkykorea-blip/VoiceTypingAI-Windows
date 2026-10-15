<p align="center">
  <img src="assets/github-hero.svg?v=0.4.83-en" alt="Voice Typing AI for Windows — voice typing, translation, notes, and real-time conversations" width="100%">
</p>

<h1 align="center">Voice Typing AI for Windows</h1>

<p align="center"><strong>Press a shortcut, speak your thoughts, and put them into the text field.</strong></p>
<p align="center">Windows x64 · 0.4.83 · Gemini / OpenAI · Bring Your Own API Key · <a href="LICENSE">MIT License</a></p>

<p align="center">
  <a href="https://github.com/silkykorea-blip/VoiceTypingAI-Windows/releases/tag/v0.4.83"><strong>Download for Windows</strong></a>
  &nbsp;·&nbsp; <a href="#getting-started">Getting started</a>
  &nbsp;·&nbsp; <a href="#default-shortcuts">Shortcuts</a>
  &nbsp;·&nbsp; <a href="#privacy-and-local-data">Privacy</a>
  &nbsp;·&nbsp; <a href="#android-app">Android app</a>
</p>

---

Voice Typing AI is an open-source Windows desktop tool for dictation, translation, voice notes, and real-time conversations with spoken replies. Global shortcuts let you start speaking while working in another application.

**The Windows app uses BYOK (Bring Your Own Key): you must supply your own Gemini or OpenAI API key.** The repository and release packages do not include the developer's credentials or cloud credits. Speech recognition and conversations require an internet connection. Pricing, quotas, model access, and service availability depend on your chosen provider.

## Download and install

Current release: **0.4.83**. Download from the [GitHub release page](https://github.com/silkykorea-blip/VoiceTypingAI-Windows/releases/tag/v0.4.83) and choose either the installer or the portable executable.

| Package | Download | How it works |
| --- | --- | --- |
| Installer | [VoiceTypingAI-v0.4.83-setup.exe](https://github.com/silkykorea-blip/VoiceTypingAI-Windows/releases/download/v0.4.83/VoiceTypingAI-v0.4.83-setup.exe) | Installs for the current Windows user and creates desktop and Start menu shortcuts |
| Portable | [VoiceTypingAI-v0.4.83-portable.exe](https://github.com/silkykorea-blip/VoiceTypingAI-Windows/releases/download/v0.4.83/VoiceTypingAI-v0.4.83-portable.exe) | Save it in a writable folder and run the EXE directly |
| Checksums | [SHA256SUMS.txt](https://github.com/silkykorea-blip/VoiceTypingAI-Windows/releases/download/v0.4.83/SHA256SUMS.txt) | Verify the SHA-256 hash of your download |

You need **Windows x64, .NET Framework 4.x, a working microphone, and an internet connection**. The project builds with the Windows .NET Framework compiler. If the runtime is missing, see [Microsoft's .NET Framework installation guide](https://learn.microsoft.com/dotnet/framework/install/).

The installer places the application in `%LOCALAPPDATA%\VoiceTypingAI`. The portable executable still writes settings and history to Windows user-data folders; it does **not** keep all of its data beside the EXE.

<details>
<summary>Verify a download</summary>

Open PowerShell in your download folder and run the command for the package you downloaded:

```powershell
Get-FileHash .\VoiceTypingAI-v0.4.83-setup.exe -Algorithm SHA256
Get-FileHash .\VoiceTypingAI-v0.4.83-portable.exe -Algorithm SHA256
```

Compare the result with `SHA256SUMS.txt` from the same release.

</details>

## Features

| Feature | What you can do |
| --- | --- |
| **Voice typing** | Dictate into an editable text field. If automatic insertion is unavailable, check the clipboard and paste manually |
| **Translation input** | Speak and receive text in your selected target language. The default target is English |
| **Notes** | Capture a voice note with its own shortcut, then view and manage it in the floating Notes window |
| **Real-time voice chat** | Talk with your selected provider and receive text and spoken replies. Use 3 separate chat pages, rename their titles, or clear the current page |
| **Schedules and memos** | Explicitly ask a live conversation to add or read local schedules and memos, then view the entries in Notes |
| **Window and shortcut settings** | Customize shortcuts, show or hide Notes and chat windows, pin them, and control their appearance in Alt+Tab. Gemini chat also offers optional web search, disabled by default |

The language setting offers **31 named language/region choices plus System**. Translation has 31 target options. These are selectable settings, not a guarantee of identical recognition quality across languages or providers.

Voice typing and translation have a **3-minute maximum per recording**. Latency and accuracy depend on the microphone, background noise, network, provider model, and account limits.

## Getting started

### 1. Add your own API key

Open the app's settings, select **Gemini** or **OpenAI**, enter the corresponding API key, and save. The current features use the provider and key selected in these settings.

| Provider | Official setup guide |
| --- | --- |
| Gemini | [Create and manage a Gemini API key](https://ai.google.dev/gemini-api/docs/api-key) |
| OpenAI | [Find and manage an OpenAI API key](https://help.openai.com/en/articles/4936850-where-do-i-find-my-op) |

Check that your account has access to the APIs and models used by this version and has sufficient quota. **BYOK does not mean free cloud processing.** The app cannot guarantee provider permissions, free credits, or continued model availability.

### 2. Try your first dictation

1. Select your default input microphone in Windows and allow desktop apps to access it.
2. Open Notepad or another ordinary editable text field and place the cursor where you want the text.
3. **Press Right Alt once to start recording. Press it again to stop and submit.** You do not need to hold the key.
4. Wait for processing and review the result. If it was not inserted automatically, check the clipboard before trying `Ctrl + V`.

Keep the target field and focus stable while recording. Password fields, protected controls, and applications running at a different privilege level may reject automatic insertion.

### 3. Try translation, Notes, and chat

- **Translation:** choose a target language in settings, then use the translation shortcut to start and stop recording.
- **Notes:** use the Notes shortcut to start and stop a voice note, then review it in the Notes window.
- **Chat:** use the real-time chat shortcut to start a conversation and press it again to end it. Select the chat page you want to use in the chat window.

## Default shortcuts

| Shortcut | Action |
| --- | --- |
| **Right Alt** | Start / stop voice typing |
| **Scroll Lock** | Start / stop translation input |
| **Right Alt + Space** | Start / stop a voice note |
| **Right Alt + Right Ctrl** | Start / end a real-time voice conversation |
| **Pause / Break** | Show / hide floating windows |

All of these shortcuts can be changed in settings. If your keyboard lacks a dedicated Scroll Lock or Pause / Break key, choose another combination. Changing a shortcut can also resolve conflicts with other applications.

## Privacy and local data

For the full notice, see [Privacy for the Windows app](docs/PRIVACY.md).

### How data is handled

- **Cloud processing:** audio, text, and conversation context needed for recognition, translation, or chat are sent to the selected AI provider. Your key authenticates those requests. The provider's policies govern its processing and retention.
- **Local credentials:** saved API keys are encrypted with Windows DPAPI in the `CurrentUser` scope, tied to the current Windows user. Configuration files should still be treated as private.
- **Local content:** notes, chat history, schedules, memos, logs, and audio may remain on your computer. When you ask a live conversation to read a schedule or memo, the relevant content is also used in that provider session.
- **Clipboard:** recognized text may be placed on the Windows clipboard for automatic insertion or manual pasting.

### What version 0.4.83 retains

**This version automatically saves processed input recordings and may leave temporary recordings. There is no user-facing switch to disable recording archival and no automatic expiry for archived recordings.** Manage these files according to your privacy needs, especially on a shared computer.

| Data | Common location |
| --- | --- |
| Settings and encrypted API keys | `%APPDATA%\VoiceTypingAI\settings.ini` |
| Notes, undated memos, and some window state | `%APPDATA%\VoiceTypingAI\widgets.json` |
| Schedules and compatible legacy memos | `%APPDATA%\VoiceTypingAI\schedules.json` |
| Real-time chat history | `%APPDATA%\VoiceTypingAI\realtime-conversations.json` |
| Debug logs and diagnostic data | `%APPDATA%\VoiceTypingAI\debug.log`, `debug.log.1`, and `diagnostics\` |
| Floating-window layout | `%LOCALAPPDATA%\VoiceTypingAI\widget-layout.ini` |
| Processed input recordings | `processed-audio\`; see the location rules below |
| Copy of the latest recording | `%TEMP%\voice-typing-ai-last-recording.wav` |
| Other temporary recordings | `%TEMP%\voice-typing-ai-*.wav` |

Settings and history files may also have `.bak` backups. Processed audio first uses the directory specified by `VOICE_TYPING_AI_PROCESSED_AUDIO_DIR`. Otherwise, the app tries a detected project folder, the EXE's folder, and then `%LOCALAPPDATA%\VoiceTypingAI\processed-audio`. The log location can also be overridden with `VOICE_TYPING_AI_DEBUG_LOG_PATH`.

Exit the app before cleaning up its data, and preserve any notes or schedules you still need. Clearing a chat page, deleting the EXE, or downloading a new release does not imply that all of these files have been removed. Users upgrading from older versions may also want to check the legacy `generated-audio` folder for archived spoken replies.

**Development recordings, transcript text, logs, and experiment outputs were removed before this source publication.** That cleanup did not change the app's archival behavior. Running the app from source can create sensitive files again. Review newly generated files before committing code, attaching diagnostics to an issue, or sharing a project folder.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| Invalid key, access denied, or insufficient quota | Confirm the selected provider, the complete key, account permissions, and quota for this version's configured models |
| Recording starts but no sound is captured | Check the Windows default input device, microphone permissions, mute state, and input level. Test the device with a Windows recording app first |
| Text is recognized but not inserted | Keep focus in the original editable field. Check the clipboard and paste manually if needed; also check the target app's privilege level |
| A shortcut does nothing | Check that you are using the **right-hand** Alt / Ctrl keys where required, look for conflicts, or assign another shortcut |
| Processing is slow or a request fails | Check connectivity, provider status, quota, and model permissions. Try a short recording first; retries may create additional API usage |
| The Notes or chat window is missing | Check its display setting, or use Pause / Break or your custom window shortcut |
| A saved key does not work after moving to another computer | Key protection is tied to the Windows user. Open settings in the new environment and enter your own key again |
| The downloaded app will not start | Confirm Windows x64, verify the file, and check the .NET Framework runtime. Download only from this repository's Releases |

To report a problem, open an [issue](https://github.com/silkykorea-blip/VoiceTypingAI-Windows/issues) with the app version, Windows environment, selected provider, and reproduction steps. **Do not attach API keys, complete user configurations, personal recordings, or unreviewed conversation/debug logs.**

## Build from source

This repository contains the **C# / WinForms source for the Windows app**. The current build calls the Windows .NET Framework compiler and system assemblies directly. It does not require Node.js, Python, or an online NuGet restore.

Open PowerShell in the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

| Output | Purpose |
| --- | --- |
| `bin\VoiceTypingAI.exe` | Windows x64 application |
| `dist\VoiceTypingAISetup.exe` | Installer |

The build script uses `Framework64\v4.0.30319\csc.exe` under the standard Windows installation path and references the WPF UI Automation assemblies. The `v4.0.30319` folder name identifies the tool location; it is not a claim that every older Windows release or .NET Framework 4.x environment has been validated.

<details>
<summary>Source map</summary>

| File / folder | Contents |
| --- | --- |
| [Program.cs](Program.cs) | Main UI, voice input, settings, audio processing, and automatic text insertion |
| [RealtimeConversation.cs](RealtimeConversation.cs) | Separate chat pages and conversation storage |
| [WakeAndConversation.cs](WakeAndConversation.cs) | Voice sessions and provider communication; the filename retains historical naming |
| [FeatureLocalization.cs](FeatureLocalization.cs) | Feature UI localization |
| [installer/](installer/) | Installer and installation files |
| [tests/](tests/) | Offline regression tests and provider test tools |
| [build.ps1](build.ps1) | Application and installer build entry point |

</details>

## Tests and validation limits

Run the offline regression suite:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\build-and-run-tests.ps1
```

The local checks before this source publication completed with a **successful build and 132 / 132 offline tests passing**. The script then compiles `ProviderSmokeTests.exe`, but does not automatically run live provider requests.

Offline tests check local logic. They do not replace testing every microphone, Bluetooth device, target text field, elevated application, or live provider API. They also do not guarantee future cloud-model availability. Running the provider test tool yourself requires your own keys and non-sensitive test audio; it sends audio to both Gemini and OpenAI and may incur charges. Personal development recordings are not distributed with the repository.

## Android app

<a href="https://play.google.com/store/apps/details?id=com.silkytypingai.app">
  <img src="assets/android-card.svg?v=0.4.83-en" alt="Voice Typing AI - Talk to Text — view the separate Android app on Google Play" width="100%">
</a>

For your phone, see the separate **Voice Typing AI - Talk to Text** app:

**[View the Android app on Google Play →](https://play.google.com/store/apps/details?id=com.silkytypingai.app)**

This repository contains only the Windows source. The Android app has its own features, account requirements, and terms, as described in its current Google Play listing. The Windows BYOK setup and feature descriptions do not define the Android app's behavior.

## Author and licensing

Developer: **SILKYKOREA**  
Contact: [cuizhe699@gmail.com](mailto:cuizhe699@gmail.com)

This Windows project is open source under the **[MIT License](LICENSE)**. You may use, modify, redistribute, and sell copies, including in closed-source commercial products, provided you retain the copyright and license notice. The software is provided without warranty; see the license for the full terms.

This license covers this Windows repository and the Windows 0.4.83 binaries distributed in its GitHub Release. The separately linked Android app is not included in this license grant. Gemini and OpenAI services remain subject to their own terms, and you must supply your own API key.
