using System.Collections.Generic;
using System.Text;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Şifre ipucu levhası (ENV çapaları `Clues/Clue1..Clue3`'e FE_ClueSign_N prefab'ı olarak konur; kart arayüzü
    /// ayrı prefab: FE_ClueCardView). [E] İpucu levhasını oku (n/3) → kart: "1. hane: 4" gibi HANE + RAKAM (tek anlamlı).
    /// Kart E / Space / Enter / tık / Esc ile kapanır; levha tekrar tekrar okunabilir (IsDone hep false).
    /// Rakamlar `code`dan türetilir (varsayılan 4725): levha 1 → 1. hane, levha 2 → 2. ve 3. hane, levha 3 → 4. hane.
    /// Alt satır: storyJson'da footerSceneId sahnesinin ilk satırı varsa o, yoksa Inspector'daki footer.
    /// Okunan levhalar şifre panelinin sağ sütununda listelenir.
    /// </summary>
    public class CodeClueSign : FloodPuzzleInteractionBase
    {
        public const string MinigameId = "clue";

        [Header("Arayüz")]
        [Tooltip("FE_ClueCardView prefab'ı (ya da sahnedeki örneği). Boşsa aynı düzen kodla kurulur.")]
        [SerializeField] private ClueCardView view;

        [Header("İpucu")]
        [Tooltip("Levha numarası (1..clueCount).")]
        [SerializeField, Min(1)] private int clueIndex = 1;
        [SerializeField, Min(1)] private int clueCount = 3;
        [Tooltip("Şifre panelindeki kodla aynı olmalı.")]
        [SerializeField] private string code = CodePanelMinigame.DefaultCode;
        [Tooltip("Bu levhanın verdiği haneler (1'den başlar). Boşsa: levha 1 → {1}, levha 2 → {2,3}, levha 3 → {4}.")]
        [SerializeField] private int[] positions = new int[0];
        [Tooltip("{0} = hane, {1} = rakam")]
        [SerializeField] private string lineFormat = "{0}. hane: <color=#F6C67D>{1}</color>";
        [Tooltip("Kartın alt satırı (storyJson'da footerSceneId bulunamazsa).")]
        [SerializeField, TextArea] private string footer = "";
        [Tooltip("İsteğe bağlı: Assets/Data/Story/flood_evening.json")]
        [SerializeField] private TextAsset storyJson;
        [Tooltip("İsteğe bağlı: alt satırın story ID'si (ör. FE_CLUE_1). Bulunursa footer'ın yerine geçer.")]
        [SerializeField] private string footerSceneId = "";
        [Tooltip("{0} = levha numarası, {1} = toplam. [E] ipucundaki yazı (base'deki actionLabel boşsa).")]
        [SerializeField] private string labelFormat = "İpucu levhasını oku ({0}/{1})";

        private ClueCardView viewInstance;
        private bool closeRequested;
        private string resolvedFooter;

        /// <summary>Bu levha en az bir kez okundu mu (ResetModule sıfırlar).</summary>
        public bool HasBeenRead { get; private set; }

        public int ClueIndex => clueIndex;

        public override bool IsDone => false; // tekrar okunabilir

        protected override string HubId => MinigameId;
        protected override string DefaultLabel => string.Format(labelFormat, clueIndex, clueCount);

        /// <summary>Bu levhanın (hane, rakam) çiftleri.</summary>
        public IEnumerable<KeyValuePair<int, char>> Entries()
        {
            foreach (int p in Positions())
                if (!string.IsNullOrEmpty(code) && p >= 1 && p <= code.Length)
                    yield return new KeyValuePair<int, char>(p, code[p - 1]);
        }

        private int[] Positions()
        {
            if (positions != null && positions.Length > 0) return positions;
            switch (clueIndex)
            {
                case 1: return new[] { 1 };
                case 2: return new[] { 2, 3 };
                case 3: return new[] { 4 };
                default: return clueIndex <= (code?.Length ?? 0) ? new[] { clueIndex } : new int[0];
            }
        }

        private string Footer()
        {
            if (resolvedFooter != null) return resolvedFooter;
            resolvedFooter = footer ?? "";
            if (storyJson != null && !string.IsNullOrEmpty(footerSceneId))
            {
                try
                {
                    var data = JsonUtility.FromJson<StoryData>(storyJson.text);
                    var scene = data != null ? data.FindScene(footerSceneId) : null;
                    if (scene != null && scene.lines != null && scene.lines.Length > 0 && !string.IsNullOrEmpty(scene.lines[0].text))
                        resolvedFooter = scene.lines[0].text;
                }
                catch (System.Exception e) { Debug.LogWarning($"[CodeClueSign] {footerSceneId} okunamadı: {e.Message}", this); }
            }
            return resolvedFooter;
        }

        protected override void ShowView()
        {
            HasBeenRead = true;
            closeRequested = false;
            if (viewInstance == null)
            {
                FloodPuzzleViewBase.Resolve(view, ref viewInstance, this, "FE_ClueCardView");
                viewInstance.OkClicked += () => { if (IsOpen) closeRequested = true; };
            }
            var sb = new StringBuilder();
            int lines = 0;
            foreach (var kv in Entries())
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(string.Format(lineFormat, kv.Key, kv.Value));
                lines++;
            }
            viewInstance.Show(clueIndex, clueCount, sb.ToString(), lines, Footer());
        }

        protected override void HideView()
        {
            if (viewInstance != null) viewInstance.Hide();
        }

        protected override bool Tick(float dt, float unscaledDt)
        {
            viewInstance.TickAppear(unscaledDt);
            if (closeRequested) return false;
            return !(GameInput.ContinuePressed || GameInput.BackPressed);
        }

        protected override void ResetState()
        {
            HasBeenRead = false;
        }
    }
}
