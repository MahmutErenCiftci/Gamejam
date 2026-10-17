# Akşam Sel — Zikzak rota görev dağılımı

Tasarım: [`docs/SEL_ZIGZAG_PROMPT.md`](SEL_ZIGZAG_PROMPT.md) (liderin promptu, birebir) · Görsel referans: `visuals/concept/sel_aksam_zigzag_ref.jpeg`
Mevcut temel ve kod haritası: [`docs/SEL_AKSAM_DEVIR.md`](SEL_AKSAM_DEVIR.md) · Genel görevler: [`docs/GOREV_DAGILIMI.md`](GOREV_DAGILIMI.md)

## Akış (özet)

Sokakta başlangıç (uyarı bildirimi bir kez, görev: "Güvenli güzergâhı takip et.") → dönüşlü sokaklar (≥2 dönüş) → **tek kavşak** (KISA YOL / UZUN YOL)
- **Kısa yol:** köşeyi dönünce yıkıntıyla kapalı → "Yol kapanmış. Kavşağa dönüp diğer yolu kullanmalıyım." → oyuncu **yürüyerek** geri döner
- **Uzun yol:** ≥3 dönüş → köşeden sonra **ağaç düşer** (tek sefer, güvenli mesafe) → küçük kırık dalı **E'ye art arda basarak** kenara it → "Yol açıldı." → bir köşe daha → **devam noktası** (bölüm sonu)

Rotadan çıkanlar (kodu projede durur, sahnede kapalı): şifre paneli, bahçe kapısı, ipucu levhaları, söylenti kartı, rüzgâr. Yeni sayaç yok. Cenk asla sel suyunda yürümez.

## Kim ne yapar

| ID | Kişi | İş | Dosyalar (sahiplik) | Bitti sayılır |
|---|---|---|---|---|
| SEL3-01 | **Mahmut** (+agent) | Zikzak harita: dönüşlü sokaklar, kavşak, kısa yolun kapalı ucu, uzun yol, collider'lar, kamera sınırları, tabelalar (dünya sanatı) | `Assets/Prefabs/FloodEvening/EveningNeighborhood.prefab`, `Assets/Art/FloodEvening/ENV/`, `Assets/Scenes/Chapters/Flood/FloodEvening.unity` | §14'teki harita/kamera kabul maddeleri; anchor'lar: `Spawn, Fork, LowRoad/BarrierZone, HighRoad, TreeStopSpot, TreeFallSpot, SafeZone, SafeSpot` |
| SEL3-02 | **Mahmut** (+agent) | Akış: kısa yol mesajı bir kez, ağaç → dal → devam noktası sırası, sayaç kaldırma, sonuç ekranı sadeleştirme, ekip teslimlerini sahneye yerleştirme | `Assets/Scripts/Chapters/FloodEvening/Flow/` | Deprem → Sel → sonuç uçtan uca, iki yol da denenmiş |
| SEL3-10 | **Eren** | **Ağaç düşüşü** (tek seferlik kısa sinematik): Cenk güvenli noktada durur, hareket kısa kilit, kırılma sesi, ağaç kısa animasyonla yola düşer, yaprak/toz, kontrol geri gelir. Büyük gövde dekor; ağaç Cenk'in üstüne düşmez; tekrar oynamaz | yeni: `Assets/Scripts/Chapters/FloodEvening/Events/FloodTreeFallEvent.cs`, `Assets/Prefabs/FloodEvening/FE_TreeFall.prefab`, `Assets/Art/FloodEvening/Events/Tree/` | `Test_Eren_TreeFall.unity`'de bir kez oynuyor, kontrol geri geliyor, reset'te sıfırlanıyor |
| SEL3-20 | **Arda** | **E'ye art arda basma**: `BranchObstacle`'a "art arda bas" kipi (varsayılan): her ayrı E basışı ilerletir, basılı tutmak ilerletmez, basış sayısı Inspector'da (varsayılan 12), menzil dışında/diyalog açıkken çalışmaz, uzaklaşıp dönünce ilerleme korunur, bitince dal kenara kayar + collider taşınır + `branch_cleared` | `Assets/Scripts/Chapters/FloodEvening/Events/BranchObstacle.cs`, `Assets/Prefabs/FloodEvening/FE_BranchObstacle.prefab` | `Test_FloodEvening_EVENTS.unity`'de §10–11 maddeleri tek tek doğrulanmış |
| SEL3-21 | **Arda** | Metinler: kısa yol kapalı, kavşak iç sesi, ağaç sonrası, "Yol açıldı.", görev metni, sonuç ekranı satırları | `Assets/Data/Story/flood_evening.json` | Oyundaki tüm yeni metinler JSON'dan geliyor, Türkçe karakterler doğru |
| SEL3-30 | **Berk** | UI: "[E] Art arda bas — Engeli kenara it" ipucu + ilerleme çubuğu (basışta küçük geri bildirim), "Yol açıldı." kısa bildirim, görev metni şeridi, kısa yol uyarı kutusu görünümü | `Assets/Scripts/Chapters/FloodEvening/UI/`, `Assets/Prefabs/FloodEvening/UI/` | 480x270 ve 1920x1080'de okunur, ekran içinde; Arda'nın bileşenine prefab olarak bağlanıyor |
| SEL3-40 | **Eren** | Başlangıç uyarı bildirimi metni ve görünümü (AlertNotification) yeni akışa uygun | `Assets/Data/FloodEvening/flood_evening_alert.json`, `FE_WarningPhone.prefab` | Bildirim başta bir kez, okunur, E ile kapanıyor |

