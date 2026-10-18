// Apartman finali — dünya görselleri (cephe, kapı kanadı kareleri, hol, kol/tuş takımı, posta kutusu, lamba, paspas, saksılar, ışık).
//   node build_world_art.js            → Assets/Art/FloodEvening/Final/World/*.png + .meta, out/manifest.json
// Cephe (PROP_FE_Apt_Facade) eski PROP_Street_Facade_Home'un piksellerinden türetilir (aynı boyut + pivot; harita layout.js'te
// houseX(59,-1) yerine konur): giriş katı yeniden çizilir, kapı boşluğu saydamdır. Diğer her şey FE_ApartmentFinal prefabındadır;
// konumlar cephe çapasına (u=0,d=0,h=0 = ızgara (59,-1)) göre manifest.json'da (yerel dünya ofseti, sıralama, ışıksız mı).
// Sıralama (Default katmanı): cephe -50 · Cenk kasada -51 · kasa içi -52 · kol/tuş -53 · cam ışığı -54 · kanat -55 · Cenk içeride -56 · hol -57 · gövde -62
'use strict';
const fs = require('fs'), path = require('path');
const { Layer, rgb, clamp, shade, lerpC, hash, C } = require('./iso.js');
const ROOT = path.resolve(__dirname, '../../..').split(path.sep).join('/');
const DST = process.env.ART_OUT || (ROOT + '/Assets/Art/FloodEvening/Final/World');   // denemede ART_OUT=geçici klasör
const OUT = __dirname + '/out';
fs.mkdirSync(DST, { recursive: true }); fs.mkdirSync(OUT, { recursive: true });

// ------------------------------------------------------------------ çalışma tuvali (cephe çapası = (OX, OY))
const W = 230, H = 290, OX = 46, OY = 236;
const FAC_SRC = ROOT + '/Assets/Art/Props/Street/PROP_Street_Facade_Home.png';
const FAC_PIV = [9, 197];                     // eski cephenin pivotu (y-aşağı sürekli) = ızgara (59,-1)
const FAC_DX = OX - FAC_PIV[0], FAC_DY = OY - FAC_PIV[1];
const newL = () => new Layer(W, H, OX, OY);

// ------------------------------------------------------------------ ölçüler (u, d hücre; h piksel)
const G = {
  uL: 0.5625, uR: 1.4375,          // kapı boşluğu (28 px)
  hB: 1, hT: 65,                    // boşluk alt/üst
  dLeaf: 0.22,                      // kanat düzlemi (girinti derinliği)
  jamb: 0.11, lintel: 6,            // söve kalınlığı (u), lento (px)
  canopy: { u0: 0.36, u1: 1.66, d0: -0.46, h0: 73, h1: 78 },
  mail: { u0: 2.12, u1: 2.86, d0: -0.13, h0: 29, h1: 51, cols: 3, rows: 2 },
  lamp: { u: 0.72, d: -0.30, h: 63 },   // saçağın altında asılı fener
  plate: { u0: 0.12, u1: 0.42, h0: 44 },
  mat: { u0: 0.60, u1: 1.40, d0: -0.48, d1: -0.08 },
  hall: { dBack: 2.3, uLw: 0.25, uRw: 1.95, hC: 80 },
};
G.w = G.uR - G.uL;
const TARGET = { col: 0, row: 0 };          // Cenk'in posta kutusu (alt sıra, sol: Cenk sağında durur, kapak görünür kalır)

// ------------------------------------------------------------------ renkler
const OL = '#14141F';
const STUCCO = [177, 156, 118], ST_L = [191, 171, 134], ST_D = [151, 131, 95], ST_DD = [138, 119, 86];
const STONE = [166, 169, 173], STONE_L = [201, 198, 189], STONE_D = [115, 119, 124], STONE_DD = [78, 81, 86];
const CONC_T = [168, 171, 174], CONC_F = [142, 146, 151], CONC_S = [110, 114, 119];
const stucco = (x, y) => { const r = hash(x * 3 + 11, y * 5 + 7); const r2 = hash(Math.floor(x / 2) + 101, Math.floor(y / 2) + 37); return r < 0.05 ? ST_L : (r > 0.975 ? ST_D : (r2 > 0.985 ? ST_DD : STUCCO)); };

