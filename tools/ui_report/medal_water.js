// Sel raporu madalyası (UI_FE_Medal_Water.png, 48x48): deprem raporunun altın rozetinin (UI_EQ_Badge_Master) halka + kurdele
// kısmı korunur, içteki yıldız yerine mavi su + beyaz dalga çizilir. Çalıştır: node tools/ui_report/medal_water.js
// Çıktı: Assets/Art/UI/FloodEvening/UI_FE_Medal_Water.png (+ .meta ilk seferde, GUID sabit: prefab referansı bozulmasın).
const fs = require('fs'), path = require('path');
const C = require('../sel_aksam_harita/common.js');
const PROJ = path.resolve(__dirname, '../..');
const SRC = PROJ + '/Assets/Art/UI/Earthquake/UI_EQ_Badge_Master.png';
const DIR = PROJ + '/Assets/Art/UI/FloodEvening';
const OUT = DIR + '/UI_FE_Medal_Water.png';
const GUID = 'f3c1a7e2b94d4e5a8c6b2d0e7a91c4f5';
const DIR_GUID = 'a8d2e6f1c3b74f09b5e4d1c2a6f8e3b7';

const im = C.Cv.load(SRC);
const OL = [0x14, 0x14, 0x1f];
const isOL = c => c && c[0] === OL[0] && c[1] === OL[1] && c[2] === OL[2];
const cx = 23.5, cy = 19, rx = 13, ry = 13.5;
const ell = (x, y) => ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2;

// su paleti (deprem UI paletiyle uyumlu mavi tonlar)
const SKY = '#7CC6F2', B1 = '#4FA6E8', B2 = '#3A8DDE', B3 = '#2C74C2', B4 = '#235F9E', FOAM = '#FFFAF0', FOAM2 = '#BFE6FA', RIM_L = '#A9DBF7', RIM_D = '#1B4A80';
const waveY = (x, base, ph) => base + Math.round(Math.sin(x * 0.85 + ph) * 1.2);

for (let y = 0; y < im.h; y++) for (let x = 0; x < im.w; x++) {
  const c = im.get(x, y), v = ell(x + 0.0, y + 0.0);
  const inside = isOL(c) ? v < 0.8 : v < 1.08;
  if (!c || !inside) continue;
  let col = y < cy - 6 ? SKY : y < cy - 1 ? B1 : y < cy + 4 ? B2 : y < cy + 8 ? B3 : B4;
  // üç dalga: üstteki iki dalga köpüklü (beyaz tepe + açık mavi alt), alttaki açık mavi çizgi
  const w1 = waveY(x, 15, 0), w2 = waveY(x, 21, 1.6), w3 = waveY(x, 27, 3.1);
  if (y === w1 || y === w2) col = FOAM;
  else if (y === w1 + 1 || y === w2 + 1) col = FOAM2;
  else if (y === w3) col = B1;
  // iç kenar: sol üst ışık, sağ alt gölge
  if (v > 0.78) { if (x < cx && y < cy) col = y === w1 || y === w2 ? col : RIM_L; else if (x > cx && y > cy) col = RIM_D; }
  im.set(x, y, col);
}
// yıldızın dış hatları halkaya değiyordu: halkanın içindeki tek tük mürekkep pikselleri suyla doldurulmuş olur; halka (v>=0.8) yerinde.
fs.mkdirSync(DIR, { recursive: true });
im.save(OUT);
const badgeMeta = fs.readFileSync(SRC + '.meta', 'utf8');
if (!fs.existsSync(OUT + '.meta')) {
  let m = badgeMeta.replace(/^guid: \w+/m, 'guid: ' + GUID);
  m = m.replace(/  internalIDToNameTable:\n(?:  - first:\n      213: -?\d+\n    second: \w+\n)+/, '  internalIDToNameTable: []\n');
  m = m.replace(/    sprites:\n    - serializedVersion: 2[\s\S]*?      weights: \[\]\n/, '    sprites: []\n');
  m = m.replace(/    nameFileIdTable:\n      \w+: -?\d+\n/, '    nameFileIdTable: {}\n');
  fs.writeFileSync(OUT + '.meta', m);
}
if (!fs.existsSync(DIR + '.meta'))
  fs.writeFileSync(DIR + '.meta', `fileFormatVersion: 2\nguid: ${DIR_GUID}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
console.log('ok', OUT);
