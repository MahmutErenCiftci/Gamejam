using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// [E] ile kullanılan bölüm etkileşimi (söylenti panosu, dal/alternatif geçit, bahçe kapısı, şifre paneli, ipucu levhası...).
    /// FloodEveningController sahnedeki bütün uygulayıcıları bulur, Point'i kendi RoomInteractor'ına kaydeder ve oyuncu
    /// E'ye basınca Play()'i çalıştırır. Modül E tuşunu KENDİSİ dinlemez (aynı girdi iki işlem yapmasın).
    /// Play() boyunca oyuncu hareketi controller tarafından kilitlidir; Play bitince geri açılır.
    /// Modül Play başında Hub.NotifyOpened(id), sonunda Hub.NotifyClosed(id) ve tamamlandıysa Hub.Complete(id) çağırır.
    /// Play iptalle (Esc / Kapat / sağ tık) tamamlanmadan da bitebilir; nokta açık kalır, tekrar denenebilir.
    /// </summary>
    public interface IFloodEveningInteraction
    {
        /// <summary>Dünyadaki [E] noktası (null ise controller bu modülü kaydetmez).</summary>
        Interactable Point { get; }
        /// <summary>Şu an [E] ile kullanılabilir mi (ör. kapı açıldıysa false).</summary>
        bool CanUse { get; }
        /// <summary>Etkileşimin kendisi (panel, ilerleme çubuğu, mini oyun). Bitince döner.</summary>
        IEnumerator Play();
    }

    /// <summary>
    /// Sel uyarısı (telefon bildirimi). Controller Warning durumunda ShowWarning()'i bekler; "Tamam" ile kapanınca döner.
    /// Uygulayıcı yoksa controller kendi yedek kartını gösterir.
    /// </summary>
    public interface IFloodEveningWarning
    {
        IEnumerator ShowWarning();
    }

    /// <summary>Bölüm yeniden kurulurken modülün kendi durumunu sıfırlaması (kapı kapalı, kod boş, dal yerinde...).</summary>
    public interface IFloodEveningResettable
    {
        void ResetModule();
    }
}
