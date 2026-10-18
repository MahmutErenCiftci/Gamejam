// Akşam sel — çevre tehlikeleri sprite'ları (FLOOD-MECH, 18 Eki). node hazards.js
// Çıktı (PNG + .meta, GUID'ler yeniden üretimde korunur):
//   Assets/Art/FloodEvening/ENV/Hazards/  direkler, tel açıklıkları (16 px dilimli, her dilimin pivotu teli taşıyan zemin hattında →
//                                         izometrik sıralama dilim dilim doğru), kopuk teller, kıvılcım/ark kareleri, ⚡ uyarı simgesi,
//                                         gökyüzü şimşek çizgileri, enkaz (tahta, kasa, şişe, dal, taş, moloz)
//   Assets/Art/FloodEvening/ENV/Trees/    ağaç salınım kareleri (kaynak ağaç sprite'ından satır kaydırmalı, 1 px ızgarada; sakin + fırtına)
//   out/hazards.json                      tel dilimlerinin yerleşimi (layout.js okur) + üretilen dosya listesi
// Sıra: node terrain.js && node props.js && node hazards.js && node install.js --png
const C = require('./common.js');
const { fs, path, Cv, E, fl, mod, hash, OL } = C;
const D = require('./hazards_def.js');
const { V, box, rbox, cylZ, capsule, ellipsoid, lambert } = E;

const HZ = C.DST + '/Hazards', TR = C.DST + '/Trees';
const REL = 'Assets/Art/FloodEvening/ENV';
fs.mkdirSync(HZ, { recursive: true }); fs.mkdirSync(TR, { recursive: true });
const manifest = { strips: [], files: [] };
const ramp4 = (n, c, bias = 0) => { const l = lambert(n) + bias; return l > 0.85 ? c[0] : l > 0.55 ? c[1] : l > 0.25 ? c[2] : c[3]; };

function writeMeta(file, base, w, h, sprites) {
  const mp = file + '.meta';
  fs.writeFileSync(mp, C.metaText(C.guidFor(mp), base, w, h, sprites));
}
// tek sprite: içeriğe kırp, pivot (tuval pikseli: soldan x, yukarıdan y = taban satırının alt kenarı)
function emitSingle(dir, sub, name, cv, pivX, pivYdown) {
  const c = cv.crop(); const px = pivX - c.x0, pyUp = c.cv.h - (pivYdown - c.y0);
  const file = dir + '/' + name + '.png'; c.cv.save(file);
  const sp = { name: name + '_0', x: 0, y: 0, w: c.cv.w, h: c.cv.h, px: +(px / c.cv.w).toFixed(6), py: +(pyUp / c.cv.h).toFixed(6) };
  writeMeta(file, name, c.cv.w, c.cv.h, [sp]);
  manifest.files.push(REL + '/' + sub + '/' + name + '.png');
  console.log(name, c.cv.w + 'x' + c.cv.h, 'pivot', px, pyUp);
  return sp;
}
// yan yana eşit kareler (çoklu sprite): frames = Cv[], pivot tuval pikseli (her kare için aynı)
function emitSheet(dir, sub, name, frames, pivX, pivYdown) {
  const w = frames[0].w, h = frames[0].h; const sheet = new Cv(w * frames.length, h);
  frames.forEach((f, i) => sheet.blit(f, i * w, 0));
  const file = dir + '/' + name + '.png'; sheet.save(file);
  const sprites = frames.map((f, i) => ({ name: name + '_' + i, x: i * w, y: 0, w, h, px: +(pivX / w).toFixed(6), py: +((h - pivYdown) / h).toFixed(6) }));
  writeMeta(file, name, sheet.w, h, sprites);
  manifest.files.push(REL + '/' + sub + '/' + name + '.png');
  console.log(name, frames.length + ' kare', w + 'x' + h);
}
function rend(objs, W, H, ox, oy, opt = {}) {
  const r = E.render(objs, W, H, ox, oy, Object.assign({ zmax: 130, innerThr: 2.5 }, opt));
  const cv = new Cv(W, H); cv.d.set(r.rgba); return cv;
}
const screen = (gx, gy, z) => [32 * (gx - gy), 16 * (gx + gy) + z];   // px, y yukarı

