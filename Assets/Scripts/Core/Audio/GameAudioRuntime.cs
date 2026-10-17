using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Core
{
    /// <summary>Statik API'nin coroutine ve AudioSource sahibi. Sahne/prefab gerektirmez.</summary>
    [DisallowMultipleComponent]
    public sealed class GameAudioRuntime : MonoBehaviour
    {
        private sealed class Channel
        {
            public readonly List<AudioSource> Voices = new List<AudioSource>();
            public AudioSource Current;
            public Coroutine Fade;
        }

        private const int SfxVoiceLimit = 12;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, Channel> channels = new Dictionary<string, Channel>();
        private readonly List<AudioSource> effects = new List<AudioSource>();
        private readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
        private int stealIndex;
        private AudioListener fallbackListener;

        private void OnApplicationQuit() => GameAudio.Shutdown();

        /// <summary>Sahnede etkin AudioListener yoksa (ör. Bedroom/Bathroom) kendi dinleyicisini açar; varsa kapatır.</summary>
        public void EnsureListener()
        {
            bool other = false;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
                if (listener != fallbackListener && listener.isActiveAndEnabled) { other = true; break; }
            if (!other && fallbackListener == null) fallbackListener = gameObject.AddComponent<AudioListener>();
            if (fallbackListener != null) fallbackListener.enabled = !other;
        }

        public AudioClip Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (clips.TryGetValue(id, out var clip)) return clip;
            string path = id.Contains("/") ? id :
                id.StartsWith("SFX_") ? "SFX/" + id : id.StartsWith("AMB_") ? "Ambience/" + id :
                id.StartsWith("MUS_") ? "Music/" + id : id;
            // Kullanıcının seçtiği ücretsiz hazır sesler aynı ID'yi geçersiz kılar; sentez paketi yedektir.
            clip = Resources.Load<AudioClip>("Audio/Overrides/" + path.Substring(path.LastIndexOf('/') + 1));
            if (clip == null) clip = Resources.Load<AudioClip>("Audio/" + path);
            clips[id] = clip;
            if (clip == null) Debug.LogWarning("[AUDIO] Klip bulunamadı: Audio/" + path, this);
            return clip;
        }

        private AudioSource CreateSource(string label, bool loop)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = loop ? 128 : 96;
            return source;
        }

        public void PlaySfx(string id, float volume)
        {
            if (volume <= 0f) return;
            var clip = Load(id);
            if (clip == null) return;
            // Aynı efekt aynı anda üst üste binmesin (hover/tık yağmuru).
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(clip, out float last) && now - last < 0.06f) return;
            lastPlayed[clip] = now;
            var voice = effects.Find(x => !x.isPlaying);
            if (voice == null && effects.Count < SfxVoiceLimit)
            {
                voice = CreateSource("SFX", false);
                effects.Add(voice);
            }
            if (voice == null) voice = effects[stealIndex++ % effects.Count];
            voice.Stop();
            voice.clip = clip;
            voice.volume = volume;
            voice.Play();
        }

        public void PlayLoop(string id, string name, float volume, float seconds)
        {
            if (string.IsNullOrEmpty(name)) return;
            var clip = Load(id);
            if (clip == null) return;
            if (!channels.TryGetValue(name, out var channel))
            {
                channel = new Channel();
                channels.Add(name, channel);
            }
            if (channel.Current != null && channel.Current.clip == clip)
            {
                FadeTo(channel, channel.Current, volume, seconds);
                return;
            }
            // Kesilen crossfade'in hâlâ duyulan kaynaklarını koru; boş kaynağı yeniden kullan.
            var voice = channel.Voices.Find(x => !x.isPlaying);
            if (voice == null)
            {
                // Hızlı A/B geçişlerinde aynı klip çalıyor olabilir; zamanını sıfırlamadan geri yükselt.
                voice = channel.Voices.Find(x => x.clip == clip);
            }
            if (voice == null)
            {
                voice = CreateSource("Loop_" + name, true);
                channel.Voices.Add(voice);
            }
            if (!voice.isPlaying)
            {
                voice.clip = clip;
                voice.volume = 0f;
                voice.Play();
            }
            channel.Current = voice;
            FadeTo(channel, voice, volume, seconds);
        }

        public void StopLoop(string name, float seconds)
        {
            if (name == null || !channels.TryGetValue(name, out var channel)) return;
            channel.Current = null;
            FadeTo(channel, null, 0f, seconds);
        }

        public void SetChannelVolume(string name, float volume, float seconds)
        {
            if (name == null || !channels.TryGetValue(name, out var channel) || channel.Current == null) return;
            FadeTo(channel, channel.Current, volume, seconds);
        }

        private void FadeTo(Channel channel, AudioSource target, float volume, float seconds)
        {
            if (channel.Fade != null) StopCoroutine(channel.Fade);
            if (seconds <= 0f) { ApplyFinal(channel, target, volume); channel.Fade = null; }
            else channel.Fade = StartCoroutine(Fade(channel, target, volume, seconds));
        }

        private IEnumerator Fade(Channel channel, AudioSource target, float volume, float seconds)
        {
            var voices = channel.Voices.ToArray();
            var starts = new float[voices.Length];
            for (int i = 0; i < voices.Length; i++) starts[i] = voices[i].volume;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
                for (int i = 0; i < voices.Length; i++)
                    voices[i].volume = Mathf.Lerp(starts[i], voices[i] == target ? volume : 0f, t);
                yield return null;
            }
            ApplyFinal(channel, target, volume);
            channel.Fade = null;
        }

        private static void ApplyFinal(Channel channel, AudioSource target, float volume)
        {
            foreach (var voice in channel.Voices)
            {
                voice.volume = voice == target ? volume : 0f;
                if (voice != target) { voice.Stop(); voice.clip = null; }
            }
        }

        public void StopTransientAudio()
        {
            foreach (var voice in effects) voice.Stop();
            foreach (var pair in channels)
                if (pair.Key != GameAudio.MusicChannel && pair.Key != GameAudio.AmbienceChannel) StopLoop(pair.Key, 0f);
        }
    }
}
