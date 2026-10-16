# Risk Defteri ve Kapsam Kesme Listesi

**Ana risk:** 4 kişilik Unity projesinde sahne merge çatışması ve tüm zamanın çizim/animasyona gitmesi.

| Risk | Olasılık | Etki | Erken belirti | Sahip | Tepki |
|---|---|---|---|---|---|
| Aynı `.unity` sahnesini iki kişi editler | Yüksek | Kritik | PR conflict veya prefab/meta uyuşmazlığı | K1 | Sahne sahipliği; bir owner; PR kapsamını geri böl |
| Deprem 5 saniye UI okunmuyor | Orta | Yüksek | İlk testte oyuncu hiçbir seçeneği okuyamıyor | K3/K4 | Daha kısa seçenek; 1–5 tuşları; büyük panel |
| Sel sahne sayısı çok fazla | Yüksek | Yüksek | T+18'de üçüncü sahne henüz açılmıyor | K2 | Oda+sabah bir sahne; sokak ve söylenti bir sahne |
| Şeffaf PNG'ler yapay görsel; animasyon sheet'i değil | Yüksek | Orta | Pikseller tutarsız; kareler hizalanmıyor | K3 | Basit idle, Coroutine hareketi; sprite sheet icat etmeye çalışma |
| İnteraktif menü yerine konsept resmi konur | Orta | Yüksek | Butonlar ayrı Unity UI değil | K1 | Gerçek Button ve UI event, arka plan ayrı |
| Audio/licensing | Orta | Orta | Kaynağı belirsiz ses dosyası | K4 | CC0 veya Unity/kişisel üretim; sessiz fallback |
| Eğitimde tehlikeli seçenek iyi diye sunulur | Düşük | Kritik | QA eğitim testinde tehlike görülür | K4 | Metin/puan geri alınır, güvenli feedback eklenir |
| Unity editor/batch build başarısız | Orta | Kritik | Compile red console | K1 | İlk build T+10; son çalışan commit; feature freeze |
| Takım tükenmesi | Orta | Yüksek | Geç saatte büyük hatalı PR | PM | Dönüşümlü mola, kritik iş için yedek kişi |

## Kesilebilirler (önce bunları kes)

1. Toz parçacıkları ve gelişmiş ışık shaderları.
2. Menü bulutlarının hareketli çok kareli animasyonları (statik PNG + alpha tween yeter).
3. Evde detaylı yürüyüş (E ile etkileşim + kısa geçiş yeter).
4. Banyo ayrı oda (1 saniyelik geçiş kartına indirge).
5. Selde çoklu sokak haritaları (tek sokak arka planını değiştir).
6. Depremde gerçekçi yıkılan fizik objeleri (kamera shake + ses yeter).
7. Fazladan sesler, tekrar eden NPC diyalogları.
8. Alternatif sonlar, skor tablosu ve toplanabilirler.

## Asla kesilmeyecek çekirdek

- Menünün gerçekten çalışması.
- **Sel oynanış + doğrulanmış bilgi/sel suyu kararları + sonuç**.
- **Deprem 5 seçenek / 5 saniye + doğru Çök–Kapan–Tutun feedback + sonuç**.
- Bölümden menüye geri dönme.
- Windows build'in çalışması.

**Kesme yetkisi:** PM; teknik güvenlik ve build engelinde K1 PM'ye tavsiye verir. Agent tek başına “zaman kalmadı” diyerek zorunlu bölümü silmez.