// ================================================================ ELEKTRİK DİREĞİ (ahşap, travers, 2 insülatör, uyarı levhası)
const WOOD = ['#8C6C4A', '#74583A', '#5C452D', '#45331F'], WOODD = ['#7A5C3E', '#634A31', '#4C3926', '#372818'];
const CER = ['#E6E8E2', '#C3C7BF', '#9DA29A', '#7A7F78'];
function pole(axis) {
  const along = axis === 'x';               // hat gx boyunca → travers gy boyunca
  const arm = (a0, a1, t0, t1, z0, z1) => along ? box(t0, t1, a0, a1, z0, z1) : box(a0, a1, t0, t1, z0, z1);
  const objs = [
    { sdf: cylZ(0, 0, 2.6, 0, 92), mat: (p, n) => { const g = hash(fl(Math.atan2(p.v, p.u) * 3), fl(p.z / 5), 4) < 0.18; return ramp4(n, g ? WOODD : WOOD); } },
    { sdf: arm(-13.5, 13.5, -1.7, 1.7, 82, 85.5), mat: (p, n) => ramp4(n, WOODD, 0.05) },
    { sdf: arm(-5, 5, -1, 1, 76, 82), mat: (p, n) => ramp4(n, ['#5E6A72', '#47525A', '#353E45', '#272E34']) },   // demir payanda
  ];
  for (const s of [-1, 1]) {
    const [cu, cv] = along ? [0, s * 11] : [s * 11, 0];
    objs.push({ sdf: cylZ(cu, cv, 1.5, 85.5, 90.5), mat: (p, n) => ramp4(n, CER, 0.1) });
  }
  // sarı-siyah "yüksek gerilim" levhası (görünen -u yüzünde)
  objs.push({ sdf: box(-3.6, -2.4, -2.2, 2.2, 50, 57), mat: (p, n, c) => {
    if (c.face === 'top') return '#14141F';
    const yy = fl(57 - p.z), xx = fl(p.v + 2.2); if (yy < 1 || yy > 5 || xx < 1 || xx > 3) return '#14141F';
    return (yy === 3 && xx === 2) ? '#14141F' : '#F2C230'; } });
  const W = 70, H = 128, ox = 35, oy = 118;
  const cv = rend(objs, W, H, ox, oy, { shadow: (u, v) => (u * u + v * v < 30 ? 70 : 0) });
  return { cv, ox, oy };
}
const poleSprites = {};
for (const axis of ['x', 'y']) { const p = pole(axis); poleSprites[axis] = emitSingle(HZ, 'Hazards', 'PROP_FE_Pole_' + axis.toUpperCase(), p.cv, p.ox, p.oy); }

