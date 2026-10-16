# GitHub / Unity 4 Kişilik Çakışmasız Çalışma Rehberi

## Git düzeni

```text
main       = son başarılı, gösterilebilir build
   └─ dev = entegre ve smoke test edilmiş oyun
        ├─ feature/K1-menu
        ├─ feature/K2-flood
        ├─ feature/K3-earthquake-art
        └─ feature/K4-story-qa
```

Her branch owner'a aittir. Büyük/uzun ömürlü çok sayıda feature branch üretmeyin. Özellikle `.unity` scene owner branch'i dışında edit edilmez.

## Unity ayarları (K1, ilk 1 saatte)

- `Edit > Project Settings > Editor > Asset Serialization > Force Text`.
- `Edit > Project Settings > Editor > Version Control > Mode: Visible Meta Files` (sürüme göre menü isimleri değişebilir).
- Unity sürümü `ProjectSettings/ProjectVersion.txt` ile sabit tutulur.
- `.gitignore`: `Library/`, `Temp/`, `Obj/`, `Builds/`, `Logs/`, `UserSettings/`, `.vs/`, IDE geçici dosyaları.
- Sprite eklerken PNG ile `.meta` birlikte commit edilir; GUID korunur.
- Git LFS kullanılacaksa **başta** K1 kurar ve 4 kişiye eşitlemeden devreye girmez; aceleyle ortada etkinleştirmeyin.

## Günlük geliştirme (kısa)

```bash
git fetch origin
git checkout dev
git pull --ff-only origin dev
git checkout -b feature/K2-flood       # K2 ilk kez oluştururken
# sadece kendi sahne, script ve ilgili meta alanlarında geliştirme
git status
git add Assets/Scenes/Chapters/Flood Assets/Scripts/Chapters/Flood
git commit -m "GAME-04 Flood decision flow"
git push -u origin feature/K2-flood
# PR -> dev, inceleme K1, test bilgisi gerekli
```

K1 diğer branch'leri merge ederken zorla overwrite etmez; özellikle sahne GUID ve `.meta` conflict durumunda ilgili owner'ı çağırır. `git reset --hard` ile başkasının çalışmasını silmek yasak.

## PR kabul şartları

- PR başlığı: `[GAME-04] Flood decision flow`.
- Hangi dosyalar değişti? `AGENTS.md` ownership ile uyumlu mu?
- **Çalıştırma/test adımları** yazıldı mı?
- Uygunsa ekran görüntüsü/oynanış GIF'i linki var mı?
- `UNTESTED` ise açıkça yazıldı mı?
- K1 PR'ı inceleyip `dev`'e merge ettikten sonra menü → Sel → Deprem smoke test yapar.

## Çakışma azaltma

1. Her sahne için **tek owner**.
2. Ortak prefab dosyalarında tek owner K3; kullanıcılar prefab instance referanslar.
3. `ProjectSettings/`, `Packages/` ve scenes in build sadece K1.
4. Menüye yeni bir button eklemek için K3 grafik verir ama sahneyi K1 edit eder.
5. İki kişiye aynı scripti “hızlansın” diye aynı anda assign etme.
6. T+36'dan sonra yalnız bugfix PR ve başarılı build işaretlenir.

## Kriz prosedürü

- `dev` kırılırsa K1 son çalışan commit/hash'i bulur, owner'lardan hangisinin PR'ında hata olduğunu belirler.
- Son çalışan build klasörü silinmez; `Builds/Windows/RC_...` ayrı yedek.
- Conflict genişse rastgele satır birleştirmek yerine ilgili branch owner'ı sahneyi yeniden export/entegrasyon yapar.
- Sonuç bir PR açıklamasına ve `qa/BUG_LOG.md` kaydına yazılır.

## Bu repoya özel (GitHub'da zorunlu)

- Repo: https://github.com/MahmutErenCiftci/Gamejam · varsayılan dal `dev` (bu belgedeki eski `develop` adı = `dev`).
- `main` ve `dev`: doğrudan push, force push ve dal silme **yasak**; kod yalnızca PR ile girer.
- `dev`'e ve `main`'e **yalnızca MahmutErenCiftci (K1)** birleştirebilir; diğer üyelerde merge butonu çalışmaz.
- Dal adı: `feature/K<n>-<iş>` (ör. `feature/K2-flood`). PR açıklaması `.github/pull_request_template.md` ile otomatik gelir.
