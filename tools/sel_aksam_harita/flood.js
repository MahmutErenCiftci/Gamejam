// Akşam sel — 120 sn dinamik su verisi (FloodWaterField + FE_FloodWater.shader).
// Çalıştır: node flood.js   → Assets/Art/FloodEvening/ENV/Flood/FE_FloodData.png (+ küçük efekt sprite'ları)
//
// FE_FloodData: ızgara hücresi başına K×K texel (G sınırları, map.js). Kanallar:
//   R = dolma eşiği t (0..1): ilerleme p > t olunca o noktada su belirir; derinlik = p - t. 255 = hiç dolmaz.
//   G = akış yönü (ızgara uzayında açı, 0..255 → 0..2π), B = akış hızı (0..1), A = 255 su çıkabilir / 0 çıkamaz.
// Bölge eşikleri rotayla ilişkilidir: kısa yol (D/E) ve kanal kıyısı ilk dolar, başlangıç sokağı (A) kaldırım
// kenarından birikir, uzun yol (F→G→H→I) giderek daha geç, rüzgâr koridoru (J) en son; apartman avlusu (AVLU) kuru kalır.
// Duvar ve kaldırım kenarları biraz daha erken dolar; düşük frekanslı gürültü küçük birikintilerin ayrı ayrı doğup
// büyüyerek birleşmesini sağlar.
const C = require('./common');
const M = require('./map');
const { G, R, corridorOf } = M;

const K = 4;
const W = G.gx1 - G.gx0, H = G.gy1 - G.gy0;
const TW = W * K, TH = H * K;
const DST = C.DST + '/Flood';

const isCanal = (gx, gy) => gy >= -9 && gy < -3 && gx < 50;
const isFlood = (gx, gy) => M.inR(gx, gy, 36, 49, -9, 7);
const isBank = (gx, gy) => gy >= -3 && gy < 0 && gx >= -12 && gx < 50;

// --- gürültü (değer gürültüsü, fbm) ---
const hash2 = (x, y) => { let h = (x * 374761393 + y * 668265263 + 1013904223) | 0; h = (h ^ (h >>> 13)) * 1274126177 | 0; h ^= h >>> 16; return ((h >>> 0) % 100000) / 100000; };
function vnoise(x, y) {
  const x0 = Math.floor(x), y0 = Math.floor(y), fx = x - x0, fy = y - y0;
  const s = t => t * t * (3 - 2 * t);
  const a = hash2(x0, y0), b = hash2(x0 + 1, y0), c = hash2(x0, y0 + 1), d = hash2(x0 + 1, y0 + 1);
  return (a + (b - a) * s(fx)) + ((c + (d - c) * s(fx)) - (a + (b - a) * s(fx))) * s(fy);
}
const fbm = (x, y) => (vnoise(x, y) * 0.6 + vnoise(x * 2.1 + 7, y * 2.1 + 3) * 0.3 + vnoise(x * 4.3 + 1, y * 4.3 + 9) * 0.1) * 2 - 1;

// --- bölge tabanı (gürültüsüz) ---
function base(gx, gy) {
  const k = corridorOf(Math.floor(gx), Math.floor(gy));
  const lerp = (a, b, t) => a + (b - a) * Math.max(0, Math.min(1, t));
  switch (k) {
    case 'A': { const row = Math.floor(gy); return [0.12, 0.1, 0.03, 0.3, 0.36][row] ?? 0.36; }
    case 'B': return lerp(0.1, 0.16, (gy - 5) / 12);
    case 'C': return lerp(0.16, 0.2, (gx - 16) / 8);
    case 'FORK': { const cx = (gx - 27) / 4, cy = (gy - 14.5) / 4.5; return 0.2 + 0.05 * Math.max(0, 1 - (cx * cx + cy * cy)); }
    case 'D': return lerp(0.02, 0.18, (gy - 2) / 8);
    case 'E': return 0.0;
    case 'F': return lerp(0.2, 0.26, (gy - 19) / 12);
    case 'G': return lerp(0.26, 0.32, (gx - 30) / 16);
    case 'H': return lerp(0.3, 0.34, (gy - 13) / 13);
    case 'I': return lerp(0.32, 0.38, (gx - 46) / 13);
    case 'J': return lerp(0.44, 0.48, (gy + 1) / 14);
    case 'AVLU': return 1.02;
  }
  if (isBank(gx, gy)) return lerp(0.15, 0.25, (gy + 3) / 3);
  return null;
}
const masked = (gx, gy) => !isCanal(gx, gy) && !isFlood(gx, gy) && base(gx, gy) !== null;

