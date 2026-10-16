using System;
using UnityEngine;

namespace AfetUyanisi.News
{
    // Assets/Data/News/*.json içindeki TV haber yayınının veri modeli.
    [Serializable]
    public class NewsLegendEntry
    {
        public string color;
        public string label;
    }

    [Serializable]
    public class NewsBroadcastData
    {
        public string id;
        public string channelName;
        public string programName;
        public string fictionNotice;
        public string speakerName;
        public string breakingLabel;
        public string headline;
        public string subheadline;
        public string[] lines;
        public string legendTitle;
        public string legendNote;
        public NewsLegendEntry[] legend;

        public static NewsBroadcastData FromJson(TextAsset json)
        {
            if (json == null)
            {
                Debug.LogError("[News] Haber JSON'u atanmamış.");
                return null;
            }

            var data = JsonUtility.FromJson<NewsBroadcastData>(json.text);
            if (data == null || data.lines == null || data.lines.Length == 0)
            {
                Debug.LogError($"[News] '{json.name}' okunamadı veya 'lines' boş.");
                return null;
            }
            return data;
        }
    }
}