// ------------------------------------------------------------------ yardımcılar
const files = [];
function writeSprite(sub, name, w, h, rgba, sprites) {
  const dir = sub ? DST + '/' + sub : DST; fs.mkdirSync(dir, { recursive: true });
  const file = dir + '/' + name + '.png'; fs.writeFileSync(file, C.encodePng(w, h, rgba));
  fs.writeFileSync(file + '.meta', C.metaText(C.guidFor(file + '.meta'), name, w, h, sprites)); files.push(file);
}
const manifest = { anchor: { gx: 59, gy: -1 }, ppu: 64, items: {} };
// Tek kare: pivot sol-alt; yerel konum = kırpmanın sol-alt köşesi
function emit(name, layer, opt = {}) {
  const r = opt.rect || layer.bbox(); if (!r) { console.log('BOŞ', name); return; }
  const rgba = layer.crop(r);
  let px = 0, py = 0, lx = (r.x - OX) / 64, ly = (OY - (r.y + r.h)) / 64;
  if (opt.pivotAt) { const [X, Y] = opt.pivotAt; px = +((X - r.x) / r.w).toFixed(6); py = +(((r.y + r.h) - Y) / r.h).toFixed(6); lx = (X - OX) / 64; ly = (OY - Y) / 64; }
  writeSprite(opt.sub, name, r.w, r.h, rgba, [{ name: name + '_0', x: 0, y: 0, w: r.w, h: r.h, px, py }]);
  manifest.items[name] = { file: (opt.sub ? opt.sub + '/' : '') + name + '.png', sprites: [name + '_0'], local: [+lx.toFixed(5), +ly.toFixed(5)], order: opt.order || 0, unlit: !!opt.unlit, rect: r };
  return r;
}
// Çok kare (aynı kırpma): yan yana dizilir
function emitFrames(name, layers, opt = {}) {
  let r = null; for (const L of layers) { const b = L.bbox(); if (!b) continue; if (!r) r = { ...b }; else { const x1 = Math.max(r.x + r.w, b.x + b.w), y1 = Math.max(r.y + r.h, b.y + b.h); r.x = Math.min(r.x, b.x); r.y = Math.min(r.y, b.y); r.w = x1 - r.x; r.h = y1 - r.y; } }
  if (opt.rect) r = opt.rect;
  const n = layers.length, w = r.w * n, out = new Uint8Array(w * r.h * 4);
  layers.forEach((L, k) => { const c = L.crop(r); for (let y = 0; y < r.h; y++) out.set(c.subarray(y * r.w * 4, (y + 1) * r.w * 4), (y * w + k * r.w) * 4); });
  const names = layers.map((_, k) => name + '_' + k);
  writeSprite(opt.sub, name, w, r.h, out, names.map((nm, k) => ({ name: nm, x: k * r.w, y: 0, w: r.w, h: r.h, px: 0, py: 0 })));
  manifest.items[name] = { file: (opt.sub ? opt.sub + '/' : '') + name + '.png', sprites: names, local: [+((r.x - OX) / 64).toFixed(5), +((OY - (r.y + r.h)) / 64).toFixed(5)], order: opt.order || 0, unlit: !!opt.unlit, rect: r, ...(opt.extra || {}) };
  return r;
}
const P = (L, u, d, h) => L.proj(u, d, h);
// kapı boşluğu (duvar düzlemi d=0) içinde mi? İçerideki katmanlar (kasa, kanat, cam ışığı, hol) buna kırpılır.
const inOpening = (X, Y) => { const u = (X + 0.5 - OX) / 32; const h = OY - 16 * u - (Y + 0.5); return u >= G.uL && u < G.uR && h >= G.hB && h < G.hT; };
const clipToOpening = L => { for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (L.alpha(x, y) && !inOpening(x, y)) L.clear(x, y); return L; };

