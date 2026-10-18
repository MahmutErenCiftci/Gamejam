using System.Collections.Generic;
using System.Text;

namespace AfetUyanisi.Dialogue
{
    // Daktilo efekti için zengin metin (<b>, <i>, <color=..>, <size=..>) yardımcısı.
    // Etiketler sayılmaz ve yarım kalmaz: kesilen yerde açık etiketler kapatılır.
    public static class RichTextTyper
    {
        public static int VisibleLength(string rich)
        {
            int count = 0;
            for (int i = 0; i < rich.Length; i++)
            {
                if (rich[i] == '<')
                {
                    int end = rich.IndexOf('>', i);
                    if (end > i) { i = end; continue; }
                }
                count++;
            }
            return count;
        }

        public static string Substring(string rich, int visibleCount)
        {
            var sb = new StringBuilder(rich.Length + 16);
            var open = new Stack<string>();
            int shown = 0;
            for (int i = 0; i < rich.Length && shown < visibleCount; i++)
            {
                if (rich[i] == '<')
                {
                    int end = rich.IndexOf('>', i);
                    if (end > i)
                    {
                        string tag = rich.Substring(i + 1, end - i - 1);
                        if (tag.StartsWith("/")) { if (open.Count > 0) open.Pop(); }
                        else open.Push(TagName(tag));
                        sb.Append(rich, i, end - i + 1);
                        i = end;
                        continue;
                    }
                }
                sb.Append(rich[i]);
                shown++;
            }
            while (open.Count > 0) sb.Append("</").Append(open.Pop()).Append('>');
            return sb.ToString();
        }

        static string TagName(string tag)
        {
            int eq = tag.IndexOf('=');
            return eq < 0 ? tag : tag.Substring(0, eq);
        }
    }
}
