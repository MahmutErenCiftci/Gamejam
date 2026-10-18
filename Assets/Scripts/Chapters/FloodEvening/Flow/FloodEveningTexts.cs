using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Akşam sel bölümünün ekran metinleri — HEPSİ Assets/Data/Story/flood_evening.json'dan (Arda'nın dosyası) okunur,
    /// kodda Türkçe metin tutulmaz. Kullanılan bölümler:
    ///  • "scenes": diyalog sahneleri (FE_*) + özel sahneler: FE_TITLE (speaker = kart başlığı, text = alt yazı),
    ///    FE_RESULT_SUCCESS / FE_RESULT_TIMEOUT, FE_RESULT_STEPS (speaker = Hub adım id'si), FE_RESULT_MISTAKES (speaker = hata id'si).
    ///  • "result": title_success, title_with_mistakes, title_timeout, learned[], note.
    ///  • "ui": [{ "id": "...", "text": "..." }] — HUD, uyarı şeritleri, sonuç ekranı etiketleri (anahtarlar: FloodEveningTexts.Keys).
    /// Eksik anahtar Console'a bir kez yazılır ve ekranda anahtarın kendisi görünür (metin unutulmasın diye).
    /// DialogueRunner bilinmeyen alanları yok sayar; aynı dosya diyaloglar için de kullanılır.
    /// </summary>
    public class FloodEveningTexts
    {
        [Serializable] private class Line { public string speaker; public string text; }
        [Serializable] private class Scene { public string id; public Line[] lines; }
        [Serializable] private class Entry { public string id; public string text; }
        [Serializable] private class Result { public string title_success, title_with_mistakes, title_timeout, note; public string[] learned; }
        [Serializable] private class File { public Scene[] scenes; public Result result; public Entry[] ui; }

        private readonly Dictionary<string, string> ui = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Scene> scenes = new Dictionary<string, Scene>(StringComparer.Ordinal);
        private readonly HashSet<string> warned = new HashSet<string>();
        private Result result;

        public bool Loaded { get; private set; }

        public static FloodEveningTexts Load(TextAsset json)
        {
            var t = new FloodEveningTexts();
            if (json == null) { Debug.LogWarning("[FE] flood_evening.json bağlı değil; metinler anahtar olarak görünecek."); return t; }
            try
            {
                var f = JsonUtility.FromJson<File>(json.text);
                if (f.scenes != null) foreach (var s in f.scenes) if (s != null && !string.IsNullOrEmpty(s.id)) t.scenes[s.id] = s;
                if (f.ui != null) foreach (var e in f.ui) if (e != null && !string.IsNullOrEmpty(e.id)) t.ui[e.id] = e.text ?? "";
                t.result = f.result;
                t.Loaded = true;
            }
            catch (Exception ex) { Debug.LogError("[FE] flood_evening.json okunamadı: " + ex.Message); }
            return t;
        }

        /// <summary>"ui" bölümündeki metin (yoksa anahtarın kendisi).</summary>
        public string Ui(string key)
        {
            if (ui.TryGetValue(key, out var v)) return v;
            Warn("ui." + key);
            return key;
        }

        /// <summary>"ui" bölümünde bu anahtar var mı (yoksa çağıran kendi varsayılan metnini kullanır).</summary>
        public bool HasUi(string key) => !string.IsNullOrEmpty(key) && ui.ContainsKey(key);

        public bool HasScene(string id) => !string.IsNullOrEmpty(id) && scenes.ContainsKey(id);

        /// <summary>Sahnenin speaker'ı verilen satırının metni (FE_RESULT_STEPS gibi sözlük sahneleri için).</summary>
        public string SceneLine(string sceneId, string speaker)
        {
            if (scenes.TryGetValue(sceneId, out var s) && s.lines != null)
                foreach (var l in s.lines) if (l != null && l.speaker == speaker) return l.text;
            Warn(sceneId + "/" + speaker);
            return null;
        }

        /// <summary>Sahnenin ilk satırı (speaker, text).</summary>
        public bool FirstLine(string sceneId, out string speaker, out string text)
        {
            speaker = text = null;
            if (!scenes.TryGetValue(sceneId, out var s) || s.lines == null || s.lines.Length == 0) { Warn(sceneId); return false; }
            speaker = s.lines[0].speaker; text = s.lines[0].text;
            return true;
        }

        public string ResultTitle(bool success, bool withMistakes)
        {
            string v = result == null ? null : !success ? result.title_timeout : withMistakes && !string.IsNullOrEmpty(result.title_with_mistakes) ? result.title_with_mistakes : result.title_success;
            if (string.IsNullOrEmpty(v)) { Warn("result.title"); return success ? "result.title_success" : "result.title_timeout"; }
            return v;
        }

        public string[] Learned => result != null && result.learned != null ? result.learned : Array.Empty<string>();
        public string Note => result != null ? result.note : null;

        private void Warn(string key)
        {
            if (warned.Add(key)) Debug.LogWarning("[FE] flood_evening.json'da metin yok: " + key);
        }

        /// <summary>"ui" anahtarları (Arda bu listeyi JSON'da doldurur).</summary>
        public static class Keys
        {
            public const string CardNote = "card_note";
            public const string GaugeTitle = "gauge_title";
            public const string GaugeNote = "gauge_note";
            public const string Stage1 = "stage_1", Stage2 = "stage_2", Stage3 = "stage_3", Stage4 = "stage_4";
            public const string Toast2Title = "toast_stage2_title", Toast2Body = "toast_stage2_body";
            public const string Toast3Title = "toast_stage3_title", Toast3Body = "toast_stage3_body";
            public const string Toast4Title = "toast_stage4_title", Toast4Body = "toast_stage4_body";
            public const string ToastFinalTitle = "toast_final_title", ToastFinalBody = "toast_final_body";
            public const string ToastSafeTitle = "toast_safe_title", ToastSafeBody = "toast_safe_body";
            public const string ToastTimeoutTitle = "toast_timeout_title", ToastTimeoutBody = "toast_timeout_body";
            public const string ToastSayTitle = "toast_say_title";
            public const string WarningFallbackTitle = "warning_fallback_title", WarningFallbackBody = "warning_fallback_body";
            public const string ResultHeader = "result_header";
            public const string ResultTimeoutTitle = "result_timeout_title";
            public const string ResultSuccessMessage = "result_success_message";
            public const string ResultCorrectHeader = "result_correct_header";
            public const string ResultCorrectedHeader = "result_corrected_header";
            public const string ResultLessonsHeader = "result_lessons_header";
            public const string ResultNoCorrect = "result_no_correct";
            public const string ResultStayedDry = "result_stayed_dry";
            public const string ResultRetry = "result_retry";
            public const string ResultMenu = "result_menu";
        }
    }
}
