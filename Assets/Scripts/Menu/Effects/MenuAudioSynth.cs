using UnityEngine;

namespace AfetUyanisi.Menu
{
    // Menü için kodla üretilen basit sesler: yağmur döngüsü, gök gürültüsü, deprem gürlemesi.
    // Lisanslı ses dosyası gelene kadar yedektir; efekt scriptlerinde clip atanırsa o kullanılır.
    // Her ses ilk istendiğinde bir kez üretilir ve sahne değişse de saklanır.
    public static class MenuAudioSynth
    {
        const int SampleRate = 22050;

        static AudioClip rain;
        static AudioClip thunder;
        static AudioClip rumble;

        public static AudioClip Rain => rain != null ? rain : (rain = CreateRain());
        public static AudioClip Thunder => thunder != null ? thunder : (thunder = CreateThunder());
        public static AudioClip Rumble => rumble != null ? rumble : (rumble = CreateRumble());
        public static AudioClip RoomTone => roomTone != null ? roomTone : (roomTone = CreateRoomTone());

        static AudioClip roomTone;

        // Uyku ortamı: çok alçak, yumuşak oda uğultusu; 4 sn'lik kesintisiz döngü.
        static AudioClip CreateRoomTone()
        {
            int length = SampleRate * 4;
            int crossfade = SampleRate / 2;
            var random = new System.Random(31);
            var source = new float[length + crossfade];
            float brown = 0f, lowPass = 0f;
            for (int i = 0; i < source.Length; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                brown = (brown + 0.02f * white) * 0.998f;
                lowPass += 0.03f * (brown - lowPass);
                // Yavaş "nefes" dalgası: sesin hafifçe kabarıp inmesi
                float swell = 0.8f + 0.2f * Mathf.Sin(i / (float)SampleRate * Mathf.PI * 0.5f);
                source[i] = lowPass * swell;
            }
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                if (i < crossfade)
                {
                    float w = i / (float)crossfade;
                    data[i] = source[i] * w + source[length + i] * (1f - w);
                }
                else
                {
                    data[i] = source[i];
                }
            }
            return Build("MenuRoomTone", data, 0.5f);
        }

        // 2 sn'lik kesintisiz döngü: uçlar çapraz geçişle birleştirilir, döngü noktasında tık olmaz.
        static AudioClip CreateRain()
        {
            int length = SampleRate * 2;
            int crossfade = SampleRate / 5;
            var random = new System.Random(7);
            var source = new float[length + crossfade];
            float lowPass = 0f;
            for (int i = 0; i < source.Length; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                lowPass += 0.2f * (white - lowPass);
                // Hışırtı + daha yumuşak alçak katman; arada tek tük damla tıkırtısı.
                float drop = random.NextDouble() < 0.0008 ? (float)(random.NextDouble() * 0.6) : 0f;
                source[i] = white * 0.18f + lowPass * 0.9f + drop;
            }

            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                if (i < crossfade)
                {
                    float w = i / (float)crossfade;
                    data[i] = source[i] * w + source[length + i] * (1f - w);
                }
                else
                {
                    data[i] = source[i];
                }
            }
            return Build("MenuRain", data, 0.5f);
        }

        // Kısa çatırtı + uzun, alçalan gürleme.
        static AudioClip CreateThunder()
        {
            int length = (int)(SampleRate * 3.2f);
            var random = new System.Random(11);
            var data = new float[length];
            float brown = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                brown = (brown + 0.02f * white) * 0.998f;
                float attack = Mathf.Clamp01(t / 0.04f);
                float body = brown * 18f * attack * Mathf.Exp(-t * 1.3f);
                float crack = white * Mathf.Exp(-t * 14f) * 0.35f;
                data[i] = body + crack;
            }
            return Build("MenuThunder", data, 0.9f);
        }

        // Deprem önizlemesi: alçak, dalgalanan gürleme; yumuşak başlar, yumuşak biter.
        static AudioClip CreateRumble()
        {
            int length = (int)(SampleRate * 1.6f);
            var random = new System.Random(23);
            var data = new float[length];
            float brown = 0f;
            float lowPass = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                brown = (brown + 0.02f * white) * 0.999f;
                lowPass += 0.05f * (brown - lowPass);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float wobble = 0.75f + 0.25f * Mathf.Sin(i / (float)SampleRate * Mathf.PI * 2f * 7f);
                data[i] = lowPass * envelope * wobble;
            }
            return Build("MenuRumble", data, 0.9f);
        }

        static AudioClip Build(string name, float[] data, float peak)
        {
            float max = 0.0001f;
            foreach (float s in data)
                max = Mathf.Max(max, Mathf.Abs(s));
            float gain = peak / max;
            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;

            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
