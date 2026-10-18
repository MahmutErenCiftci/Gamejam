# Berk — görsel kaynakları ve katkı kaydı

Kullanıcı `Berk_Gorseller.zip` dosyasını sağladı. **127 PNG ve 5 metin/lisans kaydı**, toplam 132 dosya orijinal byte içerikleri korunarak `berk_gorseller/` altına eklendi. 132 dosyanın arşiv/kopya SHA256’ları eşleşti.

[Önizlemeli galeri](BERK_GORSELLER_GALERI.html) · [Tam dosya ve hash kayıtları](BERK_ARSIV_KAYITLARI.json) · [Berk’in özgün kaynakça notu](berk_gorseller/Berk_Gorseller_Kaynakca/KAYNAKCA.txt)

## Kaynak ayrımı

- **ui_kit.png:** Berk’in notu commit kaydına dayanarak Codex üretimi olduğunu belirtiyor. Düşünce balonlarının kaynak sayfası; kaynak görselin elle çizildiği iddia edilmiyor.
- **bubble_quake_128.png / bubble_flood_128.png:** Kaynağın commitlerde yazmadığı açıkça belirtilmiş. Başka bir hizmete/üreticiye tahminen atfedilmeyecek. Menüde kullanılan temizlenmiş türevler ve glow katmanları ayrı dosyalar.
- **furniture / room_decor / boy_front_frames / eski logo:** Berk bunları artık kullanılmayan eski kaynaklar olarak ayırmış. Public repo içinde var olmaları lisans kontrolü gerekliliğini ortadan kaldırmaz.
- **Kodla çizilen 103 PNG:** Menü şehir/efekt katmanları ve sel açılış kapağı. Üretim kayıtları `Assets/Scripts/Menu/Editor/MenuArtBuilder*.cs` ve `Assets/Art/UI/FloodTitle/Tools~/make_flood_title.py`. Kodla üretim, AI desteği beyanından muaf değildir; görev/Claude kayıtları da açıklanır.
- **Mahmut’tan alınan ekip görselleri (11):** EŞİK logo ve deprem UI kaynakları; Berk temel görsel üreticisi olarak gösterilmez.
- **4 font metni:** Pixelify Sans OFL, Pixel Operator CC0/README ve Press Start 2P OFL.

## Ayarlar ve duraklatma katkısı

Berk’in paylaştığı PR #18 devir notu, ana menü ayarları ve Esc duraklatma menüsünü açıklıyor. Ses, ekran, parlaklık, klavye/fare ve zaman/girdi kilidi alanlarında ekip testleri bildirilmiş; çözünürlük/tam ekranın Windows buildinde ayrıca denenmesi gerektiği yazıyor. Bunlar **Berk’in bildirdiği testlerdir**, bu agent’ın gerçekleştirdiği testler olarak kaydedilmedi.

