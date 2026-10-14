using System;
using System.Collections.Generic;

namespace VoiceSecretaryDesktop
{
    internal static class FeatureLocalizationCatalog
    {
        private static readonly string[] Keys =
        {
            "conversation_busy",
            "conversation_connecting",
            "conversation_active",
            "conversation_auto_stop",
            "conversation_ended",
            "conversation_failed",
            "conversation_stopping",
            "connecting_realtime",
            "press_again_to_stop",
            "recording_translation",
            "recording_note",
            "recording_question",
            "recording_conversation",
            "recording_typing",
            "tray_open_settings",
            "tray_toggle_recording",
            "tray_toggle_conversation",
            "tray_exit",
            "qa_title",
            "copy",
            "untitled_note",
            "note_title",
            "native_question_missing",
            "native_answer_missing",
            "native_audio_missing",
            "native_qa_route_error"
        };

        private static readonly string[] Realtime291Keys =
        {
            "show_realtime_conversation",
            "conversation_hotkey",
            "gemini_realtime_search",
            "gemini_realtime_search_help",
            "grounding_fail_closed",
            "search_sources_title",
            "search_temporary_notice",
            "chat_input_prefix",
            "chat_output_prefix"
        };

        private static readonly string[] RetiredSimpleQaKeys =
        {
            "qa_hotkey",
            "show_qa",
            "tray_show_qa",
            "qa_title",
            "recording_question",
            "clear_qa",
            "clear_qa_tooltip",
            "answering",
            "answer_ready",
            "no_answer",
            "native_question_missing",
            "native_answer_missing",
            "native_audio_missing",
            "native_qa_route_error"
        };

        private static readonly string[] RetiredDesktopUiKeys =
        {
            "conversation_start",
            "conversation_stop",
            "conversation_help",
            "wake_conversation",
            "wake_conversation_help",
            "wake_busy",
            "wake_unavailable",
            "privacy",
            "show_realtime",
            "tray_show_realtime",
            "realtime_translation_hotkey",
            "show_realtime_translation",
            "tray_show_realtime_translation",
            "realtime_translation_title",
            "realtime_translation_tooltip",
            "clear_realtime_translation",
            "clear_realtime_translation_tooltip",
            "realtime_translation_connecting",
            "realtime_translation_active",
            "realtime_translation_stopping",
            "realtime_translation_ended",
            "realtime_translation_failed",
            "translation_input_prefix",
            "translation_output_prefix"
        };

        internal static string[] RequiredKeys
        {
            get
            {
                var visible = new List<string>();
                foreach (var key in Keys)
                {
                    if (IsRetiredDesktopUiKey(key) ||
                        key == "recording_question" ||
                        key == "qa_title" ||
                        key == "native_question_missing" ||
                        key == "native_answer_missing" ||
                        key == "native_audio_missing" ||
                        key == "native_qa_route_error")
                    {
                        continue;
                    }
                    visible.Add(key);
                }
                var extras = new[]
                {
                    "realtime_title",
                    "clear_realtime",
                    "clear_realtime_tooltip"
                };
                visible.AddRange(extras);
                visible.AddRange(Realtime291Keys);
                return visible.ToArray();
            }
        }

        internal static void Apply(Dictionary<string, string> data, string code)
        {
            if (data == null) throw new ArgumentNullException("data");
            var values = Values(code);
            if (values != null)
            {
                if (values.Length != Keys.Length)
                {
                    throw new InvalidOperationException(
                        "Feature localization entry count mismatch for " + code +
                        ": expected " + Keys.Length +
                        ", got " + values.Length + ".");
                }
                for (var i = 0; i < Keys.Length; i++)
                {
                    if (!IsRetiredSimpleQaKey(Keys[i]) &&
                        !IsRetiredDesktopUiKey(Keys[i]))
                    {
                        data[Keys[i]] = values[i];
                    }
                }
            }

            data["tray_show_notes"] = Value(data, "show_notes", "Show Notes");
            data["notes_title"] = Value(data, "quick_notes", "Notes");
            var realtime = RealtimeUiValues(code);
            data["realtime_title"] = realtime[0];
            data["clear_realtime"] = realtime[1];
            data["clear_realtime_tooltip"] = realtime[2];
            var realtime291 = Realtime291UiValues(code);
            if (realtime291.Length != Realtime291Keys.Length)
            {
                throw new InvalidOperationException(
                    "Realtime 291 localization entry count mismatch for " +
                    code + ".");
            }
            for (var index = 0;
                 index < Realtime291Keys.Length;
                 index++)
            {
                data[Realtime291Keys[index]] =
                    realtime291[index];
            }
            data["default_hotkeys"] =
                Value(data, "typing_hotkey", "Typing") +
                ": Right Alt  \u00b7  " +
                Value(data, "notes_title", "Notes") +
                ": Right Alt + Space  \u00b7  " +
                Value(data, "translation_hotkey", "Translation") +
                ": Scroll Lock";
            data["hotkey_help"] =
                "Right Alt \u2192 " +
                Value(data, "typing_hotkey", "Typing") +
                "; Right Alt + Space \u2192 " +
                Value(data, "notes_title", "Notes") +
                "; Scroll Lock \u2192 " +
                Value(data, "translation_hotkey", "Translation");
            foreach (var retiredKey in RetiredSimpleQaKeys)
            {
                data.Remove(retiredKey);
            }
            foreach (var retiredKey in RetiredDesktopUiKeys)
            {
                data.Remove(retiredKey);
            }
        }

