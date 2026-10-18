using UnityEngine;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Deprem modu arayüzünün ortak görünümü: yazı tipi + sprite yuvaları (Assets/Art/UI/Earthquake/UI_EQ_*.png).
    /// Bütün deprem View'ları bunu kendi Inspector alanından, yoksa üst objelerden, yoksa sahnede arayarak bulur.
    /// Hiçbir sprite atanmasa da arayüz aynı yerleşimle düz renklerle çalışır.
    /// 9-slice sprite'ların border'ı import ayarında tanımlı olmalı (yoksa Simple çizilir). PPU fark etmez:
    /// View'lar 1 sprite pikseli = pixelScale referans birimi (640x360) olacak şekilde ölçekler.
    /// </summary>
    public class EarthquakeUISkin : MonoBehaviour
    {
        [Header("Yazı")]
        [Tooltip("Türkçe karakterleri (ç ğ ı İ ö ş ü) içeren font: Assets/Fonts/PixelOperator/PixelOperatorTR-Bold.ttf. Boşsa oyun içi yazı tipi (GameFonts.Story).")]
        [SerializeField] private Font font;
        [Tooltip("1 sprite pikseli = kaç referans (640x360) birimi. Piksel sanatı için 1.")]
        [SerializeField] private float pixelScale = 1f;

        [Header("Genel")]
        [Tooltip("UI_EQ_Dim (tam ekran karartma, alfası gömülü).")]
        [SerializeField] private Sprite dim;
        [Tooltip("UI_EQ_Button (lacivert genel buton, 9-slice).")]
        [SerializeField] private Sprite button;
        [Tooltip("UI_EQ_Button_Hover.")]
        [SerializeField] private Sprite buttonHover;
        [Tooltip("UI_EQ_ButtonStart (yeşil ana buton: BAŞLA / Tekrar Dene, 9-slice).")]
        [SerializeField] private Sprite buttonStart;
        [Tooltip("UI_EQ_ButtonStart_Hover.")]
        [SerializeField] private Sprite buttonStartHover;
        [Tooltip("Tuş rozeti ([E], [1]). Boşsa İsim plakası (UI_EQ_NamePlate) kullanılır.")]
        [SerializeField] private Sprite keyCap;
        [Tooltip("UI_EQ_Check (12x12).")]
        [SerializeField] private Sprite check;
        [Tooltip("UI_EQ_Cross (12x12).")]
        [SerializeField] private Sprite cross;

        [Header("Sayaç")]
        [Tooltip("UI_EQ_TimerPanel (9-slice L21 R5 T4 B6).")]
        [SerializeField] private Sprite timerPanel;
        [Tooltip("UI_EQ_ClockIcon (13x13).")]
        [SerializeField] private Sprite clockIcon;

        [Header("Telefon")]
        [Tooltip("UI_EQ_PhoneFrame (168x288, Simple).")]
        [SerializeField] private Sprite phoneFrame;
        [Tooltip("UI_EQ_PhoneNotifCard (9-slice L17 R4 T15 B6).")]
        [SerializeField] private Sprite phoneNotifCard;

        [Header("Bilgi kartı / uyarı")]
        [Tooltip("UI_EQ_InfoCard (9-slice L6 R6 T20 B6).")]
        [SerializeField] private Sprite infoCard;
        [Tooltip("UI_EQ_Toast (9-slice L23 R5 T5 B7).")]
        [SerializeField] private Sprite toast;
        [SerializeField] private Sprite iconHazard;
        [SerializeField] private Sprite iconSafe;
        [SerializeField] private Sprite iconInfo;
        [SerializeField] private Sprite iconExit;

        [Header("Anne sorusu")]
        [Tooltip("UI_EQ_QuestionPanel (9-slice 8/8/8/10).")]
        [SerializeField] private Sprite questionPanel;
        [Tooltip("UI_EQ_PortraitFrame (72x72, 9-slice 4).")]
        [SerializeField] private Sprite portraitFrame;
        [Tooltip("UI_EQ_NamePlate (9-slice 4).")]
        [SerializeField] private Sprite nameplate;
        [Tooltip("Open() portre vermezse kullanılır (anne portresi).")]
        [SerializeField] private Sprite defaultPortrait;
        [Tooltip("UI_EQ_Option (9-slice L20 R5 T5 B7).")]
        [SerializeField] private Sprite option;
        [SerializeField] private Sprite optionHover;
        [SerializeField] private Sprite optionCorrect;
        [SerializeField] private Sprite optionWrong;
        [Tooltip("UI_EQ_TimeBar_Back (9-slice 3).")]
        [SerializeField] private Sprite timeBarBack;
        [Tooltip("UI_EQ_TimeBar_Fill (beyaz, renklendirilir; 9-slice 2).")]
        [SerializeField] private Sprite timeBarFill;

        [Header("Korunma (Çök-Kapan-Tutun)")]
        [Tooltip("UI_EQ_ProtectPanel (9-slice 8/8/10/10).")]
        [SerializeField] private Sprite protectPanel;
        [SerializeField] private Sprite stepDotOff;
        [SerializeField] private Sprite stepDotOn;
        [SerializeField] private Sprite stepDotDone;
        [Tooltip("UI_EQ_HoldBar_Back (9-slice 4).")]
        [SerializeField] private Sprite holdBarBack;
        [Tooltip("UI_EQ_HoldBar_Fill (yeşil gömülü, 9-slice L2 R2 T3 B3).")]
        [SerializeField] private Sprite holdBarFill;

        [Header("Rapor")]
        [Tooltip("UI_EQ_ReportPanel (9-slice L8 R8 T33 B10).")]
        [SerializeField] private Sprite reportPanel;
        [SerializeField] private Sprite badgeMaster;
        [SerializeField] private Sprite badgeReady;
        [SerializeField] private Sprite badgeGrowing;
        [SerializeField] private Sprite badgeLearn;

        public Font Font => font;
        public float PixelScale => pixelScale > 0f ? pixelScale : 1f;
        public Sprite Dim => dim;
        public Sprite Button => button;
        public Sprite ButtonHover => buttonHover;
        public Sprite ButtonStart => buttonStart;
        public Sprite ButtonStartHover => buttonStartHover;
        public Sprite KeyCap => keyCap != null ? keyCap : nameplate;
        public Sprite Check => check;
        public Sprite Cross => cross;
        public Sprite TimerPanel => timerPanel;
        public Sprite ClockIcon => clockIcon;
        public Sprite PhoneFrame => phoneFrame;
        public Sprite PhoneNotifCard => phoneNotifCard;
        public Sprite InfoCard => infoCard;
        public Sprite Toast => toast;
        public Sprite IconHazard => iconHazard;
        public Sprite IconSafe => iconSafe;
        public Sprite IconInfo => iconInfo;
        public Sprite IconExit => iconExit;
        public Sprite QuestionPanel => questionPanel;
        public Sprite PortraitFrame => portraitFrame;
        public Sprite Nameplate => nameplate;
        public Sprite DefaultPortrait => defaultPortrait;
        public Sprite Option => option;
        public Sprite OptionHover => optionHover;
        public Sprite OptionCorrect => optionCorrect;
        public Sprite OptionWrong => optionWrong;
        public Sprite TimeBarBack => timeBarBack;
        public Sprite TimeBarFill => timeBarFill;
        public Sprite ProtectPanel => protectPanel;
        public Sprite StepDotOff => stepDotOff;
        public Sprite StepDotOn => stepDotOn;
        public Sprite StepDotDone => stepDotDone;
        public Sprite HoldBarBack => holdBarBack;
        public Sprite HoldBarFill => holdBarFill;
        public Sprite ReportPanel => reportPanel;

        public Sprite KindIcon(HazardClass kind)
        {
            switch (kind)
            {
                case HazardClass.Hazard: return iconHazard;
                case HazardClass.Protective:
                case HazardClass.Safety: return iconSafe != null ? iconSafe : iconInfo;
                case HazardClass.Exit: return iconExit != null ? iconExit : iconInfo;
                default: return iconInfo;
            }
        }

        /// <summary>0 = AFET BİLİNCİ USTASI, 1 = HAZIRLIKLI, 2 = GELİŞİYOR, 3 = DAHA FAZLA ÖĞREN.</summary>
        public Sprite RankBadge(int tier)
        {
            switch (tier)
            {
                case 0: return badgeMaster;
                case 1: return badgeReady;
                case 2: return badgeGrowing;
                default: return badgeLearn;
            }
        }
    }
}
