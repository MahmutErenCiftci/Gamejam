using System.Collections.Generic;
using System.Text;
using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// ŞİFRE PANELİ mantığı (ENV çapası `CodeDoor`'a eklenir; arayüz ayrı prefab: FE_CodePanelView).
    /// [E] Şifre panelini kullan → 4 haneli tuş takımı. Fare: rakam düğmeleri, Sil, Onayla, Kapat.
    /// Klavye: 0–9 (üst sıra / numpad), Backspace/Delete = Sil, Enter (ya da Space) = Onayla, Esc = Kapat.
    /// Karede en fazla bir işlem (fare + klavye aynı karede iki kez yazmaz). Panel açıkken yürüme kilitli.
    /// Yanlış kod: açıklayıcı mesaj + Hub.ReportMistake("wrong_code") (sıfırlamaya kadar bir kez, bilgi amaçlı);
    /// kilitlenme/bekleme yok. Doğru kod (varsayılan 4725): `CodeDoor` açılır, Hub.Complete("code_opened").
    /// Panelin sağ sütunu aynı sahnedeki CodeClueSign levhalarından OKUNMUŞ olanları listeler.
    /// Girilen rakamlar panel kapatılıp açılınca korunur; ResetModule her şeyi sıfırlar.
    /// </summary>
    public class CodePanelMinigame : FloodPuzzleDoorBase
    {
        public const string MinigameId = "code";
        public const string CompletedStepId = "code_opened";
        public const string WrongCodeMistakeId = "wrong_code";
        public const string DefaultCode = "4725";

        [Header("Arayüz")]
        [Tooltip("FE_CodePanelView prefab'ı (ya da sahnedeki örneği). Boşsa aynı düzen kodla kurulur.")]
        [SerializeField] private CodePanelView view;

        [Header("Şifre")]
        [Tooltip("Doğru kod (yalnızca rakam, 4 hane). İpucu levhaları (CodeClueSign) da aynı kodu kullanmalı.")]
        [SerializeField] private string code = DefaultCode;
        [Tooltip("Boşsa sahnedeki bütün CodeClueSign'lar.")]
        [SerializeField] private CodeClueSign[] clues = new CodeClueSign[0];
        [Tooltip("Doğru koddan sonra panelin açık kalacağı süre (sn, gerçek zaman).")]
        [SerializeField, Min(0f)] private float successHoldSeconds = 0.9f;

        private readonly StringBuilder entry = new StringBuilder();
        private CodePanelView viewInstance;
        private float successTimer = -1f;
        private bool showingWrong;
        private bool wrongReported;
        private bool closeRequested;
        private int lastActionFrame = -1;
        private string message;
        private CodePanelView.Tone tone;

        /// <summary>Kaç yanlış deneme yapıldı (sıfırlamaya kadar).</summary>
        public int WrongAttempts { get; private set; }

        /// <summary>Şu an girilmiş rakamlar (test/sürücü için).</summary>
        public string Entry => entry.ToString();

        public string Code => code;

        protected override string HubId => MinigameId;
        protected override string StepId => CompletedStepId;
        protected override string DefaultLabel => "Şifre panelini kullan";

        private int Length => string.IsNullOrEmpty(code) ? 4 : code.Length;

        private CodePanelView View
        {
            get
            {
                if (viewInstance == null)
                {
                    FloodPuzzleViewBase.Resolve(view, ref viewInstance, this, "FE_CodePanelView");
                    viewInstance.DigitClicked += PressDigit;
                    viewInstance.DeleteClicked += PressDelete;
                    viewInstance.ConfirmClicked += PressConfirm;
                    viewInstance.CloseClicked += () => { if (IsOpen) closeRequested = true; };
                }
                return viewInstance;
            }
        }

        // ---------------------------------------------------------------- akış

        protected override void ShowView()
        {
            successTimer = -1f;
            closeRequested = false;
            var v = View;
            v.Show();
            if (message == null || (!showingWrong && entry.Length == 0)) SetMessage(v.MsgStart, CodePanelView.Tone.Info);
            RefreshNotes();
            Render();
        }

        protected override void HideView()
        {
            if (viewInstance != null) viewInstance.Hide();
        }

        protected override bool Tick(float dt, float unscaledDt)
        {
            var v = View;
            v.TickAppear(unscaledDt);
            v.TickShake(unscaledDt);

            if (successTimer >= 0f)
            {
                successTimer += unscaledDt;
                return successTimer < successHoldSeconds;
            }
            if (closeRequested || GameInput.BackPressed) return false;

            if (lastActionFrame != Time.frameCount)
            {
                int digit = GameInput.DigitPressed;
                if (digit >= 0) PressDigit(digit);
                else if (GameInput.DeletePressed) PressDelete();
                else if (GameInput.ConfirmPressed) PressConfirm();
            }
            return true;
        }

        protected override void ResetState()
        {
            base.ResetState();
            entry.Length = 0;
            WrongAttempts = 0;
            wrongReported = false;
            showingWrong = false;
            successTimer = -1f;
            message = null;
        }

        // ---------------------------------------------------------------- tuşlar (fare ve klavye aynı yoldan)

        private bool TakeAction()
        {
            if (!IsOpen || successTimer >= 0f || lastActionFrame == Time.frameCount) return false;
            lastActionFrame = Time.frameCount;
            return true;
        }

        /// <summary>Bir rakam ekler (fare düğmesi ya da klavye).</summary>
        public void PressDigit(int digit)
        {
            if (digit < 0 || digit > 9 || !TakeAction()) return;
            var v = View;
            if (showingWrong) { entry.Length = 0; showingWrong = false; }
            if (entry.Length >= Length)
            {
                SetMessage(v.Overflow(Length), CodePanelView.Tone.Warn);
                Render();
                return;
            }
            entry.Append((char)('0' + digit));
            if (entry.Length == Length) SetMessage(v.MsgFull, CodePanelView.Tone.Ok);
            else SetMessage(v.Progress(entry.Length, Length), CodePanelView.Tone.Info);
            Render();
        }

        public void PressDelete()
        {
            if (!TakeAction()) return;
            var v = View;
            if (showingWrong) { entry.Length = 0; showingWrong = false; }
            else if (entry.Length > 0) entry.Length--;
            SetMessage(entry.Length == 0 ? v.MsgStart : v.Progress(entry.Length, Length), CodePanelView.Tone.Info);
            Render();
        }

        public void PressConfirm()
        {
            if (!TakeAction()) return;
            var v = View;
            if (showingWrong)
            {
                SetMessage(v.MsgAfterWrong, CodePanelView.Tone.Warn);
                Render();
                return;
            }
            if (entry.Length < Length)
            {
                SetMessage(v.TooShort(Length, entry.Length), CodePanelView.Tone.Warn);
                v.Shake(0.2f);
                Render();
                return;
            }

            string tried = entry.ToString();
            if (tried == code)
            {
                SetMessage(v.MsgSuccess, CodePanelView.Tone.Success);
                successTimer = 0f;
                Render();
                OpenDoorPermanently();
                return;
            }

            WrongAttempts++;
            showingWrong = true;
            v.Shake(0.35f);
            CountClues(out int read, out int total);
            SetMessage(v.Wrong(tried, read >= total), CodePanelView.Tone.Error);
            Debug.Log($"[CodePanelMinigame] yanlış kod {tried} (deneme {WrongAttempts}, okunan levha {read}/{total})", this);
            if (!wrongReported)
            {
                wrongReported = true;
                FloodEveningHub.ReportMistake(WrongCodeMistakeId);
            }
            Render();
        }

        // ---------------------------------------------------------------- ipuçları / çizim

        private IEnumerable<CodeClueSign> Clues()
        {
            if (clues != null && clues.Length > 0)
            {
                foreach (var c in clues) if (c != null) yield return c;
                yield break;
            }
            foreach (var c in FindObjectsByType<CodeClueSign>(FindObjectsInactive.Exclude))
                if (c.gameObject.scene == gameObject.scene) yield return c;
        }

        private void CountClues(out int read, out int total)
        {
            read = total = 0;
            foreach (var c in Clues()) { total++; if (c.HasBeenRead) read++; }
        }

        private void RefreshNotes()
        {
            var known = new SortedDictionary<int, char>();
            int read = 0, total = 0;
            foreach (var c in Clues())
            {
                total++;
                if (!c.HasBeenRead) continue;
                read++;
                foreach (var kv in c.Entries()) known[kv.Key] = kv.Value;
            }
            View.SetNotes(known, read, total);
        }

        private void SetMessage(string text, CodePanelView.Tone t)
        {
            message = text;
            tone = t;
        }

        private void Render()
        {
            if (viewInstance == null) return;
            var state = successTimer >= 0f || Opened ? CodePanelView.SlotState.Success
                : showingWrong ? CodePanelView.SlotState.Wrong : CodePanelView.SlotState.Normal;
            viewInstance.RenderEntry(entry.ToString(), Length, state);
            viewInstance.SetMessage(message, tone);
        }
    }
}
