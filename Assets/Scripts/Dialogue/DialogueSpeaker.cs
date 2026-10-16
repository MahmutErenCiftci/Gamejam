using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Dialogue
{
    // Sahnedeki karaktere eklenir; JSON'daki "speaker" adı buradaki adlardan biriyle eşleşirse
    // o satırın balonu bu karakterin başının üstünde açılır. Örn. Çocuk: { "Çocuk", "İç ses" }.
    public class DialogueSpeaker : MonoBehaviour
    {
        [SerializeField] string[] speakerNames = { "Çocuk" };
        [Tooltip("Balonun bağlanacağı nokta (karakterin pivotuna göre, dünya birimi).")]
        [SerializeField] Vector3 headOffset = new Vector3(0f, 1.6f, 0f);

        static readonly List<DialogueSpeaker> active = new List<DialogueSpeaker>();

        public Vector3 HeadPosition => transform.position + headOffset;

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        public static DialogueSpeaker Find(string speakerName)
        {
            if (string.IsNullOrEmpty(speakerName)) return null;
            foreach (var s in active)
                foreach (var n in s.speakerNames)
                    if (n == speakerName) return s;
            return null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(HeadPosition, 0.1f);
        }
    }
}
