using System;
using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters.Rooms
{
    /// <summary>
    /// Odada serbest dolaşma döngüsü (Managers objesinde). Açıkken ve oyuncu yürüyebilirken her karede oyuncuya
    /// en yakın, kullanılabilir (Available) ve açık Interactable'ı bulur (kendi useRadius'u içinde, izometrik zemin
    /// uzaklığı); "[E] etiket" ipucunu gösterir, yoksa gizler. E'ye basılınca:
    /// hareket kapanır → Used/onUsed → Register ile verilen "before" coroutine → dialogueId diyaloğu →
    /// oneShot ise nokta kapanır → "after" coroutine → (döngü hâlâ açıksa) hareket açılır.
    /// Oda akışı (BedroomIntroController, BathroomController) Configure ile oyuncu/diyalog/ipucu verir.
    /// </summary>
    public class RoomInteractor : MonoBehaviour
    {
        private sealed class Handler
        {
            public Func<IEnumerator> Before;
            public Func<IEnumerator> After;
        }

        [Tooltip("Sahnedeki çocuk. Oda akışı Configure ile de verir.")]
        [SerializeField] private IsoPlayerController player;
        [Tooltip("\"[E] ...\" ipucu. Oda akışı Configure ile de verir.")]
        [SerializeField] private InteractionHintBase hint;
        [Tooltip("Diyalog oynatıcısı (DialogueRunnerAdapter). Oda akışı Configure ile de verir.")]
        [SerializeField] private DialogueRunnerBase dialogue;
        [Tooltip("İsteğe bağlı: diyalog oynatıcısı yoksa satırlar bu JSON'dan Console'a yazılır (Assets/Data/Story/rooms.json)")]
        [SerializeField] private TextAsset storyJson;
        [Tooltip("Boşsa sahnedeki bütün Interactable'lar (kapalı olanlar dahil) ilk karede toplanır.")]
        [SerializeField] private Interactable[] interactables = Array.Empty<Interactable>();
        [Tooltip("Sahne başında döngü açık mı? Normalde kapalı; oda akışı açılıştan sonra açar.")]
        [SerializeField] private bool interactionEnabled;

        private readonly List<Interactable> points = new List<Interactable>();
        private readonly Dictionary<Interactable, Handler> handlers = new Dictionary<Interactable, Handler>();
        private Shared.StoryData story;
        private bool collected;
        private Interactable current;
        private int ignoreInputUntilFrame;

        /// <summary>Şu an bir nokta kullanılıyor (diyalog/özel coroutine sürüyor).</summary>
        public bool IsBusy { get; private set; }

        /// <summary>Şu an bir diyalog balonu oynuyor (Say). Görev paneli bu sırada gizlenir.</summary>
        public bool IsSaying { get; private set; }

        /// <summary>İpucu gösterilen en yakın nokta (yoksa null).</summary>
        public Interactable Current => current;

        /// <summary>Herhangi bir nokta kullanıldığında (Used/onUsed ile aynı anda) tetiklenir.</summary>
        public event Action<Interactable> Used;

        /// <summary>true iken diyaloglar oynatılmaz, satırlar Console'a yazılır (otomatik test).</summary>
        public bool AutoAdvance { get; set; }

        /// <summary>Serbest dolaşma döngüsü açık mı? Kapatınca ipucu ve vurgu hemen gizlenir.</summary>
        public bool InteractionEnabled
        {
            get => interactionEnabled;
            set
            {
                interactionEnabled = value;
                if (!value) ClearCurrent();
            }
        }

        /// <summary>Oda akışının referansları vermesi için. null verilen alan Inspector'daki değerini korur.</summary>
        public void Configure(IsoPlayerController playerController, DialogueRunnerBase dialogueRunner,
            InteractionHintBase interactionHint, TextAsset fallbackStoryJson = null)
        {
            if (playerController != null) player = playerController;
            if (dialogueRunner != null) dialogue = dialogueRunner;
            if (interactionHint != null) hint = interactionHint;
            if (fallbackStoryJson != null)
            {
                storyJson = fallbackStoryJson;
                story = null;
            }
        }

        /// <summary>
        /// Noktaya özel coroutine bağlar. before: Used'dan hemen sonra, diyalogdan önce (ör. alarmı kapat, kapıdan çık).
        /// after: diyalog ve oneShot'tan sonra (ör. lavabodan sonra mutfak kapısını aç). Aynı noktaya tekrar çağrılırsa değiştirir.
        /// </summary>
        public void Register(Interactable target, Func<IEnumerator> before = null, Func<IEnumerator> after = null)
        {
            if (target == null) return;
            handlers[target] = new Handler { Before = before, After = after };
            if (!points.Contains(target)) points.Add(target);
        }

        private void Start()
        {
            CollectPoints();
        }

        private void CollectPoints()
        {
            if (collected) return;
            collected = true;

            if (interactables != null && interactables.Length > 0)
            {
                foreach (var i in interactables)
                    if (i != null && !points.Contains(i)) points.Add(i);
                return;
            }

            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var i in root.GetComponentsInChildren<Interactable>(true))
                    if (!points.Contains(i)) points.Add(i);
        }

        private void Update()
        {
            if (IsBusy) return;

            var best = CanInteract() ? FindNearest() : null;
            if (best != current)
            {
                if (current != null) current.SetHighlighted(false);
                current = best;
                if (current != null)
                {
                    current.SetHighlighted(true);
                    string label = string.IsNullOrEmpty(current.ActionLabel) ? current.name : current.ActionLabel;
                    if (hint != null) hint.Show(current.KeyLabel, label, current.PromptAnchor, current.PromptOffset);
                    else Debug.Log($"[RoomInteractor]   [{current.KeyLabel}] {label}", current);
                }
                else if (hint != null) hint.Hide();
            }

            // Etiketi evre/görev değiştirebilir; aynı hedefte eski yazı kalmasın.
            if (current != null && hint != null)
                hint.Show(current.KeyLabel, string.IsNullOrEmpty(current.ActionLabel) ? current.name : current.ActionLabel,
                    current.PromptAnchor, current.PromptOffset);

            if (current != null && Time.frameCount > ignoreInputUntilFrame && GameInput.InteractPressed)
                StartCoroutine(Use(current));
        }

        private bool CanInteract() =>
            interactionEnabled && !IsSaying && player != null && player.isActiveAndEnabled && player.CanMove;

        private Interactable FindNearest()
        {
            CollectPoints();
            Interactable best = null;
            float bestDistance = float.MaxValue;
            foreach (var p in points)
            {
                if (p == null || !p.Available || !p.isActiveAndEnabled) continue;
                if (!Visibility.RoomVisibilityManager.AllowsInteraction(p)) continue; // karanlık (aktif olmayan) odadaki nokta
                float d = player.GroundDistanceTo(p.transform.position);
                if (d <= p.UseRadius && d < bestDistance)
                {
                    best = p;
                    bestDistance = d;
                }
            }
            return best;
        }

        private void ClearCurrent()
        {
            if (current != null) current.SetHighlighted(false);
            current = null;
            if (hint != null) hint.Hide();
        }

        /// <summary>
        /// Noktayı kullanır (E'ye basılınca bu çalışır; otomatik testte oda akışı doğrudan çağırabilir).
        /// Nokta kapalıysa ya da başka bir kullanım sürüyorsa hiçbir şey yapmaz.
        /// </summary>
        public IEnumerator Use(Interactable target)
        {
            if (target == null || IsBusy) yield break;
            if (!target.Available || !target.gameObject.activeInHierarchy)
            {
                Debug.Log($"[RoomInteractor] {target.name} kullanılamaz, atlandı.", target);
                yield break;
            }

            IsBusy = true;
            if (hint != null) hint.Press();
            ClearCurrent();
            SetPlayerControl(false);
            Debug.Log($"[RoomInteractor] {Time.timeSinceLevelLoad:0.0} sn → {target.name} ({target.ActionLabel})", target);

            target.RaiseUsed();
            Used?.Invoke(target);

            handlers.TryGetValue(target, out var handler);
            if (handler != null && handler.Before != null)
                yield return handler.Before();

            string id = target.AppendChapterSuffix ? RoomChapter.WithSuffix(target.DialogueId) : target.DialogueId;
            if (!string.IsNullOrEmpty(id))
                yield return Say(id);

            if (target.OneShot) target.SetAvailable(false);

            if (handler != null && handler.After != null)
                yield return handler.After();

            // Kapıdan çıkarken döngü kapatıldıysa oyuncu yürümesin.
            if (interactionEnabled) SetPlayerControl(true);
            ignoreInputUntilFrame = Time.frameCount + 1; // diyaloğu kapatan E yeni bir kullanımı başlatmasın
            IsBusy = false;
        }

        /// <summary>
        /// Story JSON sahnesini oynatır (konuşma balonu). Diyalog oynatıcısı yoksa ya da AutoAdvance açıksa
        /// satırlar Console'a yazılır ve beklemeden geçilir.
        /// </summary>
        public IEnumerator Say(string storySceneId)
        {
            if (string.IsNullOrEmpty(storySceneId)) yield break;

            if (dialogue != null && !AutoAdvance)
            {
                IsSaying = true;
                try { yield return dialogue.Play(storySceneId); }
                finally { IsSaying = false; }
                yield break;
            }

            var lines = FindLines(storySceneId);
            if (lines == null)
                Debug.Log($"[RoomInteractor] (diyalog yok) {storySceneId}");
            else
                foreach (var line in lines)
                    Debug.Log($"[RoomInteractor]   {storySceneId} · {line.Key}: {line.Value}");
            yield return null;
        }

        // Console'a yazmak için satırlar: önce storyJson, yoksa bağlı DialogueRunner'ın verisi.
        private List<KeyValuePair<string, string>> FindLines(string id)
        {
            if (story == null && storyJson != null)
                story = JsonUtility.FromJson<Shared.StoryData>(storyJson.text);

            var result = new List<KeyValuePair<string, string>>();
            var scene = story != null ? story.FindScene(id) : null;
            if (scene != null && scene.lines != null)
            {
                foreach (var l in scene.lines) result.Add(new KeyValuePair<string, string>(l.speaker, l.text));
                return result;
            }

            var adapter = dialogue as DialogueRunnerAdapter;
            if (adapter != null && adapter.Runner != null)
            {
                var data = adapter.Runner.Data;
                var s = data != null && data.scenes != null ? Array.Find(data.scenes, x => x.id == id) : null;
                if (s != null && s.lines != null)
                {
                    foreach (var l in s.lines) result.Add(new KeyValuePair<string, string>(l.speaker, l.text));
                    return result;
                }
            }
            return null;
        }

        private void SetPlayerControl(bool canMove)
        {
            if (player != null) player.CanMove = canMove;
        }

        private void OnDisable() => ClearCurrent();
    }
}
