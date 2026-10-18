using System.Collections.Generic;
using AfetUyanisi.Chapters.Earthquake.Mode;
using UnityEngine;

namespace AfetUyanisi.Chapters.Visibility
{
    /// <summary>
    /// Dinamik duvar kesme (Mahmut). Aktif odada, Cenk'in ÖNÜNDE çizilen (oyuncunun sorting layer/order'ından büyük ya da
    /// aynı order'da CustomAxis Y'si küçük) ve sıkı sprite dikdörtgeni Cenk'in ÜST gövdesini örten ön duvar / uzun eşya
    /// (dolap, buzdolabı, ağaç, sarkıt lamba, durak çatısı...) yumuşakça RoomVisibilitySettings.CutawayAlpha opaklığına iner;
    /// Cenk çıkınca geri gelir. Sınama tamamen dünya uzayındadır (kamera sarsıntısı / yakınlaşma etkilemez) ve histerezisli
    /// (girme %35, çıkma %15 örtme; Y payı ±depthHysteresis) — sarsıntıda eşya ±2 px titrese de yanıp sönmez.
    /// Mini oyun açıkken (EarthquakeMinigameHub.AnyOpen) durum dondurulur. Saydamlık oda karartmasıyla aynı
    /// MaterialPropertyBlock _Color'unda birleşir (rgb = karartma, a = kesme); SpriteRenderer.color'a dokunmaz.
    /// RoomVisibilityManager ile aynı objede durur, kendi Update'i yoktur (yönetici Tick'ler).
    /// </summary>
    [DisallowMultipleComponent]
    public class DynamicWallCutaway : MonoBehaviour
    {
        private static readonly Dictionary<Sprite, Rect> TightRects = new Dictionary<Sprite, Rect>();

        private RoomVisibilityZone current;
        private IsoPlayerController cachedPlayer;
        private SpriteRenderer playerRenderer;

        /// <summary>Mini oyun açık olduğu için durum donduruldu mu?</summary>
        public bool Frozen { get; private set; }

        /// <summary>Şu an saydamlaşmış (ya da saydamlaşan) obje sayısı.</summary>
        public int OccludedCount { get; private set; }

        /// <summary>Tanı: kaç kez bir obje saydam ↔ opak durum değiştirdi (titreme testi için).</summary>
        public int StateChanges { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => TightRects.Clear();

        internal void ZoneRemoved(RoomVisibilityZone zone)
        {
            if (zone == null || zone != current) return;
            var m = GetComponent<RoomVisibilityManager>();
            if (m != null) RestoreInstant(m, current);
            current = null;
        }

        internal void Tick(RoomVisibilityManager m, RoomVisibilityZone zone, IsoPlayerController player, float dt)
        {
            if (zone != current)
            {
                if (current != null) RestoreInstant(m, current); // eski oda ekran dışında (kararma altında): hemen düzelsin
                current = zone;
            }
            OccludedCount = 0;
            if (current == null) return;

            var s = m.Settings;
            var list = current.CutawayCandidates;
            bool on = s.CutawayEnabled && player != null;
            Frozen = EarthquakeMinigameHub.AnyOpen;

            if (on && !Frozen) Evaluate(s, list, player);
            else if (!on)
                for (int i = 0; i < list.Count; i++) list[i].occluded = false;

            float fadeStep = dt / Mathf.Max(0.01f, s.CutawayFadeSeconds);
            float restoreStep = dt / Mathf.Max(0.01f, s.CutawayRestoreSeconds);
            var block = m.Block;
            var dark = s.DarknessColor;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t.occluded) OccludedCount++;
                float goal = t.occluded ? 1f : 0f;
                if (Mathf.Approximately(t.cutProgress, goal)) continue;
                t.cutProgress = Mathf.MoveTowards(t.cutProgress, goal, goal > t.cutProgress ? fadeStep : restoreStep);
                float minAlpha = t.AlphaOverride >= 0f ? t.AlphaOverride : s.CutawayAlpha;
                t.alpha = Mathf.Lerp(1f, minAlpha, Mathf.SmoothStep(0f, 1f, t.cutProgress));
                t.Apply(block, dark);
            }
        }

        private void Evaluate(RoomVisibilitySettings s, List<RoomVisibilityZone.Target> list, IsoPlayerController player)
        {
            if (player != cachedPlayer)
            {
                cachedPlayer = player;
                playerRenderer = player.GetComponentInChildren<SpriteRenderer>(true);
            }
            Vector2 feet = player.transform.position;
            int playerLayer = playerRenderer != null ? SortingLayer.GetLayerValueFromID(playerRenderer.sortingLayerID) : 0;
            int playerOrder = playerRenderer != null ? playerRenderer.sortingOrder : 0;

            float h = Mathf.Max(0.1f, s.PlayerBodyHeight);
            float hw = Mathf.Max(0.02f, s.PlayerBodyHalfWidth);
            float bx0 = feet.x - hw, bx1 = feet.x + hw;
            float by0 = feet.y + h * s.UpperBodyStart, by1 = feet.y + h;
            float bodyArea = (bx1 - bx0) * (by1 - by0);

            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                var r = t.Renderer;
                bool was = t.occluded;
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || (t.Sprite != null && t.Sprite.sprite == null))
                {
                    Set(t, false, was);
                    continue;
                }

