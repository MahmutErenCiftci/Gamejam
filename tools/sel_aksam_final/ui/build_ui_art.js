// Akşam sel — apartman finali yakın plan arayüz görselleri (elektronik kilit + kapı kolu + mektup).
// Hepsi kodla çizilir (yerel piksel, 480x270 sanal tuvalde tam sayı katla ölçeklenir), .meta'larıyla
// Assets/Art/FloodEvening/Final/UI/ altına yazılır (Point, sıkıştırmasız, PPU 64, mipmap yok — common.js şablonu).
//   node tools/sel_aksam_final/ui/build_ui_art.js            → yaz
//   node tools/sel_aksam_final/ui/build_ui_art.js --preview  → yalnızca önizleme (out/ klasörüne)
// Işık sol üstten; 1 px koyu dış çizgi; sınırlı palet (lacivert arayüz + gümüş metal + kâğıt).
const fs = require('fs'), path = require('path');
const C = require('../../sel_aksam_harita/common.js');
const { Cv, metaText, guidFor, encodePng } = C;
const ROOT = path.resolve(__dirname, '../../..').split(path.sep).join('/');
const DST = ROOT + '/Assets/Art/FloodEvening/Final/UI';
const OUT = __dirname + '/out';
const PREVIEW = process.argv.includes('--preview');
fs.mkdirSync(OUT, { recursive: true });

// ------------------------------------------------------------------ palet
const P = {
  OL: '#0B0E18',                                      // genel dış çizgi
  OUT: '#04102A', NAVY: '#122040', NAVY2: '#1A2C55', EDGE: '#274584', EDGE2: '#3A62B0',
  M1: '#454C5B', M2: '#666E7E', M3: '#8C94A3', M3b: '#949CAA', M4: '#B4BBC7', M5: '#D6DBE3', M6: '#F2F4F8',
  K0: '#090B10', K1: '#14171F', K2: '#1E222C', K3: '#2A2F3B',
  KF: '#353B49', KFh: '#3E4555', KHi: '#566074', KL: '#464D5D', KR: '#262A35', KS: '#1C1F28', KS2: '#272B36',
  GLYPH: '#E9EDF3', GLYPHS: '#0E1016', TEAL: '#35C6C0', TEALD: '#1E7F86',
  LCD0: '#0B1714', LCD1: '#12221D', LCDG: '#3D5A52',
  AMBER: '#F5B731', AMBERD: '#B9801A', AMBERL: '#FFE08A', CREAM: '#F2E6C8',
  // kapı (koyu arduvaz mavisi boyalı çelik) + kasa
  D0: '#222A36', D1: '#2C3544', D2: '#36404F', D3: '#3F4A5B', D4: '#4B5768', D5: '#5A687B',
  J0: '#1A1E26', J1: '#2B3039', J2: '#3A404B', J3: '#4A515D', J4: '#5C6470',
  // kâğıt
  PAPER: '#F2E6C8', PAPER2: '#E8D9B4', PAPER3: '#DCCA9E', PAPERL: '#FBF3DE', PAPERO: '#5A4630', INK: '#1F2236', INKB: '#2E4C8E', STAMP: '#B8432F',
  ENV: '#E3D2AA', ENV2: '#D2BE90', ENV3: '#BFA877', ENVD: '#A08A5C', ENVIN: '#8F7A50',
};
const rgb = C.rgb;
const fl = Math.floor;
const hash = C.hash;

// ------------------------------------------------------------------ yazıcı
const written = [];
function write(name, cv, sprites) {
  const file = DST + '/' + name + '.png';
  if (!PREVIEW) {
    fs.mkdirSync(DST, { recursive: true });
    fs.writeFileSync(file, encodePng(cv.w, cv.h, cv.d));
    fs.writeFileSync(file + '.meta', metaText(guidFor(file + '.meta'), name, cv.w, cv.h, sprites || single(name, cv.w, cv.h)));
  }
  fs.writeFileSync(OUT + '/' + name + '.png', encodePng(cv.w, cv.h, cv.d));
  written.push(name + ' ' + cv.w + 'x' + cv.h + ' (' + (sprites ? sprites.length : 1) + ' sprite)');
}
const single = (name, w, h, px = 0.5, py = 0.5) => [{ name: name + '_0', x: 0, y: 0, w, h, px, py }];
/** Tek satır şerit: kareler soldan sağa, her biri fw x fh; pivot karenin içinde normalize. */
function strip(names, fw, fh, px = 0.5, py = 0.5) { return names.map((n, i) => ({ name: n, x: i * fw, y: 0, w: fw, h: fh, px, py })); }

