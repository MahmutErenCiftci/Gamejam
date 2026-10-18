using System;
using UnityEngine;

namespace AfetUyanisi.Dialogue
{
    // Assets/Data/Story/*.json veri modeli (data/*_story_example.json yapısı + time_limit).
    [Serializable]
    public class StoryLine
    {
        public string speaker;
        public string text;
    }

    [Serializable]
    public class StoryScene
    {
        public string id;
        public string title;
        public StoryLine[] lines;
    }

    [Serializable]
    public class StoryChoice
    {
        public string id;
        public string text;
        public bool correct;
        public string feedback;
    }

    [Serializable]
    public class StoryDecision
    {
        public string id;
        public string prompt;
        public float time_limit;
        public string timeout_feedback;
        public StoryChoice[] choices;
    }

    [Serializable]
    public class StoryData
    {
        public string chapter_id;
        public string version;
        public StoryScene[] scenes;
        public StoryDecision[] decisions;

        public static StoryData FromJson(TextAsset json)
        {
            if (json == null)
            {
                Debug.LogError("[Dialogue] Hikâye JSON'u atanmamış.");
                return null;
            }

            var data = JsonUtility.FromJson<StoryData>(json.text);
            if (data == null || data.scenes == null)
            {
                Debug.LogError($"[Dialogue] '{json.name}' okunamadı.");
                return null;
            }
            return data;
        }

        public StoryScene FindScene(string id)
        {
            var scene = Array.Find(scenes, s => s.id == id);
            if (scene == null) Debug.LogError($"[Dialogue] '{chapter_id}' içinde '{id}' sahnesi yok.");
            return scene;
        }

        public StoryDecision FindDecision(string id)
        {
            var decision = decisions == null ? null : Array.Find(decisions, d => d.id == id);
            if (decision == null) Debug.LogError($"[Dialogue] '{chapter_id}' içinde '{id}' kararı yok.");
            return decision;
        }
    }
}
