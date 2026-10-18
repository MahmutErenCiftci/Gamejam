# Akşam Sel — Zikzak rota (lider promptu)

Görsel referans: `visuals/concept/sel_aksam_zigzag_ref.jpeg` (olay sırası + atmosfer; düz koridorları kopyalama).
Görev dağılımı: [`docs/SEL_ZIGZAG_GOREVLER.md`](SEL_ZIGZAG_GOREVLER.md).

## Lider kararları (uygulama)
- Bölüm akışı: Sokakta başlangıç (mevcut uyarı bildirimi = Eren'in AlertNotification'ı, başta bir kez kalsın) → dönüşlü sokaklar → TEK kavşak (KISA YOL / UZUN YOL tabelaları) → kısa yol: köşe dönünce yıkıntı/bariyerle kapalı, oyuncu yürüyerek geri döner → uzun yol: ≥3 dönüş → köşeden sonra AĞAÇ DÜŞÜŞÜ (tek sefer, güvenli mesafe) → hafif KIRIK DAL'ı E'ye ART ARDA basarak (10–15 basış, basılı tutmak ilerletmez) kenara itme → "Yol açıldı." → en az bir köşe daha → DEVAM NOKTASI = bölüm sonu (mevcut FloodEvening sonuç/bitiş akışı).
- ROTADAN ÇIKARILAN (kod/prefab projede KALIR, sahneye yerleştirilmez veya kapalı durur — ekip sonra açabilir): şifre paneli (CodeDoor), bahçe kapısı mini oyunu (GardenGate), ipucu levhaları (Clues), söylenti kartı (RumorSpot), rüzgâr geçidi (WindPass), sayaç/süre baskısı (yeni sayaç YOK; mevcut süre varsa kapat ya da görünmez yap — zaman kaybı = gerçek yürüme süresi).
- Cenk görünümü: deprem modundaki Cenk aynen (yağmurluk/çanta YOK).
- Güvenlik: Cenk asla sel suyunda yürümez; yürünebilir rota kuru/yükseltilmiş, su sadece kenarlarda tehlike görseli. Büyük gövde dekor; itilen şey küçük hafif kırık dal; elektrik hattı/akıntı yok.

---

# LİDERİN PROMPTU (birebir)

GÖREV: MEVCUT OYUNA ZİKZAK SOKAKLARDAN OLUŞAN SEL BÖLÜMÜ EKLE

Mevcut Unity 2D oyunumuz üzerinde çalışıyorsun. Oyunu sıfırdan oluşturma. Mevcut karakter, hareket, kamera, input ve etkileşim sistemlerini kullanarak aşağıdaki Sel bölümünü ekle.

Ana karakter CENK. Mevcut karakter sprite'ını ve animasyonlarını koru.

Ekli görsel olay sırası ve atmosfer referansıdır. Görseldeki düz koridorları birebir kopyalama. Bu bölümde yollar; binalar, bahçe duvarları ve mahalle köşeleri arasında dönüşler yaparak ilerlemeli.

1. TEMEL AKIŞ
Mevcut Deprem → Sel sırasını koru.
Sel bölümü: Sokakta başlangıç → dönüşlü mahalle sokaklarında ilerleme → iki seçenekli tek kavşak
   ├─ kısa yol → birkaç dönüş → kapalı yol → kavşağa geri dönüş
   └─ uzun yol → zikzak güzergâh → ağaç düşüşü
→ E'ye art arda basarak hafif engeli kenara itme → açılan yoldan devam.
Sel'e girildiğinde yeniden uyanma veya sabah rutini oynatma. Geçiş zaten uygulanmışsa yeniden kurma.
Bu görev için şifre paneli, kapı mini oyunu, envanter, yeni sayaç veya yeni bölüm ekleme.

2. HARİTA TASARIMI: DÜZ KORİDOR OLMAYACAK
Başlangıçtan sona uzanan tek düz yol yapma. Yürünebilir güzergâhın kendisi yön değiştirsin. Düz yolun kenarına dekor yerleştirmek yeterli değildir.
Zikzakları mahalle dokusuyla oluştur: bina köşeleri; bahçe ve avlu duvarları; dar sokak bağlantıları; birbirine açılan küçük sokaklar; park etmiş araçların belirlediği güvenli geçitler; köşe başındaki küçük meydancıklar.
Cenk bir süre ilerlesin, köşeyi dönsün, başka yönde yürüsün ve sonraki sokağa geçsin. Sürekli aynı yöne basarak bölümü tamamlamak mümkün olmasın.
Mevcut kamera açısına uygun yönler kullan. Oyun izometrikse yollar izometrik eksenlerle uyumlu olsun; karakter hareket sistemini bunun için değiştirme.
Dönüşler doğal ve okunur olsun: çok kısa aralıklarla sağ-sol yaptırma; labirent/yol bulma bulmacası yok; keskin köşelerde karakter takılmasın; yol genişliği karakter collider'ına rahat pay bıraksın; köşeleri çapraz keserek duvarlardan geçmek mümkün olmasın; yön animasyonları yürüyüş yönüne uysun.
Haritayı mevcut sahne yapısında çöz. Her sokak veya dönüş için ayrı Unity sahnesi oluşturma.

