using System.Collections;
using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Menüdeki sürekli ortam sesi (uyku ortamı). Clip boşsa kodla üretilen oda sesi çalınır; yumuşak girer.
    [RequireComponent(typeof(AudioSource))]
    public class AmbientLoop : MonoBehaviour
    {
        [SerializeField] AudioClip clip;
        [SerializeField] float volume = 0.25f;
        [SerializeField] float fadeInDuration = 2f;

        IEnumerator Start()
        {
            var source = GetComponent<AudioSource>();
            source.clip = clip != null ? clip : MenuAudioSynth.RoomTone;
            source.loop = true;
            source.volume = 0f;
            source.Play();
            for (float t = 0f; t < fadeInDuration; t += Time.unscaledDeltaTime)
            {
                source.volume = volume * t / fadeInDuration;
                yield return null;
            }
            source.volume = volume;
        }
    }
}
