using System;
using System.Collections.Generic;
using AfetUyanisi.Chapters.Earthquake.Mode;
using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>Hazırlık görevinin hangi gerçek oyun olayıyla tamamlandığı.</summary>
    public enum PrepTaskSource
    {
        /// <summary>Mini oyun bildirir: EarthquakeMinigameHub.CompleteTask(id) (ya da PreparationQuestManager.CompleteTask).</summary>
        External,
        /// <summary>keys içindeki incelemelerden (EarthquakeStats.inspectedIds) herhangi biri yapılınca.</summary>
        InspectAny,
        /// <summary>keys içindeki incelemelerin hepsi yapılınca (ara ilerleme gösterilir, ör. 1/2).</summary>
        InspectAll,
        /// <summary>keys içindeki anne sorularının hepsi cevaplanınca (doğru ya da yanlış; süresi dolan sayılmaz).</summary>
        AnswerAll
    }

    /// <summary>Bir hazırlık görevi (HUD satırı).</summary>
    [Serializable]
    public class PrepTaskDefinition
    {
        [Tooltip("Görev kimliği (ör. emergency_bag). Mini oyunlar bu kimlikle bildirir.")]
        public string id;
        [Tooltip("HUD satırındaki kısa metin (~30 karakter)")]
        public string label;
        [Tooltip("Uzun açıklama (rapor / ileride telefon görünümü)")]
        [TextArea] public string detail;
        public PrepTaskSource source;
        [Tooltip("İnceleme id'leri (InspectAny/InspectAll) ya da soru id'leri (AnswerAll)")]
        public string[] keys = Array.Empty<string>();

        public PrepTaskDefinition() { }

        public PrepTaskDefinition(string id, string label, PrepTaskSource source, string detail, params string[] keys)
        {
            this.id = id;
            this.label = label;
            this.source = source;
            this.detail = detail;
            this.keys = keys ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// DEPREM HAZIRLIK GÖREVLERİ — yetkili görev durumu (HUD yalnızca gösterir). 120 sn hazırlık evresinde (Intro/Prep)
    /// gerçek oyun olaylarından tamamlanır:
    ///  • mini oyunlar: EarthquakeMinigameHub.TaskCompleted (emergency_bag, secure_bookshelf),
    ///  • incelemeler ve anne soruları: EarthquakeModeController.Stats (inspectedIds / answers) değiştikçe okunur.
    /// Tamamlama idempotenttir (aynı görev iki kez sayılmaz). Sayaç bitince ya da sarsıntı başlayınca kilitlenir;
    /// son durum EarthquakeStats.prepTasks* alanlarına yazılır (sonuç raporu okur).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public class PreparationQuestManager : MonoBehaviour
    {
        [Tooltip("Boşsa sahnede bulunur")]
        [SerializeField] private EarthquakeModeController mode;
        [Tooltip("Boşsa sahnede bulunur (bitince görevler kilitlenir)")]
        [SerializeField] private EmergencyCountdownManager countdown;
        [SerializeField] private List<PrepTaskDefinition> tasks = DefaultTasks();
        [SerializeField] private bool logEvents = true;

        private readonly HashSet<string> done = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> doneOrder = new List<string>();
        private readonly Dictionary<string, int> progress = new Dictionary<string, int>(StringComparer.Ordinal);
        private int seenInspected = -1;
        private int seenAnswers = -1;
        private bool subscribedCountdown;

        /// <summary>Bir görev ilk kez tamamlanınca (id).</summary>
        public event Action<string> TaskCompleted;
        /// <summary>Herhangi bir durum/ilerleme değişikliğinde (tamamlama dahil).</summary>
        public event Action Changed;
        /// <summary>Hazırlık evresi bitti (sayaç 0 / sarsıntı); bundan sonra görev tamamlanmaz.</summary>
        public event Action Locked;

        public IReadOnlyList<PrepTaskDefinition> Tasks => tasks;
        public int TotalCount => tasks.Count;
        public int DoneCount => done.Count;
        public bool IsLocked { get; private set; }
        public IReadOnlyList<string> CompletedInOrder => doneOrder;

        /// <summary>İlk tamamlanmamış görev (HUD'da "aktif" vurgu); hepsi bittiyse null.</summary>
        public string ActiveTaskId
        {
            get
            {
                foreach (var t in tasks)
                    if (t != null && !done.Contains(t.id)) return t.id;
                return null;
            }
        }

        public static List<PrepTaskDefinition> DefaultTasks() => new List<PrepTaskDefinition>
        {
            new PrepTaskDefinition("emergency_bag", "Afet çantasını hazırla", PrepTaskSource.External,
                "Su, gıda, el feneri, düdük, ilk yardım seti: çantayı hazırla ve kapat."),
            new PrepTaskDefinition("secure_bookshelf", "Kitaplığı sabitle", PrepTaskSource.External,
                "Devrilebilecek kitaplığı depremden ÖNCE duvara sabitle."),
            new PrepTaskDefinition("check_windows", "Pencereleri kontrol et", PrepTaskSource.InspectAll,
                "Yatak odası ve salon pencerelerini incele: sarsıntıda camlardan uzak dur.", "bedroom_window", "living_window"),
            new PrepTaskDefinition("safe_spot", "Çök–Kapan–Tutun yerini belirle", PrepTaskSource.InspectAny,
                "Sağlam bir masa bul: çalışma masası ya da yemek masası.", "desk", "dining_table"),
            new PrepTaskDefinition("utilities_location", "Vana ve sigorta yerini öğren", PrepTaskSource.InspectAny,
                "Ocağı incele: doğalgaz vanası ve sigorta kutusunun YERİNİ öğren (tesisata dokunma).", "stove"),
            new PrepTaskDefinition("family_communication", "Annenin sorularını cevapla", PrepTaskSource.AnswerAll,
                "Annenle deprem planını konuş: üç sorusunu cevapla.", "Q1", "Q2", "Q3"),
            new PrepTaskDefinition("assembly_point", "Annenden toplanma yerini öğren", PrepTaskSource.InspectAny,
                "Salondaki annenle konuş: ailenin toplanma alanını öğren.", "mother"),
        };

        private void Awake()
        {
            if (mode == null) mode = FindAnyObjectByType<EarthquakeModeController>(FindObjectsInactive.Include);
            if (countdown == null) countdown = FindAnyObjectByType<EmergencyCountdownManager>(FindObjectsInactive.Include);
            if (tasks == null || tasks.Count == 0) tasks = DefaultTasks();
            tasks.RemoveAll(t => t == null || string.IsNullOrEmpty(t.id));
        }

        private void OnEnable()
        {
            EarthquakeMinigameHub.TaskCompleted += OnHubTaskCompleted;
            if (countdown != null && !subscribedCountdown)
            {
                countdown.OnTimerExpired += Lock;
                subscribedCountdown = true;
            }
        }

        private void OnDisable()
        {
            EarthquakeMinigameHub.TaskCompleted -= OnHubTaskCompleted;
            if (countdown != null && subscribedCountdown)
            {
                countdown.OnTimerExpired -= Lock;
                subscribedCountdown = false;
            }
        }

        private void Start() => WriteStats();

        public PrepTaskDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var t in tasks)
                if (t != null && t.id == id) return t;
            return null;
        }

        public bool IsDone(string id) => !string.IsNullOrEmpty(id) && done.Contains(id);

        /// <summary>Ara ilerleme (InspectAll/AnswerAll için; diğerlerinde 0/1 ya da 1/1). Görev yoksa false.</summary>
        public bool TryGetProgress(string id, out int current, out int max)
        {
            current = 0;
            max = 1;
            var t = Find(id);
            if (t == null) return false;
            if (t.source == PrepTaskSource.InspectAll || t.source == PrepTaskSource.AnswerAll)
                max = Mathf.Max(1, t.keys != null ? t.keys.Length : 1);
            if (done.Contains(id)) current = max;
            else progress.TryGetValue(id, out current);
            return true;
        }

        /// <summary>
        /// Görevi tamamlar (idempotent). Kilitliyse, görev tanımlı değilse ya da zaten tamamsa false.
        /// Mini oyunlar normalde EarthquakeMinigameHub.CompleteTask(id) çağırır; bu metot da aynı yoldan geçer.
        /// </summary>
        public bool CompleteTask(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (IsLocked || !AcceptingPhase())
            {
                Log($"görev '{id}' bildirildi ama hazırlık evresi bitti → sayılmadı");
                return false;
            }
            var t = Find(id);
            if (t == null)
            {
                Debug.LogWarning($"[EQ-HUD] tanımsız hazırlık görevi '{id}' (PreparationQuestManager.tasks) → yok sayıldı", this);
                return false;
            }
            if (!done.Add(id)) return false;
            doneOrder.Add(id);
            Log($"görev tamamlandı: {id} ({done.Count}/{tasks.Count})");
            WriteStats();
            TaskCompleted?.Invoke(id);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Hazırlık evresini kapatır (sayaç bitti). Bir kez çalışır.</summary>
        public void Lock()
        {
            if (IsLocked) return;
            Poll(true); // aynı karede yapılmış son incelemeler kaçmasın
            IsLocked = true;
            WriteStats();
            Log($"hazırlık kilitlendi: {done.Count}/{tasks.Count} görev");
            Locked?.Invoke();
            Changed?.Invoke();
        }

        private bool AcceptingPhase()
        {
            if (mode == null) return true;
            return mode.Phase == EarthquakePhase.Intro || mode.Phase == EarthquakePhase.Prep;
        }

        private void OnHubTaskCompleted(string id) => CompleteTask(id);

        private void Update()
        {
            if (IsLocked) return;
            if (mode != null && mode.Phase != EarthquakePhase.Intro && mode.Phase != EarthquakePhase.Prep)
            {
                Lock();
                return;
            }
            Poll(false);
        }

        /// <summary>Stats'taki inceleme/cevap listeleri değiştiyse görevleri yeniden değerlendirir.</summary>
        private void Poll(bool force)
        {
            if (mode == null || mode.Stats == null) return;
            var s = mode.Stats;
            // Çanta mini oyununun kalıcı kaydı (hub bildirimi kaçsa ya da otomatik test çantayı doğrudan hazırlasa da sayılır).
            if (s.emergencyBagPrepared && !done.Contains("emergency_bag") && Find("emergency_bag") != null) CompleteTask("emergency_bag");
            int ins = s.inspectedIds.Count, ans = s.answers.Count;
            if (!force && ins == seenInspected && ans == seenAnswers) return;
            seenInspected = ins;
            seenAnswers = ans;

            bool changed = false;
            foreach (var t in tasks)
            {
                if (t == null || t.source == PrepTaskSource.External || done.Contains(t.id) || t.keys == null || t.keys.Length == 0) continue;
                int n = 0;
                foreach (var k in t.keys)
                {
                    if (string.IsNullOrEmpty(k)) continue;
                    bool hit = t.source == PrepTaskSource.AnswerAll
                        ? s.answers.Exists(a => a.questionId == k && !a.timedOut && a.chosenIndex >= 0)
                        : s.inspectedIds.Contains(k);
                    if (hit) n++;
                }
                bool complete = t.source == PrepTaskSource.InspectAny ? n > 0 : n >= t.keys.Length;
                if (complete)
                {
                    progress.Remove(t.id);
                    if (!IsLocked) CompleteTask(t.id); // Changed'i CompleteTask gönderir
                    continue;
                }
                progress.TryGetValue(t.id, out int old);
                if (old != n)
                {
                    progress[t.id] = n;
                    changed = true;
                }
            }
            if (changed) Changed?.Invoke();
        }

        /// <summary>Son durumu rapor için EarthquakeStats'a yazar.</summary>
        private void WriteStats()
        {
            if (mode == null || mode.Stats == null) return;
            var s = mode.Stats;
            s.prepTasksTotal = tasks.Count;
            s.prepTasksDone.Clear();
            s.prepTasksDoneLabels.Clear();
            s.prepTasksMissedLabels.Clear();
            foreach (var t in tasks)
            {
                if (done.Contains(t.id))
                {
                    s.prepTasksDone.Add(t.id);
                    s.prepTasksDoneLabels.Add(t.label);
                }
                else s.prepTasksMissedLabels.Add(t.label);
            }
        }

        private void Log(string message)
        {
            if (logEvents) Debug.Log("[EQ-HUD] " + message, this);
        }
    }
}