// =====================================================================================================
// 1) CEPHE (lit, -50): eski cephe + yeni giriş katı, kapı boşluğu saydam
// =====================================================================================================
const fac = newL();
{
  const src = Layer.fromPng(FAC_SRC, 0, 0);
  for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) { const c = src.get(x, y); if (c) fac.put(x + FAC_DX, y + FAC_DY, c); }
  fac.z.fill(1e9);
  const baseY = X => OY - 16 * ((X - OX) / 32);                      // duvar dibi (d=0, h=0)
  const hAt = (X, Y) => baseY(X) - Y;                                // duvar düzleminde yükseklik
  const inWall = (X, Y) => fac.alpha(X, Y) > 0;
  // giriş katını sıva ile yeniden boya: (a) kapı bölgesi, (b) eski pencere (posta kutusu buraya)
  const topA = X => (FAC_DY + 99) - (X - (FAC_DX + 14)) / 2;         // üst pencere denizliğinin hemen altı
  const topB = X => (FAC_DY + 85) - (X - (FAC_DX + 74)) / 2;
  for (let Y = 0; Y < H; Y++) for (let X = 0; X < W; X++) {
    if (!inWall(X, Y)) continue; const sx = X - FAC_DX;
    const h = hAt(X + 0.5, Y + 0.5);
    if (h < 15 || h > 140) continue;                                  // taş kaide ve üst kat kalır
    const inA = sx >= 10 && sx <= 64 && Y + 0.5 > topA(X);
    const inB = sx >= 75 && sx <= 104 && Y + 0.5 > topB(X);
    if (!inA && !inB) continue;
    fac.put(X, Y, h < 18 ? ST_D : stucco(sx, Y - FAC_DY));            // kaidenin üstünde koyu gölge şeridi
  }
  fac.z.fill(1e9);
  // denizlik altını kapatan ince çizgi (kesilen yerlerde)
  for (let sx = 12; sx <= 64; sx++) { const X = sx + FAC_DX, Y = Math.ceil(topA(X)); if (inWall(X, Y)) { const c = fac.get(X, Y - 1); if (c && (c[0] < 190 || c[1] > 160)) fac.put(X, Y, ST_D); } }

  fac.z.fill(1e9);
  // --- söveler + lento (taş, duvardan hafif çıkıntılı), kapı boşluğu, eşik
  const { uL, uR, hB, hT, jamb, lintel } = G;
  const frameFace = (s, t, p) => (t < 0.08 || s < 0.06) ? STONE_L : (s > 0.9 ? STONE_D : STONE);
  // sol söve
  fac.box(uL - jamb, uL, -0.05, 0, hB - 1, hT + lintel, { front: (s, t) => s < 0.25 ? STONE_L : (s > 0.8 ? STONE_D : STONE), left: STONE_D, top: STONE_L });
  // sağ söve
  fac.box(uR, uR + jamb, -0.05, 0, hB - 1, hT + lintel, { front: (s, t) => s < 0.2 ? STONE_L : (s > 0.75 ? STONE_D : STONE), top: STONE_L });
  // lento
  fac.box(uL - jamb, uR + jamb, -0.05, 0, hT, hT + lintel, { front: (s, t) => t < 0.2 ? STONE_L : (t > 0.8 ? STONE_D : STONE), left: STONE_D, top: STONE_L });
  // eşik taşı (zeminde, duvarın önünde)
  fac.box(uL - jamb - 0.04, uR + jamb + 0.04, -0.16, 0, 0, hB, { front: STONE_D, left: STONE_DD, top: STONE });
  // kapı boşluğu: saydam
  fac.quad([uL, 0, hB], [G.w, 0, 0], [0, 0, hT - hB], () => [0, 0, 0, 0]);
  for (let Y = 0; Y < H; Y++) for (let X = 0; X < W; X++) { const o = (Y * W + X) * 4; if (fac.d[o + 3] === 0 && fac.z[Y * W + X] < 1e8) fac.clear(X, Y); }
  fac.z.fill(1e9);
  // girintinin sol iç kenarını ve lento altını koyu çiz (boşluğa bakan kenar)
  // --- saçak (beton tente) + iki ince demir dikme
  const cn = G.canopy;
  fac.box(cn.u0, cn.u1, cn.d0, 0, cn.h0, cn.h1, {
    top: (s, t) => (t < 0.12 ? CONC_F : (hash(s * 40, t * 20) > 0.93 ? STONE_L : CONC_T)),
    front: (s, t) => t < 0.25 ? CONC_T : (t > 0.8 ? CONC_S : CONC_F), left: CONC_S,
  });
  // --- apartman adı levhası (saçağın üstünde): "GÜNEŞ APT."
  // Harfler tek tek, bütün olarak yerleştirilir (sütun sütun kaydırma 3x5 harfleri bozuyordu: N→H, Ü/Ş noktaları kayboluyordu).
  // Her harf, levhanın kendi sütun merkezindeki eğime oturur; levha 12 px: alt kenar 1 + çengel (Ş) 1 + harf 5 + boşluk 1 + nokta 1 + üst pay 2 + kenar 1.
  {
    const txt = 'GÜNEŞ APT.'; const tw = C.textW(txt, 1); const PH = 12;
    const pu0 = (G.uL + G.uR) / 2 - (tw + 6) / 64; const ph0 = cn.h1 + 2;
    const pw = (tw + 6) / 32; // u genişliği (yatay piksel = tw + 6)
    fac.quad([pu0, -0.02, ph0], [pw, 0, 0], [0, 0, PH], (s, t) => (s * (tw + 6) < 1 || s * (tw + 6) >= tw + 5 || t * PH < 1 || t * PH >= PH - 1) ? [40, 44, 52] : [28, 58, 96]);
    const X0 = Math.round(P(fac, pu0, -0.02, 0)[0]) + 3;
    let gx = 0;
    for (const ch of txt) {
      const g = C.text3(ch, 1); const gw = Math.max(1, g.w);
      const Xc = X0 + gx + gw / 2;                         // harf merkezi sütunu
      const u = (Xc - OX) / 32 - 0.02;                      // levha düzleminde (d = -0.02) X = OX + 32(u + 0.02)
      const Yb = OY - 16 * (u - 0.02) - ph0;                // levhanın alt kenarı (sürekli Y)
      const base = Math.round(Yb - 3);                      // harf satırı 4 (alt) → levhanın 3. iç pikseli
      for (const [x, y] of g.px) fac.put(X0 + gx + x, base - 4 + y, [240, 238, 226]);
      gx += (ch === ' ' ? 2 : gw + 1);
    }
  }
  // --- kapı numarası (eski mavi levha, sola-aşağı taşındı)
  {
    const ox = 13, oy = 118, w = 12, h = 19; const tu = G.plate.u0, th = G.plate.h0;
    const [Xt, Yt] = P(fac, tu, -0.01, th + h); const dx = Math.round(Xt) - (ox + FAC_DX), dy = Math.round(Yt) - (oy + FAC_DY);
    for (let y = oy; y < oy + h; y++) for (let x = ox; x < ox + w; x++) { const c = src.get(x, y); if (!c) continue; const blue = c[2] > c[0] + 30 || (c[0] > 230 && c[1] > 230); if (blue) fac.put(x + FAC_DX + dx, y + FAC_DY + dy, c); }
  }
  // --- posta kutusunun gölgesi / montaj plakası (duvarda)
  { const m = G.mail; fac.quad([m.u0 - 0.03, -0.001, m.h0 - 3], [m.u1 - m.u0 + 0.06, 0, 0], [0, 0, m.h1 - m.h0 + 4], () => ST_DD); }
  fac.outline(OL, (X, Y) => { const sx = X - FAC_DX; return sx >= 8 && sx <= 105 && hAt(X + 0.5, Y + 0.5) > 1; });
  // cephe pivotu korunur: eski boyutta kırp
  emit('PROP_FE_Apt_Facade', fac, { rect: { x: FAC_DX, y: FAC_DY, w: 138, h: 198 }, pivotAt: [OX, OY], order: -50 });
}

