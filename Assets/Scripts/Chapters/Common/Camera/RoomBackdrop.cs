using UnityEngine;

namespace AfetUyanisi.Chapters
{
    /// <summary>
    /// Odaların arkasındaki simsiyah boşluk yerine kameraya bağlı, piksel sanatla uyumlu arka plan (Mahmut):
    /// bantlı + Bayer dither'lı dikey gradyan ve dither'lı kenar vinyeti. Her şeyin arkasında çizilir (sorting order
    /// -1000/-999); sokak gibi kendi gökyüzü olan yerlerde gökyüzü önde kalır. Palet sahneye ve kamera konumuna göre
    /// GameCameraSettings.PaletteFor'dan gelir. Deprem sarsıntısında (SetAlarm) vinyet yavaşça kırmızımsı olur, flaş yok.
    /// Sokaktaki gökyüzü bandı için CameraBackdrop ayrı ve değişmeden kalır.
    /// GameCameraSettings tarafından Main Camera'ya eklenir; elle eklemek gerekmez.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class RoomBackdrop : MonoBehaviour
    {
        public const int GradientOrder = -1000;
        public const int VignetteOrder = -999;

        private const int GradientLevels = 7;      // iki durak arası bant sayısı
        private const int VignetteLevels = 5;
        private const float VignetteMaxAlpha = 0.85f;
        private const float AlarmFadeSpeed = 1.5f; // 1/sn

        private static readonly float[] Bayer4 =
        {
            0f / 16f, 8f / 16f, 2f / 16f, 10f / 16f,
            12f / 16f, 4f / 16f, 14f / 16f, 6f / 16f,
            3f / 16f, 11f / 16f, 1f / 16f, 9f / 16f,
            15f / 16f, 7f / 16f, 13f / 16f, 5f / 16f,
        };

        private static bool alarm;

        /// <summary>Deprem sarsıntısı başlayınca true, bitince false (EarthquakeCameraShake çağırır).</summary>
        public static void SetAlarm(bool on) => alarm = on;
        public static bool Alarm => alarm;

        private Camera cam;
        private string sceneName;
        private SpriteRenderer gradientRenderer, vignetteRenderer;
        private Texture2D gradientTex, vignetteTex;
        private int texW, texH;
        private GameCameraSettings.BackdropPalette palette;
        private bool hasPalette;
        private float alarmLevel;

        /// <summary>Sahne adı (palet seçimi için).</summary>
        public void Configure(string scene) => sceneName = scene;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (string.IsNullOrEmpty(sceneName)) sceneName = gameObject.scene.name;
            gradientRenderer = CreateLayer("RoomBackdrop_Gradient", GradientOrder);
            vignetteRenderer = CreateLayer("RoomBackdrop_Vignette", VignetteOrder);
        }

        private SpriteRenderer CreateLayer(string layerName, int order)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(transform, false);
            float depth = Mathf.Clamp(cam != null ? cam.farClipPlane * 0.5f : 50f, 1f, 50f);
            go.transform.localPosition = new Vector3(0f, 0f, depth);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        private void LateUpdate()
        {
            if (cam == null) return;
            EnsureSize();

            var p = GameCameraSettings.PaletteFor(sceneName, transform.position);
            if (!hasPalette || !p.SameAs(palette))
            {
                palette = p;
                hasPalette = true;
                PaintGradient();
            }

            alarmLevel = Mathf.MoveTowards(alarmLevel, alarm ? 1f : 0f, AlarmFadeSpeed * Time.deltaTime);
            var vig = Color.Lerp(palette.vignette, GameCameraSettings.AlarmVignette, alarmLevel);
            vig.a = Mathf.Lerp(1f, GameCameraSettings.AlarmVignette.a, alarmLevel);
            vignetteRenderer.color = vig;
            cam.backgroundColor = palette.top;
        }

        // Görüntüyü (PixelPerfectCamera'nın gösterdiği alan) kapsayan, 1 doku pikseli = 1 oyun pikseli boyutta doku.
        private void EnsureSize()
        {
            float ppu = GameCameraSettings.PixelsPerUnit;
            float h = cam.orthographic ? cam.orthographicSize * 2f : 10f;
            int needH = Mathf.CeilToInt(h * ppu) + 8;
            int needW = Mathf.CeilToInt(h * cam.aspect * ppu) + 8;
            needW += needW % 2;
            needH += needH % 2;
            if (gradientTex != null && needW <= texW && needH <= texH) return;
            texW = Mathf.Max(needW, texW);
            texH = Mathf.Max(needH, texH);

            if (gradientTex != null) Destroy(gradientTex);
            if (vignetteTex != null) Destroy(vignetteTex);
            gradientTex = NewTexture("RoomBackdrop_Gradient");
            vignetteTex = NewTexture("RoomBackdrop_Vignette");
            hasPalette = false; // yeniden boya
            PaintVignette();
            gradientRenderer.sprite = Sprite.Create(gradientTex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            vignetteRenderer.sprite = Sprite.Create(vignetteTex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        }

        private Texture2D NewTexture(string texName)
        {
            return new Texture2D(texW, texH, TextureFormat.RGBA32, false)
            {
                name = texName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
        }

        private void PaintGradient()
        {
            var px = new Color32[texW * texH];
            int steps = GradientLevels * 2;
            for (int y = 0; y < texH; y++)
            {
                float t = texH > 1 ? (float)y / (texH - 1) : 0f; // 0 alt, 1 üst
                float v = t * steps;
                int lo = Mathf.FloorToInt(v);
                float frac = v - lo;
                for (int x = 0; x < texW; x++)
                {
                    int level = frac > Bayer4[(y & 3) * 4 + (x & 3)] ? lo + 1 : lo;
                    px[y * texW + x] = GradientColor(Mathf.Clamp01((float)level / steps));
                }
            }
            gradientTex.SetPixels32(px);
            gradientTex.Apply(false);
        }

        private Color32 GradientColor(float t)
        {
            Color c = t < 0.5f
                ? Color.Lerp(palette.bottom, palette.mid, t * 2f)
                : Color.Lerp(palette.mid, palette.top, (t - 0.5f) * 2f);
            c.a = 1f;
            return c;
        }

        // Beyaz + kademeli (dither'lı) alfa; renk SpriteRenderer.color'dan gelir (alarmda kırmızımsı).
        private void PaintVignette()
        {
            var px = new Color32[texW * texH];
            for (int y = 0; y < texH; y++)
            {
                float dy = ((y + 0.5f) / texH) * 2f - 1f;
                for (int x = 0; x < texW; x++)
                {
                    float dx = ((x + 0.5f) / texW) * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy); // hafif yatay basık elips; köşeler en koyu
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1.35f, r));
                    float v = a * VignetteLevels;
                    int lo = Mathf.FloorToInt(v);
                    int level = (v - lo) > Bayer4[(y & 3) * 4 + (x & 3)] ? lo + 1 : lo;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01((float)level / VignetteLevels) * VignetteMaxAlpha * 255f);
                    px[y * texW + x] = new Color32(255, 255, 255, alpha);
                }
            }
            vignetteTex.SetPixels32(px);
            vignetteTex.Apply(false);
        }

        private void OnDestroy()
        {
            if (gradientTex != null) Destroy(gradientTex);
            if (vignetteTex != null) Destroy(vignetteTex);
        }
    }
}
