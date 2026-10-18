using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Chapters.FloodEvening
{
    /// <summary>
    /// SADECE test sahnesi (Assets/Scenes/Test/Test_Eren_TreeFall.unity): Play'e basınca ağaç düşüşünü kendiliğinden
    /// oynatır. Harness serbest dolaşımı açınca Cenk'i tetik noktasının önüne koyar, ağacın salınması görünsün diye
    /// kısa bekler ve FloodTreeFallEvent.Play()'i çağırır. autoPlay kapatılırsa hiçbir şey yapmaz (oyuncu kendisi yürür).
    /// Bölüm sahnesinde KULLANILMAZ.
    /// </summary>
    public class FloodTreeFallDemo : MonoBehaviour
    {
        [SerializeField] private bool autoPlay = true;
        [SerializeField] private FloodTreeFallEvent treeFall;
        [SerializeField] private IsoPlayerController player;
        [Tooltip("Cenk'in olaydan önce durduğu yer (dünya).")]
        [SerializeField] private Vector3 standAt = new Vector3(0f, -1.4f, 0f);
        [Tooltip("Ağaç düşmeden önce bekleme (sn): fırtınadaki salınım görünsün.")]
        [SerializeField, Min(0f)] private float delay = 1.5f;

        private IEnumerator Start()
        {
            if (!autoPlay) yield break;
            if (treeFall == null) treeFall = FindAnyObjectByType<FloodTreeFallEvent>();
            if (player == null) player = FindAnyObjectByType<IsoPlayerController>();
            if (treeFall == null) yield break;

            while (!FloodEveningHub.FreeRoam) yield return null; // harness hazır (uyarı kapalı/atlandı)
            if (player != null) player.PlaceAt(standAt, Vector2.up);
            for (float t = 0f; t < delay; t += Time.deltaTime) yield return null;
            treeFall.Play();
        }
    }
}
