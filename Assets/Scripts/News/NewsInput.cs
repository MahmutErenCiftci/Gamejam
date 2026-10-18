using UnityEngine.InputSystem;

namespace AfetUyanisi.News
{
    // Haber prefablarının ortak "devam" girdisi: E / Space / Enter / sol tık.
    public static class NewsInput
    {
        public static bool ContinuePressed()
        {
            if (AfetUyanisi.Core.GameInput.GameplayLocked) return false; // duraklatma menüsü açık
            var kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                return true;
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }
    }
}