// ================================================================ TELLER (dilimli)
// polys: [[ [gx,gy,z], ... ], ...] çizilecek teller; g0 → g1: dilim pivotlarının zemin hattı; color: tel rengi
function wireSheet(name, polys, g0, g1, extraDraw) {
  const pts = polys.map(poly => poly.map(([gx, gy, z]) => { const [x, y] = screen(gx - g0[0], gy - g0[1], z); return [x, y]; }));
  let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
  for (const poly of pts) for (const [x, y] of poly) { x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); }
  const M = 3; const W = Math.ceil(x1 - x0) + 2 * M + 1, H = Math.ceil(y1 - y0) + 2 * M + 1;
  const ox = Math.round(-x0 + M), oy = Math.round(y1 + M);       // O (g0 zemini) tuvalde (ox, oy)
  const cv = new Cv(W, H);
  const plot = (x, y, c, a = 255) => cv.set(Math.round(ox + x), Math.round(oy - y), c, a);
  for (const poly of pts) {
    for (let i = 0; i + 1 < poly.length; i++) {
      const [ax, ay] = poly[i], [bx, by] = poly[i + 1]; const n = Math.max(1, Math.ceil(Math.max(Math.abs(bx - ax), Math.abs(by - ay)) * 2));
      for (let k = 0; k <= n; k++) plot(ax + (bx - ax) * k / n, ay + (by - ay) * k / n, '#191C24');
    }
  }
  if (extraDraw) extraDraw((gx, gy, z, c, a) => { const [x, y] = screen(gx - g0[0], gy - g0[1], z); plot(x, y, c, a); });
  // 16 px sütun dilimleri; pivot = sütun ortasının altındaki zemin hattı noktası
  const dgx = g1[0] - g0[0], dgy = g1[1] - g0[1]; const denom = 32 * (dgx - dgy);
  const file = HZ + '/' + name + '.png'; const sprites = [];
  let k = 0;
  for (let sx = 0; sx < W; sx += 16, k++) {
    const w = Math.min(16, W - sx);
    let ty = 1e9, by = -1; for (let y = 0; y < H; y++) for (let x = sx; x < sx + w; x++) if (cv.a(x, y)) { ty = Math.min(ty, y); by = Math.max(by, y); }
    if (by < 0) continue;
    const cxRel = sx + w / 2 - ox; const t = Math.abs(denom) < 1e-3 ? 0.5 : Math.min(1, Math.max(0, cxRel / denom));
    const yUpRel = 16 * t * (dgx + dgy);
    const pivYdown = oy - yUpRel; const rectY = H - 1 - by, rh = by - ty + 1;
    const pyUp = (H - pivYdown) - rectY;
    const sn = name + '_' + k;
    sprites.push({ name: sn, x: sx, y: rectY, w, h: rh, px: 0.5, py: +(pyUp / rh).toFixed(6) });
    const wx0 = (g0[0] - g0[1]) / 2, wy0 = (g0[0] + g0[1]) / 4;
    manifest.strips.push({ group: 'Hazards/Wires/' + name.replace('WIRE_FE_', ''), name: sn.replace('WIRE_FE_', ''), file: REL + '/Hazards/' + name + '.png', sprite: sn,
      wx: +(wx0 + cxRel / 64).toFixed(4), wy: +(wy0 + yUpRel / 64).toFixed(4) });
  }
  cv.save(file); writeMeta(file, name, W, H, sprites); manifest.files.push(REL + '/Hazards/' + name + '.png');
  console.log(name, W + 'x' + H, sprites.length, 'dilim');
}
const attach = (id, side) => { const [gx, gy, axis] = D.POLES[id]; return axis === 'x' ? [gx, gy + side * D.ARM] : [gx + side * D.ARM, gy]; };
for (const [a, b, sides] of D.SPANS) {
  const pa = D.POLES[a], pb = D.POLES[b]; const len = Math.hypot(pb[0] - pa[0], pb[1] - pa[1]);
  const sag = 7 + len * 1.4;
  const polys = sides.map(s => { const A = attach(a, s), B = attach(b, s); const out = [];
    for (let i = 0; i <= 48; i++) { const t = i / 48; out.push([A[0] + (B[0] - A[0]) * t, A[1] + (B[1] - A[1]) * t, D.WIRE_Z - 1 - sag * 4 * t * (1 - t)]); } return out; });
  wireSheet('WIRE_FE_' + a + '_' + b, polys, [pa[0], pa[1]], [pb[0], pb[1]]);
}
for (const z of D.SNAPPED) {
  const A = attach(z.pole, z.side), [cx, cy] = z.contact; const p0 = D.POLES[z.pole];
  const poly = [];
  for (let i = 0; i <= 64; i++) { const t = i / 64; const gx = A[0] + (cx - A[0]) * t, gy = A[1] + (cy - A[1]) * t;
    const zz = (D.WIRE_Z - 1) * Math.pow(1 - t, 1.8) + 6 * Math.sin(Math.PI * Math.min(1, t * 1.15)) * (1 - t); poly.push([gx, gy, Math.max(0.5, zz)]); }
  // yerde kıvrılan uç (suya değen kısım) + bakır uç
  const dx = cx - A[0], dy = cy - A[1], L = Math.hypot(dx, dy), ux = dx / L, uy = dy / L;
  poly.push([cx + ux * 0.18 - uy * 0.08, cy + uy * 0.18 + ux * 0.08, 0.5], [cx + ux * 0.3 + uy * 0.05, cy + uy * 0.3 - ux * 0.05, 0.5]);
  wireSheet('WIRE_FE_Snapped_' + z.id, [poly], [p0[0], p0[1]], [cx + ux * 0.3, cy + uy * 0.3], plot => {
    plot(cx + ux * 0.3 + uy * 0.05, cy + uy * 0.3 - ux * 0.05, 0.5, '#E09A4A'); plot(cx + ux * 0.33 + uy * 0.07, cy + uy * 0.33 - ux * 0.07, 1.5, '#F2C27A');
  });
}

