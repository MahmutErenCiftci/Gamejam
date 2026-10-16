# Gamejam — Ekip Kuralları

Unity 2D oyun projesi. Unity sürümü: **6000.6.6f1** (Unity Hub'da tam olarak bu sürüm kullanılmalı).
Repo: https://github.com/MahmutErenCiftci/Gamejam

## Dal (branch) yapısı

- `main` — Oyunun ana/yayın sürümü. Sadece proje lideri (MahmutErenCiftci), `dev` oynanabilir ve sağlam olduğunda `dev` → `main` Pull Request'i ile günceller.
- `dev` — Herkesin işinin birleştiği, her zaman **oynanabilir** olması gereken geliştirme dalı.
- `feature/<isim>-<iş>` — Her iş için `dev`'den açılan kişisel dal. Örn: `feature/berk-oyuncu-hareketi`.

## Claude için kesin kurallar

- `main` veya `dev` dalına **asla doğrudan commit veya push yapma**. Her zaman bir `feature/...` dalında çalış.
- İşe başlamadan önce: `git checkout dev && git pull`, sonra `git checkout -b feature/<isim>-<iş>`.
- Yeni dal açmadan önce kullanıcının hangi feature dalında olduğunu `git branch --show-current` ile kontrol et.
- Pull Request her zaman **`dev`'e** açılır, asla `main`'e değil: `gh pr create --base dev`.
- `git push --force`, `git reset --hard`, `git rebase` (paylaşılan dallarda), dal silme gibi geri alınamaz işlemleri **kullanıcıya sormadan yapma**.
- Commit atmadan önce `git status` çıktısını kullanıcıya göster; ilgisiz dosyaları (başka sahneler, ayar dosyaları) commit'e katma.
- PR açmadan önce `dev`'in son halini kendi dalına al (`git pull origin dev`) ve çakışma varsa kullanıcıya bildir.

## Unity kuralları

- Her asset ile birlikte onun `.meta` dosyası da commit edilmeli; `.meta` dosyalarını asla silme veya elle değiştirme.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj` commit edilmez (`.gitignore`'da var).
- `.unity` sahne ve `.prefab` dosyalarında çakışma çok zor çözülür:
  - Başkasının üzerinde çalıştığı sahneyi düzenleme; kendi işini mümkünse ayrı prefab veya ayrı test sahnesinde yap.
  - Sahne/prefab dosyasında merge conflict çıkarsa **otomatik çözmeye çalışma**, kullanıcıya bildir.
- Scriptler `Assets/Scripts/` altında, sahneler `Assets/Scenes/` altında, prefablar `Assets/Prefabs/` altında tutulur.

## Commit mesajları

Kısa ve Türkçe, ne yapıldığını anlatan: `Oyuncu zıplama eklendi`, `Düşman hasar hatası düzeltildi`.