// ------------------------------------------------------------------ çizim yardımcıları
function fillRect(cv, x0, y0, w, h, c) { cv.rect(x0, y0, w, h, c); }
function hline(cv, x0, x1, y, c) { for (let x = x0; x <= x1; x++) cv.set(x, y, c); }
function vline(cv, x, y0, y1, c) { for (let y = y0; y <= y1; y++) cv.set(x, y, c); }
/** Köşeleri r piksel kırpılmış dikdörtgen maskesi (r=1 → tek piksel köşe). */
function inRound(x, y, x0, y0, w, h, r) {
  if (x < x0 || y < y0 || x >= x0 + w || y >= y0 + h) return false;
  const dx = Math.min(x - x0, x0 + w - 1 - x), dy = Math.min(y - y0, y0 + h - 1 - y);
  if (dx >= r || dy >= r) return true;
  const ex = r - dx - 0.5, ey = r - dy - 0.5; return ex * ex + ey * ey <= r * r + 0.2;
}
/** Kenarlıklı yuvarlak köşe dikdörtgen: dış çizgi ol, iç dolgu fn(x,y) ya da renk. */
function roundBox(cv, x0, y0, w, h, r, ol, fill) {
  for (let y = y0; y < y0 + h; y++) for (let x = x0; x < x0 + w; x++) {
    if (!inRound(x, y, x0, y0, w, h, r)) continue;
    const edge = !inRound(x - 1, y, x0, y0, w, h, r) || !inRound(x + 1, y, x0, y0, w, h, r) || !inRound(x, y - 1, x0, y0, w, h, r) || !inRound(x, y + 1, x0, y0, w, h, r);
    if (edge && ol) cv.set(x, y, ol); else cv.set(x, y, typeof fill === 'function' ? fill(x - x0, y - y0) : fill);
  }
}
/** İçe gömük çerçeve (sol/üst koyu, sağ/alt açık) — ışık sol üstten. */
function insetFrame(cv, x0, y0, w, h, dark, light) {
  hline(cv, x0, x0 + w - 1, y0, dark); vline(cv, x0, y0, y0 + h - 1, dark);
  hline(cv, x0 + 1, x0 + w - 1, y0 + h - 1, light); vline(cv, x0 + w - 1, y0 + 1, y0 + h - 1, light);
}
/** Dışa kabarık çerçeve (sol/üst açık, sağ/alt koyu). */
function raisedFrame(cv, x0, y0, w, h, light, dark) { insetFrame(cv, x0, y0, w, h, light, dark); }
/** 1 px dış çizgi (saydam piksellerin 4 komşusu dolu ise). */
function outline(cv, col = P.OL) { return cv.outline(col); }
/** "##." satırlarından bitmap çiz. */
function bitmap(cv, rows, x0, y0, c) { rows.forEach((r, y) => [...r].forEach((ch, x) => { if (ch === '#') cv.set(x0 + x, y0 + y, c); })); }
function blend(a, b, t) { const A = rgb(a), B = rgb(b); return '#' + A.map((v, i) => Math.round(v + (B[i] - v) * t).toString(16).padStart(2, '0')).join(''); }

// ================================================================== KİLİT GÖVDESİ (90x150)
// Fırçalanmış gümüş gövde, LCD penceresi, LED yuvası + sürgü göstergesi yuvası, siyah tuş paneli (tuşlar ayrı sprite).
// Koordinatlar ApartmentLockView ile aynı olmalı (LCD, LED, sürgü, tuş ızgarası).
const BODY = { w: 90, h: 150, lcd: [10, 8, 70, 20], led: [70, 30, 10, 10], bolt: [12, 32, 30, 7], pad: [6, 43, 78, 96], key0: [10, 47], keyStep: [25, 22], key: [20, 18] };
function lockBody() {
  const { w, h } = BODY; const cv = new Cv(w, h);
  // fırçalanmış metal dolgu: sol üst açık → sağ alt koyu, yatay fırça izleri
  const metal = (x, y) => {
    const g = (x / w) * 0.45 + (y / h) * 0.55;
    const streak = hash(fl(x / 7), y, 3) < 0.28 ? 1 : hash(x, y, 9) < 0.05 ? -1 : 0;
    const lv = g < 0.22 ? 4 : g < 0.55 ? 3 : g < 0.8 ? 2.5 : 2;
    let c = lv === 4 ? P.M4 : lv === 3 ? (streak > 0 ? P.M3b : P.M3) : lv === 2.5 ? (streak > 0 ? P.M3 : blend(P.M3, P.M2, 0.5)) : P.M2;
    if (lv === 4 && streak > 0) c = blend(P.M4, P.M5, 0.5);
    return c;
  };
  roundBox(cv, 0, 0, w, h, 5, P.OL, metal);
  // kabartma kenar: 1 px açık üst/sol, 1 px koyu alt/sağ
  for (let y = 1; y < h - 1; y++) for (let x = 1; x < w - 1; x++) {
    if (!cv.a(x, y)) continue; const c = cv.get(x, y); if (c[0] === rgb(P.OL)[0] && c[1] === rgb(P.OL)[1]) continue;
    const nOL = (dx, dy) => { const q = cv.get(x + dx, y + dy); return q && q[0] === rgb(P.OL)[0] && q[1] === rgb(P.OL)[1] && q[2] === rgb(P.OL)[2]; };
    if (nOL(0, -1) || nOL(-1, 0)) cv.set(x, y, P.M6);
    else if (nOL(0, 1) || nOL(1, 0)) cv.set(x, y, P.M1);
  }
  // ikinci iç kenar (yumuşak bevel)
  for (let y = 2; y < h - 2; y++) { if (cv.get(2, y) && y > 4 && y < h - 5) cv.set(2, y, P.M5); if (y > 4 && y < h - 5) cv.set(w - 3, y, P.M2); }
  for (let x = 4; x < w - 4; x++) { cv.set(x, 2, P.M5); cv.set(x, h - 3, P.M2); }
  // LCD penceresi: gömük çerçeve + koyu cam + köşede cam parlaması
  { const [x0, y0, lw, lh] = BODY.lcd;
    insetFrame(cv, x0 - 1, y0 - 1, lw + 2, lh + 2, P.M1, P.M6);
    fillRect(cv, x0, y0, lw, lh, P.OL);
    fillRect(cv, x0 + 1, y0 + 1, lw - 2, lh - 2, P.LCD0);
    hline(cv, x0 + 1, x0 + lw - 2, y0 + 1, P.LCD1);
    for (let i = 0; i < 6; i++) { cv.set(x0 + 3 + i, y0 + 2 + 0, blend(P.LCD0, '#FFFFFF', 0.10)); }
    for (let i = 0; i < 3; i++) cv.set(x0 + 2, y0 + 3 + i, blend(P.LCD0, '#FFFFFF', 0.08));
  }
  // sürgü göstergesi yuvası (gömük)
  { const [x0, y0, bw, bh] = BODY.bolt; insetFrame(cv, x0 - 1, y0 - 1, bw + 2, bh + 2, P.M1, P.M6); fillRect(cv, x0, y0, bw, bh, P.K1); hline(cv, x0, x0 + bw - 1, y0, P.K0); }
  // hoparlör delikleri
  for (let i = 0; i < 4; i++) for (let j = 0; j < 2; j++) { const x = 49 + i * 4, y = 33 + j * 3; cv.set(x, y, P.M1); cv.set(x + 1, y, P.M2); }
  // LED yuvası: koyu halka
  { const [x0, y0] = BODY.led; for (let y = 0; y < 10; y++) for (let x = 0; x < 10; x++) { const dx = x - 4.5, dy = y - 4.5, d = dx * dx + dy * dy; if (d <= 21) cv.set(x0 + x, y0 + y, d > 13 ? (dx + dy < 0 ? P.M1 : P.M5) : P.K1); } }
  // tuş paneli: siyah, gömük, her tuşun yuvası bir ton koyu
  { const [x0, y0, pw, ph] = BODY.pad;
    insetFrame(cv, x0 - 1, y0 - 1, pw + 2, ph + 2, P.M1, P.M6);
    roundBox(cv, x0, y0, pw, ph, 2, P.K0, (x, y) => (y < 2 ? P.K2 : P.K1));
    for (let r = 0; r < 4; r++) for (let c = 0; c < 3; c++) {
      const kx = BODY.key0[0] + c * BODY.keyStep[0], ky = BODY.key0[1] + r * BODY.keyStep[1];
      fillRect(cv, kx - 1, ky - 1, BODY.key[0] + 2, BODY.key[1] + 2, P.K0);
      hline(cv, kx, kx + BODY.key[0] - 1, ky + BODY.key[1], P.K2);
    }
  }
  // alt vidalar
  for (const [sx, sy] of [[6, 142], [81, 142]]) { bitmap(cv, ['.#.', '###', '.#.'], sx, sy, P.M1); cv.set(sx + 1, sy + 1, P.M5); cv.set(sx, sy, P.M5); }
  return cv;
}