// ================================================================ KIVILCIM (4 kare), SU YÜZEYİ ARKI (4 kare), ⚡ UYARI, GÖKYÜZÜ ŞİMŞEĞİ
{
  const frames = [];
  for (let f = 0; f < 4; f++) {
    const cv = new Cv(15, 15); const cx = 7, cy = 11;
    const n = 3 + (f % 2) * 2;
    for (let i = 0; i < n; i++) {
      const ang = -Math.PI * (0.1 + 0.8 * hash(i, f, 31)), len = 2 + fl(hash(i, f, 37) * 5);
      for (let s = 1; s <= len; s++) { const x = cx + Math.round(Math.cos(ang) * s), y = cy + Math.round(Math.sin(ang) * s * 0.9); cv.set(x, y, s < len ? '#FFF6C8' : '#8FE3FF'); }
    }
    cv.set(cx, cy, '#FFFFFF'); cv.set(cx - 1, cy, '#FFE27A'); cv.set(cx + 1, cy, '#FFE27A'); cv.set(cx, cy - 1, '#FFFFFF');
    if (f === 2) { cv.set(cx - 2, cy + 1, '#8FE3FF'); cv.set(cx + 2, cy + 1, '#8FE3FF'); }
    frames.push(cv);
  }
  emitSheet(HZ, 'Hazards', 'FX_FE_Spark', frames, 7, 12);
}
{
  const frames = [];
  for (let f = 0; f < 4; f++) {
    const cv = new Cv(20, 9); let y = 4 + (hash(f, 1, 41) < 0.5 ? -1 : 1);
    for (let x = 2 + f % 2; x < 18 - (f >> 1); x++) {
      if (hash(x, f, 43) < 0.42) y += hash(x, f, 47) < 0.5 ? -1 : 1; y = Math.max(1, Math.min(7, y));
      cv.set(x, y, hash(x, f, 53) < 0.3 ? '#FFFFFF' : '#BDF4FF', 235);
      if (hash(x, f, 59) < 0.12) cv.set(x, y + (hash(x, f, 61) < 0.5 ? -1 : 1), '#7FD8F5', 200);
    }
    frames.push(cv);
  }
  emitSheet(HZ, 'Hazards', 'FX_FE_Arc', frames, 10, 5);
}
{ // ⚡ uyarı: sarı üçgen, koyu kenar, şimşek — arayüz paletiyle (FloodEveningHud uyarı şeridi sarısı)
  const cv = new Cv(15, 14);
  for (let y = 0; y < 12; y++) { const half = Math.floor(y / 2 + 0.5); for (let x = 7 - half; x <= 7 + half; x++) cv.set(x, y + 1, '#F2C230'); }
  for (let x = 1; x <= 13; x++) cv.set(x, 12, '#D99A1E');
  for (const [x, y] of [[8, 3], [7, 4], [7, 5], [6, 6], [7, 6], [8, 6], [8, 7], [7, 8], [7, 9], [6, 10]]) cv.set(x, y, '#14141F');
  cv.outline(OL);
  emitSingle(HZ, 'Hazards', 'UI_FE_PowerWarn', cv, 7, 14);
}
{ // uzak şimşek çizgileri (gökyüzü bandında; ince, solgun, kenarsız)
  const frames = [];
  for (let f = 0; f < 3; f++) {
    const cv = new Cv(24, 72); let x = 12 + (f - 1) * 3;
    const core = [];
    for (let y = 0; y < 70; y++) { if (y % 3 === 0) x += hash(y, f, 71) < 0.5 ? -1 : 1; if (hash(y, f, 73) < 0.06) x += hash(y, f, 79) < 0.5 ? -2 : 2; x = Math.max(3, Math.min(20, x)); core.push([x, y]); }
    const br = 20 + fl(hash(f, 2, 83) * 25); let bx = core[br][0]; const dir = hash(f, 3, 89) < 0.5 ? -1 : 1;
    for (let y = br; y < br + 14 && y < 70; y++) { if (y % 2) bx += dir; core.push([bx, y, 1]); }
    for (const [cx, cy, thin] of core) { for (const [dx, dy] of [[-1, 0], [1, 0]]) if (!cv.a(cx + dx, cy + dy)) cv.set(cx + dx, cy + dy, '#B7B4EE', thin ? 90 : 150); }
    for (const [cx, cy, thin] of core) cv.set(cx, cy, thin ? '#DCDAFA' : '#F6F4FF', thin ? 200 : 255);
    frames.push(cv);
  }
  emitSheet(HZ, 'Hazards', 'FX_FE_SkyBolt', frames, 12, 72);
}