**Bağımlılık:** herkes, liderin `feature/mahmut-entegrasyon` → `dev` PR'ı birleşince `dev`'den kendi dalını açar. SEL3-30 (Berk) çubuğu, SEL3-20 (Arda) bileşenine bağlanır: Arda önce Inspector'da `progressView` gibi bir alan açar, Berk prefab'ı yapar. Sahneye yerleştirme (FloodEvening.unity) sadece Mahmut.

## Hazır promptlar (kendi Claude'una yapıştır)

### Berk
```
Ben Berk (berkatac41). Görevim SEL3-30: akşam sel bölümünün UI'ı.
1) git checkout dev && git pull && git checkout -b feature/berk-sel-ui
2) Oku: CLAUDE.md, AGENTS.md, planning/EKIP_ROLLERI.md, docs/SEL_ZIGZAG_PROMPT.md (§10, §11), docs/SEL_ZIGZAG_GOREVLER.md, docs/SEL_AKSAM_DEVIR.md. Görsel: visuals/concept/sel_aksam_zigzag_ref.jpeg (kare 11: ilerleme çubuğu).
3) Yap: "[E] Art arda bas — Engeli kenara it" ipucu + yatay ilerleme çubuğu (her basışta küçük zıplama/parlama), "Yol açıldı." kısa bildirimi, görev metni şeridi ("Güvenli güzergâhı takip et."), kısa yol uyarı kutusu. Sadece Assets/Scripts/Chapters/FloodEvening/UI/ ve Assets/Prefabs/FloodEvening/UI/ altında çalış; mevcut stil (Pixelify, palet) ile uyumlu.
4) Arda'nın BranchObstacle bileşenine bağlanacak arayüzü (ör. SetProgress(float), Show/Hide) Arda ile konuş; sahne dosyalarına dokunma, test için Assets/Scenes/Test/Test_Berk_SelUI.unity kullan.
5) 480x270 ve 1920x1080'de dene. Bitince dev'e PR aç (gh pr create --base dev), Mahmut'a haber ver.
```

### Arda
```
Ben Arda (ardabekduz123-beep). Görevlerim SEL3-20 (E'ye art arda basarak dal itme) ve SEL3-21 (akşam sel metinleri).
1) git checkout dev && git pull && git checkout -b feature/arda-sel-itme
2) Oku: CLAUDE.md, AGENTS.md, planning/EKIP_ROLLERI.md, docs/SEL_ZIGZAG_PROMPT.md (§9–§11), docs/SEL_ZIGZAG_GOREVLER.md, docs/SEL_AKSAM_DEVIR.md.
3) SEL3-20: Assets/Scripts/Chapters/FloodEvening/Events/BranchObstacle.cs'e "art arda bas" kipini ekle ve varsayılan yap: her ayrı E basışı (GameInput, bu karede basıldı) ilerletir; basılı tutmak ilerletmez; gereken basış Inspector'da (varsayılan 12); kare hızından bağımsız; menzil dışında ve FloodEveningHub.Paused iken çalışmaz; aynı basış başka etkileşimi tetiklemez; uzaklaşıp dönünce ilerleme korunur; bitince dal kısa animasyonla yol kenarına kayar, collider da taşınır, "Yol açıldı." gösterilir, FloodEveningHub.Complete("branch_cleared"). Eski "basılı tut" kipini silme. Çubuk/ipucu için Berk'in UI prefab'ını Inspector alanından al.
4) SEL3-21: Assets/Data/Story/flood_evening.json'a yeni satırlar: kısa yol kapalı ("Yol kapanmış. Kavşağa dönüp diğer yolu kullanmalıyım."), kavşak iç sesi, ağaç düştükten sonra, "Yol açıldı.", görev metni, sonuç satırları. Dosyadaki "ui" dizisini KORU.
5) Test: Assets/Scenes/Test/Test_FloodEvening_EVENTS.unity (sahneyi kaydetmen gerekirse kendi Test_Arda_* kopyanı aç). §14'teki E maddelerini tek tek dene. Bitince dev'e PR aç, Mahmut'a haber ver.
```