// =====================================================================================================
// 2) KASA İÇİ (lit, -52): sağ iç yüz + eşik (kanat düzleminin önü)
// =====================================================================================================
{
  const L = newL(); const { uL, uR, hB, hT, dLeaf } = G;
  L.quad([uR, 0, hB], [0, dLeaf, 0], [0, 0, hT - hB], (s, t) => t > 0.96 ? STONE_D : (s < 0.25 ? STONE : lerpC(STONE, STONE_D, 0.35)));
  L.quad([uL, 0, hB], [G.w, 0, 0], [0, dLeaf, 0], (s, t) => (hash(s * 28, t * 6) > 0.9 ? STONE_L : (t > 0.75 ? STONE_D : STONE)));
  emit('PROP_FE_Apt_Recess', clipToOpening(L), { order: -52 });
}

// =====================================================================================================
// 3) KAPI KANADI (lit, -55) + CAM IŞIĞI (unlit, -54): açı kareleri
// =====================================================================================================
const TW = 28, TH = 63;                 // kanat dokusu (s yatay, t yukarıdan aşağı)
const DOOR = { f: [74, 58, 46], fl: [112, 88, 66], fd: [52, 40, 32], fdd: [38, 29, 23], glass: [56, 70, 90], glassL: [92, 108, 130], kp: [34, 38, 48], kpk: [150, 160, 172], kick: [138, 142, 147], kickL: [176, 180, 186] };
function leafTex(inner) {
  // döner: [TW*TH] renkler (null = yok), cam maskesi
  const tex = [], glass = [];
  for (let y = 0; y < TH; y++) for (let x0 = 0; x0 < TW; x0++) {
    const x = inner ? TW - 1 - x0 : x0; let c = DOOR.f; let g = 0;
    const edge = x === 0 || x === TW - 1 || y === 0;
    const inGlass = x >= 4 && x <= 23 && y >= 4 && y <= 34;
    const inPanel = x >= 4 && x <= 23 && y >= 40 && y <= 54;
    if (edge) c = DOOR.fdd;
    else if (inGlass) { g = 1; const k = (x - y * 0.6); c = (k > -10 && k < -6) || (k > -4 && k < -3) ? DOOR.glassL : DOOR.glass; if (x === 4 || y === 4) c = DOOR.fd; }
    else if (inPanel) { c = (x === 4 || y === 40) ? DOOR.fd : (x === 23 || y === 54) ? DOOR.fl : lerpC(DOOR.f, DOOR.fd, 0.25); }
    else if (y >= 57) c = (y === 57) ? DOOR.kickL : DOOR.kick;
    else if (x === 1 || y === 1) c = DOOR.fl;
    else if (x === TW - 2) c = DOOR.fd;
    if (!inner) {
      // tuş takımı kutusu (sağ üst, camın sağ kenarına binen)
      if (x >= 19 && x <= 24 && y >= 21 && y <= 31) { c = DOOR.kp; g = 0; if (x === 19 || y === 21) c = [58, 64, 78]; if (x >= 20 && x <= 23 && y >= 24 && y <= 30 && (x % 2 === 0) && (y % 2 === 0)) c = DOOR.kpk; }
    }
    // kol göbeği (orta kayıt üstünde)
    if (x >= 22 && x <= 24 && y >= 36 && y <= 38) c = inner ? [150, 156, 164] : [96, 100, 108];
    tex.push(c); glass.push(g);
  }
  return { tex, glass };
}
const OUTER = leafTex(false), INNER = leafTex(true);
const ANGLES = [0, 3, 6, 9, 13, 20, 28, 37, 46, 56, 66, 76, 84, 90];
function leafLayer(theta, glowOnly, withHandle) {
  const L = newL(); const r = theta * Math.PI / 180; const { uL, hB, dLeaf, w } = G; const lh = TH + 0.5;
  const U = [w * Math.cos(r), w * Math.sin(r), 0], V = [0, 0, -lh], A = [uL, dLeaf, hB + lh];
  const innerVisible = Math.sin(r) > Math.cos(r);       // 45°'den sonra iç yüz görünür
  const T = innerVisible ? INNER : OUTER;
  L.quad(A, U, V, (s, t) => {
    const x = Math.min(TW - 1, Math.floor(s * TW)), y = Math.min(TH - 1, Math.floor(t * TH)); const i = y * TW + x;
    if (glowOnly) { if (!T.glass[i]) return null; const k = y / TH; return [255, Math.round(214 - 30 * k), Math.round(140 - 40 * k), 255]; }
    let c = T.tex[i];
    if (theta > 0 && !innerVisible) c = shade(c, 1 - 0.35 * Math.sin(r));             // döndükçe hafif kararır
    if (innerVisible) c = shade(c, 0.7 + 0.25 * Math.sin(r));
    return c;
  });
  if (!glowOnly && theta > 0 && withHandle) {
    // açık karelerde kol gövdeye işlenmiş (yukarıda)
    const hy = hB + lh - 37 / TH * lh;
    L.quad([uL + U[0] * (16 / TW), dLeaf + U[1] * (16 / TW), hy + 0.6], [U[0] * (8 / TW), U[1] * (8 / TW), 0], [0, 0, 2], () => innerVisible ? [168, 172, 180] : [200, 205, 212], { bias: -0.01 });
  }
  return L;
}
{
  const leaves = ANGLES.map(a => clipToOpening(leafLayer(a, false, true)));
  const glows = ANGLES.map(a => clipToOpening(leafLayer(a, true, false)));
  const r = emitFrames('PROP_FE_Apt_Door', leaves, { order: -55, extra: { angles: ANGLES } });
  emitFrames('PROP_FE_Apt_DoorGlow', glows, { order: -54, unlit: true, rect: r, extra: { angles: ANGLES } });
}
// kol (kapalı kanat üstünde): yukarı / aşağı
{
  const { uL, hB, dLeaf, w } = G; const lh = TH + 0.5; const hy = hB + lh - 37 / TH * lh; const ux = w / TW;
  const mk = drop => { const L = newL();
    // kol: göbekten (s=23) camın ortasına doğru (s=15) uzanan çubuk; aşağıdayken ucu 3 px düşer
    for (let k = 0; k <= 8; k++) { const u = uL + ux * (23.5 - k); const hh = hy + 0.5 - drop * (k / 8); L.quad([u, dLeaf - 0.004, hh], [ux, 0, 0], [0, 0, 2], () => k === 8 ? [150, 156, 164] : [206, 210, 216]); }
    L.quad([uL + ux * 22, dLeaf - 0.004, hy - 1], [ux * 3, 0, 0], [0, 0, 4], () => [120, 126, 134], { bias: 0.01 });
    return L; };
  emitFrames('PROP_FE_Apt_Handle', [mk(0), mk(3.2)], { order: -53 });
}
// tuş takımı ışığı (ışıksız): kapalı / bekleme / kırmızı / yeşil
{
  const { uL, hB, dLeaf, w } = G; const lh = TH + 0.5; const ux = w / TW; const hv = y => hB + lh - (y + 0.5) / TH * lh;
  const mk = (led, key) => { const L = newL();
    if (led) L.quad([uL + ux * 21, dLeaf - 0.004, hv(22.5) - 0.5], [ux * 2, 0, 0], [0, 0, 1.2], () => led);
    if (key) for (let y = 24; y <= 30; y += 2) for (let x = 20; x <= 23; x += 2) L.quad([uL + ux * x, dLeaf - 0.004, hv(y) - 0.5], [ux, 0, 0], [0, 0, 1], () => key);
    return L; };
  const idle = mk([255, 190, 80], [120, 200, 255]), off = mk([60, 40, 30], null), red = mk([255, 70, 60], [255, 110, 90]), green = mk([90, 255, 120], [140, 255, 170]);
  emitFrames('PROP_FE_Apt_Keypad', [off, idle, red, green], { order: -53, unlit: true });
}