[PR #18](https://github.com/MahmutErenCiftci/Gamejam/pull/18) · [Paylaşılan devir notu](BERK_MENU_TESLIM.md)

## Dosyalar

| ID | Dosya | Kaynak grubu | Repo ile birebir eşleşme |
| --- | --- | --- | --- |
| BERK-001 | [KAYNAKCA.txt](berk_gorseller/Berk_Gorseller_Kaynakca/KAYNAKCA.txt) | KAYNAKCA.txt | Eşleşme yok / metin kaydı |
| BERK-002 | [bubble_flood_128.png](berk_gorseller/Berk_Gorseller_Kaynakca/1_Hazir_Kaynak_Gorseller/bubble_flood_128.png) | 1_Hazir_Kaynak_Gorseller | Assets/Art/Menu/Source~/bubble_flood_128.png |
| BERK-003 | [bubble_quake_128.png](berk_gorseller/Berk_Gorseller_Kaynakca/1_Hazir_Kaynak_Gorseller/bubble_quake_128.png) | 1_Hazir_Kaynak_Gorseller | Assets/Art/Menu/Source~/bubble_quake_128.png |
| BERK-004 | [ui_kit.png](berk_gorseller/Berk_Gorseller_Kaynakca/1_Hazir_Kaynak_Gorseller/ui_kit.png) | 1_Hazir_Kaynak_Gorseller | Assets/Art/Menu/Source~/ui_kit.png |
| BERK-005 | [boy_front_frames.png](berk_gorseller/Berk_Gorseller_Kaynakca/1_Hazir_Kaynak_Gorseller/kullanilmayan/boy_front_frames.png) | 1_Hazir_Kaynak_Gorseller | Assets/Art/Menu/Source~/boy_front_frames.png |
| BERK-006 | [furniture.png](berk_gorseller/Berk_Gorseller_Kaynakca/1_Hazir_Kaynak_Gorseller/kullanilmayan/furniture.png) | 1_Hazir_Kaynak_Gorseller | Assets/Art/Menu/Source~/furniture.png |
| BERK-007 | [logo.png](berk_gorseller/Berk_Gorseller_Kaynakca/1_Hazir_Kaynak_Gorseller/kullanilmayan/logo.png) | 1_Hazir_Kaynak_Gorseller | Assets/Art/Menu/Source~/logo.png |
| BERK-008 | [room_decor.png](berk_gorseller/Berk_Gorseller_Kaynakca/1_Hazir_Kaynak_Gorseller/kullanilmayan/room_decor.png) | 1_Hazir_Kaynak_Gorseller | Assets/Art/Menu/Source~/room_decor.png |
| BERK-009 | [BubbleFlood.png](berk_gorseller/Berk_Gorseller_Kaynakca/2_Oyundaki_Halleri/BubbleFlood.png) | 2_Oyundaki_Halleri | Assets/Art/Menu/Bubbles/BubbleFlood.png |
| BERK-010 | [BubbleFloodGlow.png](berk_gorseller/Berk_Gorseller_Kaynakca/2_Oyundaki_Halleri/BubbleFloodGlow.png) | 2_Oyundaki_Halleri | Assets/Art/Menu/Bubbles/BubbleFloodGlow.png |
| BERK-011 | [BubbleQuake.png](berk_gorseller/Berk_Gorseller_Kaynakca/2_Oyundaki_Halleri/BubbleQuake.png) | 2_Oyundaki_Halleri | Assets/Art/Menu/Bubbles/BubbleQuake.png |
| BERK-012 | [BubbleQuakeGlow.png](berk_gorseller/Berk_Gorseller_Kaynakca/2_Oyundaki_Halleri/BubbleQuakeGlow.png) | 2_Oyundaki_Halleri | Assets/Art/Menu/Bubbles/BubbleQuakeGlow.png |
| BERK-013 | [ThoughtPathFlood.png](berk_gorseller/Berk_Gorseller_Kaynakca/2_Oyundaki_Halleri/ThoughtPathFlood.png) | 2_Oyundaki_Halleri | Assets/Art/Menu/UI/ThoughtPathFlood.png |
| BERK-014 | [ThoughtPathQuake.png](berk_gorseller/Berk_Gorseller_Kaynakca/2_Oyundaki_Halleri/ThoughtPathQuake.png) | 2_Oyundaki_Halleri | Assets/Art/Menu/UI/ThoughtPathQuake.png |
| BERK-015 | [Apartment.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Apartment.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Apartment.png |
| BERK-016 | [Bird0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Bird0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Bird0.png |
| BERK-017 | [Bird1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Bird1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Bird1.png |
| BERK-018 | [City.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/City.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/City.png |
| BERK-019 | [CityCracks.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/CityCracks.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/CityCracks.png |
| BERK-020 | [CityLights0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/CityLights0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/CityLights0.png |
| BERK-021 | [CityLights1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/CityLights1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/CityLights1.png |
| BERK-022 | [CityLights2.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/CityLights2.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/CityLights2.png |
| BERK-023 | [CityLights3.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/CityLights3.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/CityLights3.png |
| BERK-024 | [CityRuins.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/CityRuins.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/CityRuins.png |
| BERK-025 | [ClockGlow.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/ClockGlow.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/ClockGlow.png |
| BERK-026 | [Dust.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Dust.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Dust.png |
| BERK-027 | [Flood0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Flood0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Flood0.png |
| BERK-028 | [Flood1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Flood1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Flood1.png |
| BERK-029 | [Flood2.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Flood2.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Flood2.png |
| BERK-030 | [Flood3.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Flood3.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Flood3.png |
| BERK-031 | [Flood4.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Flood4.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Flood4.png |
| BERK-032 | [Flood5.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Flood5.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Flood5.png |
| BERK-033 | [Flood6.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Flood6.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Flood6.png |
| BERK-034 | [Flood7.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Flood7.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Flood7.png |
| BERK-035 | [Flood8.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Flood8.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Flood8.png |
| BERK-036 | [Flood9.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Flood9.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Flood9.png |
| BERK-037 | [FloodWave0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/FloodWave0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/FloodWave0.png |
| BERK-038 | [FloodWave1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/FloodWave1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/FloodWave1.png |
| BERK-039 | [FloodWave2.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/FloodWave2.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/FloodWave2.png |
| BERK-040 | [Pixel.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Pixel.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Pixel.png |
| BERK-041 | [RainDrop.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/RainDrop.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/RainDrop.png |
| BERK-042 | [Rubble.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Rubble.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Rubble.png |
| BERK-043 | [ShadeLeft.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/ShadeLeft.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/ShadeLeft.png |
| BERK-044 | [WindowGlass.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/WindowGlass.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/WindowGlass.png |
| BERK-045 | [WindowStorm.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/WindowStorm.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/WindowStorm.png |
| BERK-046 | [ZBig.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/ZBig.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/ZBig.png |
| BERK-047 | [ZSmall.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/ZSmall.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/ZSmall.png |
| BERK-048 | [CollapseBottom0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseBottom0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseBottom0.png |
| BERK-049 | [CollapseBottom1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseBottom1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseBottom1.png |
| BERK-050 | [CollapseBottom10.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseBottom10.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseBottom10.png |
| BERK-051 | [CollapseBottom11.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseBottom11.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseBottom11.png |
| BERK-052 | [CollapseBottom2.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseBottom2.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseBottom2.png |
| BERK-053 | [CollapseBottom3.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseBottom3.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseBottom3.png |
| BERK-054 | [CollapseBottom4.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseBottom4.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseBottom4.png |
| BERK-055 | [CollapseBottom5.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseBottom5.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseBottom5.png |
| BERK-056 | [CollapseBottom6.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseBottom6.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseBottom6.png |
| BERK-057 | [CollapseBottom7.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseBottom7.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseBottom7.png |
| BERK-058 | [CollapseBottom8.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseBottom8.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseBottom8.png |
| BERK-059 | [CollapseBottom9.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseBottom9.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseBottom9.png |
| BERK-060 | [CollapseMask0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseMask0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseMask0.png |
| BERK-061 | [CollapseMask1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseMask1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseMask1.png |
| BERK-062 | [CollapseMask10.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseMask10.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseMask10.png |
| BERK-063 | [CollapseMask11.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseMask11.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseMask11.png |
| BERK-064 | [CollapseMask2.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseMask2.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseMask2.png |
| BERK-065 | [CollapseMask3.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseMask3.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseMask3.png |
| BERK-066 | [CollapseMask4.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseMask4.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseMask4.png |
| BERK-067 | [CollapseMask5.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseMask5.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseMask5.png |
| BERK-068 | [CollapseMask6.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseMask6.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseMask6.png |
| BERK-069 | [CollapseMask7.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseMask7.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseMask7.png |
| BERK-070 | [CollapseMask8.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseMask8.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseMask8.png |
| BERK-071 | [CollapseMask9.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseMask9.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseMask9.png |
| BERK-072 | [CollapseTop0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseTop0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseTop0.png |
| BERK-073 | [CollapseTop1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseTop1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseTop1.png |
| BERK-074 | [CollapseTop10.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseTop10.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseTop10.png |
| BERK-075 | [CollapseTop11.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseTop11.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseTop11.png |
| BERK-076 | [CollapseTop2.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseTop2.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseTop2.png |
| BERK-077 | [CollapseTop3.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseTop3.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseTop3.png |
| BERK-078 | [CollapseTop4.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseTop4.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseTop4.png |
| BERK-079 | [CollapseTop5.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseTop5.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseTop5.png |
| BERK-080 | [CollapseTop6.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseTop6.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseTop6.png |
| BERK-081 | [CollapseTop7.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseTop7.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseTop7.png |
| BERK-082 | [CollapseTop8.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseTop8.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseTop8.png |
| BERK-083 | [CollapseTop9.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Collapse/CollapseTop9.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Collapse/CollapseTop9.png |
| BERK-084 | [CarX0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/CarX0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/CarX0.png |
| BERK-085 | [CarX1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/CarX1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/CarX1.png |
| BERK-086 | [CarX2.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/CarX2.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/CarX2.png |
| BERK-087 | [CarX3.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/CarX3.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/CarX3.png |
| BERK-088 | [CarY0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/CarY0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/CarY0.png |
| BERK-089 | [CarY1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/CarY1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/CarY1.png |
| BERK-090 | [CarY2.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/CarY2.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/CarY2.png |
| BERK-091 | [CarY3.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/CarY3.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/CarY3.png |
| BERK-092 | [CarY4.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/CarY4.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/CarY4.png |
| BERK-093 | [Person0_0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/Person0_0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/Person0_0.png |
| BERK-094 | [Person0_1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/Person0_1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/Person0_1.png |
| BERK-095 | [Person1_0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/Person1_0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/Person1_0.png |
| BERK-096 | [Person1_1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/Person1_1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/Person1_1.png |
| BERK-097 | [Person2_0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/Person2_0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/Person2_0.png |
| BERK-098 | [Person2_1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/Person2_1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/Person2_1.png |
| BERK-099 | [Person3_0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/Person3_0.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/Person3_0.png |
| BERK-100 | [Person3_1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Fx/Street/Person3_1.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Fx/Street/Person3_1.png |
| BERK-101 | [TileBaseboard.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Tiles/TileBaseboard.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Tiles/TileBaseboard.png |
| BERK-102 | [TileFloor.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Tiles/TileFloor.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Tiles/TileFloor.png |
| BERK-103 | [TileWall.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Tiles/TileWall.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Tiles/TileWall.png |
| BERK-104 | [TileWallTop.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/Tiles/TileWallTop.png) | 3_Kodla_Cizilen | Assets/Art/Menu/Tiles/TileWallTop.png |
| BERK-105 | [Panel.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/UI/Panel.png) | 3_Kodla_Cizilen | Assets/Art/Menu/UI/Panel.png |
| BERK-106 | [PanelLit.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Menu_Sehir_ve_Efektler/UI/PanelLit.png) | 3_Kodla_Cizilen | Assets/Art/Menu/UI/PanelLit.png |
| BERK-107 | [FT_Background.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Sel_Kapak_EveDonus/FT_Background.png) | 3_Kodla_Cizilen | Assets/Art/UI/FloodTitle/FT_Background.png |
| BERK-108 | [FT_Child.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Sel_Kapak_EveDonus/FT_Child.png) | 3_Kodla_Cizilen | Assets/Art/UI/FloodTitle/FT_Child.png |
| BERK-109 | [FT_Flash.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Sel_Kapak_EveDonus/FT_Flash.png) | 3_Kodla_Cizilen | Assets/Art/UI/FloodTitle/FT_Flash.png |
| BERK-110 | [FT_Puddles_0.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Sel_Kapak_EveDonus/FT_Puddles_0.png) | 3_Kodla_Cizilen | Assets/Art/UI/FloodTitle/FT_Puddles_0.png |
| BERK-111 | [FT_Puddles_1.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Sel_Kapak_EveDonus/FT_Puddles_1.png) | 3_Kodla_Cizilen | Assets/Art/UI/FloodTitle/FT_Puddles_1.png |
| BERK-112 | [FT_Puddles_2.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Sel_Kapak_EveDonus/FT_Puddles_2.png) | 3_Kodla_Cizilen | Assets/Art/UI/FloodTitle/FT_Puddles_2.png |
| BERK-113 | [FT_Puddles_3.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Sel_Kapak_EveDonus/FT_Puddles_3.png) | 3_Kodla_Cizilen | Assets/Art/UI/FloodTitle/FT_Puddles_3.png |
| BERK-114 | [FT_Puddles_4.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Sel_Kapak_EveDonus/FT_Puddles_4.png) | 3_Kodla_Cizilen | Assets/Art/UI/FloodTitle/FT_Puddles_4.png |
| BERK-115 | [FT_Puddles_5.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Sel_Kapak_EveDonus/FT_Puddles_5.png) | 3_Kodla_Cizilen | Assets/Art/UI/FloodTitle/FT_Puddles_5.png |
| BERK-116 | [FT_Puddles_6.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Sel_Kapak_EveDonus/FT_Puddles_6.png) | 3_Kodla_Cizilen | Assets/Art/UI/FloodTitle/FT_Puddles_6.png |
| BERK-117 | [FT_Puddles_7.png](berk_gorseller/Berk_Gorseller_Kaynakca/3_Kodla_Cizilen/Sel_Kapak_EveDonus/FT_Puddles_7.png) | 3_Kodla_Cizilen | Assets/Art/UI/FloodTitle/FT_Puddles_7.png |
| BERK-118 | [LogoStacked.png](berk_gorseller/Berk_Gorseller_Kaynakca/4_Ekip_Gorselleri_Mahmut/LogoStacked.png) | 4_Ekip_Gorselleri_Mahmut | Assets/Art/Menu/UI/LogoStacked.png |
| BERK-119 | [logo_esik.jpg](berk_gorseller/Berk_Gorseller_Kaynakca/4_Ekip_Gorselleri_Mahmut/logo_esik.jpg) | 4_Ekip_Gorselleri_Mahmut | Assets/Art/Menu/Source~/logo_esik.jpg |
| BERK-120 | [UI_EQ_HoldBar_Back.png](berk_gorseller/Berk_Gorseller_Kaynakca/4_Ekip_Gorselleri_Mahmut/UI_EQ_HoldBar_Back.png) | 4_Ekip_Gorselleri_Mahmut | Assets/Art/UI/Earthquake/UI_EQ_HoldBar_Back.png |
| BERK-121 | [UI_EQ_HoldBar_Fill.png](berk_gorseller/Berk_Gorseller_Kaynakca/4_Ekip_Gorselleri_Mahmut/UI_EQ_HoldBar_Fill.png) | 4_Ekip_Gorselleri_Mahmut | Assets/Art/UI/Earthquake/UI_EQ_HoldBar_Fill.png |
| BERK-122 | [UI_EQ_NamePlate.png](berk_gorseller/Berk_Gorseller_Kaynakca/4_Ekip_Gorselleri_Mahmut/UI_EQ_NamePlate.png) | 4_Ekip_Gorselleri_Mahmut | Assets/Art/UI/Earthquake/UI_EQ_NamePlate.png |
| BERK-123 | [UI_EQ_PhoneNotifCard.png](berk_gorseller/Berk_Gorseller_Kaynakca/4_Ekip_Gorselleri_Mahmut/UI_EQ_PhoneNotifCard.png) | 4_Ekip_Gorselleri_Mahmut | Assets/Art/UI/Earthquake/UI_EQ_PhoneNotifCard.png |
| BERK-124 | [UI_EQ_QuestBanner.png](berk_gorseller/Berk_Gorseller_Kaynakca/4_Ekip_Gorselleri_Mahmut/UI_EQ_QuestBanner.png) | 4_Ekip_Gorselleri_Mahmut | Assets/Art/UI/Earthquake/QuestHUD/UI_EQ_QuestBanner.png |
| BERK-125 | [UI_EQ_QuestPanel.png](berk_gorseller/Berk_Gorseller_Kaynakca/4_Ekip_Gorselleri_Mahmut/UI_EQ_QuestPanel.png) | 4_Ekip_Gorselleri_Mahmut | Assets/Art/UI/Earthquake/QuestHUD/UI_EQ_QuestPanel.png |
| BERK-126 | [UI_EQ_QuestToast.png](berk_gorseller/Berk_Gorseller_Kaynakca/4_Ekip_Gorselleri_Mahmut/UI_EQ_QuestToast.png) | 4_Ekip_Gorselleri_Mahmut | Assets/Art/UI/Earthquake/QuestHUD/UI_EQ_QuestToast.png |
| BERK-127 | [UI_EQ_StepDot_Done.png](berk_gorseller/Berk_Gorseller_Kaynakca/4_Ekip_Gorselleri_Mahmut/UI_EQ_StepDot_Done.png) | 4_Ekip_Gorselleri_Mahmut | Assets/Art/UI/Earthquake/UI_EQ_StepDot_Done.png |
| BERK-128 | [UI_EQ_Toast.png](berk_gorseller/Berk_Gorseller_Kaynakca/4_Ekip_Gorselleri_Mahmut/UI_EQ_Toast.png) | 4_Ekip_Gorselleri_Mahmut | Assets/Art/UI/Earthquake/UI_EQ_Toast.png |
| BERK-129 | [PixelifySans_OFL.txt](berk_gorseller/Berk_Gorseller_Kaynakca/5_Yazi_Tipleri/PixelifySans_OFL.txt) | 5_Yazi_Tipleri | Eşleşme yok / metin kaydı |
| BERK-130 | [PixelOperator_CC0.txt](berk_gorseller/Berk_Gorseller_Kaynakca/5_Yazi_Tipleri/PixelOperator_CC0.txt) | 5_Yazi_Tipleri | Eşleşme yok / metin kaydı |
| BERK-131 | [PixelOperator_README.txt](berk_gorseller/Berk_Gorseller_Kaynakca/5_Yazi_Tipleri/PixelOperator_README.txt) | 5_Yazi_Tipleri | Eşleşme yok / metin kaydı |
| BERK-132 | [PressStart2P_OFL.txt](berk_gorseller/Berk_Gorseller_Kaynakca/5_Yazi_Tipleri/PressStart2P_OFL.txt) | 5_Yazi_Tipleri | Eşleşme yok / metin kaydı |
