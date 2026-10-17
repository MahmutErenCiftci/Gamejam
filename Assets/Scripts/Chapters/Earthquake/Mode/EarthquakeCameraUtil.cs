using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Deprem modunda kamerayı oynatan küçük yardımcı. Main Camera'da ChapterCamera varsa onu kullanır
    /// (ShowPoint / Follow, piksel ızgarasına yuvarlar); yoksa kameranın konumunu doğrudan yazar (1/64'e yuvarlanmış).
    /// Sarsıntı sırasında çağrılmaz (EarthquakeCameraShake kameranın yerini kendisi tutar).
    /// </summary>
    public static class EarthquakeCameraUtil
    {
        private const float Ppu = 64f;

        public static Camera Main => Camera.main;

        /// <summary>Kamerayı noktaya getirir. cut: hemen; değilse seconds boyunca yumuşak kayar ve bekler.</summary>
        public static IEnumerator MoveTo(Vector3 point, bool cut, float seconds)
        {
            var cam = Main;
            if (cam == null) yield break;
            var chapterCamera = cam.GetComponent<ChapterCamera>();
            if (chapterCamera != null)
            {
                chapterCamera.ShowPoint(point, cut);
                if (!cut && seconds > 0f) yield return new WaitForSeconds(seconds);
                yield break;
            }

            Vector3 start = cam.transform.position;
            Vector3 end = new Vector3(point.x, point.y, start.z);
            if (cut || seconds <= 0f)
            {
                cam.transform.position = Snap(end);
                yield break;
            }
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                cam.transform.position = Snap(Vector3.Lerp(start, end, k));
                yield return null;
            }
            cam.transform.position = Snap(end);
        }

        /// <summary>Kamerayı hemen noktaya koyar.</summary>
        public static void CutTo(Vector3 point)
        {
            var cam = Main;
            if (cam == null) return;
            var chapterCamera = cam.GetComponent<ChapterCamera>();
            if (chapterCamera != null)
            {
                chapterCamera.ShowPoint(point, true);
                return;
            }
            cam.transform.position = Snap(new Vector3(point.x, point.y, cam.transform.position.z));
        }

        /// <summary>Oyuncuyu alan içinde takip ettirir (ChapterCamera yoksa noktaya keser).</summary>
        public static void Follow(Transform target, CameraBounds bounds)
        {
            var cam = Main;
            if (cam == null || target == null) return;
            var chapterCamera = cam.GetComponent<ChapterCamera>();
            if (chapterCamera != null) chapterCamera.Follow(target, bounds, true);
            else CutTo(bounds != null ? bounds.transform.position : target.position);
        }

        private static Vector3 Snap(Vector3 p) =>
            new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, p.z);
    }
}
