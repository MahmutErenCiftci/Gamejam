using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Yalnız Test_Eren_Wind için: bölüm akışı olmadan serbest dolaşımı açar, kamerayı Cenk'e bağlar.
    /// Böylece rüzgâr ve Cenk'in tepkileri bölüm sahnesine dokunmadan denenir.
    /// </summary>
    public class FloodWindTestDriver : MonoBehaviour
    {
        [SerializeField] private IsoPlayerController player;
        [SerializeField] private ChapterCamera chapterCamera;
        [SerializeField] private CameraBounds bounds;

        private void Start()
        {
            FloodEveningHub.FreeRoam = true;
            FloodEveningHub.Progress01 = 1f;
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>();
            if (player != null) player.CanMove = true;
            if (chapterCamera == null && Camera.main != null) chapterCamera = Camera.main.GetComponent<ChapterCamera>();
            if (chapterCamera != null && player != null)
            {
                if (bounds != null) chapterCamera.Follow(player.transform, bounds, true);
                else chapterCamera.FollowArea(player.transform, new Rect(-1000f, -1000f, 2000f, 2000f), true);
            }
        }

        private void OnDestroy() => FloodEveningHub.FreeRoam = false;
    }
}