// ================================================================== TUŞLAR (20x18, 3 durum: N/H/P) — rakam tuşun içine basılı
const GL = {
  '1': ['..#..', '.##..', '..#..', '..#..', '..#..', '..#..', '.###.'],
  '2': ['.###.', '#...#', '....#', '...#.', '..#..', '.#...', '#####'],
  '3': ['.###.', '#...#', '....#', '..##.', '....#', '#...#', '.###.'],
  '4': ['...#.', '..##.', '.#.#.', '#..#.', '#####', '...#.', '...#.'],
  '5': ['#####', '#....', '####.', '....#', '....#', '#...#', '.###.'],
  '6': ['..##.', '.#...', '#....', '####.', '#...#', '#...#', '.###.'],
  '7': ['#####', '....#', '...#.', '..#..', '.#...', '.#...', '.#...'],
  '8': ['.###.', '#...#', '#...#', '.###.', '#...#', '#...#', '.###.'],
  '9': ['.###.', '#...#', '#...#', '.####', '....#', '...#.', '.##..'],
  '0': ['.###.', '#...#', '#..##', '#.#.#', '##..#', '#...#', '.###.'],
  'Star': ['.....', '#.#.#', '.###.', '#####', '.###.', '#.#.#', '.....'],
  'Hash': ['.#.#.', '.#.#.', '#####', '.#.#.', '#####', '.#.#.', '.#.#.'],
};
const KEY_IDS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', 'Star', '0', 'Hash'];
function keyCap(id, state) {
  const [w, h] = BODY.key; const cv = new Cv(w, h); const pressed = state === 'P', hover = state === 'H';
  const top = pressed ? 1 : 0;                         // basılıyken kapak 1 px aşağıda
  const faceBottom = pressed ? 14 : 13;                // yüz alanı alt satırı
  // dış çizgi + kapak
  roundBox(cv, 0, top, w, h - top, 2, P.K0, P.KS);
  for (let y = top + 1; y <= faceBottom; y++) for (let x = 1; x < w - 1; x++) {
    if (!inRound(x, y, 0, top, w, h - top, 2) || !inRound(x - 1, y, 0, top, w, h - top, 2) || !inRound(x + 1, y, 0, top, w, h - top, 2)) continue;
    let c = pressed ? '#2B303C' : hover ? P.KFh : P.KF;
    if (y === top + 1) c = pressed ? '#3A404E' : hover ? '#6A7488' : P.KHi;
    else if (x === 1) c = pressed ? '#323744' : P.KL;
    else if (x === w - 2) c = P.KR;
    cv.set(x, y, c);
  }
  // etek (tuşun yanı): basılıyken ince
  for (let y = faceBottom + 1; y < h - 1; y++) for (let x = 1; x < w - 1; x++) if (inRound(x, y, 0, top, w, h - top, 2) && cv.get(x, y)) cv.set(x, y, y === faceBottom + 1 ? P.KS2 : P.KS);
  // üzerine gelince turkuaz iç çerçeve
  if (hover) {
    for (let x = 2; x < w - 2; x++) { cv.set(x, top + 1, P.TEAL); }
    for (let y = top + 2; y <= faceBottom; y++) { cv.set(1, y, P.TEAL); cv.set(w - 2, y, P.TEALD); }
    for (let x = 2; x < w - 2; x++) cv.set(x, faceBottom + 1, P.TEALD);
  }
  // rakam (5x7) + 1 px gölge
  const g = GL[id]; const gx = 8 - (id === '1' ? 0 : 0), gy = top + 4;
  bitmap(cv, g, gx - 1 + 1, gy + 1, P.GLYPHS);
  bitmap(cv, g, gx - 1, gy, pressed ? '#BFC5CF' : hover ? '#FFFFFF' : P.GLYPH);
  return cv;
}
function keysSheet() {
  const [w, h] = BODY.key; const states = ['N', 'H', 'P'];
  const cv = new Cv(w * KEY_IDS.length, h * states.length); const sprites = [];
  states.forEach((st, r) => KEY_IDS.forEach((id, c) => {
    const k = keyCap(id, st); cv.blit(k, c * w, r * h);
    sprites.push({ name: 'UI_FE_Final_Key_' + id + '_' + st, x: c * w, y: (states.length - 1 - r) * h, w, h, px: 0.5, py: 0.5 });
  }));
  return { cv, sprites };
}