// =====================================================================================================
// 4) HOL (unlit, -57): kapının arkasındaki sıcak, aydınlık giriş holü (ışın izleme: zemin, sağ duvar, arka duvar, merdiven)
// =====================================================================================================
{
  const L = newL(); const { uL, uR, hB } = G; const hl = G.hall;
  const lightP = [(uL + uR) / 2 + 0.2, 1.1, 76];
  const lit = (p, base, n) => { const dx = (p[0] - lightP[0]) * 32, dd = (p[1] - lightP[1]) * 32, dh = p[2] - lightP[2]; const dist = Math.sqrt(dx * dx + dd * dd + dh * dh); const k = clamp(1.22 - dist / 110, 0.62, 1.12, 0); return base.map(v => clamp(Math.round(v * k))); };
  const TILE_A = [255, 228, 170], TILE_B = [222, 140, 78], WALL = [255, 214, 132], WALL_D = [236, 170, 96], BASEB = [180, 100, 52], STEP_T = [255, 210, 140], STEP_F = [214, 142, 80];
  // zemin
  L.quad([hl.uLw, G.dLeaf, hB], [hl.uRw - hl.uLw, 0, 0], [0, hl.dBack - G.dLeaf, 0], (s, t, p) => { const a = (Math.floor(p[0] * 4) + Math.floor(p[1] * 4)) & 1; return lit(p, a ? TILE_A : TILE_B); });
  // sağ duvar (görünür: normal -gx)
  L.quad([hl.uRw, G.dLeaf, hB], [0, hl.dBack - G.dLeaf, 0], [0, 0, hl.hC - hB], (s, t, p) => lit(p, p[2] < hB + 5 ? BASEB : (Math.abs(p[2] - 44) < 0.8 ? WALL_D : shade(WALL, 0.92))));
  // arka duvar (görünür: normal -gy) + iç kapı
  L.quad([hl.uLw, hl.dBack, hB], [hl.uRw - hl.uLw, 0, 0], [0, 0, hl.hC - hB], (s, t, p) => {
    if (p[2] < hB + 5) return lit(p, BASEB);
    if (p[0] > 0.55 && p[0] < 1.05 && p[2] < hB + 60) return lit(p, (p[0] < 0.6 || p[0] > 1.0 || p[2] > hB + 56) ? [150, 96, 58] : [186, 124, 74]);
    return lit(p, WALL);
  });
  // merdiven (sağda, arkaya doğru yükselir)
  for (let i = 0; i < 6; i++) {
    const d0 = 1.0 + i * 0.22, h1 = hB + 7 * (i + 1);
    L.box(1.30, hl.uRw, d0, hl.dBack, hB, h1, { top: (s, t, p) => lit(p, STEP_T), front: (s, t, p) => lit(p, t < 0.15 ? STEP_T : STEP_F), left: (s, t, p) => lit(p, shade(STEP_F, 0.85)) });
  }
  // korkuluk (merdiven kenarında koyu çubuk)
  for (let i = 0; i <= 5; i++) { const d0 = 1.05 + i * 0.22, h1 = hB + 7 * (i + 1); L.box(1.29, 1.31, d0, d0 + 0.03, h1, h1 + 22, { front: [96, 64, 40], left: [80, 52, 32] }); }
  L.box(1.29, 1.31, 1.05, 2.2, hB + 29, hB + 31, { top: [120, 80, 50], left: [96, 64, 40] });
  // tavan lambası (parlak)
  L.box(lightP[0] - 0.08, lightP[0] + 0.08, lightP[1] - 0.08, lightP[1] + 0.08, lightP[2] - 4, lightP[2], { front: [255, 250, 220], left: [255, 236, 190], top: [255, 255, 240] });
  // yalnız kapı boşluğundan görünen bölge yeter: boşluk çevresinde kırp (cephenin arkasında kalır)
  const [ax, ay] = P(L, G.uL - 0.2, 0, hB - 2), [bx, by] = P(L, G.uR + 0.2, 0, G.hT + 4);
  const r = { x: Math.floor(Math.min(ax, bx)), y: Math.floor(Math.min(ay, by)), w: 0, h: 0 }; r.w = Math.ceil(Math.max(ax, bx)) - r.x; r.h = Math.ceil(Math.max(ay, by)) - r.y;
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (x < r.x || x >= r.x + r.w || y < r.y || y >= r.y + r.h) L.clear(x, y);
  emit('PROP_FE_Apt_Interior', clipToOpening(L), { order: -57, unlit: true });
}

