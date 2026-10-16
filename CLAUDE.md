# AFET UYANIŞI — Claude Code Kuralları

48 saatlik game jam projesi: Unity 2D/C#, 4 kişi, **Sel + Deprem** bölümleri oynanabilir, Orman Yangını ve Heyelan menüde kilitli.
Unity sürümü: **6000.6.6f1** (Unity Hub'da tam olarak bu sürüm kullanılmalı).
Repo: https://github.com/MahmutErenCiftci/Gamejam

## Zorunlu başlangıç

1. [`AGENTS.md`](AGENTS.md) dosyasını **baştan sona oku**. Rol (K1–K4), dosya sahipliği, kapsam ve güvenlik kuralları için bağlayıcı kaynaktır.
2. [`START_HERE.md`](START_HERE.md) içindeki tablodan kullanıcının rolüne ait belgeleri oku. Rol atamaları: [`planning/OWNERS_AND_HANDOFFS.md`](planning/OWNERS_AND_HANDOFFS.md).
3. Kullanıcının rolünü (K1/K2/K3/K4) bilmiyorsan **sor**; rol bilinmeden `Assets/` altında dosya değiştirme.

Agent olarak sadece atanmış görev ID'si ve rolünün owner path'lerinde çalış. Önce `Görev ID | değişecek dosyalar | risk` bildir; sonra uygula. Başkasının `.unity` sahnesine veya `ProjectSettings/` dosyalarına izinsiz dokunma. Doğrulamadığın işi "test edildi / çalışıyor" diye raporlama. Bitirince `değişiklikler / gerçek testler / kalan risk / teslim edilen kişi` raporunu ver.

Git ve merge konusunda bu dosya ile `AGENTS.md` çelişirse **bu dosyadaki git kuralları geçerlidir** (repo ayarları GitHub'da bunlara göre zorunlu tutuluyor).

## Dal (branch) yapısı

- `main` — Son başarılı, gösterilebilir build. Sadece proje lideri (MahmutErenCiftci, K1), `dev` oynanabilir ve sağlam olduğunda `dev` → `main` Pull Request'i ile günceller.
- `dev` — Herkesin işinin birleştiği, her zaman **oynanabilir** olması gereken geliştirme dalı (paket belgelerindeki `develop` = `dev`).
- `feature/K<n>-<iş>` — Her iş için `dev`'den açılan dal. Örn: `feature/K2-flood`, `feature/K3-earthquake-art`.

## Claude için kesin kurallar

- `main` veya `dev` dalına **asla doğrudan commit veya push yapma**. Her zaman bir `feature/...` dalında çalış.
- İşe başlamadan önce: `git checkout dev && git pull`, sonra `git checkout -b feature/K<n>-<iş>`.
- Yeni dal açmadan önce kullanıcının hangi feature dalında olduğunu `git branch --show-current` ile kontrol et.
- Pull Request her zaman **`dev`'e** açılır, asla `main`'e değil: `gh pr create --base dev`.
- `git push --force`, `git reset --hard`, `git rebase` (paylaşılan dallarda), dal silme gibi geri alınamaz işlemleri **kullanıcıya sormadan yapma**.
- Commit atmadan önce `git status` çıktısını kullanıcıya göster; ilgisiz dosyaları (başka sahneler, ayar dosyaları) commit'e katma.
- PR açmadan önce `dev`'in son halini kendi dalına al (`git pull origin dev`) ve çakışma varsa kullanıcıya bildir.

## Birleştirme (merge) kuralı

- `dev` ve `main`'e **birleştirmeyi sadece proje lideri (MahmutErenCiftci) yapar**. GitHub bunu zorunlu tutar; ekip üyelerinin birleştirme butonu çalışmaz.
- Ekip üyesi olarak işin: feature dalında çalışmak, push etmek ve `dev`'e PR açmak. PR'ı açtıktan sonra kullanıcıya liderin incelemesini beklemesini söyle.
- Ekip üyesi için **asla** `gh pr merge` çalıştırma, `main`'e PR açma.
- PR'a yorum gelirse aynı feature dalında düzeltip tekrar push et; PR otomatik güncellenir.
- `main` sadece lider tarafından `dev` → `main` PR'ı ile güncellenir.

## Unity kuralları

- Her asset ile birlikte onun `.meta` dosyası da commit edilmeli; `.meta` dosyalarını asla silme veya elle değiştirme.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj` commit edilmez (`.gitignore`'da var).
- `.unity` sahne ve `.prefab` dosyalarında çakışma çok zor çözülür:
  - Başkasının üzerinde çalıştığı sahneyi düzenleme; kendi işini mümkünse ayrı prefab veya ayrı test sahnesinde yap.
  - Sahne/prefab dosyasında merge conflict çıkarsa **otomatik çözmeye çalışma**, kullanıcıya bildir.
- Klasör yapısı ve sahne sahipliği `technical/UNITY_PLAN.md` ve `AGENTS.md` §3'tedir (ör. `Assets/Scenes/Chapters/Flood/` sadece K2).
- Kök dizindeki `visuals/` klasörü **referans** görsellerdir, Unity'ye import edilmez. Oyunda kullanılacak PNG'leri K3 `Assets/Art/` altına kopyalayıp import ayarlarını yapar ve `.meta` dosyalarıyla birlikte commit eder (GUID'ler tek makinede oluşsun diye başka kimse aynı PNG'yi `Assets/`'e kopyalamaz).
- `data/*.json` örnek içeriktir; oyundaki gerçek veri `Assets/Data/` altında, K4'ün sahipliğindedir.

## Unity MCP (Claude'un Unity Editor'ü kontrol etmesi)

Proje `com.unity.ai.assistant` paketini içerir; bu paket Unity MCP sunucusunu sağlar. Kurulum her bilgisayarda bir kez yapılır (ayar kişiseldir, repoya girmez):

1. Projeyi Unity'de en az bir kez aç. Unity, relay programını `%USERPROFILE%\.unity\relay\relay_win.exe` konumuna kurar.
2. Unity'de **Edit > Project Settings > AI > Unity MCP Server** altında **Unity Bridge**'in **Running** olduğunu kontrol et (değilse **Start**).
3. Proje klasöründe şu komutu çalıştır (yolları kendi bilgisayarına göre değiştir):
   ```
   claude mcp add unity-mcp --scope local -- "C:/Users/<kullanıcı>/.unity/relay/relay_win.exe" --mcp --project-path "C:/Users/<kullanıcı>/Gamejam"
   ```
4. Claude Code'u yeniden başlat. Unity'de aynı sayfadaki **Pending Connections** altında bağlantıya **Allow** de (sadece ilk seferde).
5. Test: Claude'a "Unity konsolundaki hataları göster" de; cevap Unity'den gelirse bağlantı çalışıyordur.

Claude için Unity MCP kuralları:
- Unity MCP araçlarıyla sahnede değişiklik yapmadan önce kullanıcıya ne yapacağını söyle; başkasının sahnesini MCP ile de düzenleme.
- `Unity_AssetGeneration_GenerateAsset` (AI ile asset üretimi) Unity hesabından kredi harcayabilir; kullanıcı açıkça istemedikçe çağırma.
- Değişiklikten sonra `Unity_GetConsoleLogs` ile hata olup olmadığını kontrol et.
- MCP araçları bulunamıyorsa: Unity açık mı, Bridge **Running** mi, bağlantıya **Allow** denmiş mi kontrol ettir ve `claude mcp get unity-mcp` çıktısına bak.

## Commit mesajları

Kısa ve Türkçe, ne yapıldığını anlatan: `Oyuncu zıplama eklendi`, `Düşman hasar hatası düzeltildi`.