// ================================================================ ENKAZ (engine; pivot = zemin merkezi)
function debris(name, objs, W = 40, H = 30, ox = 20, oy = 22, shadow) {
  const cv = rend(objs, W, H, ox, oy, { zmax: 40, shadow });
  emitSingle(HZ, 'Hazards', name, cv, ox, oy);
}
const PLANK = ['#B07E4E', '#946538', '#784F2A', '#5A3A1E'];
debris('PROP_FE_Float_Plank', [{ sdf: box(-13, 13, -2.6, 2.6, 0, 2.4), mat: (p, n, c) => { if (c.face === 'top' && (mod(p.v + 2.6, 2.6) < 0.5)) return '#6E4826'; if (c.face === 'top' && Math.abs(Math.abs(p.u) - 10) < 0.6 && Math.abs(p.v) < 0.6) return '#3A2A1C'; return ramp4(n, PLANK); } }]);
const CRATE = ['#6FA6E0', '#4A82C4', '#33649E', '#244874'];
debris('PROP_FE_Float_Crate', [{ sdf: rbox(-7, 7, -5.5, 5.5, 0, 8, 0.8), mat: (p, n, c) => {
  if (c.face === 'top') return (Math.abs(p.u) < 5.6 && Math.abs(p.v) < 4.2) ? '#1D3A60' : CRATE[0];
  const a = c.face === 'left' ? p.v : p.u; if (p.z > 2 && p.z < 6 && mod(a + 7, 3.5) < 1.6) return '#1D3A60'; return ramp4(n, CRATE); } }]);
debris('PROP_FE_Float_Bottle', [
  { sdf: capsule(V(-4.5, 0, 2.2), V(3, 0, 2.2), 2.1), mat: (p, n) => (p.u > -2 && p.u < 0.5) ? '#E8E2C8' : ramp4(n, ['#9FE0B8', '#6FBF92', '#4E9A72', '#3A7556']) },
  { sdf: capsule(V(3, 0, 2.2), V(6, 0, 2.2), 1.0), mat: (p, n) => ramp4(n, ['#9FE0B8', '#6FBF92', '#4E9A72', '#3A7556']) },
  { sdf: cylZ(0, 0, 0, 0, 0), mat: () => '#000' }, { sdf: capsule(V(6.2, 0, 2.2), V(7.4, 0, 2.2), 1.2), mat: (p, n) => ramp4(n, ['#E55A4A', '#C23E30', '#962E24', '#6E2018']) }]);
debris('PROP_FE_Float_Branch', (() => { const o = []; const BR = ['#8A5A36', '#6E4428', '#56331C', '#3F2412'], LF = ['#6E9A48', '#557F3A', '#41652E', '#2F4B22'];
  o.push({ sdf: capsule(V(-13, -2, 1.6), V(13, 2, 1.8), 1.5), mat: (p, n) => ramp4(n, BR) });
  o.push({ sdf: capsule(V(-3, -0.5, 1.6), V(4, -7, 1.6), 0.9), mat: (p, n) => ramp4(n, BR) });
  o.push({ sdf: capsule(V(5, 1, 1.6), V(10, 7, 1.6), 0.8), mat: (p, n) => ramp4(n, BR) });
  for (const [u, v, r] of [[4.5, -7.5, 3.2], [10.5, 7.5, 3], [13, 2.5, 2.6], [-12, -2.5, 2.2]]) o.push({ sdf: ellipsoid(u, v, 2.4, r, r, r * 0.6), mat: (p, n) => { const h = hash(fl(p.u), fl(p.v), 3); return h < 0.15 ? '#8DB860' : ramp4(n, LF, (h - 0.5) * 0.2); } });
  return o; })(), 44, 34, 22, 25);
const STONE = ['#A7A49C', '#8E8B84', '#74726C', '#5A5853'], STONE2 = ['#A39B8E', '#8A8276', '#6F685E', '#555048'];
const speck = pal => (p, n) => { const h = hash(fl(p.u * 1.3), fl(p.v * 1.3), fl(p.z * 1.3)); if (h < 0.08) return '#4E4C47'; return ramp4(n, pal, (h - 0.5) * 0.15); };
debris('PROP_FE_Stone_A', [{ sdf: ellipsoid(0, 0, 2.4, 6, 4.6, 3.6), mat: speck(STONE) }, { sdf: ellipsoid(5.5, 3.5, 1.4, 2.6, 2.2, 1.8), mat: speck(STONE2) }], 36, 26, 18, 19, (u, v) => (u * u / 64 + v * v / 36 < 1 ? 60 : 0));
debris('PROP_FE_Stone_B', [{ sdf: ellipsoid(0, 0, 2.2, 4.6, 4, 3.2), mat: speck(STONE2) }, { sdf: ellipsoid(-4.5, 2.5, 1.2, 2.2, 1.8, 1.5), mat: speck(STONE) }], 34, 24, 17, 18, (u, v) => (u * u / 40 + v * v / 25 < 1 ? 60 : 0));
debris('PROP_FE_Rubble_S', [{ sdf: rbox(-6, 4, -4.5, 3, 0, 5.5, 0.8), mat: speck(['#BAB5A9', '#9D988D', '#7F7B72', '#5F5C56']) },
  { sdf: rbox(2.5, 8.5, -1, 4.5, 0, 3.2, 0.5), mat: (p, n) => mod(p.u + p.v, 3) < 0.5 ? '#8E8A80' : ramp4(n, ['#B0603E', '#924B2E', '#743A23', '#552918']) },
  { sdf: capsule(V(-2, -1, 5), V(1, 2, 9), 0.6), mat: (p, n) => ramp4(n, ['#8A5A3A', '#6E4428', '#56331C', '#3A2212']) }], 36, 30, 18, 23, (u, v) => (u > -8 && u < 10 && v > -6 && v < 6 ? 55 : 0));

