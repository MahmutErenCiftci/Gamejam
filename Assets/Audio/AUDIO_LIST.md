# AFET UYANIŞI — Ses paketi ve otomatik bağlantılar

34 özgün sentez WAV (kodla üretildi, `tools/audio/generate_audio.js`; lisans: proje sahibi) + 7 Kenney CC0 OGG
(`Licenses/`). Paketi Codex (Astra, `feature/astra-audio`) hazırladı; güncel oyuna entegrasyonu ve bağlantılar
bu sürümde güncellendi.

## Nasıl çalışır

- Sahne/prefab düzenlemesi yok. `GameAudio` ilk sahneden önce (`RuntimeInitializeOnLoadMethod BeforeSceneLoad`)
  tek bir DontDestroyOnLoad `GameAudio` nesnesi kurar: `GameAudioRuntime` (kaynaklar, fade) + `AudioAutoWiring`.
- `AudioAutoWiring` sahne yüklenince, bir kare sonra ve 1,5 sn'de bir (sonradan kurulan UI için) sahneyi tarar,
  oyunun **public** olay/özellik/View setter'larına bağlanır. Mevcut oyun kodu değiştirilmedi; private alana
  reflection ile erişilmez. Bileşen yoksa atlanır.
- Kaynak: `Assets/Resources/Audio/{Music,Ambience,SFX}/`, ID = dosya adı. `Resources/Audio/Overrides/<ID>` varsa önceliklidir.
- Seviyeler: müzik 0.5, ortam 0.4, efekt 0.8 (`GameAudio.MusicLevel/AmbienceLevel/SfxLevel`); çağrılardaki ses değeri
  bu seviyeye görelidir. Ana ses yalnız `AudioListener.volume` (menü Ayarlar > Ses, PlayerPrefs `AfetUyanisi.Volume`).
- Müzik ve ortam ayrı kanallarda 0,8 sn crossfade yapar; sahne geçişinde aynı parça kesilmeden sürer.
  Kısa efektler ve alarm/telefon/gürleme döngüleri sahne değişince susar.
- Sahnede etkin AudioListener yoksa (Bedroom, Bathroom) GameAudio kendi dinleyicisini açar.

```csharp
GameAudio.PlaySfx("SFX_UI_Click", 0.5f);
GameAudio.PlayMusic("MUS_Calm_Loop");
GameAudio.PlayAmbience("AMB_Rain_Heavy_Loop");
GameAudio.PlayLoop("SFX_Alarm_Clock", "alarm", 0.55f);  GameAudio.StopLoop("alarm");
```

## Hangi ses nerede

