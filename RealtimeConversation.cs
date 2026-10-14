using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace VoiceSecretaryDesktop
{
    internal sealed class RealtimeConversationTurn
    {
        public string question;
        public string answer;
        public string createdAt;
        public string deliveryId;

        internal RealtimeConversationTurn()
        {
        }

        internal RealtimeConversationTurn(
            string question,
            string answer,
            string createdAt,
            string deliveryId)
        {
            this.question = (question ?? "").Trim();
            this.answer = (answer ?? "").Trim();
            this.createdAt = createdAt ?? "";
            this.deliveryId = deliveryId ?? "";
        }

        internal RealtimeConversationTurn Clone()
        {
            return new RealtimeConversationTurn(
                question,
                answer,
                createdAt,
                deliveryId);
        }
    }

    internal sealed class RealtimeConversationBinding
    {
        internal const int MaximumHistoryTurns = 50;
        internal const int MaximumHistoryCharacters = 20000;

        internal RealtimeConversationBinding(
            string sessionId,
            long sessionGeneration,
            int window,
            string windowId,
            RealtimeConversationTurn[] history)
        {
            SessionId = string.IsNullOrWhiteSpace(sessionId)
                ? Guid.NewGuid().ToString("N")
                : sessionId.Trim();
            SessionGeneration = sessionGeneration;
            Window = window;
            WindowId = windowId ?? "";
            History = CloneTurns(history);
        }

        internal static RealtimeConversationBinding Empty()
        {
            return new RealtimeConversationBinding(
                Guid.NewGuid().ToString("N"),
                0,
                -1,
                "",
                new RealtimeConversationTurn[0]);
        }

        internal string SessionId { get; private set; }
        internal long SessionGeneration { get; private set; }
        internal int Window { get; private set; }
        internal string WindowId { get; private set; }
        internal RealtimeConversationTurn[] History { get; private set; }

        private static RealtimeConversationTurn[] CloneTurns(
            RealtimeConversationTurn[] source)
        {
            if (source == null || source.Length == 0)
            {
                return new RealtimeConversationTurn[0];
            }
            var result = new RealtimeConversationTurn[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                result[index] = source[index] == null
                    ? new RealtimeConversationTurn()
                    : source[index].Clone();
            }
            return result;
        }
    }

    internal sealed class RealtimeConversationDraftUpdate
    {
        internal RealtimeConversationDraftUpdate(
            string sessionId,
            string turnId,
            int turnSequence,
            long revision,
            string question,
            string answer,
            bool cleared)
        {
            SessionId = sessionId ?? "";
            TurnId = turnId ?? "";
            TurnSequence = turnSequence;
            Revision = revision;
            Question = (question ?? "").Trim();
            Answer = (answer ?? "").Trim();
            Cleared = cleared;
        }

        internal string SessionId { get; private set; }
        internal string TurnId { get; private set; }
        internal int TurnSequence { get; private set; }
        internal long Revision { get; private set; }
        internal string Question { get; private set; }
        internal string Answer { get; private set; }
        internal bool Cleared { get; private set; }
    }

    internal sealed class RealtimeConversationDraftSnapshot
    {
        internal RealtimeConversationDraftSnapshot(
            string sessionId,
            string turnId,
            int turnSequence,
            long revision,
            int window,
            string question,
            string answer)
        {
            SessionId = sessionId ?? "";
            TurnId = turnId ?? "";
            TurnSequence = turnSequence;
            Revision = revision;
            Window = window;
            Question = question ?? "";
            Answer = answer ?? "";
        }

        internal string SessionId { get; private set; }
        internal string TurnId { get; private set; }
        internal int TurnSequence { get; private set; }
        internal long Revision { get; private set; }
        internal int Window { get; private set; }
        internal string Question { get; private set; }
        internal string Answer { get; private set; }
    }

    internal sealed class RealtimeConversationExchange
    {
        internal RealtimeConversationExchange(
            string sessionId,
            string turnId,
            int turnSequence,
            long revision,
            string question,
            string answer)
            : this(
                sessionId,
                turnId,
                turnSequence,
                revision,
                question,
                answer,
                true)
        {
        }

        internal RealtimeConversationExchange(
            string sessionId,
            string turnId,
            int turnSequence,
            long revision,
            string question,
            string answer,
            bool persist)
        {
            SessionId = sessionId ?? "";
            TurnId = turnId ?? "";
            TurnSequence = turnSequence;
            Revision = revision;
            Question = (question ?? "").Trim();
            Answer = (answer ?? "").Trim();
            Persist = persist;
        }

        internal string SessionId { get; private set; }
        internal string TurnId { get; private set; }
        internal int TurnSequence { get; private set; }
        internal long Revision { get; private set; }
        internal string Question { get; private set; }
        internal string Answer { get; private set; }
        internal bool Persist { get; private set; }
    }

    internal sealed class RealtimeConversationWindowData
    {
        public string id;
        public string name;
        public int color;
        public RealtimeConversationTurn[] turns;
    }

    internal sealed class RealtimeConversationData
    {
        public int version = 1;
        public int activeWindow;
        public RealtimeConversationWindowData[] windows;
    }

    internal sealed class RealtimeConversationStore
    {
        internal const int MaximumWindows = 3;
        internal const int MaximumTurnsPerWindow = 50;

        private readonly object gate = new object();
        private readonly bool persistChanges;
        private readonly List<RealtimeConversationWindowData> windows =
            new List<RealtimeConversationWindowData>();
        private int activeWindow;
        private bool writeBlocked;
        private string recoveryPath = "";
        private long nextSessionGeneration;
        private string activeSessionId = "";
        private long activeSessionGeneration;
        private int activeSessionWindow = -1;
        private string activeSessionWindowId = "";
        private int latestTurnSequence;
        private long latestTurnRevision = -1;
        private RealtimeConversationDraftSnapshot draft;

        internal RealtimeConversationStore(bool persistChanges)
        {
            this.persistChanges = persistChanges;
            EnsureThreeWindows();
        }

        internal static RealtimeConversationStore Load()
        {
            var store = new RealtimeConversationStore(true);
            if (!File.Exists(store.StorePath)) return store;
            try
            {
                store.ApplyData(ReadAndValidate(store.StorePath));
                return store;
            }
            catch (Exception ex)
            {
                store.writeBlocked = true;
                store.recoveryPath = PreserveCorruptCopy(
                    store.StorePath);
                DebugLog.Write(
                    "realtime conversation store primary rejected type=" +
                    ex.GetType().Name +
                    " recoveryCreated=" +
                    (!string.IsNullOrWhiteSpace(store.recoveryPath)));
                try
                {
                    var backup = store.StorePath + ".bak";
                    if (File.Exists(backup))
                    {
                        store.ApplyData(ReadAndValidate(backup));
                    }
                }
                catch (Exception backupError)
                {
                    DebugLog.Write(
                        "realtime conversation store backup rejected type=" +
                        backupError.GetType().Name);
                }
                return store;
            }
        }

        internal static RealtimeConversationStore CreateInMemory()
        {
            return new RealtimeConversationStore(false);
        }

        internal bool WriteBlocked
        {
            get { lock (gate) return writeBlocked; }
        }

        internal string RecoveryPath
        {
            get { lock (gate) return recoveryPath; }
        }

        internal int ActiveWindow
        {
            get { lock (gate) return activeWindow; }
        }

        internal bool HasActiveSession
        {
            get { lock (gate) return activeSessionGeneration > 0; }
        }

        internal void SetActiveWindow(int window)
        {
            if (window < 0 || window >= MaximumWindows) return;
            lock (gate)
            {
                activeWindow = window;
                TryPersist();
            }
        }

        internal string GetWindowName(int window)
        {
            lock (gate)
            {
                window = NormalizeWindow(window);
                var name = windows[window].name;
                return string.IsNullOrWhiteSpace(name)
                    ? (window + 1).ToString(
                        CultureInfo.InvariantCulture)
                    : name;
            }
        }

        internal void SetWindowName(int window, string name)
        {
            lock (gate)
            {
                window = NormalizeWindow(window);
                var previous = windows[window].name;
                windows[window].name = string.IsNullOrWhiteSpace(name)
                    ? (window + 1).ToString(
                        CultureInfo.InvariantCulture)
                    : name.Trim();
                if (!TryPersist()) windows[window].name = previous;
            }
        }

        internal Color GetWindowColor(int window)
        {
            lock (gate)
            {
                window = NormalizeWindow(window);
                return Color.FromArgb(windows[window].color);
            }
        }

        internal RealtimeConversationTurn[] SnapshotWindow(int window)
        {
            lock (gate)
            {
                window = NormalizeWindow(window);
                return CloneTurns(windows[window].turns);
            }
        }

        internal RealtimeConversationDraftSnapshot SnapshotDraftForWindow(
            int window)
        {
            lock (gate)
            {
                if (draft == null || draft.Window != NormalizeWindow(window))
                {
                    return null;
                }
                return new RealtimeConversationDraftSnapshot(
                    draft.SessionId,
                    draft.TurnId,
                    draft.TurnSequence,
                    draft.Revision,
                    draft.Window,
                    draft.Question,
                    draft.Answer);
            }
        }

        internal RealtimeConversationBinding FreezeActiveWindow(string sessionId)
        {
            lock (gate)
            {
                var window = NormalizeWindow(activeWindow);
                nextSessionGeneration++;
                if (nextSessionGeneration <= 0) nextSessionGeneration = 1;
                activeSessionId = string.IsNullOrWhiteSpace(sessionId)
                    ? Guid.NewGuid().ToString("N")
                    : sessionId.Trim();
                activeSessionGeneration = nextSessionGeneration;
                activeSessionWindow = window;
                activeSessionWindowId = windows[window].id;
                latestTurnSequence = 0;
                latestTurnRevision = -1;
                draft = null;
                return new RealtimeConversationBinding(
                    activeSessionId,
                    activeSessionGeneration,
                    window,
                    activeSessionWindowId,
                    SelectContext(windows[window].turns));
            }
        }

        internal bool ApplyDraft(
            RealtimeConversationBinding binding,
            RealtimeConversationDraftUpdate update)
        {
            if (binding == null || update == null) return false;
            lock (gate)
            {
                if (!MatchesActiveSession(binding) ||
                    !string.Equals(
                        binding.SessionId,
                        update.SessionId,
                        StringComparison.Ordinal) ||
                    update.TurnSequence <= 0 ||
                    string.IsNullOrWhiteSpace(update.TurnId))
                {
                    return false;
                }
                if (update.TurnSequence < latestTurnSequence) return false;
                if (update.TurnSequence == latestTurnSequence &&
                    update.Revision <= latestTurnRevision)
                {
                    return false;
                }

                latestTurnSequence = update.TurnSequence;
                latestTurnRevision = update.Revision;
                if (update.Cleared)
                {
                    if (draft != null &&
                        draft.TurnSequence == update.TurnSequence)
                    {
                        draft = null;
                    }
                    return true;
                }

                draft = new RealtimeConversationDraftSnapshot(
                    update.SessionId,
                    update.TurnId,
                    update.TurnSequence,
                    update.Revision,
                    binding.Window,
                    update.Question,
                    update.Answer);
                return true;
            }
        }

        internal bool CommitExchange(
            RealtimeConversationBinding binding,
            RealtimeConversationExchange exchange)
        {
            if (binding == null || exchange == null) return false;
            lock (gate)
            {
                if (!MatchesActiveSession(binding) ||
                    !string.Equals(
                        binding.SessionId,
                        exchange.SessionId,
                        StringComparison.Ordinal) ||
                    exchange.TurnSequence <= 0 ||
                    exchange.TurnSequence < latestTurnSequence ||
                    (exchange.TurnSequence == latestTurnSequence &&
                     exchange.Revision < latestTurnRevision))
                {
                    return false;
                }
                var question = (exchange.Question ?? "").Trim();
                var answer = (exchange.Answer ?? "").Trim();
                var turnId = (exchange.TurnId ?? "").Trim();
                if (question.Length == 0 ||
                    answer.Length == 0 ||
                    turnId.Length == 0)
                {
                    return false;
                }
                if (!exchange.Persist)
                {
                    if (draft != null &&
                        draft.TurnSequence == exchange.TurnSequence)
                    {
                        draft = null;
                    }
                    latestTurnSequence = Math.Max(
                        latestTurnSequence,
                        exchange.TurnSequence);
                    latestTurnRevision = Math.Max(
                        latestTurnRevision,
                        exchange.Revision);
                    return true;
                }

                var window = binding.Window;
                var deliveryId =
                    "realtime:" + binding.SessionId + ":" + turnId;
                var current = windows[window].turns ??
                              new RealtimeConversationTurn[0];
                foreach (var existing in current)
                {
                    if (existing != null &&
                        string.Equals(
                            existing.deliveryId,
                            deliveryId,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                var next = new List<RealtimeConversationTurn>();
                foreach (var existing in current)
                {
                    if (existing != null) next.Add(existing.Clone());
                }
                next.Add(new RealtimeConversationTurn(
                    question,
                    answer,
                    DateTime.Now.ToString(
                        "o",
                        CultureInfo.InvariantCulture),
                    deliveryId));
                while (next.Count > MaximumTurnsPerWindow)
                {
                    next.RemoveAt(0);
                }

                var previous = windows[window].turns;
                windows[window].turns = next.ToArray();
                if (!TryPersist())
                {
                    windows[window].turns = previous;
                    return false;
                }
                if (draft != null &&
                    draft.TurnSequence == exchange.TurnSequence)
                {
                    draft = null;
                }
                latestTurnSequence = Math.Max(
                    latestTurnSequence,
                    exchange.TurnSequence);
                latestTurnRevision = Math.Max(
                    latestTurnRevision,
                    exchange.Revision);
                return true;
            }
        }

        internal bool ClearActiveWindow(bool destructiveMutationAllowed)
        {
            lock (gate)
            {
                if (!destructiveMutationAllowed ||
                    activeSessionGeneration > 0)
                {
                    return false;
                }
                var window = NormalizeWindow(activeWindow);
                var previous = windows[window].turns;
                windows[window].turns =
                    new RealtimeConversationTurn[0];
                if (TryPersist()) return true;
                windows[window].turns = previous;
                return false;
            }
        }

        internal bool EndSession(RealtimeConversationBinding binding)
        {
            if (binding == null) return false;
            lock (gate)
            {
                if (!MatchesActiveSession(binding)) return false;
                activeSessionId = "";
                activeSessionGeneration = 0;
                activeSessionWindow = -1;
                activeSessionWindowId = "";
                latestTurnSequence = 0;
                latestTurnRevision = -1;
                draft = null;
                return true;
            }
        }

        private bool MatchesActiveSession(
            RealtimeConversationBinding binding)
        {
            return binding != null &&
                   activeSessionGeneration > 0 &&
                   binding.SessionGeneration == activeSessionGeneration &&
                   binding.Window == activeSessionWindow &&
                   string.Equals(
                       binding.SessionId,
                       activeSessionId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       binding.WindowId,
                       activeSessionWindowId,
                       StringComparison.Ordinal) &&
                   binding.Window >= 0 &&
                   binding.Window < windows.Count &&
                   string.Equals(
                       binding.WindowId,
                       windows[binding.Window].id,
                       StringComparison.Ordinal);
        }

        private bool TryPersist()
        {
            if (!persistChanges) return true;
            if (writeBlocked) return false;
            try
            {
                var data = new RealtimeConversationData
                {
                    version = 1,
                    activeWindow = activeWindow,
                    windows = CloneWindows(windows)
                };
                AtomicFile.WriteAllText(
                    StorePath,
                    Serializer().Serialize(data),
                    Encoding.UTF8,
                    true);
                return true;
            }
            catch (Exception ex)
            {
                DebugLog.Write(
                    "realtime conversation store save failed type=" +
                    ex.GetType().Name);
                return false;
            }
        }

        private void ApplyData(RealtimeConversationData data)
        {
            windows.Clear();
            if (data != null && data.windows != null)
            {
                foreach (var window in data.windows)
                {
                    windows.Add(CloneWindow(window));
                }
            }
            EnsureThreeWindows();
            activeWindow = data == null
                ? 0
                : Math.Max(
                    0,
                    Math.Min(
                        MaximumWindows - 1,
                        data.activeWindow));
        }

        private void EnsureThreeWindows()
        {
            while (windows.Count < MaximumWindows)
            {
                windows.Add(NewWindow(windows.Count));
            }
            if (windows.Count > MaximumWindows)
            {
                windows.RemoveRange(
                    MaximumWindows,
                    windows.Count - MaximumWindows);
            }
            for (var index = 0; index < windows.Count; index++)
            {
                if (windows[index] == null)
                {
                    windows[index] = NewWindow(index);
                }
                if (string.IsNullOrWhiteSpace(windows[index].id))
                {
                    windows[index].id = Guid.NewGuid().ToString("N");
                }
                if (string.IsNullOrWhiteSpace(windows[index].name))
                {
                    windows[index].name =
                        (index + 1).ToString(
                            CultureInfo.InvariantCulture);
                }
                if (windows[index].color == 0)
                {
                    windows[index].color =
                        DefaultWindowColor(index).ToArgb();
                }
                if (windows[index].turns == null)
                {
                    windows[index].turns =
                        new RealtimeConversationTurn[0];
                }
            }
        }

        private int NormalizeWindow(int window)
        {
            if (window < 0 || window >= MaximumWindows) return 0;
            return window;
        }

        private static RealtimeConversationWindowData NewWindow(int index)
        {
            return new RealtimeConversationWindowData
            {
                id = Guid.NewGuid().ToString("N"),
                name = (index + 1).ToString(
                    CultureInfo.InvariantCulture),
                color = DefaultWindowColor(index).ToArgb(),
                turns = new RealtimeConversationTurn[0]
            };
        }

        private static Color DefaultWindowColor(int index)
        {
            var colors = new[]
            {
                Color.FromArgb(109, 40, 217),
                Color.FromArgb(126, 34, 206),
                Color.FromArgb(88, 28, 135)
            };
            return colors[Math.Abs(index) % colors.Length];
        }

        private static RealtimeConversationData ReadAndValidate(
            string path)
        {
            var data = Serializer().Deserialize<RealtimeConversationData>(
                File.ReadAllText(path, Encoding.UTF8));
            if (data == null ||
                data.version != 1 ||
                data.windows == null ||
                data.windows.Length == 0 ||
                data.windows.Length > MaximumWindows)
            {
                throw new InvalidDataException(
                    "Realtime conversation data envelope is invalid.");
            }
            if (data.activeWindow < 0 ||
                data.activeWindow >= MaximumWindows)
            {
                throw new InvalidDataException(
                    "Realtime conversation active window is invalid.");
            }
            var windowIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var window in data.windows)
            {
                if (window == null ||
                    string.IsNullOrWhiteSpace(window.id) ||
                    !windowIds.Add(window.id) ||
                    window.turns == null ||
                    window.turns.Length > MaximumTurnsPerWindow)
                {
                    throw new InvalidDataException(
                        "Realtime conversation window is invalid.");
                }
                var deliveryIds =
                    new HashSet<string>(StringComparer.Ordinal);
                foreach (var turn in window.turns)
                {
                    if (turn == null ||
                        string.IsNullOrWhiteSpace(turn.question) ||
                        string.IsNullOrWhiteSpace(turn.answer) ||
                        string.IsNullOrWhiteSpace(turn.deliveryId) ||
                        !deliveryIds.Add(turn.deliveryId))
                    {
                        throw new InvalidDataException(
                            "Realtime conversation turn is invalid.");
                    }
                }
            }
            return data;
        }

        private static RealtimeConversationWindowData[] CloneWindows(
            List<RealtimeConversationWindowData> source)
        {
            var result =
                new RealtimeConversationWindowData[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                result[index] = CloneWindow(source[index]);
            }
            return result;
        }

        private static RealtimeConversationWindowData CloneWindow(
            RealtimeConversationWindowData source)
        {
            if (source == null) return NewWindow(0);
            return new RealtimeConversationWindowData
            {
                id = source.id ?? "",
                name = source.name ?? "",
                color = source.color,
                turns = CloneTurns(source.turns)
            };
        }

        private static RealtimeConversationTurn[] CloneTurns(
            RealtimeConversationTurn[] source)
        {
            if (source == null || source.Length == 0)
            {
                return new RealtimeConversationTurn[0];
            }
            var result = new RealtimeConversationTurn[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                result[index] = source[index] == null
                    ? new RealtimeConversationTurn()
                    : source[index].Clone();
            }
            return result;
        }

        internal static RealtimeConversationTurn[] SelectContext(
            RealtimeConversationTurn[] source)
        {
            var selectedNewestFirst =
                new List<RealtimeConversationTurn>();
            var characters = 0;
            source = source ?? new RealtimeConversationTurn[0];
            for (var index = source.Length - 1;
                 index >= 0 &&
                 selectedNewestFirst.Count <
                 RealtimeConversationBinding.MaximumHistoryTurns;
                 index--)
            {
                var turn = source[index];
                if (turn == null ||
                    string.IsNullOrWhiteSpace(turn.question) ||
                    string.IsNullOrWhiteSpace(turn.answer))
                {
                    continue;
                }
                var turnCharacters =
                    turn.question.Trim().Length +
                    turn.answer.Trim().Length;
                if (characters + turnCharacters >
                    RealtimeConversationBinding.MaximumHistoryCharacters)
                {
                    break;
                }
                selectedNewestFirst.Add(turn.Clone());
                characters += turnCharacters;
            }
            selectedNewestFirst.Reverse();
            return selectedNewestFirst.ToArray();
        }

        private static string PreserveCorruptCopy(string path)
        {
            try
            {
                if (!File.Exists(path)) return "";
                var destination =
                    path + ".corrupt-" +
                    DateTime.Now.ToString(
                        "yyyyMMdd-HHmmss",
                        CultureInfo.InvariantCulture);
                File.Copy(path, destination, false);
                return destination;
            }
            catch
            {
                return "";
            }
        }

        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue
            };
        }

        private string StorePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    AppInfo.InstallFolder,
                    "realtime-conversations.json");
            }
        }
    }

    internal sealed class RealtimeConversationWidgetForm :
        AppleWidgetForm
    {
        internal const int DraftRefreshIntervalMilliseconds = 500;

        private readonly RealtimeConversationStore store;
        private readonly FlowLayoutPanel list;
        private readonly RoundButton[] windowButtons =
            new RoundButton[RealtimeConversationStore.MaximumWindows];
        private readonly TextBox titleBox;
        private readonly RoundButton clearButton;
        private readonly Label headerLabel;
        private readonly ToolTip toolTip = new ToolTip();
        private readonly System.Windows.Forms.Timer draftRefreshTimer;
        private Localizer i18n;
        private bool conversationActive;

        internal RealtimeConversationWidgetForm(
            RealtimeConversationStore store,
            Localizer i18n)
            : base(
                "Realtime voice",
                new Size(460, 560),
                new Point(928, 120))
        {
            if (store == null) throw new ArgumentNullException("store");
            this.store = store;
            this.i18n = i18n;

            headerLabel = HeaderLabel("", 82, 20);
            headerLabel.Size = new Size(350, 34);
            Controls.Add(headerLabel);

            for (var index = 0;
                 index < RealtimeConversationStore.MaximumWindows;
                 index++)
            {
                var window = index;
                var button = SmallButton(
                    (index + 1).ToString(
                        CultureInfo.InvariantCulture),
                    22,
                    112 + index * 42);
                button.Click += delegate
                {
                    store.SetActiveWindow(window);
                    RefreshContent();
                };
                windowButtons[index] = button;
                Controls.Add(button);
            }

            titleBox = new TextBox
            {
                Location = new Point(82, 70),
                Size = new Size(276, 28),
                Font = new Font(
                    "Segoe UI",
                    12F,
                    FontStyle.Bold),
                ForeColor = Color.FromArgb(49, 18, 75),
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(253, 250, 255),
                Anchor =
                    AnchorStyles.Top |
                    AnchorStyles.Left |
                    AnchorStyles.Right
            };
            titleBox.Leave += delegate { SaveTitle(); };
            titleBox.KeyDown += delegate(
                object sender,
                KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                SaveTitle();
                e.SuppressKeyPress = true;
                list.Focus();
            };
            Controls.Add(titleBox);

            clearButton = SmallButton("x", 404, 68);
            clearButton.Anchor =
                AnchorStyles.Top | AnchorStyles.Right;
            clearButton.BaseColor = Color.FromArgb(250, 245, 255);
            clearButton.BackColor = clearButton.BaseColor;
            clearButton.ForeColor = Color.FromArgb(107, 33, 168);
            clearButton.Click += delegate { ClearCurrentWindow(); };
            Controls.Add(clearButton);

            list = new FlowLayoutPanel
            {
                Location = new Point(82, 104),
                Size = new Size(350, 420),
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Anchor =
                    AnchorStyles.Top |
                    AnchorStyles.Bottom |
                    AnchorStyles.Left |
                    AnchorStyles.Right
            };
            Controls.Add(list);

            draftRefreshTimer = new System.Windows.Forms.Timer
            {
                Interval = DraftRefreshIntervalMilliseconds
            };
            draftRefreshTimer.Tick += delegate
            {
                draftRefreshTimer.Stop();
                RefreshContent();
            };

            ApplyLocalizer(i18n);
            RefreshContent();
        }

        internal void ApplyLocalizer(Localizer value)
        {
            if (value == null) return;
            i18n = value;
            const string titleKey = "realtime_title";
            Text = i18n.T(titleKey);
            headerLabel.Text = i18n.T(titleKey);
            toolTip.SetToolTip(
                clearButton,
                i18n.T("clear_realtime_tooltip"));
            RefreshContent();
        }

        internal void SetConversationActive(bool active)
        {
            conversationActive = active;
            clearButton.Enabled = !active;
            clearButton.BaseColor = active
                ? Color.FromArgb(241, 245, 249)
                : Color.FromArgb(250, 245, 255);
            clearButton.BackColor = clearButton.BaseColor;
            clearButton.ForeColor = active
                ? Color.FromArgb(148, 163, 184)
                : Color.FromArgb(107, 33, 168);
            clearButton.Invalidate();
        }

        internal void ScheduleDraftRefresh()
        {
            if (IsDisposed) return;
            if (!draftRefreshTimer.Enabled)
            {
                draftRefreshTimer.Start();
            }
        }

        internal void RefreshContent()
        {
            var active = store.ActiveWindow;
            if (!titleBox.Focused)
            {
                titleBox.Text = store.GetWindowName(active);
            }
            titleBox.Width =
                Math.Max(
                    160,
                    clearButton.Left - titleBox.Left - 12);
            for (var index = 0;
                 index < RealtimeConversationStore.MaximumWindows;
                 index++)
            {
                var color = store.GetWindowColor(index);
                windowButtons[index].BaseColor =
                    index == active
                        ? color
                        : Blend(color, Color.White, 0.58f);
                windowButtons[index].ForeColor =
                    index == active
                        ? Color.White
                        : Color.FromArgb(49, 18, 75);
                windowButtons[index].BorderColor =
                    index == active
                        ? Blend(color, Color.Black, 0.15f)
                        : Blend(color, Color.White, 0.15f);
                windowButtons[index].Invalidate();
            }

            var turns = store.SnapshotWindow(active);
            var currentDraft =
                store.SnapshotDraftForWindow(active);
            list.SuspendLayout();
            list.Controls.Clear();
            if (currentDraft != null &&
                (!string.IsNullOrWhiteSpace(currentDraft.Question) ||
                 !string.IsNullOrWhiteSpace(currentDraft.Answer)))
            {
                AddTurnCards(
                    currentDraft.Question,
                    currentDraft.Answer,
                    true);
            }
            for (var index = turns.Length - 1; index >= 0; index--)
            {
                var turn = turns[index];
                if (turn == null) continue;
                AddTurnCards(
                    turn.question,
                    turn.answer,
                    false);
            }
            list.ResumeLayout();
        }

        private void AddTurnCards(
            string question,
            string answer,
            bool isDraft)
        {
            if (!string.IsNullOrWhiteSpace(question))
            {
                list.Controls.Add(
                    CreateMessageCard(
                        i18n.T("chat_input_prefix") +
                        question.Trim(),
                        question.Trim(),
                        true,
                        isDraft));
            }
            if (!string.IsNullOrWhiteSpace(answer))
            {
                list.Controls.Add(
                    CreateMessageCard(
                        i18n.T("chat_output_prefix") +
                        answer.Trim(),
                        answer.Trim(),
                        false,
                        isDraft));
            }
        }

        private Control CreateMessageCard(
            string displayText,
            string copyText,
            bool question,
            bool isDraft)
        {
            var width = Math.Max(
                240,
                list.ClientSize.Width - 18);
            var labelWidth = width - 30;
            var measured = TextRenderer.MeasureText(
                string.IsNullOrWhiteSpace(displayText)
                    ? " "
                    : displayText,
                Font,
                new Size(labelWidth, 100000),
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding);
            var panel = new RoundedPanel
            {
                Size = new Size(
                    width,
                    Math.Max(42, measured.Height + 28)),
                Margin = new Padding(0, 0, 0, 6),
                BackColor = question
                    ? Color.FromArgb(243, 232, 255)
                    : Color.FromArgb(250, 245, 255),
                BorderColor = isDraft
                    ? Color.FromArgb(147, 51, 234)
                    : (question
                        ? Color.FromArgb(216, 180, 254)
                        : Color.FromArgb(233, 213, 255)),
                Radius = 14
            };
            var label = new Label
            {
                Text = displayText,
                Location = new Point(14, 12),
                Size = new Size(
                    width - 30,
                    panel.Height - 24),
                AutoEllipsis = false,
                ForeColor = Color.FromArgb(49, 18, 75),
                Font = isDraft
                    ? new Font(
                        Font,
                        FontStyle.Italic)
                    : Font
            };
            var copyMenu = CreateCopyMenu(copyText);
            panel.ContextMenuStrip = copyMenu;
            label.ContextMenuStrip = copyMenu;
            panel.Controls.Add(label);
            return panel;
        }

        private ContextMenuStrip CreateCopyMenu(string text)
        {
            var menu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                AutoSize = true
            };
            var item = new ToolStripMenuItem(i18n.T("copy"))
            {
                Enabled = !string.IsNullOrEmpty(text)
            };
            item.Click += delegate
            {
                if (!string.IsNullOrEmpty(text))
                {
                    Clipboard.SetText(text);
                }
            };
            menu.Items.Add(item);
            return menu;
        }

        private void SaveTitle()
        {
            store.SetWindowName(
                store.ActiveWindow,
                titleBox.Text);
            RefreshContent();
        }

        private void ClearCurrentWindow()
        {
            if (conversationActive) return;
            var result = MessageBox.Show(
                this,
                i18n.T("clear_realtime"),
                AppInfo.DisplayName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;
            if (store.ClearActiveWindow(!conversationActive))
            {
                RefreshContent();
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            RefreshContent();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (list != null) RefreshContent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (draftRefreshTimer != null)
                {
                    draftRefreshTimer.Stop();
                    draftRefreshTimer.Dispose();
                }
                toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        private static Label HeaderLabel(
            string text,
            int x,
            int y)
        {
            return new Label
            {
                Text = text,
                UseMnemonic = false,
                Font = new Font(
                    "Segoe UI",
                    17F,
                    FontStyle.Bold),
                ForeColor = Color.FromArgb(49, 18, 75),
                Location = new Point(x, y),
                Size = new Size(280, 34),
                BackColor = Color.Transparent
            };
        }

        private static RoundButton SmallButton(
            string text,
            int x,
            int y)
        {
            var button = new RoundButton
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(34, 34),
                Font = new Font(
                    "Segoe UI",
                    10F,
                    FontStyle.Bold),
                BackColor = Color.FromArgb(250, 245, 255),
                BaseColor = Color.FromArgb(250, 245, 255)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static RoundButton HeaderActionButton(
            string text,
            int x,
            int y)
        {
            var button = SmallButton(text, x, y);
            button.Size = new Size(36, 36);
            button.Font = new Font(
                "Segoe UI Symbol",
                12F,
                FontStyle.Bold);
            return button;
        }

        private static Color Blend(
            Color source,
            Color target,
            float targetAmount)
        {
            targetAmount = Math.Max(
                0f,
                Math.Min(1f, targetAmount));
            var sourceAmount = 1f - targetAmount;
            return Color.FromArgb(
                (int)(source.R * sourceAmount +
                      target.R * targetAmount),
                (int)(source.G * sourceAmount +
                      target.G * targetAmount),
                (int)(source.B * sourceAmount +
                      target.B * targetAmount));
        }
    }
}
