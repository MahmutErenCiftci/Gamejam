using System;
using System.Collections;
using AfetUyanisi.Chapters.TempUI;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;

namespace AfetUyanisi.Chapters.Rooms
{
    /// <summary>
    /// Oda sahnelerinin (Bedroom, Bathroom) ortak altyapısı (Mahmut). Ekip parçaları Inspector'dan bağlanır;
    /// ipucu/kararma bağlı değilse geçicileri (TempUI) oluşturulur, RoomInteractor yoksa bu objeye eklenir.
    /// Bölüm: GameSession.CurrentChapter (seçilmediyse Sel, bkz. RoomChapter).
    /// </summary>
    public abstract class RoomControllerBase : MonoBehaviour
    {
        [Header("Oyuncu")]
        [Tooltip("Sahnedeki çocuk (IsoPlayerController + DialogueSpeaker \"Çocuk\")")]
        [SerializeField] private IsoPlayerController player;

        [Header("Ekip parçaları")]
        [Tooltip("DialogueRunnerAdapter (DialogueRunner prefabının storyJson'u = Assets/Data/Story/rooms.json)")]
        [SerializeField] private DialogueRunnerBase dialogue;
        [Tooltip("Boşsa TempInteractionHint oluşturulur")]
        [SerializeField] private InteractionHintBase hint;
        [Tooltip("Boşsa TempScreenFader oluşturulur")]
        [SerializeField] private ScreenFaderBase fader;
        [Tooltip("Boşsa bu objeye eklenir")]
        [SerializeField] private RoomInteractor interactor;
        [Tooltip("İsteğe bağlı: diyalog oynatıcısı yokken satırlar Console'a buradan yazılır (rooms.json)")]
        [SerializeField] private TextAsset storyJson;

        [Header("Hata ayıklama")]
        [Tooltip("İşaretliyse açılış tuş beklemeden ilerler, diyaloglar oynatılmaz (Console'a yazılır).")]
        [SerializeField] private bool debugAutoAdvance;
        [Tooltip("debugAutoAdvance ile birlikte: açılıştan sonra odadaki noktaları sırayla kendisi kullanır (kapı dahil).")]
        [SerializeField] private bool debugAutoTour;

        protected IsoPlayerController Player => player;
        protected InteractionHintBase Hint => hint;
        protected RoomInteractor Interactor => interactor;
        protected bool AutoAdvance => debugAutoAdvance;
        protected bool AutoTour => debugAutoAdvance && debugAutoTour;

        /// <summary>Console satırlarının ön eki: "Bedroom", "Bathroom".</summary>
        protected abstract string LogTag { get; }

        /// <summary>Odanın baştan sona akışı.</summary>
        protected abstract IEnumerator RunRoom();

        protected virtual void Awake()
        {
            RoomChapter.EnsureChapter();

            if (hint == null) hint = new GameObject("TempInteractionHint").AddComponent<TempInteractionHint>();
            if (fader == null) fader = new GameObject("TempScreenFader").AddComponent<TempScreenFader>();
            if (interactor == null) interactor = gameObject.AddComponent<RoomInteractor>();

            interactor.Configure(player, dialogue, hint, storyJson);
            interactor.AutoAdvance = debugAutoAdvance;
            interactor.InteractionEnabled = false;
            SetPlayerControl(false);
        }

        protected virtual void Start()
        {
            StartCoroutine(RunRoom());
        }

        protected void Log(string message) =>
            Debug.Log($"[{LogTag}] {Time.timeSinceLevelLoad:0.0} sn → {message}", this);

        protected void SetPlayerControl(bool canMove)
        {
            if (player != null) player.CanMove = canMove;
        }

        protected void SetPlayerVisible(bool visible)
        {
            if (player != null) player.gameObject.SetActive(visible);
        }

        /// <summary>Oyuncuyu gösterir ve noktaya koyar (nokta yoksa yerinde kalır, sadece döner).</summary>
        protected void PlacePlayer(Transform spawn, Vector2 facing)
        {
            if (player == null) return;
            player.gameObject.SetActive(true);
            Vector3 position = spawn != null ? spawn.position : player.transform.position;
            player.PlaceAt(position, facing);
        }

        /// <summary>Serbest dolaşmayı açar/kapatır: hareket + en yakın noktanın ipucu.</summary>
        protected void SetFreeRoam(bool on)
        {
            interactor.InteractionEnabled = on;
            SetPlayerControl(on);
        }

        protected IEnumerator Say(string storySceneId) => interactor.Say(storySceneId);

        /// <summary>"[E] label" ipucunu gösterir ve E'ye basılana kadar bekler (oyuncunun yerinden bağımsız).</summary>
        protected IEnumerator WaitForInteract(string label)
        {
            ShowHint("E", label);
            yield return null; // önceki adımda basılan tuş bu adımı da geçirmesin
            while (!debugAutoAdvance && !GameInput.InteractPressed)
                yield return null;
            HideHint();
        }

        protected void ShowHint(string keyLabel, string actionLabel)
        {
            if (hint != null) hint.Show(keyLabel, actionLabel);
        }

        protected void HideHint()
        {
            if (hint != null) hint.Hide();
        }

        protected void SetBlack()
        {
            if (fader != null) fader.SetOpaque(true);
        }

        protected IEnumerator FadeIn(float seconds)
        {
            if (fader == null) yield break;
            // Sahne yüklendikten sonraki ilk karelerin süresi çok uzun olabiliyor (yükleme takılması);
            // kararma tek karede bitmesin diye kare süreleri oturana kadar beklenir.
            yield return WaitForSteadyFrames();
            yield return fader.FadeIn(seconds);
        }

        private static IEnumerator WaitForSteadyFrames()
        {
            int steady = 0;
            for (int i = 0; i < 30 && steady < 2; i++)
            {
                yield return null;
                steady = Time.unscaledDeltaTime < 0.1f ? steady + 1 : 0;
            }
        }

        protected IEnumerator FadeOut(float seconds)
        {
            if (fader != null) yield return fader.FadeOut(seconds);
        }

        /// <summary>
        /// Kapıdan başka bir oda sahnesine geçer: döngü kapanır, ekran kararır, GameSceneRouter.LoadRoom.
        /// Sahne Build Settings'te yoksa kararmaz, hata yazar ve odada kalınır.
        /// </summary>
        protected IEnumerator ExitTo(string sceneName, float fadeSeconds)
        {
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[{LogTag}] '{sceneName}' sahnesi Build Settings listesinde yok; kapı çalışmadı.", this);
                yield break;
            }

            Log($"kapı → {sceneName}");
            SetFreeRoam(false);
            yield return FadeOut(fadeSeconds);
            GameSceneRouter.LoadRoom(sceneName);
        }

        /// <summary>Noktayı kurar (etiket, diyalog, tek seferlik) ve özel coroutine'lerini bağlar.</summary>
        protected void SetupPoint(Interactable point, string label, string dialogueId, bool chapterSuffix, bool oneShot,
            Func<IEnumerator> before = null, Func<IEnumerator> after = null)
        {
            if (point == null) return;
            point.Setup(label, dialogueId, chapterSuffix, oneShot);
            interactor.Register(point, before, after);
        }

        /// <summary>Otomatik test: noktayı oyuncu E'ye basmış gibi kullanır.</summary>
        protected IEnumerator AutoUse(Interactable point)
        {
            if (point == null) yield break;
            yield return interactor.Use(point);
        }
    }
}
