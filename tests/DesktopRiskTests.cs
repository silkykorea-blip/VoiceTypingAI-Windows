using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;

namespace VoiceSecretaryDesktop
{
    internal static class DesktopRiskTests
    {
        private const int SampleRate = 1000;
        private const int BytesPerSample = 2;
        private static int passed;

        private static GeminiFinalTextCoordinator.Decision CompletionPoll(GeminiFinalTextCoordinator c, long time,
            bool rescue = true, string generated = "", int generation = 0, int turns = 0, int stable = 1000, bool failed = false)
        {
            return c.Poll(time, 7, rescue, generated, generation, turns, stable, failed);
        }

        private static void AndroidFinalTurnCannotBeReleasedByPriorSignal()
        {
            var c = new GeminiFinalTextCoordinator();
            string reason;
            True(c.Accept(new GeminiFinalTranscriptSignal(1, "first", "first section", true), out reason), "accept first");
            c.ObserveEnd(1, 0); c.ObserveEnd(2, 1000); c.Configure(2, 1000);
            c.ObserveRaw("first section and the final sentence", 1000);
            Equal("wait", CompletionPoll(c, 4500, true, "first section", 1, 1).Action, "prior completion is not final permission");
            Equal("wait", CompletionPoll(c, 4500, true, "first section", 2, 2).Action, "old text with new completion is not fresh");
            Equal("rescue", CompletionPoll(c, 6000, true, "first section", 1, 1).Action, "old events cannot bypass rescue");
            Equal("first section and the final sentence", CompletionPoll(c, 11000).Text, "deadline keeps latest full raw");
        }

        private static void AndroidFunctionIsImmediateAndProtectsTail()
        {
            foreach (var truncated in new[] { false, true })
            {
                var c = new GeminiFinalTextCoordinator();
                c.Configure(1, 1000); c.ObserveEnd(1, 1000);
                c.ObserveRaw("I want bananas and apples.", 1000);
                string reason;
                c.Accept(new GeminiFinalTranscriptSignal(1, "f", truncated ? "I want bananas" : "I want bananas and apples.", true), out reason);
                var answer = CompletionPoll(c, 1001);
                Equal("deliver", answer.Action, "no generationComplete or stability wait for a valid function");
                Equal("I want bananas and apples.", answer.Text, "complete final sentence retained");
                Equal("wait", CompletionPoll(c, 1002).Action, "delivery claimed only once");
            }
        }

        private static void AndroidRescueWaitsForStableRawAndRejectsStaleResults()
        {
            var c = new GeminiFinalTextCoordinator();
            c.ObserveEnd(1, 0); c.Configure(1, 0);
            c.ObserveRaw("first transcript", 4900);
            Equal("wait", CompletionPoll(c, 5000).Action, "raw not stable at five seconds");
            var rescue = CompletionPoll(c, 5500);
            Equal("rescue", rescue.Action, "one rescue after 600ms raw stability");
            Equal("g7t1e0", rescue.Nonce, "nonce includes the physical activityEnd time");
            string reason;
            True(!c.Accept(new GeminiFinalTranscriptSignal(1, "late", "late primary", true), out reason), "late primary cannot overwrite rescue");
            c.ObserveRaw("first transcript with a newly arrived ending", 5501);
            True(!c.AcceptRescue(new GeminiTranscriptRescueSignal("r", rescue.Nonce, "first transcript")), "old rescue snapshot rejected");
            Equal("wait", CompletionPoll(c, 6500).Action, "never launch a second rescue");
            Equal("first transcript with a newly arrived ending", CompletionPoll(c, 10000, true, "short old prefix").Text, "hard deadline never prefers stale generated prefix");
        }

        private static void AndroidRescueAcceptedTextCanBeSupersededBeforeDelivery()
        {
            var c = new GeminiFinalTextCoordinator();
            c.Configure(1, 0); c.ObserveEnd(1, 0); c.ObserveRaw("raw", 0);
            var rescue = CompletionPoll(c, 5000);
            True(c.AcceptRescue(new GeminiTranscriptRescueSignal("r", rescue.Nonce, "Clean raw.")), "rescue accepted");
            c.ObserveRaw("raw and its final clause", 5100);
            Equal("wait", CompletionPoll(c, 5101).Action, "unpublished stale rescue cannot release");
            Equal("raw and its final clause", CompletionPoll(c, 10000).Text, "latest raw wins");
        }

        private static void AndroidTranslationNeverReturnsSourceLanguageFallback()
        {
            var c = new GeminiFinalTextCoordinator();
            c.Configure(1, 0); c.ObserveEnd(1, 0); c.ObserveRaw("source language", 0);
            Equal("wait", CompletionPoll(c, 6000, false).Action, "translation has no raw rescue");
            Equal("error", CompletionPoll(c, 15000, false).Action, "do not paste raw as a translation");
            c = new GeminiFinalTextCoordinator(); c.Configure(1, 0); c.ObserveRaw(new string('x',100),0);
            string reason;
            c.Accept(new GeminiFinalTranscriptSignal(1, "translation", "译文", true), out reason);
            Equal("译文", CompletionPoll(c, 1, false).Text, "short translation remains translated");
        }

        private static void AndroidCancellationAndDuplicateCallsCannotReplaceText()
        {
            var c = new GeminiFinalTextCoordinator(); c.Configure(1,0); c.ObserveEnd(1,0);
            string reason;
            var original = new GeminiFinalTranscriptSignal(1,"f","complete original",true);
            True(c.Accept(original,out reason),"accept");
            True(c.Accept(original,out reason),"identical duplicate is idempotent");
            True(!c.Accept(new GeminiFinalTranscriptSignal(1,"f2","short",true),out reason),"same turn cannot overwrite full result");
            c.Cancel("f");
            Equal("wait",CompletionPoll(c,1).Action,"cancelled unpublished text is not deliverable");
            True(c.RecordRejection(1),"one corrective retry");
            True(!c.RecordRejection(1),"bounded at two rejections");
        }

        private static void AndroidProviderTailRequiresFreshTextAndActualFinalCompletion()
        {
            var c = new GeminiFinalTextCoordinator(); c.Configure(1,0); c.ObserveEnd(1,0);
            Equal("wait",CompletionPoll(c,1000,false,"complete",0,0).Action,"nonempty delta is not final");
            Equal("wait",CompletionPoll(c,1100,false,"complete",1,0).Action,"start tail at final generation");
            Equal("wait",CompletionPoll(c,1599,false,"complete",1,0).Action,"500ms minimum on unstructured route");
            Equal("wait",CompletionPoll(c,1600,false,"complete",1,0,599).Action,"600ms text stability");
            Equal("complete",CompletionPoll(c,1600,false,"complete",1,0,600).Text,"release fresh complete text");
        }

        private static void AndroidSessionUsesFinalTurnDespiteOldManualEvent()
        {
            using(var session=new RealtimeSpeechSession(new AppSettings { Provider=AiProvider.GeminiLocalVadCumulative },
                PromptBuilder.LocalVadCumulativeStageOne(false,"English"),false,PromptBuilder.LongRecordingStageTwo(false,"English")))
            {
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var gate=(GeminiLocalVadTurnGate)typeof(RealtimeSpeechSession).GetField("localVadTurnGate",flags).GetValue(session);
                var coordinator=(GeminiFinalTextCoordinator)typeof(RealtimeSpeechSession).GetField("finalText",flags).GetValue(session);
                var oldEvent=(System.Threading.ManualResetEvent)typeof(RealtimeSpeechSession).GetField("finishedSignal",flags).GetValue(session);
                gate.BeginActivityEnd(); gate.CompleteCurrentTurn(); gate.BeginActivityEnd();
                var now=GeminiFinalTextCoordinator.NowMs(); coordinator.ObserveEnd(2,now);
                string reason; coordinator.Accept(new GeminiFinalTranscriptSignal(1,"one","first",true),out reason);
                coordinator.ObserveRaw("first and final",now); oldEvent.Set();
                var work=System.Threading.Tasks.Task.Run(delegate {
                    return (string)typeof(RealtimeSpeechSession).GetMethod("FinishGeminiLocalVad",flags).Invoke(session,new object[]{false});
                });
                True(!work.Wait(100),"old manual event must not release or poll-spin through final turn");
                coordinator.Accept(new GeminiFinalTranscriptSignal(2,"two","first and final",true),out reason);
                True(work.Wait(1000),"new final function releases promptly");
                Equal("first and final",work.Result,"session returns exact final body");
            }
        }