// =====================================================================================================
// 5) POSTA KUTUSU (lit, -49) + Cenk'in kutusunun kapağı (kapalı / açık-zarflı / açık-boş, -48) + zarf ucu + vurgu
// =====================================================================================================
const MB = { face: [104, 118, 132], faceL: [134, 148, 162], faceD: [72, 84, 98], top: [150, 162, 176], side: [82, 94, 108], slot: [28, 32, 40], tag: [226, 228, 220], inner: [26, 28, 36], innerL: [44, 48, 58] };
const mailCell = (c, r) => { const m = G.mail; const cw = (m.u1 - m.u0) / m.cols, rh = (m.h1 - m.h0) / m.rows; return { u0: m.u0 + c * cw, u1: m.u0 + (c + 1) * cw, h0: m.h0 + r * rh, h1: m.h0 + (r + 1) * rh, cw, rh }; };
function doorFace(s, t) { // bir kutu kapağı dokusu: kenar, isim etiketi, mektup yarığı, kilit
  if (s < 0.08 || t < 0.08) return MB.faceL; if (s > 0.92 || t > 0.9) return MB.faceD;
  if (t > 0.22 && t < 0.33 && s > 0.18 && s < 0.82) return MB.slot;
  if (t > 0.48 && t < 0.62 && s > 0.2 && s < 0.6) return MB.tag;
  if (t > 0.66 && t < 0.78 && s > 0.72 && s < 0.84) return [196, 170, 90];
  return MB.face;
}
{
  const L = newL(); const m = G.mail;
  // gövde
  L.box(m.u0, m.u1, m.d0, 0, m.h0, m.h1, { top: MB.top, left: MB.side, front: () => MB.faceD });
  for (let c = 0; c < m.cols; c++) for (let r = 0; r < m.rows; r++) {
    if (c === TARGET.col && r === TARGET.row) continue;
    const k = mailCell(c, r); L.quad([k.u0 + 0.01, m.d0 - 0.001, k.h0 + 0.7], [k.cw - 0.02, 0, 0], [0, 0, k.rh - 1.4], (s, t) => doorFace(s, 1 - t));
  }
  // hedef kutunun boşluğu (iç koyu; kapak ayrı katmanda)
  { const k = mailCell(TARGET.col, TARGET.row); L.quad([k.u0 + 0.01, m.d0 - 0.001, k.h0 + 0.7], [k.cw - 0.02, 0, 0], [0, 0, k.rh - 1.4], (s, t) => t > 0.85 || s < 0.12 ? MB.innerL : MB.inner); }
  L.outline(OL);
  emit('PROP_FE_Apt_Mailbox', L, { order: -49 });
  // kapaklar: menteşe solda, dışarı (kameraya) açılır
  const k = mailCell(TARGET.col, TARGET.row);
  const lidL = (theta, env) => { const Lx = newL(); const rr = theta * Math.PI / 180; const lw = k.cw - 0.02;
    if (env) { // zarf: kutunun içinde, biraz eğik
      Lx.quad([k.u0 + 0.05, m.d0 + 0.06, k.h0 + 2], [lw - 0.08, 0, 0], [0, -0.02, k.rh - 5], (s, t) => (s < 0.06 || s > 0.94 || t < 0.08 || t > 0.92) ? [196, 186, 160] : (Math.abs(t - 0.5 - (s - 0.5) * 0.5) < 0.06 ? [210, 200, 176] : [246, 240, 222]), { bias: -0.2 });
    }
    Lx.quad([k.u0 + 0.01, m.d0 - 0.002, k.h0 + 0.7], [lw * Math.cos(rr), -lw * Math.sin(rr), 0], [0, 0, k.rh - 1.4], (s, t) => { const c = doorFace(s, 1 - t); return theta > 45 ? shade(c, 0.78) : c; }, { bias: -0.5 });
    Lx.outline(OL); return Lx; };
  const closed = lidL(0, false), openFull = lidL(105, true), openEmpty = lidL(105, false);
  emitFrames('PROP_FE_Apt_MailLid', [closed, openFull, openEmpty], { order: -48 });
  // kapalı kutunun yarığından görünen zarf ucu (kilit kontrolünden sonra)
  { const Lx = newL(); Lx.quad([k.u0 + 0.06, m.d0 - 0.004, k.h0 + 0.7 + (k.rh - 1.4) * 0.7], [k.cw * 0.55, 0, 0], [0, 0, 2], () => [246, 240, 222]); emit('PROP_FE_Apt_EnvelopeTip', Lx, { order: -47 }); }
  // vurgu (en yakın nokta olunca): kutunun çevresinde açık çizgi (ışıksız)
  { const Lx = newL(); Lx.quad([k.u0 - 0.02, m.d0 - 0.003, k.h0 - 0.5], [k.cw + 0.04, 0, 0], [0, 0, k.rh + 1], () => [255, 236, 160]); const inner = newL(); inner.quad([k.u0 + 0.02, m.d0 - 0.003, k.h0 + 1], [k.cw - 0.04, 0, 0], [0, 0, k.rh - 2], () => [1, 1, 1]);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (inner.alpha(x, y)) Lx.clear(x, y); emit('PROP_FE_Apt_MailHighlight', Lx, { order: -46, unlit: true }); }
}
// tuş takımı vurgusu (kapalı kanadın üstünde)
{
  const { uL, hB, dLeaf, w } = G; const lh = TH + 0.5; const ux = w / TW; const hv = y => hB + lh - (y + 0.5) / TH * lh;
  const Lx = newL(); Lx.quad([uL + ux * 18, dLeaf - 0.006, hv(33)], [ux * 8, 0, 0], [0, 0, hv(19) - hv(33)], () => [255, 236, 160]);
  const inner = newL(); inner.quad([uL + ux * 19, dLeaf - 0.006, hv(32)], [ux * 6, 0, 0], [0, 0, hv(20) - hv(32)], () => [1, 1, 1]);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (inner.alpha(x, y)) Lx.clear(x, y);
  emit('PROP_FE_Apt_LockHighlight', Lx, { order: -46, unlit: true });
}

