# STOVE-01 — elektrikli ocağı kapatma

Dal: `feature/arda-interaction-ui`. Entegrasyon sahibi: Mahmut; UI gözden geçirme: Berk.

## Değiştirilen dosyalar

- Yeni: `Assets/Scripts/Chapters/Earthquake/Mode/StoveFireController.cs` ve Unity'nin ürettiği meta.
- Yeni: `Assets/Scripts/Chapters/Earthquake/Editor/StoveFireChecks.cs` ve meta.
- `Assets/Scripts/Chapters/Earthquake/Editor/EarthquakeBalanceChecks.cs`: geçici Game View boyutunu kaldırırken Unity'nin beklediği mutlak indeks kullanılır.
- `EarthquakeModeController.cs`: mevcut `stove` noktasına runtime bağlama, sayaç duraklatmasından muaf tutma, ilk tamamlamada inceleme kaydı.
- `PreparationQuestManager.cs`: aynı `utilities_location` görevini kapatma sonucuna bağlama; kilitlenmeden önce başlamış eylemleri sonuçlandırma olayı.
- `Assets/Scripts/Chapters/Common/World/Interactable.cs`: mevcut ipucu offset'inin koddan ayarlanabilmesi.
- `Assets/Data/Earthquake/earthquake_mode.json`: elektrikli ocak ve sıcak plaka metinleri.

## Ne çalışır

Gerçek `EarthquakeHome` mutfağı kullanılır. Var olan görsel dört elektrikli plakalıdır; referanstaki gaz alevi bu görsele eklenmedi. Tek plakada sekiz karelik küçük piksel kızarma animasyonu ve düğme göstergesi oluşturulur. E, mevcut RoomInteractor üzerinden kapanmayı başlatır. Düğme döner, kızarma 0,45 saniyede kaybolur; yalnız ocak tekrar kullanılamaz. Sayaç ve oyuncu hareketi sürer. Görev ancak OFF olduğunda tamamlanır, mevcut görev kimliği/sayısı korunur.

Süre kapanma ortasında biterse düğmeye basılmış eylem `BeforeLock` ile aynı karede OFF'a sonuçlanır ve görev anlık görüntüsüne girer. Sarsıntı animasyonun bitmesini beklemez. Kilit sonrası ikinci tamamlama yoktur. Başlanmamış ocak görevi sayılmaz. OFF durumu oda değişiminde/disable–enable sırasında korunur; yeni bölüm yüklemesinde sıfırlanır. Görev dışarıdan tamamlanmışsa görsel de OFF'a geçer.

Sahne/prefab YAML dosyaları düzenlenmedi. Runtime'da mevcut etkileşim noktası ocağın önüne taşınır, ipucu yukarı alınır; görselin altına iki SpriteRenderer ve gerekirse SortingGroup eklenir. Renderer materyali, sıralama katmanı ve oda rengi devralınır. Ayrı, karanlıkta sızan bir ışık eklenmez; aktif oda dışındaki efekt/ses kapalıdır. Tüm üretilen sprite/texture nesneleri temizlenir.

## Inspector / eksik varlıklar

Zorunlu manuel referans yoktur: chapter kurulumu aynı sahnedeki `PROP_Kitchen_Stove` görselini bir kez çözer ve mevcut görev/sayaç bağlarını verir. Varsa önceden eklenmiş StoveFireController'ın Inspector değerleri korunur. Plaka/düğme offset'i, FPS, kapanma süresi ve düğme açısı ayarlanabilir. Gaz ocağı sanatı bu teslimde yoktur; mevcut elektrikli ocak korunur. `runningClip` ve `clickClip` isteğe bağlı kancalardır; uygun ses dosyası bulunmadığından bağlı değildir ve sesli ocak doğrulanmamıştır.

## Nasıl test edilir

Unity: **Afet Uyanisi > Stove > Run integrated checks**. Gerçek sahneyi yükler, geçici 640×360 Game View kullanır, Input System sanal klavyesiyle E gönderir. Sonuç: `Logs/stove-play-checks.txt`; görseller `Logs/stove-on.png`, `Logs/stove-off.png`. Ardından **Afet Uyanisi > Earthquake balance > Run integrated Play Mode checks** bölümün hazırlık → deprem → korunma → sonrası → rapor regresyonunu çalıştırır. Test araçları sahneyi kaydetmez; test sonunda Edit Mode'a dönün.

Elle: Yeni Oyun → Deprem → hazırlık başladıktan sonra salondaki mutfağa git → `[E] Ocağı kapat` → kızarmayı/düğmeyi/soğumayı izle. Sayaç akmalı, görev bir kez işaretlenmeli, tekrar E ipucu çıkmamalı. Odadan çıkıp dön; ocak kapalı kalmalı. Tekrar Dene; açık başlamalı. Sarsıntı sırasında ocağa yönlendiren etkileşim çıkmamalı.

## Eğitsel içerik

Kapatma hazırlık sırasında yapılır. Sarsıntı başladıktan sonra ocağa gitme ödüllendirilmez; Çök–Kapan–Tutun önceliklidir. Plakanın kapandıktan sonra sıcak kalabileceği söylenir. Çocuk tesisata/vanaya müdahale etmez. Gaz kokusunda düğmelere dokunmama ve büyüğe haber verme metni korunur. Kontrol kaynağı: [AFAD deprem önlemleri](https://www.afad.gov.tr/deprem-oncesi-ani-ve-sonrasi-onlemini-al-guvende-kal), [AFAD ilk 72 saat](https://www.afad.gov.tr/kurumlar/afad.gov.tr/3473/xfiles/ilk72saat1.pdf).

## Test sonucu / teslim

Play Mode raporları ve son durum aşağıdaki teslim kaydına yazılır. Windows build, diğer bilgisayar testi, owner incelemesi ve dev'e birleştirme bu görevde yapılmadı. Main/dev'e push yoktur. PM/K1 incelemesi ve menü–Sel–Deprem entegrasyon smoke testi sonraki sahibindedir.

17.10.2026 son doğrulama: **PASS**, Unity 6000.6.6f1'de derleme. `Logs/stove-play-checks.txt` 18:17:51: gerçek mutfakta E, sekiz kare animasyon, süre/hareket devamı, tek tamamlama, OFF/yeniden yükleme, tint/gizleme, süre bitişi ve disable kesintileri geçti. `Logs/earthquake-balance-play-checks.txt` 18:18:40: hazırlık → sarsıntı → denge → sonrası → rapor, çanta ve A/D/Space regresyonu geçti. Son MCP Console sorgusu: **0 Error**. 640×360 ON/OFF ekran görüntüleri gözle incelendi; Türkçe ipucu görünür, kızarma doğru plakadadır. `git diff --check` geçti. Ses **UNTESTED**: klipler eksik. Windows build **UNTESTED**: bu teslimde alınmadı. Sahne dosyaları kaydedilmedi/değiştirilmedi. Owner incelemesi bekleniyor; birleşmiş/Done olarak işaretlenmedi.
