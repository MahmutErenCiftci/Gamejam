// (18 Eki, P-FLOOD) Devrilen ağacın I sokağı ön duvarını (Wall_I_Front) kırdığı yer: dilim 6..14 (gx ~48.9..53.1) için
// kırık duvar dilimleri. Orijinal dilimlerin piksellerinden türetilir (aynı renk, aynı pivot): ortada gövdenin geçtiği
// yer (kökün ekrandaki genişliği, ~2,5 hücre) neredeyse tabana kadar yıkık, kenarlar tırtıklı, kırık yüzeyde kaba beton, önde/üstte moloz parçaları.
// node wall_break.js [--preview]  → Assets/Art/FloodEvening/ENV/Lines/LINE_FE_Wall_I_Front_Broken.png (+ .meta)
// Sahnede: FE_WallBreak (build_scene.cs / patch_scene.cs) — ağaç düşünce bu dilimler görünür, sağlam dilimler gizlenir.
const C = require('./common.js');
const { fs, Cv, hash, OL } = C;

const SRC = C.DST + '/Lines/LINE_FE_Wall_I_Front.png';
const NAME = 'LINE_FE_Wall_I_Front_Broken';
const OUTF = C.DST + '/Lines/' + NAME + '.png';
const K0 = 6, K1 = 14, SW = 16, PADB = 10;       // dilim aralığı, dilim genişliği, alta moloz payı
const CROSS = 70, LOW = 40, TRANS = 22;          // kökün ortası (yerel sütun, dilim 6'nın solundan; gx ~50.8), düz yıkık yarı genişlik, tırtıklı geçiş

const meta = fs.readFileSync(SRC + '.meta', 'utf8');
const rects = {};
{ const re = /name: LINE_FE_Wall_I_Front_(\d+)\s*\n\s*rect:\s*\n\s*serializedVersion: 2\s*\n\s*x: (\d+)\s*\n\s*y: (\d+)\s*\n\s*width: (\d+)\s*\n\s*height: (\d+)[\s\S]*?pivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}/g; let r;
  while ((r = re.exec(meta))) rects[+r[1]] = { x: +r[2], y: +r[3], w: +r[4], h: +r[5], px: +r[6], py: +r[7] }; }
const src = Cv.load(SRC);
const X0 = rects[K0].x, W = (K1 - K0 + 1) * SW, H = src.h + PADB;
// çalışma tuvali: kaynak sayfanın X0..X0+W sütunları, altta PADB boş satır (y-aşağı koordinatlar aynı kalır)
const cv = new Cv(W, H);
for (let y = 0; y < src.h; y++) for (let x = 0; x < W; x++) { const c = src.get(X0 + x, y); if (c) cv.set(x, y, c, c[3]); }
const orig = new Cv(W, H); orig.d.set(cv.d);

// sayfada taban kontur satırı (y-aşağı): dilim 9'dan ölçüldü (x 144 → satır 125, sağa her 2 px'te 1 satır yukarı)
const baseRow = x => Math.round(197 - (X0 + x) / 2);
const FULL = 31;                                  // sağlam duvarın tabandan üst kontura yüksekliği (px)

// kalan yükseklik profili (tabandan px): ortada 1..4 px kütük, kenarlara doğru tırtıklı basamaklarla sağlam yüksekliğe
const hk = [];
for (let x = 0; x < W; x++) {
  const d = Math.abs(x - CROSS);
  const n = hash(Math.floor(x / 3), 17, 5), n2 = hash(Math.floor(x / 2), 29, 9);
  let v;
  if (d < LOW) v = 2 + Math.round(n * 3);
  else if (d < LOW + TRANS) { const t = (d - LOW) / TRANS; v = Math.round(5 + t * t * 22 + (n - 0.5) * 7 + (n2 - 0.5) * 3); }
  else v = 99;
  hk.push(Math.max(1, v));
}
// kırık sütunları kur
const ROUGH = ['#B9AD93', '#A0957D', '#8C887E', '#C9C1AE'];
for (let x = 0; x < W; x++) {
  if (hk[x] >= FULL - 7) continue;                  // üst başlık neredeyse sağlam: dokunma
  const b = baseRow(x), top = b - hk[x];            // kalan ön yüzün üst satırı
  for (let y = 0; y < top - 5; y++) cv.d[(y * W + x) * 4 + 3] = 0;
  // kırık üst yüzey (yukarıdan görülen kaba beton iç), 5 satır; ön kenarı koyu
  for (let y = top - 5; y < top; y++) { if (!orig.a(x, y) && !orig.a(x, top)) continue; const r = hash(x, y, 3); cv.set(x, y, ROUGH[r < 0.45 ? 0 : r < 0.75 ? 1 : r < 0.92 ? 2 : 3]); }
  cv.set(x, top, '#706C63');
  if (hash(x, 3, 11) < 0.35) cv.set(x, top + 1, '#8C887E');
}
// kırık kenarlarda çatlaklar (sağlam kısmın ön yüzünde, kırığa doğru inen koyu çizgiler)
for (const [sx, dir] of [[CROSS - LOW - TRANS - 1, -1], [CROSS + LOW + TRANS, 1], [CROSS - LOW - TRANS + 6, -1], [CROSS + LOW + TRANS - 5, 1]]) {
  let x = sx, y = baseRow(sx) - FULL + 9 + Math.round(hash(sx, 1, 2) * 4);
  for (let i = 0; i < 9; i++) { if (cv.a(x, y)) cv.set(x, y, '#706C63'); y += 1; if (hash(sx, i, 7) < 0.5) x += dir; }
}
// oyulan kenarlara kontur: önceden dolu, şimdi boş ve dolu komşusu olan pikseller
{ const add = [];
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    if (cv.a(x, y) || !orig.a(x, y)) continue;
    if (cv.a(x - 1, y) === 255 || cv.a(x + 1, y) === 255 || cv.a(x, y + 1) === 255 || cv.a(x, y - 1) === 255) add.push([x, y]);
  }
  for (const [x, y] of add) cv.set(x, y, OL); }

