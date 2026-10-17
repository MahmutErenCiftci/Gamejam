using System;
using System.Collections.Generic;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Cenk'in sırt çantası (Cenk.prefab'ta, SpriteRenderer ile aynı objede).
    /// Animator ve pozlar (IsoCharacterAnimator.ShowPose) her zaman çantalı sprite'ları yazar; bu bileşen
    /// LateUpdate'te (çizimden hemen önce) gösterilen sprite'ı çiftler tablosundan çantasız eşiyle değiştirir.
    /// Böylece yön, kare ve poz aynı kalır; çanta alınınca bir sonraki çizimde çantalı görünür (zıplama yok).
    /// Çanta: GameSession.BackpackTaken (Sel'de yatak odasında "[E] Çantanı al"; Deprem'de hiç alınmaz).
    /// Çiftler: Assets/Art/Characters/Cenk/CHR_Cenk_Walk_*.png ↔ NoBag/CHR_Cenk_Walk_*_NoBag.png ve
    /// Poses/CHR_Cenk_*.png ↔ Poses/NoBag/CHR_Cenk_*_NoBag.png (aynı dilim sırası).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(1000)]
    public class CenkOutfit : MonoBehaviour
    {
        [Serializable]
        public struct SpritePair
        {
            public Sprite withBag;
            public Sprite noBag;
        }

        [Tooltip("Çantalı sprite ↔ aynı karenin çantasız hali")]
        [SerializeField] private SpritePair[] pairs = Array.Empty<SpritePair>();
        [Tooltip("İşaretliyse oyun durumuna bakmadan hep çantalı görünür (test için)")]
        [SerializeField] private bool forceBackpack;

        private static readonly List<CenkOutfit> Instances = new List<CenkOutfit>();

        private SpriteRenderer spriteRenderer;
        private readonly Dictionary<Sprite, Sprite> toNoBag = new Dictionary<Sprite, Sprite>();
        private readonly Dictionary<Sprite, Sprite> toWithBag = new Dictionary<Sprite, Sprite>();
        private bool wearing;

        /// <summary>Şu an çantalı mı görünüyor.</summary>
        public bool WearingBackpack => wearing;

        private bool Wanted => forceBackpack || GameSession.BackpackTaken;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            foreach (var pair in pairs)
            {
                if (pair.withBag == null || pair.noBag == null) continue;
                toNoBag[pair.withBag] = pair.noBag;
                toWithBag[pair.noBag] = pair.withBag;
            }
            wearing = Wanted;
            Apply();
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
            wearing = Wanted;
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
            if (spriteRenderer == null) return;
            var current = spriteRenderer.sprite;
            if (current == null) return;
            var map = wearing ? toWithBag : toNoBag;
            if (map.TryGetValue(current, out var replacement) && replacement != null)
                spriteRenderer.sprite = replacement;
        }
    }
}