// kenara (yürünemeyen komşuya) uzaklık, hücre cinsinden (0..1 aralığında ilgileniyoruz)
function edgeDist(gx, gy) {
  let best = 1;
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
    for (let s = 0.25; s <= 1; s += 0.25) {
      if (!masked(gx + dx * s, gy + dy * s)) { best = Math.min(best, s - 0.125); break; }
    }
  }
  return Math.max(0, best);
}

// 1) taban alanı
const B = new Float32Array(TW * TH), Mk = new Uint8Array(TW * TH);
for (let j = 0; j < TH; j++) for (let i = 0; i < TW; i++) {
  const gx = G.gx0 + (i + 0.5) / K, gy = G.gy0 + (j + 0.5) / K;
  const o = j * TW + i;
  if (!masked(gx, gy)) { Mk[o] = 0; B[o] = 1.5; continue; }
  Mk[o] = 1;
  B[o] = base(gx, gy);
}
// 2) yumuşatma (bölge geçişlerinde basamak olmasın) — yalnız maske içinde
function blur(src) {
  const out = new Float32Array(src.length);
  for (let j = 0; j < TH; j++) for (let i = 0; i < TW; i++) {
    const o = j * TW + i; if (!Mk[o]) { out[o] = src[o]; continue; }
    let s = 0, n = 0;
    for (let dj = -2; dj <= 2; dj++) for (let di = -2; di <= 2; di++) {
      const ii = i + di, jj = j + dj; if (ii < 0 || jj < 0 || ii >= TW || jj >= TH) continue;
      const q = jj * TW + ii; if (!Mk[q]) continue; s += src[q]; n++;
    }
    out[o] = s / n;
  }
  return out;
}
let S = blur(blur(B));
// 3) akış: yumuşatılmış tabanın eğimi boyunca aşağı (ızgara uzayı)
const FA = new Uint8Array(TW * TH), FS = new Uint8Array(TW * TH);
for (let j = 1; j < TH - 1; j++) for (let i = 1; i < TW - 1; i++) {
  const o = j * TW + i; if (!Mk[o]) continue;
  const at = (ii, jj) => { const q = jj * TW + ii; return Mk[q] ? S[q] : S[o]; };
  const dx = (at(i + 1, j) - at(i - 1, j)) * K / 2, dy = (at(i, j + 1) - at(i, j - 1)) * K / 2;
  let fx = -dx, fy = -dy, mag = Math.hypot(fx, fy);
  const gx = G.gx0 + (i + 0.5) / K, gy = G.gy0 + (j + 0.5) / K;
  const k = corridorOf(Math.floor(gx), Math.floor(gy));
  if (mag < 0.004) { // düz yerde: sokak boyunca hafif akış
    const along = { A: [1, 0], B: [0, -1], C: [-1, 0], F: [0, -1], G: [-1, 0], H: [0, -1], I: [-1, 0], J: [0, 1] }[k] || [1, 0];
    fx = along[0]; fy = along[1]; mag = 0.004;
  }
  const ang = Math.atan2(fy, fx);
  FA[o] = Math.round(((ang + Math.PI * 2) % (Math.PI * 2)) / (Math.PI * 2) * 255);
  FS[o] = Math.round(Math.max(0.15, Math.min(1, mag * 18)) * 255);
}
// 4) son eşik: taban + kenar etkisi + gürültü
const img = new C.Cv(TW, TH);
for (let j = 0; j < TH; j++) for (let i = 0; i < TW; i++) {
  const o = j * TW + i; const row = TH - 1 - j; // PNG satır 0 = üst; Unity v=0 = alt
  if (!Mk[o]) {
    // maske dışı: komşu maskeli texellerin eşiği (çift doğrusal örnekleme kenarda suyu geri itmesin), A = 0
    let s = 0, n = 0;
    for (let dj = -2; dj <= 2; dj++) for (let di = -2; di <= 2; di++) {
      const ii = i + di, jj = j + dj; if (ii < 0 || jj < 0 || ii >= TW || jj >= TH) continue;
      const q = jj * TW + ii; if (Mk[q]) { s += S[q]; n++; }
    }
    const v = n ? Math.round(Math.max(0, Math.min(1, s / n - 0.04)) * 254) : 255;
    img.set(i, row, [v, 0, 0], 1);
    continue;
  }
  const gx = G.gx0 + (i + 0.5) / K, gy = G.gy0 + (j + 0.5) / K;
  let t = S[o];
  const ed = edgeDist(gx, gy);
  if (ed < 0.6) t -= 0.06 * (1 - ed / 0.6);
  t += fbm(gx * 0.85, gy * 0.85) * 0.1 + fbm(gx * 2.7 + 11, gy * 2.7 + 5) * 0.025;
  const v = t >= 1.0 ? 255 : Math.round(Math.max(0, Math.min(1, t)) * 254);
  img.set(i, row, [v, FA[o], FS[o]], 255);
}
img.save(DST + '/FE_FloodData.png');