// ================================================================ AĞAÇ SALINIMI (satır kaydırma, 8 kare; sakin A=1, fırtına A=2)
const TREES = [
  ['Tree_Round', 'Props/Street/Surroundings/PROP_StreetBG_Tree_Round', 0.34],
  ['Tree_Small', 'Props/Street/Surroundings/PROP_StreetBG_Tree_Small', 0.38],
  ['Tree_Pine', 'Props/Street/Surroundings/PROP_StreetBG_Tree_Pine', 0.42],
  ['Street_Tree', 'Props/Street/PROP_Street_Tree', 0.36],
  ['Bush', 'Props/Street/Surroundings/PROP_StreetBG_Bush', 0.5],
];
manifest.trees = [];
for (const [key, rel, trunk] of TREES) {
  const src = Cv.load(C.ART + '/' + rel + '.png'); const meta = fs.readFileSync(C.ART + '/' + rel + '.png.meta', 'utf8');
  const pm = meta.match(/spritePivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}/); const pivX = +pm[1] * src.w, pivUp = +pm[2] * src.h;
  const ground = src.h - pivUp;                                  // zemin satırı (y aşağı)
  let top = 0; while (top < src.h && ![...Array(src.w).keys()].some(x => src.a(x, top))) top++;
  const hTop = ground - top;
  // gövde üstü: zeminden yukarı, dolu genişliğin gövde genişliğinin ~2,2 katını geçtiği ilk satır (taç başlangıcı); salınım oradan başlar
  const widthAt = y => { let n = 0; for (let x = 0; x < src.w; x++) if (src.a(x, y) > 128) n++; return n; };
  const trunkW = widthAt(Math.round(ground - hTop * 0.15)) || 4;
  let crown = Math.round(ground - hTop * 0.2); while (crown > top && widthAt(crown) < trunkW * 2.2) crown--;
  const h0 = Math.max(hTop * trunk * 0.6, ground - crown - 3);
  const M = 2; const sets = {};
  for (const [tag, A] of [['Calm', 1], ['Storm', key === 'Bush' ? 1 : 2]]) {
    const frames = [];
    for (let f = 0; f < 8; f++) {
      const cv = new Cv(src.w + 2 * M, src.h);
      for (let y = 0; y < src.h; y++) {
        const hy = ground - y; const w = Math.max(0, Math.min(1, (hy - h0) / Math.max(1, hTop - h0)));
        const amp = key === 'Bush' && tag === 'Calm' ? 0.75 : A;
        const s = Math.round(amp * Math.pow(w, 1.3) * Math.sin(2 * Math.PI * f / 8 - 0.9 * w));
        for (let x = 0; x < src.w; x++) { const c = src.get(x, y); if (c) cv.set(x + M + s, y, c, c[3]); }
      }
      frames.push(cv);
    }
    const name = 'FX_FE_Sway_' + key + '_' + tag;
    emitSheet(TR, 'Trees', name, frames, pivX + M, ground);
    sets[tag] = REL + '/Trees/' + name + '.png';
  }
  manifest.trees.push({ key, source: 'Assets/Art/' + rel + '.png', calm: sets.Calm, storm: sets.Storm });
}

fs.writeFileSync(C.OUT + '/hazards.json', JSON.stringify(manifest, null, 1));
console.log('hazards: ' + manifest.strips.length + ' tel dilimi, ' + manifest.files.length + ' dosya');
