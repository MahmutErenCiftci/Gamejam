using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Cenk'in rüzgâra yaslanan yürüme kareleri (FloodWindCenkReaction okur). Her kayıt Cenk'in bir yürüme karesini
    /// (8 izometrik yön × 6 kare × 4 görünüm: okul çantalı, çantasız, deprem çantalı, yağmurluklu) ve aynı karenin
    /// ayaklar yerinde kalarak gövdesi -MaxShear..+MaxShear piksel yana yaslanmış hallerini tutar (pozitif = tepe sağa,
    /// eğim yukarı doğru artar: kalçadan bükülme). 0 = karenin kendisi. Üretici: FloodWindCenkPoseBaker.
    /// </summary>
    [CreateAssetMenu(menuName = "AfetUyanisi/Flood Evening/Wind Cenk Poses", fileName = "FE_WindCenkPoses")]
    public class FloodWindCenkPoses : ScriptableObject
    {
        public const int MaxShear = 6;
        public const int LeanCount = MaxShear * 2; // 0 hariç: -6..-1, 1..6

        [Serializable]
        public class Entry
        {
            public Sprite source;
            public CenkOutfit.Look look;
            public IsoCharacterAnimator.Direction direction;
            public int frame;
            [Tooltip("Sıra: -MaxShear..-1, 1..MaxShear.")]
            public Sprite[] lean = Array.Empty<Sprite>();
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<Sprite, Entry> bySource;
        private Dictionary<Sprite, Entry> byLean;

        public Entry[] Entries
        {
            get => entries;
            set { entries = value ?? Array.Empty<Entry>(); bySource = null; }
        }

        private void OnEnable() => bySource = null;

        private void Build()
        {
            bySource = new Dictionary<Sprite, Entry>();
            byLean = new Dictionary<Sprite, Entry>();
            foreach (var e in entries)
            {
                if (e == null || e.source == null) continue;
                bySource[e.source] = e;
                foreach (var s in e.lean) if (s != null) byLean[s] = e;
            }
        }

        /// <summary>Gösterilen kare (yaslanmış hali olabilir) için asıl yürüme karesi; tanınmıyorsa kendisi.</summary>
        public Sprite SourceOf(Sprite current)
        {
            if (current == null) return null;
            if (bySource == null) Build();
            return byLean.TryGetValue(current, out var e) ? e.source : current;
        }

        /// <summary>Karenin shear piksel yaslanmış hali; kare tanınmıyorsa olduğu gibi döner.</summary>
        public Sprite Lean(Sprite current, int shear)
        {
            if (current == null) return null;
            if (bySource == null) Build();
            var src = SourceOf(current);
            shear = Mathf.Clamp(shear, -MaxShear, MaxShear);
            if (shear == 0 || !bySource.TryGetValue(src, out var e) || e.lean.Length < LeanCount) return src;
            var s = e.lean[shear < 0 ? shear + MaxShear : shear + MaxShear - 1];
            return s != null ? s : src;
        }

        /// <summary>Görünüm + yön + kare için yürüme karesi (yoksa okul çantalı).</summary>
        public Sprite Frame(CenkOutfit.Look look, IsoCharacterAnimator.Direction dir, int frame)
        {
            Sprite fallback = null;
            foreach (var e in entries)
            {
                if (e == null || e.direction != dir || e.frame != frame) continue;
                if (e.look == look) return e.source;
                if (e.look == CenkOutfit.Look.SchoolBag) fallback = e.source;
            }
            return fallback;
        }
    }
}
