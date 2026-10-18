// Akşam sel mahallesi v3 — ZİKZAK. Izgara (gx, gy). Dünya: x=(gx-gy)/2, y=(gx+gy)/4 + h/64. h piksel (yukarı +).
// +gx = ekranda sağ-yukarı, +gy = ekranda sol-yukarı.
// Rota:  A (başlangıç sokağı, +gx) → köşe → B (ara sokak, +gy) → köşe → C (+gx) → KAVŞAK meydanı
//   KISA YOL: D (-gy, aşağı-sağ) → köşe → E (+gx) → YIKINTI (gx 34) → ötesi derin sel (gx 36..49) → güvenli meydan görünür
//   UZUN YOL: F (+gy) → köşe → G (+gx) → köşe → H (-gy) → köşe → I (+gx): AĞAÇ OLAYI + dal → köşe → J: dar RÜZGÂR KORİDORU (-gy) → KORUNAKLI AVLU
const G = { gx0: -26, gx1: 80, gy0: -16, gy1: 44 };
const inR = (gx, gy, x0, x1, y0, y1) => gx >= x0 && gx < x1 && gy >= y0 && gy < y1;

const RUBBLE_GX = 34;           // kısa yol yürüme alanı gx < 34
const R = {                     // yürünebilir dikdörtgenler [x0, x1, y0, y1)
  A: [-10, 16, 0, 5],
  B: [11, 16, 5, 17],
  C: [16, 24, 12, 17],
  FORK: [23, 31, 10, 19],
  D: [25, 30, 2, 10],
  E: [30, RUBBLE_GX, 2, 7],
  F: [25, 30, 19, 31],
  G: [30, 46, 26, 31],
  H: [41, 46, 13, 26],
  I: [46, 59, 13, 18],
  J: [56, 59, -1, 13],          // rüzgâr koridoru: 3 hücre geniş, 14 hücre uzun
  AVLU: [53, 63, -8, -1],       // korunaklı avlu (devam noktası)
};
const inRect = (gx, gy, k) => inR(gx, gy, ...R[k]);
const WALK_KEYS = Object.keys(R);
function corridorOf(gx, gy) { for (const k of WALK_KEYS) if (inRect(gx, gy, k)) return k; return null; }

// Sel suyu bölgeleri
const isCanal = (gx, gy) => gy >= -9 && gy < -3 && gx < 50;                       // sokağın önündeki kanal
const isFlood = (gx, gy) => inR(gx, gy, 36, 49, -9, 7);                          // kısa yolun ötesi: derin akan sel
function H(gx, gy) {
  if (isFlood(gx, gy)) return gx === 36 ? -24 : -40;
  if (isCanal(gx, gy)) return -40;
  return 0;
}
function WALK(gx, gy) { return corridorOf(gx, gy) !== null; }
function VOID(gx, gy) { return gy >= 42; }

function SURF(gx, gy) {
  const k = corridorOf(gx, gy);
  if (k === 'A') {
    return gy === 0 ? (((gx * 7 + gy) % 3) ? 'Road_A' : 'Road_B') : gy === 1 ? 'Road_Lane' : gy === 2 ? 'Road_Gutter' : gy === 3 ? 'Curb' : (((gx * 5) % 4) ? 'Sidewalk_A' : 'Sidewalk_B');
  }
  if (k === 'FORK' || k === 'AVLU') return ((gx + gy) & 1) ? 'PathA' : 'PathB';
  if (k) return 'cobble';
  if (inR(gx, gy, RUBBLE_GX, 36, 2, 7)) return 'cobble~mud';                      // yıkıntının altı (çamurlu)
  if (isFlood(gx, gy) || isCanal(gx, gy)) return 'mud';
  if (gy >= -3 && gy < 0) return 'GrassS~mud';                                     // kanal kıyısı
  return ((gx * 3 + gy * 5) % 7) < 2 ? 'GrassB' : 'GrassS';
}
function FACE(gx, gy) { return isFlood(gx, gy) ? 'concrete' : 'stone'; }
function WATER(gx, gy) {
  if (isFlood(gx, gy)) return 'LowRoad';
  if (isCanal(gx, gy)) return 'Canal';
  return null;
}
const WATER_LEVELS = { Canal: [-30, -21, -12], LowRoad: [-18, -10, -5] };   // Stage1 / Stage2 / Stage3 (px)

// Görsel duvar/korkuluk hatları (block: true → yürüme maskesini de keser)
const LINES = [
  { id: 'Rail_A_Front', kind: 'rail', axis: 'x', at: 0, from: -12, to: 16 },
  { id: 'Wall_A_End', kind: 'wall', axis: 'y', at: 16, from: 0, to: 5 },
  { id: 'Wall_A_Start', kind: 'wall', axis: 'y', at: -10, from: 0, to: 5 },
  { id: 'Wall_B_Left', kind: 'wall', axis: 'y', at: 11, from: 5, to: 17 },
  { id: 'Wall_C_Front', kind: 'wall', axis: 'x', at: 12, from: 16, to: 23 },
  { id: 'Wall_D_Left', kind: 'wall', axis: 'y', at: 25, from: 2, to: 10 },
  { id: 'Rail_E_Front', kind: 'rail', axis: 'x', at: 2, from: 25, to: 36 },
  { id: 'Wall_F_Left', kind: 'wall', axis: 'y', at: 25, from: 19, to: 31 },
  { id: 'Wall_H_Left', kind: 'wall', axis: 'y', at: 41, from: 13, to: 26 },
  { id: 'Wall_I_Front', kind: 'wall', axis: 'x', at: 13, from: 46, to: 56 },
  { id: 'Wall_J_Left', kind: 'wall', axis: 'y', at: 56, from: -1, to: 13 },
  { id: 'HiWall_Avlu_Back', kind: 'hiwall', axis: 'x', at: -1, from: 53, to: 56 },
  { id: 'HiWall_Avlu_Right', kind: 'hiwall', axis: 'y', at: 63, from: -8, to: -1 },
  { id: 'Rail_Avlu_Front', kind: 'rail', axis: 'x', at: -8, from: 53, to: 63 },
  { id: 'Rail_Avlu_Left', kind: 'rail', axis: 'y', at: 53, from: -8, to: -1 },
];

module.exports = { G, H, WALK, VOID, SURF, FACE, WATER, WATER_LEVELS, LINES, RUBBLE_GX, BARRIER_GX: RUBBLE_GX, inR, R, corridorOf };
