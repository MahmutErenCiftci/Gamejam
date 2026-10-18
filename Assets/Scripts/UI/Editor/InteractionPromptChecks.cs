using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AfetUyanisi.Chapters;
using AfetUyanisi.Chapters.Rooms;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace AfetUyanisi.UI.Editor
{
    /// <summary>Geçici çalışma zamanı nesneleriyle gerçek TMP ve RoomInteractor entegrasyon kontrolü.</summary>
    [InitializeOnLoad]
    public static class InteractionPromptChecks
    {
        private const string PendingKey = "AfetUyanisi.InteractionUIChecks";
        private static bool running;

        static InteractionPromptChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
                SessionState.SetBool(PendingKey, false);
                Run();
            };
        }

        [MenuItem("Afet Uyanisi/Interaction UI/Run Play Mode checks")]
        public static void Start()
        {
            if (running) return;
            if (EditorApplication.isPlaying) Run();
            else
            {
                SessionState.SetBool(PendingKey, true);
                EditorApplication.EnterPlaymode();
            }
        }

        private static async void Run()
        {
            if (running) return;
            running = true;
            var report = new StringBuilder();
            GameObject root = null;
            Camera camera = null;
            float cameraSize = 0, timeScale = Time.timeScale;
            try
            {
                await Wait(300);
                var prefab = Resources.Load<InteractionPromptUI>("InteractionUI/InteractionPromptUI");
                Check(prefab != null, "Prefab Resources üzerinden yükleniyor", report);
                root = new GameObject("InteractionPromptChecks_Temporary");
                var view = UnityEngine.Object.Instantiate(prefab, root.transform);
                camera = Camera.main;
                Check(camera != null, "Dünya kamerası var", report);
                cameraSize = camera.orthographicSize;
                var a = new GameObject("PromptTargetA"); a.transform.SetParent(root.transform);
                var b = new GameObject("PromptTargetB"); b.transform.SetParent(root.transform);
                float depth = Mathf.Max(1f, -camera.transform.position.z);
                a.transform.position = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
                b.transform.position = camera.ViewportToWorldPoint(new Vector3(0.7f, 0.6f, depth));

                Check(view.Font.HasCharacters(InteractionPromptAssets.Turkish), "Türkçe ç ğ ı İ ö ş ü Ç Ğ Ö Ş Ü atlas içinde", report);
                view.Show("E", "Konuş", a.transform, Vector3.zero);
                await Wait(250);
                var shortSize = view.PanelSize;
                Check(view.IsVisible, "Görünme animasyonu tamamlanıyor", report);
                view.Show("E", "Acil çantayı hazırla", a.transform, Vector3.zero);
                var longSize = view.PanelSize;
                Check(longSize.x > shortSize.x + 15f && Mathf.Abs(longSize.y - shortSize.y) < 1f,
                    "Metin ölçümü: Konuş " + shortSize + " / Acil çantayı hazırla " + longSize, report);
                var text = Array.Find(view.GetComponentsInChildren<TextMeshProUGUI>(true), t => t.name == "Label");
                text.ForceMeshUpdate();
                Check(!text.isTextOverflowing, "Uzun Türkçe etiket kutuya sığıyor", report);
                view.Show("E", "Kitaplığı sabitle", b.transform, Vector3.zero);
                await Wait(100);
                var beforeZoom = view.Panel.anchoredPosition;
                camera.orthographicSize *= 0.75f;
                await Wait(100);
                Check((view.Panel.anchoredPosition - beforeZoom).sqrMagnitude > 1f, "Hedef/kamera zoom takibi", report);
                camera.orthographicSize = cameraSize;
                b.transform.position = camera.ViewportToWorldPoint(new Vector3(0.99f, 0.97f, depth));
                view.Show("E", "Dışarıya bak", b.transform, Vector3.zero);
                await Wait(150);
                var corners = new Vector3[4]; view.Panel.GetWorldCorners(corners);
                Check(corners[0].x >= 0 && corners[2].x <= Screen.width + 1 && corners[0].y >= 0 && corners[2].y <= Screen.height + 1,
                    "Ekran kenarında panel görünür alana kısılıyor", report);
                view.Show("E", "Uzun etkileşim açıklaması satır kaydırarak tamamıyla okunabilir kalmalıdır.", a.transform, Vector3.zero);
                await Wait(100);
                text.ForceMeshUpdate();
                Check(view.PanelSize.y > shortSize.y && !text.isTextOverflowing, "Uzun açıklama kesilmeden satır kaydırıyor", report);

                Time.timeScale = 0;
                view.Hide(); await Wait(250);
                Check(!view.IsVisible, "Zaman duruyorken gizlenme", report);
                view.Show("E", "Telefonu aç", a.transform, Vector3.zero); await Wait(250);
                Check(view.IsVisible, "Zaman duruyorken görünme", report);
                Time.timeScale = timeScale;

                var point = a.AddComponent<Interactable>(); point.Setup("Konuş", "", false, false);
                var playerObject = new GameObject("PromptTestPlayer"); playerObject.transform.SetParent(root.transform);
                var player = playerObject.AddComponent<IsoPlayerController>();
                player.PlaceAt(a.transform.position, Vector2.down);
                var interactor = root.AddComponent<RoomInteractor>();
                var serialized = new SerializedObject(interactor);
                var points = serialized.FindProperty("interactables"); points.arraySize = 1;
                points.GetArrayElementAtIndex(0).objectReferenceValue = point;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                interactor.Configure(player, null, view);
                interactor.Register(point);
                player.CanMove = true; interactor.InteractionEnabled = true;
                await Wait(300);
                Check(interactor.Current == point && view.IsVisible, "RoomInteractor aynı prefabı hedefe bağlı gösteriyor", report);
                point.Setup("Acil çantayı hazırla", "", false, false); await Wait(100);
                Check(text.text == "Acil çantayı hazırla", "Aynı nesnenin değişen etiketi yenileniyor", report);
                player.CanMove = false; await Wait(250);
                Check(!view.IsVisible, "Mini oyun/kamera için hareket kilitlenince gizleniyor", report);
                player.CanMove = true; await Wait(250);
                Check(view.IsVisible, "Hedef menzildeyse kilit kalkınca geri geliyor", report);
                int used = 0; point.Used += _ => used++;
                interactor.StartCoroutine(interactor.Use(point)); await Wait(100);
                Check(used == 1, "Bir kullanım tek Used olayı üretiyor; UI etkileşim tetiklemiyor", report);
                point.SetAvailable(false); await Wait(250);
                Check(!view.IsVisible && interactor.Current == null, "Kullanılamaz hedefin ipucu kalmıyor", report);
                interactor.InteractionEnabled = false;
                view.Show("E", "Konuş", b.transform, Vector3.zero); await Wait(250);
                UnityEngine.Object.Destroy(b); await Wait(250);
                Check(!view.IsVisible, "Hedef silinince yüzen ipucu kalmıyor", report);
                report.AppendLine("PASS: Bütün Interaction UI kontrolleri geçti.");
            }
            catch (Exception exception)
            {
                report.AppendLine("FAIL: " + exception);
                Debug.LogException(exception);
            }
            finally
            {
                Time.timeScale = timeScale;
                if (camera != null) camera.orthographicSize = cameraSize;
                if (root != null) UnityEngine.Object.Destroy(root);
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/interaction-ui-checks.txt", report.ToString());
                Debug.Log("[Interaction UI Checks]\n" + report);
                running = false;
            }
        }

        private static async Task Wait(int milliseconds)
        {
            await Task.Delay(milliseconds);
            if (!EditorApplication.isPlaying) throw new OperationCanceledException("Play Mode kapatıldı.");
        }

        private static void Check(bool condition, string label, StringBuilder report)
        {
            if (!condition) throw new InvalidOperationException(label);
            report.AppendLine("PASS: " + label);
        }
    }
}
