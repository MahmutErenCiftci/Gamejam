# Akşam sel haritası üretici (EveningNeighborhood)

`Assets/Prefabs/FloodEvening/EveningNeighborhood.prefab` bu klasördeki ızgara tanımından üretilir. Prefab'ı elle düzenlemeyin; bir sonraki kurulumda üzerine yazılır. Sahip: Mahmut.

| Dosya | Ne yapar |
|---|---|
| `map.js` | Yürünebilir koridorlar (A, B, C, FORK, D, E, F, G, H, I, J, PLAZA), su, zemin türleri, duvar/korkuluk hatları |
| `layout.js` | Evler, çitler, ağaçlar, lambalar, tabelalar, yıkıntı; çapalar (`Spawn`, `Fork`, `TreeStopSpot`, `BranchSpot`, `SafeZone`...) ve trigger alanları |
| `terrain.js` | Zemin parçaları + su kademeleri + yürüme sınırı (WalkBounds) → `out/` |
| `props.js` | Yeni sprite'lar (tabelalar, yıkıntı, duvar hatları, gökyüzü) → `out/props` |
| `install.js` | PNG + .meta'ları `Assets/Art/FloodEvening/ENV`'e kopyalar (`--png`), `build_data.txt` yazar |
| `preview.js` / `sheet.js` | Unity'siz kamera önizlemesi: `node preview.js etiket 1 gx,gy ...` → `shots/` |
| `check.js` | Bir binanın arkadaki yolu örtüp örtmediğini denetler |
| `cs/build.cs` | (Unity MCP) prefab'ı `build_data.txt`'den temiz kurar |
| `cs/build_scene.cs` | (Unity MCP) `FloodEvening.unity`'yi baştan kurar; `FE_TreeFall.prefab` varsa `TreeStopSpot`'a koyar |

Sıra: `node terrain.js && node props.js && node install.js --png` → Unity'de `cs/build.cs` → `cs/build_scene.cs`.
Izgara → dünya: `x = (gx - gy) / 2`, `y = (gx + gy) / 4`.
