// SEL DURUMU göstergesi: el yapımı piksel "taşkın ölçüm direği" sprite sayfası.
// node tools/sel_gauge/gauge.js [önizlemeKlasörü]
//   → Assets/Resources/UI/FloodGauge/UI_FE_FloodGauge.png (+ .meta, GUID korunur)
// Sayfa düzeni (y aşağı; FloodGaugeView aynı sabitleri kullanır):
//   [0..43]   direk: asma kayışlar, dikey ölçek latası (0 / 0,5 / 1,0 / 1,5 m), sarı-kırmızı tehlike bölgesi,
//             ikaz çizgili tepe, beton/taş ayak (plaka bu sprite'ta YOK, ayrı çizilir)
//   [44..263] 5 tabela plakası (44x30): amber, turuncu, kırmızı, açık kırmızı (nabız), yeşil (güvende)
//   [264..283] aşama kurdelesi (20x14, 9 dilim: sol 7 / sağ 4 / üst-alt 4)
const C = require('../sel_aksam_harita/common.js');
const { Cv, OL, fs, path, PROJ } = C;

const W = 44, H = 136, PW = 44, PH = 30, RW = 20, RH = 14;
const ZERO = 125, SPAN = 78;               // 0 m çizgisi ve 1,5 m'ye kadar piksel
const STAFF_X0 = 11, STAFF_X1 = 32, STAFF_TOP = 36, STAFF_BOT = 127;
const lv = p => ZERO - Math.round(p * SPAN);

const pal = {
  cream: '#F4EBD8', creamHi: '#FFFBF0', creamSh: '#DDCFB2', creamSh2: '#BFB197',
  amberZone: '#F6C67D', amberZoneSh: '#D9A660', redZone: '#F08A7A', redZoneSh: '#CF6A5C',
  red: '#E8392B', redDk: '#7A140C', ink: OL,
  steel: '#6E7891', steelHi: '#A3ACBF', steelDk: '#454E66',
  stone: '#5A6378', stoneHi: '#7A849A', stoneHi2: '#A3ACBF', stoneDk: '#3A4256',
  grass: '#3E7A3A', grassHi: '#5FA34F',
};

// 3x5 rakam + virgül (ölçek etiketleri)
const G = {
  '0': ['###', '#.#', '#.#', '#.#', '###'], '1': ['.#', '##', '.#', '.#', '.#'], '5': ['###', '#..', '##.', '..#', '##.'],
  ',': ['.', '.', '.', '#', '#'], 'm': ['.....', '####.', '#.#.#', '#.#.#', '#.#.#'],
};
function txt(cv, s, x, y, col) { for (const ch of s) { const g = G[ch]; g.forEach((r, j) => [...r].forEach((c, i) => { if (c === '#') cv.set(x + i, y + j, col); })); x += g[0].length + 1; } }
const txtW = s => [...s].reduce((a, ch) => a + G[ch][0].length + 1, -1);