// ================================================================== LCD RAKAMLARI (8x14, beyaz; çalışma anında renklendirilir)
const SEG = { a: [[2, 0, 4, 2]], b: [[6, 2, 2, 4]], c: [[6, 8, 2, 4]], d: [[2, 12, 4, 2]], e: [[0, 8, 2, 4]], f: [[0, 2, 2, 4]], g: [[2, 6, 4, 2]] };
const DIG7 = { 0: 'abcdef', 1: 'bc', 2: 'abged', 3: 'abgcd', 4: 'fgbc', 5: 'afgcd', 6: 'afgedc', 7: 'abc', 8: 'abcdefg', 9: 'abcdfg', Ghost: 'abcdefg', Dash: 'g', Under: 'd' };
function lcdGlyph(segs) {
  const cv = new Cv(8, 14);
  for (const s of segs) for (const [x0, y0, w, h] of SEG[s]) {
    fillRect(cv, x0, y0, w, h, '#FFFFFF');
    // segment uçları eğik: köşe pikseli %60 parlak (yumuşak 7 segment görünümü, hâlâ keskin)
  }
  // köşe bağlantıları (a-f, a-b ...) tek piksel: segmentler birleşik görünsün
  const has = s => segs.includes(s);
  const join = (x, y, p, q) => { if (has(p) && has(q)) cv.set(x, y, '#FFFFFF'); };
  join(1, 1, 'a', 'f'); join(6, 1, 'a', 'b'); join(1, 6, 'f', 'g'); join(6, 6, 'b', 'g'); join(1, 7, 'e', 'g'); join(6, 7, 'c', 'g'); join(1, 12, 'e', 'd'); join(6, 12, 'c', 'd');
  join(1, 6, 'f', 'e'); join(1, 7, 'f', 'e'); join(6, 6, 'b', 'c'); join(6, 7, 'b', 'c');
  return cv;
}
function lcdSheet() {
  const ids = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'Ghost', 'Dash', 'Under'];
  const cv = new Cv(8 * ids.length, 14);
  ids.forEach((id, i) => cv.blit(lcdGlyph([...DIG7[id]]), i * 8, 0));
  return { cv, sprites: strip(ids.map(id => 'UI_FE_Final_Lcd_' + id), 8, 14) };
}

// ================================================================== LED (10x10: kapalı / kırmızı / yeşil, açıkken hafif hale)
function led(kind) {
  const cv = new Cv(10, 10);
  const pal = kind === 'Red' ? ['#FF4A3A', '#FFC2B4', '#B8261C', '#FF4A3A'] : kind === 'Green' ? ['#49E07A', '#D2FFE0', '#1F9A48', '#49E07A'] : ['#5C2522', '#7E3D36', '#3E1714', null];
  if (pal[3]) for (let y = 0; y < 10; y++) for (let x = 0; x < 10; x++) { const dx = x - 4.5, dy = y - 4.5, d = dx * dx + dy * dy; if (d > 9 && d <= 22) cv.set(x, y, pal[3], d <= 15 ? 110 : 55); }
  for (let y = 0; y < 10; y++) for (let x = 0; x < 10; x++) {
    const dx = x - 4.5, dy = y - 4.5, d = dx * dx + dy * dy; if (d > 9) continue;
    let c = pal[0]; if (dx + dy > 2) c = pal[2]; if (d < 2.6 && dx < 0.6 && dy < 0.6) c = pal[1];
    if (d > 5.6) c = dx + dy < -1 ? pal[0] : P.OL;
    cv.set(x, y, c);
  }
  cv.set(3, 3, pal[1]);
  return cv;
}
function ledSheet() { const ids = ['Off', 'Red', 'Green']; const cv = new Cv(30, 10); ids.forEach((id, i) => cv.blit(led(id), i * 10, 0)); return { cv, sprites: strip(ids.map(i => 'UI_FE_Final_Led_' + i), 10, 10) }; }

// ================================================================== SÜRGÜ GÖSTERGESİ (gövdedeki yuvada kayan çubuk, 22x5)
function deadbolt() {
  const cv = new Cv(22, 5);
  fillRect(cv, 0, 0, 22, 5, P.OL);
  for (let x = 1; x < 21; x++) { cv.set(x, 1, P.M5); cv.set(x, 2, P.M4); cv.set(x, 3, P.M2); }
  for (let y = 1; y < 4; y++) { cv.set(19, y, '#E8574A'); cv.set(20, y, '#B8261C'); }   // kırmızı uç: "kilitli"
  return cv;
}

