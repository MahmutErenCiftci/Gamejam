using System;
using UnityEngine;

namespace AfetUyanisi.News
{
    // Assets/Data/News/*_alert.json içindeki kurgu telefon bildiriminin veri modeli.
    [Serializable]
    public class AlertNotificationData
    {
        public string id;
        public string appName;
        public string time;
        public string title;
        public string body;
        public string fictionNotice;

        public static AlertNotificationData FromJson(TextAsset json)
        {
            if (json == null)
            {
                Debug.LogError("[News] Bildirim JSON'u atanmamış.");
                return null;
            }

            var data = JsonUtility.FromJson<AlertNotificationData>(json.text);
            if (data == null || string.IsNullOrEmpty(data.title))
            {
                Debug.LogError($"[News] '{json.name}' okunamadı veya 'title' boş.");
                return null;
            }
            return data;
        }
    }
}