// =====================================================================================================
// 6) LAMBA (lit gövde -49, ışıksız ampul + hale -47), PASPAS (-89), SAKSILAR (0, pivot tabanda)
// =====================================================================================================
{
  const { u, d, h } = G.lamp; const cn = G.canopy;
  // saçaktan inen kısa çubuk + fener çatısı + fener tabanı (cam ayrı, ışıksız katman)
  const L = newL();
  L.box(u - 0.012, u + 0.012, d - 0.012, d + 0.012, h + 8, cn.h0, { front: [40, 44, 54], left: [30, 34, 42] });
  L.box(u - 0.075, u + 0.075, d - 0.075, d + 0.075, h + 7, h + 9, { top: [86, 92, 104], front: [52, 58, 68], left: [40, 44, 54] });
  L.box(u - 0.06, u + 0.06, d - 0.06, d + 0.06, h - 1, h, { top: [70, 76, 88], front: [44, 48, 58], left: [34, 38, 46] });
  L.outline(OL); emit('PROP_FE_Apt_Lamp', L, { order: -49 });
  const B = newL(); B.box(u - 0.055, u + 0.055, d - 0.055, d + 0.055, h, h + 7, { front: (s, t) => (s > 0.42 && s < 0.58) ? [222, 160, 92] : (t < 0.35 ? [255, 250, 225] : [255, 214, 140]), left: (s, t) => (s > 0.42 && s < 0.58) ? [200, 140, 80] : [255, 196, 120], top: [255, 240, 200] });
  emit('PROP_FE_Apt_LampBulb', B, { order: -47, unlit: true });
}
{
  const m = G.mat; const L = newL();
  L.quad([m.u0, m.d0, 0], [m.u1 - m.u0, 0, 0], [0, m.d1 - m.d0, 0], (s, t) => {
    if (s < 0.06 || s > 0.94 || t < 0.12 || t > 0.88) return [74, 50, 32];
    return (Math.floor(s * 14) & 1) ? [128, 92, 56] : [114, 80, 48];
  });
  emit('PROP_FE_Apt_Mat', L, { order: -89 });
}
function planter(name, uc, dc, variant, sc = 1) {
  const L = newL(); const r = 0.17 * sc, hp = Math.round(10 * sc);
  const POT = variant ? [[150, 154, 160], [116, 120, 126], [92, 96, 102]] : [[176, 98, 66], [142, 74, 50], [112, 58, 40]];
  L.box(uc - r, uc + r, dc - r, dc + r, 0, hp, { top: [58, 44, 34], front: (s, t) => t < 0.15 ? POT[0] : POT[1], left: POT[2] });
  // yapraklar: üst üste yuvarlak kümeler (koyu → açık)
  const LV = [[44, 86, 52], [58, 112, 62], [80, 140, 72], [112, 168, 88]];
  const blobs = variant ? [[0, 0, 14, 7], [-0.07, 0.05, 20, 6], [0.06, -0.04, 22, 6], [0, 0.02, 27, 5], [0.03, -0.02, 31, 3.5]]
                        : [[0, 0, 13, 8], [-0.08, 0.04, 17, 7], [0.07, -0.05, 18, 6], [0.02, 0.0, 23, 6], [-0.03, 0.03, 26, 4]];
  for (let [du, dd, hc, rad] of blobs) {
    const [cx, cy] = P(L, uc + du * sc, dc + dd * sc, hc * sc); rad *= sc;
    for (let y = Math.floor(cy - rad - 1); y <= cy + rad + 1; y++) for (let x = Math.floor(cx - rad * 1.25 - 1); x <= cx + rad * 1.25 + 1; x++) {
      const nx = (x + 0.5 - cx) / (rad * 1.25), ny = (y + 0.5 - cy) / rad; const q = nx * nx + ny * ny; if (q > 1) continue;
      const jag = hash(x * 7 + hc, y * 3 + rad); if (q > 0.75 && jag > 0.55) continue;
      const lv = ny < -0.35 ? 3 : (ny < 0.1 ? 2 : (ny < 0.55 ? 1 : 0)); const c = LV[clamp(lv + (jag > 0.88 ? 1 : 0) - (jag < 0.08 ? 1 : 0), 0, 3)];
      L.put(x, y, c, 255, uc + dc - 0.5 - hc / 100);
    }
  }
  L.outline(OL);
  const [X, Y] = P(L, uc, dc, 0);
  emit(name, L, { pivotAt: [X, Y], order: 0 });
}
planter('PROP_FE_Apt_Plant_A', 0.07, -0.17, 0, 0.72);
planter('PROP_FE_Apt_Plant_B', 3.45, -0.28, 1);

