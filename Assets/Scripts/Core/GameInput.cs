using UnityEngine;
using UnityEngine.InputSystem;

namespace AfetUyanisi.Core
{
    /// <summary>
    /// Oyunun bütün tuşları tek yerde (tuş haritası: docs/OYUN_AKISI.md §3).
    /// Proje sadece yeni Input System'i kullanır: Input.GetKeyDown gibi eski çağrılar bu projede HATA verir,
    /// tuş okumak için hep bu sınıfı kullan. "Pressed" = bu karede basıldı, "Held" = basılı tutuluyor.
    /// </summary>
    public static class GameInput
    {
        /// <summary>
        /// İzometrik yürüme: W/A/S/D veya ok tuşları, ekran yönünde 8 yön. Uzunluğu en fazla 1 (çaprazda normalize).
        /// </summary>
        public static Vector2 MoveVector
        {
            get
            {
                var v = new Vector2(MoveX, 0f);
                if (Held(Key.W) || Held(Key.UpArrow)) v.y += 1f;
                if (Held(Key.S) || Held(Key.DownArrow)) v.y -= 1f;
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        /// <summary>Yatay yürüme yönü: -1 sol, 0 dur, +1 sağ (A/D veya ←/→).</summary>
        public static float MoveX
        {
            get
            {
                float x = 0f;
                if (Held(Key.A) || Held(Key.LeftArrow)) x -= 1f;
                if (Held(Key.D) || Held(Key.RightArrow)) x += 1f;
                return x;
            }
        }

        /// <summary>Menüde bir önceki bulut (A / ←).</summary>
        public static bool LeftPressed => Pressed(Key.A) || Pressed(Key.LeftArrow);

        /// <summary>Menüde bir sonraki bulut (D / →).</summary>
        public static bool RightPressed => Pressed(Key.D) || Pressed(Key.RightArrow);

        /// <summary>Etkileşim: kalk, yüzünü yıka, eşyayı al, tabelayı incele (E).</summary>
        public static bool InteractPressed => Pressed(Key.E);

        /// <summary>Etkileşim tuşu basılı tutuluyor (E) — deprem modunda TUTUN adımı.</summary>
        public static bool InteractHeld => Held(Key.E);

        /// <summary>Başlat / bildirimi kapat: E, Space, Enter veya sol tık (ContinuePressed ile aynı).</summary>
        public static bool StartPressed => ContinuePressed;

        /// <summary>Deprem modu: hazırlık tamamken simülasyonu hızlandır (Space).</summary>
        public static bool AcceleratePressed => Pressed(Key.Space);

        /// <summary>Diyaloğu ilerlet: E, Space, Enter veya sol tık.</summary>
        public static bool ContinuePressed =>
            Pressed(Key.E) || Pressed(Key.Space) || EnterPressed || ClickPressed;

        /// <summary>Onayla / başlat: Enter veya Space.</summary>
        public static bool ConfirmPressed => EnterPressed || Pressed(Key.Space);

        /// <summary>Menü introsunu atla: Space veya Enter. Aynı karede oyunu başlatmamaya dikkat.</summary>
        public static bool SkipPressed => Pressed(Key.Space) || EnterPressed;

        /// <summary>Geri / ayarları kapat: Esc.</summary>
        public static bool BackPressed => Pressed(Key.Escape);

        public static bool ClickPressed
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasPressedThisFrame;
            }
        }

        /// <summary>Bu karede basılan seçim tuşu 1..5 (üst sıra veya numpad); basılmadıysa 0.</summary>
        public static int ChoiceKeyPressed
        {
            get
            {
                if (Pressed(Key.Digit1) || Pressed(Key.Numpad1)) return 1;
                if (Pressed(Key.Digit2) || Pressed(Key.Numpad2)) return 2;
                if (Pressed(Key.Digit3) || Pressed(Key.Numpad3)) return 3;
                if (Pressed(Key.Digit4) || Pressed(Key.Numpad4)) return 4;
                if (Pressed(Key.Digit5) || Pressed(Key.Numpad5)) return 5;
                return 0;
            }
        }

        // Deprem: Çök – Kapan – Tutun
        public static bool DropPressed => Pressed(Key.S);
        public static bool CoverPressed => Pressed(Key.A);
        public static bool HoldHeld => Held(Key.D);

        private static bool EnterPressed => Pressed(Key.Enter) || Pressed(Key.NumpadEnter);

        private static bool Pressed(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        private static bool Held(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].isPressed;
        }
    }
}
