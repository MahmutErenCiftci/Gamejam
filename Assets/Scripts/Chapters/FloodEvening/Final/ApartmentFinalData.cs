using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Apartman finalinin kapı kodu + ekran metinleri (Assets/Data/FloodEvening/apartment_final.json, JsonUtility).
    /// PIN TEK KAYNAKTIR: mektup data.pin'i gösterir, kilit data.pin'i kabul eder; kodun başka hiçbir yerinde yazılmaz.
    /// Geçerli PIN = tam 4 rakam; dosyadaki değer geçersizse uyarı yazılır ve varsayılan kullanılır.
    ///
    /// JSON anahtarları = alan adları (snake_case):
    ///   pin
    ///   lock_reset_title, lock_reset_body, lock_hint_mailbox          (kilit sıfırlanmış uyarısı)
    ///   mailbox_prompt, mailbox_empty ({0} = kod), lock_prompt, lock_prompt_pin   ([E] ipuçları — dünya tarafı)
    ///   letter_header, letter_body1, letter_body2, letter_close        (mektup yakın planı)
    ///   pin_title, pin_wrong, pin_ok, pin_hint, handle_hint, cancel_hint   (kilit yakın planı)
    ///   end_title, end_subtitle                                         (liderin bitiş ekranı)
    /// Dosyada olmayan alan varsayılan Türkçe metniyle kalır. Dosyaya sonradan eklenen ve sınıfta alanı olmayan
    /// anahtarlar da Text("anahtar") ile okunur (bilinmeyen anahtarlar hata vermez).
    /// </summary>
    [Serializable]
    public class ApartmentFinalData
    {
        public string pin = "4729";

        public string lock_reset_title = "KİLİT SIFIRLANMIŞ";
        public string lock_reset_body = "Kilit sıfırlanmış. Yeni kod gerekli.";
        public string lock_hint_mailbox = "Posta kutusuna bir bak.";

        public string mailbox_prompt = "Posta Kutusunu Kontrol Et";
        public string mailbox_empty = "Posta kutusu boş. Kod: {0}";
        public string lock_prompt = "Kilidi Kontrol Et";
        public string lock_prompt_pin = "Kodu Gir";

        public string letter_header = "APARTMAN YÖNETİMİ";
        public string letter_body1 = "Elektronik kapı kilidi için yeni sıfırlama kodunuz:";
        public string letter_body2 = "Lütfen bu kodu giriş paneline giriniz.";
        public string letter_close = "[E] Kapat";

        public string pin_title = "GİRİŞ KODU";
        public string pin_wrong = "HATALI KOD";
        public string pin_ok = "KOD DOĞRU";
        public string pin_hint = "Rakamlara fare ile tıkla";
        public string handle_hint = "SOL TIK BASILI TUT VE AŞAĞI SÜRÜKLE";
        public string cancel_hint = "[Esc] Geri";

        public string end_title = "EVE ULAŞTIN!";
        public string end_subtitle = "EŞİK · SEL BÖLÜMÜ TAMAMLANDI";

        [NonSerialized] private string rawJson;
        [NonSerialized] private HashSet<string> warned;

        [Serializable] private class Wrap { public string v; }

        /// <summary>PIN'in rakamları (0..9), her zaman 4 adet.</summary>
        public int[] PinDigits
        {
            get
            {
                var d = new int[4];
                for (int i = 0; i < 4; i++) d[i] = pin[i] - '0';
                return d;
            }
        }

        /// <summary>"Posta kutusu boş. Kod: 4729" — posta kutusu ikinci kez açıldığında.</summary>
        public string MailboxEmptyText => SafeFormat(mailbox_empty, pin);

        /// <summary>Tam 4 ASCII rakam mı?</summary>
        public static bool IsValidPin(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 4) return false;
            for (int i = 0; i < 4; i++) if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }

        /// <summary>
        /// JSON'u okur. json null / bozuk ise varsayılanlar döner (Console'a uyarı). Boş bırakılmış metin alanı varsayılana döner.
        /// </summary>
        public static ApartmentFinalData Load(TextAsset json)
        {
            var d = new ApartmentFinalData();
            if (json == null) { Debug.LogWarning("[FinalUI] apartment_final.json bağlı değil; varsayılan metinler kullanılıyor."); return d; }
            try
            {
                JsonUtility.FromJsonOverwrite(json.text, d);
                d.rawJson = json.text;
            }
            catch (Exception ex)
            {
                Debug.LogError("[FinalUI] apartment_final.json okunamadı: " + ex.Message + " — varsayılanlar kullanılıyor.");
                d = new ApartmentFinalData();
            }
            d.Validate();
            return d;
        }

        /// <summary>PIN'i ve boş metinleri denetler (Load çağırır; elle kurulan nesne için de kullanılabilir).</summary>
        public void Validate()
        {
            var defaults = new ApartmentFinalData();
            if (!IsValidPin(pin))
            {
                Debug.LogWarning("[FinalUI] apartment_final.json: geçersiz pin '" + pin + "' (tam 4 rakam olmalı); varsayılan kullanılıyor.");
                pin = defaults.pin;
            }
            foreach (var f in typeof(ApartmentFinalData).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType != typeof(string)) continue;
                if (string.IsNullOrEmpty((string)f.GetValue(this))) f.SetValue(this, f.GetValue(defaults));
            }
        }

        /// <summary>
        /// Anahtara göre metin: önce sınıftaki alan, yoksa JSON'daki aynı adlı metin (sonradan eklenen anahtarlar),
        /// o da yoksa anahtarın kendisi (Console'a bir kez uyarı).
        /// </summary>
        public string Text(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            var f = typeof(ApartmentFinalData).GetField(key, BindingFlags.Public | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(this);
            if (!string.IsNullOrEmpty(rawJson))
            {
                var m = Regex.Match(rawJson, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                if (m.Success)
                {
                    try { return JsonUtility.FromJson<Wrap>("{\"v\":\"" + m.Groups[1].Value + "\"}").v; }
                    catch (Exception) { return m.Groups[1].Value; }
                }
            }
            warned ??= new HashSet<string>();
            if (warned.Add(key)) Debug.LogWarning("[FinalUI] apartment_final.json: '" + key + "' anahtarı yok.");
            return key;
        }

        private static string SafeFormat(string format, string arg)
        {
            try { return string.Format(format ?? "", arg); }
            catch (FormatException) { return (format ?? "") + " " + arg; }
        }
    }
}
