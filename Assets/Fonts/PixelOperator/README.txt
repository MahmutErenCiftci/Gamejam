Pixel Operator TR Bold
=====================

Kaynak: "Pixel Operator" — Jayvee Enaguas (HarvettFox96), CC0 1.0 (LICENSE-CC0.txt).
İndirilen dosya: PixelOperator-Bold.ttf (2018-10-04 sürümü; DaFont ve fontlibrary.org'daki paket aynı).

Neden değiştirildi: Pixel Operator'ın yayımlanmış hiçbir sürümünde Ğ ğ İ Ş ş yok. Oyunda bu harfler
sessizce başka fonta düşmesin diye fontun KENDİ parçalarıyla eklendi (yapay kalınlaştırma yok):
  Ş ş = S / s + fontun kendi cedilla glifi, Ç / ç'deki konumla birebir
  Ğ ğ = G / g + fontun kendi breve glifi, Ö / ö'deki gibi bir boş piksel satırla
  İ   = I + Ö'deki noktayla aynı 2x1 piksel nokta
Arayüzde kullanılan ama fontta olmayan simgeler de aynı 2 piksel çizgi kalınlığıyla piksel piksel çizildi:
  → ← ▶ ▼ ✓ ✗
Başka hiçbir glif değişmedi. Ad tablosu "Pixel Operator TR" olarak değiştirildi (orijinalle karışmasın).

Yeniden üretmek için: Tools~/make_tr.py (fontTools 4.54.1)
  python make_tr.py <fontTools yolu> PixelOperator-Bold.ttf PixelOperatorTR-Bold.ttf

Piksel ızgarası: 1 piksel = 100 birim, em = 16 piksel. En net görünüm: ekrandaki yazı boyu 16'nın katı.

Pixel Operator TR Heavy (diyalog yazıları)
------------------------------------------
Pixel Operator'ın yayımlanmış en kalın kesimi Bold'dur (2 px çizgi). Diyalog yazıları daha kalın istendiği için
Bold'dan, Regular -> Bold geçişindeki yöntemle bir kesim daha türetildi: her dikey çizgi sağa 1 font pikseli
kalınlaşır (2 px -> 3 px), glif genişliği 1 piksel artar, harf arası korunur. Kalınlaşma 1 px'lik bir boşluğu
kapatacaksa (m, w, k, r, G, M, N, R, 0, 1, 4, Ğ ... 52 glif) o sütun kopyalanır: çizgi yine 3 px olur, boşluk
kapanmaz, glif bir piksel daha genişler. Sonuç tam piksel ızgarasındadır (gölge/kontur ile yapay kalınlaştırma yok).
Satır genişliği Bold'dan yaklaşık %12 fazladır.

Yeniden üretmek için: Tools~/make_heavy.py
  python make_heavy.py <fontTools yolu> PixelOperatorTR-Bold.ttf PixelOperatorTR-Heavy.ttf