3. ÖNERİLEN YERLEŞİM (bağlantı mantığı, ölçek değil)
BAŞLANGIÇ → Sokak A → Köşe 1 → Sokak B → Köşe 2 → KAVŞAK
  KISA YOL: dönüş → KAPALI YOL → aynı yoldan geri
  UZUN YOL: dönüş → sokak → dönüş → AĞAÇ OLAYI → ENGELİ İTME → dönüş → DEVAM NOKTASI
Başlangıç ile kavşak arasında en az iki belirgin yön değişimi. Uzun yol kavşaktan sonra en az üç belirgin yön değişimi (farklı sokak parçaları, yapay kıvrım değil). Kısa yol daha az mesafeli görünsün, kapalı nokta kavşağın hemen yanında görünmesin; oyuncu bir köşeyi döndükten sonra engelle karşılaşsın. Gereksiz büyük harita üretme.

4. OYUNCUYU HARİTAYA YEDİREREK YÖNLENDİR
Çevresel ipuçları: köşeden görünen sokak lambası; duvarların oluşturduğu geçiş; yol döşemesinin devamı; kavşakta okunur tabelalar; açık ve kapalı geçitlerin belirgin ayrımı. Gerekirse küçük görev/ipucu metni; ekranı sürekli büyük oklarla doldurma.
Binalar ve ağaçlar Cenk'i uzun süre gizlemesin (mevcut sorting/örtüşme çözümünü kullan).
Kamera: mevcut takip davranışını korusun; dönüşlerde yürünecek alanı göstersin; kavşakta iki kolun girişi anlaşılsın; harita dışı boşluk göstermesin; her dönüşte zorunlu kamera kesmesi/kontrol kilidi olmasın.

5. SOKAKTA BAŞLANGIÇ
Yağmurlu mahallede güvenli yürüyüş noktası. Atmosfer: ıslak taş zemin, yağmur, sıcak sokak lambaları, koyu mavi akşam, çevrede artan su baskını belirtileri. Yürünebilir rota sel akıntısından ayrılmış, kuru veya yükseltilmiş; Cenk'i sel suyunda yürütme; su görseli tehlikeli çevre bölgelerinde.
Görev metni: "Güvenli güzergâhı takip et."

6. İKİ SEÇENEKLİ TEK KAVŞAK
"KISA YOL" / "UZUN YOL" tabelaları. Oyuncu karakteri ilgili kola yürüterek seçer; tam ekran menü yok. İki rota fiziksel olarak farklı yerlere bağlansın. Uzun yoldan doğrudan devam edilebilsin; kısa yolu denemek zorunlu değil.

7. KISA YOL: KAPALI GEÇİT VE ZAMAN KAYBI
Kısa ara sokak → en az bir köşe → bariyer/yıkıntıyla kapalı; oyuncu tehlikeli bölgeye girmeden collider ile durdurulur. Metin: "Yol kapanmış. Kavşağa dönüp diğer yolu kullanmalıyım." Aynı kıvrımlı yoldan yürüyerek geri döner: ışınlama yok, otomatik geri taşıma yok, geri dönüşü kapatan engel yok, kamera takip eder, açıklama tekrar tekrar tetiklenmez, yeniden denemek akışı bozmaz. Zaman kaybı = gerçek oynanış süresi; yeni sayaç/yapay ceza yok.

8. UZUN YOL: ZİKZAK
Duvar boyunca yürüme, köşeden başka yöne dönme, küçük açık alandan geçme, yeni ara sokağa bağlanma, ağaç noktasına ulaşma. Uzunluk yürüme hızını düşürerek değil gerçek mesafe ve dönüşlerle. Kavşağa istemsiz döngü veya dekor boşluğu yok.

9. AĞAÇ OLAYI BİR DÖNÜŞTEN SONRA
Ağaç kavşaktan görünmesin. Köşeyi dönünce olay alanı. Tek seferlik: Cenk güvenli mesafede durur → hareket kısa süre kilitlenir → kırılma sesi (varsa) → ağaç kısa animasyonla ilerideki yola düşer → hafif yaprak/toz efekti → yolun kapandığı görülür → kontrol geri verilir. Ağaç Cenk'in üzerine düşmez, düşüş alanına giriş engellenir. Gerçekçi fizik yok. Collider zamanlaması görselle tutarlı. Geri dönünce tekrar çalışmaz.
Güvenli uyarlama: büyük gövde çevresel görsel; yürünebilir geçidi kapatan ve itilen nesne küçük, hafif kırık dal parçası. Etkileşim kuru ve korunaklı noktada; elektrik hattı, akıntı veya düşmeye devam eden ağaç altında itme yok.