// ================================================================== KAPI KOLU (yatay metal kol, önceden çizilmiş 16 kare: 0°..45°, 3° adım)
// Neden önceden çizilmiş kare: döndürülen tek bir Image piksel ızgarasından kayar (yamuk/bulanık "mixel" oluşur);
// kareler her açıda 1 px dış çizgi + sol üst ışıkla yeniden çizildiği için keskin kalır. Pivot = rozet/menteşe merkezi.
const HANDLE = { w: 96, h: 80, pivot: [14, 14], len: 70, frames: 16, step: 3 };
function leverMask(lx, ly) {
  // yerel koordinat: x kol boyunca, y aşağı. Dönüş: 0 dışarıda, 1 kol, 2 göbek
  if (lx * lx + ly * ly <= 7.6 * 7.6) return 2;
  if (lx >= 0 && lx <= HANDLE.len && Math.abs(ly) <= (lx < 16 ? 5 - (lx - 6) * 0.1 : 4)) return 1;
  const ex = lx - HANDLE.len - 1, ey = ly - 0.4; if (ex * ex + ey * ey <= 5.1 * 5.1) return 1;   // tutma ucu biraz kalın ve yuvarlak
  return 0;
}
function handleFrame(deg, glow) {
  const { w, h, pivot } = HANDLE; const cv = new Cv(w, h); const a = deg * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a);
  const kind = new Uint8Array(w * h);
  const local = (x, y) => { const dx = x + 0.5 - pivot[0], dy = y + 0.5 - pivot[1]; return [dx * ca + dy * sa, -dx * sa + dy * ca, dx, dy]; };
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const [lx, ly] = local(x, y); kind[y * w + x] = leverMask(lx, ly); }
  const K = (x, y) => (x < 0 || y < 0 || x >= w || y >= h) ? 0 : kind[y * w + x];
  if (glow) {
    // vurgu halkası: dış çizginin hemen dışı (1. halka tam, 2. halka yarı saydam)
    const solid = (x, y) => K(x, y) > 0 || K(x - 1, y) > 0 || K(x + 1, y) > 0 || K(x, y - 1) > 0 || K(x, y + 1) > 0;   // gövde + dış çizgi
    const ring1 = new Uint8Array(w * h);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { if (solid(x, y)) continue; if (solid(x - 1, y) || solid(x + 1, y) || solid(x, y - 1) || solid(x, y + 1)) { ring1[y * w + x] = 1; cv.set(x, y, P.AMBER); } }
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { if (solid(x, y) || ring1[y * w + x]) continue; const r = (xx, yy) => xx >= 0 && yy >= 0 && xx < w && yy < h && ring1[yy * w + xx]; if (r(x - 1, y) || r(x + 1, y) || r(x, y - 1) || r(x, y + 1)) cv.set(x, y, P.AMBERL, 110); }
    return cv;
  }
  // gölge (kapıya düşen, +2,+2)
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (K(x - 2, y - 2) > 0 || K(x - 3, y - 2) > 0) cv.set(x, y, '#000000', 80);
  // kol
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const k = K(x, y); if (!k) continue; const [lx, ly, dx, dy] = local(x, y);
    let c;
    if (k === 1) {
      const ht = lx > HANDLE.len - 3 ? 5.1 : lx < 16 ? 5 - (lx - 6) * 0.1 : 4; const v = ly / ht;
      c = v < -0.62 ? (lx > 10 && lx < HANDLE.len - 4 ? P.M6 : P.M5) : v < -0.15 ? P.M5 : v < 0.3 ? P.M4 : v < 0.7 ? P.M3 : P.M2;
      if (lx > HANDLE.len - 2 && v > -0.3) c = v < 0.3 ? P.M3 : P.M2;                     // uç kapak hafif gölgeli
      if (lx > 7.5 && lx < 10.5) c = v < -0.2 ? P.M4 : P.M2;                               // göbek bileziği
    } else {
      // göbek: dünyaya göre (dönmez) küresel gölgelendirme, ışık sol üstten
      const r = Math.sqrt(dx * dx + dy * dy); const lt = (-dx - dy) / (Math.SQRT2 * Math.max(1, r));
      c = r > 6.4 ? (lt > 0.2 ? P.M5 : lt > -0.4 ? P.M3 : P.M1) : r < 2.2 ? (lt > 0 ? P.M6 : P.M4) : lt > 0.35 ? P.M5 : lt > -0.3 ? P.M4 : P.M3;
    }
    cv.set(x, y, c);
  }
  // 1 px dış çizgi (gölge piksellerinin üstüne)
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { if (K(x, y)) continue; if (K(x - 1, y) || K(x + 1, y) || K(x, y - 1) || K(x, y + 1)) cv.set(x, y, P.OL); }
  return cv;
}
function handleSheets() {
  const { w, h, frames, step, pivot } = HANDLE; const a = new Cv(w * frames, h), g = new Cv(w * frames, h); const sa = [], sg = [];
  for (let i = 0; i < frames; i++) {
    a.blit(handleFrame(i * step, false), i * w, 0); g.blit(handleFrame(i * step, true), i * w, 0);
    const pv = { px: pivot[0] / w, py: (h - pivot[1]) / h };
    sa.push({ name: 'UI_FE_Final_Handle_' + String(i).padStart(2, '0'), x: i * w, y: 0, w, h, ...pv });
    sg.push({ name: 'UI_FE_Final_HandleGlow_' + String(i).padStart(2, '0'), x: i * w, y: 0, w, h, ...pv });
  }
  return { a, g, sa, sg };
}
// rozet (kolun takıldığı yuvarlak plaka, 28x28; merkez = pivot)
function rosette() {
  const cv = new Cv(28, 28); const c0 = 13;
  for (let y = 0; y < 28; y++) for (let x = 0; x < 28; x++) { const dx = x + 0.5 - c0 - 2, dy = y + 0.5 - c0 - 2; if (dx * dx + dy * dy <= 12.4 * 12.4) cv.set(x, y, '#000000', 80); }
  for (let y = 0; y < 28; y++) for (let x = 0; x < 28; x++) {
    const dx = x + 0.5 - c0, dy = y + 0.5 - c0, r = Math.sqrt(dx * dx + dy * dy); if (r > 12.4) continue;
    const lt = (-dx - dy) / (Math.SQRT2 * Math.max(1, r));
    let c = r > 11.4 ? P.OL : r > 9.6 ? (lt > 0.25 ? P.M6 : lt > -0.25 ? P.M4 : P.M1) : r > 8.6 ? (lt > 0 ? P.M2 : P.M5) : (hash(x, y, 4) < 0.18 ? P.M3b : P.M3);
    cv.set(x, y, c);
  }
  return cv;
}
// mandal dili (kapı kenarından kasaya giren yaylı dil, 14x9; sağ üst köşe eğik)
function latchBolt() {
  const cv = new Cv(14, 9);
  for (let y = 0; y < 9; y++) for (let x = 0; x < 14; x++) {
    if (x > 13 - Math.max(0, 4 - y) && y < 4) continue;                       // eğik yüz
    cv.set(x, y, y === 0 || x === 0 ? P.M5 : y >= 7 ? P.M2 : y >= 5 ? P.M3 : P.M4);
  }
  const c = new Cv(14, 9); c.blit(cv, 0, 0);
  for (let y = 0; y < 9; y++) for (let x = 0; x < 14; x++) { if (!cv.a(x, y)) continue; const e = !cv.a(x - 1, y) && x > 0 || !cv.a(x + 1, y) || !cv.a(x, y - 1) || !cv.a(x, y + 1) || y === 8 || x === 13; if (e && x > 0) c.set(x, y, P.OL); }
  return c;
}

