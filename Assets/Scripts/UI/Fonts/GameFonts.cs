using UnityEngine;

namespace AfetUyanisi.UI
{
    /// <summary>
    /// Oyunun yazı tipi eşlemesi tek yerde (Assets/Fonts/Resources/AfetUyanisiFonts/GameFonts.asset):
    /// Menu = Press Start 2P (ana menü, ayarlar, duraklatma), Story = Pixel Operator TR Bold (diyalog, hikâye,
    /// haber, etkileşim ipuçları ve diğer oyun içi yazılar), Objective = Pixelify Sans (görev paneli).
    /// Kodla kurulan arayüzler fontu buradan alır; Inspector'da font atanmışsa o kullanılır.
    /// Piksel ızgarası: Press Start 2P 8 px, Pixel Operator 16 px em; en net görünüm bu boyların katlarında.
    /// </summary>
    [CreateAssetMenu(menuName = "Afet Uyanisi/Game Fonts", fileName = "GameFonts")]
    public sealed class GameFonts : ScriptableObject
    {
        public const string ResourcePath = "AfetUyanisiFonts/GameFonts";

        [SerializeField] private Font menu;
        [SerializeField] private Font story;
        [SerializeField] private Font objective;

        private static GameFonts instance;
        private static bool reported;

        public static Font Menu => Pick(Instance != null ? Instance.menu : null, "Menu");
        public static Font Story => Pick(Instance != null ? Instance.story : null, "Story");
        public static Font Objective => Pick(Instance != null ? Instance.objective : null, "Objective");

        /// <summary>
        /// Nunito/Pixelify boyutuyla yazılmış arayüzler için Pixel Operator boyu bu katsayıyla büyütülür: 18/16'da
        /// küçük harf yüksekliği (7/16 em) ve satır genişliği Nunito ile neredeyse aynı (ölçüm: +%2), kutular taşmaz.
        /// </summary>
        public const float StoryScale = 1.125f;

        /// <summary>Eski (Nunito/Pixelify) boyutu, verilen font Story ise Pixel Operator karşılığına çevirir.</summary>
        public static int SizeFor(Font font, int legacySize)
        {
            if (font == null || Instance == null || font != Instance.story) return legacySize;
            return Mathf.Max(1, Mathf.RoundToInt(legacySize * StoryScale));
        }

        /// <summary>Best fit alt sınırı için SizeFor: aşağı yuvarlar, yazı eskiden sığdığı boya kadar küçülebilsin.</summary>
        public static int MinSizeFor(Font font, int legacyMinSize)
        {
            if (font == null || Instance == null || font != Instance.story) return legacyMinSize;
            return Mathf.Max(1, Mathf.FloorToInt(legacyMinSize * StoryScale));
        }

        private static GameFonts Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<GameFonts>(ResourcePath);
                return instance;
            }
        }

        private static Font Pick(Font font, string slot)
        {
            if (font != null) return font;
            if (!reported)
            {
                reported = true;
                Debug.LogError($"[GameFonts] {slot} fontu bulunamadı (Resources/{ResourcePath}); geçici olarak varsayılan font kullanılıyor.");
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
