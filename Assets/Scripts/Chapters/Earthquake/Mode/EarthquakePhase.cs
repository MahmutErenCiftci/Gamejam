namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Deprem modunun evresi. Intro: uyanış + telefon; Prep: 120 sn hazırlık; Quake: sarsıntı + korunma;
    /// After: sarsıntı sonrası (çanta, koridor, toplanma alanı); Report: sonuç raporu.
    /// HazardZone hangi evrelerde çalışacağını buna göre seçer.
    /// </summary>
    public enum EarthquakePhase { Intro, Prep, Quake, After, Report }
}