                // Oyuncunun önünde mi çiziliyor? (sorting layer > order > CustomAxis (0,1,0): küçük Y önde)
                int layerId, order;
                float sortY;
                if (t.Group != null && t.Group.isActiveAndEnabled)
                {
                    layerId = t.Group.sortingLayerID;
                    order = t.Group.sortingOrder;
                    sortY = t.Group.transform.position.y;
                }
                else
                {
                    layerId = r.sortingLayerID;
                    order = r.sortingOrder;
                    sortY = t.Sprite != null && t.Sprite.spriteSortPoint == SpriteSortPoint.Pivot
                        ? r.transform.position.y
                        : r.bounds.center.y;
                }
                int layer = SortingLayer.GetLayerValueFromID(layerId);
                bool inFront;
                if (layer != playerLayer) inFront = layer > playerLayer;
                else if (order != playerOrder) inFront = order > playerOrder;
                else inFront = sortY < feet.y + (was ? s.DepthHysteresis : -s.DepthHysteresis);
                if (!inFront)
                {
                    Set(t, false, was);
                    continue;
                }

                Rect rect = WorldRect(t);
                if (t.Cutaway != RoomVisualGroup.CutawayMode.Always && rect.height < s.MinOccluderHeight)
                {
                    Set(t, false, was);
                    continue;
                }
                float ix = Mathf.Min(rect.xMax, bx1) - Mathf.Max(rect.xMin, bx0);
                float iy = Mathf.Min(rect.yMax, by1) - Mathf.Max(rect.yMin, by0);
                float coverage = ix > 0f && iy > 0f ? ix * iy / bodyArea : 0f;
                Set(t, coverage >= (was ? s.CoverageExit : s.CoverageEnter), was);
            }
        }

        private void Set(RoomVisibilityZone.Target t, bool occluded, bool was)
        {
            t.occluded = occluded;
            if (occluded != was) StateChanges++;
        }

        // Sprite'ın şeffaf kenarsız (sıkı) dünya dikdörtgeni; dönüş yok sayılır (sarkıt lamba ±10° önemsiz).
        private static Rect WorldRect(RoomVisibilityZone.Target t)
        {
            var sr = t.Sprite;
            if (sr != null && sr.drawMode == SpriteDrawMode.Simple)
            {
                Rect local = Tight(sr.sprite);
                var tr = sr.transform;
                Vector3 pos = tr.position;
                Vector3 sc = tr.lossyScale;
                float sx = sc.x * (sr.flipX ? -1f : 1f);
                float sy = sc.y * (sr.flipY ? -1f : 1f);
                float x0 = pos.x + local.xMin * sx, x1 = pos.x + local.xMax * sx;
                float y0 = pos.y + local.yMin * sy, y1 = pos.y + local.yMax * sy;
                return Rect.MinMaxRect(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Max(x0, x1), Mathf.Max(y0, y1));
            }
            var b = t.Renderer.bounds;
            return Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
        }

        private static Rect Tight(Sprite sprite)
        {
            if (TightRects.TryGetValue(sprite, out var rect)) return rect;
            var v = sprite.vertices; // sprite başına bir kez (önbellek)
            if (v == null || v.Length == 0)
            {
                var b = sprite.bounds;
                rect = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
            }
            else
            {
                float xMin = v[0].x, yMin = v[0].y, xMax = v[0].x, yMax = v[0].y;
                for (int i = 1; i < v.Length; i++)
                {
                    xMin = Mathf.Min(xMin, v[i].x); yMin = Mathf.Min(yMin, v[i].y);
                    xMax = Mathf.Max(xMax, v[i].x); yMax = Mathf.Max(yMax, v[i].y);
                }
                rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }
            TightRects[sprite] = rect;
            return rect;
        }

        private static void RestoreInstant(RoomVisibilityManager m, RoomVisibilityZone zone)
        {
            var list = zone.CutawayCandidates;
            var block = m.Block;
            var dark = m.Settings.DarknessColor;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                t.occluded = false;
                if (t.cutProgress <= 0f && t.alpha >= 1f) continue;
                t.cutProgress = 0f;
                t.alpha = 1f;
                t.Apply(block, dark);
            }
        }
    }
}
