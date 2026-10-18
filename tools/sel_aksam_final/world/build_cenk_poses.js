// Apartman finali — Cenk poz kareleri (kuzeybatıya, apartman duvarına bakar). Oyunun NoBag kuzeybatı yürüyüş karesinden (kare 0)
// piksel düzenlemesiyle türetilir: sarkan kol ve kısa kol silinip yelek tamamlanır, kol (kısa kollu tişört + ten) yeni açıyla çizilir.
// Renkler yalnız Cenk'in kendi paletinden; 96x96 kare, pivot (0.5, 0.19791667) yürüyüşle aynı → poz değişince karakter kaymaz.
//   node build_cenk_poses.js      → Assets/Art/FloodEvening/Final/Cenk/CHR_Cenk_Final_*.png (+ .meta)
'use strict';
const fs = require('fs'), path = require('path');
const C = require('../../sel_aksam_harita/common.js');
const ROOT = path.resolve(__dirname, '../../..').split(path.sep).join('/');
const DST = process.env.CENK_OUT || (ROOT + '/Assets/Art/FloodEvening/Final/Cenk');
const SRC = ROOT + '/Assets/Art/Characters/Cenk/NoBag/CHR_Cenk_Walk_NorthWest_NoBag.png';
fs.mkdirSync(DST, { recursive: true });

const sheet = C.decodePng(fs.readFileSync(SRC));
const base = new Uint8Array(96 * 96 * 4);
for (let y = 0; y < 96; y++) base.set(sheet.rgba.subarray(y * sheet.w * 4, (y * sheet.w + 96) * 4), y * 96 * 4);

// palet (kaynak karedeki renkler)
const K = {
  a: [0, 0, 0], C: [124, 42, 37], A: [99, 28, 20], o: [87, 23, 16], J: [41, 47, 65], K: [47, 54, 70],
  F: [237, 221, 175], E: [212, 188, 147], G: [227, 206, 164], D: [187, 164, 143],
  z: [247, 186, 142], p: [244, 175, 125], y: [233, 143, 103], t: [237, 161, 119], s: [205, 111, 82], x: [218, 125, 91], r: [151, 74, 52],
  env: [246, 240, 222], envD: [200, 190, 164], envL: [255, 252, 240],
};
const X0 = 31;   // dökümdeki sütun 0 = x 31
class Cell {
  constructor(src) { this.d = new Uint8Array(src); }
  get(x, y) { if (x < 0 || y < 0 || x >= 96 || y >= 96) return null; const o = (y * 96 + x) * 4; return this.d[o + 3] ? [this.d[o], this.d[o + 1], this.d[o + 2]] : null; }
  set(x, y, c) { if (x < 0 || y < 0 || x >= 96 || y >= 96) return; const o = (y * 96 + x) * 4; if (!c) { this.d.fill(0, o, o + 4); return; } this.d.set([c[0], c[1], c[2], 255], o); }
  // dökümdeki satırı (sütun c0'dan başlayarak) harflerle yaz: '.' saydam, '-' dokunma
  row(y, c0, str) { [...str].forEach((ch, i) => { if (ch === '-') return; this.set(X0 + c0 + i, y, ch === '.' ? null : K[ch]); }); }
  shift(y0, y1, dx) { // y0..y1 satırlarını dx kaydır
    for (let y = y0; y <= y1; y++) { const rowc = []; for (let x = 0; x < 96; x++) rowc.push(this.get(x, y)); for (let x = 0; x < 96; x++) this.set(x, y, rowc[x - dx] || null); }
  }
}

