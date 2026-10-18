using System;
using System.Collections;
using System.Collections.Generic;
using AfetUyanisi.Core;
using AfetUyanisi.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AfetUyanisi.Chapters.Objectives
{
    /// <summary>
    /// Sahneye ait görev (hedef) durumu: Resources/Objectives/objectives_&lt;bölüm&gt;.json'u okur, olayları (Notify)
    /// görevlere çevirir, tamamlanmaları sıraya koyup görünüme (ObjectiveViewBase, varsayılan ObjectiveHudView) oynatır.
    /// Kalıcı durum GameSession bayraklarındadır ("obj:..."), böylece Bedroom → Bathroom → bölüm sahnesi arasında taşınır.
    /// Görünüm diyalog/karar/haber açıkken gizlenir (DeferFeedback, SetDialogueOpen, AddVisibilityBlocker); bu sırada
    /// biten görevler sırada bekler ve panel geri gelince tek tek oynatılır.
    /// Olay türleri: "room" (Bedroom/Bathroom), "step" (F05_STREET), "point" (F04_MOTHER/Coat, Bedroom/Clock),
    /// "say" (diyalog sahnesi bitti), "decision" (karar; success = ilk denemede doğru), "news", "event" (serbest anahtar).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class ObjectiveTracker : MonoBehaviour
    {
        [SerializeField] private ObjectiveViewBase view;
        [SerializeField] private bool createDefaultView = true;
        [SerializeField] private bool logEvents = true;

        private sealed class Entry
        {
            public ObjectiveDefinition Definition;
            public ObjectiveRowState Row;
            public bool PresentedDone;
        }

        private sealed class Feedback
        {
            public string Id;
            public bool Complete;
        }

        private sealed class FeedbackScope : IDisposable
        {
            private ObjectiveTracker owner;
            public FeedbackScope(ObjectiveTracker tracker) { owner = tracker; owner.deferredScopes++; }
            public void Dispose()
            {
                if (owner == null) return;
                owner.deferredScopes = Mathf.Max(0, owner.deferredScopes - 1);
                owner = null;
            }
        }

        private const int MaxWarnings = 99;
        // Tamamlanma animasyonu: tik + çizgi ≈ 0,3 sn, satırın çıkışı 1,4–1,6 sn (ObjectiveHudView).
        private const float CompleteSpacing = 1.65f, QueuedSpacing = 0.9f, WarnSpacing = 0.35f, ShowDelay = 0.3f;
        private const int VisibleRows = 4; // ObjectiveHudView en çok 4 satır gösterir

        private static readonly Dictionary<Scene, ObjectiveTracker> instances = new Dictionary<Scene, ObjectiveTracker>();
        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<string, Entry> byId = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Queue<Feedback> pending = new Queue<Feedback>();
        private readonly List<Func<bool>> blockers = new List<Func<bool>>();
        private readonly List<ObjectiveRowState> rowsSnapshot = new List<ObjectiveRowState>();
        private ObjectiveChapterDefinition data;
        private ChapterId? loadedChapter;
        private int groupIndex;
        private int deferredScopes;
        private bool dialogueOpen;
        private bool rejectedDuplicate;
        private bool dirty;
        private bool snapshotDirty = true;
        private bool? shown;
        private float nextFeedbackAt;
        private float lastPresentedAt = float.NegativeInfinity;
        private string sessionKey;

        /// <summary>Bir görev tamamlandığında (mantık anında, görünüm sırası beklenmeden). Parametre: görev ID'si.</summary>
        public event Action<string> Completed;

        /// <summary>Herhangi bir görev durumu, sayaç, uyarı ya da etkin grup değiştiğinde.</summary>
        public event Action Changed;

        /// <summary>Ana görev ilerlemesi (0..1, görünümün oynattığı değil mantık durumu).</summary>
        public float Progress01 => Progress(false);
        public int PendingCount => pending.Count;
        public bool IsFeedbackDeferred => dialogueOpen || deferredScopes > 0 || AnyBlocker();
        public string CurrentGroup => data == null ? null : data.groups[groupIndex].id;
        public string CurrentGroupTitle => data == null ? null : data.groups[groupIndex].title;

        /// <summary>Bölümdeki bütün görevlerin anlık kopyaları (JSON sırası). Changed'de yenilenir.</summary>
        public IReadOnlyList<ObjectiveRowState> Rows
        {
            get
            {
                if (!EnsureLoaded()) { rowsSnapshot.Clear(); return rowsSnapshot; }
                if (snapshotDirty)
                {
                    rowsSnapshot.Clear();
                    foreach (var entry in entries) rowsSnapshot.Add(entry.Row.Copy());
                    snapshotDirty = false;
                }
                return rowsSnapshot;
            }
        }

        /// <summary>Aktif sahnedeki takipçi (yoksa null, oluşturmaz).</summary>
        public static ObjectiveTracker Current
        {
            get
            {
                var scene = SceneManager.GetActiveScene();
                return instances.TryGetValue(scene, out var tracker) && tracker != null ? tracker : null;
            }
        }

        /// <summary>Aktif sahnedeki tek takipçiyi döndürür, yoksa kurar (varsayılan HUD ile).</summary>
        public static ObjectiveTracker Ensure() => Ensure(true);

        /// <summary>withDefaultView false: görünüm kurulmaz (kendi HUD'unu bağlayan sahneler için, bkz. BindView).</summary>
        public static ObjectiveTracker Ensure(bool withDefaultView)
        {
            var scene = SceneManager.GetActiveScene();
            if (instances.TryGetValue(scene, out var existing) && existing != null) return existing;
            foreach (var tracker in FindObjectsByType<ObjectiveTracker>(FindObjectsInactive.Include))
            {
                if (tracker.gameObject.scene != scene || tracker.rejectedDuplicate) continue;
                instances[scene] = tracker;
                return tracker;
            }
            var go = new GameObject("ObjectiveTracker");
            SceneManager.MoveGameObjectToScene(go, scene);
            var created = go.AddComponent<ObjectiveTracker>();
            created.createDefaultView = withDefaultView;
            return created;
        }

        private void Awake()
        {
            var handle = gameObject.scene;
            if (instances.TryGetValue(handle, out var other) && other != null && other != this)
            {
                rejectedDuplicate = true;
                enabled = false;
                Destroy(this); // Kullanıcının GameObject'ini veya diğer bileşenlerini silme.
                return;
            }
            instances[handle] = this;
        }

        private void Start() => EnsureLoaded();

        private void OnDestroy()
        {
            var handle = gameObject.scene;
            if (instances.TryGetValue(handle, out var tracker) && tracker == this) instances.Remove(handle);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instances.Clear();

        // ------------------------------------------------------------------ görünüm ve gizleme

        /// <summary>Özel görünüm bağlamak ve testlerde pasif bir görünüm kullanmak için.</summary>
        public void BindView(ObjectiveViewBase newView)
        {
            if (view != null && view != newView) view.SetVisible(false);
            view = newView;
            shown = null;
            dirty = true;
            if (data != null) RefreshView();
        }

        /// <summary>İç içe Say/Ask/haber çağrılarını using ile güvenli biçimde askıya alır (panel gizlenir).</summary>
        public IDisposable DeferFeedback() => new FeedbackScope(this);

        /// <summary>Tek bir yöneticinin aç/kapa sinyali (true = panel gizli, tamamlanmalar bekler).</summary>
        public void SetDialogueOpen(bool open) => dialogueOpen = open;

        /// <summary>Her karede sorulan gizleme koşulu (ör. () => interactor.IsSaying). true dönerse panel gizlenir.</summary>
        public void AddVisibilityBlocker(Func<bool> blocked)
        {
            if (blocked != null && !blockers.Contains(blocked)) blockers.Add(blocked);
        }

        public void RemoveVisibilityBlocker(Func<bool> blocked) => blockers.Remove(blocked);

        private bool AnyBlocker()
        {
            foreach (var b in blockers)
                if (b != null && b()) return true;
            return false;
        }

        /// <summary>
        /// Sıradaki tamamlanmalar oynatılana ve son tamamlanma en az minShown sn görünene kadar bekler (en çok maxSeconds).
        /// Panel gizliyse beklemez. Oda geçişi / sonuç ekranından önce: oyuncu tiki görsün.
        /// </summary>
        public IEnumerator WaitForFeedback(float minShown, float maxSeconds)
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < maxSeconds && !IsFeedbackDeferred &&
                   (pending.Count > 0 || Time.unscaledTime - lastPresentedAt < minShown))
                yield return null;
        }

        /// <summary>Aktif sahnede takipçi varsa WaitForFeedback, yoksa hemen biter.</summary>
        public static IEnumerator Settle(float minShown, float maxSeconds)
        {
            var tracker = Current;
            if (tracker != null) yield return tracker.WaitForFeedback(minShown, maxSeconds);
        }

        // ------------------------------------------------------------------ sorgular

        public ObjectiveRowState GetRow(string id) => TryEntry(id, out var entry) ? entry.Row.Copy() : null;

        public bool IsCompleted(string id) => TryEntry(id, out var entry) && entry.Row.state == ObjectiveState.Done;

        /// <summary>Sonuç ekranı: ana görevler (Main + sayaç) ve bonuslar.</summary>
        public void GetSummary(out int mainDone, out int mainTotal, out int bonusDone, out int bonusTotal)
        {
            mainDone = mainTotal = bonusDone = bonusTotal = 0;
            if (!EnsureLoaded()) return;
            foreach (var entry in entries)
            {
                bool done = entry.Row.state == ObjectiveState.Done;
                var kind = entry.Definition.Kind;
                if (kind == ObjectiveKind.Main) { mainTotal++; if (done) mainDone++; }
                else if (kind == ObjectiveKind.Bonus) { bonusTotal++; if (done) bonusDone++; }
            }
        }

        /// <summary>Aktif sahnede takipçi varsa özetini verir.</summary>
        public static bool TryGetSummary(out int mainDone, out int mainTotal, out int bonusDone, out int bonusTotal)
        {
            mainDone = mainTotal = bonusDone = bonusTotal = 0;
            var tracker = Current;
            if (tracker == null || !tracker.EnsureLoaded()) return false;
            tracker.GetSummary(out mainDone, out mainTotal, out bonusDone, out bonusTotal);
            return mainTotal + bonusTotal > 0;
        }

        // ------------------------------------------------------------------ durum değişiklikleri

        public void Complete(string id)
        {
            if (!TryEntry(id, out var entry) || IsTerminal(entry)) return;
            entry.Row.state = ObjectiveState.Done;
            entry.Row.count = Mathf.Max(entry.Row.count, entry.Definition.target);
            GameSession.SetFlag("obj:" + id);
            pending.Enqueue(new Feedback { Id = id, Complete = true });
            MarkChanged();
            Log("tamamlandı: " + id + (IsFeedbackDeferred ? " (panel gizli, sırada)" : ""));
            Completed?.Invoke(id);
        }

        /// <summary>Uyarı: satır sarı "N UYARI" olur. Keep görevi uyarılınca tamamlanmaz; diğerleri sonra yine tamamlanabilir.</summary>
        public void Warn(string id)
        {
            if (!TryEntry(id, out var entry) || IsTerminal(entry)) return;
            entry.Row.state = ObjectiveState.Warned;
            entry.Row.warnings = Mathf.Min(MaxWarnings, entry.Row.warnings + 1);
            GameSession.SetFlag("obj:" + id + ":warned");
            GameSession.SetFlag($"obj:{id}:warn:{entry.Row.warnings}");
            if (!HasPendingWarn(id)) pending.Enqueue(new Feedback { Id = id });
            MarkChanged();
            Log($"uyarı: {id} ({entry.Row.warnings})");
        }

        /// <summary>Tamamlanamaz (Lesson türünde: kırmızı "DERS" satırı).</summary>
        public void Fail(string id)
        {
            if (!TryEntry(id, out var entry) || IsTerminal(entry)) return;
            entry.Row.state = ObjectiveState.Failed;
            GameSession.SetFlag("obj:" + id + ":failed");
            pending.Enqueue(new Feedback { Id = id });
            MarkChanged();
            Log("ders/başarısız: " + id);
        }

        /// <summary>Benzersiz öğrenilmiş öğelerin artan sayısı. max, JSON'daki toplamla aynı olmalıdır.</summary>
        public void SetProgress(string id, int n, int max)
        {
            if (!TryEntry(id, out var entry) || entry.Row.state == ObjectiveState.Failed) return;
            if (max != entry.Definition.max || max < 1)
            {
                Debug.LogWarning($"[OBJ] {id}: max={max}, veri max={entry.Definition.max}; güncelleme reddedildi.", this);
                return;
            }
            int count = Mathf.Clamp(n, entry.Row.count, max);
            if (count == entry.Row.count) return;
            for (int i = entry.Row.count + 1; i <= count; i++) GameSession.SetFlag($"obj:{id}:count:{i}");
            entry.Row.count = count;
            MarkChanged();
            Log($"sayaç: {id} {count}/{max}");
            if (count >= entry.Definition.target) Complete(id);
        }

        /// <summary>Sayaç kutucuklarının renkleri (ObjectivePip). Sahne içi gösterim; kalıcı değildir.</summary>
        public void SetPips(string id, int[] pips)
        {
            if (!TryEntry(id, out var entry)) return;
            entry.Row.pips = pips != null ? (int[])pips.Clone() : null;
            MarkChanged();
        }

        /// <summary>
        /// Olay bildirir. warnOn eşleşirse uyarı; completeOn eşleşirse: success=false → uyarı, Lesson → "DERS" satırı,
        /// uyarı almış Keep → tamamlanmaz, sayaç → +1 (aynı anahtar bir kez), diğerleri → tamamlandı.
        /// activateOn eşleşen ileri grup etkin olur (geri dönülmez).
        /// </summary>
        public void Notify(string kind, string key, bool success = true)
        {
            if (!EnsureLoaded() || string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(key)) return;
            Log($"olay: {kind}/{key}{(success ? "" : " (yanlış)")}");
            if (success)
            {
                for (int i = groupIndex + 1; i < data.groups.Length; i++)
                {
                    if (!ObjectiveDefinition.Matches(data.groups[i].activateOn, kind, key)) continue;
                    groupIndex = i;
                    GameSession.SetFlag("obj:group:" + data.chapter + ":" + data.groups[i].id);
                    MarkChanged();
                    Log("grup: " + data.groups[i].id);
                }
            }
            foreach (var entry in entries)
            {
                var definition = entry.Definition;
                if (ObjectiveDefinition.Matches(definition.warnOn, kind, key)) Warn(definition.id);
                else if (ObjectiveDefinition.Matches(definition.completeOn, kind, key))
                {
                    if (!success) Warn(definition.id);
                    else if (definition.Kind == ObjectiveKind.Lesson) Fail(definition.id);
                    else if (definition.Kind == ObjectiveKind.Keep && entry.Row.warnings > 0) continue;
                    else if (definition.max > 1)
                    {
                        // Aynı nokta/soru tekrar bildirilirse sayaç artmaz; bayrak sahneler arasında da kalır.
                        string eventKey = $"obj:{definition.id}:seen:{kind.ToLowerInvariant()}:{key}";
                        if (GameSession.HasFlag(eventKey)) continue;
                        GameSession.SetFlag(eventKey);
                        SetProgress(definition.id, entry.Row.count + 1, definition.max);
                    }
                    else Complete(definition.id);
                }
            }
        }

        /// <summary>Aktif sahnede takipçi varsa Notify (oluşturmaz).</summary>
        public static void Emit(string kind, string key, bool success = true)
        {
            var tracker = Current;
            if (tracker != null) tracker.Notify(kind, key, success);
        }

        private bool HasPendingWarn(string id)
        {
            foreach (var f in pending)
                if (f.Id == id && !f.Complete) return true;
            return false;
        }

        private void MarkChanged()
        {
            dirty = true;
            snapshotDirty = true;
            Changed?.Invoke();
        }

        private bool TryEntry(string id, out Entry entry)
        {
            entry = null;
            return EnsureLoaded() && id != null && byId.TryGetValue(id, out entry);
        }

        private static bool IsTerminal(Entry entry) =>
            entry.Row.state == ObjectiveState.Done || entry.Row.state == ObjectiveState.Failed;

        // ------------------------------------------------------------------ yükleme

        private bool EnsureLoaded()
        {
            if (rejectedDuplicate) return false;
            var chapter = GameSession.CurrentChapter;
            if (data != null && loadedChapter == chapter && GameSession.HasFlag(sessionKey)) return true;
            if (data == null && loadedChapter == chapter && sessionKey != null && (chapter == null || GameSession.HasFlag(sessionKey))) return false;

            loadedChapter = chapter;
            entries.Clear(); byId.Clear(); pending.Clear(); data = null;
            groupIndex = 0;
            nextFeedbackAt = 0f;
            snapshotDirty = true;
            sessionKey = "obj:session:" + chapter;
            if (view != null) view.SetVisible(false);
            shown = false;
            if (chapter != ChapterId.Flood && chapter != ChapterId.Earthquake) return false;
            GameSession.SetFlag(sessionKey);
            // Sel: akşam mahalle seli (FloodEvening) kendi görev listesini kullanır; eski sabah akışı objectives_flood.json.
            string name = chapter == ChapterId.Flood
                ? (gameObject.scene.name == GameSceneRouter.FloodScene ? "flood_evening"
                    : gameObject.scene.name == GameSceneRouter.FloodSchoolScene ? "flood_school" : "flood")
                : "earthquake";
            var json = Resources.Load<TextAsset>("Objectives/objectives_" + name);
            try
            {
                if (json == null) throw new ArgumentException("Resources/Objectives/objectives_" + name + ".json bulunamadı.");
                var parsed = JsonUtility.FromJson<ObjectiveChapterDefinition>(json.text);
                Validate(parsed, name);
                data = parsed;
            }
            catch (Exception ex)
            {
                Debug.LogError("[OBJ] Görev verisi yüklenemedi: " + ex.Message, this);
                return false;
            }

            for (int i = 0; i < data.groups.Length; i++)
                if (GameSession.HasFlag("obj:group:" + name + ":" + data.groups[i].id)) groupIndex = i;
            foreach (var definition in data.objectives)
            {
                string prefix = "obj:" + definition.id;
                bool done = GameSession.HasFlag(prefix);
                int count = done ? definition.target : 0;
                for (int i = 1; i <= definition.max; i++)
                    if (GameSession.HasFlag(prefix + ":count:" + i)) count = i;
                int warnings = 0;
                for (int i = 1; i <= MaxWarnings && GameSession.HasFlag($"{prefix}:warn:{i}"); i++) warnings = i;
                var entry = new Entry
                {
                    Definition = definition, PresentedDone = done,
                    Row = new ObjectiveRowState
                    {
                        id = definition.id, text = definition.text, kind = definition.RowKind,
                        max = definition.max, target = definition.target, count = count, warnings = warnings,
                        unit = definition.unit,
                        state = done ? ObjectiveState.Done : GameSession.HasFlag(prefix + ":failed") ? ObjectiveState.Failed :
                            warnings > 0 ? ObjectiveState.Warned : ObjectiveState.Todo
                    }
                };
                entries.Add(entry); byId.Add(definition.id, entry);
            }
            if (view == null && createDefaultView) view = gameObject.AddComponent<ObjectiveHudView>();
            if (view != null) view.SetSegments(MainCount());
            dirty = true;
            shown = null;
            RefreshView();
            Log($"yüklendi: {name}, {entries.Count} görev, grup {CurrentGroup}, %{Mathf.RoundToInt(Progress01 * 100f)}");
            Changed?.Invoke();
            return true;
        }

        private static void Validate(ObjectiveChapterDefinition chapter, string expected)
        {
            if (chapter == null || chapter.chapter != expected || chapter.groups == null || chapter.groups.Length == 0 ||
                chapter.objectives == null || chapter.objectives.Length == 0) throw new ArgumentException("Bölüm/grup/görev listesi geçersiz.");
            // JSON'da yazılmayan alanlar (JsonUtility iç içe nesnelerde alan ilk değerlerine güvenilmez): tek seferlik ana görev.
            foreach (var item in chapter.objectives)
            {
                if (item == null) continue;
                if (string.IsNullOrEmpty(item.kind)) item.kind = "Main";
                if (item.max < 1) item.max = 1;
                if (item.target < 1) item.target = item.max == 1 ? 1 : item.max;
            }
            var groups = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in chapter.groups)
                if (group == null || string.IsNullOrWhiteSpace(group.id) || !groups.Add(group.id))
                    throw new ArgumentException("Grup ID boş veya tekrarlı.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in chapter.objectives)
                if (item == null || string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id) || !groups.Contains(item.group) ||
                    string.IsNullOrWhiteSpace(item.text) || item.max < 1 || item.max > 1000 || item.target < 1 || item.target > item.max ||
                    (!string.IsNullOrEmpty(item.keepUntil) && !groups.Contains(item.keepUntil)) ||
                    !Enum.TryParse<ObjectiveKind>(item.kind, true, out var kind) || !Enum.IsDefined(typeof(ObjectiveKind), kind))
                    throw new ArgumentException("Görev ID, grup, kind, keepUntil veya sayaç geçersiz: " + item?.id);
        }

        // ------------------------------------------------------------------ oynatma

        private void Update()
        {
            if (!EnsureLoaded()) return;
            bool visible = !IsFeedbackDeferred;
            if (shown != visible)
            {
                shown = visible;
                if (view != null) view.SetVisible(visible);
                // Panel geri gelince önce görünsün, sonra bekleyen tamamlanmalar tek tek oynasın.
                if (visible) nextFeedbackAt = Mathf.Max(nextFeedbackAt, Time.unscaledTime + ShowDelay);
            }
            if (!visible) return;

            if (Time.unscaledTime >= nextFeedbackAt && pending.Count > 0)
            {
                var feedback = pending.Dequeue();
                var entry = byId[feedback.Id];
                if (feedback.Complete) entry.PresentedDone = true;
                RefreshView(feedback.Id, feedback.Complete);
                if (view != null)
                {
                    if (feedback.Complete) view.PlayComplete(feedback.Id);
                    else view.PlayWarn(feedback.Id);
                }
                lastPresentedAt = Time.unscaledTime;
                if (feedback.Complete) CheckGroupCompleted(entry.Definition.group);
                // En fazla dört satır: yoğun olaylarda tamamlanma animasyonunu okunabilir tut.
                // Sırada başka tamamlanma varsa daha sık oynat (satırlar üst üste kayarak çıkar).
                nextFeedbackAt = Time.unscaledTime + (!feedback.Complete ? WarnSpacing : pending.Count > 0 ? QueuedSpacing : CompleteSpacing);
            }
            else if (dirty) RefreshView();
        }

        private int MainCount()
        {
            int n = 0;
            foreach (var entry in entries) if (entry.Definition.Kind == ObjectiveKind.Main) n++;
            return n;
        }

        private float Progress(bool presented)
        {
            int total = 0, done = 0;
            foreach (var entry in entries)
            {
                if (entry.Definition.Kind != ObjectiveKind.Main) continue;
                total++;
                if (entry.Row.state == ObjectiveState.Done && (!presented || entry.PresentedDone)) done++;
            }
            return total == 0 ? 0f : (float)done / total;
        }

        private void RefreshView(string focusId = null, bool focusCompleting = false)
        {
            dirty = false;
            if (view == null || data == null) return;
            // JSON sırası korunur; oynatılan satır panelin ilk 4 satırına sığmıyorsa en üste alınır.
            var rows = new List<ObjectiveRowState>();
            int focusIndex = -1;
            foreach (var entry in entries)
            {
                bool isFocus = entry.Row.id == focusId;
                if (!isFocus && !IsListed(entry)) continue;
                if (isFocus) focusIndex = rows.Count;
                rows.Add(Present(entry, isFocus && focusCompleting));
            }
            if (focusIndex >= VisibleRows)
            {
                var focus = rows[focusIndex];
                rows.RemoveAt(focusIndex);
                rows.Insert(0, focus);
            }
            view.SetRows(rows);
            view.SetHeader(data.groups[groupIndex].title, Progress(true));
        }

        // Panelde görünür mü: etkin grubun görevleri + süren Keep'ler; bitmiş ve oynatılmış olanlar çıkar
        // (sayacı dolmamış sayaç hariç), Lesson sadece tetiklenince.
        private bool IsListed(Entry entry)
        {
            var definition = entry.Definition;
            int position = GroupPosition(definition.group);
            bool inGroup = position == groupIndex;
            if (!inGroup && definition.Kind == ObjectiveKind.Keep && position < groupIndex)
                inGroup = !string.IsNullOrEmpty(definition.keepUntil) && GroupPosition(definition.keepUntil) >= groupIndex;
            if (!inGroup) return false;
            if (definition.Kind == ObjectiveKind.Lesson && entry.Row.state == ObjectiveState.Todo) return false;
            if (!entry.PresentedDone) return true;
            return entry.Row.kind == ObjectiveKind.Counter && entry.Row.count < entry.Row.max;
        }

        // Mantık durumu anında kaydedilir; sırada bekleyen başarı henüz yeşil görünmez.
        private static ObjectiveRowState Present(Entry entry, bool completing)
        {
            var row = entry.Row.Copy();
            bool showDone = row.state == ObjectiveState.Done && entry.PresentedDone && !completing;
            if (row.state == ObjectiveState.Todo || (row.state == ObjectiveState.Done && !showDone))
                row.state = row.warnings > 0 ? ObjectiveState.Warned : ObjectiveState.Active;
            return row;
        }

        private int GroupPosition(string id)
        {
            for (int i = 0; i < data.groups.Length; i++) if (data.groups[i].id == id) return i;
            return int.MaxValue;
        }

        private void CheckGroupCompleted(string group)
        {
            bool hasMain = false;
            foreach (var entry in entries)
            {
                if (entry.Definition.group != group || entry.Definition.Kind != ObjectiveKind.Main) continue;
                hasMain = true;
                if (entry.Row.state != ObjectiveState.Done || !entry.PresentedDone) return;
            }
            string key = "obj:groupdone:" + data.chapter + ":" + group;
            if (!hasMain || GameSession.HasFlag(key)) return;
            GameSession.SetFlag(key);
            var definition = data.groups[GroupPosition(group)];
            if (view != null) view.ShowBanner(BannerText(definition));
            Log("grup tamamlandı: " + group);
        }

        // "SEL · MUTFAK" → "MUTFAK TAMAM!"
        private static string BannerText(ObjectiveGroupDefinition group)
        {
            if (!string.IsNullOrEmpty(group.banner)) return group.banner;
            string title = group.title ?? group.id;
            int dot = title.LastIndexOf('·');
            if (dot >= 0) title = title.Substring(dot + 1).Trim();
            return title + " TAMAM!";
        }

        private void Log(string text) { if (logEvents) Debug.Log("[OBJ] " + text, this); }
    }
}
