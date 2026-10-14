using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace VoiceSecretaryDesktop
{
    internal static class ProviderSmokeTests
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public static int Main(string[] args)
        {
            try
            {
                if (args == null || args.Length != 1)
                {
                    throw new ArgumentException(
                        "Usage: ProviderSmokeTests.exe <notes-speech.wav>. " +
                        "Supply your own non-sensitive spoken notes as mono 16-bit PCM WAV; " +
                        "this live test uploads the audio to Gemini and OpenAI. " +
                        "Recordings are intentionally not included in the repository.");
                }
                var noteWave = ReadPcmWave(args[0]);
                var saved = AppSettings.Load();
                if (string.IsNullOrWhiteSpace(saved.GeminiApiKey))
                {
                    throw new InvalidOperationException("Gemini API key is missing.");
                }
                if (string.IsNullOrWhiteSpace(saved.OpenAiApiKey))
                {
                    throw new InvalidOperationException("OpenAI API key is missing.");
                }

                var failures = new List<string>();
                RunProvider(saved, AiProvider.Gemini, noteWave, failures);
                RunProvider(saved, AiProvider.OpenAi, noteWave, failures);
                if (failures.Count == 0)
                {
                    Console.WriteLine("PROVIDER_SMOKE PASS 4/4");
                    return 0;
                }
                Console.Error.WriteLine(
                    "PROVIDER_SMOKE FAIL " + failures.Count + "/4");
                foreach (string failure in failures)
                {
                    Console.Error.WriteLine("FAILED " + failure);
                }
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "PROVIDER_SMOKE FAIL " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static void RunProvider(
            AppSettings saved,
            AiProvider provider,
            PcmWave noteWave,
            List<string> failures)
        {
            var settings = new AppSettings
            {
                Provider = provider,
                GeminiApiKey = saved.GeminiApiKey,
                OpenAiApiKey = saved.OpenAiApiKey,
                LanguageCode = saved.LanguageCode
            };
            string name = provider == AiProvider.Gemini ? "Gemini" : "OpenAI";

            RunOne(name + " Notes", delegate { RunNote(settings, noteWave); }, failures);
            RunOne(
                name + " Realtime Conversation",
                provider == AiProvider.Gemini
                    ? (Action)delegate { RunGeminiConversation(settings.GeminiApiKey); }
                    : delegate { RunOpenAiConversation(settings.OpenAiApiKey); },
                failures);
        }

        private static void RunOne(string name, Action action, List<string> failures)
        {
            try
            {
                action();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                failures.Add(name + " | " + ex);
            }
        }

        private static void RunNote(AppSettings settings, PcmWave wave)
        {
            using (var session = new RealtimeSpeechSession(
                settings,
                PromptBuilder.NotesCapture(),
                true,
                null))
            {
                session.MarkCaptureStarted();
                session.Start();
                Feed(session, wave);
                session.RequestStop();
                string result = session.Finish();
                if (string.IsNullOrWhiteSpace(result))
                {
                    throw new InvalidOperationException("Notes returned empty text.");
                }
                string expected = settings.Provider == AiProvider.OpenAi
                    ? "openai-integrated"
                    : "gemini-integrated";
                if (!string.Equals(session.CompletionRoute, expected, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Notes used an unexpected route: " + session.CompletionRoute);
                }
            }
        }

        private static void Feed(RealtimeSpeechSession session, PcmWave source)
        {
            byte[] pcm = Resample(source.Pcm, source.SampleRate, session.RecordingSampleRate);
            int chunkBytes = Math.Max(2, session.RecordingSampleRate * 2 / 10);
            for (int offset = 0; offset < pcm.Length; offset += chunkBytes)
            {
                int count = Math.Min(chunkBytes, pcm.Length - offset);
                var chunk = new byte[count];
                Buffer.BlockCopy(pcm, offset, chunk, 0, count);
                session.SendAudio(chunk);
                Thread.Sleep(10);
            }
        }

        private static void RunGeminiConversation(string apiKey)
        {
            using (var socket = new SimpleSecureWebSocket(
                LiveConversationProtocol.GeminiEndpoint + "?key=" + Uri.EscapeDataString(apiKey),
                new Dictionary<string, string>(),
                1000))
            {
                socket.Connect();
                socket.SendText(Json.Serialize(LiveConversationProtocol.GeminiSetup()));
                WaitFor(socket, 25000, delegate(Dictionary<string, object> map)
                {
                    ThrowProviderError(map);
                    return map != null && map.ContainsKey("setupComplete");
                }, "Gemini conversation setup did not complete.");

                socket.SendText(Json.Serialize(new Dictionary<string, object>
                {
                    {
                        "clientContent",
                        new Dictionary<string, object>
                        {
                            {
                                "turns",
                                new object[]
                                {
                                    new Dictionary<string, object>
                                    {
                                        { "role", "user" },
                                        {
                                            "parts",
                                            new object[]
                                            {
                                                new Dictionary<string, object>
                                                {
                                                    { "text", "Reply briefly that the realtime connection works." }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            { "turnComplete", true }
                        }
                    }
                }));

                bool audio = false;
                bool completed = false;
                WaitFor(socket, 45000, delegate(Dictionary<string, object> map)
                {
                    ThrowProviderError(map);
                    var serverContent = AsMap(Get(map, "serverContent"));
                    var modelTurn = AsMap(Get(serverContent, "modelTurn"));
                    var parts = AsArray(Get(modelTurn, "parts"));
                    if (parts != null)
                    {
                        foreach (object item in parts)
                        {
                            var part = AsMap(item);
                            var inline = AsMap(Get(part, "inlineData")) ??
                                         AsMap(Get(part, "inline_data"));
                            string data = OptString(inline, "data");
                            if (!string.IsNullOrWhiteSpace(data)) audio = true;
                        }
                    }
                    if (serverContent != null &&
                        (OptBool(serverContent, "turnComplete") ||
                         OptBool(serverContent, "generationComplete")))
                    {
                        completed = true;
                    }
                    return audio && completed;
                }, "Gemini conversation returned no completed audio response.");
            }
        }

        private static void RunOpenAiConversation(string apiKey)
        {
            using (var socket = new SimpleSecureWebSocket(
                LiveConversationProtocol.OpenAiEndpoint + "?model=" +
                Uri.EscapeDataString(LiveConversationProtocol.OpenAiModel),
                new Dictionary<string, string> { { "Authorization", "Bearer " + apiKey } },
                1000))
            {
                socket.Connect();
                WaitFor(socket, 25000, delegate(Dictionary<string, object> map)
                {
                    ThrowProviderError(map);
                    return OptString(map, "type") == "session.created";
                }, "OpenAI conversation session was not created.");
                socket.SendText(Json.Serialize(LiveConversationProtocol.OpenAiSessionUpdate()));
                WaitFor(socket, 25000, delegate(Dictionary<string, object> map)
                {
                    ThrowProviderError(map);
                    return OptString(map, "type") == "session.updated";
                }, "OpenAI conversation session was not updated.");

                socket.SendText(Json.Serialize(new Dictionary<string, object>
                {
                    { "type", "conversation.item.create" },
                    {
                        "item",
                        new Dictionary<string, object>
                        {
                            { "type", "message" },
                            { "role", "user" },
                            {
                                "content",
                                new object[]
                                {
                                    new Dictionary<string, object>
                                    {
                                        { "type", "input_text" },
                                        { "text", "Reply briefly that the realtime connection works." }
                                    }
                                }
                            }
                        }
                    }
                }));
                socket.SendText(Json.Serialize(new Dictionary<string, object>
                {
                    { "type", "response.create" },
                    {
                        "response",
                        new Dictionary<string, object>
                        {
                            { "output_modalities", new object[] { "audio" } }
                        }
                    }
                }));

                bool audio = false;
                bool completed = false;
                WaitFor(socket, 45000, delegate(Dictionary<string, object> map)
                {
                    ThrowProviderError(map);
                    string type = OptString(map, "type");
                    if ((type == "response.output_audio.delta" ||
                         type == "response.audio.delta") &&
                        !string.IsNullOrWhiteSpace(OptString(map, "delta")))
                    {
                        audio = true;
                    }
                    if (type == "response.done")
                    {
                        var response = AsMap(Get(map, "response"));
                        string status = OptString(response, "status");
                        if (status == "failed" || status == "cancelled")
                        {
                            throw new InvalidOperationException(
                                "OpenAI conversation response status: " + status);
                        }
                        completed = true;
                    }
                    return audio && completed;
                }, "OpenAI conversation returned no completed audio response.");
            }
        }

        private static void WaitFor(
            SimpleSecureWebSocket socket,
            int timeoutMs,
            Func<Dictionary<string, object>, bool> predicate,
            string timeoutMessage)
        {
            int deadline = unchecked(Environment.TickCount + timeoutMs);
            while (unchecked(Environment.TickCount - deadline) < 0)
            {
                string raw = socket.ReadText();
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var map = Json.DeserializeObject(raw) as Dictionary<string, object>;
                if (predicate(map)) return;
            }
            throw new TimeoutException(timeoutMessage);
        }

        private static void ThrowProviderError(Dictionary<string, object> map)
        {
            if (map == null) return;
            string type = OptString(map, "type");
            var error = AsMap(Get(map, "error"));
            if (type != "error" && error == null) return;
            string message = OptString(error, "message");
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(message) ? "Provider returned an error." : message);
        }

        private static PcmWave ReadPcmWave(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
                {
                    throw new InvalidDataException("Not a RIFF WAV file: " + path);
                }
                reader.ReadInt32();
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
                {
                    throw new InvalidDataException("Not a WAVE file: " + path);
                }

                int sampleRate = 0;
                short format = 0;
                short channels = 0;
                short bits = 0;
                byte[] pcm = null;
                while (stream.Position + 8 <= stream.Length)
                {
                    string id = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    int length = reader.ReadInt32();
                    if (length < 0 || stream.Position + length > stream.Length)
                    {
                        throw new InvalidDataException("Invalid WAV chunk.");
                    }
                    if (id == "fmt ")
                    {
                        long end = stream.Position + length;
                        format = reader.ReadInt16();
                        channels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        reader.ReadInt32();
                        reader.ReadInt16();
                        bits = reader.ReadInt16();
                        stream.Position = end;
                    }
                    else if (id == "data")
                    {
                        pcm = reader.ReadBytes(length);
                    }
                    else
                    {
                        stream.Position += length;
                    }
                    if ((length & 1) != 0 && stream.Position < stream.Length) stream.Position++;
                }
                if (format != 1 || channels != 1 || bits != 16 ||
                    sampleRate <= 0 || pcm == null || pcm.Length == 0)
                {
                    throw new InvalidDataException("Expected mono 16-bit PCM WAV audio.");
                }
                return new PcmWave(sampleRate, pcm);
            }
        }

        private static byte[] Resample(byte[] source, int sourceRate, int targetRate)
        {
            if (sourceRate == targetRate) return (byte[])source.Clone();
            int sourceSamples = source.Length / 2;
            int targetSamples = (int)Math.Round(sourceSamples * (double)targetRate / sourceRate);
            var result = new byte[targetSamples * 2];
            for (int i = 0; i < targetSamples; i++)
            {
                double position = i * (double)sourceRate / targetRate;
                int left = Math.Min(sourceSamples - 1, (int)position);
                int right = Math.Min(sourceSamples - 1, left + 1);
                double fraction = position - left;
                short a = ReadInt16(source, left * 2);
                short b = ReadInt16(source, right * 2);
                int sample = (int)Math.Round(a + (b - a) * fraction);
                result[i * 2] = (byte)(sample & 0xff);
                result[i * 2 + 1] = (byte)((sample >> 8) & 0xff);
            }
            return result;
        }

        private static short ReadInt16(byte[] data, int offset)
        {
            return (short)(data[offset] | (data[offset + 1] << 8));
        }

        private static object Get(Dictionary<string, object> map, string key)
        {
            if (map == null) return null;
            object value;
            return map.TryGetValue(key, out value) ? value : null;
        }

        private static Dictionary<string, object> AsMap(object value)
        {
            return value as Dictionary<string, object>;
        }

        private static object[] AsArray(object value)
        {
            var array = value as object[];
            if (array != null) return array;
            var list = value as System.Collections.ArrayList;
            return list == null ? null : list.ToArray();
        }

        private static string OptString(Dictionary<string, object> map, string key)
        {
            object value = Get(map, key);
            return value == null ? "" : Convert.ToString(value) ?? "";
        }

        private static bool OptBool(Dictionary<string, object> map, string key)
        {
            object value = Get(map, key);
            if (value is bool) return (bool)value;
            bool result;
            return value != null && bool.TryParse(Convert.ToString(value), out result) && result;
        }

        private sealed class PcmWave
        {
            internal readonly int SampleRate;
            internal readonly byte[] Pcm;

            internal PcmWave(int sampleRate, byte[] pcm)
            {
                SampleRate = sampleRate;
                Pcm = pcm;
            }
        }
    }
}
