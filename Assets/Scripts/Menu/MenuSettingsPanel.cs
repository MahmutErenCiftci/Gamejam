using System;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Menu
{
    /// <summary>
    /// Ana menü ayarları: Ses (%0–100, 10'ar), Tam ekran (Açık/Kapalı), Geri.
    /// W/S satır, A/D ses, Enter/Space değiştir/seç, Esc kapat. Fareyle satıra tıklamak: ses satırında
    /// sol yarı azaltır, sağ yarı artırır. Değerler PlayerPrefs'te saklanır (AudioListener.volume oyunun her yerinde geçerli).
    /// </summary>
    public class MenuSettingsPanel : MonoBehaviour
    {
        public const string VolumeKey = "AfetUyanisi.Volume";

        [SerializeField] private CanvasGroup group;
        [SerializeField] private MenuListItem volumeRow;
        [SerializeField] private MenuListItem fullscreenRow;
        [SerializeField] private MenuListItem backRow;

        public event Action Closed;
        public bool IsOpen { get; private set; }

        private MenuListItem[] rows;
        private int focus;
        private float lastY;

        /// <summary>Kayıtlı ses seviyesini uygular (menü açılışında çağrılır).</summary>
        public static void ApplySaved() => AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);

        private void Awake()
        {
            rows = new[] { volumeRow, fullscreenRow, backRow };
            foreach (MenuListItem row in rows)
            {
                if (row == null) continue;
                row.Hovered += r => Focus(Array.IndexOf(rows, r));
                row.Clicked += OnClicked;
            }
            SetVisible(false);
        }

        public void Open()
        {
            IsOpen = true;
            SetVisible(true);
            Focus(0);
            Refresh();
            lastY = 0f;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SetVisible(false);
            Closed?.Invoke();
        }

        /// <summary>MainMenuScreen her kare çağırır (panel açıkken).</summary>
        public void Tick()
        {
            if (!IsOpen) return;
            float y = GameInput.MoveVector.y;
            bool up = y > 0.5f && lastY <= 0.5f, down = y < -0.5f && lastY >= -0.5f;
            lastY = y;

            if (GameInput.BackPressed) { Close(); return; }
            if (up) Focus((focus + rows.Length - 1) % rows.Length);
            if (down) Focus((focus + 1) % rows.Length);
            if (focus == 0)
            {
                if (GameInput.LeftPressed) ChangeVolume(-0.1f);
                if (GameInput.RightPressed) ChangeVolume(0.1f);
            }
            if (GameInput.ConfirmPressed) Activate(focus, 1);
        }

        private void OnClicked(MenuListItem row)
        {
            int i = Array.IndexOf(rows, row);
            int dir = 1;
            if (i == 0)
            {
                // Ses satırında tıklanan yarı yönü belirler.
                var rt = (RectTransform)row.transform;
                Vector2 local;
                Vector2 mouse = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, mouse, null, out local))
                    dir = local.x < rt.rect.center.x ? -1 : 1;
            }
            Focus(i);
            Activate(i, dir);
        }

        private void Activate(int i, int dir)
        {
            switch (i)
            {
                case 0: ChangeVolume(0.1f * dir); break;
                case 1:
                    bool next = !CurrentFullscreen;
                    fullscreenOverride = next;
                    Screen.fullScreen = next;
                    Refresh();
                    break;
                default: Close(); break;
            }
        }

        // Editörde Screen.fullScreen değişmez; yazının yine de tepki vermesi için son seçimi tutarız.
        private bool? fullscreenOverride;
        private bool CurrentFullscreen => fullscreenOverride ?? Screen.fullScreen;

        private void ChangeVolume(float delta)
        {
            float v = Mathf.Clamp01(Mathf.Round((AudioListener.volume + delta) * 10f) / 10f);
            AudioListener.volume = v;
            PlayerPrefs.SetFloat(VolumeKey, v);
            PlayerPrefs.Save();
            Refresh();
        }

        private void Refresh()
        {
            if (volumeRow != null) volumeRow.Text = $"Ses   <  %{Mathf.RoundToInt(AudioListener.volume * 100f)}  >";
            if (fullscreenRow != null) fullscreenRow.Text = "Tam ekran   " + (CurrentFullscreen ? "Açık" : "Kapalı");
            if (backRow != null) backRow.Text = "Geri";
        }

        private void Focus(int i)
        {
            if (i < 0) return;
            focus = i;
            for (int k = 0; k < rows.Length; k++)
                if (rows[k] != null) rows[k].SetFocused(k == focus);
        }

        private void SetVisible(bool visible)
        {
            if (group == null) { gameObject.SetActive(visible); return; }
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }
    }
}