// moloz: küçük beton/sıva parçaları (üstü açık, gövdesi sıva, altı koyu, mürekkep konturlu)
function chunk(x0, y0, w, h, seed) {
  const pts = [];
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    if ((x === 0 || x === w - 1) && (y === 0 || y === h - 1) && hash(x0 + x, y0 + y, seed) < 0.6) continue; // köşeleri kırp
    pts.push([x0 + x, y0 + y, y === 0 ? (hash(x, seed, 1) < 0.5 ? '#E9E2D0' : '#C9C1AE') : y === h - 1 ? '#706C63' : (hash(x, y, seed) < 0.7 ? '#B9AD93' : '#A0957D')]);
  }
  const has = new Set(pts.map(p => p[0] + ',' + p[1]));
  for (const [x, y] of pts) for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) if (!has.has((x + dx) + ',' + (y + dy))) cv.set(x + dx, y + dy, OL);
  for (const [x, y, c] of pts) cv.set(x, y, c);
}
const RUB = []; // [yerel x, tabana göre satır (+ = önde/aşağı), w, h] — kırığın boyunca, çoğu önde (çimde)
for (let i = 0; i < 26; i++) { const x = Math.round(CROSS - LOW - 12 + hash(i, 1, 3) * (2 * LOW + 24)); const dy = Math.round(-2 + hash(i, 2, 5) * 12); const w = 2 + Math.round(hash(i, 3, 7) * 3); RUB.push([x, dy, w, w > 3 && hash(i, 4, 9) < 0.6 ? 3 : 2]); }
RUB.forEach(([x, dy, w, h], i) => chunk(x, baseRow(x) + dy - h + 1, w, h, 40 + i));

// dilimlere böl (orijinal dilimlerle aynı x ve pivot noktası; alta PADB satır eklenir)
const sprites = [];
for (let k = K0; k <= K1; k++) {
  const r = rects[k]; const lx = r.x - X0;
  sprites.push({ name: NAME + '_' + k, x: lx, y: r.y, w: r.w, h: r.h + PADB, px: r.px, py: +((r.py * r.h + PADB) / (r.h + PADB)).toFixed(6) });
}
// rect y'leri tuval tabanından (y-yukarı): kaynak y + PADB - PADB = kaynak y (tuval alttan PADB büyüdü)
cv.save(OUTF);
fs.writeFileSync(OUTF + '.meta', C.metaText(C.guidFor(OUTF + '.meta'), NAME, W, H, sprites));
console.log(NAME, W + 'x' + H, sprites.length, 'dilim', 'profil', hk.map(v => v > 40 ? '#' : v).join(','));

if (process.argv.includes('--preview')) {
  // önizleme: çim zemin üstünde sağlam (üst) ve kırık (alt) hali, 4x
  const S = 4, PW = W * S, PH = (H * 2) * S, pv = new Cv(PW, PH);
  for (let y = 0; y < PH; y++) for (let x = 0; x < PW; x++) pv.set(x, y, y < PH / 2 ? '#2E4A2C' : '#2E4A2C');
  const draw = (c, oy) => { for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const p = c.get(x, y); if (p) for (let j = 0; j < S; j++) for (let i = 0; i < S; i++) pv.set(x * S + i, (y + oy) * S + j, p); } };
  draw(orig, -40); draw(cv, H - 40);
  pv.save(__dirname + '/shots/wall_break_preview.png'); console.log('preview ok');
}