// ---- 1) kolsuz gövde: sarkan kol + kısa kol silinir, yelek/şort kenarı tamamlanır (satırlar kaynak kare 0'a göre)
function armless() {
  const c = new Cell(base);
  c.row(45, 10, 'aCCCCCCCCC'); c.row(46, 10, 'aCCCCCCCCC'); c.row(47, 10, 'aCCCCCCCCC'); c.row(48, 10, 'aCCCCCCCCC');
  c.row(49, 9, '.aCCCCCCCCC'); c.row(50, 9, '.aCCCCCCCCC'); c.row(51, 8, '.aCCCCCCCCCC');
  c.row(52, 8, 'aCCCCCC'); c.row(53, 8, 'aCCCCCC'); c.row(54, 8, 'aCCCCCC'); c.row(55, 8, 'aACCCCC'); c.row(56, 8, '.aCCCCC');
  c.row(57, 8, '.aooooo'); c.row(58, 8, '.aooooo');
  c.row(59, 7, '......a'); c.row(60, 7, '......a'); c.row(61, 7, '......a'); c.row(62, 7, '......a'); c.row(63, 7, '......a');
  c.row(64, 7, '....aJ');
  return c;
}

// ---- 2) kol çizimi: omuz → dirsek → el; omuz tarafı tişört kolu (açık bej), gerisi ten; alt kenar gölgeli, saydama siyah çizgi
const SHOULDER = [45.5, 46.5];
function drawArm(c, elbow, hand, opt = {}) {
  const added = new Set(); const key = (x, y) => x + ',' + y;
  const segs = [[SHOULDER, elbow], [elbow, hand]];
  const lenA = Math.hypot(elbow[0] - SHOULDER[0], elbow[1] - SHOULDER[1]);
  const sleeveLen = opt.sleeve !== undefined ? opt.sleeve : 5.2;
  const paint = (x, y, col) => { c.set(x, y, col); added.add(key(x, y)); };
  for (let y = 30; y < 72; y++) for (let x = 14; x < 70; x++) {
    const px = x + 0.5, py = y + 0.5; let best = null;
    segs.forEach(([p0, p1], si) => {
      const vx = p1[0] - p0[0], vy = p1[1] - p0[1], L2 = vx * vx + vy * vy || 1; let t = ((px - p0[0]) * vx + (py - p0[1]) * vy) / L2; t = Math.max(0, Math.min(1, t));
      const qx = p0[0] + t * vx, qy = p0[1] + t * vy; const dist = Math.hypot(px - qx, py - qy);
      const along = si === 0 ? t * lenA : lenA + t * Math.hypot(vx, vy);
      let nx = -vy, ny = vx; if (ny < 0 || (ny === 0 && nx < 0)) { nx = -nx; ny = -ny; } const side = ((px - qx) * nx + (py - qy) * ny) / Math.sqrt(L2); // + = ekranda alt taraf
      const sleeve = along < sleeveLen; const rad = sleeve ? 2.45 - 0.25 * (along / sleeveLen) : 1.45;
      if (dist <= rad && (!best || dist < best.dist)) best = { dist, sleeve, side, rad, along };
    });
    if (!best) continue;
    const down = best.side > 0; // aşağı bakan kenar
    const edge = best.dist > best.rad - 0.85;
    let col;
    if (best.sleeve) col = edge ? (down ? K.D : K.G) : (best.along < 1.2 ? K.E : K.F);
    else col = edge ? (down ? K.y : K.z) : K.p;
    paint(x, y, col);
  }
  // el (küçük yumru)
  const hr = opt.handR || 1.7;
  for (let y = Math.floor(hand[1] - 3); y <= hand[1] + 3; y++) for (let x = Math.floor(hand[0] - 3); x <= hand[0] + 3; x++) {
    const d = Math.hypot(x + 0.5 - hand[0], y + 0.5 - hand[1]); if (d > hr) continue; paint(x, y, d > hr - 0.7 && y + 0.5 > hand[1] ? K.t : K.z);
  }
  if (opt.envelope) drawEnvelope(c, opt.envelope, added, paint);
  // dış çizgi: yeni piksellerin saydam komşuları
  const out = [];
  for (const k of added) { const [x, y] = k.split(',').map(Number); for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { if (!c.get(x + dx, y + dy) && !added.has(key(x + dx, y + dy))) out.push([x + dx, y + dy]); } }
  for (const [x, y] of out) c.set(x, y, K.a);
  // kolun gövdeyle birleştiği yerde (yelek üstünde) koyu ayrım çizgisi: kolun alt kenarı yeleğe değiyorsa
  for (const k of added) { const [x, y] = k.split(',').map(Number); const b = c.get(x, y + 1); if (b && !added.has(key(x, y + 1)) && b[0] === K.C[0] && b[1] === K.C[1]) c.set(x, y + 1, K.A); }
}
function drawEnvelope(c, e, added, paint) {
  const [ex, ey, w, h] = e; // sol-üst, boy
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const X = ex + x, Y = ey + y; const border = x === 0 || y === 0 || x === w - 1 || y === h - 1;
    const flap = Math.abs((x - (w - 1) / 2)) + y * 1.4 < (w / 2) && y < h / 2 + 1;
    paint(X, Y, border ? K.envD : (flap && y > 0 ? K.envL : K.env));
  }
}

