using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// Akşam sel bölümünün oyuncu ayarları (PlayerPrefs). Şimşek ışığı ışığa duyarlı oyuncular için kapatılabilir:
    /// "AfetUyanisi.Lightning" (1 = açık, varsayılan; 0 = kapalı). Kapalıyken gök gürültüsü sesi yine çalar, ekran aydınlanmaz.
    /// Oyunda [L] tuşu değiştirir (FloodLightning). Menü ayar panelinde de bir satır olarak sunulabilir (Berk): <c>FloodEveningSettings.LightningEnabled = false;</c>
    /// </summary>
    public static class FloodEveningSettings
    {
        public const string LightningKey = "AfetUyanisi.Lightning";

        /// <summary>Şimşek aydınlanması açık mı (varsayılan açık).</summary>
        public static bool LightningEnabled
        {
            get => PlayerPrefs.GetInt(LightningKey, 1) != 0;
            set { PlayerPrefs.SetInt(LightningKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
