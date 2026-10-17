using AfetUyanisi.Shared;
using AfetUyanisi.UI;
using UnityEngine;

namespace AfetUyanisi.Chapters.TempUI
{
    /// <summary>Mevcut sahne referanslarını koruyarak ortak etkileşim prefabına bağlanır.</summary>
    public class TempInteractionHint : InteractionHintBase
    {
        private InteractionPromptUI view;

        private void Awake()
        {
            var prefab = Resources.Load<InteractionPromptUI>("InteractionUI/InteractionPromptUI");
            if (prefab != null) view = Instantiate(prefab, transform);
            else Debug.LogError("InteractionPromptUI prefabı eksik. Afet Uyanisi > Interaction UI > Create or update assets çalıştırın.", this);
        }

        public override void Show(string keyLabel, string actionLabel)
        {
            if (view != null) view.Show(keyLabel, actionLabel);
        }

        public override void Show(string keyLabel, string actionLabel, Transform anchor, Vector3 worldOffset)
        {
            if (view != null) view.Show(keyLabel, actionLabel, anchor, worldOffset);
        }

        public override void Press() { if (view != null) view.Press(); }
        public override void Hide() { if (view != null) view.Hide(); }
        private void OnDisable() { if (view != null) view.HideImmediate(); }
    }
}
