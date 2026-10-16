using System;

namespace AfetUyanisi.Shared
{
    // Assets/Data/Story/<bölüm>.json dosyalarının yapısı (JsonUtility ile okunur).
    // Alan adları JSON anahtarlarıyla birebir aynı olmalı. ID listesi: docs/OYUN_AKISI.md §9.

    [Serializable]
    public class StoryData
    {
        public string chapter_id;
        public string version;
        public StoryScene[] scenes;
        public DecisionData[] decisions;
        public ResultData result;

        public StoryScene FindScene(string id) =>
            scenes == null ? null : Array.Find(scenes, s => s.id == id);

        public DecisionData FindDecision(string id) =>
            decisions == null ? null : Array.Find(decisions, d => d.id == id);
    }

    [Serializable]
    public class StoryScene
    {
        public string id;              // ör. "F04_MOTHER"
        public string title;
        public DialogueLine[] lines;
    }

    [Serializable]
    public class DialogueLine
    {
        public string speaker;         // ekranda görünen ad: "Çocuk", "Anne", "Arkadaş 1"...
        public string text;
    }

    [Serializable]
    public class DecisionData
    {
        public string id;              // ör. "F_D2"
        public string prompt;
        public float time_limit;       // saniye; 0 = süresiz (E_D1 = 5)
        public int required_correct;   // kaç doğru bulununca biter; 0 veya boş = 1 (E_D2 = 2)
        public string timeout_feedback;
        public ChoiceData[] choices;

        public int GetRequiredCorrect() => required_correct > 0 ? required_correct : 1;
    }

    [Serializable]
    public class ChoiceData
    {
        public string id;              // ör. "F_D2_C"
        public int key;                // klavye kısayolu 1..5
        public string text;
        public bool correct;
        public string feedback;
        public string wrong_example;   // yanlışsa sonuç ekranındaki "yanlış davranış" maddesi
    }

    [Serializable]
    public class ResultData
    {
        public string title_success;
        public string title_with_mistakes;
        public string[] learned;
        public string note;
    }
}