### Eren
```
Ben Eren (erenatacc41). Görevlerim SEL3-10 (ağaç düşüşü sinematiği) ve SEL3-40 (başlangıç uyarı bildirimi).
1) git checkout dev && git pull && git checkout -b feature/eren-sel-agac
2) Oku: CLAUDE.md, AGENTS.md, planning/EKIP_ROLLERI.md, docs/SEL_ZIGZAG_PROMPT.md (§9), docs/SEL_ZIGZAG_GOREVLER.md, docs/SEL_AKSAM_DEVIR.md. Görsel: visuals/concept/sel_aksam_zigzag_ref.jpeg (kare 9–10). Eski FloodSweepCutscene yaklaşımın iyi bir başlangıç.
3) SEL3-10: yeni Assets/Scripts/Chapters/FloodEvening/Events/FloodTreeFallEvent.cs (namespace AfetUyanisi.Chapters.FloodEvening, IFloodEveningResettable) + Assets/Prefabs/FloodEvening/FE_TreeFall.prefab. Oyuncu TreeStopSpot trigger'ına girince TEK SEFER: Cenk durur, hareket kısa kilitlenir (FloodEveningHub.NotifyOpened/Closed), kırılma sesi (GameAudio'da varsa), ağaç kısa dönme/kayma animasyonuyla ilerideki yola düşer, yaprak/toz, yol kapanır (collider görselle aynı anda), kontrol geri gelir. Ağaç Cenk'in üstüne düşmez; büyük gövde dekor, geçidi kapatan küçük dal Arda'nın FE_BranchObstacle'ı. Sanat: Assets/Art/FloodEvening/Events/Tree/ (PPU 64, Point filtre). Fizik yok.
4) SEL3-40: Assets/Data/FloodEvening/flood_evening_alert.json metnini yeni akışa göre gözden geçir (akşam, mahalle, güvenli güzergâh); bildirim başta bir kez gelsin.
5) Test: Assets/Scenes/Test/Test_Eren_TreeFall.unity. Olay bir kez oynuyor, kontrol geri geliyor, reset'te sıfırlanıyor. Bitince dev'e PR aç, Mahmut'a haber ver.
```

### Mahmut (kendi Claude'u / agent)
SEL3-01 zikzak harita + SEL3-02 akış. Agent'a ver: `docs/SEL_ZIGZAG_PROMPT.md` tamamı, ENV ve LEAD bölümleri. Ekip PR'ları gelince FloodEvening.unity'ye yerleştir, uçtan uca oyna, build al.

## Teknik notlar (önceki agent'tan devir)

- **Art arda basma (Arda):** Her E basışı mevcut `RoomInteractor` üzerinden `Play()`'i bir kez çağırsın, her çağrı +1 ilerletsin. Böylece menzil kontrolü, tek tetikleme ve basılı tutunca ilerlememe hazır gelir. Yeni kipi enum'a açık numarayla ekle (`AlternativePassage=0, SmallBranch=1, Mash=2`), varsayılanı `Mash` yap; aksi halde kayıtlı prefab'ın kipi kayar.
- **Ağaç → dal bağlantısı (Eren + Arda):** Ağaç düşene kadar dal kapalı/etkileşimsiz olsun (ör. `startArmed=false` + `Arm()`). Düşüşten önce düşüş alanını katı bir `FallZoneBlock` collider'ı kapatsın; düşüş anında dalın collider'ı açılsın.
- **Ses (Eren):** GameAudio'da ağaç kırılma sesi yok; geçici olarak `SFX_Glass_Crack` veya `SFX_Books_Fall`. Gerçek ses Mahmut'un ses paketine eklenecek.
- **Aynı adlı görsel:** `Assets/Art/FloodEvening/ENV/Props/PROP_FE_Branch.png` ile `Assets/Art/FloodEvening/Events/` altındaki dal aynı adlı ama farklı GUID. Yeni sanatı farklı adla koy (ör. `PROP_FE_TreeTrunk`, `PROP_FE_BranchSmall`).
