# AFET UYANIŞI — Claude Code Kuralları

48 saatlik game jam projesi: Unity 2D/C#, 4 kişi, **Sel + Deprem** bölümleri oynanabilir, Orman Yangını ve Heyelan menüde kilitli.
Unity sürümü: **6000.6.6f1** (Unity Hub'da tam olarak bu sürüm kullanılmalı).
Repo: https://github.com/MahmutErenCiftci/Gamejam

## Zorunlu başlangıç

1. [`AGENTS.md`](AGENTS.md) dosyasını **baştan sona oku**. Kapsam, agent davranışı ve afet güvenlik kuralları için bağlayıcı kaynaktır.
2. [`planning/EKIP_ROLLERI.md`](planning/EKIP_ROLLERI.md) dosyasını oku. **Kim hangi dosyaya/sahneye sahip, kim kime ne teslim eder — bu dosya geçerlidir**; paket belgelerindeki K2/K3/K4 rol tanımları bu ekipte kullanılmıyor.
   - Mahmut (MahmutErenCiftci): lider/genel, Sel + Deprem sahneleri, görsel, ses, test, build
   - Berk (berkatac41): giriş sayfası (ana menü) + ortak UI
   - Arda (ardabekduz123-beep): diyalog sistemi + diyalog metinleri
   - Eren (erenatacc41): haber (TV haberi, söylenti kartları, deprem uyarı bildirimi)
3. Kullanıcının kim olduğunu `CLAUDE.local.md`'den veya `git config user.name`'den anla; bilmiyorsan **sor**. Kim olduğu bilinmeden `Assets/` altında dosya değiştirme. Görevlerini `planning/TASK_BOARD.csv`'de kendi adına göre bul.

Agent olarak sadece atanmış görev ID'si ve rolünün owner path'lerinde çalış. Önce `Görev ID | değişecek dosyalar | risk` bildir; sonra uygula. Başkasının `.unity` sahnesine veya `ProjectSettings/` dosyalarına izinsiz dokunma. Doğrulamadığın işi "test edildi / çalışıyor" diye raporlama. Bitirince `değişiklikler / gerçek testler / kalan risk / teslim edilen kişi` raporunu ver.

Git ve merge konusunda bu dosya ile `AGENTS.md` çelişirse **bu dosyadaki git kuralları geçerlidir** (repo ayarları GitHub'da bunlara göre zorunlu tutuluyor).

## Dal (branch) yapısı

- `main` — Son başarılı, gösterilebilir build. Sadece proje lideri (MahmutErenCiftci), `dev` oynanabilir ve sağlam olduğunda `dev` → `main` Pull Request'i ile günceller.
- `dev` — Herkesin işinin birleştiği, her zaman **oynanabilir** olması gereken geliştirme dalı (paket belgelerindeki `develop` = `dev`).
- `feature/<isim>-<iş>` — Her iş için `dev`'den açılan dal. Örn: `feature/berk-menu`, `feature/arda-dialogue`, `feature/eren-news`, `feature/mahmut-flood`.

## Claude için kesin kurallar

- `main` veya `dev` dalına **asla doğrudan commit veya push yapma**. Her zaman bir `feature/...` dalında çalış.
- İşe başlamadan önce: `git checkout dev && git pull`, sonra `git checkout -b feature/<isim>-<iş>`.
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
- Klasör ve sahne sahipliği `planning/EKIP_ROLLERI.md`'dedir: `MainMenu.unity` sadece Berk, `FloodChapter.unity`/`EarthquakeChapter.unity` sadece Mahmut. Arda ve Eren sahne dosyası düzenlemez, **prefab + script** teslim eder (deneme için kendi `Assets/Scenes/Test/<İsim>_*.unity` sahnelerini kullanabilir).
- Kök dizindeki `visuals/` klasörü **referans** görsellerdir, kendiliğinden Unity'ye girmez. Bir PNG'yi `Assets/` altına sadece o klasörün sahibi kopyalar (karakter/arka plan → Mahmut, menü/UI → Berk, haber görselleri → Eren) ve `.meta` dosyasıyla commit eder. Aynı PNG'yi iki kişi ayrı ayrı import ederse farklı GUID oluşur ve referanslar kırılır.
- `data/*.json` örnek içeriktir; oyundaki diyalog verisi `Assets/Data/Story/` (Arda), haber verisi `Assets/Data/News/` (Eren) altındadır.

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
