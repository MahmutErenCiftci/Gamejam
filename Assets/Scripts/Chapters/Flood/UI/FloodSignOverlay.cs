using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// F07 "[E] Tabelayı incele": tabelanın büyük, okunur hâli (ekranın üst ortasında, diyalog balonunun üstünde).
    /// Arayüz kodla kurulur; obje açılıp kapanarak gösterilir (FloodChapterController.signOverlay).
    /// </summary>
    public class FloodSignOverlay : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private int sortingOrder = 40;
        [SerializeField] private string streetName = "OKUL CADDESİ";
        [SerializeField] private string line1 = "SU BİRİKİNTİSİ";
        [SerializeField] private string line2 = "TEHLİKELİDİR!";
        [SerializeField] private string line3 = "YOL KAPALI · Yayalar köprü üstü yaya yolunu kullanın";

        private bool built;

        private void Awake() => Build();

        private void Build()
        {
            if (built) return;
            built = true;
            if (font == null) font = AfetUyanisi.UI.GameFonts.Story;
            if (!TryGetComponent<Canvas>(out var canvas)) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            if (!TryGetComponent<CanvasScaler>(out var scaler)) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.matchWidthOrHeight = 0.5f;

            var board = Box("Board", transform, new Color(0.08f, 0.08f, 0.12f), Vector2.zero, new Vector2(236f, 104f));
            board.anchorMin = board.anchorMax = board.pivot = new Vector2(0.5f, 1f);
            board.anchoredPosition = new Vector2(0f, -30f);
            var inner = Box("Yellow", board, new Color(0.95f, 0.76f, 0.19f), Vector2.zero, new Vector2(230f, 98f));
            var street = Box("Street", board, new Color(0.18f, 0.36f, 0.66f), new Vector2(0f, 58f), new Vector2(120f, 18f));
            Label(street, streetName, 10, Color.white, Vector2.zero, new Vector2(118f, 18f));
            // uyarı işareti (kırmızı eşkenar dörtgen içinde "!")
            var mark = Box("Mark", inner, new Color(0.66f, 0.14f, 0.09f), new Vector2(-88f, 14f), new Vector2(26f, 26f));
            mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Label(inner, "!", 18, new Color(0.98f, 0.92f, 0.7f), new Vector2(-88f, 14f), new Vector2(30f, 30f));
            Label(inner, line1, 17, new Color(0.08f, 0.08f, 0.12f), new Vector2(18f, 26f), new Vector2(180f, 24f));
            Label(inner, line2, 17, new Color(0.66f, 0.14f, 0.09f), new Vector2(18f, 3f), new Vector2(180f, 24f));
            Label(inner, line3, 8, new Color(0.08f, 0.08f, 0.12f), new Vector2(0f, -32f), new Vector2(216f, 20f));
        }

        private static RectTransform Box(string name, Transform parent, Color color, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private void Label(RectTransform parent, string text, int size, Color color, Vector2 pos, Vector2 box)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = AfetUyanisi.UI.GameFonts.SizeFor(font, size);
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
        }
    }
}
