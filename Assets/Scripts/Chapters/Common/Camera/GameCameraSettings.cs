using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Oyun kamerasının MERKEZİ ayarı (Mahmut). Sahne dosyalarına dokunmadan, her sahne yüklenince çalışır:
    /// <list type="bullet">
    /// <item>PixelPerfectCamera referans çözünürlüğü 640x360 olan oyun kameralarını <see cref="RefResolution"/>'a çeker
    /// (480x270 → 1080p'de 4x yakın). Ana menü (<see cref="ExcludedScenes"/>) ve farklı referanslı kameralar hariç.</item>
    /// <item>Main Camera'ya gerekirse ChapterCamera, oda takibi (RoomCameraRig) ve renkli arka plan (RoomBackdrop) ekler.</item>
    /// <item>Odaların "Floor" iso tilemap'lerine maket tabanı (DioramaBase) ekler.</item>
    /// </list>
    /// Yakınlığı değiştirmek için tek satır: <c>RefResolution = new Vector2Int(480, 270)</c> (eski görünüm: 640, 360).
    /// </summary>
    public static class GameCameraSettings
    {
        /// <summary>Oyun kameralarının referans çözünürlüğü (PPU 64). 480x270 = 1080p'de 4x, 640x360 = 3x (eski).</summary>
        public static Vector2Int RefResolution = new Vector2Int(480, 270);

        /// <summary>Sahnelerde kayıtlı eski referans; yalnızca bu değerdeki kameralar değiştirilir.</summary>
        public static readonly Vector2Int LegacyRefResolution = new Vector2Int(640, 360);

        public const float PixelsPerUnit = 64f;

        /// <summary>Oda ekrana sığmazsa kamera oyuncuyu oda içeriği içinde yumuşak takip etsin.</summary>
        public static bool FollowInRooms = true;
        /// <summary>Odaların arkasına gradyan + vinyet çizilsin.</summary>
        public static bool BackdropEnabled = true;
        /// <summary>İç mekân odalarının zemini altına maket tabanı çizilsin.</summary>
        public static bool DioramaEnabled = true;

        /// <summary>Hiç dokunulmayan sahneler (kendi kamera düzeni olanlar).</summary>
        public static readonly string[] ExcludedScenes = { "MainMenu" };

        /// <summary>Sahnelerin eski çerçeve boyutu (dünya birimi): 640x360 / 64 = 10 x 5.625.</summary>
        public static Vector2 LegacyViewSize =>
            new Vector2(LegacyRefResolution.x / PixelsPerUnit, LegacyRefResolution.y / PixelsPerUnit);

        // ---------------- Arka plan paletleri ----------------

        /// <summary>Arka plan paleti: dikey gradyan (üst → orta → alt) + kenar vinyeti rengi.</summary>
        [Serializable]
        public struct BackdropPalette
        {
            public Color top, mid, bottom, vignette;
            public BackdropPalette(string topHex, string midHex, string bottomHex, string vignetteHex)
            {
                top = Hex(topHex); mid = Hex(midHex); bottom = Hex(bottomHex); vignette = Hex(vignetteHex);
            }
            public bool SameAs(BackdropPalette o) => top == o.top && mid == o.mid && bottom == o.bottom && vignette == o.vignette;
        }

        /// <summary>Sel yatak odası / banyo: şafak laciverti → mor.</summary>
        public static BackdropPalette DawnPalette = new BackdropPalette("#121A2E", "#1C2742", "#3A2F5B", "#090B15");
        /// <summary>Sel mutfak: sıcak koyu kahve → turuncu.</summary>
        public static BackdropPalette KitchenPalette = new BackdropPalette("#1A110D", "#2E1C14", "#55321D", "#0C0705");
        /// <summary>Sel dış mekân (sokak, otobüs, okul yolu; kendi gökyüzü önde kalır): fırtına grisi.</summary>
        public static BackdropPalette StormPalette = new BackdropPalette("#10151F", "#1A2331", "#2A3646", "#070A0F");
        /// <summary>Akşam sel mahallesi: koyu gece mavisi (sıcak sokak lambaları önde).</summary>
        public static BackdropPalette EveningPalette = new BackdropPalette("#0A1124", "#121E3B", "#1E2F55", "#04070F");
        /// <summary>Deprem evi: koyu lacivert.</summary>
        public static BackdropPalette QuakePalette = new BackdropPalette("#14141F", "#1C2742", "#263559", "#09090F");
        /// <summary>Sarsıntıda vinyetin döndüğü renk (düşük alfa, flaş yok).</summary>
        public static Color AlarmVignette = new Color(0.847f, 0.263f, 0.231f, 0.55f); // #D8433B

        /// <summary>Sahne ve kamera konumuna göre palet (Sel sahnesinde odalar x ekseninde dizili: mutfak x≈0, sokak 30+).</summary>
        public static BackdropPalette PaletteFor(string sceneName, Vector3 cameraPosition)
        {
            switch (sceneName)
            {
                case "Bedroom":
                case "Bathroom":
                    return DawnPalette;
                case "FloodChapter":
                    return cameraPosition.x < 15f ? KitchenPalette : StormPalette;
                case "FloodEvening":
                    return EveningPalette;
                case "EarthquakeHome":
                case "EarthquakeChapter":
                    return QuakePalette;
                default:
                    return DawnPalette;
            }
        }

        // ---------------- Kurulum ----------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RoomBackdrop.SetAlarm(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try { Apply(scene); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static bool IsExcluded(string sceneName)
        {
            foreach (var n in ExcludedScenes)
                if (string.Equals(n, sceneName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Sahnedeki oyun kameralarını ve odaları ayarlar (sceneLoaded'da kendiliğinden çağrılır).</summary>
        public static void Apply(Scene scene)
        {
            if (!scene.IsValid() || IsExcluded(scene.name)) return;
            RoomBackdrop.SetAlarm(false);
            bool gameCamera = false;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var ppc in root.GetComponentsInChildren<PixelPerfectCamera>(true))
            {
                if (ppc.refResolutionX != LegacyRefResolution.x || ppc.refResolutionY != LegacyRefResolution.y) continue;
                ppc.refResolutionX = RefResolution.x;
                ppc.refResolutionY = RefResolution.y;
                var cam = ppc.GetComponent<Camera>();
                if (cam == null || !cam.CompareTag("MainCamera")) continue;
                gameCamera = true;
                SetupCamera(cam, scene);
            }
            if (gameCamera && DioramaEnabled) DioramaBase.AttachAll(scene);
        }

        private static void SetupCamera(Camera cam, Scene scene)
        {
            var go = cam.gameObject;
            if (FollowInRooms)
            {
                if (go.GetComponent<ChapterCamera>() == null) go.AddComponent<ChapterCamera>();
                if (go.GetComponent<RoomCameraRig>() == null) go.AddComponent<RoomCameraRig>();
            }
            if (BackdropEnabled && go.GetComponent<RoomBackdrop>() == null)
                go.AddComponent<RoomBackdrop>().Configure(scene.name);
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }
    }
}
