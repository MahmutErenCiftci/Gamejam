using System;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// "E'ye art arda bas" ilerleme göstergesinin sözleşmesi (SEL3-30 → SEL3-20). Arda'nın BranchObstacle bileşeni bunu
    /// Inspector'dan alır; somut görünüm FloodMashProgressView (FE_MashProgress.prefab).
    /// Görünüm tuş OKUMAZ: her geçerli E basışında modül SetProgress'i yeni değerle çağırır, görünüm artışı görünce küçük
    /// bir zıplama + parlama oynatır. Gizliyken çağrılan SetProgress değeri sessizce saklar (uzaklaşıp dönünce korunur).
    /// </summary>
    public abstract class MashProgressViewBase : MonoBehaviour
    {
        /// <summary>Fareyle tuş kapağına/çubuğa tıklandı (isteğe bağlı E eşdeğeri; modül isterse bir basış sayar).</summary>
        public event Action Clicked;

        /// <summary>İpucunu ve çubuğu gösterir. İlerlemeyi sıfırlamaz.</summary>
        public abstract void Show();

        /// <summary>İpucunu ve çubuğu gizler. İlerlemeyi sıfırlamaz.</summary>
        public abstract void Hide();

        /// <summary>0..1 ilerleme. Görünürken artış = basış geri bildirimi; azalış/gizliyken = anında ayar.</summary>
        public abstract void SetProgress(float progress01);

        /// <summary>Şu an ekranda mı.</summary>
        public abstract bool IsVisible { get; }

        protected void RaiseClicked() => Clicked?.Invoke();
    }
}
