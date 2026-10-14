# Privacy and Data Handling

This notice describes the current implementation of **Voice Typing AI for Windows 0.4.83**. Android is a separate product; refer to its store listing and privacy notice for details.

## Your Own API Key

The Windows version does not provide a developer API key or a shared allowance of free API calls. Before using the app, select Gemini or OpenAI in Settings and enter your own key. Voice typing, translation input, Notes, and real-time voice conversations use the currently selected provider and its corresponding key. Charges, quotas, available models, and regional restrictions are determined by that provider.

Keys are encrypted locally using Windows DPAPI with the `CurrentUser` scope and saved in `%APPDATA%\VoiceTypingAI\settings.ini`. The encrypted data is tied to the current Windows user environment, so this file should not be distributed as a reusable configuration. DPAPI does not protect against software that has already gained control of your Windows user session; keep your Windows account secure.

## Data Sent to Cloud Providers

The app connects to the selected provider over the internet. Recordings or audio streams, text to be processed, translation content, and conversation context needed for the selected feature may be sent to Gemini or OpenAI. When a real-time conversation uses local schedules or memos, relevant content may become part of that conversation's context. Enabling web search for Gemini conversations also uses the provider's search capabilities; this setting is off by default.

"Local VAD" means that voice activity is detected on your computer. **It does not mean offline speech recognition.** Consult the current terms for your provider and account to understand server-side data retention, use, and billing. This project does not promise zero retention by providers and has not verified availability for every account or region.

## Data That May Remain on Your Computer

| Data | Location and behavior |
| --- | --- |
| Settings and encrypted keys | `%APPDATA%\VoiceTypingAI\settings.ini` |
| Notes, schedules, and conversation history | Files such as `widgets.json`, `schedules.json`, and `realtime-conversations.json` in the same directory; `.bak` backups may also exist |
| Runtime logs | Files such as `debug.log` and `debug.log.1` in the same directory; they may contain recognized text, context, or diagnostic information |
| Processed input recordings | The current version automatically writes these to `processed-audio` under a directory selected by the app; when running from source, this may be inside the project directory |
| Temporary recordings | Audio files in the Windows temporary directory, including `voice-typing-ai-last-recording.wav` |

The portable version still writes to your Windows user data directories; it does not leave your computer free of application data when you exit. The current version has no setting to disable recording archives. Removing the application does not necessarily remove the data listed above. Notes, schedules, conversations, and audio are not encrypted merely because API keys are protected with DPAPI.

## Before Sharing a Bug Report

Do not upload `settings.ini`, your complete application data directory, recordings, conversation history, unchecked logs, or screenshots that reveal a key. Prefer sharing the application version, selected provider, steps to reproduce the issue, and a redacted error summary.

Field names, empty key defaults, and dummy values in tests are not usable credentials. Never put your real keys in source code, test fixtures, build scripts, commits, or issues.

## Removing Your Local Data

Exit the app before inspecting the locations above. Delete only files you have confirmed belong to this application and no longer need. When clearing conversations, check for backups; when clearing recordings, also check temporary files. If you delete the settings file, you will need to enter your own key again. If a key has been publicly exposed, revoke or rotate it through the provider. Deleting a local or GitHub file cannot recall credentials that have already been disclosed.

## Scope of the Public Repository

The public repository contains only the source code needed to build the Windows version, build scripts, offline tests, and public documentation assets. Publication uses an explicit file list and excludes the developer's runtime configuration, historical recordings, personal logs, internal handoff documents, and old-version backups. `tests/fixtures/android829-input.json` is an input-protocol regression fixture for Windows, not Android application source code.

The installer and portable executable are provided as assets attached to the corresponding GitHub Release, along with SHA-256 checksums. Privacy checks help reduce the risk of disclosure; they do not replace a comprehensive security audit of the application.
