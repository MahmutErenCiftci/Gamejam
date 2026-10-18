using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// GameCameraSettings'in Main Camera'ya eklediği küçük bileşen. Tek odalı sahnelerde (Bedroom, Bathroom: bölüm
    /// denetleyicisi kamerayı yönetmiyorsa) ilk karede kamerayı oyuncuyu oda içeriği dikdörtgeninde takip ettirir.
    /// Bölüm sahnelerinde (ChapterControllerBase / EarthquakeModeController) hiçbir şey yapmaz; orada kamera onlardadır.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RoomCameraRig : MonoBehaviour
    {
        private Vector2 startPoint;
        private bool done;

        private void Awake()
        {
            startPoint = transform.position; // sahnede kaydedilmiş (eski çerçevenin) kamera noktası
        }

        private void LateUpdate()
        {
            if (done) return;
            done = true;
            enabled = false;
            if (!GameCameraSettings.FollowInRooms) return;

            var scene = gameObject.scene;
            if (HasType<ChapterControllerBase>(scene) || HasType<Earthquake.EarthquakeModeController>(scene)) return;

            var chapterCamera = GetComponent<ChapterCamera>();
            if (chapterCamera == null || chapterCamera.IsDriving) return;

            IsoPlayerController player = null;
            foreach (var p in FindObjectsByType<IsoPlayerController>(FindObjectsInactive.Include))
                if (p.gameObject.scene == scene) { player = p; break; }
            if (player == null) return;

            var frame = RoomFraming.Frame(scene, null, startPoint, GetComponent<Camera>());
            chapterCamera.FollowArea(player.transform, frame, true);
            Debug.Log($"[Kamera] {scene.name}: oyuncu oda alanında takip ediliyor {frame}", this);
        }

        private static bool HasType<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (var c in FindObjectsByType<T>(FindObjectsInactive.Include))
                if (c.gameObject.scene == scene) return true;
            return false;
        }
    }
}
