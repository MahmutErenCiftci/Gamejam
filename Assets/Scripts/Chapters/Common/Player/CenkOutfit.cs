using System;
using System.Collections.Generic;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Cenk'in sırtındaki çanta (Cenk.prefab'ta, SpriteRenderer ile aynı objede).
    /// Animator ve pozlar (IsoCharacterAnimator.ShowPose) her zaman okul çantalı sprite'ları yazar; bu bileşen
    /// LateUpdate'te (çizimden hemen önce) gösterilen sprite'ı çiftler tablosundan istenen görünümdeki eşiyle değiştirir.
    /// Böylece yön, kare ve poz aynı kalır; çanta alınınca bir sonraki çizimde yeni görünüm gelir (zıplama yok).
    /// Görünümler (öncelik sırasıyla):
    ///  • Deprem çantalı: GameSession.QuakeBagTaken (deprem modunda afet çantası sarsıntıdan sonra alınınca) →
    ///    QuakeBag/CHR_Cenk_Walk_*_QuakeBag.png, Poses/QuakeBag/*.png (yerdeki PROP_EQ_EmergencyBag_Ready ile aynı renkler)
    ///  • Sarı yağmurluk + okul çantası: GameSession "raincoat_taken" (RaincoatFlag) VE BackpackTaken →
    ///    Raincoat/CHR_Cenk_Walk_*_Raincoat.png, Poses/Raincoat/*.png (çanta yağmurluğun üstünde, kasket açıkta)
    ///  • Okul çantalı: GameSession.BackpackTaken (Sel'de yatak odasında "[E] Çantanı al") → orijinal sayfalar
    ///  • Çantasız (varsayılan): NoBag/CHR_Cenk_Walk_*_NoBag.png, Poses/NoBag/*.png
    /// Her tablo aynı dilim sırasını kullanır (sayfa kopyaları; aynı internal ID).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(1000)]
    public class CenkOutfit : MonoBehaviour
    {
        public enum Look
        {
            NoBag,
            SchoolBag,
            QuakeBag,
            Raincoat
        }

        /// <summary>Yağmurluk giyildi bayrağı (eski sabah sel akışıyla aynı ID: FloodChapterController.RaincoatFlag).</summary>
        public const string RaincoatFlag = "raincoat_taken";

        [Serializable]
        public struct SpritePair
        {
            public Sprite withBag;
            public Sprite noBag;
        }

        [Serializable]
        public struct QuakeBagPair
        {
            [Tooltip("Okul çantalı (orijinal) kare")]
            public Sprite withBag;
            [Tooltip("Aynı karenin deprem (afet) çantalı hali")]
            public Sprite quakeBag;
        }

        [Serializable]
        public struct RaincoatPair
        {
            [Tooltip("Okul çantalı (orijinal) kare")]
            public Sprite withBag;
            [Tooltip("Aynı karenin sarı yağmurluklu + okul çantalı hali")]
            public Sprite raincoat;
        }

        [Tooltip("Çantalı sprite ↔ aynı karenin çantasız hali")]
        [SerializeField] private SpritePair[] pairs = Array.Empty<SpritePair>();
        [Tooltip("Okul çantalı sprite ↔ aynı karenin deprem çantalı hali")]
        [SerializeField] private QuakeBagPair[] quakeBagPairs = Array.Empty<QuakeBagPair>();
        [Tooltip("Okul çantalı sprite ↔ aynı karenin sarı yağmurluklu (çanta yağmurluğun üstünde) hali")]
        [SerializeField] private RaincoatPair[] raincoatPairs = Array.Empty<RaincoatPair>();
        [Tooltip("İşaretliyse oyun durumuna bakmadan hep okul çantalı görünür (test için)")]
        [SerializeField] private bool forceBackpack;
        [Tooltip("İşaretliyse oyun durumuna bakmadan hep deprem çantalı görünür (test için)")]
        [SerializeField] private bool forceQuakeBag;
        [Tooltip("İşaretliyse oyun durumuna bakmadan hep yağmurluklu + okul çantalı görünür (test için; deprem çantası yine önce gelir)")]
        [SerializeField] private bool forceRaincoat;

        private static readonly List<CenkOutfit> Instances = new List<CenkOutfit>();

        private SpriteRenderer spriteRenderer;
        // Her görünümdeki sprite → okul çantalı (orijinal) eşi; orijinal → istenen görünümdeki eşi.
        private readonly Dictionary<Sprite, Sprite> toCanonical = new Dictionary<Sprite, Sprite>();
        private readonly Dictionary<Sprite, Sprite> toNoBag = new Dictionary<Sprite, Sprite>();
        private readonly Dictionary<Sprite, Sprite> toQuakeBag = new Dictionary<Sprite, Sprite>();
        private readonly Dictionary<Sprite, Sprite> toRaincoat = new Dictionary<Sprite, Sprite>();
        private Look look;
        private bool mapsBuilt; // serileştirilmez: Play sırasında kod yeniden derlenirse tablolar yeniden kurulur

        /// <summary>Şu anki görünüm.</summary>
        public Look CurrentLook => look;

        /// <summary>Şu an sırtında okul çantası görünüyor mu (yağmurluklu görünümde de çanta sırttadır).</summary>
        public bool WearingBackpack => look == Look.SchoolBag || look == Look.Raincoat;

        /// <summary>Şu an sarı yağmurluklu mu görünüyor.</summary>
        public bool WearingRaincoat => look == Look.Raincoat;

        /// <summary>Oyun durumu yağmurluklu görünümü istiyor mu (yağmurluk + okul çantası bayrakları).</summary>
        public static bool RaincoatWanted => GameSession.HasFlag(RaincoatFlag) && GameSession.BackpackTaken;

        private Look Wanted
        {
            get
            {
                if (forceQuakeBag || GameSession.QuakeBagTaken) return Look.QuakeBag;
                if (forceRaincoat || RaincoatWanted) return Look.Raincoat;
                if (forceBackpack || GameSession.BackpackTaken) return Look.SchoolBag;
                return Look.NoBag;
            }
        }

        /// <summary>
        /// Cenk'i sarı yağmurluk + okul çantasıyla giydirir: iki bayrağı da açar ve sahnedeki bütün Cenk'leri günceller.
        /// (Akşam sel bölümü başında çağrılır. Deprem çantası bayrağı açıksa o görünüm önce gelir.)
        /// </summary>
        public static void WearRaincoatWithBackpack()
        {
            GameSession.MarkBackpackTaken();
            GameSession.SetFlag(RaincoatFlag);
            RefreshAll();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            BuildMaps();
            look = Wanted;
            Apply();
        }

        private void BuildMaps()
        {
            mapsBuilt = true;
            toCanonical.Clear();
            toNoBag.Clear();
            toQuakeBag.Clear();
            toRaincoat.Clear();
            foreach (var pair in pairs)
            {
                if (pair.withBag == null || pair.noBag == null) continue;
                toNoBag[pair.withBag] = pair.noBag;
                toCanonical[pair.noBag] = pair.withBag;
            }
            foreach (var pair in quakeBagPairs)
            {
                if (pair.withBag == null || pair.quakeBag == null) continue;
                toQuakeBag[pair.withBag] = pair.quakeBag;
                toCanonical[pair.quakeBag] = pair.withBag;
            }
            foreach (var pair in raincoatPairs)
            {
                if (pair.withBag == null || pair.raincoat == null) continue;
                toRaincoat[pair.withBag] = pair.raincoat;
                toCanonical[pair.raincoat] = pair.withBag;
            }
        }

        private void OnEnable()
        {
            if (!Instances.Contains(this)) Instances.Add(this);
            Refresh();
        }

        private void OnDisable() => Instances.Remove(this);

        /// <summary>Çanta/yağmurluk durumunu GameSession'dan yeniden okur ve sprite'ı hemen günceller.</summary>
        public void Refresh()
        {
            look = Wanted;
            Apply();
        }

        /// <summary>Sahnedeki bütün Cenk'leri günceller (çanta/yağmurluk alındığında çağrılır).</summary>
        public static void RefreshAll()
        {
            for (int i = Instances.Count - 1; i >= 0; i--)
                if (Instances[i] != null) Instances[i].Refresh();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) return;
            if (!mapsBuilt) BuildMaps();
            var current = spriteRenderer.sprite;
            if (current == null) return;
            if (!toCanonical.TryGetValue(current, out var canonical)) canonical = current;
            Sprite target = canonical;
            if (look == Look.NoBag && toNoBag.TryGetValue(canonical, out var noBag) && noBag != null) target = noBag;
            else if (look == Look.QuakeBag && toQuakeBag.TryGetValue(canonical, out var quake) && quake != null) target = quake;
            else if (look == Look.Raincoat && toRaincoat.TryGetValue(canonical, out var coat) && coat != null) target = coat;
            if (target != current) spriteRenderer.sprite = target;
        }
    }
}