// =====================================================================================================
// 7) IŞIK: zemine taşan ışık şekli (Light2D sprite çerezi, beyaz) + ışıksız sıcak havuz (yarı saydam)
// =====================================================================================================
{
  const { uL, uR } = G; const mk = warm => { const L = newL(); const len = 1.9;
    L.quad([uL - 0.6, -len, 0], [G.w + 1.2, 0, 0], [0, len + G.dLeaf, 0], (s, t, p) => {
      const along = clamp((G.dLeaf - p[1]) / (len + G.dLeaf), 0, 1, 0);               // 0 kapı → 1 uç
      const half = G.w / 2 + 0.06 + along * 0.55, cu = (uL + uR) / 2 + along * 0.05; const side = Math.abs(p[0] - cu) / half;
      if (side > 1) return null; const edge = Math.pow(1 - side, 0.6), fall = Math.pow(1 - along, 1.6);
      const a = Math.round(255 * clamp(edge * fall * (warm ? 0.9 : 1), 0, 1, 0)); if (a < 4) return null;
      return warm ? [255, 200, 120, a] : [255, 255, 255, a];
    }); return L; };
  emit('FX_FE_Apt_LightSpill', mk(false), { order: -88 });
  emit('FX_FE_Apt_SpillGlow', mk(true), { order: -88, unlit: true });
}

// ------------------------------------------------------------------ manifest + çapalar (yerel dünya ofsetleri, cephe çapasına göre)
const wl = (u, d) => [+((u - d) / 2).toFixed(5), +((u + d) / 4).toFixed(5)];
const mc = mailCell(TARGET.col, TARGET.row);
manifest.anchors = {
  // kapıdan içeri yürüyüş: ön → eşik → kanat düzlemi → hol (kapı ortasının biraz sağı; menteşe solda)
  DoorFront: wl(1.05, -0.42), Threshold: wl(1.05, 0.0), LeafPlane: wl(1.10, G.dLeaf + 0.03), Inside: wl(1.42, 0.82),
  // kilit: Cenk sağ sövenin önünde durur (tuş takımı solunda görünür kalır); [E] noktası kapının sağ yarısının önünde
  LockPoint: wl(1.30, -0.30), LockStand: wl(1.40, -0.22), LockPrompt: wl(1.25, G.dLeaf).map((v, i) => i ? +(v + 48 / 64).toFixed(5) : v),
  MailPoint: wl((mc.u0 + mc.u1) / 2, -0.36), MailStand: wl((mc.u0 + mc.u1) / 2 + 0.36, -0.26), MailPrompt: wl((mc.u0 + mc.u1) / 2, G.mail.d0).map((v, i) => i ? +(v + (G.mail.h1 + 6) / 64).toFixed(5) : v),
  CameraFocus: wl(1.45, -0.6).map((v, i) => i ? +(v + 0.55).toFixed(5) : v), DoorLight: wl(1.0, -0.2), LampLight: wl(G.lamp.u, G.lamp.d),
  PlantA: wl(0.07, -0.17), PlantB: wl(3.45, -0.28),
};
manifest.lampHeight = (G.lamp.h + 3) / 64;
manifest.geometry = G;
fs.writeFileSync(OUT + '/manifest.json', JSON.stringify(manifest, null, 1));
console.log('yazıldı:', files.length, 'dosya →', DST);
