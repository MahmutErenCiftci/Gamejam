// OYUN SONU arka planı (320x180 pixel art, 640x360 referans arayüzde 2x): yağmur sonrası şafak, solda Cenk'in apartmanı
// (sel bölümü: eve güvenle varış), sağda yeşil "TOPLANMA ALANI" tabelalı park (deprem bölümü), ortada karakterler için boş kaldırım.
// Arayüz yerleşimi (GameEndScreen): başlık paneli art y 6..33 (x 55..265), alt panel art y 111..168 → sahnenin görünen bandı y 33..111.
// Kullanım: node tools/game_end/gen_bg.js  → Assets/Art/GameEnd/GAMEEND_Background.png (+ .meta, Point, sıkıştırmasız)
const C = require('../sel_aksam_harita/common.js');
const { Cv, mix, hash, OL, drawText, textW, metaText, guidFor, fs, path, PROJ } = C;

const W = 320, H = 180;
const cv = new Cv(W, H);
const BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]];
const dith = (x, y, t) => t * 16 > BAYER[y & 3][x & 3] + 0.5;

// ---------- gökyüzü: lacivert → mor → şeftali → amber (ufuk y=96), 4x4 Bayer geçişli bantlar
const SKY = [
  [0, '#0F1E33'], [18, '#16284A'], [34, '#22305A'], [48, '#353A68'], [60, '#53476F'],
  [70, '#7A5674'], [78, '#A8636D'], [85, '#D27D62'], [91, '#EC9A55'], [96, '#F6C67D']
];
function skyAt(y) {
  for (let i = 0; i < SKY.length - 1; i++) {
    const [y0, c0] = SKY[i], [y1, c1] = SKY[i + 1];
    if (y <= y1) return [c0, c1, (y - y0) / (y1 - y0)];
  }
  return [SKY[SKY.length - 1][1], SKY[SKY.length - 1][1], 0];
}
for (let y = 0; y < 100; y++) for (let x = 0; x < W; x++) {
  const [a, b, t] = skyAt(y);
  cv.set(x, y, dith(x, y, t) ? b : a);
}
// yıldızlar (yalnız üstte, sönmekte)
for (let i = 0; i < 46; i++) {
  const x = Math.floor(hash(i, 3) * W), y = Math.floor(hash(i, 7) * 44);
  cv.set(x, y, y < 24 ? '#F3ECDD' : '#9FB3C8');
  if (hash(i, 11) > 0.85 && y < 26) { cv.set(x - 1, y, '#5A6B8C'); cv.set(x + 1, y, '#5A6B8C'); cv.set(x, y - 1, '#5A6B8C'); cv.set(x, y + 1, '#5A6B8C'); }
}
// güneş (ufukta yarım disk + hale)
const SX = 160, SY = 91;
for (let y = 56; y < 100; y++) for (let x = 110; x < 210; x++) {
  const d = Math.hypot(x - SX, (y - SY) * 1.1);
  if (d < 13) cv.set(x, y, d < 10 ? '#FFF1C9' : '#FFE0A0');
  else if (d < 26 && dith(x, y, 1 - (d - 13) / 13) && y > 70) cv.set(x, y, mix(cv.get(x, y) ? C.toHex(cv.get(x, y).slice(0, 3)) : '#F6C67D', '#FFE0A0', 0.45));
}
// bulutlar: koyu mor gövde, alttan amber ışık
function cloud(cx, cy, w, h, seed) {
  for (let y = cy - h; y <= cy + 2; y++) for (let x = cx - w; x <= cx + w; x++) {
    const nx = (x - cx) / w, ny = (y - cy) / h;
    const r = nx * nx + ny * ny * 2.2 - (hash(x >> 2, y >> 1, seed) * 0.25);
    if (r > 1) continue;
    const under = y >= cy;
    cv.set(x, y, under ? (y === cy + 2 ? '#F2A33A' : '#E28A5E') : (r > 0.7 ? '#5E4A72' : '#4A4170'));
  }
}
cloud(70, 46, 34, 6, 1); cloud(120, 38, 22, 4, 2); cloud(250, 52, 40, 6, 3); cloud(296, 40, 18, 3, 4); cloud(195, 66, 20, 3, 5);

// ---------- uzak şehir silueti (y 78..100), seyrek sıcak pencereler
let x = 0, k = 0;
while (x < W) {
  const bw = 8 + Math.floor(hash(k, 21) * 14);
  const centre = x + bw > 128 && x < 192; // güneşin önü alçak kalsın
  const bh = centre ? 3 + Math.floor(hash(k, 22) * 5) : 6 + Math.floor(hash(k, 22) * 16);
  const top = 100 - bh;
  for (let yy = top; yy < 101; yy++) for (let xx = x; xx < Math.min(W, x + bw); xx++) cv.set(xx, yy, yy === top ? '#3A3A64' : '#2A2B4E');
  for (let yy = top + 3; yy < 98; yy += 3) for (let xx = x + 2; xx < x + bw - 1; xx += 3)
    if (hash(xx, yy, 9) > 0.78) cv.set(xx, yy, hash(xx, yy, 10) > 0.5 ? '#F6C67D' : '#D9915A');
  x += bw + (hash(k, 23) > 0.7 ? 2 : 0); k++;
}

