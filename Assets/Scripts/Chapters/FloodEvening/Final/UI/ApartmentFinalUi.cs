using UnityEngine;
using UnityEngine.UI;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// FE_ApartmentLockUi prefab'ının kökü: tek Screen Space Overlay tuval (sıra 70, ConstantPixelSize, tam sayı ölçek)
    /// + iki yakın plan görünümü (Letter → ApartmentLetterView, Lock → ApartmentLockView) + apartment_final.json bağlantısı.
    /// Kullanım (dünya tarafı):
    ///   var ui = Instantiate(prefab).GetComponent&lt;ApartmentFinalUi&gt;();   // sahne köküne ya da UI olmayan bir objenin altına
    ///   yield return ui.Letter.Show(ui.Data);
    ///   yield return ui.Lock.Run(ui.Data);  if (ui.Lock.HandleTurned) { ...kapıyı aç... }
    /// Görünümler Hub'a kendileri "read_letter" / "lock" bildirir; çağıran ayrıca bildirmez.
    /// </summary>
    public class ApartmentFinalUi : MonoBehaviour
    {
        [SerializeField] private ApartmentLetterView letter;
        [SerializeField] private ApartmentLockView lockView;
        [Tooltip("Assets/Data/FloodEvening/apartment_final.json (PIN + metinler)")]
        [SerializeField] private TextAsset dataJson;

        private ApartmentFinalData data;
        private CanvasScaler scaler;

        public ApartmentLetterView Letter => letter;
        public ApartmentLockView Lock => lockView;

        /// <summary>apartment_final.json'dan okunan veri (ilk erişimde yüklenir; dosya yoksa varsayılanlar).</summary>
        public ApartmentFinalData Data => data ??= ApartmentFinalData.Load(dataJson);

        private void Awake()
        {
            if (letter == null) letter = GetComponentInChildren<ApartmentLetterView>(true);
            if (lockView == null) lockView = GetComponentInChildren<ApartmentLockView>(true);
            scaler = GetComponent<CanvasScaler>();
            ApartmentUi.ApplyScale(scaler);
        }

        private void LateUpdate() => ApartmentUi.ApplyScale(scaler);

        /// <summary>İki görünümü de tamamen sıfırlar (Hub.ResetRequested zaten her birini ayrıca sıfırlar).</summary>
        public void ResetAll()
        {
            if (letter != null) letter.ResetView();
            if (lockView != null) lockView.ResetView();
        }
    }
}