// ================================================================== ARKA PLAN: kapının yakın planı (272x196)
const BG = { w: 272, h: 196, body: [20, 22], rosette: [136, 112], doorEdge: 229, strike: [233, 94, 13, 36], hole: [235, 107, 9, 11], bolt: [229, 108] };
function lockBg() {
  const { w, h } = BG; const cv = new Cv(w, h);
  roundBox(cv, 0, 0, w, h, 4, P.OUT, P.NAVY);
  roundBox(cv, 1, 1, w - 2, h - 2, 3, P.EDGE, P.NAVY);
  roundBox(cv, 2, 2, w - 4, h - 4, 2, P.OUT, P.NAVY);
  const X0 = 3, Y0 = 3, X1 = w - 4, Y1 = h - 4;
  // kapı yüzeyi: boyalı çelik, sol üst açık → sağ alt koyu, ince dikey fırça
  for (let y = Y0; y <= Y1; y++) for (let x = X0; x < BG.doorEdge; x++) {
    // bant sınırları 2 px'lik dama deseniyle yumuşatılır (keskin çapraz çizgi olmasın)
    let g = (x - X0) / (BG.doorEdge - X0) * 0.55 + (y - Y0) / (Y1 - Y0) * 0.45;
    if (((x + y) & 1) === 0) g += 0.012; else g -= 0.012;
    let c = g < 0.28 ? P.D4 : g < 0.62 ? P.D3 : P.D2;
    if (hash(x, fl(y / 14), 5) < 0.05) c = g < 0.28 ? P.D5 : g < 0.62 ? P.D4 : P.D3;
    cv.set(x, y, c);
  }
  // gömme panel çizgisi
  insetFrame(cv, 12, 11, 208, 176, P.D1, P.D5); insetFrame(cv, 13, 12, 206, 174, P.D2, P.D4);
  // kapı kenarı + aralık + kasa
  vline(cv, BG.doorEdge - 1, Y0, Y1, P.D5); vline(cv, BG.doorEdge, Y0, Y1, P.OL); vline(cv, BG.doorEdge + 1, Y0, Y1, P.J0);
  for (let y = Y0; y <= Y1; y++) for (let x = BG.doorEdge + 2; x <= X1; x++) {
    const t = (x - BG.doorEdge - 2) / (X1 - BG.doorEdge - 2);
    let c = t < 0.12 ? P.J1 : t < 0.2 ? P.J4 : t < 0.7 ? P.J3 : t < 0.8 ? P.J2 : P.J1;
    if (hash(x, y, 7) < 0.06) c = blend(c, '#000000', 0.15);
    cv.set(x, y, c);
  }
  // karşılık sacı + mandal deliği
  { const [sx, sy, sw, sh] = BG.strike;
    roundBox(cv, sx, sy, sw, sh, 1, P.OL, (x, y) => (y < 2 || x < 1 ? P.M5 : x > sw - 3 || y > sh - 3 ? P.M2 : P.M4));
    const [hx, hy, hw, hh] = BG.hole; fillRect(cv, hx, hy, hw, hh, P.K0); hline(cv, hx, hx + hw - 1, hy + hh, P.M5); vline(cv, hx + hw, hy, hy + hh, P.M5);
    for (const vy of [sy + 4, sy + sh - 6]) { cv.set(sx + 6, vy, P.M1); cv.set(sx + 6, vy + 1, P.M2); cv.set(sx + 5, vy, P.M6); }
  }
  // kilit gövdesinin kapıya düşen gölgesi (+2,+2)
  { const [bx, by] = BG.body; for (let y = by + 3; y < by + BODY.h + 3; y++) for (let x = bx + 3; x < bx + BODY.w + 3; x++) if (inRound(x - bx - 3, y - by - 3, 0, 0, BODY.w, BODY.h, 5)) { const c = cv.get(x, y); if (c) cv.set(x, y, blend(C.toHex(c), '#000000', 0.35)); } }
  return cv;
}

// ================================================================== küçük ikonlar
function dragArrow() {
  // aşağı ok: a = açık kenar, b = dolgu, d = gölgeli kenar; dış çizgi sonra eklenir
  const rows = ['...aab...', '...abd...', '...abd...', '...abd...', '...abd...', 'aaaabdddd', '.abbbbbd.', '..abbbd..', '...abd...', '....d....'];
  const cv = new Cv(11, 12); const col = { a: P.AMBERL, b: P.AMBER, d: P.AMBERD };
  rows.forEach((r, y) => [...r].forEach((ch, x) => { if (col[ch]) cv.set(x + 1, y + 1, col[ch]); }));
  return outline(cv, P.OUT);
}
function mouseIcon() {
  const cv = new Cv(11, 15);
  bitmap(cv, ['..#####..', '.#######.', '#########', '#########', '#########', '#########', '#########', '#########', '#########', '.#######.', '..#####..'], 1, 2, P.CREAM);
  bitmap(cv, ['..##', '.###', '####', '####'], 1, 2, P.AMBER);
  vline(cv, 5, 2, 6, P.OUT); hline(cv, 1, 9, 6, P.OUT);
  for (let y = 7; y < 12; y++) cv.set(9, y, '#C9BFA6');
  return outline(cv, P.OUT);
}

