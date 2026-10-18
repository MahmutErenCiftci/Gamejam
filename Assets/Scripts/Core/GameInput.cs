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

        /// <summary>Yeni korunma denge alıştırması: Space basılı; A/D düzeltmesi MoveX ile okunur.</summary>
        public static bool ProtectionHeld => Held(Key.Space);

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

        /// <summary>Tekrar dene: R (sonuç / başarısız ekranları).</summary>
        public static bool RetryPressed => Pressed(Key.R);

        /// <summary>Yalnız geliştirici kısayolları (F5..F12 vb.): Editor ve Development build dışında her zaman false.</summary>
        public static bool DevKeyPressed(Key key)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return Pressed(key);
#else
            return false;
#endif
        }

        public static bool ClickPressed
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasPressedThisFrame;
            }
        }

        /// <summary>Sağ fare tuşu bu karede basıldı (yakın plan panelini geri kapatma).</summary>
        public static bool RightClickPressed
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null && mouse.rightButton.wasPressedThisFrame;
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

        /// <summary>Şifre paneli: bu karede basılan rakam 0..9 (üst sıra veya numpad); basılmadıysa -1.</summary>
        public static int DigitPressed
        {
            get
            {
                if (Pressed(Key.Digit0) || Pressed(Key.Numpad0)) return 0;
                if (Pressed(Key.Digit1) || Pressed(Key.Numpad1)) return 1;
                if (Pressed(Key.Digit2) || Pressed(Key.Numpad2)) return 2;
                if (Pressed(Key.Digit3) || Pressed(Key.Numpad3)) return 3;
                if (Pressed(Key.Digit4) || Pressed(Key.Numpad4)) return 4;
                if (Pressed(Key.Digit5) || Pressed(Key.Numpad5)) return 5;
                if (Pressed(Key.Digit6) || Pressed(Key.Numpad6)) return 6;
                if (Pressed(Key.Digit7) || Pressed(Key.Numpad7)) return 7;
                if (Pressed(Key.Digit8) || Pressed(Key.Numpad8)) return 8;
                if (Pressed(Key.Digit9) || Pressed(Key.Numpad9)) return 9;
                return -1;
            }
        }

        /// <summary>Şifre paneli: son rakamı sil (Backspace veya Delete).</summary>
        public static bool DeletePressed => Pressed(Key.Backspace) || Pressed(Key.Delete);

        /// <summary>Sol fare tuşu basılı tutuluyor (ör. bahçe kapısının kolunu çevirme).</summary>
        public static bool ClickHeld
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.isPressed;
            }
        }

        /// <summary>Fare imlecinin ekran konumu (piksel, sol alt orijin); fare yoksa (0,0).</summary>
        public static Vector2 PointerPosition
        {
            get
            {
                var mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
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
