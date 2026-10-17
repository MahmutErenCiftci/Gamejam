using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfetUyanisi.Shared
{
    public enum ObjectiveKind { Main, Bonus, Keep, Counter, Lesson }
    public enum ObjectiveState { Todo, Active, Done, Warned, Failed }

    /// <summary>Sayaç satırındaki tek kutucuğun durumu (ObjectiveRowState.pips).</summary>
    public static class ObjectivePip
    {
        public const int Empty = 0, Filled = 1, Good = 2, Bad = 3, Dropped = 4;
    }

    [Serializable]
    public sealed class ObjectiveRowState
    {
        public string id;
        public string text;
        public ObjectiveKind kind;
        public ObjectiveState state;
        public int count;
        public int max;
        /// <summary>Sayaçta tamamlanma eşiği (ör. 7 tehlikeden 4'ü).</summary>
        public int target;
        /// <summary>Bu görevde kaç kez uyarı alındı ("1 UYARI").</summary>
        public int warnings;
        /// <summary>İsteğe bağlı kutucuk renkleri (ObjectivePip); null ise count kadar dolu.</summary>
        public int[] pips;
        /// <summary>Sayaç artınca uçan yazının birimi ("+1 TEHLİKE"); boşsa sadece "+1".</summary>
        public string unit;

        public ObjectiveRowState Copy()
        {
            var copy = (ObjectiveRowState)MemberwiseClone();
            if (pips != null) copy.pips = (int[])pips.Clone();
            return copy;
        }
    }

    /// <summary>Pasif hedef görünümü. Girdi okumaz, sahne veya oyun akışını yönetmez.</summary>
    public abstract class ObjectiveViewBase : MonoBehaviour
    {
        public abstract void SetHeader(string title, float progress01);
        public abstract void SetRows(IReadOnlyList<ObjectiveRowState> rows);
        public abstract void PlayComplete(string id);
        public abstract void PlayWarn(string id);
        public abstract void ShowBanner(string text);
        public abstract void SetVisible(bool visible);

        /// <summary>İlerleme çubuğundaki bölme sayısı (bölümdeki ana görev sayısı). İsteğe bağlı.</summary>
        public virtual void SetSegments(int count) { }
    }
}