// ------------------------------------------------------------------ direk
function post() {
  const cv = new Cv(W, H);
  // asma kayışları (plakanın altından lata tepesine)
  for (const bx of [15, 27]) {
    for (let y = 27; y <= STAFF_TOP + 2; y++) { cv.set(bx - 1, y, pal.ink); cv.set(bx, y, pal.steelHi); cv.set(bx + 1, y, pal.steel); cv.set(bx + 2, y, pal.ink); }
    cv.set(bx, STAFF_TOP + 1, pal.steelDk); cv.set(bx + 1, STAFF_TOP + 1, pal.steelDk);
  }
  // lata gövdesi
  for (let y = STAFF_TOP; y <= STAFF_BOT; y++)
    for (let x = STAFF_X0; x <= STAFF_X1; x++) {
      const edge = x === STAFF_X0 || x === STAFF_X1 || y === STAFF_TOP || y === STAFF_BOT;
      if (edge) { cv.set(x, y, pal.ink); continue; }
      // bölge rengi: %90–100 kırmızı, %75–90 sarı, altı krem
      let base = pal.cream, sh = pal.creamSh;
      if (y <= lv(0.9) && y >= lv(1)) { base = pal.redZone; sh = pal.redZoneSh; }
      else if (y <= lv(0.75) && y > lv(0.9)) { base = pal.amberZone; sh = pal.amberZoneSh; }
      let c = base;
      if (x === STAFF_X0 + 1) c = y < lv(0.75) ? base : pal.creamHi;          // sol ışık
      if (x >= STAFF_X1 - 2) c = sh;                                         // sağ gölge
      if (x === STAFF_X1 - 1) c = y < lv(0.75) && y >= lv(1) ? sh : pal.creamSh2;
      cv.set(x, y, c);
    }
  // tepe: 1,5 m üstü ikaz çizgili şapka (kırmızı / krem çapraz)
  for (let y = STAFF_TOP + 1; y < lv(1) - 1; y++)
    for (let x = STAFF_X0 + 1; x < STAFF_X1; x++) cv.set(x, y, ((x + y) >> 2) % 2 ? pal.red : pal.cream);
  for (let x = STAFF_X0 + 1; x < STAFF_X1; x++) cv.set(x, lv(1) - 1, pal.ink);
  // E deseni: her 0,1 m kısa çizgi, 0,5 m'de uzun kalın çizgi + etiket
  for (let i = 0; i <= 15; i++) {
    const y = lv(i / 15);
    const major = i % 5 === 0;
    const len = major ? 9 : i % 5 === 0 ? 6 : (i % 2 ? 4 : 5);
    for (let x = STAFF_X0 + 1; x < STAFF_X0 + 1 + len; x++) { cv.set(x, y, pal.ink); if (major && i > 0) cv.set(x, y + 1, pal.ink); }
    if (i === 0) for (let x = STAFF_X0 + 1; x < STAFF_X1; x++) cv.set(x, y, pal.ink);
  }
  // E'nin dikey sırtı (0,1'lik bloklar arasında)
  // gölge: latanın sağında yarı saydam 1 px
  for (let y = STAFF_TOP + 1; y <= STAFF_BOT; y++) cv.set(STAFF_X1 + 1, y, pal.ink, 110);
  // etiketler: 0,5 / 1,0 mürekkep, 1,5 kırmızı
  const labels = [['0,5', 1 / 3, pal.ink], ['1,0', 2 / 3, pal.ink], ['1,5', 1, pal.redDk]];
  for (const [s, p, col] of labels) txt(cv, s, STAFF_X1 - 2 - txtW(s), lv(p) + 3, col);
  txt(cv, 'm', STAFF_X1 - 2 - txtW('m'), lv(0) - 7, pal.creamSh2);
  // vida başları
  for (const [x, y] of [[STAFF_X0 + 3, STAFF_TOP + 2], [STAFF_X1 - 4, STAFF_TOP + 2]]) { cv.set(x, y, pal.steelHi); cv.set(x + 1, y, pal.steel); }
  // beton / taş ayak
  const fy0 = 126, fy1 = 133;
  for (let y = fy0; y <= fy1; y++) {
    const inset = Math.max(0, fy0 + 1 - y) + (y === fy1 ? 1 : 0);
    for (let x = 6 + inset; x <= 37 - inset; x++) {
      const edge = y === fy1 || x === 6 + inset || x === 37 - inset || y === fy0;
      // lata betona gömülü: ayak latanın önünde çizilir
      let c = edge ? pal.ink : pal.stone;
      if (!edge) {
        const k = C.hash(x >> 1, y >> 1, 7);
        if (y === fy0 + 1) c = pal.stoneHi;
        if (k > 0.72) c = pal.stoneHi; else if (k < 0.18) c = pal.stoneDk;
        if (y === fy1 - 1) c = pal.stoneDk;
        if ((x === 12 || x === 25 || x === 33) && y > fy0 + 1 && y < fy1 - 1) c = pal.stoneDk; // taş araları
        if ((x === 9 || x === 19 || x === 29) && y === fy0 + 2) c = pal.stoneHi2;
      }
      cv.set(x, y, c);
    }
  }
  // ot tutamları
  for (const [x, dir] of [[7, -1], [9, 1], [35, 1], [33, -1]]) {
    cv.set(x, fy0 - 1, pal.grass); cv.set(x, fy0 - 2, pal.grassHi); cv.set(x + dir, fy0 - 3, pal.grassHi);
  }
  for (const [x] of [[7], [9], [35], [33]]) { cv.set(x - 1, fy0 - 2, pal.ink); }
  return cv;
}

