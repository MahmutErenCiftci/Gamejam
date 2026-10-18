using AfetUyanisi.Core;
using UnityEngine;

namespace AfetUyanisi.Chapters.Flood
{
    /// <summary>
    /// Okul çıkışı (FloodSchool) → akşam sel sokağı (FloodEvening) geçişinde taşınan küçük bilgi.
    /// FloodSchoolController sahneyi değiştirmeden hemen önce MarkArrival çağırır; FloodEveningController Awake'te
    /// ConsumeArrival ile okur (okunan bilgi silinir). Böylece FloodEvening kendi "SEL BÖLÜMÜ" kartını göstermez,
    /// Cenk sokağa yürüyerek girer ve yağmur aynı şiddette sürer. FloodEvening doğrudan açılırsa (test) bilgi yoktur,
    /// sahne eskisi gibi kendi kartıyla başlar. GameSession bayrağı değil: FloodEvening Awake'te bayrakları siler.
    /// Sel sayacı dolup "Tekrar Dene" seçilirse (RetryInStreet) okul ve köprü yeniden oynatılmaz: süre okulda işlemediği
    /// için başarısızlık yalnız mahalle bölümüne aittir; FloodEvening okuldan gelinmiş gibi baştan kurulur.
    /// </summary>
    public static class FloodSchoolHandoff
    {
        private static bool pending;
        private static float rainIntensity = -1f;
        // Bu oyunda okuldan en son hangi yağmurla gelindi (tekrar denemede aynı geliş; -1: okuldan gelinmedi).
        private static float lastArrivalRain = -1f;

        /// <summary>Okul çıkışı bitti, FloodEvening okuldan gelindiğini bilsin (rain: o anki yağmur şiddeti, 0..1).</summary>
        public static void MarkArrival(float rain)
        {
            pending = true;
            rainIntensity = rain;
            lastArrivalRain = Mathf.Max(0f, rain);
        }

        /// <summary>Okuldan mı gelindi? Bilgiyi okur ve siler (rain: okuldaki yağmur şiddeti; yoksa -1).</summary>
        public static bool ConsumeArrival(out float rain)
        {
            bool was = pending;
            rain = was ? rainIntensity : -1f;
            pending = false;
            rainIntensity = -1f;
            return was;
        }

        /// <summary>
        /// FloodEvening başarısız ekranındaki "Tekrar Dene": bu bölüm okuldan başladıysa FloodEvening'i okuldan gelinmiş gibi
        /// yeniden açar (kart yok, okul/köprü tekrar oynanmaz) ve true döner. Okuldan gelinmediyse (sahne tek başına açıldı)
        /// ya da FloodEvening build listesinde yoksa false döner; çağıran GameSceneRouter.RestartCurrentChapter kullanır.
        /// </summary>
        public static bool RetryInStreet()
        {
            if (lastArrivalRain < 0f || !Application.CanStreamedLevelBeLoaded(GameSceneRouter.FloodScene)) return false;
            MarkArrival(lastArrivalRain);
            Debug.Log("[FloodSchool] Tekrar Dene → FloodEvening (okul ve köprü tekrar oynanmaz)");
            GameSceneRouter.LoadRoom(GameSceneRouter.FloodScene);
            return true;
        }

        /// <summary>Okul bölümü yeniden başladı (yeni oyun ya da Sel menüden yeniden seçildi): eski geliş bilgisi silinir.</summary>
        public static void ResetRun()
        {
            pending = false;
            rainIntensity = -1f;
            lastArrivalRain = -1f;
        }

        // Play'ler arasında static değer kalmasın (domain reload kapatılırsa diye).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetRun();
    }
}