        internal static bool IsRetiredSimpleQaKey(string key)
        {
            foreach (var retiredKey in RetiredSimpleQaKeys)
            {
                if (string.Equals(
                        retiredKey,
                        key,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool IsRetiredDesktopUiKey(string key)
        {
            foreach (var retiredKey in RetiredDesktopUiKeys)
            {
                if (string.Equals(
                        retiredKey,
                        key,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static string[] RealtimeUiValues(string code)
        {
            if (code == "zh") return V("实时语音", "清空当前实时语音窗口？", "清空当前实时语音窗口");
            if (code == "zh-TW") return V("即時語音", "清空目前即時語音視窗？", "清空目前即時語音視窗");
            if (code == "ko") return V("실시간 음성", "현재 실시간 음성 창을 지울까요?", "현재 실시간 음성 창 지우기");
            if (code == "fr") return V("Voix en temps réel", "Effacer la fenêtre vocale actuelle ?", "Effacer la fenêtre vocale actuelle");
            if (code == "de") return V("Echtzeit-Sprache", "Aktuelles Echtzeitfenster leeren?", "Aktuelles Echtzeitfenster leeren");
            if (code == "es") return V("Voz en tiempo real", "¿Borrar la ventana de voz actual?", "Borrar la ventana de voz actual");
            if (code == "ar") return V("الصوت المباشر", "مسح نافذة الصوت الحالية؟", "مسح نافذة الصوت الحالية");
            if (code == "ur") return V("ریئل ٹائم آواز", "موجودہ ریئل ٹائم ونڈو صاف کریں؟", "موجودہ ریئل ٹائم ونڈو صاف کریں");
            if (code == "bn") return V("রিয়েলটাইম ভয়েস", "বর্তমান রিয়েলটাইম উইন্ডো মুছবেন?", "বর্তমান রিয়েলটাইম উইন্ডো মুছুন");
            if (code == "fa") return V("صدای زنده", "پنجره زنده فعلی پاک شود؟", "پاک کردن پنجره زنده فعلی");
            if (code == "fil" || code == "tl") return V("Realtime voice", "I-clear ang kasalukuyang realtime window?", "I-clear ang kasalukuyang realtime window");
            if (code == "sw") return V("Sauti ya moja kwa moja", "Futa dirisha la sasa la sauti?", "Futa dirisha la sasa la sauti");
            if (code == "my") return V("တိုက်ရိုက်အသံ", "လက်ရှိ တိုက်ရိုက်အသံဝင်းဒိုးကို ရှင်းမလား?", "လက်ရှိ တိုက်ရိုက်အသံဝင်းဒိုးကို ရှင်းရန်");
            if (code == "am") return V("የቀጥታ ድምፅ", "የአሁኑን የቀጥታ መስኮት ይጥፋ?", "የአሁኑን የቀጥታ መስኮት አጽዳ");
            if (code == "pl") return V("Głos na żywo", "Wyczyścić bieżące okno rozmowy?", "Wyczyść bieżące okno rozmowy");
            if (code == "ms") return V("Suara masa nyata", "Kosongkan tetingkap masa nyata semasa?", "Kosongkan tetingkap masa nyata semasa");
            if (code == "uk") return V("Голос у реальному часі", "Очистити поточне голосове вікно?", "Очистити поточне голосове вікно");
            if (code == "kk") return V("Нақты уақыттағы дауыс", "Ағымдағы дауыс терезесін тазарту керек пе?", "Ағымдағы дауыс терезесін тазарту");
            if (code == "ro") return V("Voce în timp real", "Golești fereastra vocală curentă?", "Golește fereastra vocală curentă");
            if (code == "nl") return V("Realtime spraak", "Huidig spraakvenster wissen?", "Huidig spraakvenster wissen");
            if (code == "hi") return V("रीयलटाइम आवाज़", "मौजूदा रीयलटाइम विंडो साफ़ करें?", "मौजूदा रीयलटाइम विंडो साफ़ करें");
            if (code == "ja") return V("リアルタイム音声", "現在のリアルタイム音声ウィンドウを消去しますか？", "現在のリアルタイム音声ウィンドウを消去");
            if (code == "ru") return V("Голос в реальном времени", "Очистить текущее голосовое окно?", "Очистить текущее голосовое окно");
            if (code == "pt") return V("Voz em tempo real", "Limpar a janela de voz atual?", "Limpar a janela de voz atual");
            if (code == "it") return V("Voce in tempo reale", "Cancellare la finestra vocale corrente?", "Cancella la finestra vocale corrente");
            if (code == "id") return V("Suara realtime", "Hapus jendela suara saat ini?", "Hapus jendela suara saat ini");
            if (code == "th") return V("เสียงแบบเรียลไทม์", "ล้างหน้าต่างเสียงปัจจุบันหรือไม่?", "ล้างหน้าต่างเสียงปัจจุบัน");
            if (code == "vi") return V("Giọng nói thời gian thực", "Xóa cửa sổ giọng nói hiện tại?", "Xóa cửa sổ giọng nói hiện tại");
            if (code == "tr") return V("Gerçek zamanlı ses", "Geçerli ses penceresi temizlensin mi?", "Geçerli ses penceresini temizle");
            return V("Realtime voice", "Clear the current realtime voice window?", "Clear the current realtime voice window");
        }

        private static string[] Realtime291UiValues(string code)
        {
            var search = RealtimeSearchSetting(code);
            return V(
                RealtimeConversationVisibilityLabel(code),
                RealtimeConversationHotkeyLabel(code),
                search[0],
                search[1],
                "\u26a0 " + search[0],
                "Google Search",
                "\u23f3 Google Search",
                "\u2191 ",
                "\u2193 ");
        }

        private static string RealtimeConversationHotkeyLabel(
            string code)
        {
            if (code == "zh") return "实时对话快捷键";
            if (code == "zh-TW") return "即時對話快捷鍵";
            if (code == "ko") return "실시간 대화 단축키";
            if (code == "fr") return "Raccourci de conversation en temps réel";
            if (code == "de") return "Tastenkürzel für Echtzeit-Unterhaltung";
            if (code == "es") return "Atajo de conversación en tiempo real";
            if (code == "ar") return "اختصار المحادثة الفورية";
            if (code == "ur") return "ریئل ٹائم گفتگو کا شارٹ کٹ";
            if (code == "bn") return "রিয়েল-টাইম কথোপকথনের শর্টকাট";
            if (code == "fa") return "میانبر گفت‌وگوی زنده";
            if (code == "fil" || code == "tl") return "Shortcut ng realtime na pag-uusap";
            if (code == "sw") return "Njia ya mkato ya mazungumzo ya moja kwa moja";
            if (code == "my") return "အချိန်နှင့်တပြေးညီ စကားဝိုင်း ဖြတ်လမ်း";
            if (code == "am") return "የቀጥታ ውይይት አቋራጭ";
            if (code == "pl") return "Skrót rozmowy w czasie rzeczywistym";
            if (code == "ms") return "Pintasan perbualan masa nyata";
            if (code == "uk") return "Гаряча клавіша розмови в реальному часі";
            if (code == "kk") return "Нақты уақыттағы сөйлесу пернелер тіркесімі";
            if (code == "ro") return "Scurtătură conversație în timp real";
            if (code == "nl") return "Sneltoets voor realtime gesprek";
            if (code == "hi") return "रीयलटाइम बातचीत शॉर्टकट";
            if (code == "ja") return "リアルタイム会話のショートカット";
            if (code == "ru") return "Горячая клавиша разговора в реальном времени";
            if (code == "pt") return "Atalho da conversa em tempo real";
            if (code == "it") return "Scorciatoia conversazione in tempo reale";
            if (code == "id") return "Pintasan percakapan realtime";
            if (code == "th") return "ปุ่มลัดการสนทนาแบบเรียลไทม์";
            if (code == "vi") return "Phím tắt trò chuyện thời gian thực";
            if (code == "tr") return "Gerçek zamanlı konuşma kısayolu";
            return "Realtime conversation shortcut";
        }

        private static string RealtimeConversationVisibilityLabel(
            string code)
        {
            if (code == "zh") return "显示实时对话窗口";
            if (code == "zh-TW") return "顯示即時對話視窗";
            if (code == "ko") return "실시간 대화 창 표시";
            if (code == "fr") return "Afficher la fenêtre de conversation en temps réel";
            if (code == "de") return "Echtzeit-Unterhaltungsfenster anzeigen";
            if (code == "es") return "Mostrar la ventana de conversación en tiempo real";
            if (code == "ar") return "إظهار نافذة المحادثة الفورية";
            if (code == "ur") return "ریئل ٹائم گفتگو کی ونڈو دکھائیں";
            if (code == "bn") return "রিয়েল-টাইম কথোপকথনের উইন্ডো দেখান";
            if (code == "fa") return "نمایش پنجره گفت‌وگوی زنده";
            if (code == "fil" || code == "tl") return "Ipakita ang realtime na window ng pag-uusap";
            if (code == "sw") return "Onyesha dirisha la mazungumzo ya moja kwa moja";
            if (code == "my") return "အချိန်နှင့်တပြေးညီ စကားဝိုင်းဝင်းဒိုးကို ပြပါ";
            if (code == "am") return "የቀጥታ ውይይት መስኮት አሳይ";
            if (code == "pl") return "Pokaż okno rozmowy w czasie rzeczywistym";
            if (code == "ms") return "Tunjukkan tetingkap perbualan masa nyata";
            if (code == "uk") return "Показувати вікно розмови в реальному часі";
            if (code == "kk") return "Нақты уақыттағы сөйлесу терезесін көрсету";
            if (code == "ro") return "Afișează fereastra conversației în timp real";
            if (code == "nl") return "Realtime gespreksvenster tonen";
            if (code == "hi") return "रीयलटाइम बातचीत विंडो दिखाएँ";
            if (code == "ja") return "リアルタイム会話ウィンドウを表示";
            if (code == "ru") return "Показывать окно разговора в реальном времени";
            if (code == "pt") return "Mostrar janela de conversa em tempo real";
            if (code == "it") return "Mostra la finestra di conversazione in tempo reale";
            if (code == "id") return "Tampilkan jendela percakapan realtime";
            if (code == "th") return "แสดงหน้าต่างสนทนาแบบเรียลไทม์";
            if (code == "vi") return "Hiện cửa sổ trò chuyện thời gian thực";
            if (code == "tr") return "Gerçek zamanlı konuşma penceresini göster";
            return "Show realtime conversation window";
        }

        private static string[] RealtimeSearchSetting(string code)
        {
            if (code == "zh") return V("打开或关闭联网搜索功能", "此功能仅限已启用付费计划的 Gemini API Key。");
            if (code == "zh-TW") return V("開啟或關閉聯網搜尋功能", "此功能僅限已啟用付費方案的 Gemini API Key。");
            if (code == "ko") return V("웹 검색 켜기 또는 끄기", "유료 요금제가 활성화된 Gemini API 키에서만 사용할 수 있습니다.");
            if (code == "fr") return V("Activer ou désactiver la recherche web", "Cette fonction nécessite une clé API Gemini associée à une formule payante.");
            if (code == "de") return V("Websuche ein- oder ausschalten", "Diese Funktion erfordert einen Gemini-API-Schlüssel mit aktiviertem kostenpflichtigem Tarif.");
            if (code == "es") return V("Activar o desactivar la búsqueda web", "Esta función requiere una clave API de Gemini con un plan de pago habilitado.");
            if (code == "ar") return V("تشغيل بحث الويب أو إيقافه", "تتطلب هذه الميزة مفتاح Gemini API بخطة مدفوعة مفعّلة.");
            if (code == "ur") return V("ویب تلاش آن یا آف کریں", "یہ خصوصیت فعال بامعاوضہ پلان والی Gemini API کلید تک محدود ہے۔");
            if (code == "bn") return V("ওয়েব অনুসন্ধান চালু বা বন্ধ করুন", "এই সুবিধার জন্য সক্রিয় পেইড প্ল্যানসহ Gemini API কী প্রয়োজন।");
            if (code == "fa") return V("جست‌وجوی وب را روشن یا خاموش کنید", "این قابلیت فقط با کلید Gemini API دارای طرح پولی فعال قابل استفاده است.");
            if (code == "fil" || code == "tl") return V("I-on o i-off ang web search", "Kailangan ng feature na ito ang Gemini API key na may aktibong bayad na plano.");
            if (code == "sw") return V("Washa au zima utafutaji wa wavuti", "Kipengele hiki kinahitaji ufunguo wa Gemini API wenye mpango wa kulipia uliowashwa.");
            if (code == "my") return V("ဝဘ်ရှာဖွေမှု ဖွင့်ရန် သို့မဟုတ် ပိတ်ရန်", "ဤလုပ်ဆောင်ချက်အတွက် အခပေးအစီအစဉ် ဖွင့်ထားသော Gemini API Key လိုအပ်သည်။");
            if (code == "am") return V("የድር ፍለጋን አብራ ወይም አጥፋ", "ይህ ባህሪ የሚሰራው የክፍያ ዕቅድ በነቃበት Gemini API ቁልፍ ብቻ ነው።");
            if (code == "pl") return V("Włącz lub wyłącz wyszukiwanie w sieci", "Ta funkcja wymaga klucza Gemini API z aktywnym płatnym planem.");
            if (code == "ms") return V("Hidupkan atau matikan carian web", "Ciri ini memerlukan kunci API Gemini dengan pelan berbayar yang diaktifkan.");
            if (code == "uk") return V("Увімкнути або вимкнути вебпошук", "Для цієї функції потрібен ключ Gemini API з активованим платним планом.");
            if (code == "kk") return V("Веб-іздеуді қосу немесе өшіру", "Бұл мүмкіндікке ақылы жоспары қосылған Gemini API кілті қажет.");
            if (code == "ro") return V("Activează sau dezactivează căutarea web", "Această funcție necesită o cheie API Gemini cu un plan plătit activat.");
            if (code == "nl") return V("Webzoeken in- of uitschakelen", "Deze functie vereist een Gemini-API-sleutel met een actief betaald abonnement.");
            if (code == "hi") return V("वेब खोज चालू या बंद करें", "इस सुविधा के लिए सक्रिय सशुल्क योजना वाली Gemini API कुंजी आवश्यक है।");
            if (code == "ja") return V("ウェブ検索をオンまたはオフにする", "この機能には有料プランが有効な Gemini API キーが必要です。");
            if (code == "ru") return V("Включить или выключить веб-поиск", "Для этой функции нужен ключ Gemini API с активированным платным тарифом.");
            if (code == "pt") return V("Ativar ou desativar a pesquisa na web", "Este recurso requer uma chave da API Gemini com um plano pago ativado.");
            if (code == "it") return V("Attiva o disattiva la ricerca web", "Questa funzione richiede una chiave API Gemini con un piano a pagamento attivo.");
            if (code == "id") return V("Aktifkan atau nonaktifkan penelusuran web", "Fitur ini memerlukan kunci API Gemini dengan paket berbayar yang diaktifkan.");
            if (code == "th") return V("เปิดหรือปิดการค้นหาเว็บ", "ฟีเจอร์นี้ต้องใช้คีย์ Gemini API ที่เปิดใช้แพ็กเกจแบบชำระเงินแล้ว");
            if (code == "vi") return V("Bật hoặc tắt tìm kiếm trên web", "Tính năng này yêu cầu khóa API Gemini đã bật gói trả phí.");
            if (code == "tr") return V("Web aramasını aç veya kapat", "Bu özellik, ücretli planı etkinleştirilmiş bir Gemini API anahtarı gerektirir.");
            return V("Turn web search on or off", "This feature requires a Gemini API key with an enabled paid plan.");
        }

        private static string Value(Dictionary<string, string> data, string key, string fallback)
        {
            string value;
            return data.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;
        }

        private static string[] Values(string code)
        {
            if (code == "zh")
            {
                return V(
                    "请先结束当前语音任务，再开始实时对话。", "正在连接实时语音对话", "实时语音对话进行中",
                    "检测到安静，正在结束实时语音对话", "实时语音对话已结束", "实时语音对话失败",
                    "正在结束实时语音对话", "正在连接实时会话", "再次按快捷键即可停止",
                    "正在录制翻译", "正在录制笔记", "正在录制问题", "正在录制对话", "正在录音",
                    "打开设置", "开始/停止语音输入", "开始/停止实时语音对话", "退出",
                    "问答", "复制", "无标题笔记", "笔记",
                    "当前实时服务未返回语音问题的文字记录。", "当前实时服务未返回答案文字。",
                    "当前实时服务未返回原生答案语音。", "简单问答必须使用当前服务的原生实时语音通道。");
            }
            if (code == "zh-TW")
            {
                return V(
                    "請先結束目前的語音工作，再開始即時對話。", "正在連線即時語音對話", "即時語音對話進行中",
                    "偵測到安靜，正在結束即時語音對話", "即時語音對話已結束", "即時語音對話失敗",
                    "正在結束即時語音對話", "正在連線即時工作階段", "再次按快捷鍵即可停止",
                    "正在錄製翻譯", "正在錄製筆記", "正在錄製問題", "正在錄製對話", "正在錄音",
                    "開啟設定", "開始/停止語音輸入", "開始/停止即時語音對話", "結束",
                    "問答", "複製", "未命名筆記", "筆記",
                    "目前的即時服務未傳回語音問題的文字記錄。", "目前的即時服務未傳回答案文字。",
                    "目前的即時服務未傳回原生答案語音。", "簡單問答必須使用目前服務的原生即時語音通道。");
            }
            if (code == "ko")
            {
                return V(
                    "실시간 대화를 시작하기 전에 현재 음성 작업을 끝내세요.", "실시간 음성 대화 연결 중", "실시간 음성 대화 실행 중",
                    "정적이 감지되어 실시간 음성 대화를 종료합니다", "실시간 음성 대화가 종료됨", "실시간 음성 대화 실패",
                    "실시간 음성 대화 종료 중", "실시간 세션 연결 중", "중지하려면 단축키를 다시 누르세요",
                    "번역 녹음 중", "메모 녹음 중", "질문 녹음 중", "대화 녹음 중", "녹음 중",
                    "설정 열기", "음성 입력 시작/중지", "실시간 음성 대화 시작/중지", "종료",
                    "Q&A", "복사", "제목 없는 메모", "메모",
                    "선택한 실시간 제공자가 말한 질문의 전사를 반환하지 않았습니다.", "선택한 실시간 제공자가 답변 전사를 반환하지 않았습니다.",
                    "선택한 실시간 제공자가 기본 답변 오디오를 반환하지 않았습니다.", "간단한 Q&A는 선택한 제공자의 기본 실시간 오디오 경로를 사용해야 합니다.");
            }
            if (code == "fr")
            {
                return V(
                    "Terminez la tâche vocale actuelle avant de démarrer la conversation.", "Connexion à la conversation en temps réel", "Conversation en temps réel active",
                    "Silence détecté ; arrêt de la conversation", "Conversation en temps réel terminée", "Échec de la conversation en temps réel",
                    "Arrêt de la conversation en temps réel", "Connexion à la session en temps réel", "Appuyez de nouveau sur le raccourci pour arrêter",
                    "Enregistrement de la traduction", "Enregistrement de la note", "Enregistrement de la question", "Enregistrement de la conversation", "Enregistrement",
                    "Ouvrir les paramètres", "Démarrer/arrêter la saisie vocale", "Démarrer/arrêter la conversation", "Quitter",
                    "Q&R", "Copier", "Note sans titre", "Note",
                    "Le fournisseur temps réel sélectionné n’a pas renvoyé la transcription de la question.", "Le fournisseur temps réel sélectionné n’a pas renvoyé la transcription de la réponse.",
                    "Le fournisseur temps réel sélectionné n’a pas renvoyé l’audio natif de la réponse.", "Le Q&R simple doit utiliser le canal audio temps réel natif du fournisseur sélectionné.");
            }
            if (code == "de")
            {
                return V(
                    "Beenden Sie die aktuelle Sprachaufgabe, bevor Sie das Echtzeitgespräch starten.", "Echtzeit-Sprachgespräch wird verbunden", "Echtzeit-Sprachgespräch ist aktiv",
                    "Ruhe erkannt; Echtzeitgespräch wird beendet", "Echtzeit-Sprachgespräch beendet", "Echtzeit-Sprachgespräch fehlgeschlagen",
                    "Echtzeit-Sprachgespräch wird beendet", "Echtzeitsitzung wird verbunden", "Zum Beenden das Tastenkürzel erneut drücken",
                    "Übersetzung wird aufgenommen", "Notiz wird aufgenommen", "Frage wird aufgenommen", "Gespräch wird aufgenommen", "Aufnahme läuft",
                    "Einstellungen öffnen", "Spracheingabe starten/stoppen", "Echtzeitgespräch starten/stoppen", "Beenden",
                    "F&A", "Kopieren", "Unbenannte Notiz", "Notiz",
                    "Der gewählte Echtzeitanbieter hat keine Transkription der gesprochenen Frage geliefert.", "Der gewählte Echtzeitanbieter hat keine Antworttranskription geliefert.",
                    "Der gewählte Echtzeitanbieter hat kein natives Antwortaudio geliefert.", "Einfache F&A muss den nativen Echtzeit-Audiokanal des gewählten Anbieters verwenden.");
            }
            if (code == "es")
            {
                return V(
                    "Finalice la tarea de voz actual antes de iniciar la conversación.", "Conectando la conversación en tiempo real", "Conversación en tiempo real activa",
                    "Silencio detectado; finalizando la conversación", "Conversación en tiempo real finalizada", "Falló la conversación en tiempo real",
                    "Finalizando la conversación en tiempo real", "Conectando la sesión en tiempo real", "Pulse de nuevo el atajo para detener",
                    "Grabando traducción", "Grabando nota", "Grabando pregunta", "Grabando conversación", "Grabando",
                    "Abrir configuración", "Iniciar/detener escritura por voz", "Iniciar/detener conversación", "Salir",
                    "Preguntas y respuestas", "Copiar", "Nota sin título", "Nota",
                    "El proveedor en tiempo real seleccionado no devolvió la transcripción de la pregunta hablada.", "El proveedor en tiempo real seleccionado no devolvió la transcripción de la respuesta.",
                    "El proveedor en tiempo real seleccionado no devolvió el audio nativo de la respuesta.", "Preguntas y respuestas debe usar la ruta de audio nativa en tiempo real del proveedor seleccionado.");
            }
            if (code == "ar")
            {
                return V(
                    "أنه المهمة الصوتية الحالية قبل بدء المحادثة الفورية.", "جارٍ الاتصال بالمحادثة الفورية", "المحادثة الفورية نشطة",
                    "تم اكتشاف الهدوء؛ جارٍ إنهاء المحادثة", "انتهت المحادثة الفورية", "فشلت المحادثة الفورية",
                    "جارٍ إنهاء المحادثة الفورية", "جارٍ الاتصال بالجلسة الفورية", "اضغط الاختصار مرة أخرى للإيقاف",
                    "جارٍ تسجيل الترجمة", "جارٍ تسجيل الملاحظة", "جارٍ تسجيل السؤال", "جارٍ تسجيل المحادثة", "جارٍ التسجيل",
                    "فتح الإعدادات", "بدء/إيقاف الكتابة الصوتية", "بدء/إيقاف المحادثة الفورية", "خروج",
                    "سؤال وجواب", "نسخ", "ملاحظة بلا عنوان", "ملاحظة",
                    "لم يُرجع مزود الوقت الفعلي المحدد نص السؤال المنطوق.", "لم يُرجع مزود الوقت الفعلي المحدد نص الإجابة.",
                    "لم يُرجع مزود الوقت الفعلي المحدد صوت الإجابة الأصلي.", "يجب أن تستخدم الأسئلة والأجوبة البسيطة مسار الصوت الفوري الأصلي للمزود المحدد.");
            }
            if (code == "ur")
            {
                return V(
                    "ریئل ٹائم گفتگو سے پہلے موجودہ صوتی کام ختم کریں۔", "ریئل ٹائم گفتگو سے رابطہ ہو رہا ہے", "ریئل ٹائم گفتگو جاری ہے",
                    "خاموشی ملی؛ گفتگو ختم کی جا رہی ہے", "ریئل ٹائم گفتگو ختم ہو گئی", "ریئل ٹائم گفتگو ناکام",
                    "ریئل ٹائم گفتگو ختم کی جا رہی ہے", "ریئل ٹائم سیشن سے رابطہ ہو رہا ہے", "بند کرنے کے لیے شارٹ کٹ دوبارہ دبائیں",
                    "ترجمہ ریکارڈ ہو رہا ہے", "نوٹ ریکارڈ ہو رہا ہے", "سوال ریکارڈ ہو رہا ہے", "گفتگو ریکارڈ ہو رہی ہے", "ریکارڈنگ جاری ہے",
                    "ترتیبات کھولیں", "صوتی ٹائپنگ شروع/بند کریں", "ریئل ٹائم گفتگو شروع/بند کریں", "باہر نکلیں",
                    "سوال و جواب", "کاپی", "بلا عنوان نوٹ", "نوٹ",
                    "منتخب ریئل ٹائم فراہم کنندہ نے بولے گئے سوال کا متن واپس نہیں کیا۔", "منتخب ریئل ٹائم فراہم کنندہ نے جواب کا متن واپس نہیں کیا۔",
                    "منتخب ریئل ٹائم فراہم کنندہ نے اصل جواب کی آڈیو واپس نہیں کی۔", "سادہ سوال جواب کے لیے منتخب فراہم کنندہ کا اصل ریئل ٹائم آڈیو راستہ استعمال ہونا چاہیے۔");
            }
            if (code == "bn")
            {
                return V(
                    "রিয়েল-টাইম কথোপকথন শুরুর আগে বর্তমান ভয়েস কাজ শেষ করুন।", "রিয়েল-টাইম কথোপকথনে সংযোগ হচ্ছে", "রিয়েল-টাইম কথোপকথন সক্রিয়",
                    "নীরবতা শনাক্ত হয়েছে; কথোপকথন শেষ হচ্ছে", "রিয়েল-টাইম কথোপকথন শেষ", "রিয়েল-টাইম কথোপকথন ব্যর্থ",
                    "রিয়েল-টাইম কথোপকথন শেষ হচ্ছে", "রিয়েল-টাইম সেশনে সংযোগ হচ্ছে", "বন্ধ করতে শর্টকাট আবার চাপুন",
                    "অনুবাদ রেকর্ড হচ্ছে", "নোট রেকর্ড হচ্ছে", "প্রশ্ন রেকর্ড হচ্ছে", "কথোপকথন রেকর্ড হচ্ছে", "রেকর্ড হচ্ছে",
                    "সেটিংস খুলুন", "ভয়েস টাইপিং শুরু/বন্ধ", "রিয়েল-টাইম কথোপকথন শুরু/বন্ধ", "প্রস্থান",
                    "প্রশ্নোত্তর", "কপি", "শিরোনামহীন নোট", "নোট",
                    "নির্বাচিত রিয়েল-টাইম প্রদানকারী কথিত প্রশ্নের প্রতিলিপি দেয়নি।", "নির্বাচিত রিয়েল-টাইম প্রদানকারী উত্তরের প্রতিলিপি দেয়নি।",
                    "নির্বাচিত রিয়েল-টাইম প্রদানকারী উত্তরের নেটিভ অডিও দেয়নি।", "সহজ প্রশ্নোত্তরে নির্বাচিত প্রদানকারীর নেটিভ রিয়েল-টাইম অডিও পথ ব্যবহার করতে হবে।");
            }
            if (code == "fa")
            {
                return V(
                    "پیش از شروع گفت‌وگوی زنده، کار صوتی فعلی را تمام کنید.", "در حال اتصال گفت‌وگوی زنده", "گفت‌وگوی زنده فعال است",
                    "سکوت تشخیص داده شد؛ گفت‌وگو در حال پایان است", "گفت‌وگوی زنده پایان یافت", "گفت‌وگوی زنده ناموفق بود",
                    "در حال پایان گفت‌وگوی زنده", "در حال اتصال نشست زنده", "برای توقف دوباره میانبر را فشار دهید",
                    "در حال ضبط ترجمه", "در حال ضبط یادداشت", "در حال ضبط پرسش", "در حال ضبط گفت‌وگو", "در حال ضبط",
                    "بازکردن تنظیمات", "شروع/توقف تایپ صوتی", "شروع/توقف گفت‌وگوی زنده", "خروج",
                    "پرسش و پاسخ", "کپی", "یادداشت بدون عنوان", "یادداشت",
                    "ارائه‌دهنده زنده انتخاب‌شده رونویسی پرسش گفتاری را برنگرداند.", "ارائه‌دهنده زنده انتخاب‌شده رونویسی پاسخ را برنگرداند.",
                    "ارائه‌دهنده زنده انتخاب‌شده صدای بومی پاسخ را برنگرداند.", "پرسش‌وپاسخ ساده باید از مسیر صوتی زنده بومی ارائه‌دهنده انتخاب‌شده استفاده کند.");
            }
            if (code == "fil" || code == "tl")
            {
                return V(
                    "Tapusin muna ang kasalukuyang voice task bago simulan ang realtime na usapan.", "Kumokonekta sa realtime na usapan", "Aktibo ang realtime na usapan",
                    "May katahimikang natukoy; tinatapos ang usapan", "Natapos ang realtime na usapan", "Nabigo ang realtime na usapan",
                    "Tinatapos ang realtime na usapan", "Kumokonekta sa realtime session", "Pindutin muli ang shortcut para huminto",
                    "Nire-record ang pagsasalin", "Nire-record ang note", "Nire-record ang tanong", "Nire-record ang usapan", "Nire-record",
                    "Buksan ang settings", "Simulan/ihinto ang voice typing", "Simulan/ihinto ang realtime na usapan", "Lumabas",
                    "Q&A", "Kopyahin", "Note na walang pamagat", "Note",
                    "Hindi ibinalik ng napiling realtime provider ang transcription ng binigkas na tanong.", "Hindi ibinalik ng napiling realtime provider ang transcription ng sagot.",
                    "Hindi ibinalik ng napiling realtime provider ang native na audio ng sagot.", "Dapat gamitin ng simpleng Q&A ang native realtime audio route ng napiling provider.");
            }
            if (code == "sw")
            {
                return V(
                    "Maliza jukumu la sasa la sauti kabla ya kuanzisha mazungumzo.", "Inaunganisha mazungumzo ya wakati halisi", "Mazungumzo ya wakati halisi yanaendelea",
                    "Ukimya umegunduliwa; mazungumzo yanamalizwa", "Mazungumzo ya wakati halisi yameisha", "Mazungumzo ya wakati halisi yameshindwa",
                    "Inamaliza mazungumzo ya wakati halisi", "Inaunganisha kipindi cha wakati halisi", "Bonyeza njia ya mkato tena kusimamisha",
                    "Inarekodi tafsiri", "Inarekodi dokezo", "Inarekodi swali", "Inarekodi mazungumzo", "Inarekodi",
                    "Fungua mipangilio", "Anza/simamisha kuandika kwa sauti", "Anza/simamisha mazungumzo", "Toka",
                    "Maswali na Majibu", "Nakili", "Dokezo lisilo na kichwa", "Dokezo",
                    "Mtoa huduma wa wakati halisi hakurudisha maandishi ya swali lililosemwa.", "Mtoa huduma wa wakati halisi hakurudisha maandishi ya jibu.",
                    "Mtoa huduma wa wakati halisi hakurudisha sauti asili ya jibu.", "Maswali na Majibu rahisi lazima yatumie njia asili ya sauti ya wakati halisi ya mtoa huduma aliyechaguliwa.");
            }
            if (code == "my")
            {
                return V(
                    "တိုက်ရိုက်စကားပြောမှု မစတင်မီ လက်ရှိအသံလုပ်ငန်းကို ပြီးဆုံးပါ။", "တိုက်ရိုက်စကားပြောမှု ချိတ်ဆက်နေသည်", "တိုက်ရိုက်စကားပြောမှု လုပ်ဆောင်နေသည်",
                    "ငြိမ်သက်မှု တွေ့ရှိ၍ စကားပြောမှု ရပ်နေသည်", "တိုက်ရိုက်စကားပြောမှု ပြီးဆုံးပြီ", "တိုက်ရိုက်စကားပြောမှု မအောင်မြင်ပါ",
                    "တိုက်ရိုက်စကားပြောမှု ရပ်နေသည်", "တိုက်ရိုက်ဆက်ရှင် ချိတ်ဆက်နေသည်", "ရပ်ရန် ဖြတ်လမ်းကို ထပ်နှိပ်ပါ",
                    "ဘာသာပြန် အသံသွင်းနေသည်", "မှတ်စု အသံသွင်းနေသည်", "မေးခွန်း အသံသွင်းနေသည်", "စကားပြောမှု အသံသွင်းနေသည်", "အသံသွင်းနေသည်",
                    "ဆက်တင် ဖွင့်ရန်", "အသံဖြင့်စာရိုက်ခြင်း စ/ရပ်", "တိုက်ရိုက်စကားပြောမှု စ/ရပ်", "ထွက်ရန်",
                    "အမေးအဖြေ", "ကူးယူရန်", "ခေါင်းစဉ်မရှိ မှတ်စု", "မှတ်စု",
                    "ရွေးချယ်ထားသော တိုက်ရိုက်ဝန်ဆောင်မှုက ပြောထားသောမေးခွန်းစာသားကို မပေးပါ။", "ရွေးချယ်ထားသော တိုက်ရိုက်ဝန်ဆောင်မှုက အဖြေစာသားကို မပေးပါ။",
                    "ရွေးချယ်ထားသော တိုက်ရိုက်ဝန်ဆောင်မှုက မူရင်းအဖြေအသံကို မပေးပါ။", "ရိုးရှင်းသော အမေးအဖြေသည် ရွေးချယ်ထားသောဝန်ဆောင်မှု၏ မူရင်းတိုက်ရိုက်အသံလမ်းကြောင်းကို သုံးရမည်။");
            }
            if (code == "am")
            {
                return V(
                    "የቀጥታ ውይይት ከመጀመርዎ በፊት የአሁኑን የድምፅ ሥራ ይጨርሱ።", "የቀጥታ ውይይት በመገናኘት ላይ", "የቀጥታ ውይይት ንቁ ነው",
                    "ጸጥታ ተገኝቷል፤ ውይይቱ እየተቋረጠ ነው", "የቀጥታ ውይይት ተጠናቋል", "የቀጥታ ውይይት አልተሳካም",
                    "የቀጥታ ውይይት እየተቋረጠ ነው", "የቀጥታ ክፍለ ጊዜ በመገናኘት ላይ", "ለማቆም አቋራጩን እንደገና ይጫኑ",
                    "ትርጉም በመቅዳት ላይ", "ማስታወሻ በመቅዳት ላይ", "ጥያቄ በመቅዳት ላይ", "ውይይት በመቅዳት ላይ", "በመቅዳት ላይ",
                    "ቅንብሮችን ክፈት", "የድምፅ ጽሑፍ ጀምር/አቁም", "የቀጥታ ውይይት ጀምር/አቁም", "ውጣ",
                    "ጥያቄና መልስ", "ቅዳ", "ርዕስ የሌለው ማስታወሻ", "ማስታወሻ",
                    "የተመረጠው የቀጥታ አቅራቢ የተነገረውን ጥያቄ ጽሑፍ አልመለሰም።", "የተመረጠው የቀጥታ አቅራቢ የመልሱን ጽሑፍ አልመለሰም።",
                    "የተመረጠው የቀጥታ አቅራቢ የመልሱን ድምፅ አልመለሰም።", "ቀላል ጥያቄና መልስ የተመረጠውን አቅራቢ ተፈጥሯዊ የቀጥታ ድምፅ መንገድ መጠቀም አለበት።");
            }
            if (code == "pl")
            {
                return V(
                    "Zakończ bieżące zadanie głosowe przed rozpoczęciem rozmowy.", "Łączenie z rozmową na żywo", "Rozmowa na żywo jest aktywna",
                    "Wykryto ciszę; kończenie rozmowy", "Rozmowa na żywo zakończona", "Rozmowa na żywo nie powiodła się",
                    "Kończenie rozmowy na żywo", "Łączenie z sesją na żywo", "Naciśnij skrót ponownie, aby zatrzymać",
                    "Nagrywanie tłumaczenia", "Nagrywanie notatki", "Nagrywanie pytania", "Nagrywanie rozmowy", "Nagrywanie",
                    "Otwórz ustawienia", "Uruchom/zatrzymaj pisanie głosowe", "Uruchom/zatrzymaj rozmowę na żywo", "Zakończ",
                    "Pytania i odpowiedzi", "Kopiuj", "Notatka bez tytułu", "Notatka",
                    "Wybrany dostawca czasu rzeczywistego nie zwrócił transkrypcji wypowiedzianego pytania.", "Wybrany dostawca czasu rzeczywistego nie zwrócił transkrypcji odpowiedzi.",
                    "Wybrany dostawca czasu rzeczywistego nie zwrócił natywnego dźwięku odpowiedzi.", "Proste pytania i odpowiedzi muszą używać natywnej ścieżki audio czasu rzeczywistego wybranego dostawcy.");
            }
            if (code == "ms")
            {
                return V(
                    "Selesaikan tugas suara semasa sebelum memulakan perbualan.", "Menyambung perbualan masa nyata", "Perbualan masa nyata aktif",
                    "Senyap dikesan; menamatkan perbualan", "Perbualan masa nyata tamat", "Perbualan masa nyata gagal",
                    "Menamatkan perbualan masa nyata", "Menyambung sesi masa nyata", "Tekan pintasan sekali lagi untuk berhenti",
                    "Merakam terjemahan", "Merakam nota", "Merakam soalan", "Merakam perbualan", "Merakam",
                    "Buka tetapan", "Mula/henti menaip suara", "Mula/henti perbualan masa nyata", "Keluar",
                    "Soal Jawab", "Salin", "Nota tanpa tajuk", "Nota",
                    "Penyedia masa nyata yang dipilih tidak mengembalikan transkripsi soalan lisan.", "Penyedia masa nyata yang dipilih tidak mengembalikan transkripsi jawapan.",
                    "Penyedia masa nyata yang dipilih tidak mengembalikan audio jawapan asli.", "Soal Jawab ringkas mesti menggunakan laluan audio masa nyata asli penyedia yang dipilih.");
            }
            if (code == "uk")
            {
                return V(
                    "Завершіть поточне голосове завдання перед початком розмови.", "Підключення до розмови наживо", "Розмова наживо активна",
                    "Виявлено тишу; розмова завершується", "Розмову наживо завершено", "Помилка розмови наживо",
                    "Завершення розмови наживо", "Підключення до сеансу наживо", "Натисніть сполучення ще раз, щоб зупинити",
                    "Запис перекладу", "Запис нотатки", "Запис запитання", "Запис розмови", "Запис",
                    "Відкрити налаштування", "Почати/зупинити голосове введення", "Почати/зупинити розмову наживо", "Вийти",
                    "Запитання й відповіді", "Копіювати", "Нотатка без назви", "Нотатка",
                    "Вибраний постачальник у реальному часі не повернув транскрипцію усного запитання.", "Вибраний постачальник у реальному часі не повернув транскрипцію відповіді.",
                    "Вибраний постачальник у реальному часі не повернув оригінальне аудіо відповіді.", "Прості запитання й відповіді мають використовувати оригінальний аудіоканал реального часу вибраного постачальника.");
            }
            if (code == "kk")
            {
                return V(
                    "Нақты уақыттағы сұхбатты бастамас бұрын ағымдағы дауыстық тапсырманы аяқтаңыз.", "Нақты уақыттағы сұхбатқа қосылуда", "Нақты уақыттағы сұхбат белсенді",
                    "Тыныштық анықталды; сұхбат аяқталуда", "Нақты уақыттағы сұхбат аяқталды", "Нақты уақыттағы сұхбат сәтсіз",
                    "Нақты уақыттағы сұхбат аяқталуда", "Нақты уақыттағы сеансқа қосылуда", "Тоқтату үшін пернені қайта басыңыз",
                    "Аударма жазылуда", "Жазба жазылуда", "Сұрақ жазылуда", "Сұхбат жазылуда", "Жазылуда",
                    "Параметрлерді ашу", "Дауыспен теруді бастау/тоқтату", "Нақты уақыттағы сұхбатты бастау/тоқтату", "Шығу",
                    "Сұрақ-жауап", "Көшіру", "Атаусыз жазба", "Жазба",
                    "Таңдалған нақты уақыт қызметі айтылған сұрақтың мәтінін қайтармады.", "Таңдалған нақты уақыт қызметі жауап мәтінін қайтармады.",
                    "Таңдалған нақты уақыт қызметі жауаптың төл дыбысын қайтармады.", "Қарапайым сұрақ-жауап таңдалған қызметтің төл нақты уақыт аудио жолын қолдануы керек.");
            }
            if (code == "ro")
            {
                return V(
                    "Încheiați activitatea vocală curentă înainte de conversația în timp real.", "Se conectează conversația în timp real", "Conversația în timp real este activă",
                    "Liniște detectată; conversația se încheie", "Conversația în timp real s-a încheiat", "Conversația în timp real a eșuat",
                    "Se încheie conversația în timp real", "Se conectează sesiunea în timp real", "Apăsați din nou scurtătura pentru oprire",
                    "Se înregistrează traducerea", "Se înregistrează notița", "Se înregistrează întrebarea", "Se înregistrează conversația", "Se înregistrează",
                    "Deschide setările", "Pornește/oprește tastarea vocală", "Pornește/oprește conversația", "Ieșire",
                    "Întrebări și răspunsuri", "Copiază", "Notiță fără titlu", "Notiță",
                    "Furnizorul în timp real selectat nu a returnat transcrierea întrebării rostite.", "Furnizorul în timp real selectat nu a returnat transcrierea răspunsului.",
                    "Furnizorul în timp real selectat nu a returnat sunetul nativ al răspunsului.", "Întrebările și răspunsurile trebuie să folosească traseul audio nativ în timp real al furnizorului selectat.");
            }
            if (code == "nl")
            {
                return V(
                    "Rond de huidige spraaktaak af voordat u het realtime gesprek start.", "Verbinding maken met realtime gesprek", "Realtime gesprek is actief",
                    "Stilte gedetecteerd; gesprek wordt beëindigd", "Realtime gesprek beëindigd", "Realtime gesprek mislukt",
                    "Realtime gesprek wordt beëindigd", "Verbinding maken met realtime sessie", "Druk nogmaals op de sneltoets om te stoppen",
                    "Vertaling opnemen", "Notitie opnemen", "Vraag opnemen", "Gesprek opnemen", "Opnemen",
                    "Instellingen openen", "Spraaktypen starten/stoppen", "Realtime gesprek starten/stoppen", "Afsluiten",
                    "Vraag en antwoord", "Kopiëren", "Naamloze notitie", "Notitie",
                    "De gekozen realtimeprovider heeft geen transcriptie van de gesproken vraag teruggestuurd.", "De gekozen realtimeprovider heeft geen transcriptie van het antwoord teruggestuurd.",
                    "De gekozen realtimeprovider heeft geen oorspronkelijke antwoordaudio teruggestuurd.", "Eenvoudige vraag en antwoord moet de oorspronkelijke realtime-audioroute van de gekozen provider gebruiken.");
            }
            if (code == "hi")
            {
                return V(
                    "रीयल-टाइम बातचीत शुरू करने से पहले मौजूदा वॉइस कार्य पूरा करें।", "रीयल-टाइम बातचीत से जुड़ रहा है", "रीयल-टाइम बातचीत सक्रिय है",
                    "शांति मिली; बातचीत समाप्त हो रही है", "रीयल-टाइम बातचीत समाप्त हुई", "रीयल-टाइम बातचीत विफल",
                    "रीयल-टाइम बातचीत समाप्त हो रही है", "रीयल-टाइम सत्र से जुड़ रहा है", "रोकने के लिए शॉर्टकट फिर दबाएं",
                    "अनुवाद रिकॉर्ड हो रहा है", "नोट रिकॉर्ड हो रहा है", "सवाल रिकॉर्ड हो रहा है", "बातचीत रिकॉर्ड हो रही है", "रिकॉर्ड हो रहा है",
                    "सेटिंग खोलें", "वॉइस टाइपिंग शुरू/रोकें", "रीयल-टाइम बातचीत शुरू/रोकें", "बाहर निकलें",
                    "सवाल-जवाब", "कॉपी", "बिना शीर्षक का नोट", "नोट",
                    "चुने गए रीयल-टाइम प्रदाता ने बोले गए सवाल का प्रतिलेख नहीं लौटाया।", "चुने गए रीयल-टाइम प्रदाता ने जवाब का प्रतिलेख नहीं लौटाया।",
                    "चुने गए रीयल-टाइम प्रदाता ने मूल जवाब ऑडियो नहीं लौटाया।", "सरल सवाल-जवाब को चुने गए प्रदाता का मूल रीयल-टाइम ऑडियो मार्ग उपयोग करना होगा।");
            }
            if (code == "ja")
            {
                return V(
                    "リアルタイム会話を始める前に、現在の音声タスクを終了してください。", "リアルタイム音声会話に接続中", "リアルタイム音声会話中",
                    "無音を検出したため、会話を終了しています", "リアルタイム音声会話を終了しました", "リアルタイム音声会話に失敗しました",
                    "リアルタイム音声会話を終了中", "リアルタイムセッションに接続中", "停止するにはショートカットをもう一度押してください",
                    "翻訳を録音中", "メモを録音中", "質問を録音中", "会話を録音中", "録音中",
                    "設定を開く", "音声入力を開始/停止", "リアルタイム会話を開始/停止", "終了",
                    "Q&A", "コピー", "無題のメモ", "メモ",
                    "選択したリアルタイムプロバイダーから音声質問の文字起こしが返されませんでした。", "選択したリアルタイムプロバイダーから回答の文字起こしが返されませんでした。",
                    "選択したリアルタイムプロバイダーからネイティブ回答音声が返されませんでした。", "簡単 Q&A では、選択したプロバイダーのネイティブなリアルタイム音声経路を使用する必要があります。");
            }
            if (code == "ru")
            {
                return V(
                    "Завершите текущую голосовую задачу перед началом разговора.", "Подключение к разговору в реальном времени", "Разговор в реальном времени активен",
                    "Обнаружена тишина; разговор завершается", "Разговор в реальном времени завершён", "Сбой разговора в реальном времени",
                    "Завершение разговора в реальном времени", "Подключение к сеансу в реальном времени", "Нажмите сочетание ещё раз для остановки",
                    "Запись перевода", "Запись заметки", "Запись вопроса", "Запись разговора", "Запись",
                    "Открыть настройки", "Начать/остановить голосовой ввод", "Начать/остановить разговор", "Выход",
                    "Вопросы и ответы", "Копировать", "Заметка без названия", "Заметка",
                    "Выбранный поставщик реального времени не вернул расшифровку произнесённого вопроса.", "Выбранный поставщик реального времени не вернул расшифровку ответа.",
                    "Выбранный поставщик реального времени не вернул исходное аудио ответа.", "Простые вопросы и ответы должны использовать исходный аудиоканал реального времени выбранного поставщика.");
            }
            if (code == "pt")
            {
                return V(
                    "Conclua a tarefa de voz atual antes de iniciar a conversa.", "Conectando à conversa em tempo real", "Conversa em tempo real ativa",
                    "Silêncio detectado; encerrando a conversa", "Conversa em tempo real encerrada", "Falha na conversa em tempo real",
                    "Encerrando a conversa em tempo real", "Conectando à sessão em tempo real", "Pressione o atalho novamente para parar",
                    "Gravando tradução", "Gravando nota", "Gravando pergunta", "Gravando conversa", "Gravando",
                    "Abrir configurações", "Iniciar/parar digitação por voz", "Iniciar/parar conversa em tempo real", "Sair",
                    "Perguntas e respostas", "Copiar", "Nota sem título", "Nota",
                    "O provedor em tempo real selecionado não retornou a transcrição da pergunta falada.", "O provedor em tempo real selecionado não retornou a transcrição da resposta.",
                    "O provedor em tempo real selecionado não retornou o áudio nativo da resposta.", "Perguntas e respostas deve usar a rota de áudio nativa em tempo real do provedor selecionado.");
            }
            if (code == "it")
            {
                return V(
                    "Completa l’attività vocale corrente prima di avviare la conversazione.", "Connessione alla conversazione in tempo reale", "Conversazione in tempo reale attiva",
                    "Silenzio rilevato; conversazione in chiusura", "Conversazione in tempo reale terminata", "Conversazione in tempo reale non riuscita",
                    "Chiusura della conversazione in tempo reale", "Connessione alla sessione in tempo reale", "Premi di nuovo la scorciatoia per interrompere",
                    "Registrazione traduzione", "Registrazione nota", "Registrazione domanda", "Registrazione conversazione", "Registrazione",
                    "Apri impostazioni", "Avvia/interrompi digitazione vocale", "Avvia/interrompi conversazione", "Esci",
                    "Domande e risposte", "Copia", "Nota senza titolo", "Nota",
                    "Il provider in tempo reale selezionato non ha restituito la trascrizione della domanda pronunciata.", "Il provider in tempo reale selezionato non ha restituito la trascrizione della risposta.",
                    "Il provider in tempo reale selezionato non ha restituito l’audio nativo della risposta.", "Domande e risposte deve usare il percorso audio nativo in tempo reale del provider selezionato.");
            }
            if (code == "id")
            {
                return V(
                    "Selesaikan tugas suara saat ini sebelum memulai percakapan.", "Menghubungkan percakapan realtime", "Percakapan realtime aktif",
                    "Hening terdeteksi; mengakhiri percakapan", "Percakapan realtime berakhir", "Percakapan realtime gagal",
                    "Mengakhiri percakapan realtime", "Menghubungkan sesi realtime", "Tekan pintasan lagi untuk berhenti",
                    "Merekam terjemahan", "Merekam catatan", "Merekam pertanyaan", "Merekam percakapan", "Merekam",
                    "Buka pengaturan", "Mulai/hentikan pengetikan suara", "Mulai/hentikan percakapan realtime", "Keluar",
                    "Tanya Jawab", "Salin", "Catatan tanpa judul", "Catatan",
                    "Penyedia realtime yang dipilih tidak mengembalikan transkripsi pertanyaan lisan.", "Penyedia realtime yang dipilih tidak mengembalikan transkripsi jawaban.",
                    "Penyedia realtime yang dipilih tidak mengembalikan audio jawaban asli.", "Tanya Jawab sederhana harus memakai jalur audio realtime asli dari penyedia yang dipilih.");
            }
            if (code == "th")
            {
                return V(
                    "โปรดจบงานเสียงปัจจุบันก่อนเริ่มการสนทนา", "กำลังเชื่อมต่อการสนทนาแบบเรียลไทม์", "การสนทนาแบบเรียลไทม์ทำงานอยู่",
                    "ตรวจพบความเงียบ กำลังจบการสนทนา", "จบการสนทนาแบบเรียลไทม์แล้ว", "การสนทนาแบบเรียลไทม์ล้มเหลว",
                    "กำลังจบการสนทนาแบบเรียลไทม์", "กำลังเชื่อมต่อเซสชันแบบเรียลไทม์", "กดปุ่มลัดอีกครั้งเพื่อหยุด",
                    "กำลังบันทึกคำแปล", "กำลังบันทึกโน้ต", "กำลังบันทึกคำถาม", "กำลังบันทึกการสนทนา", "กำลังบันทึก",
                    "เปิดการตั้งค่า", "เริ่ม/หยุดพิมพ์ด้วยเสียง", "เริ่ม/หยุดการสนทนาแบบเรียลไทม์", "ออก",
                    "ถามตอบ", "คัดลอก", "โน้ตไม่มีชื่อ", "โน้ต",
                    "ผู้ให้บริการเรียลไทม์ที่เลือกไม่ได้ส่งข้อความถอดเสียงของคำถามที่พูด", "ผู้ให้บริการเรียลไทม์ที่เลือกไม่ได้ส่งข้อความถอดเสียงของคำตอบ",
                    "ผู้ให้บริการเรียลไทม์ที่เลือกไม่ได้ส่งเสียงคำตอบแบบเนทีฟ", "ถามตอบแบบง่ายต้องใช้เส้นทางเสียงเรียลไทม์แบบเนทีฟของผู้ให้บริการที่เลือก");
            }
            if (code == "vi")
            {
                return V(
                    "Hãy hoàn tất tác vụ thoại hiện tại trước khi bắt đầu trò chuyện.", "Đang kết nối trò chuyện thời gian thực", "Trò chuyện thời gian thực đang hoạt động",
                    "Phát hiện im lặng; đang kết thúc trò chuyện", "Đã kết thúc trò chuyện thời gian thực", "Trò chuyện thời gian thực thất bại",
                    "Đang kết thúc trò chuyện thời gian thực", "Đang kết nối phiên thời gian thực", "Nhấn lại phím tắt để dừng",
                    "Đang ghi bản dịch", "Đang ghi ghi chú", "Đang ghi câu hỏi", "Đang ghi cuộc trò chuyện", "Đang ghi âm",
                    "Mở cài đặt", "Bắt đầu/dừng nhập liệu bằng giọng nói", "Bắt đầu/dừng trò chuyện thời gian thực", "Thoát",
                    "Hỏi đáp", "Sao chép", "Ghi chú chưa đặt tên", "Ghi chú",
                    "Nhà cung cấp thời gian thực đã chọn không trả về bản chép lời câu hỏi.", "Nhà cung cấp thời gian thực đã chọn không trả về bản chép lời câu trả lời.",
                    "Nhà cung cấp thời gian thực đã chọn không trả về âm thanh câu trả lời gốc.", "Hỏi đáp đơn giản phải dùng đường âm thanh thời gian thực gốc của nhà cung cấp đã chọn.");
            }
            if (code == "tr")
            {
                return V(
                    "Gerçek zamanlı konuşmayı başlatmadan önce mevcut ses görevini bitirin.", "Gerçek zamanlı konuşmaya bağlanılıyor", "Gerçek zamanlı konuşma etkin",
                    "Sessizlik algılandı; konuşma bitiriliyor", "Gerçek zamanlı konuşma sona erdi", "Gerçek zamanlı konuşma başarısız",
                    "Gerçek zamanlı konuşma bitiriliyor", "Gerçek zamanlı oturuma bağlanılıyor", "Durdurmak için kısayola tekrar basın",
                    "Çeviri kaydediliyor", "Not kaydediliyor", "Soru kaydediliyor", "Konuşma kaydediliyor", "Kaydediliyor",
                    "Ayarları aç", "Sesle yazmayı başlat/durdur", "Gerçek zamanlı konuşmayı başlat/durdur", "Çıkış",
                    "Soru-Cevap", "Kopyala", "Başlıksız not", "Not",
                    "Seçilen gerçek zamanlı sağlayıcı konuşulan sorunun dökümünü döndürmedi.", "Seçilen gerçek zamanlı sağlayıcı yanıt dökümünü döndürmedi.",
                    "Seçilen gerçek zamanlı sağlayıcı özgün yanıt sesini döndürmedi.", "Basit Soru-Cevap, seçilen sağlayıcının özgün gerçek zamanlı ses yolunu kullanmalıdır.");
            }
            return null;
        }

        private static string[] V(params string[] values)
        {
            return values;
        }
    }

    internal static class ScheduleLocalizationCatalog
    {
        private static readonly string[] Keys =
        {
            "schedule_title",
            "schedule_memo",
            "schedule_empty",
            "schedule_details",
            "schedule_delete",
            "schedule_delete_confirm",
            "schedule_delete_failed",
            "schedule_recovery_required"
        };

        internal static string[] RequiredKeys
        {
            get
            {
                var copy = new string[Keys.Length];
                Array.Copy(Keys, copy, Keys.Length);
                return copy;
            }
        }

        internal static void Apply(Dictionary<string, string> data, string code)
        {
            if (data == null) throw new ArgumentNullException("data");
            var values = Values(code);
            if (values.Length != Keys.Length)
            {
                throw new InvalidOperationException("Schedule localization entry count mismatch.");
            }
            for (var index = 0; index < Keys.Length; index++) data[Keys[index]] = values[index];
        }

        private static string[] Values(string code)
        {
            if (code == "zh") return V("日程与备忘录", "备忘录", "暂无日程或备忘录", "日程详情", "删除", "删除这条日程或备忘录？", "删除失败", "日程数据已损坏。为防止覆盖，写入已暂停；恢复副本已保留。");
            if (code == "zh-TW") return V("行程與備忘錄", "備忘錄", "目前沒有行程或備忘錄", "行程詳情", "刪除", "刪除這項行程或備忘錄？", "刪除失敗", "行程資料已損壞。為避免覆寫，寫入已暫停並保留復原副本。");
            if (code == "ko") return V("일정 및 메모", "메모", "일정이나 메모가 없습니다", "일정 세부정보", "삭제", "이 일정 또는 메모를 삭제할까요?", "삭제 실패", "일정 데이터가 손상되었습니다. 덮어쓰기를 막기 위해 쓰기가 중지되고 복구 사본이 보존되었습니다.");
            if (code == "fr") return V("Agenda et mémos", "Mémo", "Aucun agenda ni mémo", "Détails de l’agenda", "Supprimer", "Supprimer cet élément ?", "Échec de la suppression", "Les données sont endommagées. L’écriture est bloquée et une copie de récupération est conservée.");
            if (code == "de") return V("Termine und Memos", "Memo", "Keine Termine oder Memos", "Termindetails", "Löschen", "Diesen Termin oder dieses Memo löschen?", "Löschen fehlgeschlagen", "Die Termindaten sind beschädigt. Schreiben wurde gesperrt und eine Wiederherstellungskopie behalten.");
            if (code == "es") return V("Agenda y notas", "Nota", "No hay agenda ni notas", "Detalles de agenda", "Eliminar", "¿Eliminar este elemento?", "Error al eliminar", "Los datos están dañados. Se bloqueó la escritura y se conservó una copia de recuperación.");
            if (code == "ar") return V("الجدول والمذكرات", "مذكرة", "لا توجد مواعيد أو مذكرات", "تفاصيل الجدول", "حذف", "حذف هذا الموعد أو المذكرة؟", "فشل الحذف", "بيانات الجدول تالفة. تم إيقاف الكتابة وحفظ نسخة للاسترداد.");
            if (code == "ur") return V("شیڈول اور میمو", "میمو", "کوئی شیڈول یا میمو نہیں", "شیڈول کی تفصیل", "حذف کریں", "یہ شیڈول یا میمو حذف کریں؟", "حذف ناکام", "شیڈول ڈیٹا خراب ہے۔ اوور رائٹ روکنے کے لیے تحریر بند اور ریکوری کاپی محفوظ ہے۔");
            if (code == "bn") return V("সময়সূচি ও মেমো", "মেমো", "কোনো সময়সূচি বা মেমো নেই", "সময়সূচির বিবরণ", "মুছুন", "এটি মুছবেন?", "মোছা ব্যর্থ", "সময়সূচির ডেটা নষ্ট। ওভাররাইট ঠেকাতে লেখা বন্ধ এবং পুনরুদ্ধার কপি রাখা হয়েছে।");
            if (code == "fa") return V("برنامه و یادداشت‌ها", "یادداشت", "برنامه یا یادداشتی نیست", "جزئیات برنامه", "حذف", "این مورد حذف شود؟", "حذف ناموفق بود", "داده برنامه خراب است. نوشتن متوقف و نسخه بازیابی نگهداری شد.");
            if (code == "fil" || code == "tl") return V("Iskedyul at memo", "Memo", "Walang iskedyul o memo", "Detalye ng iskedyul", "Tanggalin", "Tanggalin ang item na ito?", "Hindi natanggal", "Sira ang datos. Itinigil ang pagsulat at itinabi ang kopya para sa pagbawi.");
            if (code == "sw") return V("Ratiba na kumbukumbu", "Kumbukumbu", "Hakuna ratiba au kumbukumbu", "Maelezo ya ratiba", "Futa", "Futa kipengee hiki?", "Kufuta kumeshindikana", "Data imeharibika. Uandishi umezuiwa na nakala ya urejeshi imehifadhiwa.");
            if (code == "my") return V("အစီအစဉ်နှင့် မှတ်စု", "မှတ်စု", "အစီအစဉ် သို့မဟုတ် မှတ်စု မရှိပါ", "အစီအစဉ် အသေးစိတ်", "ဖျက်ရန်", "ဤအရာကို ဖျက်မလား?", "ဖျက်၍မရပါ", "ဒေတာပျက်နေပါသည်။ ထပ်ရေးခြင်းကိုပိတ်ပြီး ပြန်လည်ရယူမည့်မိတ္တူကို သိမ်းထားသည်။");
            if (code == "am") return V("መርሐግብር እና ማስታወሻ", "ማስታወሻ", "መርሐግብር ወይም ማስታወሻ የለም", "ዝርዝር", "ሰርዝ", "ይህን ንጥል ይሰርዙ?", "መሰረዝ አልተሳካም", "ውሂቡ ተበላሽቷል። መጻፍ ታግዶ የመልሶ ማግኛ ቅጂ ተቀምጧል።");
            if (code == "pl") return V("Harmonogram i notatki", "Notatka", "Brak harmonogramu i notatek", "Szczegóły", "Usuń", "Usunąć ten element?", "Usuwanie nie powiodło się", "Dane są uszkodzone. Zapis zablokowano, a kopię odzyskiwania zachowano.");
            if (code == "ms") return V("Jadual dan memo", "Memo", "Tiada jadual atau memo", "Butiran jadual", "Padam", "Padam item ini?", "Gagal memadam", "Data rosak. Penulisan disekat dan salinan pemulihan disimpan.");
            if (code == "uk") return V("Розклад і нотатки", "Нотатка", "Немає розкладу чи нотаток", "Деталі розкладу", "Видалити", "Видалити цей елемент?", "Не вдалося видалити", "Дані пошкоджено. Запис заблоковано, копію для відновлення збережено.");
            if (code == "kk") return V("Кесте және жазбалар", "Жазба", "Кесте немесе жазба жоқ", "Кесте мәліметтері", "Жою", "Бұл элемент жойылсын ба?", "Жою сәтсіз", "Дерек бүлінген. Жазу бұғатталып, қалпына келтіру көшірмесі сақталды.");
            if (code == "ro") return V("Program și memorii", "Notă", "Nu există program sau note", "Detalii program", "Șterge", "Ștergeți acest element?", "Ștergerea a eșuat", "Datele sunt corupte. Scrierea a fost blocată și s-a păstrat o copie de recuperare.");
            if (code == "nl") return V("Planning en memo’s", "Memo", "Geen planning of memo’s", "Planningsdetails", "Verwijderen", "Dit item verwijderen?", "Verwijderen mislukt", "De gegevens zijn beschadigd. Schrijven is geblokkeerd en een herstelkopie is bewaard.");
            if (code == "hi") return V("कार्यक्रम और मेमो", "मेमो", "कोई कार्यक्रम या मेमो नहीं", "कार्यक्रम विवरण", "हटाएं", "यह आइटम हटाएं?", "हटाना विफल", "डेटा दूषित है। लिखना रोक दिया गया है और पुनर्प्राप्ति प्रति रखी गई है।");
            if (code == "ja") return V("予定とメモ", "メモ", "予定またはメモはありません", "予定の詳細", "削除", "この項目を削除しますか？", "削除できませんでした", "予定データが破損しています。上書きを防ぐため書き込みを停止し、復旧用コピーを保存しました。");
            if (code == "ru") return V("Расписание и заметки", "Заметка", "Нет расписания или заметок", "Подробности", "Удалить", "Удалить этот элемент?", "Не удалось удалить", "Данные повреждены. Запись заблокирована, копия для восстановления сохранена.");
            if (code == "pt") return V("Agenda e memorandos", "Memorando", "Sem agenda ou memorandos", "Detalhes da agenda", "Excluir", "Excluir este item?", "Falha ao excluir", "Os dados estão corrompidos. A gravação foi bloqueada e uma cópia de recuperação foi preservada.");
            if (code == "it") return V("Agenda e promemoria", "Promemoria", "Nessun appuntamento o promemoria", "Dettagli agenda", "Elimina", "Eliminare questo elemento?", "Eliminazione non riuscita", "I dati sono danneggiati. La scrittura è bloccata e una copia di recupero è stata conservata.");
            if (code == "id") return V("Jadwal dan memo", "Memo", "Tidak ada jadwal atau memo", "Detail jadwal", "Hapus", "Hapus item ini?", "Gagal menghapus", "Data rusak. Penulisan diblokir dan salinan pemulihan disimpan.");
            if (code == "th") return V("กำหนดการและบันทึก", "บันทึก", "ไม่มีกำหนดการหรือบันทึก", "รายละเอียดกำหนดการ", "ลบ", "ลบรายการนี้หรือไม่?", "ลบไม่สำเร็จ", "ข้อมูลเสียหาย ระบบหยุดเขียนและเก็บสำเนาสำหรับกู้คืนไว้");
            if (code == "vi") return V("Lịch và ghi nhớ", "Ghi nhớ", "Không có lịch hoặc ghi nhớ", "Chi tiết lịch", "Xóa", "Xóa mục này?", "Xóa thất bại", "Dữ liệu bị hỏng. Việc ghi bị khóa và bản sao khôi phục đã được giữ lại.");
            if (code == "tr") return V("Program ve notlar", "Not", "Program veya not yok", "Program ayrıntıları", "Sil", "Bu öğe silinsin mi?", "Silme başarısız", "Veri bozuk. Yazma engellendi ve kurtarma kopyası saklandı.");
            return V("Schedules and memos", "Memo", "No schedules or memos", "Schedule details", "Delete", "Delete this schedule or memo?", "Delete failed", "Schedule data is damaged. Writes are blocked and a recovery copy was preserved.");
        }

        private static string[] V(params string[] values)
        {
            return values;
        }
    }
}
