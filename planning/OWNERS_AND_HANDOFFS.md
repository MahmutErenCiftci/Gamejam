# Takım Sahipliği, Entegrasyon ve Handoff Sözleşmesi

> **48 saat için kural:** Bir dosyanın **tek sahibi** vardır. Sorumluluk başka birine devredilebilir ancak yazılı mutabakat ve görev durumu gerektirir. Bu belgeyi kickoff'ta 4 kişi birlikte okuyun.

## Kişi atamaları — doldurun

| Rol | İsim / GitHub kullanıcı adı | Çalışma kanalı | Onay |
|---|---|---|---|
| K1 — Tech Lead / Menu / Integration | MahmutErenCiftci | ______ | [ ] |
| K2 — Flood Gameplay | ______ | ______ | [ ] |
| K3 — Visuals + Earthquake | ______ | ______ | [ ] |
| K4 — Product / Narrative / QA | ______ | ______ | [ ] |

**İnsan PM:** ______ (ürün/kapsam onayı; K1 ile aynı kişi olmak zorunda değil).  
**Merge/Build yetkilisi:** K1 — MahmutErenCiftci (GitHub kuralıyla `dev`/`main`'e yalnız o birleştirebilir).  
**K2–K4 adayları (GitHub):** ardabekduz123-beep, erenatacc41, berkatac41 — kickoff'ta rollere yazın.  
**Ana iletişim kanalı:** ______ · **T+0 saati:** ______ · **Son teslim saati:** ______.

## Dosya sahipliği (çakışma matrisi)

| Path | Yazabilir | Diğerlerinin kullanımı |
|---|---|---|
| `Assets/Scenes/Menu/**` | **K1** | Sadece PR yorum, dosyayı değiştirme |
| `Assets/Scripts/Menu/**`, `Core/**` | **K1** | Public API sözleşmesine göre çağır |
| `Assets/Scenes/Chapters/Flood/**` | **K2** | K3 asset teslim eder, sahneyi K2 yerleştirir |
| `Assets/Scripts/Chapters/Flood/**` | **K2** | İçerik JSON'larına referans verebilir |
| `Assets/Scenes/Chapters/Earthquake/**` | **K3** | K4 metin teslim eder, sahneyi K3 değiştirir |
| `Assets/Scripts/Chapters/Earthquake/**` | **K3** | Public core API'yi kullanır |
| `Assets/Art/**`, `Assets/Animations/**`, `Assets/Prefabs/Visual/**` | **K3** | K1/K2 oluşturulmuş assetleri sahnelerinde **referanslar** |
| `Assets/Data/**`, `Assets/Audio/**`, `story/**`, `qa/**` | **K4** | K2/K3 K4'ün veri formatını kullanır |
| `ProjectSettings/**`, `Packages/**` | **Yalnız K1** | Değişiklik talebi aç |
| `Assets/Scripts/Shared/**` | **K1 kontrat sahibi** | Gerekiyorsa K2/K3'ün katkısı ayrı PR + onay |
| `planning/**`, `AGENTS.md` | **PM/K4** | Kapsam değişiklikleri PM onayıyla |

**Unutmayın:** `.meta` dosyaları sahip olunan varlıkla birlikte değişir. Sprite import ayarını K3 yapar. K1/K2 `.meta` dosyasını “otomatik düzeltiyorum” diyerek yazmaz.

## Handoff süreçleri

**K4 → K2: Sel diyalog verisi**
1. K4 `Assets/Data/Story/flood.json` veya `data/flood_story_example.json` örneği üzerinde onaylanmış Türkçe metin verir.
2. K2 akışta kullanır; yanlış veya eksik iddiayı K4'e geri bildirir, **metni sessizce değiştirmez**.
3. Kabul: T+12 ilk set, T+18 son seçim seti.

**K4 → K3: Deprem seçimi + geri bildirim**
1. K4 5 seçenekli metni, doğru cevap ID'sini ve geri bildirim cümlelerini teslim eder.
2. K3 `EarthquakeChoiceController`'a bağlar, sayaç testini yapar.
3. Kabul: T+12 içerik, T+18 oynanabilir test.

**K3 → K1: Menü grafikleri**
1. PNG'leri `Assets/Art/Menu/` altında tutar; referans UI yerleşimini `docs/ART_BIBLE.md` ile paylaşır.
2. K1 sadece `MainMenu.unity` içinde kullanır, K3'ün sprite dosyasını değiştirmez.
3. Kabul: T+8 temel görseller; T+28 polish.

**K3 → K2: Sel çevre assetleri**
1. Şeffaf karakter/prop ve tek yağmurlu sokak arka planını Unity import ayarı ile teslim eder.
2. K2 `FloodChapter.unity` içinde yerleştirir; eksik kıyafet/animasyon görevini K3'e issue açar.
3. Kabul: T+18 minimum çevre, T+28 son görsel.

**K2/K3 → K1: Bölüm entegrasyonu**
1. Bölüm sahnesinin yolu, sonuçtan menüye geri dönüş contract'ı ve build referansını PR'da bildir.
2. K1 sahnenin build listesinde olduğunu doğrular. K2/K3'ün sahnesini gizlice değiştirmeden hata için PR isteği yapar.
3. Kabul: T+28 iki bölüm tam loop.

## Bağımlılık ve blokaj protokolü

- **K1 önce sahne isimleri + `GameSceneRouter` yolunu** T+6'da duyurur.
- **K2 ve K3 ortak kod için K1'in kontratı** gelmezse geçici local adaptor yapabilir; ortak dosyayı değiştiremez.
- K4 içerik teslimi T+12 gecikirse K2/K3 örnek metinle **geçici** çalışabilir, açık `TODO-DATA` işareti bırakır.
- Bir blokaj 20 dk sürerse sohbete şu yazılır: `BLOCKED: görev / beklediğim dosya veya karar / etki / yardım edecek kişi / geçici çözüm`.
- Kimse başkasının görevini sahiplenmeden “çözmek için” onun branch'ine commit atmaz.

## Günlük durum formu (her 6 saatte 10 dk)

```
T+__ | K__ | Dün/önceki 6 saatte bitti: __
Şu an: __ | Önündeki engel: __ | Sonraki teslim T+__: __
PR / branch: __ | Build veya Unity testi: PASS / FAIL / UNTESTED
```

## Devralma / kriz senaryosu

Bir kişi 2+ saat tamamen devre dışı kalırsa PM, görev sahibini ve ilgili sahne/dosya sahipliğini **önce görev panosunda** değiştirir; önceki kişinin çalışmalarının durumu yazılır, yeni kişi tek branch'ten devam eder. Aynı sahnede paralel geliştirme yapılmaz.

## Final kabul

- **K1:** menü, sahne geçişleri, build.
- **K2:** Sel tam oynanış.
- **K3:** Deprem tam oynanış + temel görseller.
- **K4:** iki bölümdeki eğitsel metinlerin gözden geçirilmesi, test logları, jüri konuşması.
- **PM:** final sürümü yayımlama kararı.