// ================================================================== MEKTUP (180x204) + büyük rakamlar (16x24) + zarf
const LETTER = { w: 180, h: 204, folds: [68, 138], box: [30, 72, 120, 38] };
function letterPaper() {
  const { w, h } = LETTER; const cv = new Cv(w, h);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const e = Math.min(x, y, w - 1 - x, h - 1 - y);
    let c = e < 3 ? P.PAPER2 : P.PAPER;
    const cx = Math.min(x, w - 1 - x), cy = Math.min(y, h - 1 - y); if (cx + cy < 9) c = cx + cy < 5 ? P.PAPER3 : P.PAPER2;   // yıpranmış köşeler
    const n = hash(x, y, 11); if (n < 0.03) c = P.PAPER2; else if (n > 0.988) c = P.PAPERL;
    for (const [sx, sy, sr] of [[150, 44, 5], [24, 128, 4], [160, 120, 3], [60, 190, 4]]) { const dx = x - sx, dy = y - sy; if (dx * dx + dy * dy <= sr * sr && hash(x, y, 3) < 0.55) c = P.PAPER2; }   // hafif lekeler
    cv.set(x, y, c);
  }
  // katlama izleri (üçe katlanmış mektup)
  for (const fy of LETTER.folds) for (let x = 1; x < w - 1; x++) { cv.set(x, fy, P.PAPER3); cv.set(x, fy + 1, P.PAPERL); }
  // dış çizgi, köşeler 1 px kırpık
  for (let x = 1; x < w - 1; x++) { cv.set(x, 0, P.PAPERO); cv.set(x, h - 1, P.PAPERO); }
  for (let y = 1; y < h - 1; y++) { cv.set(0, y, P.PAPERO); cv.set(w - 1, y, P.PAPERO); }
  for (const [x, y] of [[0, 0], [w - 1, 0], [0, h - 1], [w - 1, h - 1]]) cv.d[(y * w + x) * 4 + 3] = 0;
  // başlık logosu: apartman binası (13x13)
  bitmap(cv, ['.....###.....', '....#####....', '..#########..', '.###########.', '.##.##.##.##.', '.###########.', '.##.##.##.##.', '.###########.', '.##.##.##.##.', '.###########.', '.#####.#####.', '.####...####.', '#############'], 12, 9, P.INKB);
  // başlık çizgisi
  hline(cv, 12, w - 13, 27, P.INKB); hline(cv, 12, w - 13, 28, P.INKB); hline(cv, 12, w - 13, 30, P.STAMP);
  // kod kutusu: çift çerçeve
  { const [bx, by, bw, bh] = LETTER.box;
    fillRect(cv, bx, by, bw, bh, P.INK); fillRect(cv, bx + 1, by + 1, bw - 2, bh - 2, P.PAPERL); insetFrame(cv, bx + 3, by + 3, bw - 6, bh - 6, P.PAPER3, P.PAPER2);
    for (const [cx, cy] of [[bx, by], [bx + bw - 1, by], [bx, by + bh - 1], [bx + bw - 1, by + bh - 1]]) cv.d[(cy * w + cx) * 4 + 3] = 0;
  }
  // imza (mavi mürekkep)
  { let px = null; for (let t = 0; t <= 1; t += 0.004) { const x = 18 + t * 52, y = 172 - Math.sin(t * Math.PI * 5.2) * 5 * (1 - t * 0.5) - t * 6 + Math.sin(t * 31) * 1.2; const q = [Math.round(x), Math.round(y)]; if (!px || px[0] !== q[0] || px[1] !== q[1]) cv.set(q[0], q[1], P.INKB); px = q; }
    hline(cv, 16, 78, 181, P.PAPER3); }
  // mühür (kırmızı, aşınmış)
  { const cx = 142, cy = 168; for (let y = -14; y <= 14; y++) for (let x = -14; x <= 14; x++) { const r = Math.sqrt(x * x + y * y); const ring = (r > 11.2 && r <= 13.4) || (r > 8.2 && r <= 9.2); if (ring && hash(x, y, 21) > 0.12) cv.set(cx + x, cy + y, P.STAMP, 215); }
    bitmap(cv, ['...#...', '..###..', '.#####.', '#######', '.#.#.#.', '.#####.', '.##.##.'], cx - 3, cy - 4, P.STAMP); }
  return cv;
}
// büyük rakamlar: vektör çizgilerden 3 px kalınlıkta (daktilo/mühür mürekkebi)
function strokeDigit(d) {
  const W = 16, H = 24, cv = new Cv(W, H), segs = [];
  const arc = (cx, cy, rx, ry, a0, a1, n = 24) => { const pts = []; for (let i = 0; i <= n; i++) { const a = (a0 + (a1 - a0) * i / n) * Math.PI / 180; pts.push([cx + rx * Math.cos(a), cy + ry * Math.sin(a)]); } return pts; };
  const poly = pts => { for (let i = 1; i < pts.length; i++) segs.push([pts[i - 1], pts[i]]); };
  switch (d) {
    case 0: poly(arc(8, 12, 5.2, 9.2, 0, 360, 40)); break;
    case 1: poly([[4.5, 6], [8.5, 2.6], [8.5, 21.4]]); poly([[4.5, 21.4], [12.5, 21.4]]); break;
    case 2: poly([...arc(8, 7.6, 5, 5, 195, 375, 20), [3.2, 21.3], [13.2, 21.3]]); break;
    case 3: poly(arc(7.8, 7.2, 4.8, 4.6, 200, 450, 22)); poly(arc(7.8, 16.4, 5.4, 5, 270, 520, 24)); break;
    case 4: poly([[11, 21.4], [11, 2.6], [2.8, 15.4], [13.6, 15.4]]); break;
    case 5: poly([[12.6, 2.6], [4.4, 2.6], [3.8, 10.4], ...arc(7.8, 15.4, 5.4, 6, 235, 500, 26)]); break;
    case 6: poly(arc(8, 15.6, 5.2, 5.8, 0, 360, 32)); poly(arc(11.5, 13.5, 8.6, 11, 255, 180, 14)); break;
    case 7: poly([[2.6, 2.6], [13.4, 2.6], [6.4, 21.4]]); break;
    case 8: poly(arc(8, 7.1, 4.4, 4.5, 0, 360, 28)); poly(arc(8, 16.5, 5.4, 5, 0, 360, 32)); break;
    case 9: poly(arc(8, 8.4, 5.2, 5.8, 0, 360, 32)); poly(arc(4.5, 10.5, 8.6, 11, 75, 0, 14)); break;
  }
  const dist = (px, py, [a, b]) => { const vx = b[0] - a[0], vy = b[1] - a[1], wx = px - a[0], wy = py - a[1]; const t = Math.max(0, Math.min(1, (vx * wx + vy * wy) / (vx * vx + vy * vy || 1))); const dx = wx - t * vx, dy = wy - t * vy; return Math.sqrt(dx * dx + dy * dy); };
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { let m = 1e9; for (const s of segs) m = Math.min(m, dist(x + 0.5, y + 0.5, s)); if (m <= 1.55) cv.set(x, y, P.INK); }
  return cv;
}
function letterDigits() { const cv = new Cv(160, 24); for (let d = 0; d < 10; d++) cv.blit(strokeDigit(d), d * 16, 0); return { cv, sprites: strip([...Array(10).keys()].map(d => 'UI_FE_Final_LetterDigit_' + d), 16, 24) }; }

