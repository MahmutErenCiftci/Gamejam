using System;
using UnityEngine;

namespace AfetUyanisi.News
{
    // Assets/Data/News/*_rumors.json: söylenti kartları + resmî duyuru kartı.
    [Serializable]
    public class RumorCardData
    {
        public string id;
        public bool official;
        public string sourceLabel;
        public string text;
        public string feedback;
    }

    [Serializable]
    public class RumorCardsData
    {
        public string id;
        public string prompt;
        public string fictionNotice;
        public RumorCardData[] cards;

        public static RumorCardsData FromJson(TextAsset json)
        {
            if (json == null)
            {
                Debug.LogError("[News] Söylenti kartı JSON'u atanmamış.");
                return null;
            }

            var data = JsonUtility.FromJson<RumorCardsData>(json.text);
            if (data == null || data.cards == null || data.cards.Length == 0)
            {
                Debug.LogError($"[News] '{json.name}' okunamadı veya 'cards' boş.");
                return null;
            }
            if (Array.FindIndex(data.cards, c => c.official) < 0)
                Debug.LogError($"[News] '{json.name}' içinde resmî kart yok; bölüm ilerleyemez.");
            return data;
        }
    }
}