        private static void AndroidPromptAndFunctionFixturesMatch()
        {
            var json=new System.Web.Script.Serialization.JavaScriptSerializer();
            var fixtures=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(FindProjectRoot(),"tests","fixtures","android829-input.json"),Encoding.UTF8));
            foreach(var translation in new[]{false,true})
            {
                var key=translation?"translation":"ordinary";
                Equal(Convert.ToString(fixtures[key+"_realtimeInstruction"]),PromptBuilder.OneStep(translation,"English"),key+" realtime prompt exact Android bytes");
                var basis=PromptBuilder.LocalVadCumulativeStageOne(translation,"English");
                Equal(Convert.ToString(fixtures[key+"_localVadMultiTurnRealtimeInstruction"]),basis,key+" local VAD prompt exact Android bytes");
                Equal(Convert.ToString(fixtures[key+"_longRecordingTextInstruction"]),PromptBuilder.LongRecordingStageTwo(translation,"English"),key+" second-stage prompt exact Android bytes");
                Equal(Convert.ToString(fixtures[key+"_structuredInstruction"]),GeminiFinalTranscriptFunctionProtocol.StructuredSystemInstruction(basis,false,!translation),key+" entire setup instruction");
                Equal(CanonicalJson(fixtures[key+"_tools"]),CanonicalJson(GeminiFinalTranscriptFunctionProtocol.Tools(!translation)),key+" full function schemas and descriptions");
            }
        }

        private static string CanonicalJson(object value)
        {
            var map=value as Dictionary<string,object>;
            if(map!=null)
            {
                var keys=new List<string>(map.Keys); keys.Sort(StringComparer.Ordinal);
                var pieces=new List<string>(); foreach(var key in keys) pieces.Add(CanonicalJson(key)+":"+CanonicalJson(map[key]));
                return "{"+string.Join(",",pieces.ToArray())+"}";
            }
            var array=value as System.Collections.IEnumerable;
            if(array!=null && !(value is string))
            {
                var pieces=new List<string>(); foreach(var item in array) pieces.Add(CanonicalJson(item));
                return "["+string.Join(",",pieces.ToArray())+"]";
            }
            return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(value);
        }

        private static void AndroidChineseParagraphBreaksSurviveDelivery()
        {
            var method=typeof(MainForm).GetMethod("NormalizeGeneratedText",BindingFlags.Static|BindingFlags.NonPublic);
            Equal("第一点\n整理正文\n第二点\n保留句尾",(string)method.Invoke(null,new object[]{"第一点\n整理正文\n第二点\n保留句尾"}),"preserve real newlines in Chinese");
        }


        private static void AndroidWebSocketKeepsUtf8AcrossFragmentBoundaries()
        {
            const string text = "第一点：完整中文和emoji🙂。\n第二点：句尾";
            var bytes = Encoding.UTF8.GetBytes(text);
            for (var split = 1; split < bytes.Length; split++)
            {
                var reader = new WebSocketTextMessage();
                var first = new byte[split]; var last = new byte[bytes.Length-split];
                Buffer.BlockCopy(bytes,0,first,0,first.Length); Buffer.BlockCopy(bytes,split,last,0,last.Length);
                Equal(null,reader.Append(1,false,first),"partial frame never returns text");
                // Idle reads and control frames do not replace the persistent message object.
                Equal(text,reader.Append(0,true,last),"UTF-8 split at byte "+split);
                Equal("next",reader.Append(1,true,Encoding.UTF8.GetBytes("next")),"new message has no old bytes");
            }
        }

        private static void AndroidEmptyFunctionRemainsEmpty()
        {
            var coordinator = new GeminiFinalTextCoordinator();
            coordinator.Configure(1,0); coordinator.ObserveEnd(1,0);
            string reason;
            True(coordinator.Accept(new GeminiFinalTranscriptSignal(1,"silence","",false),out reason),"valid silent activity accepted");
            var result=CompletionPoll(coordinator,1);
            Equal("deliver",result.Action,"silence terminates immediately");
            Equal("",result.Text,"no invented placeholder text");
        }

        public static int Main()
        {
            try
            {
                Run("AndroidEmptyFunctionRemainsEmpty", AndroidEmptyFunctionRemainsEmpty);
                Run("AndroidWebSocketKeepsUtf8AcrossFragmentBoundaries", AndroidWebSocketKeepsUtf8AcrossFragmentBoundaries);
                Run("AndroidFinalTurnCannotBeReleasedByPriorSignal", AndroidFinalTurnCannotBeReleasedByPriorSignal);
                Run("AndroidFunctionIsImmediateAndProtectsTail", AndroidFunctionIsImmediateAndProtectsTail);
                Run("AndroidRescueWaitsForStableRawAndRejectsStaleResults", AndroidRescueWaitsForStableRawAndRejectsStaleResults);
                Run("AndroidRescueAcceptedTextCanBeSupersededBeforeDelivery", AndroidRescueAcceptedTextCanBeSupersededBeforeDelivery);
                Run("AndroidTranslationNeverReturnsSourceLanguageFallback", AndroidTranslationNeverReturnsSourceLanguageFallback);
                Run("AndroidCancellationAndDuplicateCallsCannotReplaceText", AndroidCancellationAndDuplicateCallsCannotReplaceText);
                Run("AndroidProviderTailRequiresFreshTextAndActualFinalCompletion", AndroidProviderTailRequiresFreshTextAndActualFinalCompletion);
                Run("AndroidSessionUsesFinalTurnDespiteOldManualEvent", AndroidSessionUsesFinalTurnDespiteOldManualEvent);
                Run("AndroidPromptAndFunctionFixturesMatch", AndroidPromptAndFunctionFixturesMatch);
                Run("AndroidChineseParagraphBreaksSurviveDelivery", AndroidChineseParagraphBreaksSurviveDelivery);
                Run("Integrated preprocessing keeps a fixed 200ms leading preroll", FixedLeadingPreroll);
                Run("Integrated leading preroll survives chunk phases", LeadingPrerollAcrossPhases);
                Run("Integrated short pause is preserved", ShortPausePreserved);
                Run("long trailing silence discarded", LongTrailingSilenceDiscarded);
                Run("short trailing silence preserved", ShortTrailingSilencePreserved);
                Run("silent capture remains locally empty", SilentCaptureRemainsLocallyEmpty);
                Run("low background noise does not trigger speech", LowBackgroundNoiseDoesNotTriggerSpeech);
                Run("peak helper retains the weak-turn threshold", PeakHelperRetainsThreshold);
                Run("local VAD ignores sparse high-peak noise", LocalVadIgnoresSparseHighPeakNoise);
                Run("fallback WAV path uses legacy Integrated preprocessing", FallbackUsesIntegratedPreprocessing);
                Run("Local VAD fallback WAV uses the exclusive peak filter", LocalVadFallbackUsesExclusivePeakFilter);
                Run("non-local-VAD realtime routes use legacy Integrated preprocessing", NonLocalVadRealtimeRoutesUseIntegratedPreprocessing);
                Run("experimental speed processors are detached from production routes", ExperimentalSpeedProcessorsAreDetached);
                Run("OpenAI transcript uses instructions field", OpenAiInstructionSeparation);
                Run("Gemini transcript uses systemInstruction", GeminiInstructionSeparation);
                Run("realtime best-effort text remains readable", BestEffortRealtimeText);
                Run("legacy Gemini long setting migrates to Gemini model", LegacyGeminiLongSettingMigratesToGeminiModel);
                Run("legacy OpenAI long setting migrates to OpenAI model", LegacyOpenAiLongSettingMigratesToOpenAiModel);
                Run("provider choices are limited to Gemini and OpenAI", ProviderChoicesAreLimitedAndLegacySafe);
                Run("Windows input never requests result readback", TypingPlaybackSetupUsesPuckVoice);
                Run("structured transcript function protocol mirrors Android", StructuredTranscriptFunctionProtocolContract);
                Run("local VAD setup registers the structured transcript function", LocalVadSetupRegistersStructuredFunction);
                Run("Gemini local VAD base prompts exclude the retired cross-turn paragraph", GeminiLocalVadPromptsPreventCrossTurnRestatement);
                Run("Gemini activityStart carries no extra prompt", GeminiLocalVadSubsequentTurnPromptIsBoundToActivityStart);
                Run("Gemini cumulative activityStart matches Android", GeminiLocalVadCumulativePromptIsBoundToActivityStart);
                Run("Gemini local VAD cumulative mode keeps only the newest full turn", GeminiLocalVadCumulativeKeepsOnlyNewestTurn);
                Run("Gemini local VAD uses the PCM16 peak 2000 and 500ms boundary", GeminiLocalVadUsesUnifiedPeakAndSilenceBoundary);
                Run("Gemini local VAD preserves low audio before a complete boundary", GeminiLocalVadKeepsAsymmetricProtection);
                Run("Gemini local VAD splits at the lowest 20ms frame", GeminiLocalVadRollsFrontProtectionUntilSpeech);
                Run("Gemini local VAD lossless filtering is invariant across input chunks", GeminiLocalVadFilteringIsChunkInvariant);
                Run("Gemini local VAD uses strict staged 10/15/30 second turns", GeminiLocalVadUsesStrictBoundaries);
                Run("Gemini local VAD caches the post-cut prefix until active audio", GeminiLocalVadAssignsProtectionAcrossStrictBoundary);
                Run("Gemini local VAD requires continuous 500ms low peak audio", GeminiLocalVadRequiresContinuousFiveHundredMs);
                Run("Gemini local VAD preserves all audio across valid turns", GeminiLocalVadNeverHardCutsAndResets);
                Run("Gemini local VAD setup uses manual VAD and controlled interruption", GeminiLocalVadSetupUsesControlledInterruption);
                Run("Gemini local VAD outbound order is start audio end", GeminiLocalVadOutboundOrder);
                Run("Gemini local VAD discards an unclaimed low-peak final tail", GeminiLocalVadDiscardsSubSecondFinalTurn);
                Run("Gemini local VAD releases an active short tail like Android", GeminiLocalVadReleasesActiveShortFinalTurn);
                Run("Gemini local VAD releases any tail containing an active frame", GeminiLocalVadReleasesActiveTailLikeAndroid);
                Run("Gemini local VAD observed short tails reach the network", GeminiLocalVadObservedShortTailsReachNetwork);
                Run("Gemini local VAD fallback preserves original-speed PCM", GeminiLocalVadWeakTurnTimingUsesOriginalSpeedAudio);
                Run("Gemini local VAD gate releases only activityStart after generationComplete", GeminiLocalVadTurnGateBuffersUntilComplete);
                Run("Gemini local VAD capture is long-form Gemini audio", GeminiLocalVadCaptureConfiguration);
                Run("Gemini local VAD generationComplete advances the next cumulative turn safely", GeminiLocalVadIntermediateTurnsAccumulate);
                Run("Gemini local VAD has no retired delta-output branch", GeminiLocalVadGeneratedTurnBoundaryIsReadable);
                Run("Gemini local VAD final completion rejects stale generation signals", GeminiLocalVadFinalCompletionRejectsStaleGeneration);
                Run("Gemini local VAD final text tail is adaptive and bounded", GeminiLocalVadFinalTextTailIsAdaptiveAndBounded);
                Run("Gemini local VAD generationComplete cannot release audio directly", GeminiLocalVadGenerationCompleteDoesNotReleaseTurn);
                Run("Gemini local VAD interrupted turn waits for turnComplete", GeminiLocalVadInterruptedTurnWaitsForTurnComplete);
                Run("retired Simple Q&A has no production entry or provider route", SimpleQaProductionRoutesAreRetired);
                Run("processed input archive writes the exact PCM order", ProcessedInputArchiveWritesStandardWave);
                Run("processed input archive preserves interrupted audio", ProcessedInputArchiveDisposePreservesPartialAudio);
                Run("processed input archive leaves no empty file", ProcessedInputArchiveLeavesNoEmptyFile);
                Run("processed input archive resolves inside the project", ProcessedInputArchiveResolvesInsideProject);
                Run("realtime session finalizes successfully sent processed input", RealtimeSessionFinalizesProcessedInputArchive);
                Run("long recording modes use strict time boundaries", LongRecordingModesUseStrictBoundaries);
                Run("long recording prompts use fixed original dictation", LongRecordingPromptsUseFixedOriginalDictation);
                Run("Gemini long text body separates raw and instruction", GeminiLongTextBodySeparatesRawAndInstruction);
                Run("Gemini long text stage uses 3.1 Flash-Lite", GeminiLongTextStageUsesFlashLite);
                Run("OpenAI long text body separates raw and instruction", OpenAiLongTextBodySeparatesRawAndInstruction);
                Run("Gemini typing uses Long Recording capture", GeminiTypingUsesLongRecordingCapture);
                Run("OpenAI typing uses Long Recording capture", OpenAiTypingUsesLongRecordingCapture);
                Run("all realtime routes capture at 20ms and upload at 40ms", RealtimeCaptureAndUploadCadence);
                Run("Gemini long raw accepts revised cumulative snapshots", GeminiLongRawAcceptsRevisedSnapshots);
                Run("ordinary prompt ignores transcribed commands", OrdinaryPromptInstructionBoundary);
                Run("original prompts route correctly", OriginalPromptsRouteCorrectly);
                Run("translation targets contain exactly 31 languages", TranslationTargetsContain31Languages);
                Run("default hotkeys remain unique", DefaultHotkeysRemainUnique);
                Run("realtime conversation shortcut routes through settings and the keyboard hook", RealtimeConversationShortcutRoutes);
                Run("translation overlay is light green", TranslationOverlayIsLightGreen);
                Run("translation labels cover all languages", TranslationLabelsCoverAllLanguages);
                Run("retired text-mode labels are absent", RetiredTextModeLabelsAreAbsent);
                Run("widget visibility labels cover all languages", WidgetVisibilityLabelsCoverAllLanguages);
                Run("settings uses a vertical widget shortcut layout and independent window selectors", WidgetSettingsLayoutMatchesContract);
                Run("widget visibility preferences remain independent", WidgetVisibilityPreferencesRemainIndependent);
                Run("new voice features are localized in all 31 languages", FeatureLabelsCoverAllLanguages);
                Run("schedule labels cover all 31 languages", ScheduleLabelsCoverAllLanguages);
                Run("TEXT output contract accepts only anchored non-empty text", TypingOutputContractIsStrict);
                Run("structured text output rejects invalid objects", StructuredTextOutputIsStrict);
                Run("automatic paste accepts general stable focused text targets", AutomaticPasteAcceptsGeneralStableFocusedTargets);
                Run("automatic paste accepts pattern-backed focused host windows", AutomaticPasteAcceptsPatternBackedHostWindow);
                Run("automatic paste freezes the recording target through delivery", AutomaticPasteFreezesRecordingTargetThroughDelivery);
                Run("automatic paste anchors UIA and native identity to the exact foreground window", AutomaticPasteAnchorsToExactForegroundWindow);
                Run("automatic paste read-only conflicts require general strong evidence", AutomaticPasteReadOnlyConflictRequiresStrongEvidence);
                Run("automatic paste UIA inspection is single-flight and fail-closed", AutomaticPasteAutomationWorkerIsSingleFlight);
                Run("automatic paste waits for physical modifier release without synthesis", AutomaticPasteModifierPolicyIsSafe);
                Run("automatic paste rejects unsafe targets and stale focus", AutomaticPasteRejectsUnsafeTargetsAndStaleFocus);
                Run("automatic paste injects one verified Ctrl+V batch and diagnoses outcomes", AutomaticPasteInjectionContract);
                Run("local schedule store sorts and deduplicates safely", ScheduleStoreSortsAndDeduplicates);
                Run("realtime tool protocols cover both providers", RealtimeToolProtocolsCoverBothProviders);
                Run("realtime silence and speaker gating remain stable", RealtimeSilenceAndSpeakerGateMatchContract);
                Run("retired wake-word helper is not packaged", WakeWordHelperIsNotPackaged);
                Run("retired settings surfaces are absent", RetiredSettingsSurfacesAreAbsent);
                Run("realtime conversation protocol supports Gemini and OpenAI", RealtimeConversationProtocolSupportsBothProviders);
                Run("realtime histories have three windows capped at fifty turns and 20000 context characters", RealtimeHistoryHasThreeWindowsCapsFiftyAndContextCharacters);
                Run("realtime translation protocol surface is absent", RealtimeTranslationProtocolSurfaceIsAbsent);
                Run("Gemini realtime search is opt-in chat-only and fail-closed", GeminiRealtimeSearchIsChatOnlyAndFailClosed);
                Run("realtime completion uses a bounded one-second transcript revision grace", RealtimeRevisionGraceIsOneSecond);
                Run("realtime history binding freezes the third window across UI switches", RealtimeHistoryBindingFreezesThirdWindow);
                Run("realtime history protocols seed Gemini and OpenAI as text", RealtimeHistoryProtocolsSeedBothProviders);
                Run("Gemini realtime drafts use monotonic revisions and interruption-safe delivery", GeminiRealtimeDraftsAreRevisionSafe);
                Run("OpenAI realtime writeback correlates asynchronous transcripts and tool continuations", OpenAiRealtimeWritebackCorrelatesAsyncTools);
                Run("OpenAI history items and interrupted responses never become current answers", OpenAiHistoryAndInterruptedResponsesAreIsolated);
                Run("realtime writeback rejects old sessions, out-of-order revisions, and duplicates", RealtimeWritebackRejectsOldSessionsAndDuplicates);
                Run("retired realtime translation store is absent and old data stays untouched", RetiredRealtimeTranslationStoreIsAbsent);
                Run("active realtime sessions protect their frozen chat window", RealtimeConversationProtectsFrozenModuleMutation);
                Run("realtime local tools write direct Notes, reject narration, cancel, and end", RealtimeLocalToolsUseNotesAndRespectCancellation);
                Run("OpenAI continuation disables tools and natural drain has no truncate target", OpenAiContinuationAndInterruptionAreAtomic);
                Run("pre-disposed websocket aborts before any connection attempt", WebSocketAbortBeforeConnectIsImmediate);
                Run("recording diagnostics classify failures without content payloads", RecordingDiagnosticsAreSafeAndClassified);
                Run("audio capture follows the ordinary default input while playback keeps the communications device", AudioCaptureUsesDefaultInputPlaybackKeepsCommunications);
                Run("note previews expand up to five lines", NotePreviewsExpandUpToFiveLines);
                Run("display layout keys follow monitor configuration", DisplayLayoutKeysFollowMonitorConfiguration);
                Run("display layout profiles persist independently", DisplayLayoutProfilesPersistIndependently);
                Run("widget visibility animation keeps exact endpoints", WidgetVisibilityAnimationKeepsExactEndpoints);
                Run("widget animation keeps live controls visible", WidgetAnimationKeepsLiveControlsVisible);
                Run("widget animator completes without a second hide", WidgetAnimatorCompletesWithoutSecondHide);
                Run("widget visibility shortcut responds immediately", WidgetVisibilityShortcutRespondsImmediately);
                Run("portable app icon loads without sidecar file", PortableAppIconLoadsWithoutSidecarFile);
                Run("application version updated", VersionUpdated);
                Console.WriteLine("PASS " + passed + "/" + passed);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }

        private static void FixedLeadingPreroll()
        {
            var processor = new RealtimeAudioPreprocessor(SampleRate);
            var output = new List<byte[]>();
            for (var index = 0; index < 4; index++) output.AddRange(processor.Process(Silence(130)));
            output.AddRange(processor.Process(Active(100)));
            Equal(2, output.Count, "expected preroll and active chunks");
            Equal(200 * BytesPerSample, output[0].Length, "leading preroll length");
            Equal(100 * BytesPerSample, output[1].Length, "active length");
        }

        private static void LeadingPrerollAcrossPhases()
        {
            var durations = new[] { 261, 390, 519, 780, 1041 };
            foreach (var duration in durations)
            {
                var processor = new RealtimeAudioPreprocessor(SampleRate);
                var remaining = duration;
                while (remaining > 0)
                {
                    var chunk = Math.Min(37, remaining);
                    processor.Process(Silence(chunk));
                    remaining -= chunk;
                }
                var output = processor.Process(Active(50));
                Equal(2, output.Count, "expected preroll for silence " + duration + "ms");
                Equal(200 * BytesPerSample, output[0].Length, "preroll for silence " + duration + "ms");
            }
        }

        private static void ShortPausePreserved()
        {
            var processor = new RealtimeAudioPreprocessor(SampleRate);
            var first = processor.Process(Active(80));
            Equal(1, first.Count, "initial active chunk");
            Equal(0, processor.Process(Silence(200)).Count, "short silence should remain pending");
            var resumed = processor.Process(Active(80));
            Equal(2, resumed.Count, "short pause and resumed speech");
            Equal(200 * BytesPerSample, resumed[0].Length, "short pause length");
        }

        private static void LongTrailingSilenceDiscarded()
        {
            var processor = new RealtimeAudioPreprocessor(SampleRate);
            processor.Process(Active(80));
            processor.Process(Silence(500));
            Equal(0, processor.Finish().Count, "long trailing silence");
        }

        private static void ShortTrailingSilencePreserved()
        {
            var processor = new RealtimeAudioPreprocessor(SampleRate);
            processor.Process(Active(80));
            processor.Process(Silence(200));
            var tail = processor.Finish();
            Equal(1, tail.Count, "short trailing silence chunk");
            Equal(200 * BytesPerSample, tail[0].Length, "short trailing silence length");
        }

        private static void SilentCaptureRemainsLocallyEmpty()
        {
            var processor = new RealtimeAudioPreprocessor(SampleRate);
            processor.Process(Silence(1200));
            True(!processor.HasDetectedSpeech, "silence must not be treated as speech");
            processor.Process(Active(100));
            True(processor.HasDetectedSpeech, "active audio must be detected");
        }

        private static void LowBackgroundNoiseDoesNotTriggerSpeech()
        {
            var processor = new RealtimeAudioPreprocessor(SampleRate);
            processor.Process(Signal(1000, 100));
            True(!processor.HasDetectedSpeech, "low constant background noise must remain empty");
            processor.Process(Signal(100, 300));
            True(!processor.HasDetectedSpeech, "quiet speech needs confirmation time");
            processor.Process(Signal(100, 300));
            True(processor.HasDetectedSpeech, "confirmed quiet speech must be detected");
        }

        private static void PeakHelperRetainsThreshold()
        {
            Equal(96, RealtimeAudioPreprocessor.ActiveAudioPeakThreshold, "silence clipping peak threshold");
            True(!RealtimeAudioPreprocessor.IsActiveAudio(Signal(20, 95)), "peak 95 must remain silence");
            True(RealtimeAudioPreprocessor.IsActiveAudio(Signal(20, 96)), "peak 96 must remain active audio");
        }

        private static void LocalVadIgnoresSparseHighPeakNoise()
        {
            True(
                Math.Abs(RealtimeAudioPreprocessor.LocalVadActiveAudioRmsThreshold - 0.0055) < 0.0000001,
                "local VAD RMS threshold");
            var impulse = SparseImpulseAtSampleRate(20, 16000, 1500);
            True(RealtimeAudioPreprocessor.IsActiveAudio(impulse), "legacy peak detector should see the impulse");
            True(!RealtimeAudioPreprocessor.IsLocalVadActiveAudio(impulse), "single-sample impulse must remain VAD silence");
            True(
                RealtimeAudioPreprocessor.IsLocalVadActiveAudio(SignalAtSampleRate(20, 16000, 200)),
                "sustained quiet speech must remain VAD active");
        }

        private static void FallbackUsesIntegratedPreprocessing()
        {
            var pcm = Combine(
                Silence(1000),
                Active(100),
                Silence(500));
            var processed = RealtimeAudioFileProcessor.ProcessPcm(pcm, SampleRate, 200);
            Equal(300 * SampleRate / 1000 * BytesPerSample, processed.Length, "fallback preroll plus original-speed speech");
            Equal(0, RealtimeAudioFileProcessor.ProcessPcm(Signal(2000, 100), SampleRate, 74).Length, "fallback background-only audio");
        }

        private static void LocalVadFallbackUsesExclusivePeakFilter()
        {
            const int sampleRate = 16000;
            var pcm = Combine(
                SignalAtSampleRate(100, sampleRate, 5000),
                new byte[1000 * sampleRate / 1000 * BytesPerSample],
                SignalAtSampleRate(100, sampleRate, 5000),
                new byte[1000 * sampleRate / 1000 * BytesPerSample]);
            var processed = RealtimeAudioFileProcessor.ProcessLocalVadPcm(pcm, sampleRate, 74);
            Equal(pcm.Length, processed.Length, "Local VAD fallback must not clip silence around valid speech");
            True(BytesEqual(pcm, processed), "Local VAD fallback must preserve exact PCM order");
            Equal(
                0,
                RealtimeAudioFileProcessor.ProcessLocalVadPcm(
                    SignalAtSampleRate(2000, sampleRate, 1999),
                    sampleRate,
                    74).Length,
                "Local VAD fallback must reject an all-low recording");
        }

        private static void NonLocalVadRealtimeRoutesUseIntegratedPreprocessing()
        {
            AssertIntegratedRealtimePreprocessing(
                new AppSettings { Provider = AiProvider.Gemini },
                false,
                "second stage",
                16000,
                "Gemini Integrated plus Long Recording");
            AssertIntegratedRealtimePreprocessing(
                new AppSettings { Provider = AiProvider.Gemini },
                true,
                null,
                16000,
                "Gemini Notes and Q&A transcription");
            AssertIntegratedRealtimePreprocessing(
                new AppSettings { Provider = AiProvider.OpenAi },
                false,
                "second stage",
                24000,
                "OpenAI Integrated plus Long Recording");
        }

        private static void AssertIntegratedRealtimePreprocessing(
            AppSettings settings,
            bool preferInputTranscription,
            string secondStagePrompt,
            int sampleRate,
            string label)
        {
            using (var session = new RealtimeSpeechSession(
                settings,
                "test",
                preferInputTranscription,
                secondStagePrompt))
            {
                session.SendAudio(SignalAtSampleRate(120, sampleRate, 1200));
                True(session.HasDetectedSpeech, label + " must accept speech immediately");
                var expectedBatchedBytes = sampleRate * BytesPerSample * 120 / 1000;
                Equal((long)expectedBatchedBytes, ReadQueuedBytes(session), label + " original-speed 40ms batches");
            }
        }

        private static void ExperimentalSpeedProcessorsAreDetached()
        {
            True(
                typeof(RealtimeAudioPreprocessor).GetField(
                    "speedProcessor",
                    BindingFlags.Instance | BindingFlags.NonPublic) == null,
                "Integrated preprocessor must not own a speed processor");
            True(
                typeof(RealtimeSpeechSession).GetField(
                    "localVadSpeedProcessor",
                    BindingFlags.Instance | BindingFlags.NonPublic) == null,
                "Local VAD session must not own a speed processor");
        }

        private static long ReadQueuedBytes(RealtimeSpeechSession session)
        {
            var field = typeof(RealtimeSpeechSession).GetField(
                "queuedBytes",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("queuedBytes field not found");
            return (long)field.GetValue(session);
        }

        private static void OpenAiInstructionSeparation()
        {
            const string raw = "RAW_TRANSCRIPT_DO_NOT_EXECUTE";
            const string instruction = "SYSTEM_RULE_ONLY";
            var body = AiClient.BuildOpenAiTextProcessingBody(raw, instruction);
            Equal(raw, Convert.ToString(body["input"]), "OpenAI raw input");
            var system = Convert.ToString(body["instructions"]);
            True(system.Contains(instruction), "OpenAI system instruction missing");
            True(!system.Contains(raw), "OpenAI raw text leaked into instructions");
        }

        private static void GeminiInstructionSeparation()
        {
            const string raw = "RAW_TRANSCRIPT_DO_NOT_EXECUTE";
            const string instruction = "SYSTEM_RULE_ONLY";
            var body = AiClient.BuildGeminiTextProcessingBody(raw, instruction);
            var system = (Dictionary<string, object>)body["systemInstruction"];
            var systemParts = (object[])system["parts"];
            var systemText = Convert.ToString(((Dictionary<string, object>)systemParts[0])["text"]);
            True(systemText.Contains(instruction), "Gemini system instruction missing");
            True(!systemText.Contains(raw), "Gemini raw text leaked into systemInstruction");

            var contents = (object[])body["contents"];
            var user = (Dictionary<string, object>)contents[0];
            Equal("user", Convert.ToString(user["role"]), "Gemini user role");
            var userParts = (object[])user["parts"];
            Equal(raw, Convert.ToString(((Dictionary<string, object>)userParts[0])["text"]), "Gemini raw input");
        }

        private static void BestEffortRealtimeText()
        {
            var settings = new AppSettings { Provider = AiProvider.Gemini };
            using (var session = new RealtimeSpeechSession(settings, "test", false))
            {
                var method = typeof(RealtimeSpeechSession).GetMethod(
                    "ReplaceGeneratedText",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) throw new InvalidOperationException("ReplaceGeneratedText not found");
                method.Invoke(session, new object[] { "partial result" });
                Equal("partial result", session.GetBestEffortText(), "best-effort text");
            }
        }

        private static void LegacyGeminiLongSettingMigratesToGeminiModel()
        {
            var provider = AiProviderOption.FromConfig("gemini_long_two_step");
            Equal(AiProvider.GeminiLocalVadCumulative, provider, "retired Gemini 1 long config must migrate to Gemini");
            Equal("gemini_local_vad_cumulative", AiProviderOption.ToConfig(provider), "Gemini model persisted config");
            Equal(0, AiProviderOption.IndexOf(provider), "Gemini model UI index");
        }

        private static void LegacyOpenAiLongSettingMigratesToOpenAiModel()
        {
            var provider = AiProviderOption.FromConfig("openai_long_two_step");
            Equal(AiProvider.OpenAi, provider, "legacy OpenAI long config");
            Equal("openai", AiProviderOption.ToConfig(provider), "OpenAI model persisted config");
            Equal(1, AiProviderOption.IndexOf(provider), "OpenAI model UI index");
        }

        private static void ProviderChoicesAreLimitedAndLegacySafe()
        {
            Equal(AiProvider.GeminiLocalVadCumulative, AiProviderOption.At(0), "first provider must be Gemini local VAD");
            Equal(AiProvider.OpenAi, AiProviderOption.At(1), "second provider must be OpenAI");
            Equal(
                AiProvider.GeminiLocalVadCumulative,
                AiProviderOption.At(2),
                "retired Gemini 1 index must fall back to Gemini");
            Equal(AiProvider.GeminiLocalVadCumulative, AiProviderOption.At(3), "invalid provider index fallback");
            Equal(
                AiProvider.GeminiLocalVadCumulative,
                AiProviderOption.FromConfig("gemini_local_vad_multi_turn"),
                "retired Multi-turn config must migrate to Gemini 2");
            Equal(
                AiProvider.GeminiLocalVadCumulative,
                AiProviderOption.FromConfig("gemini_local_vad_cumulative"),
                "cumulative provider config");
            Equal(
                "gemini_local_vad_cumulative",
                AiProviderOption.ToConfig(AiProvider.GeminiLocalVadCumulative),
                "cumulative provider persisted config");
            True(AiProviderOption.IsGemini(AiProvider.GeminiLocalVadCumulative), "cumulative provider must use Gemini key and transport");
            True(AiProviderOption.IsGeminiLocalVad(AiProvider.GeminiLocalVadCumulative), "cumulative Local VAD route");
            True(AiProviderOption.IsGeminiLocalVadCumulative(AiProvider.GeminiLocalVadCumulative), "Gemini 2 must be cumulative");
            Equal(
                AiProvider.GeminiLocalVadCumulative,
                AiProviderOption.FromConfig("gemini_vad_integrated"),
                "legacy VAD config migration");
            Equal(
                AiProvider.GeminiLocalVadCumulative,
                AiProviderOption.FromConfig("gemini_vad_integrated_long_recording"),
                "legacy VAD plus Long config migration");
            Equal(
                AiProvider.GeminiLocalVadCumulative,
                AiProviderOption.FromConfig("gemini"),
                "retired bare Gemini 1 config must migrate to Gemini");
        }

        private static void GeminiLocalVadPromptsPreventCrossTurnRestatement()
        {
            var normal = PromptBuilder.RealtimeStageOne(
                AiProvider.GeminiLocalVadCumulative,
                false,
                "Japanese (日本語)");
            var translation = PromptBuilder.RealtimeStageOne(
                AiProvider.GeminiLocalVadCumulative,
                true,
                "Japanese (日本語)");
            var prompts = new[] { normal, translation };

            foreach (var prompt in prompts)
            {
                True(prompt.Contains("像镜子一样"), "fixed dictation mirror role missing");
                True(prompt.Contains("TEXT:"), "TEXT contract missing");
                True(!prompt.Contains("此前已经输出过的内容只能作为上下文，不得再次输出"), "retired cross-turn paragraph remained");
                True(!prompt.Contains("当前轮只输出相对于此前结果新增的内容"), "retired current-turn delta rule remained");
                True(!prompt.Contains("这是新一轮的独立输入"), "per-turn instruction leaked into session system prompt");
                True(!prompt.Contains("去除话语中啰嗦的部分"), "summary-style deletion rule remained");
            }

            True(!normal.Contains("认真理解用户所表达的思想"), "normal dictation must not summarize");
            True(translation.Contains("翻译成Japanese (日本語)"), "translation target missing");
            True(!translation.Contains("{目标语言}"), "translation placeholder remained");

            var existingGemini = PromptBuilder.RealtimeStageOne(
                AiProvider.Gemini,
                false,
                "Japanese (日本語)");
            var existingOpenAi = PromptBuilder.RealtimeStageOne(
                AiProvider.OpenAi,
                false,
                "Japanese (日本語)");
            True(!existingGemini.Contains("此前已经输出过的内容只能作为上下文"), "existing Gemini prompt changed");
            True(!existingOpenAi.Contains("此前已经输出过的内容只能作为上下文"), "existing OpenAI prompt changed");
        }

        private static void GeminiLocalVadSubsequentTurnPromptIsBoundToActivityStart()
        {
            var expected = PromptBuilder.LocalVadSubsequentTurn(
                AiProvider.GeminiLocalVadCumulative);
            True(expected.Contains("完整累计文本"), "subsequent turn must request cumulative text");
            True(expected.Contains("重新阅读本次 Live 会话中此前各轮已经输出的全部文本"), "subsequent turn must reread prior outputs");
            True(expected.Contains("不要只输出本轮新增内容"), "subsequent turn must reject delta-only output");
            True(expected.Contains("TEXT:"), "subsequent turn TEXT contract missing");
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "base prompt",
                false,
                null))
            {
                var method = typeof(RealtimeSpeechSession).GetMethod(
                    "BuildGeminiActivityStartMessage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) throw new InvalidOperationException("activityStart message builder not found");

                var first = (Dictionary<string, object>)method.Invoke(session, new object[] { 1 });
                var firstRealtime = (Dictionary<string, object>)first["realtimeInput"];
                True(firstRealtime.ContainsKey("activityStart"), "first turn activityStart missing");
                True(!firstRealtime.ContainsKey("text"), "first turn must not carry isolation text");

                for (var turn = 2; turn <= 4; turn++)
                {
                    var next = (Dictionary<string, object>)method.Invoke(session, new object[] { turn });
                    var nextRealtime = (Dictionary<string, object>)next["realtimeInput"];
                    True(nextRealtime.ContainsKey("activityStart"), "later turn activityStart missing");
                    True(!nextRealtime.ContainsKey("text"), "later activityStart contains no Windows-only prompt");
                }
            }

            using (var existing = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.Gemini },
                "base prompt",
                false,
                "second stage"))
            {
                var method = typeof(RealtimeSpeechSession).GetMethod(
                    "BuildGeminiActivityStartMessage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var root = (Dictionary<string, object>)method.Invoke(existing, new object[] { 2 });
                var realtime = (Dictionary<string, object>)root["realtimeInput"];
                True(!realtime.ContainsKey("text"), "existing Gemini mode must not receive Local VAD isolation text");
            }
        }

        private static void GeminiLocalVadCumulativePromptIsBoundToActivityStart()
        {
            var expected = PromptBuilder.LocalVadSubsequentTurn(AiProvider.GeminiLocalVadCumulative);
            True(expected.Contains("完整累计文本"), "cumulative prompt must request the full transcript");
            True(expected.Contains("此前各轮的有效内容必须保留"), "cumulative prompt must preserve valid content");
            True(expected.Contains("本轮新增内容必须加入"), "cumulative prompt must add the current turn");
            True(expected.Contains("不增加信息"), "cumulative prompt must forbid invented information");
            True(expected.Contains("TEXT:"), "cumulative prompt final-output contract missing");
            True(!expected.Contains("以上的内容是比较有用的"), "277 pollution sentence must stay absent");
            True(!expected.Contains("绝对不要重复或包含上一轮"), "delta isolation prompt leaked into cumulative mode");
            Equal(
                PromptBuilder.RealtimeStageOne(
                    AiProvider.GeminiLocalVadCumulative,
                    false,
                    "English"),
                PromptBuilder.RealtimeStageOne(
                    AiProvider.GeminiLocalVadCumulative,
                    false,
                    "English"),
                "both Local VAD modes must share the same first-turn system prompt");

            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "base prompt",
                false,
                null))
            {
                var method = typeof(RealtimeSpeechSession).GetMethod(
                    "BuildGeminiActivityStartMessage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) throw new InvalidOperationException("activityStart message builder not found");

                var first = (Dictionary<string, object>)method.Invoke(session, new object[] { 1 });
                var firstRealtime = (Dictionary<string, object>)first["realtimeInput"];
                True(!firstRealtime.ContainsKey("text"), "first cumulative turn must use only the system prompt");

                for (var turn = 2; turn <= 4; turn++)
                {
                    var next = (Dictionary<string, object>)method.Invoke(session, new object[] { turn });
                    var nextRealtime = (Dictionary<string, object>)next["realtimeInput"];
                    True(!nextRealtime.ContainsKey("text"), "cumulative rule lives only in setup, exactly as Android");
                }
            }
        }

        private static void GeminiLocalVadCumulativeKeepsOnlyNewestTurn()
        {
            using (var cumulative = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "test",
                false,
                null))
            {
                var merge = typeof(RealtimeSpeechSession).GetMethod(
                    "MergeGeneratedTranscriptionSnapshot",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var reset = typeof(RealtimeSpeechSession).GetMethod(
                    "ResetGeneratedTextAtCumulativeActivityEnd",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (merge == null || reset == null)
                {
                    throw new InvalidOperationException("cumulative output hooks not found");
                }

                merge.Invoke(cumulative, new object[] { "第一轮完整稿。" });
                reset.Invoke(cumulative, new object[] { 1 });
                Equal("第一轮完整稿。", cumulative.GetBestEffortText(), "first activityEnd must retain the first output");
                reset.Invoke(cumulative, new object[] { 2 });
                Equal("", cumulative.GetBestEffortText(), "second activityEnd establishes the new generated-output boundary");
                merge.Invoke(cumulative, new object[] { "第一轮完整稿。第二轮新增内容。" });
                Equal(
                    "第一轮完整稿。第二轮新增内容。",
                    cumulative.GetBestEffortText(),
                    "cumulative mode must retain only the newest complete rewrite");
            }

        }

        private static void GeminiLocalVadUsesUnifiedPeakAndSilenceBoundary()
        {
            Equal(2000, GeminiLocalVadPeakSilenceFilter.ActiveAudioPeakThreshold, "local VAD peak threshold");
            Equal(500, GeminiLocalVadPeakSilenceFilter.BoundaryLowPeakMs, "low-peak boundary duration");
            Equal(10000, GeminiLocalVadPeakSilenceFilter.FirstTurnMinimumDurationMs, "first-turn minimum");
            Equal(15000, GeminiLocalVadPeakSilenceFilter.SecondTurnMinimumDurationMs, "second-turn minimum");
            Equal(30000, GeminiLocalVadPeakSilenceFilter.LaterTurnMinimumDurationMs, "later-turn minimum");
            True(
                !GeminiLocalVadPeakSilenceFilter.IsActiveAudio(SignalAtSampleRate(20, 16000, 1999)),
                "peak 1999 must remain low audio");
            True(
                GeminiLocalVadPeakSilenceFilter.IsActiveAudio(SignalAtSampleRate(20, 16000, 2000)),
                "peak 2000 must count as active audio");

            var shortSpeech = new GeminiLocalVadPeakSilenceFilter(16000);
            var shortSpeechOutput = shortSpeech.Process(SignalAtSampleRate(20, 16000, 2000));
            Equal(20 * 16000 / 1000 * BytesPerSample, TotalFilteredBytes(shortSpeechOutput), "20ms speech must pass immediately");
            True(shortSpeech.HasDetectedSpeech, "short speech must count as detected speech");
        }

        private static void GeminiLocalVadKeepsAsymmetricProtection()
        {
            const int sampleRate = 16000;
            var filter = new GeminiLocalVadPeakSilenceFilter(sampleRate);
            Equal(
                10020 * sampleRate / 1000 * BytesPerSample,
                TotalFilteredBytes(filter.Process(SignalAtSampleRate(10020, sampleRate, 5000))),
                "long active audio must pass unchanged");
            Equal(
                0,
                TotalFilteredBytes(filter.Process(new byte[480 * sampleRate / 1000 * BytesPerSample])),
                "an incomplete 480ms boundary candidate remains local");
            var finish = filter.FinishLive();
            Equal(
                480 * sampleRate / 1000 * BytesPerSample,
                TotalFilteredBytes(finish),
                "stopping before 500ms must return every buffered byte to the current turn");
            Equal(0, filter.DiscardedEmptyTailDurationMs, "valid current-turn audio must not be discarded");

            var silent = new GeminiLocalVadPeakSilenceFilter(sampleRate);
            Equal(0, TotalFilteredBytes(silent.Process(new byte[2000 * sampleRate / 1000 * BytesPerSample])), "all-low recording remains local");
            Equal(0, TotalFilteredBytes(silent.FinishLive()), "all-low recording must not create a model turn");
            Equal(2000, silent.DiscardedEmptyTailDurationMs, "all-low recording is discarded only by the empty-tail gate");
            True(!silent.HasDetectedSpeech, "all-low recording must not count as speech");
        }

        private static void GeminiLocalVadFilteringIsChunkInvariant()
        {
            var pcm = Combine(
                new byte[700 * 16000 / 1000 * BytesPerSample],
                SignalAtSampleRate(100, 16000, 5000),
                new byte[1000 * 16000 / 1000 * BytesPerSample],
                SignalAtSampleRate(100, 16000, 6000),
                new byte[800 * 16000 / 1000 * BytesPerSample]);
            var oneChunk = RunLocalVadFilter(pcm, new[] { pcm.Length });
            var manyChunks = RunLocalVadFilter(pcm, new[] { 74, 118, 42, 206, 20, 512, 1000 });
            Equal(oneChunk.Length, manyChunks.Length, "chunked local VAD output length");
            True(BytesEqual(oneChunk, manyChunks), "chunk boundaries must not change local VAD output");
        }

        private static void GeminiLocalVadRollsFrontProtectionUntilSpeech()
        {
            const int sampleRate = 16000;
            var filter = new GeminiLocalVadPeakSilenceFilter(sampleRate);
            filter.Process(SignalAtSampleRate(10020, sampleRate, 5000));
            GeminiLocalVadBoundaryResult boundary = null;
            for (var frame = 0; frame < 25; frame++)
            {
                var peak = frame == 7 ? (short)100 : (short)(1000 + frame);
                foreach (var result in filter.Process(SignalAtSampleRate(20, sampleRate, peak)))
                {
                    if ((result.Action & GeminiLocalVadAction.ActivityEnd) != 0) boundary = result;
                }
            }
            True(boundary != null, "500ms low-peak run must create a boundary");
            Equal(100, boundary.BoundaryPeak, "the unique lowest 20ms frame must define the cut");
            Equal(10180, boundary.BoundaryAtMs, "cut follows the complete selected 20ms frame");
            Equal(340, boundary.CachedNextPrefixMs, "only complete frames after the selected minimum stay cached");
            Equal(160 * sampleRate / 1000 * BytesPerSample, TotalBytes(boundary.Audio), "previous turn receives the complete selected frame");
        }

        private static void GeminiLocalVadUsesStrictBoundaries()
        {
            const int sampleRate = 16000;
            var filter = new GeminiLocalVadPeakSilenceFilter(sampleRate);
            True(!ContainsAction(
                filter.Process(SignalAtSampleRate(9980, sampleRate, 5000)),
                GeminiLocalVadAction.ActivityEnd), "speech below 10 seconds cannot split");
            True(!ContainsAction(
                filter.Process(new byte[500 * sampleRate / 1000 * BytesPerSample]),
                GeminiLocalVadAction.ActivityEnd), "the frame that reaches exactly 10 seconds is not part of boundary detection");
            True(ContainsAction(
                filter.Process(new byte[20 * sampleRate / 1000 * BytesPerSample]),
                GeminiLocalVadAction.ActivityEnd), "the first turn must split only strictly after 10 seconds");
            Equal(2, filter.CurrentTurnNumber, "second turn number");
            Equal(15000, filter.CurrentMinimumTurnDurationMs, "second turn threshold");

            True(ContainsAction(
                filter.Process(SignalAtSampleRate(20, sampleRate, 2000)),
                GeminiLocalVadAction.ActivityStart), "second turn must start on active PCM");
            filter.Process(SignalAtSampleRate(15020, sampleRate, 5000));
            True(ContainsAction(
                filter.Process(new byte[500 * sampleRate / 1000 * BytesPerSample]),
                GeminiLocalVadAction.ActivityEnd), "second turn must split after 15 seconds");
            Equal(3, filter.CurrentTurnNumber, "third turn number");
            Equal(30000, filter.CurrentMinimumTurnDurationMs, "third turn threshold");

            True(ContainsAction(
                filter.Process(SignalAtSampleRate(20, sampleRate, 2000)),
                GeminiLocalVadAction.ActivityStart), "third turn must start on active PCM");
            filter.Process(SignalAtSampleRate(30020, sampleRate, 5000));
            True(ContainsAction(
                filter.Process(new byte[500 * sampleRate / 1000 * BytesPerSample]),
                GeminiLocalVadAction.ActivityEnd), "third turn must split after 30 seconds");
            Equal(3, filter.EffectiveTurnCount, "completed staged turn count");
            var durations = filter.TurnDurationsMs;
            True(durations[0] > 10000, "first duration must be strictly over 10 seconds");
            True(durations[1] > 15000, "second duration must be strictly over 15 seconds");
            True(durations[2] > 30000, "third duration must be strictly over 30 seconds");
        }

        private static void GeminiLocalVadAssignsProtectionAcrossStrictBoundary()
        {
            const int sampleRate = 16000;
            var filter = new GeminiLocalVadPeakSilenceFilter(sampleRate);
            filter.Process(SignalAtSampleRate(10020, sampleRate, 5000));
            var boundary = filter.Process(new byte[500 * sampleRate / 1000 * BytesPerSample]);
            True(ContainsAction(boundary, GeminiLocalVadAction.ActivityEnd), "qualifying low run must close the previous turn");
            True(filter.AwaitingNextActiveFrame, "next activity must stay closed after the cut");
            Equal(0, filter.PendingLowPeakDurationMs, "the last minimum frame closes the prior turn without a half-frame prefix");
            Equal(0, TotalFilteredBytes(filter.Process(new byte[1000 * sampleRate / 1000 * BytesPerSample])), "continued low audio must not start an empty next turn");
            var resumed = filter.Process(SignalAtSampleRate(20, sampleRate, 4000));
            True(ContainsAction(resumed, GeminiLocalVadAction.ActivityStart), "the first active 20ms frame starts the next turn");
            Equal(1020 * sampleRate / 1000 * BytesPerSample, TotalFilteredBytes(resumed), "cached post-cut audio and active frame keep original order");
        }

        private static void GeminiLocalVadRequiresContinuousFiveHundredMs()
        {
            const int sampleRate = 16000;
            var filter = new GeminiLocalVadPeakSilenceFilter(sampleRate);
            filter.Process(SignalAtSampleRate(10020, sampleRate, 5000));
            True(!ContainsAction(filter.Process(new byte[300 * sampleRate / 1000 * BytesPerSample]), GeminiLocalVadAction.ActivityEnd), "first 300ms low run must not split");
            True(!ContainsAction(filter.Process(SignalAtSampleRate(20, sampleRate, 4000)), GeminiLocalVadAction.ActivityEnd), "an active frame resets the low-peak run");
            True(!ContainsAction(filter.Process(new byte[480 * sampleRate / 1000 * BytesPerSample]), GeminiLocalVadAction.ActivityEnd), "fresh 480ms low run must not split");
            True(ContainsAction(filter.Process(new byte[20 * sampleRate / 1000 * BytesPerSample]), GeminiLocalVadAction.ActivityEnd), "only a continuous 500ms low run may split");
        }

        private static void GeminiLocalVadNeverHardCutsAndResets()
        {
            const int sampleRate = 16000;
            var pcm = Combine(
                new byte[200 * sampleRate / 1000 * BytesPerSample],
                SignalAtSampleRate(31000, sampleRate, 5000),
                new byte[500 * sampleRate / 1000 * BytesPerSample],
                new byte[100 * sampleRate / 1000 * BytesPerSample],
                SignalAtSampleRate(1000, sampleRate, 6000),
                new byte[300 * sampleRate / 1000 * BytesPerSample]);
            var processed = RunLocalVadFilter(pcm, new[] { 640, 74, 4096, 118 });
            Equal(pcm.Length, processed.Length, "valid resumed turns must preserve every PCM byte");
            True(BytesEqual(pcm, processed), "valid resumed turns must preserve exact PCM order");

            var continuous = new GeminiLocalVadPeakSilenceFilter(sampleRate);
            True(!ContainsAction(
                continuous.Process(SignalAtSampleRate(45000, sampleRate, 5000)),
                GeminiLocalVadAction.ActivityEnd), "continuous 45-second speech must never hard cut");
        }

        private static void GeminiLocalVadSetupUsesControlledInterruption()
        {
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "test",
                false,
                "unused second stage"))
            {
                var method = typeof(RealtimeSpeechSession).GetMethod(
                    "BuildGeminiSetup",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) throw new InvalidOperationException("BuildGeminiSetup not found");
                var root = (Dictionary<string, object>)method.Invoke(session, new object[] { "test-model" });
                var setup = (Dictionary<string, object>)root["setup"];
                var realtime = (Dictionary<string, object>)setup["realtimeInputConfig"];
                var automatic = (Dictionary<string, object>)realtime["automaticActivityDetection"];
                Equal(true, automatic["disabled"], "server VAD disabled");
                Equal(
                    "START_OF_ACTIVITY_INTERRUPTS",
                    Convert.ToString(realtime["activityHandling"]),
                    "next activityStart must close assumed playback after generationComplete");
                Equal("TURN_INCLUDES_ONLY_ACTIVITY", Convert.ToString(realtime["turnCoverage"]), "turn coverage");
            }

            using (var existing = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.Gemini },
                "test",
                false,
                "existing second stage"))
            {
                var method = typeof(RealtimeSpeechSession).GetMethod(
                    "BuildGeminiSetup",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var root = (Dictionary<string, object>)method.Invoke(existing, new object[] { "test-model" });
                var setup = (Dictionary<string, object>)root["setup"];
                var realtime = (Dictionary<string, object>)setup["realtimeInputConfig"];
                True(!realtime.ContainsKey("activityHandling"), "existing Gemini mode must remain unchanged");
            }
        }

        private static void StructuredTranscriptFunctionProtocolContract()
        {
            Equal(
                "commit_input_result",
                GeminiFinalTranscriptFunctionProtocol.FunctionName,
                "structured transcript function name");
            Equal(
                "final_readback",
                GeminiFinalTranscriptFunctionProtocol.FinalReadbackFunctionName,
                "final readback function name");
            Equal(
                "+++++GENERATION_COMPLETE+++++",
                GeminiFinalTranscriptFunctionProtocol.EndMarker,
                "structured end marker");

            var args = new Dictionary<string, object>
            {
                { "text", "第一点，测试。" },
                { "has_speech", true },
                { "end_marker", GeminiFinalTranscriptFunctionProtocol.EndMarker }
            };
            var request = new GeminiFunctionRequest("call-1", GeminiFinalTranscriptFunctionProtocol.FunctionName, args);
            string reason;
            var signal = GeminiFinalTranscriptFunctionProtocol.Validate(1, request, null, out reason);
            True(signal != null, "valid first-turn structured signal must be accepted");
            Equal("第一点，测试。", signal.Text, "structured signal text");
            Equal(true, signal.HasSpeech, "structured signal has speech");

            var badMarker = new Dictionary<string, object>
            {
                { "text", "x" },
                { "has_speech", true },
                { "end_marker", "wrong" }
            };
            var badRequest = new GeminiFunctionRequest("call-2", GeminiFinalTranscriptFunctionProtocol.FunctionName, badMarker);
            var rejected = GeminiFinalTranscriptFunctionProtocol.Validate(1, badRequest, null, out reason);
            Equal(null, rejected, "end marker mismatch must be rejected");
            Equal("end_marker_mismatch", reason, "end marker rejection reason");

            var mismatch = new Dictionary<string, object>
            {
                { "text", "" },
                { "has_speech", true },
                { "end_marker", GeminiFinalTranscriptFunctionProtocol.EndMarker }
            };
            var mismatchRequest = new GeminiFunctionRequest("call-3", GeminiFinalTranscriptFunctionProtocol.FunctionName, mismatch);
            var mismatchSignal = GeminiFinalTranscriptFunctionProtocol.Validate(1, mismatchRequest, null, out reason);
            Equal(null, mismatchSignal, "first turn speech/text mismatch must be rejected");
            Equal("first_turn_speech_text_mismatch", reason, "first turn mismatch reason");

            var unchanged = new Dictionary<string, object>
            {
                { "text", "旧文本" },
                { "has_speech", false },
                { "end_marker", GeminiFinalTranscriptFunctionProtocol.EndMarker }
            };
            var unchangedRequest = new GeminiFunctionRequest("call-4", GeminiFinalTranscriptFunctionProtocol.FunctionName, unchanged);
            var unchangedSignal = GeminiFinalTranscriptFunctionProtocol.Validate(2, unchangedRequest, "旧文本", out reason);
            True(unchangedSignal != null, "silent later turn repeating accepted text must be accepted");

            var drifted = GeminiFinalTranscriptFunctionProtocol.Validate(2, unchangedRequest, "别的文本", out reason);
            Equal(null, drifted, "silent later turn must repeat the accepted cumulative text");
            Equal("unchanged_cumulative_text_required", reason, "drifted cumulative rejection reason");

            var ack = GeminiFinalTranscriptFunctionProtocol.ResponseBody(request, true, "");
            var toolResponse = (Dictionary<string, object>)ack["toolResponse"];
            var responses = (object[])toolResponse["functionResponses"];
            var entry = (Dictionary<string, object>)responses[0];
            Equal("call-1", Convert.ToString(entry["id"]), "ack echoes the call id");
            var payload = (Dictionary<string, object>)entry["response"];
            Equal(true, payload["accepted"], "ack accepted flag");
            Equal(false, payload["speak_now"], "ordinary activity ack must never authorize audio");

            var readback = GeminiFinalTranscriptFunctionProtocol.ReadbackAuthorizationBody(request);
            var readbackTool = (Dictionary<string, object>)readback["toolResponse"];
            var readbackResponses = (object[])readbackTool["functionResponses"];
            var readbackEntry = (Dictionary<string, object>)readbackResponses[0];
            var readbackPayload = (Dictionary<string, object>)readbackEntry["response"];
            Equal(true, readbackPayload["speak_now"], "final readback authorization must allow audio");
        }

        private static void LocalVadSetupRegistersStructuredFunction()
        {
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "structured test",
                false,
                "second stage"))
            {
                var method = typeof(RealtimeSpeechSession).GetMethod(
                    "BuildGeminiSetup",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) throw new InvalidOperationException("BuildGeminiSetup not found");
                var root = (Dictionary<string, object>)method.Invoke(session, new object[] { "test-model" });
                var setup = (Dictionary<string, object>)root["setup"];
                True(setup.ContainsKey("tools"), "local VAD setup must register the structured function");
                var tools = (object[])setup["tools"];
                var group = (Dictionary<string, object>)tools[0];
                var declarations = (object[])group["functionDeclarations"];
                var first = (Dictionary<string, object>)declarations[0];
                Equal(
                    GeminiFinalTranscriptFunctionProtocol.FunctionName,
                    Convert.ToString(first["name"]),
                    "structured function declaration name");

                var instruction = (Dictionary<string, object>)setup["systemInstruction"];
                var parts = (object[])instruction["parts"];
                var part = (Dictionary<string, object>)parts[0];
                var instructionText = Convert.ToString(part["text"]);
                True(
                    instructionText.Contains(GeminiFinalTranscriptFunctionProtocol.FunctionName),
                    "structured instruction must name the commit function");
                True(
                    !instructionText.Contains("最终只输出一次 TEXT:"),
                    "structured mode must drop the legacy TEXT: output contract");
            }
        }

        private static void TypingPlaybackSetupUsesPuckVoice()
        {
            Equal("Puck", RealtimeSpeechSession.TypingPlaybackVoiceName, "typing playback voice name");
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "typing playback test",
                false,
                "second stage",
                false,
                delegate(byte[] pcm) { },
                true))
            {
                var method = typeof(RealtimeSpeechSession).GetMethod(
                    "BuildGeminiSetup",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) throw new InvalidOperationException("BuildGeminiSetup not found");
                var root = (Dictionary<string, object>)method.Invoke(session, new object[] { "test-model" });
                var setup = (Dictionary<string, object>)root["setup"];
                var generation = (Dictionary<string, object>)setup["generationConfig"];
                True(!generation.ContainsKey("speechConfig"), "Windows typing has no result readback even with an old saved switch");
            }
        }

        private static void NativeQaSetupUsesFixedVoiceWithoutSearch()
        {
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.Gemini },
                "native Q&A test",
                false,
                null,
                true,
                delegate(byte[] pcm) { }))
            {
                var method = typeof(RealtimeSpeechSession).GetMethod(
                    "BuildGeminiSetup",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) throw new InvalidOperationException("BuildGeminiSetup not found");
                var root = (Dictionary<string, object>)method.Invoke(session, new object[] { "test-model" });
                var setup = (Dictionary<string, object>)root["setup"];
                var generation = (Dictionary<string, object>)setup["generationConfig"];
                var modalities = (object[])generation["responseModalities"];
                Equal(1, modalities.Length, "native Q&A response modality count");
                Equal("AUDIO", Convert.ToString(modalities[0]), "native Q&A response modality");

                var speech = (Dictionary<string, object>)generation["speechConfig"];
                var voice = (Dictionary<string, object>)speech["voiceConfig"];
                var prebuilt = (Dictionary<string, object>)voice["prebuiltVoiceConfig"];
                Equal(RealtimeSpeechSession.NativeQaVoiceName, Convert.ToString(prebuilt["voiceName"]), "fixed native Q&A voice");
                Equal("Kore", RealtimeSpeechSession.NativeQaVoiceName, "native Q&A voice name");
                True(setup.ContainsKey("inputAudioTranscription"), "native Q&A input transcription enabled");
                True(setup.ContainsKey("outputAudioTranscription"), "native Q&A output transcription enabled");

                True(
                    !setup.ContainsKey("tools"),
                    "Gemini native Q&A must not register online lookup tools");
                Equal(
                    null,
                    typeof(RealtimeSpeechSession).GetField(
                        "geminiSearchEnabled",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic),
                    "retired Search fallback state must be removed");

                var directory =
                    new DirectoryInfo(
                        AppDomain.CurrentDomain.BaseDirectory);
                while (directory != null &&
                       !File.Exists(
                           Path.Combine(
                               directory.FullName,
                               "Program.cs")))
                {
                    directory = directory.Parent;
                }
                True(
                    directory != null,
                    "project source root must be discoverable");
                var productionSource =
                    File.ReadAllText(
                        Path.Combine(
                            directory.FullName,
                            "Program.cs"),
                        Encoding.UTF8) +
                    File.ReadAllText(
                        Path.Combine(
                            directory.FullName,
                            "WakeAndConversation.cs"),
                        Encoding.UTF8);
                foreach (var forbidden in new[]
                {
                    "googleSearch",
                    "google_search",
                    "web_search",
                    "geminiSearchEnabled",
                    "/v1beta/interactions"
                })
                {
                    True(
                        productionSource.IndexOf(
                            forbidden,
                            StringComparison.OrdinalIgnoreCase) < 0,
                        "production source contains retired lookup token " +
                        forbidden);
                }
            }
        }

        private static void SimpleQaProductionRoutesAreRetired()
        {
            var directory =
                new DirectoryInfo(
                    AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null &&
                   !File.Exists(
                       Path.Combine(
                           directory.FullName,
                           "Program.cs")))
            {
                directory = directory.Parent;
            }
            True(
                directory != null,
                "project source root must be discoverable");
            var source =
                File.ReadAllText(
                    Path.Combine(directory.FullName, "Program.cs"),
                    Encoding.UTF8);
            True(
                source.IndexOf(
                    "new QaWidgetForm(",
                    StringComparison.Ordinal) < 0,
                "retired Simple Q&A form must not be instantiated");
            True(
                source.IndexOf(
                    "ToggleRecording(CaptureTarget.Qa)",
                    StringComparison.Ordinal) < 0,
                "retired Simple Q&A capture route must have no production caller");
            True(
                source.IndexOf(
                    "retired Simple Q&A capture request ignored",
                    StringComparison.Ordinal) >= 0,
                "retired Simple Q&A capture route must fail closed even for an internal stale caller");
            True(
                source.IndexOf(
                    "RealtimeExperienceMode.Translation",
                    StringComparison.Ordinal) < 0,
                "retired realtime translation must have no MainForm route");
            True(
                source.IndexOf(
                    "\"qaHotkey=\"",
                    StringComparison.Ordinal) < 0 &&
                source.IndexOf(
                    "\"showQaWidget=\"",
                    StringComparison.Ordinal) < 0 &&
                source.IndexOf(
                    "\"realtimeTranslationHotkey=\"",
                    StringComparison.Ordinal) < 0 &&
                source.IndexOf(
                    "\"showRealtimeTranslationWidget=\"",
                    StringComparison.Ordinal) < 0 &&
                source.IndexOf(
                    "\"wakeRealtimeConversation=\"",
                    StringComparison.Ordinal) < 0,
                "retired settings must not be written");

            var saveStart = source.IndexOf(
                "private bool TrySave()",
                StringComparison.Ordinal);
            var saveEnd = source.IndexOf(
                "private void LoadDisplayLayoutProfiles",
                saveStart,
                StringComparison.Ordinal);
            True(
                saveStart >= 0 && saveEnd > saveStart,
                "widget persistence section must be discoverable");
            var saveBody = source.Substring(
                saveStart,
                saveEnd - saveStart);
            True(
                saveBody.IndexOf(
                    "qaSlots",
                    StringComparison.Ordinal) < 0 &&
                saveBody.IndexOf(
                    "qaNames",
                    StringComparison.Ordinal) < 0 &&
                saveBody.IndexOf(
                    "qaSlotIds",
                    StringComparison.Ordinal) < 0,
                "legacy Simple Q&A data may be read for compatibility but must not be persisted again");
            var currentWidgetJson =
                new System.Web.Script.Serialization.
                    JavaScriptSerializer().
                    Serialize(new WidgetSaveData());
            True(
                currentWidgetJson.IndexOf(
                    "qaSlots",
                    StringComparison.Ordinal) < 0 &&
                currentWidgetJson.IndexOf(
                    "qaNames",
                    StringComparison.Ordinal) < 0 &&
                currentWidgetJson.IndexOf(
                    "qaSlotIds",
                    StringComparison.Ordinal) < 0,
                "current widget JSON schema must omit retired Simple Q&A fields");

            var providerSmoke =
                File.ReadAllText(
                    Path.Combine(
                        directory.FullName,
                        "tests",
                        "ProviderSmokeTests.cs"),
                    Encoding.UTF8);
            True(
                providerSmoke.IndexOf(
                    "RunQuestion",
                    StringComparison.Ordinal) < 0 &&
                providerSmoke.IndexOf(
                    "Simple Q&A",
                    StringComparison.Ordinal) < 0,
                "current provider smoke tool must not invoke the retired protocol");
        }

        private static void NativeQaTranscriptsRemainIndependent()
        {
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.Gemini },
                "native Q&A test",
                false,
                null,
                true,
                delegate(byte[] pcm) { }))
            {
                var mergeInput = typeof(RealtimeSpeechSession).GetMethod(
                    "MergeInputTranscriptionSnapshot",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var mergeOutput = typeof(RealtimeSpeechSession).GetMethod(
                    "MergeNativeOutputTranscription",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (mergeInput == null || mergeOutput == null) throw new InvalidOperationException("native Q&A transcript hooks not found");
                mergeInput.Invoke(session, new object[] { "What time is it?" });
                mergeOutput.Invoke(session, new object[] { "It is ten o'clock." });
                mergeOutput.Invoke(session, new object[] { "That is the current time." });
                Equal("What time is it?", session.InputTranscription, "question transcript");
                Equal("It is ten o'clock. That is the current time.", session.GetBestEffortText(), "answer transcript");
            }
        }

        private static void NativeQaPromptCapsContextAtFiftyTurns()
        {
            var context = new List<QaMessage>();
            for (var turn = 1; turn <= 60; turn++)
            {
                context.Add(new QaMessage { role = "Q", text = "question-" + turn });
                context.Add(new QaMessage { role = "A", text = "answer-" + turn });
            }
            var prompt = PromptBuilder.NativeAudioQuestionAnswer(context);
            True(prompt.Contains("question-60"), "newest native Q&A context retained");
            True(prompt.Contains("answer-11"), "fiftieth newest turn retained");
            True(!prompt.Contains("question-10"), "older than fifty turns removed");
            Equal(50, CountOccurrences(prompt, "Earlier user question:"), "question context count");
            Equal(50, CountOccurrences(prompt, "Earlier assistant answer:"), "answer context count");
            True(prompt.Contains("current question arrives only as live microphone audio"), "current question remains audio-only");
            True(
                prompt.Contains("Online lookup tools are not available"),
                "Simple Q&A must clearly state the no-lookup boundary");
            True(
                prompt.Contains("clearly state the uncertainty"),
                "Simple Q&A must keep a current-fact uncertainty guard");
        }

        private static void NativeQaPcmOutputStreamsThroughSink()
        {
            var chunks = new List<byte[]>();
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.Gemini },
                "native Q&A test",
                false,
                null,
                true,
                chunks.Add))
            {
                var accept = typeof(RealtimeSpeechSession).GetMethod(
                    "AcceptNativeOutputAudio",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (accept == null) throw new InvalidOperationException("AcceptNativeOutputAudio not found");
                var pcm = new byte[] { 1, 0, 2, 0 };
                var inlineData = new Dictionary<string, object>
                {
                    { "mimeType", "audio/pcm;rate=24000" },
                    { "data", Convert.ToBase64String(pcm) }
                };
                Equal(4, accept.Invoke(session, new object[] { inlineData }), "accepted PCM byte count");
                Equal(1, chunks.Count, "streamed PCM chunk count");
                Equal(4L, session.NativeOutputAudioBytes, "native PCM total");

                var handle = typeof(RealtimeSpeechSession).GetMethod(
                    "HandleGeminiMessage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (handle == null) throw new InvalidOperationException("HandleGeminiMessage not found");
                handle.Invoke(session, new object[]
                {
                    "{\"serverContent\":{\"modelTurn\":{\"parts\":[{\"inlineData\":{\"mimeType\":\"audio/pcm;rate=24000\",\"data\":\"AQACAA==\"}}]},\"outputTranscription\":{\"text\":\"streamed answer\"}}}"
                });
                Equal(2, chunks.Count, "JSON object-array PCM chunk count");
                Equal(8L, session.NativeOutputAudioBytes, "JSON object-array PCM total");
                Equal("streamed answer", session.GetBestEffortText(), "JSON object-array output transcription");
                Equal(24000, RealtimeSpeechSession.NativeQaOutputSampleRate, "native output sample rate");
                Equal(12L * 1024L * 1024L, RealtimeSpeechSession.NativeQaMaximumOutputPcmBytes, "native output safety cap");
            }
        }

        private static void NativeQaSupportsBothProviders()
        {
            using (var openAi = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.OpenAi },
                "native Q&A test",
                false,
                null,
                true,
                delegate(byte[] pcm) { }))
            {
                True(openAi.IsNativeAudioQa, "OpenAI native Q&A must be enabled");
                Equal("marin", openAi.NativeQaVoice, "OpenAI native Q&A voice");
                Equal(24000, openAi.RecordingSampleRate, "OpenAI native Q&A input rate");

                var build = typeof(RealtimeSpeechSession).GetMethod(
                    "BuildOpenAiSessionUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (build == null) throw new InvalidOperationException("BuildOpenAiSessionUpdate not found");
                var root = (Dictionary<string, object>)build.Invoke(
                    openAi,
                    new object[] { "gpt-realtime-1.5" });
                var session = (Dictionary<string, object>)root["session"];
                var modalities = (object[])session["output_modalities"];
                Equal("audio", Convert.ToString(modalities[0]), "OpenAI native Q&A output modality");
                var audio = (Dictionary<string, object>)session["audio"];
                var input = (Dictionary<string, object>)audio["input"];
                var transcription = (Dictionary<string, object>)input["transcription"];
                Equal(
                    "gpt-realtime-whisper",
                    Convert.ToString(transcription["model"]),
                    "OpenAI supported input transcription model");
                True(
                    !transcription.ContainsKey("delay"),
                    "OpenAI transcription setup must not send the retired delay field");
                var output = (Dictionary<string, object>)audio["output"];
                Equal("marin", Convert.ToString(output["voice"]), "OpenAI session voice");
                var outputFormat = (Dictionary<string, object>)output["format"];
                Equal(
                    RealtimeSpeechSession.NativeQaOutputSampleRate,
                    Convert.ToInt32(outputFormat["rate"]),
                    "OpenAI native Q&A output PCM rate");
            }

            using (var gemini = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.Gemini },
                "native Q&A test",
                false,
                null,
                true,
                delegate(byte[] pcm) { }))
            {
                True(gemini.IsNativeAudioQa, "Gemini native Q&A must be enabled");
                Equal("Kore", gemini.NativeQaVoice, "Gemini native Q&A voice");
                Equal(16000, gemini.RecordingSampleRate, "Gemini native Q&A input rate");
            }

            var rejectedInputFallback = false;
            try
            {
                using (var ignored = new RealtimeSpeechSession(
                    new AppSettings { Provider = AiProvider.Gemini },
                    "native Q&A test",
                    true,
                    null,
                    true,
                    delegate(byte[] pcm) { })) { }
            }
            catch (ArgumentException)
            {
                rejectedInputFallback = true;
            }
            True(rejectedInputFallback, "native Q&A must not return the input transcript as the answer");
        }

        private static void OpenAiNativeQaStreamsAudioAndTranscripts()
        {
            var chunks = new List<byte[]>();
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.OpenAi },
                "native Q&A test",
                false,
                null,
                true,
                chunks.Add))
            {
                var handle = typeof(RealtimeSpeechSession).GetMethod(
                    "HandleOpenAiMessage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (handle == null) throw new InvalidOperationException("HandleOpenAiMessage not found");
                handle.Invoke(session, new object[]
                {
                    "{\"type\":\"conversation.item.input_audio_transcription.completed\",\"transcript\":\"What time is it?\"}"
                });
                handle.Invoke(session, new object[]
                {
                    "{\"type\":\"response.output_audio.delta\",\"delta\":\"AQACAA==\"}"
                });
                handle.Invoke(session, new object[]
                {
                    "{\"type\":\"response.output_audio_transcript.delta\",\"delta\":\"It is ten o'clock.\"}"
                });
                Equal("What time is it?", session.InputTranscription, "OpenAI spoken question transcript");
                Equal("It is ten o'clock.", session.GetBestEffortText(), "OpenAI answer transcript");
                Equal(1, chunks.Count, "OpenAI native answer audio chunks");
                Equal(4L, session.NativeOutputAudioBytes, "OpenAI native answer audio bytes");
            }
        }

        private static void NativeQaArchiveWritesStandardWave()
        {
            var directory = NewArchiveTestDirectory();
            try
            {
                var first = new byte[] { 1, 0, 2, 0 };
                var second = new byte[] { 3, 0, 4, 0 };
                string path;
                using (var archive = new NativeQaAudioArchive(directory, 24000, 2))
                {
                    archive.Append(first);
                    archive.Append(second);
                    Equal(8L, archive.BytesWritten, "archive PCM byte count");
                    path = archive.Complete();
                }
                True(File.Exists(path), "completed WAV must exist");
                True(!File.Exists(path + ".part"), "completed WAV must not leave a part file");
                var wave = File.ReadAllBytes(path);
                Equal(52, wave.Length, "WAV file length");
                Equal("RIFF", Encoding.ASCII.GetString(wave, 0, 4), "RIFF signature");
                Equal(44, BitConverter.ToInt32(wave, 4), "RIFF payload length");
                Equal("WAVE", Encoding.ASCII.GetString(wave, 8, 4), "WAVE signature");
                Equal("fmt ", Encoding.ASCII.GetString(wave, 12, 4), "format chunk");
                Equal((short)1, BitConverter.ToInt16(wave, 20), "PCM format tag");
                Equal((short)1, BitConverter.ToInt16(wave, 22), "mono channel count");
                Equal(24000, BitConverter.ToInt32(wave, 24), "WAV sample rate");
                Equal(48000, BitConverter.ToInt32(wave, 28), "WAV byte rate");
                Equal((short)2, BitConverter.ToInt16(wave, 32), "WAV block alignment");
                Equal((short)16, BitConverter.ToInt16(wave, 34), "WAV bits per sample");
                Equal("data", Encoding.ASCII.GetString(wave, 36, 4), "data chunk");
                Equal(8, BitConverter.ToInt32(wave, 40), "WAV data length");
                True(BytesEqual(new byte[] { 1, 0, 2, 0, 3, 0, 4, 0 }, Slice(wave, 44, 8)),
                    "WAV payload must preserve streamed PCM order");
            }
            finally
            {
                DeleteArchiveTestDirectory(directory);
            }
        }

        private static void NativeQaArchivePreservesPartialAudio()
        {
            var directory = NewArchiveTestDirectory();
            try
            {
                string path;
                using (var archive = new NativeQaAudioArchive(directory, 24000, 0))
                {
                    archive.Append(new byte[] { 9, 0, 10, 0 });
                    path = archive.CompletePartial();
                }
                True(File.Exists(path), "partial WAV must exist");
                True(path.EndsWith("-partial.wav", StringComparison.OrdinalIgnoreCase),
                    "partial WAV must be visibly marked");
                Equal(4, BitConverter.ToInt32(File.ReadAllBytes(path), 40), "partial WAV data length");
            }
            finally
            {
                DeleteArchiveTestDirectory(directory);
            }
        }

        private static void NativeQaArchiveAbortRemovesUnfinishedAudio()
        {
            var directory = NewArchiveTestDirectory();
            try
            {
                using (var archive = new NativeQaAudioArchive(directory, 24000, 0))
                {
                    archive.Append(new byte[] { 11, 0, 12, 0 });
                    archive.Abort();
                }
                Equal(0, Directory.GetFiles(directory).Length, "aborted archive must leave no file");
            }
            finally
            {
                DeleteArchiveTestDirectory(directory);
            }
        }

        private static void NativeQaArchiveResolvesInsideProject()
        {
            var configured = Environment.GetEnvironmentVariable("VOICE_TYPING_AI_AUDIO_DIR");
            var directory = NativeQaAudioArchive.ResolveDefaultDirectory();
            True(!string.IsNullOrWhiteSpace(directory), "default archive directory must be resolved");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                Equal(Path.GetFullPath(configured.Trim()), directory, "explicit archive directory override");
                return;
            }
            Equal("generated-audio", new DirectoryInfo(directory).Name, "project archive folder name");
            var project = Directory.GetParent(directory);
            True(project != null && File.Exists(Path.Combine(project.FullName, "Program.cs")),
                "default archive folder must belong to the desktop project");
            True(File.Exists(Path.Combine(project.FullName, "README.md")),
                "resolved project must contain its README");
        }

        private static void NativeQaExchangeIsAtomic()
        {
            var store = new WidgetStore(false);
            var operationId = store.GetQaOperationId(0);
            True(
                !store.AddQaExchange(0, operationId + "-stale", "question", "answer"),
                "stale operation must not write either side");
            Equal(0, store.QaSlots[0].Count, "stale exchange must leave the slot unchanged");

            True(
                store.AddQaExchange(0, operationId, "question", "answer"),
                "valid exchange must be accepted");
            Equal(2, store.QaSlots[0].Count, "question and answer must commit together");
            Equal("Q", store.QaSlots[0][0].role, "question role");
            Equal("A", store.QaSlots[0][1].role, "answer role");
            Equal(operationId, store.QaSlots[0][0].operationId, "question operation ID");
            Equal(operationId, store.QaSlots[0][1].operationId, "answer operation ID");
            Equal(store.QaSlots[0][0].createdAt, store.QaSlots[0][1].createdAt, "atomic timestamp");

            store.DeleteActiveQaSlot();
            True(store.GetQaOperationId(0) != operationId, "deleting the slot must invalidate in-flight delivery");
            True(
                !store.AddQaExchange(0, operationId, "late question", "late answer"),
                "late delivery must not repopulate a deleted slot");
        }

        private static void NativeQaPlaybackDeadlineCoversCompletePcm()
        {
            Equal(
                NativeQaPlaybackPolicy.MinimumCompletionTimeoutMs,
                NativeQaPlaybackPolicy.CompletionTimeoutMs(96000, 24000),
                "short answer playback deadline floor");
            Equal(
                65000,
                NativeQaPlaybackPolicy.CompletionTimeoutMs(60L * 24000L * 2L, 24000),
                "long answer playback duration plus safety margin");
            Equal(
                NativeQaPlaybackPolicy.MaximumCompletionTimeoutMs,
                NativeQaPlaybackPolicy.CompletionTimeoutMs(long.MaxValue, 24000),
                "playback deadline cap");
        }

        private static void ProcessedInputArchiveWritesStandardWave()
        {
            var directory = NewArchiveTestDirectory();
            try
            {
                var first = new byte[] { 21, 0, 22, 0 };
                var second = new byte[] { 23, 0, 24, 0 };
                string path;
                using (var archive = new ProcessedInputAudioArchive(
                    directory,
                    16000,
                    "Gemini Local VAD",
                    42))
                {
                    archive.Append(first);
                    archive.Append(second);
                    Equal(8L, archive.BytesWritten, "processed archive PCM byte count");
                    path = archive.Complete();
                    Equal(path, archive.CompletedPath, "processed archive completed path");
                }
                True(File.Exists(path), "completed processed WAV must exist");
                True(path.IndexOf("gemini-local-vad", StringComparison.OrdinalIgnoreCase) >= 0,
                    "processed WAV name must identify the route");
                True(path.IndexOf("request-000042", StringComparison.OrdinalIgnoreCase) >= 0,
                    "processed WAV name must identify the request");
                var wave = File.ReadAllBytes(path);
                Equal(52, wave.Length, "processed WAV file length");
                Equal("RIFF", Encoding.ASCII.GetString(wave, 0, 4), "processed RIFF signature");
                Equal("WAVE", Encoding.ASCII.GetString(wave, 8, 4), "processed WAVE signature");
                Equal((short)1, BitConverter.ToInt16(wave, 20), "processed PCM format tag");
                Equal((short)1, BitConverter.ToInt16(wave, 22), "processed mono channel count");
                Equal(16000, BitConverter.ToInt32(wave, 24), "processed WAV sample rate");
                Equal(32000, BitConverter.ToInt32(wave, 28), "processed WAV byte rate");
                Equal((short)16, BitConverter.ToInt16(wave, 34), "processed WAV bits per sample");
                Equal(8, BitConverter.ToInt32(wave, 40), "processed WAV data length");
                True(BytesEqual(new byte[] { 21, 0, 22, 0, 23, 0, 24, 0 }, Slice(wave, 44, 8)),
                    "processed WAV payload must preserve sent PCM order");
            }
            finally
            {
                DeleteArchiveTestDirectory(directory);
            }
        }

        private static void ProcessedInputArchiveDisposePreservesPartialAudio()
        {
            var directory = NewArchiveTestDirectory();
            try
            {
                using (var archive = new ProcessedInputAudioArchive(directory, 24000, "openai", 7))
                {
                    archive.Append(new byte[] { 31, 0, 32, 0 });
                }
                var files = Directory.GetFiles(directory, "*.wav");
                Equal(1, files.Length, "interrupted processed archive file count");
                True(files[0].EndsWith("-partial.wav", StringComparison.OrdinalIgnoreCase),
                    "interrupted processed WAV must be visibly marked");
                var wave = File.ReadAllBytes(files[0]);
                Equal(24000, BitConverter.ToInt32(wave, 24), "partial processed WAV sample rate");
                Equal(4, BitConverter.ToInt32(wave, 40), "partial processed WAV data length");
            }
            finally
            {
                DeleteArchiveTestDirectory(directory);
            }
        }

        private static void ProcessedInputArchiveLeavesNoEmptyFile()
        {
            var directory = NewArchiveTestDirectory();
            try
            {
                using (var archive = new ProcessedInputAudioArchive(directory, 16000, "gemini", 8))
                {
                }
                Equal(0, Directory.GetFiles(directory).Length,
                    "an archive without sent audio must leave no empty file");
            }
            finally
            {
                DeleteArchiveTestDirectory(directory);
            }
        }

        private static void ProcessedInputArchiveResolvesInsideProject()
        {
            var configured = Environment.GetEnvironmentVariable("VOICE_TYPING_AI_PROCESSED_AUDIO_DIR");
            var directory = ProcessedInputAudioArchive.ResolveDefaultDirectory();
            True(!string.IsNullOrWhiteSpace(directory), "processed archive directory must be resolved");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                Equal(Path.GetFullPath(configured.Trim()), directory,
                    "explicit processed archive directory override");
                return;
            }
            Equal("processed-audio", new DirectoryInfo(directory).Name,
                "processed project archive folder name");
            var project = Directory.GetParent(directory);
            True(project != null && File.Exists(Path.Combine(project.FullName, "Program.cs")),
                "processed archive folder must belong to the desktop project");
            True(File.Exists(Path.Combine(project.FullName, "README.md")),
                "processed archive project must contain its README");
        }

        private static void RealtimeSessionFinalizesProcessedInputArchive()
        {
            var directory = NewArchiveTestDirectory();
            var previous = Environment.GetEnvironmentVariable("VOICE_TYPING_AI_PROCESSED_AUDIO_DIR");
            try
            {
                Environment.SetEnvironmentVariable("VOICE_TYPING_AI_PROCESSED_AUDIO_DIR", directory);
                using (var session = new RealtimeSpeechSession(
                    new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                    "archive hook test",
                    false,
                    null))
                {
                    var append = typeof(RealtimeSpeechSession).GetMethod(
                        "ArchiveProcessedInputChunk",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    var finalize = typeof(RealtimeSpeechSession).GetMethod(
                        "FinalizeProcessedInputAudioArchive",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (append == null || finalize == null)
                    {
                        throw new InvalidOperationException("processed input session archive hooks not found");
                    }
                    append.Invoke(session, new object[] { new byte[] { 41, 0, 42, 0 } });
                    var path = (string)finalize.Invoke(session, new object[] { false });
                    True(File.Exists(path), "session archive must finalize a WAV");
                    Equal(path, session.ProcessedInputAudioPath, "session must expose the archived WAV path");
                    True(path.IndexOf("gemini-local-vad", StringComparison.OrdinalIgnoreCase) >= 0,
                        "session archive name must identify Local VAD");
                    Equal(4, BitConverter.ToInt32(File.ReadAllBytes(path), 40),
                        "session archive data length");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("VOICE_TYPING_AI_PROCESSED_AUDIO_DIR", previous);
                DeleteArchiveTestDirectory(directory);
            }
        }

        private static void GeminiLocalVadOutboundOrder()
        {
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "test",
                false,
                null))
            {
                var enqueue = typeof(RealtimeSpeechSession).GetMethod(
                    "EnqueueLocalVadItems",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var field = typeof(RealtimeSpeechSession).GetField(
                    "outboundQueue",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (enqueue == null || field == null) throw new InvalidOperationException("local VAD outbound queue hooks not found");
                enqueue.Invoke(session, new object[]
                {
                    new List<byte[]>
                    {
                        SignalAtSampleRate(160, 16000, 500),
                        SignalAtSampleRate(20, 16000, 5000)
                    },
                    GeminiLocalVadAction.ActivityStart
                });
                var queue = (Queue<RealtimeOutboundItem>)field.GetValue(session);
                Equal(3, queue.Count, "an active frame releases the follow-up immediately (Android parity)");
                enqueue.Invoke(session, new object[]
                {
                    new List<byte[]>
                    {
                        SignalAtSampleRate(820, 16000, 5000)
                    },
                    GeminiLocalVadAction.None
                });
                enqueue.Invoke(session, new object[]
                {
                    new List<byte[]>(),
                    GeminiLocalVadAction.ActivityEnd
                });
                var items = queue.ToArray();
                Equal(5, items.Length, "outbound item count");
                Equal(RealtimeOutboundKind.GeminiActivityStart, items[0].Kind, "activityStart order");
                Equal(RealtimeOutboundKind.Audio, items[1].Kind, "cached low-peak prefix order");
                Equal(RealtimeOutboundKind.Audio, items[2].Kind, "first active frame order");
                Equal(RealtimeOutboundKind.Audio, items[3].Kind, "qualification audio order");
                Equal(RealtimeOutboundKind.GeminiActivityEnd, items[4].Kind, "activityEnd order");
            }
        }

        private static void GeminiLocalVadDiscardsSubSecondFinalTurn()
        {
            const int sampleRate = 16000;
            var filter = new GeminiLocalVadPeakSilenceFilter(sampleRate);
            filter.Process(SignalAtSampleRate(10020, sampleRate, 5000));
            True(ContainsAction(
                filter.Process(new byte[500 * sampleRate / 1000 * BytesPerSample]),
                GeminiLocalVadAction.ActivityEnd), "setup boundary");
            filter.Process(new byte[1200 * sampleRate / 1000 * BytesPerSample]);
            Equal(0, TotalFilteredBytes(filter.FinishLive()), "pure low-peak tail must not create a final turn");
            Equal(1200, filter.DiscardedEmptyTailDurationMs, "trailing low audio is discarded without a half-frame prefix");
            True(!filter.ShouldEndCurrentTurn, "no final activityEnd is needed without a resumed active frame");
        }

        private static void GeminiLocalVadReleasesActiveShortFinalTurn()
        {
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "test",
                false,
                null))
            {
                var enqueue = typeof(RealtimeSpeechSession).GetMethod(
                    "EnqueueLocalVadItems",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var queueField = typeof(RealtimeSpeechSession).GetField(
                    "outboundQueue",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (enqueue == null || queueField == null)
                {
                    throw new InvalidOperationException("deferred local VAD hooks not found");
                }

                // 安卓对齐：358ms 的活跃尾轮（peak 3044 >= ACTIVE_PEAK_THRESHOLD）必须立刻
                // 建立轮次——activityStart 与整批音频一起进队列，不能再按"不足一秒"整轮丢掉。
                enqueue.Invoke(session, new object[]
                {
                    new List<byte[]> { SignalAtSampleRate(358, 16000, 3044) },
                    GeminiLocalVadAction.ActivityStart
                });
                var queue = (Queue<RealtimeOutboundItem>)queueField.GetValue(session);
                Equal(2, queue.Count, "active 358ms tail must queue activityStart plus its audio");
            }

            const int sampleRate = 16000;
            var filter = new GeminiLocalVadPeakSilenceFilter(sampleRate);
            filter.Process(SignalAtSampleRate(10020, sampleRate, 5000));
            filter.Process(new byte[500 * sampleRate / 1000 * BytesPerSample]);
            filter.Process(SignalAtSampleRate(358, sampleRate, 3044));
            filter.FinishLive();
            Equal(2, filter.EffectiveTurnCount, "local filter records the provisional final turn before the network gate");
            filter.DiscardFinishedCurrentTurn();
            Equal(1, filter.EffectiveTurnCount, "discarded provisional turn must be removed from diagnostics");
            True(!filter.ShouldEndCurrentTurn, "discarded provisional turn must not send activityEnd");
        }

        private static void GeminiLocalVadReleasesActiveTailLikeAndroid()
        {
            // 安卓对齐：判定只看是否出现过活跃语音帧（ACTIVE_PEAK_THRESHOLD=2000），
            // 不再有"必须满一秒"的硬门槛。
            Equal(2000, GeminiLocalVadDeferredTurnAudio.ActivePeakThreshold, "active peak threshold mirrors Android");
            True(!GeminiLocalVadDeferredTurnAudio.IsReadyForNetwork(2000, 1999), "tail without an active frame stays unclaimed");
            True(GeminiLocalVadDeferredTurnAudio.IsReadyForNetwork(20, 2000), "a single active frame releases the tail immediately");
            True(GeminiLocalVadDeferredTurnAudio.IsReadyForNetwork(408, 3669), "observed 408ms speech tail must reach the network");
            True(GeminiLocalVadDeferredTurnAudio.IsReadyForNetwork(898, 6190), "observed 898ms speech tail must reach the network");
            True(!GeminiLocalVadDeferredTurnAudio.IsReadyForNetwork(3000, 600), "three seconds of quiet audio without an active frame stays unclaimed");
        }

        private static void GeminiLocalVadObservedShortTailsReachNetwork()
        {
            // 这七个实测尾轮都含活跃语音帧（peak >= 2000）。旧版按"不足一秒"整轮丢弃，
            // 正是"结尾一句短话消失"的来源；安卓对齐后必须全部送到网络。
            var observed = new[]
            {
                new[] { 558, 2560 },
                new[] { 208, 2271 },
                new[] { 338, 7654 },
                new[] { 608, 12970 },
                new[] { 158, 2792 },
                new[] { 298, 7341 },
                new[] { 358, 3044 }
            };
            foreach (var tail in observed)
            {
                True(
                    GeminiLocalVadDeferredTurnAudio.IsReadyForNetwork(tail[0], tail[1]),
                    "observed short speech tail must reach the network durationMs=" + tail[0] + " maxPeak=" + tail[1]);
            }
        }

        private static void GeminiLocalVadWeakTurnTimingUsesOriginalSpeedAudio()
        {
            const int sampleRate = 16000;
            var source = Combine(
                SignalAtSampleRate(16000, sampleRate, 5000),
                new byte[500 * sampleRate / 1000 * BytesPerSample],
                SignalAtSampleRate(1200, sampleRate, 5000));
            var output = RealtimeAudioFileProcessor.ProcessLocalVadPcm(source, sampleRate, 74);
            Equal(source.Length, output.Length, "fallback PCM duration must remain unchanged");
            True(BytesEqual(source, output), "fallback PCM samples must remain unchanged");
        }

        private static void GeminiLocalVadTurnGateBuffersUntilComplete()
        {
            using (var gate = new GeminiLocalVadTurnGate())
            {
                True(gate.WaitUntilReady(0), "new gate must start open");
                Equal(1, gate.BeginActivityEnd(), "first activityEnd count");
                True(gate.IsWaitingForTurnComplete, "first activityEnd must close the gate");
                True(!gate.WaitUntilReady(0), "next outbound item must remain buffered");

                var rejectedSecondEnd = false;
                try { gate.BeginActivityEnd(); }
                catch (InvalidOperationException) { rejectedSecondEnd = true; }
                True(rejectedSecondEnd, "a second activityEnd cannot pass a closed gate");

                True(gate.MarkGenerationComplete(), "generationComplete must permit the next activityStart");
                True(gate.WaitUntilReady(0), "generationComplete must wake the sender");
                True(gate.RequiresNextActivityStart, "only activityStart may pass after generationComplete");
                True(gate.BeginActivityStart(), "next activityStart must advance the previous turn");
                True(!gate.WaitUntilReady(0), "audio must remain buffered until turnComplete");
                True(gate.CompleteCurrentTurn(), "turnComplete must release buffered audio");
                True(gate.WaitUntilReady(0), "gate must open for audio after turnComplete");
                Equal(1, gate.CompletedTurns, "first completed turn");

                Equal(2, gate.BeginActivityEnd(), "second activityEnd count");
                True(!gate.WaitUntilReady(0), "second turn must close the gate again");
                True(gate.CompleteCurrentTurn(), "interrupted turnComplete must remain a fallback");
                Equal(2, gate.CompletedTurns, "two completed turns");
                True(!gate.CompleteCurrentTurn(), "duplicate completion must not be counted");
                Equal(2, gate.CompletedTurns, "duplicate completion count");

                Equal(3, gate.BeginActivityEnd(), "third activityEnd count");
                gate.CancelWait();
                True(gate.IsAborted, "cancelled gate must remain aborted");
                True(!gate.WaitUntilReady(0), "cancellation must not reopen the sender gate");
                True(!gate.MarkGenerationComplete(), "late generationComplete must not reopen an aborted gate");
                True(!gate.CompleteCurrentTurn(), "late turnComplete must not reopen an aborted gate");
            }
        }

        private static void GeminiLocalVadCaptureConfiguration()
        {
            var settings = new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative };
            using (var session = new RealtimeSpeechSession(settings, "stage one", false, "must not activate second stage"))
            {
                Equal(20, session.CaptureChunkDurationMs, "local VAD capture duration");
                Equal(16000, session.RecordingSampleRate, "local VAD sample rate");
                Equal(180000, session.MaximumRecordingDurationMs, "local VAD maximum recording duration");
                var longField = typeof(RealtimeSpeechSession).GetField(
                    "longRecordingEnabled",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var localField = typeof(RealtimeSpeechSession).GetField(
                    "geminiLocalVadEnabled",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Equal(false, longField.GetValue(session), "local VAD must not use Long Recording second stage");
                Equal(true, localField.GetValue(session), "local VAD route enabled");
            }

            using (var cumulative = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "stage one",
                false,
                "must not activate second stage"))
            {
                Equal(20, cumulative.CaptureChunkDurationMs, "cumulative Local VAD capture duration");
                Equal(16000, cumulative.RecordingSampleRate, "cumulative Local VAD sample rate");
                Equal(180000, cumulative.MaximumRecordingDurationMs, "cumulative Local VAD maximum duration");
                var longField = typeof(RealtimeSpeechSession).GetField(
                    "longRecordingEnabled",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var localField = typeof(RealtimeSpeechSession).GetField(
                    "geminiLocalVadEnabled",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var retiredCumulativeFlag = typeof(RealtimeSpeechSession).GetField(
                    "geminiLocalVadCumulativeEnabled",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Equal(false, longField.GetValue(cumulative), "cumulative Local VAD must not use Long Recording second stage");
                Equal(true, localField.GetValue(cumulative), "cumulative Local VAD shared route enabled");
                True(retiredCumulativeFlag == null, "cumulative-only route must not keep a dead mode branch");
            }
        }

        private static void GeminiLocalVadIntermediateTurnsAccumulate()
        {
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "test",
                false,
                null))
            {
                var handle = typeof(RealtimeSpeechSession).GetMethod(
                    "HandleGeminiMessage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var replaceInput = typeof(RealtimeSpeechSession).GetMethod(
                    "ReplaceInputTranscription",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var finishedField = typeof(RealtimeSpeechSession).GetField(
                    "finishedSignal",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var stopField = typeof(RealtimeSpeechSession).GetField(
                    "stopRequested",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var gateField = typeof(RealtimeSpeechSession).GetField(
                    "localVadTurnGate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var expectedField = typeof(RealtimeSpeechSession).GetField(
                    "localVadFinalExpectedTurns",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var reset = typeof(RealtimeSpeechSession).GetMethod(
                    "ResetGeneratedTextAtCumulativeActivityEnd",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var currentGenerated = typeof(RealtimeSpeechSession).GetMethod(
                    "CurrentGeneratedText",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (handle == null || replaceInput == null || finishedField == null || stopField == null || gateField == null ||
                    expectedField == null || reset == null || currentGenerated == null)
                {
                    throw new InvalidOperationException("local VAD completion hooks not found");
                }

                var finished = (System.Threading.ManualResetEvent)finishedField.GetValue(session);
                var gate = (GeminiLocalVadTurnGate)gateField.GetValue(session);
                replaceInput.Invoke(session, new object[] { new string('x', 200) });
                gate.BeginActivityEnd();
                handle.Invoke(session, new object[]
                {
                    "{\"serverContent\":{\"outputTranscription\":{\"text\":\"first turn.\"},\"generationComplete\":true}}"
                });
                True(!finished.WaitOne(0), "intermediate turn must not finish the recording session");
                Equal(0, gate.CompletedTurns, "generationComplete must not count or release the first turn");
                True(gate.WaitUntilReady(0), "generationComplete must release the queued activityStart");
                True(gate.RequiresNextActivityStart, "generationComplete must not release audio directly");
                True(gate.BeginActivityStart(), "queued activityStart must advance the first turn");
                Equal("first turn.", session.GetBestEffortText(), "activityStart must not clear the prior output");
                True(!gate.WaitUntilReady(0), "audio must wait for the first turnComplete");

                handle.Invoke(session, new object[]
                {
                    "{\"serverContent\":{\"turnComplete\":true}}"
                });
                Equal(1, gate.CompletedTurns, "turnComplete must count the first turn");

                stopField.SetValue(session, 1);
                expectedField.SetValue(session, 2);
                gate.BeginActivityEnd();
                reset.Invoke(session, new object[] { 2 });
                Equal("", currentGenerated.Invoke(session, new object[0]), "second activityEnd must establish the cumulative output boundary");
                handle.Invoke(session, new object[]
                {
                    "{\"serverContent\":{\"outputTranscription\":{\"text\":\"second turn.\"},\"modelTurn\":{\"parts\":[{\"text\":\"second turn.\"}]},\"generationComplete\":true}}"
                });
                True(finished.WaitOne(0), "final generationComplete must finish final delivery");
                Equal(1, gate.CompletedTurns, "final generationComplete must not count as turnComplete");
                True(gate.WaitUntilReady(0), "final generationComplete may release a nonexistent next activityStart");
                True(gate.RequiresNextActivityStart, "final generationComplete must still not release audio directly");
                handle.Invoke(session, new object[]
                {
                    "{\"serverContent\":{\"turnComplete\":true}}"
                });
                True(finished.WaitOne(0), "final completion must remain signaled after turnComplete");
                Equal(2, gate.CompletedTurns, "both completed turns must be counted");
                True(gate.WaitUntilReady(0), "final turnComplete must still close and release the network turn");
                Equal("second turn.", session.GetBestEffortText(), "only the newest cumulative turn must remain");
            }
        }

        private static void GeminiLocalVadGeneratedTurnBoundaryIsReadable()
        {
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "test",
                false,
                null))
            {
                var merge = typeof(RealtimeSpeechSession).GetMethod(
                    "MergeGeneratedTranscriptionSnapshot",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var retiredBoundary = typeof(RealtimeSpeechSession).GetField(
                    "pendingGeneratedTurnBoundary",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (merge == null)
                {
                    throw new InvalidOperationException("local VAD generated text hook not found");
                }
                True(retiredBoundary == null, "retired delta boundary state remained");

                merge.Invoke(session, new object[] { "First sentence." });
                merge.Invoke(session, new object[] { "First sentence. Second sentence." });
                Equal(
                    "First sentence. Second sentence.",
                    session.GetBestEffortText(),
                    "a revised cumulative snapshot must replace rather than duplicate its prefix");
            }

            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "test",
                false,
                null))
            {
                var merge = typeof(RealtimeSpeechSession).GetMethod(
                    "MergeGeneratedTranscriptionSnapshot",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                merge.Invoke(session, new object[] { "第一句。" });
                merge.Invoke(session, new object[] { "第二句。" });
                Equal("第一句。第二句。", session.GetBestEffortText(), "CJK turns must not gain a Latin space");
            }
        }

        private static void GeminiLocalVadFinalCompletionRejectsStaleGeneration()
        {
            True(
                !GeminiLocalVadFinalCompletionPolicy.IsReady(2, 1, 1),
                "an earlier generationComplete cannot finish a later final turn");
            True(
                GeminiLocalVadFinalCompletionPolicy.IsReady(2, 2, 1),
                "the final turn generationComplete must allow delivery");
            True(
                GeminiLocalVadFinalCompletionPolicy.IsReady(2, 1, 2),
                "turnComplete must remain a fallback for an interrupted final generation");
            True(
                !GeminiLocalVadFinalCompletionPolicy.IsReady(0, 0, 0),
                "an empty session must not fabricate a completion signal");
        }

        private static void GeminiLocalVadFinalTextTailIsAdaptiveAndBounded()
        {
            Equal(500, GeminiLocalVadFinalTextStabilityPolicy.MinimumWaitMs, "minimum post-completion wait");
            Equal(600, GeminiLocalVadFinalTextStabilityPolicy.RequiredStableMs, "required text stability");
            Equal(3500, GeminiLocalVadFinalTextStabilityPolicy.MaximumWaitMs, "maximum tail wait");
            True(
                !GeminiLocalVadFinalTextStabilityPolicy.IsReady(499, 2000, 100),
                "stable text cannot bypass the minimum post-completion wait");
            True(
                !GeminiLocalVadFinalTextStabilityPolicy.IsReady(500, 599, 100),
                "a late transcription update must restart the stability window");
            True(
                GeminiLocalVadFinalTextStabilityPolicy.IsReady(500, 600, 100),
                "stable non-empty text may complete at the adaptive boundary");
            True(
                !GeminiLocalVadFinalTextStabilityPolicy.IsReady(2000, 2000, 0),
                "empty text cannot complete the tail wait early");
        }

        private static void GeminiLocalVadGenerationCompleteDoesNotReleaseTurn()
        {
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "test",
                false,
                null))
            {
                var handle = typeof(RealtimeSpeechSession).GetMethod(
                    "HandleGeminiMessage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var gateField = typeof(RealtimeSpeechSession).GetField(
                    "localVadTurnGate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (handle == null || gateField == null)
                {
                    throw new InvalidOperationException("local VAD generationComplete gate hooks not found");
                }

                var gate = (GeminiLocalVadTurnGate)gateField.GetValue(session);
                gate.BeginActivityEnd();
                handle.Invoke(session, new object[]
                {
                    "{\"serverContent\":{\"generationComplete\":true}}"
                });
                Equal(0, gate.CompletedTurns, "generationComplete must not count the turn");
                True(gate.WaitUntilReady(0), "generationComplete must release activityStart");
                True(gate.RequiresNextActivityStart, "generationComplete must not release audio directly");
                True(gate.BeginActivityStart(), "activityStart must be the only released boundary");
                True(!gate.WaitUntilReady(0), "audio must remain blocked before turnComplete");
                handle.Invoke(session, new object[]
                {
                    "{\"serverContent\":{\"turnComplete\":true}}"
                });
                Equal(1, gate.CompletedTurns, "turnComplete must count the turn");
                True(gate.WaitUntilReady(0), "turnComplete must open the gate");
            }
        }

        private static void GeminiLocalVadInterruptedTurnWaitsForTurnComplete()
        {
            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.GeminiLocalVadCumulative },
                "test",
                false,
                null))
            {
                var handle = typeof(RealtimeSpeechSession).GetMethod(
                    "HandleGeminiMessage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var stopField = typeof(RealtimeSpeechSession).GetField(
                    "stopRequested",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var gateField = typeof(RealtimeSpeechSession).GetField(
                    "localVadTurnGate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var expectedField = typeof(RealtimeSpeechSession).GetField(
                    "localVadFinalExpectedTurns",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var finishedField = typeof(RealtimeSpeechSession).GetField(
                    "finishedSignal",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (handle == null || stopField == null || gateField == null || expectedField == null ||
                    finishedField == null)
                {
                    throw new InvalidOperationException("local VAD interrupted turn hooks not found");
                }

                var gate = (GeminiLocalVadTurnGate)gateField.GetValue(session);
                gate.BeginActivityEnd();
                stopField.SetValue(session, 1);
                expectedField.SetValue(session, 1);
                handle.Invoke(session, new object[]
                {
                    "{\"serverContent\":{\"interrupted\":true}}"
                });
                True(!((System.Threading.ManualResetEvent)finishedField.GetValue(session)).WaitOne(0),
                    "interrupted signal alone must wait for turnComplete");
                handle.Invoke(session, new object[]
                {
                    "{\"serverContent\":{\"turnComplete\":true}}"
                });

                var finished = (System.Threading.ManualResetEvent)finishedField.GetValue(session);
                True(finished.WaitOne(0), "turnComplete must close an interrupted turn");
                Equal(1, gate.CompletedTurns, "interrupted turn must count exactly once");
            }
        }

        private static void LongRecordingModesUseStrictBoundaries()
        {
            True(!GeminiLongRecordingPolicy.ShouldPrewarm(5999, AiProvider.Gemini), "Gemini prewarm must not run before 6 seconds");
            True(GeminiLongRecordingPolicy.ShouldPrewarm(6000, AiProvider.Gemini), "Gemini prewarm must run at 6 seconds");
            True(!GeminiLongRecordingPolicy.UseTwoStage(10000, AiProvider.Gemini), "Gemini 10000ms must remain Integrated");
            True(GeminiLongRecordingPolicy.UseTwoStage(10001, AiProvider.Gemini), "Gemini 10001ms must use two stages");

            True(!GeminiLongRecordingPolicy.ShouldPrewarm(29999, AiProvider.OpenAi), "OpenAI prewarm must not run before 30 seconds");
            True(GeminiLongRecordingPolicy.ShouldPrewarm(30000, AiProvider.OpenAi), "OpenAI prewarm must run at 30 seconds");
            True(!GeminiLongRecordingPolicy.UseTwoStage(35000, AiProvider.OpenAi), "OpenAI 35000ms must remain Integrated");
            True(GeminiLongRecordingPolicy.UseTwoStage(35001, AiProvider.OpenAi), "OpenAI 35001ms must use two stages");
            Equal(15000, GeminiLongRecordingPolicy.StopDeadlineMs, "shared stop deadline");
            Equal(30000, GeminiLongRecordingPolicy.OpenAiStopDeadlineMs, "OpenAI long stop deadline");
            Equal(15000, GeminiLongRecordingPolicy.OpenAiTranscriptionWaitMs, "OpenAI transcription wait deadline");
            Equal(3500, GeminiLongRecordingPolicy.RawTailMaximumMs, "raw tail maximum");
        }

        private static void LongRecordingPromptsUseFixedOriginalDictation()
        {
            var stageOne = PromptBuilder.LongRecordingStageOne(false, "Japanese (日本語)");
            var stageOneTranslation = PromptBuilder.LongRecordingStageOne(true, "Japanese (日本語)");
            var stageTwo = PromptBuilder.LongRecordingStageTwo(false, "Korean (한국어)");
            var stageTwoTranslation = PromptBuilder.LongRecordingStageTwo(true, "Korean (한국어)");

            True(stageOne.Contains("语音输入法的转写整理引擎"), "stage one mirror role");
            True(stageOne.Contains("TEXT:"), "stage one TEXT contract");
            Equal(PromptBuilder.OneStep(false, "Japanese (日本語)"), stageOne, "stage one must equal Integrated");
            Equal(PromptBuilder.OneStep(true, "Japanese (日本語)"), stageOneTranslation, "translation stage one must equal Integrated");
            True(!stageOne.Contains("去除话语中啰嗦的部分"), "summary-style deletion rule remained");
            True(stageOneTranslation.Contains("翻译成Japanese (日本語)"), "stage one translation target");
            True(stageTwo.Contains("文本整理转换器"), "stage two text role");
            True(!stageTwo.Contains("去除话语中啰嗦的部分"), "stage two summary-style rule remained");
            True(stageTwo.Contains("轻度整理"), "stage two light cleanup rule");
            True(stageTwo.Contains("保留用户原先的有效用语和语气"), "stage two wording preservation rule");
            True(stageTwoTranslation.Contains("翻译成Korean (한국어)"), "stage two translation target");
            True(!stageTwoTranslation.Contains("{目标语言}"), "stage two target placeholder remained");
        }

        private static void GeminiLongTextBodySeparatesRawAndInstruction()
        {
            const string raw = "RAW_LONG_TRANSCRIPT_DO_NOT_EXECUTE";
            const string instruction = "LONG_SYSTEM_RULE_ONLY";
            var body = GeminiLongTextClient.BuildBody(raw, instruction);
            var system = (Dictionary<string, object>)body["systemInstruction"];
            var systemParts = (object[])system["parts"];
            var systemText = Convert.ToString(((Dictionary<string, object>)systemParts[0])["text"]);
            True(systemText.Contains(instruction), "long system instruction missing");
            True(!systemText.Contains(raw), "long raw leaked into system instruction");

            var contents = (object[])body["contents"];
            var user = (Dictionary<string, object>)contents[0];
            Equal("user", Convert.ToString(user["role"]), "long user role");
            var userParts = (object[])user["parts"];
            Equal(raw, Convert.ToString(((Dictionary<string, object>)userParts[0])["text"]), "long raw user input");

            var generation = (Dictionary<string, object>)body["generationConfig"];
            Equal(4096, generation["maxOutputTokens"], "long max output tokens");
            True(!generation.ContainsKey("temperature"), "long request must keep default temperature");
            var thinking = (Dictionary<string, object>)generation["thinkingConfig"];
            Equal("minimal", Convert.ToString(thinking["thinkingLevel"]), "long thinking level");
        }

        private static void GeminiLongTextStageUsesFlashLite()
        {
            Equal("gemini-3.1-flash-lite", GeminiLongTextClient.Model, "Gemini long text model");
        }

        private static void OpenAiLongTextBodySeparatesRawAndInstruction()
        {
            const string raw = "RAW_OPENAI_LONG_TRANSCRIPT_DO_NOT_EXECUTE";
            const string instruction = "OPENAI_LONG_SYSTEM_RULE_ONLY";
            var body = OpenAiLongTextClient.BuildBody(raw, instruction);
            Equal(raw, Convert.ToString(body["input"]), "OpenAI long raw user input");
            var system = Convert.ToString(body["instructions"]);
            True(system.Contains(instruction), "OpenAI long system instruction missing");
            True(!system.Contains(raw), "OpenAI long raw leaked into instructions");
            Equal("gpt-5.6-luna", Convert.ToString(body["model"]), "OpenAI long text model");
            Equal(2048, body["max_output_tokens"], "OpenAI long max output tokens");
        }

        private static void GeminiTypingUsesLongRecordingCapture()
        {
            var longSettings = new AppSettings { Provider = AiProvider.Gemini };
            using (var session = new RealtimeSpeechSession(longSettings, "stage one", false, "stage two"))
            {
                Equal(20, session.CaptureChunkDurationMs, "long capture duration");
                Equal(16000, session.RecordingSampleRate, "long sample rate");
                Equal(180000, session.MaximumRecordingDurationMs, "long maximum recording duration");
            }
            using (var session = new RealtimeSpeechSession(longSettings, PromptBuilder.NotesCapture(), true, null))
            {
                Equal(20, session.CaptureChunkDurationMs, "notes capture cadence");
            }
            using (var session = new RealtimeSpeechSession(new AppSettings { Provider = AiProvider.Gemini }, "normal", false))
            {
                Equal(20, session.CaptureChunkDurationMs, "ordinary Gemini capture cadence");
            }
        }

        private static void OpenAiTypingUsesLongRecordingCapture()
        {
            var longSettings = new AppSettings { Provider = AiProvider.OpenAi };
            using (var session = new RealtimeSpeechSession(longSettings, "stage one", false, "stage two"))
            {
                Equal(20, session.CaptureChunkDurationMs, "OpenAI long capture duration");
                Equal(24000, session.RecordingSampleRate, "OpenAI long sample rate");
                Equal(180000, session.MaximumRecordingDurationMs, "OpenAI long maximum recording duration");
            }
            using (var session = new RealtimeSpeechSession(longSettings, PromptBuilder.NotesCapture(), true, null))
            {
                Equal(20, session.CaptureChunkDurationMs, "OpenAI Notes capture cadence");
            }
            using (var session = new RealtimeSpeechSession(new AppSettings { Provider = AiProvider.OpenAi }, "normal", false))
            {
                Equal(20, session.CaptureChunkDurationMs, "ordinary OpenAI capture cadence");
            }
        }

        private static void RealtimeCaptureAndUploadCadence()
        {
            Equal(40, RealtimePcmBatcher.FrameDurationMs, "network upload frame duration");
            var batcher = new RealtimePcmBatcher(16000);
            Equal(
                0,
                TotalBytes(batcher.Process(new List<byte[]>
                {
                    SignalAtSampleRate(20, 16000, 3000)
                })),
                "one 20ms capture remains buffered");
            var output = batcher.Process(new List<byte[]>
            {
                SignalAtSampleRate(20, 16000, 3000)
            });
            Equal(1, output.Count, "two 20ms captures make one upload frame");
            Equal(40 * 16000 / 1000 * BytesPerSample, output[0].Length, "40ms upload PCM bytes");
            Equal(0, TotalBytes(batcher.Finish()), "no trailing bytes after exact frame");

            var tailBatcher = new RealtimePcmBatcher(16000);
            tailBatcher.Process(new List<byte[]>
            {
                SignalAtSampleRate(20, 16000, 3000)
            });
            var tail = tailBatcher.Finish();
            Equal(1, tail.Count, "stop must flush a partial upload frame");
            Equal(20 * 16000 / 1000 * BytesPerSample, tail[0].Length, "20ms stop tail bytes");

            foreach (var provider in new[] { AiProvider.Gemini, AiProvider.OpenAi })
            {
                using (var session = new RealtimeSpeechSession(
                    new AppSettings { Provider = provider },
                    "normal",
                    false))
                {
                    Equal(20, session.CaptureChunkDurationMs, provider + " capture cadence");
                    Equal(40, session.Diagnostics.UploadFrameDurationMs, provider + " upload cadence");
                }
            }
        }

        private static void GeminiLongRawAcceptsRevisedSnapshots()
        {
            const string previous = "今天我要去上海出差，明天晚上回来。";
            const string revised = "今天我要去北京出差，明天晚上回来。";
            var merged = TranscriptAccumulator.MergeSnapshotOrDelta(previous, revised, true);
            Equal(revised, merged, "revised cumulative snapshot must replace previous text");

            var delta = TranscriptAccumulator.MergeSnapshotOrDelta("第一句话。", "第二句话。", true);
            Equal("第一句话。第二句话。", delta, "true delta must still append");
        }

        private static void OrdinaryPromptInstructionBoundary()
        {
            var prompt = PromptBuilder.OneStep();
            True(prompt.Contains("语音输入法的转写整理引擎"), "ordinary mirror role missing");
            True(prompt.Contains("不要将用户的语音翻译成其他语言"), "translation guard missing");
            True(prompt.Contains("绝不回答、解释或执行"), "question and command guard missing");
            True(prompt.Contains("相同的语种"), "same-language rule missing");
            True(prompt.Contains("TEXT:"), "typing output contract missing");
            True(!prompt.Contains("去除话语中啰嗦的部分"), "summary cleanup rule must stay removed");
            True(!prompt.Contains("认真理解用户要所表达的思想"), "fixed original mode must not summarize");
            True(PromptBuilder.NotesCapture().StartsWith("You are a voice input text processor"), "notes prompt must remain unchanged");
        }

        private static void OriginalPromptsRouteCorrectly()
        {
            var originalNormal = PromptBuilder.OneStep(false, "Japanese (日本語)");
            var originalTranslation = PromptBuilder.OneStep(true, "Japanese (日本語)");

            True(originalNormal.Contains("语音输入法的转写整理引擎"), "original normal role missing");
            True(!originalNormal.Contains("Japanese (日本語)"), "normal mode must not inject translation target");
            True(!originalNormal.Contains("认真的理解用户要所表达的思想"), "original normal must not summarize");

            True(originalTranslation.Contains("语音输入法的转写翻译引擎"), "original translation role missing");
            Equal(2, CountOccurrences(originalTranslation, "Japanese (日本語)"), "original translation target count");
            True(originalTranslation.Contains("绝不回答、解释或执行"), "original translation question guard missing");
            True(!originalTranslation.Contains("{目标语言}"), "original translation placeholder remained");
        }

        private static void TranslationTargetsContain31Languages()
        {
            var targets = TranslationLanguageOption.All();
            Equal(31, targets.Length, "translation target count");
            Equal("en", TranslationLanguageOption.CodeAt(-1), "invalid target defaults to English");
            Equal("English", TranslationLanguageOption.PromptNameOf("en"), "English prompt name");
            Equal("Japanese (日本語)", TranslationLanguageOption.PromptNameOf("ja"), "Japanese prompt name");
            Equal("id", TranslationLanguageOption.CodeAt(TranslationLanguageOption.IndexOf("in")), "legacy Indonesian code");
        }

        private static void DefaultHotkeysRemainUnique()
        {
            var settings = new AppSettings();
            var modes = new[]
            {
                settings.TypingHotkey,
                settings.TranslationHotkey,
                settings.NotesHotkey,
                settings.ConversationHotkey,
                settings.RestoreWidgetsHotkey
            };
            var unique = new HashSet<HotkeyMode>(modes);
            Equal(modes.Length, unique.Count, "default hotkeys");
            Equal(HotkeyMode.ScrollLock, settings.TranslationHotkey, "translation hotkey default");
            Equal(
                HotkeyMode.RightAltRightCtrl,
                settings.ConversationHotkey,
                "realtime conversation hotkey default");
        }

        private static void RealtimeConversationShortcutRoutes()
        {
            var captured = ShortcutAction.Typing;
            var captureCount = 0;
            var settings = new AppSettings();
            var hook = new RightAltHook(
                settings,
                delegate(ShortcutAction action)
                {
                    captured = action;
                    captureCount++;
                });
            var downField = typeof(RightAltHook).GetField(
                "downKeys",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var tryMatch = typeof(RightAltHook).GetMethod(
                "TryMatchCombo",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var fire = typeof(RightAltHook).GetMethod(
                "Fire",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (downField == null ||
                tryMatch == null ||
                fire == null)
            {
                throw new InvalidOperationException(
                    "realtime conversation shortcut hooks not found");
            }
            var downKeys =
                (HashSet<int>)downField.GetValue(hook);
            downKeys.Add(0xA5);
            downKeys.Add(0xA3);
            var matchArguments = new object[]
            {
                ShortcutAction.Typing,
                HotkeyMode.RightAlt
            };
            True(
                Convert.ToBoolean(
                    tryMatch.Invoke(hook, matchArguments)),
                "Right Alt + Right Ctrl must match a shortcut");
            Equal(
                ShortcutAction.Conversation,
                (ShortcutAction)matchArguments[0],
                "default realtime combination action");
            Equal(
                HotkeyMode.RightAltRightCtrl,
                (HotkeyMode)matchArguments[1],
                "default realtime combination mode");

            fire.Invoke(
                hook,
                new object[] { ShortcutAction.Conversation });
            Equal(1, captureCount, "conversation shortcut callback count");
            Equal(
                ShortcutAction.Conversation,
                captured,
                "conversation shortcut callback action");

            var normalize = typeof(AppSettings).GetMethod(
                "NormalizeHotkeys",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (normalize == null)
            {
                throw new InvalidOperationException(
                    "hotkey normalization hook not found");
            }
            settings.ConversationHotkey =
                settings.RestoreWidgetsHotkey;
            normalize.Invoke(settings, null);
            Equal(
                HotkeyMode.PauseBreak,
                settings.RestoreWidgetsHotkey,
                "existing widget shortcut must survive migration");
            True(
                settings.ConversationHotkey !=
                settings.RestoreWidgetsHotkey,
                "new conversation shortcut must migrate away from a conflict");

            var mainField = typeof(MainForm).GetField(
                "conversationHotkeyBox",
                BindingFlags.Instance | BindingFlags.NonPublic);
            True(
                mainField != null &&
                mainField.FieldType ==
                typeof(System.Windows.Forms.ComboBox),
                "settings must expose a realtime conversation shortcut dropdown");
            var source = File.ReadAllText(
                Path.Combine(FindProjectRoot(), "Program.cs"),
                Encoding.UTF8);
            True(
                source.IndexOf(
                    "\"conversationHotkey=\"",
                    StringComparison.Ordinal) >= 0,
                "realtime shortcut must persist in settings");
            True(
                source.IndexOf(
                    "ShortcutAction.Conversation) ToggleRealtimeConversation()",
                    StringComparison.Ordinal) >= 0,
                "realtime shortcut must start or stop the conversation route");
        }

        private static void TranslationOverlayIsLightGreen()
        {
            var method = typeof(BottomStatusOverlayForm).GetMethod(
                "TargetColor",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("TargetColor not found");
            var color = (Color)method.Invoke(null, new object[] { CaptureTarget.Translation });
            Equal(Color.FromArgb(76, 175, 125).ToArgb(), color.ToArgb(), "translation recording color");
        }

        private static void TranslationLabelsCoverAllLanguages()
        {
            var options = LanguageOption.All();
            Equal(32, options.Length, "system plus 31 language entries");
            foreach (var option in options)
            {
                var localizer = Localizer.For(option.Code);
                True(localizer.T("translation_target") != "translation_target", "translation target label missing for " + option.Code);
                True(localizer.T("translation_hotkey") != "translation_hotkey", "translation hotkey label missing for " + option.Code);
                if (option.Code != "system" && option.Code != "en")
                {
                    True(localizer.T("translation_target") != "Target language", "translation target label fell back to English for " + option.Code);
                    True(localizer.T("translation_hotkey") != "Translation hotkey", "translation hotkey label fell back to English for " + option.Code);
                }
            }
        }

        private static void RetiredTextModeLabelsAreAbsent()
        {
            foreach (var option in LanguageOption.All())
            {
                var localizer = Localizer.For(option.Code);
                Equal("text_mode", localizer.T("text_mode"), "retired text mode label leaked for " + option.Code);
                Equal("text_mode_original", localizer.T("text_mode_original"), "retired original label leaked for " + option.Code);
                Equal("text_mode_summary", localizer.T("text_mode_summary"), "retired summary label leaked for " + option.Code);
            }
        }

        private static void WidgetVisibilityLabelsCoverAllLanguages()
        {
            foreach (var option in LanguageOption.All())
            {
                var localizer = Localizer.For(option.Code);
                True(localizer.T("restore_hotkey") != "restore_hotkey", "widget visibility hotkey label missing for " + option.Code);
                True(localizer.T("widgets_shown") != "widgets_shown", "widgets shown label missing for " + option.Code);
                if (option.Code != "system" && option.Code != "en")
                {
                    True(localizer.T("restore_hotkey") != "Widget show/hide hotkey", "widget visibility label fell back to English for " + option.Code);
                    True(localizer.T("widgets_shown") != "widgets shown", "widgets shown label fell back to English for " + option.Code);
                }
            }
        }

        private static void WidgetSettingsLayoutMatchesContract()
        {
            True(
                DesktopSettingsLayout.RestoreHotkeyUsesVerticalLayout,
                "widget show/hide shortcut dropdown must be below its label");
            Equal(
                DesktopSettingsLayout.RestoreHotkeyLabelX,
                DesktopSettingsLayout.RestoreHotkeyBoxX,
                "widget shortcut label and dropdown share one column");
            var realtimeVisibilityField = typeof(MainForm).GetField(
                "realtimeWidgetBox",
                BindingFlags.Instance | BindingFlags.NonPublic);
            True(
                realtimeVisibilityField != null &&
                realtimeVisibilityField.FieldType ==
                typeof(System.Windows.Forms.CheckBox),
                "settings must expose an independent realtime conversation visibility checkbox");
            True(
                typeof(NotesWidgetForm).GetField(
                    "recordButton",
                    BindingFlags.Instance | BindingFlags.NonPublic) == null,
                "Notes window must not keep a mouse recording trigger");
            True(
                typeof(RealtimeConversationWidgetForm).GetField(
                    "conversationButton",
                    BindingFlags.Instance | BindingFlags.NonPublic) == null,
                "realtime window must not keep a mouse conversation trigger");
        }

        private static void WidgetVisibilityPreferencesRemainIndependent()
        {
            True(
                WidgetVisibilityPolicy.HasAnyEnabled(true, false),
                "Notes-only preference must keep the widget group callable");
            True(
                WidgetVisibilityPolicy.HasAnyEnabled(false, true),
                "realtime-only preference must keep the widget group callable");
            True(
                !WidgetVisibilityPolicy.HasAnyEnabled(false, false),
                "both disabled preferences must keep the widget group hidden");
            True(
                WidgetVisibilityPolicy.ShouldShow(true, true),
                "enabled window must show while the group is logically visible");
            True(
                !WidgetVisibilityPolicy.ShouldShow(true, false),
                "disabled window must stay hidden even when its partner is visible");
            True(
                !WidgetVisibilityPolicy.ShouldShow(false, true),
                "shortcut-hidden group must not be reopened by a saved preference");
        }

        private static void FeatureLabelsCoverAllLanguages()
        {
            var english = Localizer.For("en");
            var keys = FeatureLocalizationCatalog.RequiredKeys;
            True(
                keys.Length >= 25,
                "active voice feature localization key count");
            Equal(
                keys.Length,
                new HashSet<string>(keys).Count,
                "291 localization keys must be unique");
            foreach (var option in LanguageOption.All())
            {
                var localizer = Localizer.For(option.Code);
                foreach (var key in keys)
                {
                    var value = localizer.T(key);
                    True(value != key, "new voice feature label missing for " + option.Code + ": " + key);
                    True(!string.IsNullOrWhiteSpace(value), "new voice feature label empty for " + option.Code + ": " + key);
                }
                True(localizer.T("tray_show_notes") != "tray_show_notes", "tray Notes label missing for " + option.Code);
                if (option.Code != "system" && option.Code != "en")
                {
                    True(
                        localizer.T("conversation_hotkey") !=
                        english.T("conversation_hotkey"),
                        "realtime conversation shortcut fell back to English for " +
                        option.Code);
                    True(
                        localizer.T("show_realtime_conversation") !=
                        english.T("show_realtime_conversation"),
                        "realtime conversation visibility setting fell back to English for " +
                        option.Code);
                    True(
                        localizer.T("gemini_realtime_search") !=
                        english.T("gemini_realtime_search"),
                        "realtime search setting fell back to English for " +
                        option.Code);
                }
                Equal(
                    "qa_title",
                    localizer.T("qa_title"),
                    "retired Simple Q&A title leaked for " + option.Code);
                Equal(
                    "show_qa",
                    localizer.T("show_qa"),
                    "retired Simple Q&A visibility leaked for " +
                    option.Code);
                foreach (var retired in new[]
                {
                    "conversation_start",
                    "conversation_help",
                    "wake_conversation",
                    "privacy",
                    "show_realtime",
                    "realtime_translation_title",
                    "show_realtime_translation"
                })
                {
                    Equal(
                        retired,
                        localizer.T(retired),
                        "retired settings text leaked for " +
                        option.Code + ": " + retired);
                }
            }
            Equal(
                "privacy",
                english.T("privacy"),
                "retired API-key privacy paragraph must not remain visible");
        }

        private static void ScheduleLabelsCoverAllLanguages()
        {
            var keys = ScheduleLocalizationCatalog.RequiredKeys;
            var english = Localizer.For("en");
            Equal(8, keys.Length, "schedule localization key count");
            Equal(15, NotesWidgetForm.MaximumSchedulePreviewLines, "schedule preview line limit");
            True(
                typeof(NotesWidgetForm).GetMethod(
                    "OnActivated",
                    BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly) != null,
                "schedule card must refresh when the Notes window returns to the foreground");
            foreach (var option in LanguageOption.All())
            {
                var localizer = Localizer.For(option.Code);
                foreach (var key in keys)
                {
                    var value = localizer.T(key);
                    True(value != key, "schedule label missing for " + option.Code + ": " + key);
                    True(!string.IsNullOrWhiteSpace(value), "schedule label empty for " + option.Code + ": " + key);
                }
                Equal(
                    "privacy",
                    localizer.T("privacy"),
                    "retired settings privacy paragraph leaked for " +
                    option.Code);
                if (option.Code != "system" && option.Code != "en")
                {
                    True(
                        localizer.T("schedule_title") != english.T("schedule_title"),
                        "schedule title fell back to English for " + option.Code);
                }
            }
        }

        private static void TypingOutputContractIsStrict()
        {
            string text;
            True(TypingOutputContract.TryParse("  text:完整正文", out text), "lowercase prefix");
            Equal("完整正文", text, "lowercase prefix body");
            True(TypingOutputContract.TryParse("\r\nTEXT：正文", out text), "full-width colon");
            Equal("正文", text, "full-width colon body");
            True(TypingOutputContract.TryParse("TEXT:TEXT:正文", out text), "nested prefix body");
            Equal("TEXT:正文", text, "only the first prefix may be removed");
            True(!TypingOutputContract.TryParse("正文", out text), "missing prefix must fail");
            True(!TypingOutputContract.TryParse("TEXT:  ", out text), "empty body must fail");
            True(!TypingOutputContract.TryParse("TEXT正文", out text), "missing colon must fail");
            Equal(
                "完整正文",
                CaptureOutputPolicy.AcceptProcessedFallback(
                    CaptureTarget.Typing,
                    "TEXT:完整正文"),
                "typing WAV fallback must strip one valid prefix");
            Equal(
                "",
                CaptureOutputPolicy.AcceptProcessedFallback(
                    CaptureTarget.Translation,
                    "缺少前缀"),
                "invalid typing WAV fallback must not bypass RAW fallback");
            Equal(
                "记事正文",
                CaptureOutputPolicy.AcceptProcessedFallback(
                    CaptureTarget.Notes,
                    "记事正文"),
                "notes must not inherit the typing prefix contract");

            using (var session = new RealtimeSpeechSession(
                new AppSettings { Provider = AiProvider.Gemini },
                PromptBuilder.OneStep(),
                false,
                PromptBuilder.LongRecordingStageTwo(false, "English")))
            {
                var replaceInput = typeof(RealtimeSpeechSession).GetMethod(
                    "ReplaceInputTranscription",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var addPart = typeof(RealtimeSpeechSession).GetMethod(
                    "AddPart",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (replaceInput == null || addPart == null)
                {
                    throw new InvalidOperationException("typing RAW fallback hooks not found");
                }
                replaceInput.Invoke(session, new object[] { "完整 RAW 转写" });
                addPart.Invoke(session, new object[] { "这是错误的回答，没有前缀" });
                Equal(
                    "完整 RAW 转写",
                    session.GetBestEffortText(),
                    "missing prefix must return the complete input transcription RAW");
            }
        }

        private static void StructuredTextOutputIsStrict()
        {
            string text;
            True(
                StructuredTextOutput.TryParse("{\"text\":\"完整正文\"}", out text),
                "single text field");
            Equal("完整正文", text, "structured text body");
            True(
                !StructuredTextOutput.TryParse(
                    "{\"text\":\"正文\",\"answer\":\"不允许\"}",
                    out text),
                "extra field must fail");
            True(!StructuredTextOutput.TryParse("{\"text\":\"\"}", out text), "empty text must fail");
            True(!StructuredTextOutput.TryParse("{\"answer\":\"正文\"}", out text), "missing field must fail");
            True(!StructuredTextOutput.TryParse("not-json", out text), "invalid JSON must fail");
            var schema = StructuredTextOutput.JsonSchema();
            Equal(false, Convert.ToBoolean(schema["additionalProperties"]), "schema must forbid extra fields");
        }

        private static void AutomaticPasteAcceptsGeneralStableFocusedTargets()
        {
            var textCandidateTypes = new[]
            {
                System.Windows.Automation.ControlType.Custom,
                System.Windows.Automation.ControlType.Pane,
                System.Windows.Automation.ControlType.Group,
                System.Windows.Automation.ControlType.Edit,
                System.Windows.Automation.ControlType.Document
            };
            foreach (var controlType in textCandidateTypes)
            {
                True(
                    NativeInput.IsFocusedTextCandidateControl(controlType),
                    controlType.ProgrammaticName +
                    " must remain eligible for stable-focus fallback");
            }

            var nonTextTypes = new[]
            {
                System.Windows.Automation.ControlType.Text,
                System.Windows.Automation.ControlType.Button,
                System.Windows.Automation.ControlType.Menu,
                System.Windows.Automation.ControlType.List,
                System.Windows.Automation.ControlType.Window
            };
            foreach (var controlType in nonTextTypes)
            {
                True(
                    !NativeInput.IsFocusedTextCandidateControl(controlType),
                    controlType.ProgrammaticName +
                    " must not become an automatic-paste fallback target");
            }

            True(
                NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        SupportsValuePattern = true,
                        ValueReadOnly = false,
                        FocusedTargetCandidate = true
                    }),
                "a writable ValuePattern target must be accepted");
            True(
                NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        SupportsTextPattern = true,
                        TextReadOnlyKnown = true,
                        TextReadOnly = false,
                        TextControl = false,
                        FocusedTargetCandidate = true
                    }),
                "Custom/Pane/Group TextPattern must not require Edit or Document ControlType");
            True(
                NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        SupportsTextPattern = true,
                        TextReadOnlyKnown = false,
                        FocusedTargetCandidate = true
                    }),
                "a provider with unknown TextPattern read-only state remains a candidate");
            True(
                !NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        SupportsValuePattern = true,
                        ValueReadOnly = true,
                        FocusedTargetCandidate = true
                    }),
                "ValuePattern read-only must be rejected by default");
            True(
                NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        SupportsValuePattern = true,
                        ValueReadOnly = true,
                        FocusedTargetCandidate = true,
                        AllowUnconfirmedValueReadOnly = true
                    }),
                "a nonstandard value-only target may enter the strong-evidence stage without an app whitelist");
            True(
                NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        FocusedTargetCandidate = true
                    }),
                "a nonstandard no-pattern target may enter stage one but still requires session anchor matching");
            True(
                NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        EnabledKnown = false,
                        InFocusedChain = true,
                        FocusedTargetCandidate = true
                    }),
                "an unsupported IsEnabled property must not be interpreted as disabled");

            Equal(
                16,
                NativeInput.MaximumEditableAncestorDepth,
                "deep custom editor ancestor search depth");
        }

        private static void AutomaticPasteAcceptsPatternBackedHostWindow()
        {
            var window = System.Windows.Automation.ControlType.Window;
            True(
                !NativeInput.IsFocusedPatternCandidate(
                    window,
                    false,
                    false),
                "a focused host Window without a text/value pattern must remain rejected");
            True(
                NativeInput.IsFocusedPatternCandidate(
                    window,
                    true,
                    false) &&
                NativeInput.IsFocusedPatternCandidate(
                    window,
                    false,
                    true),
                "a UIA bridge may collapse its focused editor into a pattern-backed host Window");

            True(
                !NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        SupportsValuePattern = true,
                        ValueReadOnly = true,
                        FocusedTargetCandidate =
                            NativeInput.IsFocusedPatternCandidate(
                                window,
                                true,
                                false)
                    }),
                "a standard host Window ValuePattern read-only report must be respected");
            True(
                NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        SupportsTextPattern = true,
                        TextReadOnlyKnown = true,
                        TextReadOnly = false,
                        FocusedTargetCandidate =
                            NativeInput.IsFocusedPatternCandidate(
                                window,
                                false,
                                true)
                    }),
                "a writable TextPattern host Window must remain eligible");
            True(
                NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        SupportsValuePattern = true,
                        ValueReadOnly = true,
                        SupportsTextPattern = true,
                        TextReadOnlyKnown = true,
                        TextReadOnly = false,
                        FocusedTargetCandidate = true
                    }),
                "focused writable TextPattern must override a conflicting advisory ValuePattern verdict");
            True(
                !NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        SupportsValuePattern = true,
                        ValueReadOnly = false,
                        SupportsTextPattern = true,
                        TextReadOnlyKnown = true,
                        TextReadOnly = true,
                        ReadOnlyConfirmed = true,
                        FocusedTargetCandidate =
                            NativeInput.IsFocusedPatternCandidate(
                                window,
                                false,
                                true)
                    }),
                "an explicitly read-only focused TextPattern must override a conflicting writable ValuePattern verdict");
        }

        private static void AutomaticPasteFreezesRecordingTargetThroughDelivery()
        {
            var foreground = new IntPtr(100);
            var startRuntimeId = new[] { 9, 8, 7 };
            var snapshot = new PasteTargetSnapshot(41)
            {
                TriggerAnchorAllowed = true,
                ForegroundWindow = foreground,
                ForegroundProcessId = 500,
                ForegroundThreadId = 600
            };
            True(
                snapshot.TryApplyAutomationCapture(
                    startRuntimeId,
                    true,
                    true,
                    foreground,
                    "automation_anchor_confirmed"),
                "the active recording target accepts its initial UIA identity");
            True(snapshot.AnchorConfirmed, "start-time target anchor confirmed");
            startRuntimeId[0] = 99;
            Equal(9, snapshot.FocusRuntimeId[0], "snapshot clones the captured RuntimeId");

            var routed = snapshot.FreezeForRoute();
            True(snapshot.Frozen && routed.Frozen, "stop freezes both active and routed target state");
            Equal(41L, routed.Generation, "frozen route generation");
            Equal(foreground, routed.ForegroundWindow, "frozen route foreground HWND");
            Equal((uint)500, routed.ForegroundProcessId, "frozen route foreground PID");
            True(
                !snapshot.TryApplyAutomationCapture(
                    new[] { 1, 2, 3 },
                    true,
                    true,
                    foreground,
                    "late_old_capture"),
                "a late UIA callback cannot overwrite a frozen route");
            routed.FocusRuntimeId[1] = 77;
            Equal(8, snapshot.FocusRuntimeId[1], "frozen route owns a cloned RuntimeId");

            var abandonedOldSession = new PasteTargetSnapshot(40)
            {
                TriggerAnchorAllowed = true
            };
            abandonedOldSession.Abandon();
            True(
                !abandonedOldSession.TryApplyAutomationCapture(
                    new[] { 4, 0 },
                    true,
                    true,
                    foreground,
                    "old_session_late_capture"),
                "an abandoned old session rejects a late automation result");
            True(
                NativeInput.IsPasteGenerationCurrent(41, 41) &&
                !NativeInput.IsPasteGenerationCurrent(40, 41) &&
                !NativeInput.IsPasteGenerationCurrent(0, 41),
                "only the currently frozen recording generation may deliver");

            var source = File.ReadAllText(
                Path.Combine(FindProjectRoot(), "Program.cs"),
                Encoding.UTF8);
            True(
                source.IndexOf(
                    "BeginPasteTargetSnapshot(",
                    StringComparison.Ordinal) >= 0 &&
                source.IndexOf(
                    "FreezeActivePasteTargetSnapshot(routeTarget)",
                    StringComparison.Ordinal) >= 0 &&
                source.IndexOf(
                    "routePasteTarget);",
                    StringComparison.Ordinal) >= 0 &&
                source.IndexOf(
                    "DeliverResult(text, fallback, pasteTarget);",
                    StringComparison.Ordinal) >= 0 &&
                source.IndexOf(
                    "NativeInput.SendPaste(\r\n                    pasteTarget,",
                    StringComparison.Ordinal) >= 0,
                "start, stop, route, delivery, and native injection must carry the same snapshot");
        }

        private static void AutomaticPasteAnchorsToExactForegroundWindow()
        {
            var foreground = new IntPtr(100);
            var otherWindowSameProcess = new IntPtr(101);
            var snapshot = new PasteTargetSnapshot(51)
            {
                Frozen = true,
                AnchorConfirmed = true,
                ForegroundWindow = foreground,
                ForegroundProcessId = 500,
                AutomationHostKnown = true,
                AutomationHostMatchesForeground = true,
                AutomationHostWindow = foreground,
                FocusRuntimeId = new[] { 5, 1 }
            };
            True(
                NativeInput.IsPasteSessionForegroundMatch(
                    snapshot,
                    foreground,
                    500),
                "the original foreground HWND and PID remain the session target");
            True(
                !NativeInput.IsPasteSessionForegroundMatch(
                    snapshot,
                    otherWindowSameProcess,
                    500),
                "another window in the same process must not receive the result");
            True(
                !NativeInput.IsPasteSessionForegroundMatch(
                    snapshot,
                    foreground,
                    501),
                "the same HWND with a changed PID must not receive the result");

            var startRuntimeUnavailable = new PasteTargetSnapshot(50)
            {
                Frozen = true,
                AnchorConfirmed = true,
                ForegroundWindow = foreground,
                ForegroundProcessId = 500,
                FocusRuntimeId = null
            };
            string startRuntimeFailure;
            True(
                !NativeInput.IsPasteTargetSnapshotMatch(
                    startRuntimeUnavailable,
                    new FocusedPasteTargetInspection
                    {
                        Editable = true,
                        FocusRuntimeId = new[] { 5, 0 }
                    },
                    new GuiThreadFocusSnapshot(),
                    out startRuntimeFailure),
                "a RuntimeId first appearing at delivery cannot upgrade an unknown start identity");
            Equal(
                "paste_target_start_runtime_unavailable",
                startRuntimeFailure,
                "start RuntimeId unavailable diagnostic");

            True(
                NativeInput.IsWindowOwnedByForegroundRoot(
                    foreground,
                    500,
                    foreground,
                    500),
                "matching native root and process are anchored");
            True(
                !NativeInput.IsWindowOwnedByForegroundRoot(
                    otherWindowSameProcess,
                    500,
                    foreground,
                    500),
                "same-process native root from another top-level window is rejected");
            True(
                !NativeInput.IsWindowOwnedByForegroundRoot(
                    foreground,
                    501,
                    foreground,
                    500),
                "native root from another process is rejected");

            var matchingInspection = new FocusedPasteTargetInspection
            {
                Editable = true,
                FocusRuntimeId = new[] { 5, 1 },
                AutomationHostKnown = true,
                AutomationHostMatchesForeground = true,
                AutomationHostWindow = foreground,
                RequiresSessionHostBinding = true
            };
            string failureReason;
            True(
                NativeInput.IsPasteTargetSnapshotMatch(
                    snapshot,
                    matchingInspection,
                    new GuiThreadFocusSnapshot(),
                    out failureReason),
                "same RuntimeId and exact UIA host match the frozen session");
            Equal(
                "paste_target_snapshot_match",
                failureReason,
                "matching snapshot diagnostic");

            matchingInspection.AutomationHostWindow =
                otherWindowSameProcess;
            True(
                !NativeInput.IsPasteTargetSnapshotMatch(
                    snapshot,
                    matchingInspection,
                    new GuiThreadFocusSnapshot(),
                    out failureReason),
                "a stale UIA focus from another same-process window is rejected");
            Equal(
                "paste_target_host_unconfirmed",
                failureReason,
                "same-process dual-window diagnostic");

            matchingInspection.AutomationHostWindow = foreground;
            matchingInspection.AutomationHostMatchesForeground = false;
            True(
                !NativeInput.IsPasteTargetSnapshotMatch(
                    snapshot,
                    matchingInspection,
                    new GuiThreadFocusSnapshot(),
                    out failureReason),
                "a UIA host whose root is not the foreground is rejected");
            Equal(
                "paste_target_host_changed",
                failureReason,
                "UIA root mismatch diagnostic");

            var nativeSnapshot = new PasteTargetSnapshot(52)
            {
                Frozen = true,
                AnchorConfirmed = true,
                ForegroundWindow = foreground,
                ForegroundProcessId = 500,
                NativeFocusTrusted = true,
                GuiFocus = new GuiThreadFocusSnapshot
                {
                    FocusWindow = new IntPtr(150)
                }
            };
            var nativeInspection = new FocusedPasteTargetInspection
            {
                Editable = true
            };
            True(
                NativeInput.IsPasteTargetSnapshotMatch(
                    nativeSnapshot,
                    nativeInspection,
                    new GuiThreadFocusSnapshot
                    {
                        FocusWindow = new IntPtr(150),
                        FocusOwnedByForeground = true
                    },
                    out failureReason),
                "trusted native child focus remains bound to the original root");
            True(
                !NativeInput.IsPasteTargetSnapshotMatch(
                    nativeSnapshot,
                    nativeInspection,
                    new GuiThreadFocusSnapshot
                    {
                        FocusWindow = new IntPtr(150),
                        FocusOwnedByForeground = false
                    },
                    out failureReason),
                "the same child HWND without original-root ownership is rejected");
            Equal(
                "paste_target_native_focus_changed",
                failureReason,
                "native root ownership diagnostic");
        }

        private static void AutomaticPasteReadOnlyConflictRequiresStrongEvidence()
        {
            var standardTypes = new[]
            {
                System.Windows.Automation.ControlType.Edit,
                System.Windows.Automation.ControlType.Document,
                System.Windows.Automation.ControlType.Window
            };
            foreach (var controlType in standardTypes)
            {
                True(
                    !NativeInput.IsEditableAutomationState(
                        new EditableAutomationState
                        {
                            Enabled = true,
                            EnabledKnown = true,
                            InFocusedChain = true,
                            SupportsValuePattern = true,
                            ValueReadOnly = true,
                            FocusedTargetCandidate =
                                NativeInput.IsFocusedPatternCandidate(
                                    controlType,
                                    true,
                                    false)
                        }),
                    controlType.ProgrammaticName +
                    " value-only read-only target must be rejected by default");
            }

            var nonstandardTypes = new[]
            {
                System.Windows.Automation.ControlType.Custom,
                System.Windows.Automation.ControlType.Pane,
                System.Windows.Automation.ControlType.Group
            };
            foreach (var controlType in nonstandardTypes)
            {
                True(
                    NativeInput.IsEditableAutomationState(
                        new EditableAutomationState
                        {
                            Enabled = true,
                            EnabledKnown = true,
                            InFocusedChain = true,
                            SupportsValuePattern = true,
                            ValueReadOnly = true,
                            FocusedTargetCandidate =
                                NativeInput.IsFocusedPatternCandidate(
                                    controlType,
                                    true,
                                    false),
                            AllowUnconfirmedValueReadOnly = true
                        }),
                    controlType.ProgrammaticName +
                    " may enter only the general strong-evidence stage");
            }
            True(
                !NativeInput.IsFocusedTextCandidateControl(
                    System.Windows.Automation.ControlType.Text) &&
                !NativeInput.IsFocusedPatternCandidate(
                    System.Windows.Automation.ControlType.Text,
                    false,
                    false),
                "static Text without a pattern is never an editable candidate");

            var foreground = new IntPtr(200);
            var snapshot = new PasteTargetSnapshot(61)
            {
                Frozen = true,
                AnchorConfirmed = true,
                ForegroundWindow = foreground,
                ForegroundProcessId = 700,
                FocusRuntimeId = new[] { 6, 1 }
            };
            var strongInspection = new FocusedPasteTargetInspection
            {
                Editable = true,
                Route = "value_readonly_strong_candidate",
                FocusRuntimeId = new[] { 6, 1 },
                AutomationHostKnown = true,
                AutomationHostMatchesForeground = true,
                AutomationHostWindow = foreground,
                RequiresSessionHostBinding = true
            };
            string failureReason;
            True(
                NativeInput.IsPasteTargetSnapshotMatch(
                    snapshot,
                    strongInspection,
                    new GuiThreadFocusSnapshot(),
                    out failureReason),
                "nonstandard read-only conflict needs matching start RuntimeId and exact host");
            snapshot.FocusRuntimeId = null;
            strongInspection.FocusRuntimeId = null;
            True(
                !NativeInput.IsPasteTargetSnapshotMatch(
                    snapshot,
                    strongInspection,
                    new GuiThreadFocusSnapshot(),
                    out failureReason),
                "type alone cannot authorize a no-pattern or value-read-only custom target");
            Equal(
                "strong_candidate_start_identity_unconfirmed",
                failureReason,
                "missing strong start identity diagnostic");

            True(
                NativeInput.ShouldIgnoreAncestorReadOnly(
                    strongInspection,
                    "target_text_read_only"),
                "a read-only ancestor must not override a strongly bound focused candidate");
            True(
                !NativeInput.ShouldIgnoreAncestorReadOnly(
                    strongInspection,
                    "target_value_read_only") &&
                !NativeInput.ShouldIgnoreAncestorReadOnly(
                    new FocusedPasteTargetInspection
                    {
                        Editable = true,
                        RequiresSessionHostBinding = false
                    },
                    "target_text_read_only"),
                "only the exact strong-candidate ancestor conflict may be ignored");
            True(
                !NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        SupportsValuePattern = true,
                        ValueReadOnly = false,
                        SupportsTextPattern = true,
                        TextReadOnlyKnown = true,
                        TextReadOnly = true,
                        ReadOnlyConfirmed = true,
                        FocusedTargetCandidate = true,
                        AllowUnconfirmedValueReadOnly = true
                    }),
                "focused TextPattern explicit read-only remains definitive");

            var source = File.ReadAllText(
                Path.Combine(FindProjectRoot(), "Program.cs"),
                Encoding.UTF8);
            True(
                source.IndexOf(
                    "Chrome_WidgetWin_",
                    StringComparison.Ordinal) < 0 &&
                source.IndexOf(
                    "ShouldOverrideChromiumReadOnly",
                    StringComparison.Ordinal) < 0,
                "strong evidence must stay framework-neutral and contain no application whitelist");
        }

        private static void AutomaticPasteAutomationWorkerIsSingleFlight()
        {
            using (var entered = new System.Threading.ManualResetEvent(false))
            using (var release = new System.Threading.ManualResetEvent(false))
            {
                System.Threading.Thread firstWorker = null;
                string firstReason;
                try
                {
                    True(
                        NativeInput.TryStartAutomationWorker(
                            delegate
                            {
                                entered.Set();
                                release.WaitOne();
                            },
                            "PasteAutomationSingleFlightTest",
                            out firstWorker,
                            out firstReason),
                        "the first UIA worker starts");
                    Equal(
                        "automation_worker_started",
                        firstReason,
                        "first UIA worker diagnostic");
                    True(
                        entered.WaitOne(2000),
                        "the first UIA worker entered its blocking provider call");
                    True(
                        NativeInput.AutomationWorkerActive,
                        "the single-flight gate remains owned while the worker is blocked");

                    System.Threading.Thread secondWorker;
                    string secondReason;
                    True(
                        !NativeInput.TryStartAutomationWorker(
                            delegate { },
                            "PasteAutomationRejectedSecondWorker",
                            out secondWorker,
                            out secondReason),
                        "a second UIA worker cannot accumulate behind a stuck provider");
                    Equal(
                        null,
                        secondWorker,
                        "rejected second worker handle");
                    Equal(
                        "automation_worker_busy",
                        secondReason,
                        "single-flight busy diagnostic");
                    System.Threading.Thread.Sleep(25);
                    True(
                        NativeInput.AutomationWorkerActive,
                        "a caller timeout must not release a gate still owned by the worker");
                }
                finally
                {
                    release.Set();
                    if (firstWorker != null)
                    {
                        firstWorker.Join(2000);
                    }
                }
                True(
                    !NativeInput.AutomationWorkerActive,
                    "the worker itself releases the gate from finally after it exits");
            }

            True(
                !NativeInput.IsAutomationRevalidationAcceptable(
                    true,
                    false,
                    false) &&
                !NativeInput.IsAutomationRevalidationAcceptable(
                    false,
                    true,
                    false) &&
                !NativeInput.IsAutomationRevalidationAcceptable(
                    false,
                    true,
                    true),
                "RuntimeId knowledge cannot appear only at delivery, and known starts require revalidation");
            True(
                NativeInput.IsAutomationRevalidationAcceptable(
                    false,
                    false,
                    false) &&
                NativeInput.IsAutomationRevalidationAcceptable(
                    true,
                    true,
                    true),
                "native-only fallback is limited to sessions with no known RuntimeId");
        }

        private static void AutomaticPasteModifierPolicyIsSafe()
        {
            var released = new short[8];
            True(
                !NativeInput.HasActiveModifierKeyStates(released),
                "all left/right Ctrl Alt Shift and Win keys released");
            for (var index = 0; index < released.Length; index++)
            {
                var onePressed = new short[8];
                onePressed[index] = unchecked((short)0x8000);
                True(
                    NativeInput.HasActiveModifierKeyStates(onePressed),
                    "physical modifier high bit at index " + index + " blocks injection");
            }
            var lowTransitionBitOnly = new short[8];
            lowTransitionBitOnly[0] = 1;
            True(
                !NativeInput.HasActiveModifierKeyStates(
                    lowTransitionBitOnly),
                "GetAsyncKeyState transition bit alone is not a currently pressed key");
            True(
                NativeInput.HasActiveModifierKeyStates(null) &&
                NativeInput.HasActiveModifierKeyStates(new short[7]) &&
                NativeInput.HasActiveModifierKeyStates(new short[9]),
                "missing or malformed modifier evidence fails closed");
            True(
                NativeInput.ModifierReleaseTimeoutMs >= 250 &&
                NativeInput.ModifierReleaseTimeoutMs <= 500,
                "modifier release wait remains short and bounded");

            var source = File.ReadAllText(
                Path.Combine(FindProjectRoot(), "Program.cs"),
                Encoding.UTF8);
            var modifierKeys = new[]
            {
                "VkLeftControl",
                "VkRightControl",
                "VkLeftAlt",
                "VkRightAlt",
                "VkLeftShift",
                "VkRightShift",
                "VkLeftWindows",
                "VkRightWindows"
            };
            foreach (var modifierKey in modifierKeys)
            {
                True(
                    source.IndexOf(
                        "GetAsyncKeyState(" + modifierKey + ")",
                        StringComparison.Ordinal) >= 0,
                    modifierKey + " must be checked before Ctrl+V injection");
            }
            True(
                source.IndexOf(
                    "modifier_keys_active",
                    StringComparison.Ordinal) >= 0,
                "modifier timeout keeps clipboard fallback and a diagnostic reason");
            Equal(
                1,
                CountOccurrences(
                    source,
                    "if (!WaitForModifierKeysReleased())"),
                "modifier keys receive exactly one bounded wait");
            True(
                source.IndexOf(
                    "modifier_keys_reactivated",
                    StringComparison.Ordinal) >= 0 &&
                source.IndexOf(
                    "HasActiveModifierKeyStates(\r\n                    ReadModifierKeyStates())",
                    StringComparison.Ordinal) >= 0,
                "modifiers are sampled again immediately before injection without a second wait");
            True(
                source.IndexOf(
                    "ReleaseSyntheticPasteKeys",
                    StringComparison.Ordinal) < 0,
                "the application must never synthesize releases for the user's physical keys");
        }

        private static void AutomaticPasteRejectsUnsafeTargetsAndStaleFocus()
        {
            var foreground = new IntPtr(100);
            True(
                NativeInput.IsNativeFocusFallbackEligible(
                    "focused_element_unavailable",
                    new GuiThreadFocusSnapshot
                    {
                        Available = true,
                        FocusWindow = new IntPtr(200),
                        FocusOwnedByForeground = true,
                        FocusIsChildWindow = true
                    },
                    foreground),
                "a stable focused child HWND may recover from unavailable UIA");
            True(
                NativeInput.IsNativeFocusFallbackEligible(
                    "automation_timeout",
                    new GuiThreadFocusSnapshot
                    {
                        Available = true,
                        FocusWindow = foreground,
                        CaretWindow = new IntPtr(201),
                        CaretOwnedByForeground = true
                    },
                    foreground),
                "a native caret may recover from a timed-out UIA provider");
            True(
                !NativeInput.IsNativeFocusFallbackEligible(
                    "focused_element_unavailable",
                    new GuiThreadFocusSnapshot
                    {
                        Available = true,
                        FocusWindow = foreground
                    },
                    foreground),
                "top-level-only focus without a caret is not text-input evidence");
            True(
                !NativeInput.IsNativeFocusFallbackEligible(
                    "focused_element_unavailable",
                    new GuiThreadFocusSnapshot
                    {
                        Available = true,
                        FocusWindow = new IntPtr(200)
                    },
                    foreground),
                "a nonzero native handle without foreground-root ownership is rejected");
            True(
                !NativeInput.IsNativeFocusFallbackEligible(
                    "non_text_focus_target",
                    new GuiThreadFocusSnapshot
                    {
                        Available = true,
                        FocusWindow = new IntPtr(200),
                        CaretWindow = new IntPtr(201),
                        FocusOwnedByForeground = true,
                        FocusIsChildWindow = true,
                        CaretOwnedByForeground = true
                    },
                    foreground),
                "a known non-text target must not use the UIA-unavailable fallback");

            True(
                NativeInput.RuntimeIdsEqual(
                    new[] { 42, 7, 99 },
                    new[] { 42, 7, 99 }),
                "identical UIA RuntimeId values must preserve focus identity");
            True(
                !NativeInput.RuntimeIdsEqual(
                    new[] { 42, 7, 99 },
                    new[] { 42, 7, 100 }) &&
                !NativeInput.RuntimeIdsEqual(
                    new[] { 42, 7 },
                    new[] { 42, 7, 99 }) &&
                !NativeInput.RuntimeIdsEqual(
                    null,
                    new[] { 42, 7, 99 }),
                "changed, resized, or unavailable RuntimeId values must not match");
            True(
                !NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = false,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        FocusedTargetCandidate = true
                    }),
                "an explicitly disabled target must be rejected");
            True(
                !NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        Password = true,
                        FocusedTargetCandidate = true
                    }),
                "a password target must be rejected");
            True(
                !NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        Offscreen = true,
                        FocusedTargetCandidate = true
                    }),
                "an off-screen target must be rejected");
            True(
                !NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        SupportsTextPattern = true,
                        TextReadOnlyKnown = true,
                        TextReadOnly = true,
                        ReadOnlyConfirmed = true,
                        FocusedTargetCandidate = true
                    }),
                "a reliably confirmed read-only text target must be rejected");
            True(
                !NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = false,
                        FocusedTargetCandidate = true
                    }),
                "an element outside the focused chain must be rejected");
            True(
                !NativeInput.IsEditableAutomationState(
                    new EditableAutomationState
                    {
                        Enabled = true,
                        EnabledKnown = true,
                        InFocusedChain = true,
                        FocusedTargetCandidate = false
                    }),
                "Button/Menu/List-style unknown focus must not pass the generic fallback");

            True(
                NativeInput.IsStablePasteFocus(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = true,
                        ForegroundProcessMatches = true,
                        AutomationFocusKnown = true,
                        AutomationFocusMatches = true
                    }),
                "a matching UIA RuntimeId is stable focus evidence");
            True(
                NativeInput.IsStablePasteFocus(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = true,
                        ForegroundProcessMatches = true,
                        NativeFocusKnown = true,
                        NativeFocusMatches = true
                    }),
                "matching GetGUIThreadInfo focus is a framework-neutral fallback");
            True(
                NativeInput.IsStablePasteFocus(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = true,
                        ForegroundProcessMatches = true,
                        NativeCaretKnown = true,
                        NativeCaretMatches = true
                    }),
                "matching native caret is stable focus evidence");
            True(
                !NativeInput.IsStablePasteFocus(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = false,
                        ForegroundProcessMatches = true,
                        AutomationFocusKnown = true,
                        AutomationFocusMatches = true
                    }),
                "a changed foreground HWND must cancel paste");
            True(
                !NativeInput.IsStablePasteFocus(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = true,
                        ForegroundProcessMatches = false,
                        NativeFocusKnown = true,
                        NativeFocusMatches = true
                    }),
                "a changed foreground process must cancel paste");
            True(
                !NativeInput.IsStablePasteFocus(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = true,
                        ForegroundProcessMatches = true,
                        AutomationFocusKnown = true,
                        AutomationFocusMatches = false,
                        NativeFocusKnown = true,
                        NativeFocusMatches = true
                    }),
                "a stale RuntimeId must not be overridden by another matching signal");
            True(
                !NativeInput.IsStablePasteFocus(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = true,
                        ForegroundProcessMatches = true,
                        NativeFocusKnown = true,
                        NativeFocusMatches = false
                    }),
                "a changed native focus HWND must cancel paste");
            True(
                !NativeInput.IsStablePasteFocus(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = true,
                        ForegroundProcessMatches = true,
                        NativeCaretKnown = true,
                        NativeCaretMatches = false
                    }),
                "a changed native caret must cancel paste");
            True(
                !NativeInput.IsStablePasteFocus(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = true,
                        ForegroundProcessMatches = true
                    }),
                "no focus evidence must fail closed");

            Equal(
                "foreground_changed",
                NativeInput.FocusRevalidationFailureReason(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = false,
                        ForegroundProcessMatches = true
                    }),
                "foreground diagnostic reason");
            Equal(
                "focus_changed_automation",
                NativeInput.FocusRevalidationFailureReason(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = true,
                        ForegroundProcessMatches = true,
                        AutomationFocusKnown = true,
                        AutomationFocusMatches = false
                    }),
                "RuntimeId change diagnostic reason");
            Equal(
                "focus_changed_native",
                NativeInput.FocusRevalidationFailureReason(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = true,
                        ForegroundProcessMatches = true,
                        NativeFocusKnown = true,
                        NativeFocusMatches = false
                    }),
                "native focus change diagnostic reason");
            Equal(
                "focus_revalidation_unavailable",
                NativeInput.FocusRevalidationFailureReason(
                    new PasteFocusEvidenceState
                    {
                        ForegroundWindowMatches = true,
                        ForegroundProcessMatches = true
                    }),
                "missing focus evidence diagnostic reason");
            Equal(
                "uia_focus_caret",
                NativeInput.StableFocusRoute(
                    new PasteFocusEvidenceState
                    {
                        AutomationFocusKnown = true,
                        NativeFocusKnown = true,
                        NativeCaretKnown = true
                    }),
                "sanitized stable-focus evidence route");
        }

        private static void AutomaticPasteInjectionContract()
        {
            Equal(
                3,
                NativeInput.EditableTargetRetryCount,
                "focused target retry count");
            True(
                NativeInput.EditableTargetTimeoutMs >= 800,
                "focused target retry timeout");
            Equal(
                4,
                NativeInput.PasteInputEventCount,
                "one Ctrl+V SendInput batch contains exactly four events");
            True(
                NativeInput.SendInputCountSucceeded(4),
                "the complete injected batch must be reported as injected");
            True(
                !NativeInput.SendInputCountSucceeded(0) &&
                !NativeInput.SendInputCountSucceeded(1) &&
                !NativeInput.SendInputCountSucceeded(3) &&
                !NativeInput.SendInputCountSucceeded(5),
                "zero, partial, or unexpected SendInput counts must fail closed");
            Equal(
                IntPtr.Size == 8 ? 40 : 28,
                NativeInput.NativeInputEventSize,
                "native INPUT structure size");

            var source = File.ReadAllText(
                Path.Combine(FindProjectRoot(), "Program.cs"),
                Encoding.UTF8);
            True(
                source.IndexOf(
                    "TreeWalker.RawViewWalker",
                    StringComparison.Ordinal) >= 0,
                "deep custom controls must be inspected through RawView");
            True(
                source.IndexOf(
                    "private static extern uint SendInput",
                    StringComparison.Ordinal) >= 0,
                "verified SendInput injection must be used");
            Equal(
                1,
                CountOccurrences(
                    source,
                    "var inserted = SendInput("),
                "automatic paste must inject only one Ctrl+V batch");
            True(
                source.IndexOf(
                    "keybd_event",
                    StringComparison.Ordinal) < 0,
                "unverifiable keybd_event injection must remain retired");
            True(
                source.IndexOf(
                    "send_input_rejected_",
                    StringComparison.Ordinal) >= 0 &&
                source.IndexOf(
                    "_error_",
                    StringComparison.Ordinal) >= 0,
                "failed injection diagnostics must retain event count and Win32 error");
            True(
                source.IndexOf(
                    "paste_input_injected",
                    StringComparison.Ordinal) >= 0 &&
                source.IndexOf(
                    "lastDeliveryOutcome = \"paste_success\"",
                    StringComparison.Ordinal) < 0,
                "inserting events into the system stream must not claim target consumption");
            True(
                source.IndexOf(
                    "automatic paste result=",
                    StringComparison.Ordinal) >= 0,
                "sanitized diagnostics must retain the paste route/reason");
            True(
                source.IndexOf(
                    "Chrome_WidgetWin_",
                    StringComparison.Ordinal) < 0 &&
                source.IndexOf(
                    "ShouldOverrideChromiumReadOnly",
                    StringComparison.Ordinal) < 0,
                "production paste eligibility must not depend on a Chromium class whitelist");
        }

        private static void ScheduleStoreSortsAndDeduplicates()
        {
            var store = ScheduleStore.CreateInMemory();
            ScheduleItem item;
            bool duplicate;
            string error;
            True(
                store.Add("Later", "2026-08-02", "gemini:call-later", out item, out duplicate, out error),
                "later schedule add");
            True(
                store.Add("Memo", "", "gemini:call-memo", out item, out duplicate, out error),
                "memo add");
            True(
                store.Add("Earlier", "2026-08-01", "gemini:call-earlier", out item, out duplicate, out error),
                "earlier schedule add");
            True(
                store.Add("Earlier second", "2026-08-01", "gemini:call-earlier-second", out item, out duplicate, out error),
                "same-day second schedule add");
            True(
                store.Add("Earlier", "2026-08-01", "gemini:call-earlier", out item, out duplicate, out error),
                "duplicate call must be idempotent");
            True(duplicate, "duplicate call flag");
            var snapshot = store.Snapshot();
            Equal(4, snapshot.Length, "duplicate must not write a fifth item");
            Equal("Earlier", snapshot[0].content, "date ascending");
            Equal("Earlier second", snapshot[1].content, "same date creation order");
            Equal("Later", snapshot[2].content, "later date third");
            Equal("Memo", snapshot[3].content, "memo last");
            True(
                !store.Add("Bad date", "tomorrow-ish", "openai:bad", out item, out duplicate, out error),
                "ambiguous date must not persist");
            Equal("date_requires_clarification", error, "ambiguous date error");
            var memos = store.Read("memos", "", out error);
            Equal(1, memos.Length, "memo scope");
            Equal("Memo", memos[0].content, "memo content");

            var toolStore = ScheduleStore.CreateInMemory();
            var executor = new ScheduleToolExecutor("gemini", toolStore, null);
            var addArguments = new Dictionary<string, object>
            {
                { "content", "Idempotent item" },
                { "date", "2026-08-03" }
            };
            var first = executor.Execute("add_schedule", "call-1", addArguments);
            Equal(true, Convert.ToBoolean(first["ok"]), "first tool write");
            var sameCall = executor.Execute("add_schedule", "call-1", addArguments);
            Equal(
                "duplicate_call_ignored",
                Convert.ToString(sameCall["status"]),
                "same provider write call ID must be idempotent");
            var sameTurn = executor.Execute("add_schedule", "call-2", addArguments);
            Equal(
                "duplicate_turn_write_ignored",
                Convert.ToString(sameTurn["status"]),
                "same-turn arguments must be idempotent");
            executor.CompleteAssistantTurn();
            var reconnect = executor.Execute("add_schedule", "call-3", addArguments);
            Equal(
                "already_saved",
                Convert.ToString(reconnect["status"]),
                "reconnect write must be idempotent");
            Equal(1, toolStore.Snapshot().Length, "idempotent tool writes must persist once");
            var read = executor.Execute(
                "read_schedule",
                "read-1",
                new Dictionary<string, object> { { "scope", "all" } });
            var safeItems = (Dictionary<string, object>[])read["items"];
            Equal(1, safeItems.Length, "safe tool read count");
            True(
                safeItems[0].ContainsKey("content") &&
                safeItems[0].ContainsKey("date") &&
                !safeItems[0].ContainsKey("id") &&
                !safeItems[0].ContainsKey("sourceCallId") &&
                !safeItems[0].ContainsKey("createdAt") &&
                !safeItems[0].ContainsKey("order"),
                "tool read must expose only date and content");
            var repeatedRead = executor.Execute(
                "read_schedule",
                "read-1",
                new Dictionary<string, object> { { "scope", "all" } });
            Equal(
                "items_found",
                Convert.ToString(repeatedRead["status"]),
                "read retries must remain repeatable because they do not write");
            var retryStore = ScheduleStore.CreateInMemory();
            var retryExecutor = new ScheduleToolExecutor("openai", retryStore, null);
            var rejectedWrite = retryExecutor.Execute(
                "add_schedule",
                "retry-call",
                new Dictionary<string, object>
                {
                    { "content", "Retry after clarification" },
                    { "date", "tomorrow-ish" }
                });
            Equal(false, Convert.ToBoolean(rejectedWrite["ok"]),
                "failed writes must not report success");
            var correctedWrite = retryExecutor.Execute(
                "add_schedule",
                "retry-call",
                new Dictionary<string, object>
                {
                    { "content", "Retry after clarification" },
                    { "date", "2026-08-04" }
                });
            Equal(true, Convert.ToBoolean(correctedWrite["ok"]),
                "failed call IDs must remain retryable after clarification");
            Equal(1, retryStore.Snapshot().Length,
                "corrected retry must persist exactly once");

            var directory = Path.Combine(
                Path.GetTempPath(),
                "VoiceTypingAI-schedule-corruption-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var damaged = Path.Combine(directory, "schedules.json");
                File.WriteAllText(
                    damaged,
                    "{\"version\":1,\"items\":[{\"id\":\"\",\"content\":\"bad\"}]}",
                    new UTF8Encoding(false));
                var validate = typeof(ScheduleStore).GetMethod(
                    "ReadAndValidate",
                    BindingFlags.Static | BindingFlags.NonPublic);
                var preserve = typeof(ScheduleStore).GetMethod(
                    "PreserveCorruptCopy",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (validate == null || preserve == null)
                {
                    throw new InvalidOperationException("schedule recovery hooks not found");
                }
                var rejected = false;
                try
                {
                    validate.Invoke(null, new object[] { damaged });
                }
                catch (TargetInvocationException ex)
                {
                    rejected = ex.InnerException is InvalidDataException;
                }
                True(rejected, "damaged schedule data must be rejected");
                var recovery = Convert.ToString(
                    preserve.Invoke(null, new object[] { damaged }));
                True(
                    !string.IsNullOrWhiteSpace(recovery) && File.Exists(recovery),
                    "damaged schedule data must retain a recovery copy");
            }
            finally
            {
                try { Directory.Delete(directory, true); } catch { }
            }
        }

        private static void RealtimeToolProtocolsCoverBothProviders()
        {
            var geminiSetup = (Dictionary<string, object>)
                LiveConversationProtocol.GeminiSetup()["setup"];
            var geminiTools = (object[])geminiSetup["tools"];
            Equal(1, geminiTools.Length, "Gemini tool wrapper count");
            var declarationGroup = (Dictionary<string, object>)geminiTools[0];
            var declarations = (object[])declarationGroup["functionDeclarations"];
            Equal(3, declarations.Length, "Gemini local tool count");
            foreach (var declarationValue in declarations)
            {
                var declaration =
                    (Dictionary<string, object>)declarationValue;
                var parameters =
                    (Dictionary<string, object>)declaration["parameters"];
                True(
                    !parameters.ContainsKey("additionalProperties"),
                    "Gemini Live function schemas must use its supported parameter subset");
            }
            var geminiSetupJson =
                new System.Web.Script.Serialization.JavaScriptSerializer().
                    Serialize(geminiSetup);
            True(
                geminiSetupJson.IndexOf(
                    "additionalProperties",
                    StringComparison.Ordinal) < 0,
                "Gemini Live setup JSON must not send the rejected additionalProperties field");
            var searchedGeminiSetupJson =
                new System.Web.Script.Serialization.JavaScriptSerializer().
                    Serialize(
                        LiveConversationProtocol.GeminiSetup(
                            false,
                            true));
            True(
                searchedGeminiSetupJson.IndexOf(
                    "additionalProperties",
                    StringComparison.Ordinal) < 0,
                "Gemini Live search setup must keep the same compatible function schema");

            var openAiSession = (Dictionary<string, object>)
                LiveConversationProtocol.OpenAiSessionUpdate()["session"];
            var openAiTools = (object[])openAiSession["tools"];
            Equal(3, openAiTools.Length, "OpenAI local tool count");
            foreach (var toolValue in openAiTools)
            {
                var tool = (Dictionary<string, object>)toolValue;
                Equal("function", Convert.ToString(tool["type"]), "OpenAI tool type");
                var parameters =
                    (Dictionary<string, object>)tool["parameters"];
                Equal(
                    false,
                    Convert.ToBoolean(
                        parameters["additionalProperties"]),
                    "OpenAI function schemas keep strict JSON objects");
            }

            var geminiResponses = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { { "id", "a" }, { "name", "add_schedule" }, { "response", new Dictionary<string, object>() } },
                new Dictionary<string, object> { { "id", "b" }, { "name", "read_schedule" }, { "response", new Dictionary<string, object>() } }
            };
            var toolResponse = (Dictionary<string, object>)
                LiveConversationProtocol.GeminiToolResponse(geminiResponses)["toolResponse"];
            Equal(2, ((Dictionary<string, object>[])toolResponse["functionResponses"]).Length, "Gemini multi-call response count");
            var openAiOutput = LiveConversationProtocol.OpenAiFunctionOutput("call-1", "{}");
            Equal("conversation.item.create", Convert.ToString(openAiOutput["type"]), "OpenAI function output event");
            Equal(
                "response.create",
                Convert.ToString(LiveConversationProtocol.OpenAiResponseCreate()["type"]),
                "OpenAI follow-up response event");
            var continuation = (Dictionary<string, object>)
                LiveConversationProtocol.OpenAiResponseCreate()["response"];
            Equal(
                "none",
                Convert.ToString(continuation["tool_choice"]),
                "OpenAI tool continuation must not recursively call tools");
        }

        private static void RealtimeSilenceAndSpeakerGateMatchContract()
        {
            True(
                SilenceAutoStopMonitor.ShouldStop(true, 6000, 7000, 6000, 6000),
                "realtime silence monitor must stop after 6 seconds of quiet");
            True(
                !SilenceAutoStopMonitor.ShouldStop(true, 5999, 9000, 6000, 6000),
                "conversation must not stop before 6 seconds of quiet");
            Equal(
                2000,
                SilenceAutoStopMonitor.DefaultPcm16SpeechPeakThreshold,
                "Windows realtime silence must use raw PCM16 peak scale");

            bool gated;
            var raw = new byte[] { 1, 2, 3, 4 };
            var speakerBusy = ConversationInputGate.Apply(raw, false, true, out gated);
            True(gated, "speaker input must be gated while the assistant is busy");
            Equal(raw.Length, speakerBusy.Length, "speaker gate must preserve PCM duration");
            Equal(0, speakerBusy[0] + speakerBusy[1] + speakerBusy[2] + speakerBusy[3],
                "speaker gate must upload equal-length silence");
            Equal(1, (int)raw[0], "speaker gate must not modify raw diagnostics PCM");
            var headsetBusy = ConversationInputGate.Apply(raw, true, true, out gated);
            True(!gated && object.ReferenceEquals(raw, headsetBusy),
                "confirmed headset routes must retain full-duplex microphone PCM");

            using (var fired = new System.Threading.ManualResetEvent(false))
            using (var monitor = new SilenceAutoStopMonitor(250, 250, delegate { fired.Set(); }))
            {
                monitor.SetBusy(true);
                True(!fired.WaitOne(350), "assistant busy time must not count toward auto-stop");
                monitor.SetBusy(false);
                True(!fired.WaitOne(150), "auto-stop must restart after busy playback");
                True(fired.WaitOne(600), "auto-stop must resume after the post-answer quiet period");
            }
        }

        private static void RealtimeConversationProtocolSupportsBothProviders()
        {
            Equal("gemini-3.1-flash-live-preview", LiveConversationProtocol.GeminiModel, "Gemini conversation model");
            Equal("Kore", LiveConversationProtocol.GeminiVoice, "Gemini conversation voice");
            Equal("gpt-realtime-1.5", LiveConversationProtocol.OpenAiModel, "OpenAI conversation model");
            Equal("marin", LiveConversationProtocol.OpenAiVoice, "OpenAI conversation voice");
            Equal(1200, LiveConversationProtocol.ConversationVadSilenceMs, "conversation turn silence");

            var geminiRoot = LiveConversationProtocol.GeminiSetup();
            var geminiSetup = (Dictionary<string, object>)geminiRoot["setup"];
            Equal(
                "models/" + LiveConversationProtocol.GeminiModel,
                Convert.ToString(geminiSetup["model"]),
                "Gemini conversation setup model");
            var generation = (Dictionary<string, object>)geminiSetup["generationConfig"];
            var speech = (Dictionary<string, object>)generation["speechConfig"];
            var voice = (Dictionary<string, object>)speech["voiceConfig"];
            var prebuilt = (Dictionary<string, object>)voice["prebuiltVoiceConfig"];
            Equal("Kore", Convert.ToString(prebuilt["voiceName"]), "Gemini conversation setup voice");
            var geminiRealtime = (Dictionary<string, object>)geminiSetup["realtimeInputConfig"];
            var automatic = (Dictionary<string, object>)geminiRealtime["automaticActivityDetection"];
            Equal(1200, Convert.ToInt32(automatic["silenceDurationMs"]), "Gemini conversation VAD silence");
            Equal(
                "START_OF_ACTIVITY_INTERRUPTS",
                Convert.ToString(geminiRealtime["activityHandling"]),
                "Gemini conversation interruption policy");

            var openAiRoot = LiveConversationProtocol.OpenAiSessionUpdate();
            var openAiSession = (Dictionary<string, object>)openAiRoot["session"];
            Equal(LiveConversationProtocol.OpenAiModel, Convert.ToString(openAiSession["model"]), "OpenAI conversation setup model");
            var openAiAudio = (Dictionary<string, object>)openAiSession["audio"];
            var openAiInput = (Dictionary<string, object>)openAiAudio["input"];
            var turnDetection = (Dictionary<string, object>)openAiInput["turn_detection"];
            Equal("server_vad", Convert.ToString(turnDetection["type"]), "OpenAI conversation VAD");
            Equal(1200, Convert.ToInt32(turnDetection["silence_duration_ms"]), "OpenAI conversation VAD silence");
            Equal(true, Convert.ToBoolean(turnDetection["interrupt_response"]), "OpenAI conversation interruption");
            var openAiOutput = (Dictionary<string, object>)openAiAudio["output"];
            Equal("marin", Convert.ToString(openAiOutput["voice"]), "OpenAI conversation setup voice");
            var openAiOutputFormat = (Dictionary<string, object>)openAiOutput["format"];
            Equal(
                LiveConversationProtocol.OutputSampleRate,
                Convert.ToInt32(openAiOutputFormat["rate"]),
                "OpenAI conversation output PCM rate");

            var geminiEnd = LiveConversationProtocol.GeminiAudioStreamEnd();
            var geminiEndInput = (Dictionary<string, object>)geminiEnd["realtimeInput"];
            Equal(true, Convert.ToBoolean(geminiEndInput["audioStreamEnd"]), "Gemini explicit audio stream end");
            var openAiCommit = LiveConversationProtocol.OpenAiInputCommit();
            Equal(
                "input_audio_buffer.commit",
                Convert.ToString(openAiCommit["type"]),
                "OpenAI explicit input commit");
        }

        private static void RealtimeHistoryHasThreeWindowsCapsFiftyAndContextCharacters()
        {
            var store = RealtimeConversationStore.CreateInMemory();
            Equal(
                3,
                RealtimeConversationStore.MaximumWindows,
                "realtime window count");
            Equal(
                50,
                RealtimeConversationStore.MaximumTurnsPerWindow,
                "per-window turn cap");
            Equal(
                20000,
                RealtimeConversationBinding.MaximumHistoryCharacters,
                "per-session text context character cap");

            for (var window = 0;
                 window < RealtimeConversationStore.MaximumWindows;
                 window++)
            {
                store.SetActiveWindow(window);
                var sessionId = "window-" + window;
                var binding = store.FreezeActiveWindow(sessionId);
                Equal(window, binding.Window, "frozen window index");
                for (var turn = 1; turn <= 55; turn++)
                {
                    True(
                        store.CommitExchange(
                            binding,
                            new RealtimeConversationExchange(
                                sessionId,
                                "turn-" + turn,
                                turn,
                                turn,
                                "question-" + window + "-" + turn,
                                "answer-" + window + "-" + turn)),
                        "complete realtime pair must commit");
                }
                True(store.EndSession(binding), "session must end");
                var snapshot = store.SnapshotWindow(window);
                Equal(50, snapshot.Length, "window history cap");
                Equal(
                    "question-" + window + "-6",
                    snapshot[0].question,
                    "oldest retained turn");
                Equal(
                    "answer-" + window + "-55",
                    snapshot[49].answer,
                    "newest retained turn");
            }

            var longHistory = new List<RealtimeConversationTurn>();
            for (var turn = 1; turn <= 30; turn++)
            {
                longHistory.Add(
                    new RealtimeConversationTurn(
                        new string('q', 600) + turn,
                        new string('a', 600) + turn,
                        "",
                        "long-" + turn));
            }
            var selected = RealtimeConversationStore.SelectContext(
                longHistory.ToArray());
            var selectedCharacters = 0;
            foreach (var turn in selected)
            {
                selectedCharacters += turn.question.Length +
                                      turn.answer.Length;
            }
            True(
                selectedCharacters <=
                RealtimeConversationBinding.MaximumHistoryCharacters,
                "selected history must stay within 20000 characters");
            Equal(
                "long-30",
                selected[selected.Length - 1].deliveryId,
                "character cap must retain the newest complete turn");
        }

        private static void RealtimeTranslationProtocolSurfaceIsAbsent()
        {
            var root = FindProjectRoot();
            var protocolSource = File.ReadAllText(
                Path.Combine(root, "WakeAndConversation.cs"),
                Encoding.UTF8);
            True(
                protocolSource.IndexOf(
                    "RealtimeExperienceMode",
                    StringComparison.Ordinal) < 0 &&
                protocolSource.IndexOf(
                    "targetLanguage",
                    StringComparison.Ordinal) < 0 &&
                protocolSource.IndexOf(
                    "Realtime translation",
                    StringComparison.OrdinalIgnoreCase) < 0,
                "Gemini and OpenAI realtime protocols must be chat-only");
        }

        private static void GeminiRealtimeSearchIsChatOnlyAndFailClosed()
        {
            var plain = (Dictionary<string, object>)
                LiveConversationProtocol.GeminiSetup(
                    false,
                    false)["setup"];
            var plainTools = (object[])plain["tools"];
            Equal(
                1,
                plainTools.Length,
                "search-off Gemini chat has local tools only");

            var searched = (Dictionary<string, object>)
                LiveConversationProtocol.GeminiSetup(
                    false,
                    true)["setup"];
            var searchedTools = (object[])searched["tools"];
            Equal(
                2,
                searchedTools.Length,
                "search-on Gemini chat adds exactly one search tool");
            True(
                ((Dictionary<string, object>)searchedTools[1]).
                    ContainsKey("googleSearch"),
                "Gemini chat search tool shape");

            var settings = new AppSettings();
            True(
                !settings.GeminiRealtimeSearchEnabled,
                "Gemini realtime search must default off");
            var openAi = (Dictionary<string, object>)
                LiveConversationProtocol.OpenAiSessionUpdate()["session"];
            var openAiText =
                new System.Web.Script.Serialization.
                    JavaScriptSerializer().Serialize(openAi);
            True(
                openAiText.IndexOf(
                    "googleSearch",
                    StringComparison.OrdinalIgnoreCase) < 0 &&
                openAiText.IndexOf(
                    "web_search",
                    StringComparison.OrdinalIgnoreCase) < 0,
                "OpenAI realtime must never receive a search tool");

            var metadata = new Dictionary<string, object>
            {
                {
                    "searchEntryPoint",
                    new Dictionary<string, object>
                    {
                        {
                            "renderedContent",
                            "<div>Google Search Suggestions</div>"
                        }
                    }
                },
                {
                    "groundingChunks",
                    new object[]
                    {
                        new Dictionary<string, object>
                        {
                            {
                                "web",
                                new Dictionary<string, object>
                                {
                                    { "title", "Official source" },
                                    { "uri", "https://example.com/source" }
                                }
                            }
                        }
                    }
                }
            };
            var attribution =
                LiveConversationSession.ParseGroundingAttribution(
                    metadata);
            True(
                attribution != null && attribution.IsComplete,
                "complete grounding attribution parses");

            var gate = new GeminiSearchTurnGate(true);
            gate.ObserveInput("current question");
            gate.ObserveOutput("grounded answer");
            gate.AppendAudio(new byte[] { 1, 2, 3, 4 });
            gate.ObserveAttribution(attribution);
            var presented = false;
            var release = gate.Complete(
                delegate(GeminiGroundingAttribution value)
                {
                    presented = value == attribution;
                    return true;
                });
            True(
                release.Accepted &&
                presented &&
                !release.Persist &&
                release.Audio.Length == 4,
                "grounded answer releases only after attribution and remains transient");

            var failedPresentation = new GeminiSearchTurnGate(true);
            failedPresentation.ObserveInput("question");
            failedPresentation.ObserveOutput("answer");
            failedPresentation.ObserveAttribution(attribution);
            var rejected = failedPresentation.Complete(
                delegate { return false; });
            True(
                !rejected.Accepted &&
                rejected.Failure ==
                "grounding_attribution_presentation_failed",
                "attribution presentation failure must fail closed");

            var incomplete = new GeminiSearchTurnGate(true);
            incomplete.ObserveInput("question");
            incomplete.ObserveOutput("answer");
            incomplete.ObserveAttribution(
                new GeminiGroundingAttribution
                {
                    SearchSuggestionsHtml = "<div>Search</div>"
                });
            var incompleteRelease = incomplete.Complete(
                delegate { return true; });
            True(
                !incompleteRelease.Accepted &&
                incompleteRelease.Failure ==
                "grounding_attribution_incomplete",
                "incomplete attribution must fail closed");

            var ordinary = new GeminiSearchTurnGate(true);
            ordinary.ObserveInput("ordinary question");
            ordinary.ObserveOutput("ordinary answer");
            var ordinaryRelease = ordinary.Complete(null);
            True(
                ordinaryRelease.Accepted &&
                ordinaryRelease.Persist,
                "non-grounded Gemini chat remains normal persisted history");
        }

        private static void RealtimeRevisionGraceIsOneSecond()
        {
            Equal(
                1000,
                LiveConversationProtocol.TranscriptRevisionGraceMs,
                "bounded transcript revision grace");
            True(
                LiveConversationProtocol.TranscriptRevisionGraceMs <
                LiveConversationProtocol.ConversationVadSilenceMs,
                "revision grace is a bounded delivery deadline, not an unlimited wait");
        }

        private static void RealtimeHistoryBindingFreezesThirdWindow()
        {
            var store = RealtimeConversationStore.CreateInMemory();
            store.SetActiveWindow(2);
            var seed = store.FreezeActiveWindow("seed-third");
            True(
                store.CommitExchange(
                    seed,
                    new RealtimeConversationExchange(
                        "seed-third",
                        "seed-turn",
                        1,
                        1,
                        "third-window question",
                        "third-window answer")),
                "third window seed");
            True(store.EndSession(seed), "seed session end");

            store.SetActiveWindow(2);
            var binding =
                store.FreezeActiveWindow("frozen-third");
            store.SetActiveWindow(0);
            Equal(0, store.ActiveWindow, "UI may switch windows");
            True(
                store.CommitExchange(
                    binding,
                    new RealtimeConversationExchange(
                        "frozen-third",
                        "live-turn",
                        1,
                        1,
                        "live question",
                        "live answer")),
                "completed exchange writes to frozen window");
            Equal(
                0,
                store.SnapshotWindow(0).Length,
                "switched-to window remains untouched");
            var third = store.SnapshotWindow(2);
            Equal(2, third.Length, "frozen third window receives pair");
            Equal("live question", third[1].question, "frozen question");
            Equal("live answer", third[1].answer, "frozen answer");
            Equal(
                1,
                binding.History.Length,
                "initial history is an immutable start snapshot");
        }

        private static void RealtimeHistoryProtocolsSeedBothProviders()
        {
            var history = new[]
            {
                new RealtimeConversationTurn(
                    "question one",
                    "answer one",
                    "",
                    "history-one"),
                new RealtimeConversationTurn(
                    "question two",
                    "answer two",
                    "",
                    "history-two")
            };
            var geminiSetup = (Dictionary<string, object>)
                LiveConversationProtocol.GeminiSetup(true)["setup"];
            var historyConfig = (Dictionary<string, object>)geminiSetup["historyConfig"];
            Equal(
                true,
                Convert.ToBoolean(historyConfig["initialHistoryInClientContent"]),
                "Gemini initial history mode");
            True(geminiSetup.ContainsKey("inputAudioTranscription"),
                "Gemini input transcription required for writeback");
            True(geminiSetup.ContainsKey("outputAudioTranscription"),
                "Gemini output transcription required for writeback");
            var geminiContent = (Dictionary<string, object>)
                LiveConversationProtocol.GeminiInitialHistory(history)["clientContent"];
            var geminiTurns = (object[])geminiContent["turns"];
            Equal(4, geminiTurns.Length, "Gemini text history item count");
            Equal(
                "user",
                Convert.ToString(((Dictionary<string, object>)geminiTurns[0])["role"]),
                "Gemini oldest user turn first");
            Equal(
                "model",
                Convert.ToString(((Dictionary<string, object>)geminiTurns[3])["role"]),
                "Gemini newest assistant turn last");
            Equal(true, Convert.ToBoolean(geminiContent["turnComplete"]),
                "Gemini history seeding must not trigger a model turn");

            var openAiHistory = LiveConversationProtocol.OpenAiInitialHistory(
                "session-289",
                history);
            Equal(4, openAiHistory.Count, "OpenAI text history item count");
            var firstItem = (Dictionary<string, object>)openAiHistory[0]["item"];
            var firstContent = (Dictionary<string, object>)
                ((object[])firstItem["content"])[0];
            Equal("user", Convert.ToString(firstItem["role"]), "OpenAI oldest user role");
            Equal("input_text", Convert.ToString(firstContent["type"]),
                "OpenAI user history content type");
            var assistantItem = (Dictionary<string, object>)openAiHistory[1]["item"];
            var assistantContent = (Dictionary<string, object>)
                ((object[])assistantItem["content"])[0];
            Equal("assistant", Convert.ToString(assistantItem["role"]),
                "OpenAI assistant history role");
            Equal("text", Convert.ToString(assistantContent["type"]),
                "OpenAI client-created assistant history uses the current text type");
            True(
                LiveConversationProtocol.IsOpenAiHistoryItemId(
                    Convert.ToString(assistantItem["id"])),
                "OpenAI history item IDs must be distinguishable from generated output");

            var openAiSession = (Dictionary<string, object>)
                LiveConversationProtocol.OpenAiSessionUpdate()["session"];
            var audio = (Dictionary<string, object>)openAiSession["audio"];
            var input = (Dictionary<string, object>)audio["input"];
            var transcription = (Dictionary<string, object>)input["transcription"];
            Equal("gpt-4o-mini-transcribe", Convert.ToString(transcription["model"]),
                "OpenAI realtime input transcription model");
        }

        private static void GeminiRealtimeDraftsAreRevisionSafe()
        {
            var completed = new List<RealtimeConversationExchange>();
            var drafts =
                new List<RealtimeConversationDraftUpdate>();
            var tracker = new GeminiConversationTurnTracker(
                "gemini-session",
                completed.Add,
                drafts.Add);

            tracker.ObserveInputTranscription("late question");
            tracker.ObserveOutputTranscription("late answer");
            True(drafts.Count >= 2, "both transcript drafts published");
            for (var index = 1; index < drafts.Count; index++)
            {
                True(
                    drafts[index].Revision >
                    drafts[index - 1].Revision,
                    "Gemini draft revisions must be monotonic");
            }
            tracker.MarkTurnComplete();
            Equal(0, completed.Count, "turnComplete alone must not save a partial pair");
            tracker.MarkPlaybackCompleted();
            Equal(1, completed.Count, "late output transcript completes the atomic pair");
            Equal("late question", completed[0].Question, "Gemini paired question");
            Equal("late answer", completed[0].Answer, "Gemini paired answer");
            var firstSequence = completed[0].TurnSequence;

            tracker.ObserveInputTranscription("late question");
            tracker.ObserveOutputTranscription("late answer");
            tracker.MarkInterrupted();
            tracker.MarkTurnComplete();
            tracker.ObserveOutputTranscription("late answer");
            tracker.MarkPlaybackCompleted();
            Equal(1, completed.Count, "interrupted and late output must be discarded");

            tracker.ObserveInputTranscription("late question");
            tracker.ObserveOutputTranscription("late answer");
            tracker.MarkTurnComplete();
            tracker.MarkPlaybackCompleted();
            Equal(2, completed.Count,
                "identical text in a later turn must still deliver");
            True(
                completed[1].TurnSequence > firstSequence,
                "identical-text ABA must use a newer turn sequence");
            True(
                drafts[drafts.Count - 1].TurnSequence >
                firstSequence,
                "new identical draft must not reuse the old generation");
        }

        private static void OpenAiRealtimeWritebackCorrelatesAsyncTools()
        {
            var completed = new List<RealtimeConversationExchange>();
            var drafts =
                new List<RealtimeConversationDraftUpdate>();
            var tracker = new OpenAiConversationTurnTracker(
                "openai-session",
                completed.Add,
                drafts.Add);

            tracker.ObserveResponseCreated("response-1");
            tracker.ObserveUserItem("user-1");
            tracker.CompleteResponse("response-1", "completed", false);
            tracker.ObserveOutputTranscriptionDelta(
                "response-1",
                "assistant-1",
                "answer ");
            tracker.ObserveOutputTranscriptionDelta(
                "response-1",
                "assistant-1",
                "one");
            tracker.ObserveOutputTranscription(
                "response-1",
                "assistant-1",
                "answer one");
            Equal(0, completed.Count, "missing async input transcript must not save");
            tracker.ObserveInputTranscriptionDelta(
                "user-1",
                "question ");
            tracker.ObserveInputTranscriptionDelta(
                "user-1",
                "one");
            tracker.ObserveInputTranscription("user-1", "question one");
            Equal(0, completed.Count, "OpenAI playback must drain before writeback");
            tracker.MarkPlaybackCompleted("response-1");
            Equal(1, completed.Count, "late input transcript completes correlated response");
            True(
                drafts.Count >= 4,
                "OpenAI input and output transcript deltas publish drafts");

            tracker.ObserveUserItem("user-2");
            tracker.ObserveInputTranscription("user-2", "add an appointment");
            tracker.ObserveResponseCreated("response-tool");
            tracker.CompleteResponse("response-tool", "completed", true);
            tracker.ObserveResponseCreated("response-confirmation");
            tracker.ObserveOutputTranscription(
                "response-confirmation",
                "assistant-confirmation",
                "Added.");
            tracker.CompleteResponse("response-confirmation", "completed", false);
            tracker.MarkPlaybackCompleted("response-confirmation");
            Equal(2, completed.Count,
                "tool call and spoken confirmation must save as one user turn");
            Equal("add an appointment", completed[1].Question,
                "tool continuation keeps the original user transcript");
            Equal("Added.", completed[1].Answer,
                "tool continuation saves only the final spoken answer");

            tracker.CompleteResponse("response-confirmation", "completed", false);
            tracker.ObserveOutputTranscription(
                "response-confirmation",
                "assistant-confirmation",
                "Added.");
            Equal(2, completed.Count, "duplicate response events must not duplicate delivery");

            tracker.ObserveUserItem("user-late");
            tracker.ObserveResponseCreated("response-late");
            tracker.ObserveOutputTranscription(
                "response-late",
                "assistant-late",
                "answer before cutoff");
            tracker.CompleteResponse("response-late", "completed", false);
            tracker.MarkPlaybackCompleted("response-late");
            tracker.ObserveInputTranscription("user-late", "question after cutoff");
            Equal(2, completed.Count,
                "OpenAI transcript arriving after playback cutoff must not revive a missing turn");
        }

        private static void OpenAiHistoryAndInterruptedResponsesAreIsolated()
        {
            var completed = new List<RealtimeConversationExchange>();
            var drafts =
                new List<RealtimeConversationDraftUpdate>();
            var tracker = new OpenAiConversationTurnTracker(
                "openai-interruption",
                completed.Add,
                drafts.Add);
            tracker.ObserveUserItem("user-interrupted");
            tracker.ObserveInputTranscription("user-interrupted", "stop this answer");
            tracker.ObserveResponseCreated("response-interrupted");
            True(
                !tracker.ObserveOutputItem(
                    "response-interrupted",
                    "item_hist_old_00_a"),
                "history assistant item must never become current output");
            True(
                tracker.ObserveOutputItem(
                    "response-interrupted",
                    "assistant-generated"),
                "generated assistant item may become current output");
            True(
                !tracker.IsGeneratedOutputItem("item_hist_old_00_a"),
                "history assistant item must never become a truncate target");
            tracker.InterruptActiveResponse();
            tracker.ObserveOutputTranscription(
                "response-interrupted",
                "assistant-generated",
                "partial transcript");
            tracker.CompleteResponse("response-interrupted", "cancelled", false);
            tracker.MarkPlaybackCompleted("response-interrupted");
            tracker.CompleteResponse("response-interrupted", "completed", false);
            Equal(0, completed.Count,
                "interrupted, cancelled, and late completion events must never save");
            True(
                drafts.Exists(
                    delegate(RealtimeConversationDraftUpdate update)
                    {
                        return update.Cleared;
                    }),
                "interrupted response must clear its exact draft generation");
        }

        private static void RealtimeWritebackRejectsOldSessionsAndDuplicates()
        {
            var store = RealtimeConversationStore.CreateInMemory();
            var binding =
                store.FreezeActiveWindow("current-session");
            True(
                !store.CommitExchange(
                    binding,
                    new RealtimeConversationExchange(
                        "old-session",
                        "turn-1",
                        1,
                        1,
                        "old question",
                        "old answer")),
                "old session exchange must be rejected");
            Equal(
                0,
                store.SnapshotWindow(0).Length,
                "old session leaves window unchanged");

            True(
                store.ApplyDraft(
                    binding,
                    new RealtimeConversationDraftUpdate(
                        "current-session",
                        "turn-1",
                        1,
                        5,
                        "current question",
                        "current answer",
                        false)),
                "current draft accepted");
            True(
                !store.ApplyDraft(
                    binding,
                    new RealtimeConversationDraftUpdate(
                        "current-session",
                        "turn-1",
                        1,
                        4,
                        "stale question",
                        "stale answer",
                        false)),
                "older revision rejected");
            True(
                !store.CommitExchange(
                    binding,
                    new RealtimeConversationExchange(
                        "current-session",
                        "turn-1",
                        1,
                        4,
                        "stale question",
                        "stale answer")),
                "exchange older than displayed draft rejected");

            var exchange = new RealtimeConversationExchange(
                "current-session",
                "turn-1",
                1,
                6,
                "current question",
                "current answer");
            True(store.CommitExchange(binding, exchange),
                "current session exchange must be accepted");
            True(!store.CommitExchange(binding, exchange),
                "same turn delivery must be deduplicated");
            Equal(
                1,
                store.SnapshotWindow(0).Length,
                "deduplicated exchange remains one Q&A pair");

            var oldBinding = binding;
            True(store.EndSession(binding), "first session ends");
            var nextBinding =
                store.FreezeActiveWindow("new-session");
            True(
                !store.ApplyDraft(
                    oldBinding,
                    new RealtimeConversationDraftUpdate(
                        "current-session",
                        "turn-2",
                        2,
                        7,
                        "late question",
                        "late answer",
                        false)),
                "old session draft callback must be rejected");

            True(
                store.ApplyDraft(
                    nextBinding,
                    new RealtimeConversationDraftUpdate(
                        "new-session",
                        "same-a",
                        1,
                        1,
                        "identical",
                        "identical",
                        false)),
                "first identical draft accepted");
            True(
                store.ApplyDraft(
                    nextBinding,
                    new RealtimeConversationDraftUpdate(
                        "new-session",
                        "same-a",
                        1,
                        2,
                        "",
                        "",
                        true)),
                "first identical draft cleared");
            True(
                store.ApplyDraft(
                    nextBinding,
                    new RealtimeConversationDraftUpdate(
                        "new-session",
                        "same-b",
                        2,
                        3,
                        "identical",
                        "identical",
                        false)),
                "second identical draft accepted");
            True(
                !store.ApplyDraft(
                    nextBinding,
                    new RealtimeConversationDraftUpdate(
                        "new-session",
                        "same-a",
                        1,
                        4,
                        "",
                        "",
                        true)),
                "late clear from an older turn cannot erase identical new text");
            var draft = store.SnapshotDraftForWindow(0);
            Equal(2, draft.TurnSequence, "ABA-safe draft sequence");
            Equal("identical", draft.Question, "new identical draft retained");
        }

        private static void RetiredRealtimeTranslationStoreIsAbsent()
        {
            var root = FindProjectRoot();
            var storeSource = File.ReadAllText(
                Path.Combine(root, "RealtimeConversation.cs"),
                Encoding.UTF8);
            True(
                storeSource.IndexOf(
                    "LoadTranslation",
                    StringComparison.Ordinal) < 0 &&
                storeSource.IndexOf(
                    "CreateTranslationInMemory",
                    StringComparison.Ordinal) < 0 &&
                storeSource.IndexOf(
                    "realtime-translations.json",
                    StringComparison.OrdinalIgnoreCase) < 0 &&
                storeSource.IndexOf(
                    "realtime_translation",
                    StringComparison.OrdinalIgnoreCase) < 0,
                "retired translation store and widget code must be absent");

            var chat = RealtimeConversationStore.CreateInMemory();
            var binding = chat.FreezeActiveWindow("chat-only");
            True(
                chat.CommitExchange(
                    binding,
                    new RealtimeConversationExchange(
                        "chat-only",
                        "chat-turn",
                        1,
                        1,
                        "chat question",
                        "chat answer")),
                "chat-only store remains functional");
            Equal(
                1,
                chat.SnapshotWindow(0).Length,
                "chat-only store persists one complete pair");
            True(chat.EndSession(binding), "chat-only session ends");
        }

        private static void RealtimeConversationProtectsFrozenModuleMutation()
        {
            var chat =
                RealtimeConversationStore.CreateInMemory();
            var binding =
                chat.FreezeActiveWindow("active-session");
            True(
                !chat.ClearActiveWindow(true),
                "active chat session blocks clearing its window");

            True(chat.EndSession(binding), "chat session ends");
            True(
                chat.ClearActiveWindow(true),
                "chat window may be cleared after session");
        }

        private static void RealtimeLocalToolsUseNotesAndRespectCancellation()
        {
            var scheduleStore = ScheduleStore.CreateInMemory();
            var noteStore = new WidgetStore(false);
            var executor = new ScheduleToolExecutor(
                "gemini",
                scheduleStore,
                noteStore,
                null);

            var saved = executor.Execute(
                "add_schedule",
                "note-call-1",
                new Dictionary<string, object>
                {
                    { "content", "Buy replacement filters" },
                    { "date", "" }
                });
            Equal(true, Convert.ToBoolean(saved["ok"]),
                "undated tool write succeeds");
            Equal("memo", Convert.ToString(saved["kind"]),
                "undated tool write is a Note");
            Equal(1, noteStore.SnapshotNotesForTool().Length,
                "undated tool write reaches the Notes store");
            Equal(
                "Buy replacement filters",
                noteStore.SnapshotNotesForTool()[0].text,
                "Notes store keeps the direct requested content");
            Equal(
                0,
                scheduleStore.Snapshot().Length,
                "undated tool write must not create a hidden schedule memo");

            var indirect = executor.Execute(
                "add_schedule",
                "note-call-2",
                new Dictionary<string, object>
                {
                    {
                        "content",
                        "The user plans to buy replacement filters"
                    },
                    { "date", "" }
                });
            Equal(false, Convert.ToBoolean(indirect["ok"]),
                "indirect narration must be rejected");
            Equal("content_must_be_direct",
                Convert.ToString(indirect["status"]),
                "indirect narration rejection status");
            Equal(1, noteStore.SnapshotNotesForTool().Length,
                "rejected narration must not be written");

            var read = executor.Execute(
                "read_schedule",
                "read-notes",
                new Dictionary<string, object>
                {
                    { "scope", "memos" },
                    { "date", "" }
                });
            Equal(true, Convert.ToBoolean(read["ok"]),
                "Notes read succeeds");
            var items =
                (Dictionary<string, object>[])read["items"];
            Equal(1, items.Length, "Notes read returns the saved note");
            Equal(
                "Buy replacement filters",
                Convert.ToString(items[0]["content"]),
                "Notes read exposes only direct content");
            True(!items[0].ContainsKey("id") &&
                 !items[0].ContainsKey("createdAt") &&
                 !items[0].ContainsKey("sourceCallId"),
                "tool reads must not expose local identifiers or timestamps");

            executor.CancelCalls(new[] { "cancelled-write" });
            var cancelled = executor.Execute(
                "add_schedule",
                "cancelled-write",
                new Dictionary<string, object>
                {
                    { "content", "Must never be written" },
                    { "date", "" }
                });
            Equal(false, Convert.ToBoolean(cancelled["ok"]),
                "cancelled tool call must fail closed");
            Equal("tool_call_cancelled",
                Convert.ToString(cancelled["status"]),
                "cancelled tool status");
            Equal(1, noteStore.SnapshotNotesForTool().Length,
                "cancelled call must not late-write a Note");

            var ended = executor.Execute(
                "end_conversation",
                "end-call",
                new Dictionary<string, object>());
            Equal(true, Convert.ToBoolean(ended["ok"]),
                "explicit end tool succeeds");
            True(executor.EndRequested,
                "explicit end tool sets the terminal session flag");
        }

        private static void OpenAiContinuationAndInterruptionAreAtomic()
        {
            var response = (Dictionary<string, object>)
                LiveConversationProtocol.OpenAiResponseCreate()["response"];
            Equal(
                "none",
                Convert.ToString(response["tool_choice"]),
                "tool continuation must not recursively invoke tools");
            True(
                ConversationAudioOutput.HasPendingPlayback(
                    true,
                    false,
                    0),
                "active response is interruptible");
            True(
                ConversationAudioOutput.HasPendingPlayback(
                    false,
                    true,
                    0),
                "completed response awaiting drain is interruptible");
            True(
                ConversationAudioOutput.HasPendingPlayback(
                    false,
                    false,
                    320),
                "buffered audio is interruptible");
            True(
                !ConversationAudioOutput.HasPendingPlayback(
                    false,
                    false,
                    0),
                "natural playback drain has no truncate target");
        }

        private static void WebSocketAbortBeforeConnectIsImmediate()
        {
            var socket = new SimpleSecureWebSocket(
                "wss://127.0.0.1:1/",
                new Dictionary<string, string>(),
                100);
            socket.Dispose();
            var disposed = false;
            try
            {
                socket.Connect();
            }
            catch (ObjectDisposedException)
            {
                disposed = true;
            }
            True(
                disposed,
                "pre-disposed websocket must fail before DNS, TCP, or TLS work");
        }

        private static void RecordingDiagnosticsAreSafeAndClassified()
        {
            var capture = new RecorderCaptureDiagnostics(
                16000,
                1,
                16,
                20,
                640,
                3,
                "Current Default Input Microphone",
                true,
                true,
                64000,
                100,
                20697,
                DateTime.UtcNow.AddSeconds(-2),
                DateTime.UtcNow,
                "");
            var senderFailure = new InvalidOperationException(
                "Realtime audio sender did not finish sending all audio. sk-secret-transcript");
            Equal(
                "audio_sender_drain_failure",
                AudioFailureClassifier.Classify(
                    senderFailure,
                    capture,
                    NewRealtimeDiagnostics(1, false),
                    false),
                "sender drain classification");
            Equal(
                "no_speech_or_too_short",
                AudioFailureClassifier.Classify(
                    new NoSpeechDetectedException(),
                    capture,
                    NewRealtimeDiagnostics(2, true),
                    false),
                "no-speech classification");
            Equal(
                "provider_empty_result",
                AudioFailureClassifier.Classify(
                    null,
                    capture,
                    NewRealtimeDiagnostics(3, true),
                    true),
                "empty cloud result classification");
            Equal(
                "local_audio_not_sent",
                AudioFailureClassifier.Classify(
                    new InvalidOperationException("setup failed"),
                    capture,
                    NewRealtimeDiagnostics(4, false, false),
                    false),
                "local-unsent classification");
            var interruptedCapture = new RecorderCaptureDiagnostics(
                16000,
                1,
                16,
                20,
                640,
                3,
                "Current Default Input Microphone",
                true,
                false,
                32000,
                50,
                12000,
                DateTime.UtcNow.AddSeconds(-1),
                DateTime.UtcNow,
                "waveInAddBuffer failed");
            Equal(
                "capture_stop_failure",
                AudioFailureClassifier.Classify(
                    new InvalidOperationException("device interrupted"),
                    interruptedCapture,
                    NewRealtimeDiagnostics(5, true),
                    false),
                "capture interruption classification");

            var directory = NewArchiveTestDirectory();
            try
            {
                var store = new RecordingDiagnosticsStore(directory);
                var path = store.Save(
                    CaptureTarget.Typing,
                    "realtime_failure",
                    "audio_sender_drain_failure",
                    senderFailure,
                    capture,
                    NewRealtimeDiagnostics(10, false),
                    "clipboard_fallback_automation_timeout");
                var text = File.ReadAllText(path, Encoding.UTF8);
                True(
                    text.Contains(
                        "deliveryOutcome=clipboard_fallback_automation_timeout"),
                    "paste delivery outcome missing");
                True(text.Contains("rawCaptureMaxPeak=20697"), "raw capture peak missing");
                True(text.Contains("sentMaxPeak=18000"), "sent peak missing");
                True(text.Contains("terminalSignal=gemini.activityEnd"), "terminal signal missing");
                True(text.Contains("localVadDeferredTurnsDiscarded=0"), "deferred-tail discard count missing");
                True(text.Contains("localVadLastDiscardedTurnDurationMs=0"), "deferred-tail duration diagnostic missing");
                True(text.Contains("localVadLastDiscardedTurnMaxPeak=0"), "deferred-tail peak diagnostic missing");
                True(!text.Contains("sk-secret-transcript"), "exception content leaked into diagnostics");
                True(!text.Contains("prompt="), "prompt content must never be written");
                True(!text.Contains("transcript="), "transcript content must never be written");

                for (var index = 0; index < RecordingDiagnosticsStore.MaximumFiles + 5; index++)
                {
                    store.Save(
                        CaptureTarget.Notes,
                        "success",
                        "success",
                        null,
                        capture,
                        NewRealtimeDiagnostics(100 + index, true));
                }
                var files = Directory.GetFiles(directory, "*.txt");
                True(files.Length <= RecordingDiagnosticsStore.MaximumFiles, "diagnostics retention cap");
                foreach (var file in files)
                {
                    True(
                        new FileInfo(file).Length <= RecordingDiagnosticsStore.MaximumFileBytes,
                        "diagnostics file-size cap");
                }
            }
            finally
            {
                DeleteArchiveTestDirectory(directory);
            }
        }

        private static void WakeWordHelperIsNotPackaged()
        {
            using (Stream embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream("WakeWordHost.exe"))
            {
                True(
                    embedded == null,
                    "retired wake-word helper must not be embedded into the distributable");
            }
        }

        private static void RetiredSettingsSurfacesAreAbsent()
        {
            True(
                typeof(AppSettings).GetField(
                    "WakeRealtimeConversationEnabled") == null,
                "Hi Mina setting must be removed");
            True(
                typeof(AppSettings).GetField(
                    "RealtimeTranslationHotkey") == null,
                "realtime translation hotkey setting must be removed");
            True(
                typeof(AppSettings).GetField(
                    "ShowRealtimeTranslationWidget") == null,
                "realtime translation visibility setting must be removed");
            True(
                typeof(ShortcutAction).GetField("Qa") == null,
                "retired realtime translation shortcut action must be removed");
            True(
                FeatureLocalizationCatalog.IsRetiredDesktopUiKey(
                    "privacy") &&
                FeatureLocalizationCatalog.IsRetiredDesktopUiKey(
                    "conversation_start") &&
                FeatureLocalizationCatalog.IsRetiredDesktopUiKey(
                    "show_realtime") &&
                FeatureLocalizationCatalog.IsRetiredDesktopUiKey(
                    "wake_conversation") &&
                FeatureLocalizationCatalog.IsRetiredDesktopUiKey(
                    "realtime_translation_title"),
                "retired settings localization keys must stay blocked");
        }

        private static void AudioCaptureUsesDefaultInputPlaybackKeepsCommunications()
        {
            var recorderFlag = typeof(Recorder).GetField(
                "WaveMappedDefaultCommunicationDevice",
                BindingFlags.Static | BindingFlags.NonPublic);
            var playerFlag = typeof(StreamingPcmPlayer).GetField(
                "WaveMappedDefaultCommunicationDevice",
                BindingFlags.Static | BindingFlags.NonPublic);
            var retiredCaptureRoleHelper = typeof(WindowsAudioRoute).GetMethod(
                "DefaultCommunicationCaptureId",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (playerFlag == null)
            {
                throw new InvalidOperationException("playback communications device flag not found");
            }
            True(
                recorderFlag == null,
                "microphone capture must not force the independent Windows communications role");
            True(
                retiredCaptureRoleHelper == null,
                "capture must not keep a communications-role selector beside the default mapper");
            Equal(0x10, Convert.ToInt32(playerFlag.GetRawConstantValue()), "playback communications device flag");
        }

        private static void VersionUpdated()
        {
            const string expectedVersion = "0.4.83";
            const string expectedAssemblyVersion = "0.4.83.0";
            Equal(expectedVersion, AppInfo.Version, "application version");

            var root = FindProjectRoot();
            var programSource = File.ReadAllText(
                Path.Combine(root, "Program.cs"),
                Encoding.UTF8);
            var installerSource = File.ReadAllText(
                Path.Combine(root, "installer", "Installer.cs"),
                Encoding.UTF8);
            foreach (var source in new[] { programSource, installerSource })
            {
                True(
                    source.IndexOf(
                        "AssemblyVersion(\"" +
                        expectedAssemblyVersion +
                        "\")",
                        StringComparison.Ordinal) >= 0,
                    "assembly version must match " + expectedAssemblyVersion);
                True(
                    source.IndexOf(
                        "AssemblyFileVersion(\"" +
                        expectedAssemblyVersion +
                        "\")",
                        StringComparison.Ordinal) >= 0,
                    "file version must match " + expectedAssemblyVersion);
                True(
                    source.IndexOf(
                        "AssemblyInformationalVersion(\"" +
                        expectedVersion +
                        "\")",
                        StringComparison.Ordinal) >= 0,
                    "informational version must match " + expectedVersion);
            }
        }

        private static void PortableAppIconLoadsWithoutSidecarFile()
        {
            var icon = AppIcon.LoadEmbeddedOrExecutable();
            True(icon != null, "portable icon must load");
            using (var actual = icon.ToBitmap())
            using (var fallback = SystemIcons.Application.ToBitmap())
            {
                var differs = actual.Width != fallback.Width || actual.Height != fallback.Height;
                for (var y = 0; !differs && y < actual.Height; y++)
                {
                    for (var x = 0; x < actual.Width; x++)
                    {
                        if (actual.GetPixel(x, y).ToArgb() == fallback.GetPixel(x, y).ToArgb()) continue;
                        differs = true;
                        break;
                    }
                }
                True(differs, "portable icon must not fall back to the Windows default icon");
            }
            if (!object.ReferenceEquals(icon, SystemIcons.Application)) icon.Dispose();
        }

        private static void WidgetVisibilityAnimationKeepsExactEndpoints()
        {
            Equal(220, WidgetVisibilityAnimator.DurationMilliseconds, "widget animation duration");
            Equal(10, WidgetVisibilityAnimator.FrameIntervalMilliseconds, "widget animation frame interval");
            True(
                WidgetVisibilityAnimator.DurationMilliseconds / WidgetVisibilityAnimator.FrameIntervalMilliseconds == 22,
                "widget animation must match the 0.4.22 transition cadence");
            var timerField = typeof(WidgetVisibilityAnimator).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic);
            True(timerField != null, "widget animation timer field");
            Equal(typeof(System.Windows.Forms.Timer), timerField.FieldType, "0.4.22 UI-thread timer type");
            var area = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            var saved = new Rectangle(area.Left + 120, area.Top + 180, 420, 620);
            var belowScreen = WidgetVisibilityAnimator.BelowScreenTarget(saved);
            Equal(saved.Size, belowScreen.Size, "below-screen target must preserve the rendered window size");
            Equal(saved.X, belowScreen.X, "vertical animation must preserve x");
            Equal(area.Bottom, belowScreen.Y, "hidden target must start below the display");
            Equal(belowScreen, WidgetVisibilityAnimator.Interpolate(belowScreen, saved, 0d), "animation start bounds");
            Equal(saved, WidgetVisibilityAnimator.Interpolate(belowScreen, saved, 1d), "animation end bounds");
            var middle = WidgetVisibilityAnimator.Interpolate(belowScreen, saved, 0.5d);
            Equal(saved.Width, middle.Width, "animation midpoint width remains constant");
            Equal(saved.Height, middle.Height, "animation midpoint height remains constant");
            Equal(saved.X, middle.X, "animation midpoint x remains constant");
            True(middle.Y > Math.Min(belowScreen.Y, saved.Y) && middle.Y < Math.Max(belowScreen.Y, saved.Y), "animation midpoint y");
        }

        private static void WidgetAnimationKeepsLiveControlsVisible()
        {
            using (var form = new AnimationTestWidgetForm())
            {
                form.Location = new Point(-10000, -10000);
                form.CreateControl();
                form.PerformLayout();
                form.ShowWidget();
                True(form.VisibleChild.Visible, "visible child starts visible");
                True(!form.HiddenChild.Visible, "hidden child starts hidden");

                form.SetAnimationLayer(true);
                True(form.VisibleChild.Visible, "0.4.22 animation must keep visible controls live");
                True(!form.HiddenChild.Visible, "0.4.22 animation must preserve hidden controls");
                form.SetAnimationLayer(false);
            }
        }

        private static void WidgetAnimatorCompletesWithoutSecondHide()
        {
            using (var first = new AnimationTestWidgetForm())
            using (var second = new AnimationTestWidgetForm())
            using (var animator = new WidgetVisibilityAnimator(first, second))
            {
                first.Location = new Point(-10000, -10000);
                second.Location = new Point(-9600, -10000);
                var firstFinal = first.Location;
                var secondFinal = second.Location;
                first.CreateControl();
                second.CreateControl();
                first.Hide();
                second.Hide();

                var showCompleted = false;
                True(
                    animator.Show(delegate { showCompleted = true; }),
                    "initial show animation accepted");
                var inverseCompleted = false;
                True(
                    !animator.Hide(
                        delegate { inverseCompleted = true; }),
                    "inverse trigger during show animation must be ignored");
                WaitForAnimation(delegate { return showCompleted; });
                True(
                    !inverseCompleted,
                    "ignored inverse trigger must not be queued after show completion");
                True(first.Visible && second.Visible, "both widgets must remain visible after show completion");
                Equal(firstFinal, first.Location, "first widget final location");
                Equal(secondFinal, second.Location, "second widget final location");
                True(first.VisibleChild.Visible && second.VisibleChild.Visible, "live widget controls must be restored after show");

                var hideCompleted = false;
                True(
                    animator.Hide(delegate { hideCompleted = true; }),
                    "hide animation accepted after show completion");
                WaitForAnimation(delegate { return hideCompleted; });
                True(!first.Visible && !second.Visible, "both widgets must remain hidden after hide completion");
                Equal(firstFinal, first.Location, "first widget saved location after hide");
                Equal(secondFinal, second.Location, "second widget saved location after hide");

                var subsetCompleted = false;
                True(
                    animator.Show(
                        new AppleWidgetForm[] { first },
                        delegate { subsetCompleted = true; }),
                    "single enabled widget animation accepted");
                WaitForAnimation(delegate { return subsetCompleted; });
                True(
                    first.Visible && !second.Visible,
                    "independent visibility preference must animate only the enabled widget");
                var subsetHideCompleted = false;
                animator.Hide(
                    new AppleWidgetForm[] { first },
                    delegate { subsetHideCompleted = true; });
                WaitForAnimation(
                    delegate { return subsetHideCompleted; });
            }
        }

        private static void WaitForAnimation(Func<bool> completed)
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (!completed() && DateTime.UtcNow < deadline)
            {
                System.Windows.Forms.Application.DoEvents();
                System.Threading.Thread.Sleep(5);
            }
            True(completed(), "widget animation completion callback timed out");
        }

        private static void WidgetVisibilityShortcutRespondsImmediately()
        {
            Equal(120, RightAltHook.TriggerDebounceMilliseconds, "shortcut debounce");
            Equal(500, RightAltHook.VisibilityTriggerDebounceMilliseconds, "visibility shortcut duplicate guard");
            True(
                RightAltHook.VisibilityTriggerDebounceMilliseconds >
                WidgetVisibilityAnimator.DurationMilliseconds,
                "visibility duplicate guard must outlast the complete show animation");
            True(RightAltHook.IsImmediateVisibilityKey(0x13), "Pause/Break should trigger on key down");
            True(RightAltHook.IsImmediateVisibilityKey(0x21), "Page Up should trigger on key down");
            True(RightAltHook.IsImmediateVisibilityKey(0x22), "Page Down should trigger on key down");
            True(RightAltHook.IsImmediateVisibilityKey(0x91), "Scroll Lock should trigger on key down");
            True(!RightAltHook.IsImmediateVisibilityKey(0xA5), "Right Alt must wait for possible combinations");
            True(!RightAltHook.IsImmediateVisibilityKey(0xA3), "Right Ctrl must wait for possible combinations");
            True(RightAltHook.ShouldFireImmediateVisibilityKey(0x13, true), "Pause/Break must remain responsive without a key-up event");
            True(!RightAltHook.ShouldFireImmediateVisibilityKey(0x21, true), "held Page Up must not auto-repeat the toggle");
            True(RightAltHook.ShouldFireSingleOnKeyUp(true, false), "a genuine single-key release should fire once");
            True(!RightAltHook.ShouldFireSingleOnKeyUp(false, false), "a duplicate key-up without key-down must be ignored");
            True(!RightAltHook.ShouldFireSingleOnKeyUp(true, true), "a combo-suppressed key-up must not fire a single-key shortcut");

            var visibilityTriggers = 0;
            var hook = new RightAltHook(new AppSettings(), delegate(ShortcutAction action)
            {
                if (action == ShortcutAction.RestoreWidgets) visibilityTriggers++;
            });
            var fire = typeof(RightAltHook).GetMethod("Fire", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fire == null) throw new InvalidOperationException("RightAltHook.Fire not found");
            fire.Invoke(hook, new object[] { ShortcutAction.RestoreWidgets });
            fire.Invoke(hook, new object[] { ShortcutAction.RestoreWidgets });
            Equal(1, visibilityTriggers, "an immediate duplicate visibility event must remain suppressed");
            System.Threading.Thread.Sleep(RightAltHook.VisibilityTriggerDebounceMilliseconds + 30);
            fire.Invoke(hook, new object[] { ShortcutAction.RestoreWidgets });
            Equal(2, visibilityTriggers, "a deliberate follow-up visibility press must be accepted after the duplicate guard");
        }

        private static void DisplayLayoutKeysFollowMonitorConfiguration()
        {
            var primary = new DisplayConfigurationPart("DISPLAY1", new Rectangle(0, 0, 2560, 1440), true);
            var secondary = new DisplayConfigurationPart("DISPLAY2", new Rectangle(2560, 0, 1920, 1080), false);
            var first = DisplayConfiguration.BuildKey(new[] { primary, secondary });
            var reordered = DisplayConfiguration.BuildKey(new[] { secondary, primary });
            Equal(first, reordered, "display key should not depend on enumeration order");

            var changedResolution = DisplayConfiguration.BuildKey(new[]
            {
                new DisplayConfigurationPart("DISPLAY1", new Rectangle(0, 0, 1920, 1080), true),
                secondary
            });
            True(first != changedResolution, "resolution change should select another layout profile");

            var changedPrimary = DisplayConfiguration.BuildKey(new[]
            {
                new DisplayConfigurationPart("DISPLAY1", new Rectangle(0, 0, 2560, 1440), false),
                new DisplayConfigurationPart("DISPLAY2", new Rectangle(2560, 0, 1920, 1080), true)
            });
            True(first != changedPrimary, "primary display change should select another layout profile");
        }

        private static void DisplayLayoutProfilesPersistIndependently()
        {
            var area = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            var notes = new Rectangle(area.Left + 20, area.Top + 20, 400, 500);
            var qa = new Rectangle(area.Left + 440, area.Top + 20, 420, 520);
            var realtime = new Rectangle(
                area.Left + 880,
                area.Top + 20,
                410,
                510);
            var data = new WidgetData
            {
                displayProfiles = new[]
                {
                    new DisplayWidgetLayoutData
                    {
                        key = "profile-a",
                        notesX = notes.X,
                        notesY = notes.Y,
                        notesW = notes.Width,
                        notesH = notes.Height,
                        qaX = qa.X,
                        qaY = qa.Y,
                        qaW = qa.Width,
                        qaH = qa.Height,
                        realtimeX = realtime.X,
                        realtimeY = realtime.Y,
                        realtimeW = realtime.Width,
                        realtimeH = realtime.Height
                    }
                }
            };
            var store = new WidgetStore();
            var load = typeof(WidgetStore).GetMethod("LoadDisplayLayoutProfiles", BindingFlags.Instance | BindingFlags.NonPublic);
            if (load == null) throw new InvalidOperationException("LoadDisplayLayoutProfiles not found");
            load.Invoke(store, new object[] { data });

            WidgetLayout restored;
            True(store.TryGetWidgetLayoutProfile("profile-a", out restored), "saved profile should be available");
            Equal(notes, restored.Notes, "notes profile bounds");
            Equal(qa, restored.Qa, "Q&A profile bounds");
            Equal(
                WidgetLayoutCalculator.EnsureVisible(realtime),
                restored.Realtime,
                "realtime profile bounds");
            True(!store.TryGetWidgetLayoutProfile("profile-b", out restored), "profiles should remain isolated");

            var serialize = typeof(WidgetStore).GetMethod("SerializeDisplayLayoutProfiles", BindingFlags.Instance | BindingFlags.NonPublic);
            if (serialize == null) throw new InvalidOperationException("SerializeDisplayLayoutProfiles not found");
            var persisted = (DisplayWidgetLayoutData[])serialize.Invoke(store, null);
            Equal(1, persisted.Length, "serialized profile count");
            Equal("profile-a", persisted[0].key, "serialized profile key");
            Equal(notes.Width, persisted[0].notesW, "serialized notes width");
            Equal(qa.Height, persisted[0].qaH, "serialized Q&A height");
            Equal(
                realtime.Width,
                persisted[0].realtimeW,
                "serialized realtime width");
        }

        private sealed class AnimationTestWidgetForm : AppleWidgetForm
        {
            public readonly System.Windows.Forms.Label VisibleChild;
            public readonly System.Windows.Forms.Label HiddenChild;

            public AnimationTestWidgetForm()
                : base("Animation test", new Size(320, 260), Point.Empty)
            {
                VisibleChild = new System.Windows.Forms.Label { Text = "visible", Visible = true };
                HiddenChild = new System.Windows.Forms.Label { Text = "hidden", Visible = false };
                Controls.Add(VisibleChild);
                Controls.Add(HiddenChild);
            }
        }

        private static void NotePreviewsExpandUpToFiveLines()
        {
            Equal(5, NotesWidgetForm.MaximumPreviewLines, "maximum preview lines");
            Equal(18, NotesWidgetForm.ClampPreviewLabelHeight(18, 18), "single-line preview height");
            Equal(94, NotesWidgetForm.ClampPreviewLabelHeight(300, 18), "five-line preview height cap");

            var note = new NoteItem { text = "one\ntwo\nthree\nfour\nfive\nsix" };
            var preview = NotesWidgetForm.NotePreview(note);
            Equal("one\r\ntwo\r\nthree\r\nfour\r\nfive", preview, "five-line note preview");
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        private static RealtimeSessionDiagnostics NewRealtimeDiagnostics(long requestId, bool senderDrained)
        {
            return NewRealtimeDiagnostics(requestId, senderDrained, true);
        }

        private static RealtimeSessionDiagnostics NewRealtimeDiagnostics(
            long requestId,
            bool senderDrained,
            bool hasSentAudio)
        {
            return new RealtimeSessionDiagnostics(
                requestId,
                "GeminiLocalVadCumulative",
                16000,
                20,
                40,
                64000,
                100,
                20697,
                64000,
                50,
                20697,
                hasSentAudio ? (senderDrained ? 64000 : 32000) : 0,
                hasSentAudio ? (senderDrained ? 50 : 25) : 0,
                hasSentAudio ? 18000 : 0,
                senderDrained,
                "gemini.activityEnd",
                DateTime.UtcNow.AddSeconds(-2),
                DateTime.UtcNow.AddSeconds(-1),
                DateTime.UtcNow,
                senderDrained ? "gemini-integrated" : "",
                new[] { 10520, 15520 },
                0,
                0,
                0);
        }

        private static int TotalFilteredBytes(List<RealtimeSpeechAudioFrameResult> results)
        {
            var total = 0;
            if (results == null) return total;
            foreach (var result in results)
            {
                if (result == null || result.Audio == null) continue;
                foreach (var chunk in result.Audio)
                {
                    if (chunk != null) total += chunk.Length;
                }
            }
            return total;
        }

        private static int TotalFilteredBytes(List<GeminiLocalVadBoundaryResult> results)
        {
            var total = 0;
            if (results == null) return total;
            foreach (var result in results)
            {
                if (result != null) total += TotalBytes(result.Audio);
            }
            return total;
        }

        private static bool ContainsAction(
            List<GeminiLocalVadBoundaryResult> results,
            GeminiLocalVadAction action)
        {
            if (results == null) return false;
            foreach (var result in results)
            {
                if (result != null && (result.Action & action) != 0) return true;
            }
            return false;
        }

        private static byte[] RunLocalVadFilter(byte[] input, int[] chunkPattern)
        {
            var filter = new GeminiLocalVadPeakSilenceFilter(16000);
            using (var output = new MemoryStream())
            {
                var offset = 0;
                var patternIndex = 0;
                while (offset < input.Length)
                {
                    var requested = chunkPattern[patternIndex % chunkPattern.Length];
                    patternIndex++;
                    requested = Math.Max(BytesPerSample, requested - requested % BytesPerSample);
                    var length = Math.Min(requested, input.Length - offset);
                    length -= length % BytesPerSample;
                    if (length <= 0) break;
                    var chunk = new byte[length];
                    Buffer.BlockCopy(input, offset, chunk, 0, length);
                    foreach (var result in filter.Process(chunk))
                    {
                        foreach (var audio in result.Audio)
                        {
                            output.Write(audio, 0, audio.Length);
                        }
                    }
                    offset += length;
                }
                foreach (var audio in filter.FinishAll())
                {
                    output.Write(audio, 0, audio.Length);
                }
                return output.ToArray();
            }
        }

        private static int TotalBytes(List<byte[]> chunks)
        {
            var total = 0;
            if (chunks == null) return total;
            foreach (var chunk in chunks)
            {
                if (chunk != null) total += chunk.Length;
            }
            return total;
        }

        private static int ExpectedAcceleratedBytes(int milliseconds, int sampleRate)
        {
            var inputBytes = milliseconds * sampleRate / 1000 * BytesPerSample;
            return ExpectedSpeedBytes(inputBytes);
        }

        private static int ExpectedSpeedBytes(int inputBytes)
        {
            var inputSamples = inputBytes / BytesPerSample;
            return (int)Math.Ceiling(inputSamples / RealtimePcmSpeedProcessor.InputSpeedMultiplier) * BytesPerSample;
        }

        private static int RunSpeed(byte[] input, int chunkBytes)
        {
            return RunSpeedBytes(input, new[] { chunkBytes }).Length;
        }

        private static byte[] RunSpeedBytes(byte[] input, int[] chunkPattern)
        {
            var processor = new RealtimePcmSpeedProcessor();
            using (var output = new System.IO.MemoryStream())
            {
                var offset = 0;
                var patternIndex = 0;
                while (offset < input.Length)
                {
                    var requested = chunkPattern[patternIndex % chunkPattern.Length];
                    patternIndex++;
                    requested = Math.Max(BytesPerSample, requested - requested % BytesPerSample);
                    var length = Math.Min(requested, input.Length - offset);
                    length -= length % BytesPerSample;
                    if (length <= 0) break;
                    var chunk = new byte[length];
                    Buffer.BlockCopy(input, offset, chunk, 0, length);
                    var accelerated = processor.Process(chunk);
                    output.Write(accelerated, 0, accelerated.Length);
                    offset += length;
                }
                foreach (var chunk in processor.Finish())
                {
                    output.Write(chunk, 0, chunk.Length);
                }
                return output.ToArray();
            }
        }

        private static byte[] RampPcm(int sampleCount)
        {
            var output = new byte[Math.Max(0, sampleCount) * BytesPerSample];
            for (var index = 0; index < sampleCount; index++)
            {
                var value = (short)((index * 37) % 28000 - 14000);
                output[index * BytesPerSample] = (byte)(value & 0xff);
                output[index * BytesPerSample + 1] = (byte)((value >> 8) & 0xff);
            }
            return output;
        }

        private static string NewArchiveTestDirectory()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "VoiceTypingAI-NativeQaArchive-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string FindProjectRoot()
        {
            var directory =
                new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null &&
                   !File.Exists(
                       Path.Combine(
                           directory.FullName,
                           "Program.cs")))
            {
                directory = directory.Parent;
            }
            if (directory == null)
            {
                throw new InvalidOperationException(
                    "project source root must be discoverable");
            }
            return directory.FullName;
        }

        private static void DeleteArchiveTestDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
            Directory.Delete(directory, true);
        }

        private static byte[] Slice(byte[] source, int offset, int length)
        {
            var result = new byte[length];
            Buffer.BlockCopy(source, offset, result, 0, length);
            return result;
        }

        private static byte[] Combine(params byte[][] chunks)
        {
            var total = 0;
            foreach (var chunk in chunks) total += chunk == null ? 0 : chunk.Length;
            var output = new byte[total];
            var offset = 0;
            foreach (var chunk in chunks)
            {
                if (chunk == null || chunk.Length == 0) continue;
                Buffer.BlockCopy(chunk, 0, output, offset, chunk.Length);
                offset += chunk.Length;
            }
            return output;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index]) return false;
            }
            return true;
        }

        private static byte[] Silence(int milliseconds)
        {
            return new byte[milliseconds * SampleRate / 1000 * BytesPerSample];
        }

        private static byte[] Active(int milliseconds)
        {
            return Signal(milliseconds, 1000);
        }

        private static byte[] Signal(int milliseconds, short amplitude)
        {
            return SignalAtSampleRate(milliseconds, SampleRate, amplitude);
        }

        private static byte[] SignalAtSampleRate(int milliseconds, int sampleRate, short amplitude)
        {
            var bytes = new byte[milliseconds * sampleRate / 1000 * BytesPerSample];
            for (var i = 0; i + 1 < bytes.Length; i += 2)
            {
                bytes[i] = (byte)(amplitude & 0xff);
                bytes[i + 1] = (byte)((amplitude >> 8) & 0xff);
            }
            return bytes;
        }

        private static byte[] SparseImpulseAtSampleRate(int milliseconds, int sampleRate, short amplitude)
        {
            var bytes = new byte[milliseconds * sampleRate / 1000 * BytesPerSample];
            if (bytes.Length >= BytesPerSample)
            {
                bytes[0] = (byte)(amplitude & 0xff);
                bytes[1] = (byte)((amplitude >> 8) & 0xff);
            }
            return bytes;
        }

        private static void Run(string name, Action test)
        {
            test();
            passed++;
            Console.WriteLine("PASS: " + name);
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Equal(object expected, object actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + " expected=" + Convert.ToString(expected) + " actual=" + Convert.ToString(actual));
            }
        }

        private static void Near(int expected, int actual, int tolerance, string message)
        {
            if (Math.Abs(expected - actual) > Math.Max(0, tolerance))
            {
                throw new InvalidOperationException(
                    message + " expected=" + expected + " actual=" + actual + " tolerance=" + tolerance);
            }
        }
    }
}
