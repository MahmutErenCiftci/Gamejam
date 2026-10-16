using System;
using UnityEngine;

namespace AfetUyanisi.News
{
    // Assets/Data/News/*.json içindeki TV haber yayınının veri modeli.
    [Serializable]
    public class NewsBroadcastData
    {
        public string id;
        public string channelName;
        public string fictionNotice;
        public string warningTitle;
        public string warningLevel;
        public string headline;
        public string[] lines;
        public string ticker;

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
