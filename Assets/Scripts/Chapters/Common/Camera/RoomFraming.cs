using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Bir odanın kamera dikdörtgenini hesaplar: odanın görünen içeriği (sprite + tilemap + maket tabanı) ile
    /// sahnenin eski 640x360 çerçevesinin (oda noktasına ortalı, 10 x 5.625 birim) kesişimi. Yakın kamera bu alanın
    /// içinde oyuncuyu takip eder; böylece oda kenarından öteye (boşluğa) gereğinden fazla bakmaz.
    /// </summary>
    public static class RoomFraming
    {
        private const float Pad = 2f / GameCameraSettings.PixelsPerUnit;

        /// <summary>root null ise bütün sahne taranır. anchor: odanın eski kamera noktası.</summary>
        public static Rect Frame(Scene scene, Transform root, Vector2 anchor, Camera cam)
        {
            Vector2 size = GameCameraSettings.LegacyViewSize;
            var legacy = new Rect(anchor - size * 0.5f, size);

            if (root != null) DioramaBase.EnsureBuiltUnder(root);
            else DioramaBase.EnsureBuiltInScene(scene);

            bool any = false;
            float xMin = 0f, yMin = 0f, xMax = 0f, yMax = 0f;
            foreach (var r in Renderers(scene, root))
            {
                if (!(r is SpriteRenderer || r is TilemapRenderer)) continue;
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r is SpriteRenderer sr && sr.sprite == null) continue;
                if (cam != null && r.transform.IsChildOf(cam.transform)) continue;
                if (r.GetComponentInParent<IsoPlayerController>() != null) continue;
                var b = r.bounds;
                if (b.max.x < legacy.xMin || b.min.x > legacy.xMax || b.max.y < legacy.yMin || b.min.y > legacy.yMax) continue;
                if (!any) { xMin = b.min.x; yMin = b.min.y; xMax = b.max.x; yMax = b.max.y; any = true; }
                else
                {
                    xMin = Mathf.Min(xMin, b.min.x); yMin = Mathf.Min(yMin, b.min.y);
                    xMax = Mathf.Max(xMax, b.max.x); yMax = Mathf.Max(yMax, b.max.y);
                }
            }
            if (!any) return legacy;
            xMin = Mathf.Max(xMin - Pad, legacy.xMin); yMin = Mathf.Max(yMin - Pad, legacy.yMin);
            xMax = Mathf.Min(xMax + Pad, legacy.xMax); yMax = Mathf.Min(yMax + Pad, legacy.yMax);
            if (xMax <= xMin || yMax <= yMin) return legacy;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>Kendi gökyüzü/arka planı olan dış mekân mı (sorting order &lt; -100 olan renderer var mı)?</summary>
        public static bool HasOwnBackdrop(Transform root)
        {
            if (root == null) return false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if ((r is SpriteRenderer || r is TilemapRenderer) && r.sortingOrder < DioramaBase.OwnBackdropOrder
                    && r.GetComponentInParent<DioramaBase>() == null && r.GetComponentInParent<RoomBackdrop>() == null)
                    return true;
            return false;
        }

        private static IEnumerable<Renderer> Renderers(Scene scene, Transform root)
        {
            if (root != null)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(false)) yield return r;
                yield break;
            }
            if (!scene.IsValid()) yield break;
            foreach (var go in scene.GetRootGameObjects())
                foreach (var r in go.GetComponentsInChildren<Renderer>(false)) yield return r;
        }
    }
}
