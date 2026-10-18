using System;
using AfetUyanisi.Shared;

namespace AfetUyanisi.Chapters.Objectives
{
    [Serializable]
    public sealed class ObjectiveTrigger
    {
        public string type;
        public string key;

        public bool Matches(string eventType, string eventKey) =>
            string.Equals(type, eventType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(key, eventKey, StringComparison.Ordinal);
    }

    [Serializable]
    public sealed class ObjectiveDefinition
    {
        public string id;
        public string group;
        public string text;
        // JsonUtility enum adlarını okumaz. JSON okunur metin tutar, doğrulama yükleme sırasında yapılır.
        public string kind = "Main";
        public int max = 1;
        public int target = 1;
        public ObjectiveTrigger[] completeOn = Array.Empty<ObjectiveTrigger>();
        public ObjectiveTrigger[] warnOn = Array.Empty<ObjectiveTrigger>();
        public string badge;
        /// <summary>Sayaç birimi: "+1 TEHLİKE" uçan yazısı için (isteğe bağlı).</summary>
        public string unit;
        /// <summary>Keep görevi bu gruba kadar (dahil) panelde kalır; boşsa sadece kendi grubunda.</summary>
        public string keepUntil;

        public ObjectiveKind Kind => (ObjectiveKind)Enum.Parse(typeof(ObjectiveKind), kind, true);
        // Ana sayaçlar yüzdeye dahildir; görünüme Counter olarak teslim edilir.
        public ObjectiveKind RowKind => max > 1 && Kind == ObjectiveKind.Main ? ObjectiveKind.Counter : Kind;

        public static bool Matches(ObjectiveTrigger[] triggers, string type, string key)
        {
            if (triggers == null) return false;
            foreach (var trigger in triggers)
                if (trigger != null && trigger.Matches(type, key)) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class ObjectiveGroupDefinition
    {
        public string id;
        public string title;
        /// <summary>Grubun ana görevleri bitince çıkan kısa şerit (boşsa başlıktan türetilir: "MUTFAK TAMAM!").</summary>
        public string banner;
        public ObjectiveTrigger[] activateOn = Array.Empty<ObjectiveTrigger>();
    }

    [Serializable]
    public sealed class ObjectiveChapterDefinition
    {
        public string chapter;
        public ObjectiveGroupDefinition[] groups;
        public ObjectiveDefinition[] objectives;
    }
}