const ENVL = { w: 196, h: 112, v: 56 };
function envBack() { const { w, h } = ENVL; const cv = new Cv(w, h); roundBox(cv, 0, 0, w, h, 2, P.PAPERO, (x, y) => (y < 3 ? P.ENVD : P.ENVIN)); return cv; }
function envFront() {
  const { w, h, v } = ENVL; const cv = new Cv(w, h);
  const top = x => 4 + v * (1 - Math.abs(x + 0.5 - w / 2) / (w / 2));
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    if (y < top(x) || !inRound(x, y, 0, 0, w, h, 2)) continue;
    const bottomFlap = y > h - 1 - (h - v - 4) * (1 - Math.abs(x + 0.5 - w / 2) / (w / 2)) * 1.05;
    cv.set(x, y, bottomFlap ? P.ENV2 : P.ENV);
  }
  const ol = rgb(P.PAPERO);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { if (!cv.a(x, y)) continue; if (!cv.a(x - 1, y) || !cv.a(x + 1, y) || !cv.a(x, y - 1) || !cv.a(x, y + 1) || x === 0 || x === w - 1 || y === h - 1) cv.set(x, y, P.PAPERO); }
  const isOl = c => c && c[0] === ol[0] && c[1] === ol[1] && c[2] === ol[2];
  const k = c => c ? c[0] * 65536 + c[1] * 256 + c[2] : -1;
  for (let y = 1; y < h - 1; y++) for (let x = 1; x < w - 1; x++) { const c = cv.get(x, y), r = cv.get(x, y + 1); if (c && r && k(c) !== k(r) && !isOl(c) && !isOl(r)) cv.set(x, y, P.ENV3); }
  return cv;
}
function envFlap(open) {
  const { w } = ENVL, fh = 60; const cv = new Cv(w, fh);
  for (let y = 0; y < fh; y++) for (let x = 0; x < w; x++) {
    const k = 1 - Math.abs(x + 0.5 - w / 2) / (w / 2); const lim = (fh - 3) * k;
    const inside = open ? (fh - 1 - y) <= lim : y <= lim;
    if (inside) cv.set(x, y, open ? P.ENV3 : P.ENV);
  }
  const m = new Cv(w, fh); m.d.set(cv.d);
  for (let y = 0; y < fh; y++) for (let x = 0; x < w; x++) { if (!m.a(x, y)) continue; const e = !m.a(x - 1, y) || !m.a(x + 1, y) || (open ? !m.a(x, y - 1) : !m.a(x, y + 1)) || x === 0 || x === w - 1; if (e) cv.set(x, y, P.PAPERO); }
  if (!open) { const c0 = [w / 2 - 5, 42]; for (let y = 0; y < 10; y++) for (let x = 0; x < 10; x++) { const dx = x - 4.5, dy = y - 4.5, d = dx * dx + dy * dy; if (d <= 20) cv.set(c0[0] + x, c0[1] + y, d > 13 ? '#7A2A20' : dx + dy < -2 ? '#E06A55' : P.STAMP); } }   // mühür mumu
  return cv;
}

// ================================================================== çıktı
const body = lockBody(); write('UI_FE_Final_LockBody', body);
const ks = keysSheet(); write('UI_FE_Final_Keys', ks.cv, ks.sprites);
const lc = lcdSheet(); write('UI_FE_Final_Lcd', lc.cv, lc.sprites);
const ld = ledSheet(); write('UI_FE_Final_Led', ld.cv, ld.sprites);
write('UI_FE_Final_Deadbolt', deadbolt());
const hs = handleSheets(); write('UI_FE_Final_Handle', hs.a, hs.sa); write('UI_FE_Final_HandleGlow', hs.g, hs.sg);
write('UI_FE_Final_Rosette', rosette());
write('UI_FE_Final_LatchBolt', latchBolt());
const bg = lockBg(); write('UI_FE_Final_LockBg', bg);
write('UI_FE_Final_DragArrow', dragArrow());
write('UI_FE_Final_MouseL', mouseIcon());
const paper = letterPaper(); write('UI_FE_Final_Letter', paper);
const dg = letterDigits(); write('UI_FE_Final_LetterDigits', dg.cv, dg.sprites);
write('UI_FE_Final_EnvBack', envBack()); write('UI_FE_Final_EnvFront', envFront());
write('UI_FE_Final_EnvFlapClosed', envFlap(false)); write('UI_FE_Final_EnvFlapOpen', envFlap(true));
console.log((PREVIEW ? '[önizleme] ' : '') + written.join('\n'));
module.exports = { BODY, BG, HANDLE, LETTER, ENVL, P };