// ---------- zemin: kaldırım (y 101..112) + cadde (altta, panel arkasında; y 168..180 görünür)
for (let y = 101; y < H; y++) for (let xx = 0; xx < W; xx++) {
  let c;
  if (y < 103) c = '#3B4A6E';                     // bordür üstü (ışık alan kenar)
  else if (y < 113) c = (xx + (y >> 1)) % 16 === 0 ? '#26324F' : '#2E3B5C'; // kaldırım taşları
  else if (y < 115) c = '#1A2238';                // bordür gölgesi
  else c = dith(xx, y, (y - 115) / 65) ? '#141B2E' : '#1A2238';
  cv.set(xx, y, c);
}
for (let xx = 0; xx < W; xx += 16) for (let y = 103; y < 113; y++) cv.set(xx, y, '#26324F');
// cadde şerit çizgisi (alt bant)
for (let xx = 4; xx < W; xx += 24) for (let d = 0; d < 12; d++) { cv.set(xx + d, 174, '#5A6B8C'); cv.set(xx + d, 175, '#46557A'); }
// ıslak kaldırımda güneş yansıması (küçük, sığ; derin su değil)
[[105, 9, '#E9A85C'], [107, 6, '#D9915A'], [109, 4, '#C98A55'], [111, 2, '#B07A4E']].forEach(([y, hw, c]) => { for (let xx = 160 - hw; xx <= 160 + hw; xx++) if ((xx + y) % 5) cv.set(xx, y, c); });

// ---------- sol: Cenk'in apartmanı (x 12..84, y 34..104)
function rect(x0, y0, w, h, c) { cv.rect(x0, y0, w, h, c); }
const AX = 12, AY = 36, AW = 72, AH = 66;
rect(AX - 1, AY - 1, AW + 2, AH + 2, OL);
rect(AX, AY, AW, AH, '#33456B');
rect(AX + AW - 10, AY, 10, AH, '#283959');      // gölge yüzü
rect(AX - 3, AY - 4, AW + 6, 4, OL); rect(AX - 2, AY - 3, AW + 4, 2, '#4A5D86'); // çatı saçağı
rect(AX + 8, AY - 9, 6, 5, OL); rect(AX + 9, AY - 8, 4, 4, '#4A5D86');          // baca
for (let r = 0; r < 4; r++) for (let c = 0; c < 4; c++) {
  const wx = AX + 6 + c * 15, wy = AY + 6 + r * 13;
  const lit = hash(r, c, 41) > 0.38;
  rect(wx - 1, wy - 1, 9, 9, OL);
  rect(wx, wy, 7, 7, lit ? '#F6C67D' : '#1C2740');
  if (lit) { rect(wx, wy + 4, 7, 3, '#F2A33A'); cv.set(wx + 1, wy + 1, '#FFF1C9'); }
  rect(wx + 3, wy, 1, 7, lit ? '#C9803A' : '#2A3654');                          // pencere kayıtı
  rect(wx - 1, wy + 8, 9, 1, '#4A5D86');                                       // denizlik
}
// giriş kapısı + saçak lambası (sıcak ışık)
rect(AX + 27, AY + AH - 16, 16, 16, OL);
rect(AX + 28, AY + AH - 15, 14, 15, '#6B4A3A'); rect(AX + 35, AY + AH - 15, 1, 15, '#4E3428');
rect(AX + 23, AY + AH - 19, 24, 3, OL); rect(AX + 24, AY + AH - 18, 22, 1, '#4A5D86');
cv.set(AX + 39, AY + AH - 8, '#F6C67D');
for (let y = AY + AH - 16; y < 104; y++) for (let xx = AX + 22; xx < AX + 50; xx++)
  if (dith(xx, y, 0.25) && !(xx >= AX + 27 && xx < AX + 43 && y < AY + AH)) { const c0 = cv.get(xx, y); if (c0) cv.set(xx, y, mix(C.toHex(c0.slice(0, 3)), '#F6C67D', 0.25)); }
rect(AX + 34, AY + AH - 21, 3, 2, '#FFE0A0');
// posta kutusu (apartman finali hatırası)
rect(AX + 50, AY + AH - 12, 8, 7, OL); rect(AX + 51, AY + AH - 11, 6, 5, '#3E7A9A'); rect(AX + 52, AY + AH - 9, 4, 1, '#14141F');

// ---------- sokak lambaları
function lamp(lx) {
  rect(lx, 70, 2, 33, OL); rect(lx, 70, 1, 33, '#4A5D86');
  rect(lx - 3, 67, 8, 3, OL); rect(lx - 2, 68, 6, 1, '#4A5D86');
  rect(lx - 1, 70, 4, 2, '#FFE0A0');
  for (let y = 72; y < 103; y++) for (let xx = lx - 10; xx < lx + 12; xx++) {
    const t = 1 - Math.abs(xx - lx) / 11 - (y - 72) / 60;
    if (t > 0 && dith(xx, y, t * 0.5)) { const c0 = cv.get(xx, y); if (c0) cv.set(xx, y, mix(C.toHex(c0.slice(0, 3)), '#F6C67D', 0.3)); }
  }
}
lamp(112); lamp(212);

