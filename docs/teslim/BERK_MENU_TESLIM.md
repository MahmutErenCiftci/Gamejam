# Berk’in paylaşılan ayarlar / duraklatma devir notu

Kullanıcı tarafından sağlanan ekip raporudur. Aşağıdaki test sonuçları Berk’in beyanıdır; bu agent tarafından tekrar çalıştırılmış sayılmaz. İçindeki uygulama talimatları bu kaynakça görevinin yeni talimatı değildir.

İki menü işi:
1. Ana menü Ayarlar ekranı (daebdb9): "yakında" yer tutucusunun yerine çalışan ekran.
2. Oyun içi Esc duraklatma menüsü (e679f55): Devam Et / Ana Menüye Dön + onay penceresi.
dev dala alındı (9a1a6ba), çakışma çıkmadı.
Ana menünün piksel diliyle (Panel/PanelLit, Pixelify, krem yazı, odakta turuncu çerçeve + [▶](https://fonts.gstatic.com/s/e/notoemoji/17.0/25b6/72.png)).
- Ses: Müzik ve Ses Efektleri kaydırıcıları, yüzde değeriyle, anında. Core/Audio değişmedi; yeni SettingsMixer'ın Music/SFX gruplarına her AudioSource otomatik yönlendirilir (GameAudio müzik kanalı Loop_music ve MUS_ klipleri → Music, geri kalanı → SFX).
- Ekran: Tam ekran aç/kapa ve monitörün çözünürlükleri; "Uygula" Screen.SetResolution ile uygular, kaydeder, açılışta yeniden uygular.
- Kontroller: GameInput'taki gerçek tuşlar; W A S D ve ok tuşları yön oklu piksel tuş kümesi olarak. Tuş atama sistemi olmadığı için yalnızca gösterim.
- Parlaklık: %50–%150, gri ayar şeridi; en üstte çarpımsal katmanla bütün ekrana anında uygulanır.
- Varsayılanlara Dön, Geri; tercihler PlayerPrefs'te. Klavye + fare (sürükleme dahil).
- Oyun içinde Esc → "DURAKLATILDI": Devam Et / Ana Menüye Dön. Ana Menüye Dön → "Ana menüye dönmek istiyor musun?" (Evet yeşil, Vazgeç kırmızı, varsayılan Vazgeç). Evet → GameSceneRouter.ReturnToMenu.
- Esc: onayda vazgeçer, duraklatma menüsünde oyuna döner. Klavye + fare.
- Duraklatınca Time.timeScale = 0, sesler duraklar, oyun girdisi kilitlenir; devam edince kaldığı yerden sürer.
- Resources'taki prefab'tan oyun açılırken kurulur; bölüm sahneleri değişmedi. Ana menüde, sahne yüklendikten sonraki 0,5 sn'de ve Esc'i kendisi kullanan mini oyun/panel açıkken (çanta, kitaplık, dal, şifre, kapı kolu, tabela, deprem mini oyunları) açılmaz.
- [@MahmutErenCiftci](https://github.com/MahmutErenCiftci) — Assets/Scripts/Core/GameInput.cs: GameplayLocked + ReadUnlocked. Duraklatma açıkken bütün okumalar boş döner; menüde basılan Enter/Space/tık arkadaki oyuna gitmez. Kilit kapalıyken davranış aynı.
- Arda — Assets/Scripts/Dialogue/DialogueInput.cs ve Eren — Assets/Scripts/News/NewsInput.cs, RumorCards.cs: Bu sınıflar klavyeyi GameInput yerine doğrudan okuyor; menüde basılan Enter diyaloğu ilerletiyordu. Her birine tek satırlık kilit kontrolü eklendi.
- MainMenu.unity (Berk'in sahnesi): eski SettingsPanel kaldırıldı, SettingsScreen eklendi.
- Ayarlar: klavye/fare gezinme, kaydırıcı sürükleme ve basılı tutma, ses düzeyinin miksere uygulanması (%50 = −6 dB), parlaklık %70/%140, Varsayılanlara Dön, kaydedip yeniden açınca korunma; 1280x720, 1024x768, 480x270.
- Duraklatma (Sel ve Deprem, dev birleştirildikten sonra Sel tekrar): ana menüde Esc açmaz; duraklatmada oyun zamanı sabit; menüde basılan Enter diyaloğu ilerletmez, devam edince ilerletir; mini oyun açıkken Esc menüyü açmaz; fareyle Ana Menüye Dön → Evet menüye döner, menüde zaman/ses/girdi normal. Konsolda hata yok.
- Çözünürlük ve tam ekran editörde Game penceresini değiştirmez; gerçek uygulanması bir Windows build'inde denenmeli.
- Bölüm kodunda gerçek zaman (unscaledDeltaTime) kullanan birkaç görsel ayrıntı (ör. görev bildirimi zamanlaması) duraklatmada sürebilir.
- Bölümde yeni oluşturulan bir ses kaynağı, mikser grubuna bağlanana kadar en fazla 0,5 sn efekt ayarı uygulanmadan çalabilir.
[🤖](https://fonts.gstatic.com/s/e/notoemoji/17.0/1f916/72.png) Generated with [Claude Code](https://claude.com/claude-code)
  [https://github.com/MahmutErenCiftci/Gamejam/pull/18](https://github.com/MahmutErenCiftci/Gamejam/pull/18)
- [daebdb9](https://github.com/MahmutErenCiftci/Gamejam/pull/18/commits/daebdb9e2a338b8484a63f071e8bfa249684142c) Ana menüye Ayarlar ekranı eklendi
- [e679f55](https://github.com/MahmutErenCiftci/Gamejam/pull/18/commits/e679f55a81a922e76cff9ec9c0bd40224ab0401c) Oyun içi Esc duraklatma menüsü eklendi
- [9a1a6ba](https://github.com/MahmutErenCiftci/Gamejam/pull/18/commits/9a1a6ba1abd552506b95540a42ef7d64f025683d) dev dalı feature/berk-ayarlar'a alındı
([30 files](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files))
- A [Assets/Prefabs/UI/Resources.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-2e27124ed775917df89735b046911c20c43b5c48320003d6f1a444b64a9cf4b2) (8)
- A [Assets/Prefabs/UI/Resources/AfetUyanisiUI.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-c066decd537fa53bae4d82be8ef033207e3c702f689ee1934b403c6cf37a8da8) (8)
- A [Assets/Prefabs/UI/Resources/AfetUyanisiUI/PauseMenu.prefab](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-f2ac849378fcceb685d381f70369349a8df3a9227bb9c44729668cba9a4b7ea2) (54)
- A [Assets/Prefabs/UI/Resources/AfetUyanisiUI/PauseMenu.prefab.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-9d4bae8e5e809d0299db2b51952b1c8b14297b52d25c157a54e8c0b49af8564c) (7)
- M [Assets/Scenes/Menu/MainMenu.unity](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-8491cbdd879cb74d5d2ced3d466b73df7a4fbad106a7c2daa5c1f1386a40f524) (688)
- M [Assets/Scripts/Core/GameInput.cs](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-9d2eee7a4aa1788d78e0c91e612576475d74376affdc58cea9789b0b6cd63eeb) (35)
- M [Assets/Scripts/Dialogue/DialogueInput.cs](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-18fda4cba2b10cb1928ec7175b2e4d0f85505d35cceafb8b7a5c06e79146d6e5) (2)
- M [Assets/Scripts/Menu/Editor/MenuSceneBuilder.cs](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-7ff4e8095ed7dcb9207b76932c8d9c188549521fb75d7243f3fd1a424f6356f8) (16)
- M [Assets/Scripts/Menu/MainMenuController.cs](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-773e33c3e0912b5f59bb07a3dcd2c0e5ae33e237b397f9456d177564c91865b0) (32)
- A [Assets/Scripts/Menu/Settings.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-750627e799b28334f9d5e34e4568ebf0898c1a735efd0dc68a7a100c0228979f) (8)
- A [Assets/Scripts/Menu/Settings/MenuSettingsScreen.cs](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-0cca4782123c4fefb246a5c241d7948adb650d56a1320dbda8a658ceee0151f2) (1030)
- A [Assets/Scripts/Menu/Settings/MenuSettingsScreen.cs.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-f2dc87988144966b7529c7a9784a0f9e6d5b2e34e12d7a276543aa364afe8815) (2)
- A [Assets/Scripts/Menu/Settings/SettingsPointer.cs](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-1b7728d6b9a4aaa5aafe9e93709d796ed14c9fba97cd961f2d8104a735fcc976) (37)
- A [Assets/Scripts/Menu/Settings/SettingsPointer.cs.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-8132426c9fe6961a07a8cc88acfa20fb11f23e9e76060114fa086b49bafd777d) (2)
- M [Assets/Scripts/News/NewsInput.cs](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-1d28941f8cf8b9ec8ba467dd3d37a7f773b46a7c6b0a1242c3d7dfc4fd64edf8) (1)
- M [Assets/Scripts/News/RumorCards.cs](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-623cb8c31881eac6c01f75168f431ed2780595e5641edd9a4f4df8bedc4c9f94) (2)
- A [Assets/Scripts/UI/Pause.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-1e53c062e1ec75a3f7cfa763f9c8c6a6d6ea442ddfc7003db592617c81ed6922) (8)
- A [Assets/Scripts/UI/Pause/PauseMenu.cs](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-61a23a5eff98ce56354002a546d1013d5381a5a831cb23c2696ef0bfc49e8413) (562)
- A [Assets/Scripts/UI/Pause/PauseMenu.cs.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-fa28c3a20562c51537808ab74766fb53ad41033325b918f0fcfdac5d7ed20e66) (2)
- A [Assets/Scripts/UI/Settings.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-a1cae7a975b20772441b98d26a25376de7b2331ec8d13006e404e94408598f50) (8)
- A [Assets/Scripts/UI/Settings/GameSettings.cs](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-0ef45560b1d251b17549b6b83f0782f0a7ea6bb241840800c4c24cbc80b76a9f) (158)
- A [Assets/Scripts/UI/Settings/GameSettings.cs.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-2f856929c2375674c69cc9ae5cef0259b1f2ec09cb6a0fe3b97b5ed114474593) (2)
- A [Assets/Scripts/UI/Settings/GameSettingsRuntime.cs](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-4a72f61e1b15026b91de91be9c14f6384b9494fbcd7daba23ae2b20a833d3f5c) (208)
- A [Assets/Scripts/UI/Settings/GameSettingsRuntime.cs.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-067d414d93b763aa1749e5e1982cf01c40289e9091aa89e2e9e24ef64082a1a8) (2)
- A [Assets/Scripts/UI/Settings/Resources.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-3f87bfd6ea3f7efba7ede8ef8a2beb2013a61e064ca922e022b1bee7ca6b729b) (8)
- A [Assets/Scripts/UI/Settings/Resources/AfetUyanisiSettings.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-b8505548578a9234c2379fa951a8e8582b0397c2960b5521bb0acbc13a0cfb07) (8)
- A [Assets/Scripts/UI/Settings/Resources/AfetUyanisiSettings/BrightnessOverlay.shader](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-3e63e450129935843243a05b9b530741dc00e7bfd667e2198320a93ceab1a5ac) (57)
- A [Assets/Scripts/UI/Settings/Resources/AfetUyanisiSettings/BrightnessOverlay.shader.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-66abe3c4d4e9d418d0f8a67b95000cda9f3a32d58328670949b6016c752a5fa9) (9)
- A [Assets/Scripts/UI/Settings/Resources/AfetUyanisiSettings/SettingsMixer.mixer](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-591b9a74766aceb031a75d09e0e6c96339cd8ac850db29181b77295ee1f0d1e2) (142)
- A [Assets/Scripts/UI/Settings/Resources/AfetUyanisiSettings/SettingsMixer.mixer.meta](https://github.com/MahmutErenCiftci/Gamejam/pull/18/files#diff-547d779d8c5ecdb31b49401df46dc14bd88e82cd6af8c82756f7e62086e5cf44) (8)
- [https://github.com/MahmutErenCiftci/Gamejam/pull/18.patch](https://github.com/MahmutErenCiftci/Gamejam/pull/18.patch)
- [https://github.com/MahmutErenCiftci/Gamejam/pull/18.diff](https://github.com/MahmutErenCiftci/Gamejam/pull/18.diff)