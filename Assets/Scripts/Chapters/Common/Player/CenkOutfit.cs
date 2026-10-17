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
    /// Görünümler:
    ///  • Çantasız (varsayılan): NoBag/CHR_Cenk_Walk_*_NoBag.png, Poses/NoBag/*.png
    ///  • Okul çantalı: GameSession.BackpackTaken (Sel'de yatak odasında "[E] Çantanı al") → orijinal sayfalar
    ///  • Deprem çantalı: GameSession.QuakeBagTaken (deprem modunda afet çantası sarsıntıdan sonra alınınca) →
    ///    QuakeBag/CHR_Cenk_Walk_*_QuakeBag.png, Poses/QuakeBag/*.png (yerdeki PROP_EQ_EmergencyBag_Ready ile aynı renkler)
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
            QuakeBag
        }

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

        [Tooltip("Çantalı sprite ↔ aynı karenin çantasız hali")]
        [SerializeField] private SpritePair[] pairs = Array.Empty<SpritePair>();
        [Tooltip("Okul çantalı sprite ↔ aynı karenin deprem çantalı hali")]
        [SerializeField] private QuakeBagPair[] quakeBagPairs = Array.Empty<QuakeBagPair>();
        [Tooltip("İşaretliyse oyun durumuna bakmadan hep okul çantalı görünür (test için)")]
        [SerializeField] private bool forceBackpack;
        [Tooltip("İşaretliyse oyun durumuna bakmadan hep deprem çantalı görünür (test için)")]
        [SerializeField] private bool forceQuakeBag;

        private static readonly List<CenkOutfit> Instances = new List<CenkOutfit>();

        private SpriteRenderer spriteRenderer;
        // Her görünümdeki sprite → okul çantalı (orijinal) eşi; orijinal → istenen görünümdeki eşi.
        private readonly Dictionary<Sprite, Sprite> toCanonical = new Dictionary<Sprite, Sprite>();
        private readonly Dictionary<Sprite, Sprite> toNoBag = new Dictionary<Sprite, Sprite>();
        private readonly Dictionary<Sprite, Sprite> toQuakeBag = new Dictionary<Sprite, Sprite>();
        private Look look;
        private bool mapsBuilt; // serileştirilmez: Play sırasında kod yeniden derlenirse tablolar yeniden kurulur

        /// <summary>Şu anki görünüm.</summary>
        public Look CurrentLook => look;

        /// <summary>Şu an okul çantalı mı görünüyor.</summary>
        public bool WearingBackpack => look == Look.SchoolBag;

        private Look Wanted
        {
            get
            {
                if (forceQuakeBag || GameSession.QuakeBagTaken) return Look.QuakeBag;
                if (forceBackpack || GameSession.BackpackTaken) return Look.SchoolBag;
                return Look.NoBag;
            }
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
        }

        private void OnEnable()
        {
            if (!Instances.Contains(this)) Instances.Add(this);
            Refresh();
        }

        private void OnDisable() => Instances.Remove(this);

        /// <summary>Çanta durumunu GameSession'dan yeniden okur ve sprite'ı hemen günceller.</summary>
        public void Refresh()
        {
            look = Wanted;
            Apply();
        }

        /// <summary>Sahnedeki bütün Cenk'leri günceller (çanta alındığında çağrılır).</summary>
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
            if (target != current) spriteRenderer.sprite = target;
        }
    }
}