// ---------------------------------------------------------------- küçük efekt sprite'ları (PPU 64)
const OLc = '#0E2236';
function save(name, cv) { cv.save(DST + '/' + name + '.png'); }
// ayak halkası (izometrik elips), 4 kare büyüyen
for (let f = 0; f < 4; f++) {
  const cv = new C.Cv(28, 12), rx = 5 + f * 3, ry = rx / 2.2, a = [235, 190, 140, 90][f];
  for (let k = 0; k < 360; k += 2) { const r = k * Math.PI / 180; cv.set(14 + Math.cos(r) * rx, 6 + Math.sin(r) * ry, '#CFEFFF', a); }
  save('FX_FE_Ripple_' + f, cv);
}
// suya gömülen ayakların önünde su çizgisi (ön yarım elips + köpük)
{ const cv = new C.Cv(24, 9);
  for (let x = 0; x < 24; x++) { const u = (x - 11.5) / 11.5, y = Math.round(4 + Math.sqrt(Math.max(0, 1 - u * u)) * 3.2); for (let yy = 3; yy <= y; yy++) cv.set(x, yy, yy === y ? '#D8F2FF' : '#3E7FB8', yy === y ? 230 : 185); if (x % 5 === 2) cv.set(x, y - 1, '#FFFFFF', 220); }
  save('FX_FE_FeetWater', cv); }
// sıçrama 3 kare
for (let f = 0; f < 3; f++) {
  const cv = new C.Cv(14, 12);
  const pts = [[[7, 8], [5, 7], [9, 7]], [[7, 5], [4, 6], [10, 6], [2, 8], [12, 8]], [[3, 9], [11, 9], [7, 3], [1, 10], [13, 10]]][f];
  for (const [x, y] of pts) { cv.set(x, y, '#E3F6FF', 240); if (f < 2) cv.set(x, y + 1, '#8CCBF0', 200); }
  save('FX_FE_Splash_' + f, cv);
}
// sürüklenen enkaz
{ const cv = new C.Cv(12, 6); cv.rect(1, 2, 10, 2, '#8A5A34'); cv.rect(1, 2, 10, 1, '#A87346'); cv.set(4, 3, '#5E3B22'); cv.set(8, 3, '#5E3B22'); cv.outline(OLc); save('FX_FE_Debris_Plank', cv); }
{ const cv = new C.Cv(9, 5); cv.rect(1, 1, 5, 3, '#6FB58C'); cv.rect(6, 2, 2, 1, '#6FB58C'); cv.set(2, 1, '#B9F0CF'); cv.outline(OLc); save('FX_FE_Debris_Bottle', cv); }
{ const cv = new C.Cv(13, 6); for (let x = 1; x < 12; x++) cv.set(x, 3 - Math.round(Math.sin(x * 0.7)), '#6B4A2E'); cv.set(5, 1, '#6B4A2E'); cv.set(6, 0, '#4E8A3A'); cv.set(9, 4, '#4E8A3A'); save('FX_FE_Debris_Twig', cv); }
{ const cv = new C.Cv(10, 5); for (const [x, y] of [[2, 2], [3, 1], [4, 2], [5, 2], [6, 1], [7, 2], [3, 3], [6, 3], [1, 3], [8, 3]]) cv.set(x, y, '#EAF7FF', 220); save('FX_FE_Debris_Foam', cv); }
// su baskını bariyeri (kapanan yol): kırmızı-beyaz şeritli bariyer + "SU" levhası, pivot alt orta
{ const cv = new C.Cv(46, 34);
  const leg = (x) => { cv.rect(x, 14, 2, 19, '#B8B8B0'); cv.rect(x, 14, 1, 19, '#DCDCD4'); };
  leg(5); leg(39);
  for (let x = 2; x < 44; x++) for (let y = 16; y < 22; y++) cv.set(x, y, (Math.floor((x + y) / 4) % 2) ? '#D8392B' : '#F2F2EA');
  cv.rect(13, 1, 20, 12, '#F6C67D'); cv.rect(14, 2, 18, 10, '#1B2A44');
  C.drawText(cv, 'SU', 19, 4, '#F6C67D', 1);
  cv.rect(22, 13, 2, 3, '#B8B8B0');
  cv.outline(OLc);
  save('PROP_FE_FloodBarrier', cv); }

console.log('flood: FE_FloodData ' + TW + 'x' + TH + ' (K=' + K + ', gx0=' + G.gx0 + ', gy0=' + G.gy0 + ', W=' + W + ', H=' + H + ') + efekt sprite\'ları → ' + DST);
