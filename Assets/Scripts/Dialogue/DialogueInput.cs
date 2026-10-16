using UnityEngine.InputSystem;

namespace AfetUyanisi.Dialogue
{
    // Diyalog girdileri: devam = E / Space / Enter / sol tık, seçenek = 1..9 (numpad dahil).
    public static class DialogueInput
    {
        public static bool ContinuePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                return true;
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        // Basılan seçenek numarasının 0 tabanlı indeksi, yoksa -1.
        public static int ChoicePressed(int count)
        {
            var kb = Keyboard.current;
            if (kb == null) return -1;
            for (int i = 0; i < count && i < 9; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
                    return i;
            return -1;
        }
    }
}
