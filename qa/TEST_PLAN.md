# QA / Definition of Done / Final Build Test Planı

**QA sahibi:** K4 · **Yürütme:** T+10, 18, 28, 36, 40, 44, final teslim

## Test kaydı nasıl tutulur?

Her test için `PASS / FAIL / UNTESTED`, commit hash, build yolu, tester adı ve tarih-saat yazılır. Gerçekten Unity/Windows build açılmadıysa `PASS` yazılmaz.

| Test ID | Adımlar | Beklenen | Durum |
|---|---|---|---|
| M01 | Oyunu aç | Siyah → 07:00 → oda → bulutlar → ana menü | [ ] |
| M02 | Intro sırasında Space | Animasyon atlanır; aynı tuş yanlışlıkla oyun başlatmaz | [ ] |
| M03 | Deprem bulutunu seç, Yeni Oyun | Deprem sahnesi açılır | [ ] |
| M04 | Sel bulutunu seç, Yeni Oyun | Sel sahnesi açılır | [ ] |
| M05 | Yangın/Heyelan balonuna tıkla | “Yakında”, boş sahne yok | [ ] |
| M06 | Ayarlar → ses/fullscreen → geri | Kontroller işler, ayar kapatılabilir | [ ] |
| M07 | Çıkış | Build'de uygulama kapanır | [ ] |
| F01 | Sel başla → E ile kalk | Karakter ve state ilerler | [ ] |
| F02 | Rutinde lavabo etkileşimi | Sonraki sahneye takılmadan geçer | [ ] |
| F03 | TV ve anne konuşmalarını oku | Türkçe karakter, doğru uyarı, metin taşması yok | [ ] |
| F04 | Karar 1'in 3 seçeneğini ayrı ayrı dene | Her seçim ilgili kısa feedback verir | [ ] |
| F05 | Şiddetli yağmur → kapalı yol | Yol güvenli erişim dışında, tehlikeli geçiş ödüllendirilmez | [ ] |
| F06 | 3 arkadaşın iddialarını oku | Söylentiler kaynak belirsiz diye sunulur | [ ] |
| F07 | Karar 2'nin 3 seçeneğini ayrı ayrı dene | Güvenli karar tanımlanır, yanlışlar açıklanır | [ ] |
| F08 | Sel sonucu → menü → Sel tekrar | Reset düzgün, soft lock yok | [ ] |
| E01 | Deprem bölümünü menüden başlat | Ev ve yetişkin, sağlam masa görünüyor | [ ] |
| E02 | 1 tuşuna 5 saniye içinde bas | Doğru feedback + güvenli poz | [ ] |
| E03 | 2,3,4,5 tuşlarını ayrı dene | Tehlikeli seçenek yanlış olarak açıklanır | [ ] |
| E04 | Mouse ile tüm 5 seçeneği dene | Mouse seçimleri doğru ID'ye gider | [ ] |
| E05 | 5 saniye hiçbir şey seçme | Timeout feedback, donma yok | [ ] |
| E06 | Deprem sonuç → tekrar/menü | Timer ve UI yeniden başlar, menüye döner | [ ] |
| I01 | Sel sonucu → menü → Deprem sonucu → menü | Scene routing ve geri dönüş sorunsuz | [ ] |
| I02 | Unity Console / build log | Yeni compile, Missing Script, Missing Reference hatası yok | [ ] |
| I03 | 1366×768 + 1920×1080 ekranda çalıştır | 16:9 UI sığar, butonlar erişilebilir | [ ] |
| I04 | Farklı Windows bilgisayarda çalıştır | EXE açılır ve iki bölüm biter (mümkünse) | [ ] |
| A01 | Sel ve deprem uyarılarını eğitim doğruluğu açısından oku | Yanlış/tehlikeli davranış teşvik edilmiyor | [ ] |
| A02 | Türkçe karakterleri tara | Ç, Ğ, İ, Ö, Ş, Ü, ı doğru | [ ] |
| A03 | Flaş/hareket ve ses yoğunluğu | Oyuncuyu rahatsız eden sert flaş yok | [ ] |

## Hata önem seviyeleri

- **P0 Blocker:** oyun açılmıyor, bölüm tamamlanmıyor, yanlış afet yükleniyor, gerçek tehlikeli davranış “doğru” diye anlatılıyor.
- **P1 Critical:** bir karar çalışmıyor, seçim inputu yanlış, geri dönüş bozuk, Türkçe ana cümle okunamıyor.
- **P2 Minor:** küçük sprite konumu, kozmetik sarsıntı, ses seviyesindeki küçük fark.

T+36 sonrası P0/P1 öncelikli; P2 değişiklikleri build'i kırma riski yaratıyorsa **dokunma**.

## Son Windows build imza tablosu

| Alan | Kimin onayı | Durum |
|---|---|---|
| Menü ve geçişler | K1 | [ ] |
| Sel baştan sona | K2 | [ ] |
| Deprem baştan sona | K3 | [ ] |
| Eğitim/diyalog doğruluğu | K4 | [ ] |
| Windows build / bağımsız test | K1+K4 | [ ] |
| Gönderim paketi ve demo | PM | [ ] |

**Son test yolculuğu (zorunlu):** `Oyun aç → Intro → SEL → 2 karar → sonuç → menü → DEPREM → 5 saniye karar → sonuç → menü → kilitli 2 afet → ayarlar → çıkış`.