// ---- 3) pozlar (el hedefleri, 96'lık karede; ayak pivotu (48,77)). Hesap: build_world_art anchors (kilit/kutu önünde duruş).
const P = {};
P.InspectLock = [{ elbow: [38, 42], hand: [31, 37] }, { elbow: [38, 43], hand: [30, 38] }];
P.TypeKeypad = [{ elbow: [38, 42], hand: [31, 37] }, { elbow: [37, 42], hand: [30, 36] }];
P.TurnHandle = [{ elbow: [39, 49], hand: [32, 49] }, { elbow: [39, 50], hand: [32, 52] }];
P.ReachMailbox = [{ elbow: [39, 46], hand: [32, 44] }, { elbow: [38, 47], hand: [31, 46] }];
P.TakeEnvelope = [{ elbow: [40, 50], hand: [35, 47], envelope: [30, 44, 6, 4] }, { elbow: [42, 54], hand: [38, 50], envelope: [33, 47, 6, 4] }];
P.ReadEnvelope = [{ elbow: [41, 54], hand: [37, 48], envelope: [31, 43, 7, 5] }, { elbow: [41, 54], hand: [37, 49], envelope: [31, 44, 7, 5] }];
P.ReactDoor = [{ elbow: [42, 53], hand: [39, 57], lean: 1 }, { elbow: [41, 52], hand: [37, 55], lean: 1 }];

function render(spec) {
  const c = armless();
  if (spec.lean) c.shift(18, 56, spec.lean);
  const sh = spec.lean ? [SHOULDER[0] + spec.lean, SHOULDER[1]] : SHOULDER;
  const save = SHOULDER.slice(); SHOULDER[0] = sh[0];
  drawArm(c, spec.elbow, spec.hand, { envelope: spec.envelope });
  SHOULDER[0] = save[0];
  return c.d;
}
const written = [];
for (const [name, frames] of Object.entries(P)) {
  const cells = frames.map(render); const w = 96 * cells.length, out = new Uint8Array(w * 96 * 4);
  cells.forEach((cell, k) => { for (let y = 0; y < 96; y++) out.set(cell.subarray(y * 384, (y + 1) * 384), (y * w + k * 96) * 4); });
  const file = DST + '/CHR_Cenk_Final_' + name + '.png'; const base2 = 'CHR_Cenk_Final_' + name;
  fs.writeFileSync(file, C.encodePng(w, 96, out));
  fs.writeFileSync(file + '.meta', C.metaText(C.guidFor(file + '.meta'), base2, w, 96, cells.map((_, k) => ({ name: base2 + '_' + k, x: k * 96, y: 0, w: 96, h: 96, px: 0.5, py: 0.19791667 }))));
  written.push(base2);
}
console.log('Cenk pozları:', written.join(', '));
