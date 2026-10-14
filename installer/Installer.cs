using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Voice Typing AI Setup")]
[assembly: AssemblyProduct("Voice Typing AI")]
[assembly: AssemblyCompany("Voice Typing AI")]
[assembly: AssemblyVersion("0.4.83.0")]
[assembly: AssemblyFileVersion("0.4.83.0")]
[assembly: AssemblyInformationalVersion("0.4.83")]

namespace VoiceTypingAISetup
{
    internal static class InstallerProgram
    {
        private const string DisplayName = "Voice Typing AI";
        private const string InstallFolder = "VoiceTypingAI";
        private const string ExeFileName = "VoiceTypingAI.exe";
        private const string LegacyShortcutName = "Voice Secretary.lnk";
        private const string ShutdownEventName = @"Local\VoiceTypingAI.Shutdown";

        [STAThread]
        private static void Main()
        {
            try
            {
                var installDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    InstallFolder);
                Directory.CreateDirectory(installDir);

                var exePath = Path.Combine(installDir, ExeFileName);
                var iconPath = Path.Combine(installDir, "app.ico");
                CloseInstalledApp(exePath);
                ExtractResource(ExeFileName, exePath);
                ExtractResource("app.ico", iconPath);
                File.WriteAllText(Path.Combine(installDir, "first-run-reset.flag"), DateTime.UtcNow.ToString("O"));

                DeleteIfExists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), LegacyShortcutName));
                CreateShortcut(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), DisplayName + ".lnk"),
                    exePath,
                    installDir,
                    iconPath);

                var oldStartMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Voice Secretary");
                DeleteIfExists(Path.Combine(oldStartMenuDir, LegacyShortcutName));
                var startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), DisplayName);
                Directory.CreateDirectory(startMenuDir);
                CreateShortcut(Path.Combine(startMenuDir, DisplayName + ".lnk"), exePath, installDir, iconPath);

                Process.Start(exePath);
                MessageBox.Show(DisplayName + " has been installed.", DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, DisplayName + " Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void CloseInstalledApp(string exePath)
        {
            try
            {
                using (var shutdownEvent = EventWaitHandle.OpenExisting(ShutdownEventName)) shutdownEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException) { }

            var deadline = Environment.TickCount + 5000;
            while (Environment.TickCount - deadline < 0)
            {
                var running = InstalledProcesses(exePath);
                if (running.Length == 0) break;
                foreach (var process in running) process.Dispose();
                Thread.Sleep(100);
            }
            var remaining = InstalledProcesses(exePath);
            if (remaining.Length == 0) return;

            var answer = MessageBox.Show(
                DisplayName + " is currently running and must be closed before updating. Close it now?",
                DisplayName + " Setup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) throw new InvalidOperationException("Installation was cancelled because the app is still running.");
            foreach (var process in remaining)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                finally { process.Dispose(); }
            }
        }

        private static Process[] InstalledProcesses(string exePath)
        {
            var matches = new System.Collections.Generic.List<Process>();
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exePath)))
            {
                try
                {
                    var path = process.MainModule == null ? "" : process.MainModule.FileName;
                    if (string.Equals(path, exePath, StringComparison.OrdinalIgnoreCase)) matches.Add(process);
                    else process.Dispose();
                }
                catch { process.Dispose(); }
            }
            return matches.ToArray();
        }

        private static void ExtractResource(string resourceName, string outputPath)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (input == null) throw new InvalidOperationException("Missing installer resource: " + resourceName);
                using (var output = File.Create(outputPath))
                {
                    input.CopyTo(output);
                }
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string iconPath)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            var shell = Activator.CreateInstance(shellType);
            var shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { DisplayName });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}