10. E'YE ART ARDA BASARAK ENGELİ İTME
İpucu: "[E] Art arda bas — Engeli kenara it". Her ayrı E basışı çubuğu artırır; basılı tutmak ilerletmez; ~10–15 basış (Inspector); kare hızından bağımsız; Cenk doğru yöne döner; itme animasyonu varsa kullan, yoksa sade poz/hareket + raporla; her basışta küçük geri bildirim; çubuk okunur ve ekranda. Uzakta basılan E ilerletmez; diyalog/UI input'u tüketiyorsa çalışmaz; aynı basış birden fazla etkileşimi tetiklemez; oyuncu ayrılabilir; uzaklaşıp dönünce ilerleme korunur; karakter engelin içinden geçemez; hareket kilidi etkileşim dışında açık kalmaz; uygunsa eşdeğer fare butonu.

11. YOLUN AÇILMASI
Tamamlanınca: yeni basışları durdur; dalı kısa animasyonla yol kenarına taşı; collider görselle birlikte; rahat geçiş genişliği; ipucu ve çubuğu kapat; kalıcı tamamlandı; "Yol açıldı." kısa mesaj. Dal sonraki dönüşün girişine taşınmasın. Oyuncu açılan yoldan en az bir köşe daha dönerek devam noktasına ulaşsın; engel temizlenir temizlenmez bölümü otomatik bitirme. Mevcut Sel devam akışına bağla.

12. TEKNİK SINIRLAR
Mevcut Sel controller'ı ve etkileşim altyapısı; ikinci genel yönetici yok; dar sorumluluklu ek bileşenler; GameInput/yeni Input System; Input.GetKeyDown yok; Shared kontratlar, namespace'ler, GameSceneRouter izinsiz değişmez; mevcut Tilemap/sahne kurulumu; prosedürel harita/pathfinding yok; sahiplik kuralları; yeni paket/lisansı belirsiz asset yok; yeniden başlatmada karakter, ağaç, dal, çarpışmalar, etkileşim ilerlemesi sıfırlanır; sahne çıkışında coroutine, event ve input kilitleri temizlenir.

13. UYGULAMA SIRASI
1 dönüşlü yerleşim + çarpışmalar → 2 kavşak, kısa yol, geri dönüş, uzun yol → 3 kamera okunabilirliği → 4 tek seferlik ağaç → 5 E ile engel → 6 açılan yoldan dönerek devam. Sonra çevre, en son yağmur/ses/efekt.

14. KABUL TESTLERİ
Sokakta başlıyor; tek yönde yürüyerek bitmiyor; dönüşler gerçek güzergâhı değiştiriyor; başlangıç–kavşak ≥2 dönüş; uzun yol ≥3 dönüş; köşelerde takılma/çapraz geçiş yok; kamera her dönüşte yolu gösteriyor; Cenk objelerin arkasında kaybolmuyor; iki yolun girişi ve tabelaları anlaşılıyor; kısa yol köşe sonrası kapanıyor; aynı rotadan kavşağa dönülebiliyor; doğrudan uzun yol mümkün; ağaç güvenli mesafede ve bir kez; düşüş bitince kontrol geri geliyor; E basılı tutmak ilerletmiyor; ayrı basışlar ilerletiyor; menzil dışında ilerleme yok; uzaklaşıp dönmek akışı bozmuyor; engel temizlenince geçiliyor; dal sonraki dönüşü kapatmıyor; açılan yoldan sonraki köşeye yürüyerek devam; yeniden başlatmada sıfırlanıyor; Deprem → Sel geçişi ve menü dönüşü bozulmuyor; Türkçe metinler okunuyor; Console hatası/eksik referans yok.

15. TESLİM
Rapor: değiştirilen dosyalar / ne çalışır / nasıl test edilir / test sonucu / açık blokaj / sonraki sahibine teslim. Unity'de doğrulanmayana "test edildi" deme; UNTESTED + neden.

BİTİŞ ÖLÇÜTÜ: Cenk mahalleye doğal yerleştirilmiş zikzak sokaklarda ilerliyor; kavşakta kısa yolu deneyip geri dönebiliyor veya doğrudan uzun yolu seçebiliyor. Uzun yolda köşeden sonra ağaç olayı. Cenk E'ye art arda basarak hafif engeli kenara alıyor, açılan geçitten yürüyüp sonraki köşeyi dönerek devam ediyor.