| ID | Nerede / kanca |
|---|---|
| MUS_Menu_Loop | MainMenu (alçak, ~0.35; menünün kendi oda/yağmur/gürleme efektleri olduğu için ortam kanalı menüde boş) |
| MUS_Calm_Loop | Bedroom, Bathroom, FloodChapter; EarthquakeHome Intro / After / Report |
| MUS_Tension_Loop | EarthquakeModeController.Phase = Prep (120 sn sayaç); Quake'te kısık |
| AMB_Room_Morning_Loop | Bedroom, Bathroom, FloodChapter iç mekân, EarthquakeHome (sarsıntıda susar) |
| AMB_Rain_Light_Loop / Heavy | RainController.IsRaining + Intensity / OnDownpourStarted; otobüs içi (BusWindowScroller etkin) hafif yağmur |
| SFX_Alarm_Clock | AlarmClock.IsRinging (döngü) |
| SFX_Phone_Ring | PhoneAlertController.IsRinging (döngü; resmi alarm tonu değil, iki notalı zil) |
| SFX_Phone_Notify | PhoneAlertController.View sarmalayıcısı (AudioPhoneAlertView) → bildirim açılırken |
| SFX_TV_NewsJingle | Mutfaktaki "TV" nesnesinin klipsiz AudioSource'una atanır; FloodChapterController.PlayTvJingle çalar |
| SFX_Water_Splash | PuddleHazard.Triggered (0,4 sn tekrar sınırı) |
| SFX_Bus_Arrive / Door / Depart | BusController.OnArriving / OnDoorsOpened+OnDoorsClosed / OnDeparting |
| SFX_Quake_Rumble (+Rattle) | EarthquakeEventController.IsShaking (döngü + başlangıç) |
| SFX_Books_Fall / SFX_Glass_Crack | Sarsıntı başladıktan 1,5 / 2,3 sn sonra (tek tek düşme için public olay yok) |
| SFX_Countdown_Tick | EmergencyCountdownManager son 10 sn, doğal saniye geçişi (duraklatma/sıçramada yok) |
| SFX_Penalty | EmergencyCountdownManager.OnPenalty |
| SFX_Correct / SFX_Wrong | Deprem: TimedDecisionController.View sarmalayıcısı (AudioTimedQuestionView.ShowFeedback; ceza varsa yalnız ceza sesi). Sel: karar kartında ✓/✗ göründüğü an (DecisionChoiceView). ObjectiveHudView.OnWarned/OnLesson → Wrong |
| SFX_Objective_Complete | ObjectiveHudView.OnObjectiveCompleted (Sel/odalar); PreparationQuestManager.TaskCompleted yalnız PreparationQuestHUD yoksa (HUD kendi sesini çalıyor) |
| SFX_Objective_Bonus | ObjectiveHudView.OnBonusCompleted; EmergencyCountdownManager.OnBonus |
| SFX_Badge | ObjectiveHudView.OnGroupCompleted; FloodResultView.IsShown; Deprem Phase → Report |
| SFX_UI_Hover / Click / Back | Tüm UI Button'lar (AudioUiSoundHooks: fare üstüne gelme / klavyeyle seçilme → Hover, onClick → Click; adında "back"/"geri" geçen → Back), EqButton.HoverChanged/Clicked; MainMenu'de Esc → Back, ses ayarı (AudioListener.volume) değişince Click. Menüye özel sınıflara bağlı değil |
| Ocak (StoveFireController) | State On → TurningOff: SFX_UI_Click (düğme); görev sesi PreparationQuestHUD'dan |
| Denge (ProtectionMinigameController.Balance) | Kırmızı bölgeye girince SFX_Wrong (nazik, en çok 1/sn), alıştırma bitince SFX_Correct |
| SFX_Footstep_* | **Bağlanmadı** (ayak basma olayı yok) |

Not: PreparationQuestHUD (alarm/tamamlama/uyarı bipleri) ve mini oyunlar (afet çantası, kitaplık) kendi seslerini çalıyor; dokunulmadı.

## Ücretsiz kaynaklar

17 Ekim 2026'da yayıncının resmi sayfasından indirildi; paket içi License.txt `Licenses/` altında.

- [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds), CC0-1.0
- [Kenney Impact Sounds](https://kenney.nl/assets/impact-sounds), CC0-1.0

| Override | Kaynak |
|---|---|
| Overrides/SFX_UI_Click.ogg | Interface Sounds: Audio/click_002.ogg |
| Overrides/SFX_UI_Hover.ogg | Interface Sounds: Audio/select_001.ogg |
| Overrides/SFX_UI_Back.ogg | Interface Sounds: Audio/back_001.ogg |
| Overrides/SFX_Correct.ogg | Interface Sounds: Audio/confirmation_001.ogg |
| Overrides/SFX_Wrong.ogg | Interface Sounds: Audio/error_001.ogg |
| Overrides/SFX_Books_Fall.ogg | Impact Sounds: Audio/impactWood_light_000.ogg |
| Overrides/SFX_Glass_Crack.ogg | Impact Sounds: Audio/impactGlass_light_000.ogg |

Tam kaynak eşlemesi ve SHA256: `tools/audio/cc0_sources.json`.

## Yeniden üretim

`node tools/audio/generate_audio.js` 34 WAV'ı yeniden üretir (OGG, .meta ve bu belgeye dokunmaz);
`--check` değiştirmeden denetler.
