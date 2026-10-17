using System.Collections.Generic;

namespace AfetUyanisi.Chapters.Earthquake
{
    /// <summary>
    /// Afet çantasının içeriği: sabit sayıda ana yuva (varsayılan 9) + isteğe bağlı ön cep (tek rahatlatıcı eşya).
    /// Aynı eşya iki kez girmez; yeni eşya ilk boş yuvaya girer, çıkarılan eşyanın yuvası boşalır (kaydırma yok).
    /// Saf C# sınıfı: mini oyun kapanıp açılınca (Esc) içerik korunur.
    /// </summary>
    public class EmergencyBagInventory
    {
        public enum AddResult { Slot, Pocket, Duplicate, Full }

        /// <summary>Ön cep yuvasının indeksi.</summary>
        public const int PocketIndex = -1;
        /// <summary>"Yok" indeksi.</summary>
        public const int NoSlot = -2;

        private readonly EmergencyItemData[] slots;

        public EmergencyBagInventory(int capacity, bool pocketEnabled)
        {
            slots = new EmergencyItemData[capacity < 1 ? 1 : capacity];
            PocketEnabled = pocketEnabled;
        }

        public int Capacity => slots.Length;
        public bool PocketEnabled { get; }
        public EmergencyItemData Pocket { get; private set; }

        /// <summary>Dolu ana yuva sayısı (ön cep hariç).</summary>
        public int FilledCount
        {
            get
            {
                int n = 0;
                foreach (var s in slots) if (s != null) n++;
                return n;
            }
        }

        public bool IsFull => FilledCount >= Capacity;

        /// <summary>Ana yuva (index &gt;= 0) ya da ön cep (PocketIndex).</summary>
        public EmergencyItemData Get(int index)
        {
            if (index == PocketIndex) return Pocket;
            return index >= 0 && index < slots.Length ? slots[index] : null;
        }

        public bool Contains(EmergencyItemData item) => item != null && IndexOf(item) != NoSlot;

        /// <summary>Eşyanın yuvası (ön cep: PocketIndex, yoksa NoSlot).</summary>
        public int IndexOf(EmergencyItemData item)
        {
            if (item == null) return NoSlot;
            if (Pocket == item) return PocketIndex;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == item) return i;
            return NoSlot;
        }

        /// <summary>
        /// Eşyayı koyar. Rahatlatıcı eşya ön cep boşsa oraya girer; diğerleri ilk boş ana yuvaya.
        /// index: yerleştiği yuva (Pocket → PocketIndex), olmadıysa NoSlot.
        /// </summary>
        public AddResult TryAdd(EmergencyItemData item, out int index)
        {
            index = NoSlot;
            if (item == null) return AddResult.Full;
            if (Contains(item)) return AddResult.Duplicate;
            if (PocketEnabled && Pocket == null && item.Category == EmergencyItemData.ItemCategory.Comfort)
            {
                Pocket = item;
                index = PocketIndex;
                return AddResult.Pocket;
            }
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) continue;
                slots[i] = item;
                index = i;
                return AddResult.Slot;
            }
            return AddResult.Full;
        }

        /// <summary>Yuvayı boşaltır, çıkan eşyayı döner (boşsa null).</summary>
        public EmergencyItemData RemoveAt(int index)
        {
            var item = Get(index);
            if (item == null) return null;
            if (index == PocketIndex) Pocket = null;
            else slots[index] = null;
            return item;
        }

        /// <summary>Listedeki gerekli eşyalardan kaçı çantada.</summary>
        public int CountPacked(IList<EmergencyItemData> required)
        {
            int n = 0;
            if (required == null) return 0;
            foreach (var r in required) if (Contains(r)) n++;
            return n;
        }

        public bool HasAll(IList<EmergencyItemData> required) =>
            required != null && required.Count > 0 && CountPacked(required) == required.Count;

        /// <summary>Ana yuvalardaki ilk gerekli-olmayan eşyanın yuvası (yoksa NoSlot).</summary>
        public int FirstNonRequiredSlot()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && !slots[i].IsRequired) return i;
            return NoSlot;
        }

        public void Clear()
        {
            for (int i = 0; i < slots.Length; i++) slots[i] = null;
            Pocket = null;
        }
    }
}
