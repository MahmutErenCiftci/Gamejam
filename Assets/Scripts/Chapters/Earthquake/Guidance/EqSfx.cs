using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Deprem bölümünün kısa efektleri için tek giriş: GameAudio.PlaySfx'e iletir ve Console'a "[EQ-SFX] id" yazar
    /// (oyun içi testte hangi sesin ne zaman çaldığı görülsün). Dosyalar Assets/Resources/Audio/SFX/SFX_EQ_*.
    /// </summary>
    public static class EqSfx
    {
        public const string Inspect = "SFX_EQ_Inspect";
        public const string HazardWarn = "SFX_EQ_Hazard_Warn";
        public const string DoorLocked = "SFX_EQ_Door_Locked";
        public const string DoorOpen = "SFX_EQ_Door_Open";
        public const string BagWear = "SFX_EQ_Bag_Wear";
        public const string QuestionOpen = "SFX_EQ_Question_Open";
        public const string AssemblyArrive = "SFX_EQ_Assembly_Arrive";
        public const string ProtectStep = "SFX_EQ_Protect_Step";
        public const string ProtectRelease = "SFX_EQ_Protect_Release";
        public const string Injured = "SFX_EQ_Injured";
        public const string ShelfCrash = "SFX_EQ_Shelf_Crash";

        /// <summary>Efekti çalar (GameAudio) ve kaydını düşer. vol: efekt seviyesine göre 0–1.</summary>
        public static void Play(string id, float vol = 1f)
        {
            if (string.IsNullOrEmpty(id)) return;
            Debug.Log("[EQ-SFX] " + id);
            GameAudio.PlaySfx(id, vol);
        }

        /// <summary>Kendi AudioSource'u olan bileşenler için: alan boşsa klibi Resources/Audio'dan yükler.</summary>
        public static AudioClip Load(AudioClip assigned, string id) => assigned != null ? assigned : GameAudio.LoadClip(id);
    }
}