// ------------------------------------------------------------------ plaka (44x30)
function plate(light, main, dark) {
  const cv = new Cv(PW, PH);
  const rows = PH - 3; // son 2 satır yarı saydam gölge + 1 boş
  for (let y = 0; y < rows; y++)
    for (let x = 0; x < PW; x++) {
      const cut = (y === 0 || y === rows - 1) ? 2 : (y === 1 || y === rows - 2) ? 1 : 0;
      if (x < cut || x >= PW - cut) continue;
      const edge = y === 0 || y === rows - 1 || x === cut || x === PW - 1 - cut;
      let c = edge ? pal.ink : main;
      if (!edge) {
        if (y === 1) c = light;
        if (x === 1 && y > 1) c = light;
        if (y >= rows - 3) c = dark;
        if (x === PW - 2 && y > 1) c = dark;
      }
      cv.set(x, y, c);
    }
  // alt gölge
  for (let x = 3; x < PW - 3; x++) cv.set(x, rows, pal.ink, 110);
  // perçinler (2x2)
  for (const [x, y] of [[3, 3], [PW - 5, 3], [3, rows - 6], [PW - 5, rows - 6]]) {
    cv.set(x, y, pal.steelHi); cv.set(x + 1, y, pal.steel); cv.set(x, y + 1, pal.steel); cv.set(x + 1, y + 1, pal.steelDk);
  }
  // rakam bölmesi: hafif gömme çizgi (yüzde ile süre arasında)
  for (let x = 8; x < PW - 8; x++) { cv.set(x, 16, dark); cv.set(x, 17, light); }
  return cv;
}

// ------------------------------------------------------------------ kurdele (20x14, 9 dilim)
function ribbon() {
  const cv = new Cv(RW, RH);
  const navy = '#1C2742', navyHi = '#2F4170', amber = '#F2A33A', amberDk = '#B86E1E';
  for (let y = 0; y < RH - 1; y++)
    for (let x = 0; x < RW; x++) {
      // sol uç kırlangıç kuyruğu: ortada 4 px çentik
      const mid = (RH - 2) / 2, notch = Math.max(0, 4 - Math.round(Math.abs(y - mid) * 0.9));
      if (x < notch) continue;
      const edge = y === 0 || y === RH - 2 || x === notch || x === RW - 1;
      let c = edge ? pal.ink : navy;
      if (!edge) { if (y === 1) c = amber; else if (y === RH - 3) c = amberDk; else if (y === 2) c = navyHi; }
      cv.set(x, y, c);
    }
  for (let x = 3; x < RW; x++) cv.set(x, RH - 1, pal.ink, 110);
  return cv;
}

const sheet = new Cv(W + 5 * PW + RW, H);
sheet.blit(post(), 0, 0);
const plates = [
  ['#FFD27E', '#F2A33A', '#B86E1E'], // amber (normal)
  ['#FFAE7A', '#E8692B', '#8A3412'], // turuncu (%75+)
  ['#FF8A70', '#E8392B', '#7A140C'], // kırmızı
  ['#FFB09A', '#FF6A50', '#A3241A'], // kırmızı nabız (açık)
  ['#8FF0B0', '#3CC777', '#0A6B34'], // yeşil (güvende)
];
plates.forEach((p, i) => sheet.blit(plate(...p), W + i * PW, 0));
sheet.blit(ribbon(), W + 5 * PW, 0);

const dir = PROJ + '/Assets/Resources/UI/FloodGauge';
const file = dir + '/UI_FE_FloodGauge.png';
sheet.save(file);
const metaPath = file + '.meta';
const base = 'UI_FE_FloodGauge';
fs.writeFileSync(metaPath, C.metaText(C.guidFor(metaPath), base, sheet.w, sheet.h, [{ name: base + '_0', px: 0.5, py: 0.5 }]));
console.log('wrote', file, sheet.w + 'x' + sheet.h);

// önizleme: 6x büyütülmüş, koyu zemin + örnek su
const prevDir = process.argv[2];
if (prevDir) {
  const k = 6, bg = [40, 52, 60];
  const pv = new Cv(sheet.w * k, sheet.h * k);
  for (let y = 0; y < sheet.h; y++) for (let x = 0; x < sheet.w; x++) {
    const c = sheet.get(x, y); let o = bg;
    if (c) { const a = c[3] / 255; o = [0, 1, 2].map(i => bg[i] + (c[i] - bg[i]) * a); }
    for (let yy = 0; yy < k; yy++) for (let xx = 0; xx < k; xx++) pv.set(x * k + xx, y * k + yy, o);
  }
  pv.save(path.join(prevDir, 'gauge_sheet_x6.png'));
  console.log('preview', path.join(prevDir, 'gauge_sheet_x6.png'));
}
