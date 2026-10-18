# SEL3-20 / SEL3-21 — Arda teslimi

Sahip: Arda (`ardabekduz123-beep`). Hedef: `dev`; birleştirme Mahmut'ta.

## Bağlantılar

- `BranchObstacle.Mode`: `AlternativePassage=0`, `SmallBranch=1`, `Mash=2`. Yeni varsayılan Mash, 12 ayrı E basışı.
- `FE_BranchObstacle` prefab'ı Mash kullanır. `branchRoot`, `Branch` çocuğudur; sprite ve katı collider aynı Transform ile taşınır. `clearOffset` yerel koordinattadır; Mahmut gerçek haritada boş yol kenarına göre ayarlamalı.
- `startArmed=false`: başlangıçta dal görseli/collider'ı ve etkileşim kapalı. Eren, düşüşün uygun anında `Arm()` çağırmalı; düşüş bitene kadar kendi hareket kilidi ve `FallZoneBlock` korumasını sürdürmeli. Arm çağrısının tekrarı ilerlemeyi silmez.
- Tuşu yalnız `RoomInteractor` okur. Her `Play()` çağrısı +1; Mash içinde ikinci bir input döngüsü yok. Menzil/Paused kontrolü ek güvenlik kapısıdır. Ara basışlar hareketi kalıcı kilitlemez.
- Tamamlanmada kısa kayma → `Say("FE_PATH_OPEN", ...)` → `Complete("branch_cleared")`. Dalın collider'ı silinmez; dal ile kenara gider. Bu adım bölümü bitirmez.

## Berk'e UI arayüzü

Inspector alanı `progressView`: prefab üzerindeki veya sahnedeki bir MonoBehaviour. Bileşen, `AfetUyanisi.Chapters.FloodEvening.IBranchProgressView` uygular:

```csharp
void Show(string hint);        // Her görünür kare çağrılabilir; idempotent olmalı.
void SetProgress(float value); // 0..1; ilerleme uzaklaşınca korunur.
void Press();                  // Yalnız kabul edilen basışta küçük görsel tepki.
void Hide();
```

Prefab referansı verilirse çalışma anında bir kez örneklenir. UI kendi E okuyucusunu eklememeli. Berk'in prefab'ı henüz teslim edilmediğinden alan boş; geçici, tıklamaları yakalamayan çubuk/ipucu kullanılır. Fare eşdeğeri bu teslimde yoktur.

## Metin ID'leri

`flood_evening.json` içindeki önceki 32 `ui` kaydı korunur; `objective_route`, `branch_mash_action`, `branch_mash_hint` eklenir.

- `FE_SHORT_CLOSED`: kapalı kısa yol; mevcut controller arar.
- `FE_FORK`: KISA YOL / UZUN YOL iç sesi.
- `FE_TREE_FELL`: düşüş sonrası güvenli mesafe ve hafif dal açıklaması; Eren olayından `Hub.Say` ile istenebilir.
- `FE_PATH_OPEN`: “Yol açıldı.”; BranchObstacle ister.
- `FE_OBJECTIVE` / `ui.objective_route`: “Güvenli güzergâhı takip et.”; görev şeridine Berk/Mahmut bağlar.
- `FE_RESULT_MISTAKES/short_road_tried`: nötr kısa yol kaydı; controller zaten arar.
- `FE_RESULT_STEPS/branch_cleared`: küçük hafif dal açıklaması.

Eski kiplerin diyalog ID'leri ve eski UI etiketleri silinmez. Süre baskısı kaldırıldığı için sonuç notundaki 120 saniye ifadesi güncellenmiştir.

## Yeniden çalıştırma

Unity 6000.6.6f1, `Afet Uyanisi > Arda > Run Sel Itme Checks` menüsü. Açık sahneler önce kaydedilmiş olmalı. Test kendi `Test_Arda_SelItme.unity` kopyasında Play Mode açar ve gerçek Input System klavye olaylarını mevcut GameInput / RoomInteractor üzerinden gönderir. Rapor: `Logs/arda-sel-itme-checks.txt`.

Elle denemek için kendi sahnesini aç, Play, dal bileşeninin `Test: Arm (Play Mode)` bağlam menüsünü seç, yaklaşıp E'ye bas. Basılı tutma, uzaklaş/geri gelme ve 12 basış sonunda kaymayı kontrol et. Başkasının EVENTS sahnesini kaydetme.

## Entegrasyonda kalan kontroller

Berk'in gerçek UI prefab'ı, Eren'in düşüş olayı, gerçek zikzak haritada dalın kaydığı yer ve geçiş genişliği, 480×270 / 1920×1080 görsel kontrolü, fare eşdeğeri, Deprem → Sel → Menü smoke ve Windows build bu teslimin bağımsız otomasyonundan ayrı doğrulanmalı. Ana bölüm sahnelerine dokunulmadı.

## Test sonucu — 18 Ekim 2026

Unity **6000.6.6f1** batch Play Mode'da gerçek `GameInput` / `RoomInteractor` üzerinden **19 kontrol geçti**: Arm öncesi kapalı durum; 500 ms basılı tutmanın tek basış sayılması; basışlar arasında hareket; ayrı basış; tekrar Arm; menzil dışı; Paused; uzaklaşıp dönüşte korunma; tek hedefe girdi; 11 basışta kapalı kalma; 12. basışta kayma; collider ile son konum ve tek bildirim; tekrar tetiklenmeme; reset; Inspector basış sayısı; kayma sırasında disable; kesilen kaymanın reset'i; eski AlternativePassage ve SmallBranch kipleri.

Derleme hatası yok. JSON ayrıştırması, benzersiz ID'ler ve önceki 32 UI kaydının birebir korunması ayrıca kontrol edildi. Otomasyonda yalnız geçici InputSettings örneği kullanılır; proje input ayarları kaydedilmez. Yukarıdaki entegrasyon ve görsel kontroller **UNTESTED**; tüm bölüm için Done iddiası yoktur.