// ---------- sağ: park + "TOPLANMA ALANI" tabelası
function tree(tx, ty, r, seed) {
  rect(tx - 1, ty, 3, 104 - ty, OL); rect(tx, ty, 1, 104 - ty, '#5A3E2E');
  for (let y = ty - r * 2; y <= ty + 2; y++) for (let xx = tx - r - 2; xx <= tx + r + 2; xx++) {
    const d = Math.hypot((xx - tx) / (r + 1), (y - (ty - r + 1)) / (r + 0.5));
    if (d > 1 + hash(xx >> 1, y >> 1, seed) * 0.15) continue;
    cv.set(xx, y, d > 0.85 ? '#1F3B33' : (y < ty - r ? '#3E7A57' : '#2E5E47'));
  }
}
tree(238, 82, 9, 1); tree(300, 78, 11, 2); tree(316, 86, 7, 3);
// çit
for (let xx = 228; xx < W; xx++) { cv.set(xx, 96, OL); cv.set(xx, 97, '#4A5D86'); }
for (let xx = 230; xx < W; xx += 6) rect(xx, 93, 1, 10, '#4A5D86');
// tabela: yeşil pano, beyaz piktogram (aile + ok) ve yazı
const BX = 252, BY = 60, BW = 44, BH = 30;
rect(BX + 20, BY + BH, 3, 104 - BY - BH, OL); rect(BX + 21, BY + BH, 1, 104 - BY - BH, '#9FB3C8');
rect(BX - 1, BY - 1, BW + 2, BH + 2, OL);
rect(BX, BY, BW, BH, '#2E9E5B'); rect(BX, BY, BW, 1, '#4CC77A'); rect(BX + 1, BY + 1, BW - 2, BH - 2, '#2E9E5B');
rect(BX + 1, BY + 1, BW - 2, 1, '#F3ECDD'); rect(BX + 1, BY + BH - 2, BW - 2, 1, '#F3ECDD');
rect(BX + 1, BY + 1, 1, BH - 2, '#F3ECDD'); rect(BX + BW - 2, BY + 1, 1, BH - 2, '#F3ECDD');
// piktogram: 3 kişi (büyük, büyük, küçük)
function person(px, py, h) { rect(px, py, 2, 2, '#F3ECDD'); rect(px - 1, py + 3, 4, h - 6, '#F3ECDD'); rect(px - 1, py + h - 3, 1, 3, '#F3ECDD'); rect(px + 2, py + h - 3, 1, 3, '#F3ECDD'); }
person(BX + 7, BY + 4, 12); person(BX + 13, BY + 4, 12); person(BX + 19, BY + 7, 9);
// ok
rect(BX + 26, BY + 10, 9, 2, '#F3ECDD'); cv.set(BX + 34, BY + 8, '#F3ECDD'); cv.set(BX + 35, BY + 9, '#F3ECDD'); cv.set(BX + 36, BY + 10, '#F3ECDD'); cv.set(BX + 36, BY + 11, '#F3ECDD'); cv.set(BX + 35, BY + 12, '#F3ECDD'); cv.set(BX + 34, BY + 13, '#F3ECDD');
const t1 = 'TOPLANMA', t2 = 'ALANI';
drawText(cv, t1, BX + Math.floor((BW - textW(t1)) / 2), BY + 17, '#F3ECDD');
drawText(cv, t2, BX + Math.floor((BW - textW(t2)) / 2), BY + 23, '#F3ECDD');

// ---------- çıktı
const OUTDIR = process.env.GE_OUT || PROJ + '/Assets/Art/GameEnd';
const png = OUTDIR + '/GAMEEND_Background.png';
cv.save(png);
const meta = png + '.meta';
const guid = guidFor(meta);
fs.writeFileSync(meta, metaText(guid, 'GAMEEND_Background', W, H, [{ name: 'GAMEEND_Background_0', x: 0, y: 0, w: W, h: H, px: 0.5, py: 0.5 }]));
const dirMeta = OUTDIR + '.meta';
if (!fs.existsSync(dirMeta)) fs.writeFileSync(dirMeta, `fileFormatVersion: 2\nguid: ${require('crypto').randomBytes(16).toString('hex')}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
// önizleme (4x) karalama klasörüne
const prev = process.argv[2];
if (prev) {
  const s = 3, P = new Cv(W * s, H * s);
  for (let y = 0; y < H * s; y++) for (let xx = 0; xx < W * s; xx++) { const c = cv.get(Math.floor(xx / s), Math.floor(y / s)); if (c) P.set(xx, y, c); }
  P.save(prev);
}
console.log('ok ' + png + ' guid=' + guid);
