// ENV yeni sprite'lar: duvar/korkuluk hatları (dilimli), kapılar, tabelalar, pano, dal, zemin okları, gökyüzü.
// node props.js → out/props/*.png + out/props.json  (pivot: piksel, sol-alt, y yukarı)
const C = require('./common.js');
const { fs, Cv, E, rgb, mix, fl, mod, hash, OL, drawText, textW, encodePng } = C;
const M = require('./map.js');
const { V, box, rbox, cylZ, capsule, sphere, ellipsoid, U, SUB, lambert } = E;
const OUTP = C.OUT + '/props'; fs.mkdirSync(OUTP, { recursive: true });
const man = {};
const ramp4 = (n, c, bias = 0) => { const l = lambert(n) + bias; return l > 0.85 ? c[0] : l > 0.55 ? c[1] : l > 0.25 ? c[2] : c[3]; };

// engine render -> Cv (y-down) + origin
function rend(objs, opt) {
  const W = opt.W, Hh = opt.H, ox = opt.ox, oy = opt.oy;
  const r = E.render(objs, W, Hh, ox, oy, Object.assign({ zmax: opt.zmax || 120, innerThr: opt.innerThr || 2.5 }, opt));
  const cv = new Cv(W, Hh); cv.d.set(r.rgba); return { cv, ox, oy };
}
// save single sprite: crop keeping pivot (pivot px from left / from bottom)
function saveSingle(name, cv, pivX, pivYdown, note) {
  const c = cv.crop(); const px = pivX - c.x0, pyUp = (c.cv.h - 1 - (pivYdown - c.y0)) + 1; // pivot row pivYdown is the baseline row bottom edge
  c.cv.save(OUTP + '/' + name + '.png');
  man[name] = { file: name, w: c.cv.w, h: c.cv.h, sprites: [{ name: name + '_0', x: 0, y: 0, w: c.cv.w, h: c.cv.h, px: +(px / c.cv.w).toFixed(6), py: +(pyUp / c.cv.h).toFixed(6) }], note };
  console.log(name, c.cv.w + 'x' + c.cv.h, 'pivot', px, pyUp);
}

// =============================================================== LINE WALLS (sliced into 16px strips)
const WALL = { pl: '#D6CBB2', plD: '#B9AD93', base: '#8C887E', baseD: '#706C63', cap: '#E9E2D0', capD: '#C9C1AE' };
const STN = { L: ['#9C9990', '#8E8B83'], R: ['#76736C', '#6A6760'], cap: '#B9B5AA', capD: '#99958B' };
const IRON = ['#5E6A72', '#47525A', '#353E45', '#272E34'];
function lineObjs(L, len, zoff) {
  // local coords: along = a (px), across = 0. axis x: u=a, v=0 ; axis y: u=0, v=a. zoff(a) = base height offset at a.
  const objs = [];
  const segs = []; { let a = L.from; for (const [g0, g1] of (L.gaps || []).slice().sort((p, q) => p[0] - q[0])) { if (g0 > a) segs.push([a, g0]); a = g1; } if (L.to > a) segs.push([a, L.to]); }
  const B = (a0, a1, t0, t1, z0, z1) => L.axis === 'x' ? box(a0, a1, t0, t1, z0, z1) : box(t0, t1, a0, a1, z0, z1);
  const along = p => L.axis === 'x' ? p.u : p.v;
  for (const [s0, s1] of segs) for (let g = s0; g < s1; g++) {           // per cell (step heights)
    const a0 = (g - L.from) * 32, a1 = a0 + 32, z = zoff(g);
    if (L.kind === 'rail') {
      for (let k = 0; k < 2; k++) { const pa = a0 + k * 16 + 2; objs.push({ sdf: B(pa, pa + 2, -1, 1, z, z + 18), mat: (p, n) => ramp4(n, IRON) }); }
      objs.push({ sdf: B(a0, a1, -1, 1, z + 15, z + 18), mat: (p, n) => ramp4(n, IRON, 0.1) });
      objs.push({ sdf: B(a0, a1, -0.6, 0.6, z + 7, z + 8.5), mat: (p, n) => ramp4(n, IRON) });
      objs.push({ sdf: B(a0, a1, -2, 2, z, z + 2), mat: (p, n) => ramp4(n, ['#8C887E', '#7A766D', '#66625A', '#55524B']) });
    } else if (L.kind === 'wall') {
      objs.push({ sdf: B(a0, a1, -3, 3, z, z + 6), mat: (p, n, c) => c.face === 'top' ? WALL.capD : (lambert(n) > 0.3 ? WALL.base : WALL.baseD) });
      objs.push({ sdf: B(a0, a1, -3, 3, z + 6, z + 20), mat: (p, n, c) => { if (c.face === 'top') return WALL.cap; const lit = lambert(n) > 0.3; if (hash(fl(along(p) / 3), fl(p.z / 2), 7) < 0.07) return lit ? WALL.plD : '#A0957D'; return lit ? WALL.pl : WALL.plD; } });
      objs.push({ sdf: B(a0 - 0.01, a1 + 0.01, -4, 4, z + 20, z + 23), mat: (p, n, c) => c.face === 'top' ? WALL.cap : (lambert(n) > 0.3 ? WALL.cap : WALL.capD) });
    } else if (L.kind === 'hiwall') {
      objs.push({ sdf: B(a0, a1, -4, 4, z, z + 46), mat: (p, n, c) => { if (c.face === 'top') return STN.cap; const lit = lambert(n) > 0.3; const row = fl(p.z / 8), aa = along(p) + (row & 1) * 7; if (mod(p.z, 8) < 1 || mod(aa, 14) < 1) return lit ? '#6E6B64' : '#55524C'; return (lit ? STN.L : STN.R)[hash(fl(aa / 14), row, 3) < 0.5 ? 0 : 1]; } });
      objs.push({ sdf: B(a0 - 0.01, a1 + 0.01, -5, 5, z + 46, z + 50), mat: (p, n, c) => c.face === 'top' ? STN.cap : (lambert(n) > 0.3 ? STN.cap : STN.capD) });
    }
  }
  // pillars
  for (const pg of L.pillars || []) { const a = (pg - L.from) * 32, z = zoff(fl(pg)); const ph = L.kind === 'hiwall' ? 56 : 30;
    objs.push({ sdf: B(a - 5, a + 5, -5, 5, z, z + ph), mat: (p, n, c) => c.face === 'top' ? '#C9C3B4' : (lambert(n) > 0.3 ? '#A39D91' : '#7F7A70') });
    objs.push({ sdf: B(a - 6.5, a + 6.5, -6.5, 6.5, z + ph, z + ph + 3), mat: (p, n, c) => c.face === 'top' ? '#DCD6C8' : (lambert(n) > 0.3 ? '#C2BCAE' : '#9A9488') }); }
  // wall ends: small end pillar for walls
  if (L.kind === 'wall') for (const [s0, s1] of segs) for (const e of [s0, s1]) { if ((L.pillars || []).includes(e)) continue; const a = (e - L.from) * 32, z = zoff(Math.min(fl(e), L.to - 1));
    objs.push({ sdf: B(a - 4, a + 4, -4, 4, z, z + 25), mat: (p, n, c) => c.face === 'top' ? WALL.cap : (lambert(n) > 0.3 ? WALL.pl : WALL.plD) }); }
  return objs;
}
function lineSprite(L) {
  const len = (L.to - L.from) * 32;
  const hAt = g => L.axis === 'x' ? Math.max(M.H(g, L.at), M.H(g, L.at - 1)) : Math.max(M.H(L.at, g), M.H(L.at - 1, g));
  const h0 = hAt(L.from); const zoff = g => hAt(Math.min(Math.max(g, L.from), L.to - 1)) - h0;
  let zr = 0; for (let g = L.from; g < L.to; g++) zr = Math.max(zr, Math.abs(zoff(g)));
  const objs = lineObjs(L, len, zoff);
  const W = len + 40, Hh = len / 2 + 120 + zr; const ox = L.axis === 'x' ? 20 : len + 20, oy = Hh - 10;
  const r = rend(objs, { W, H: Hh, ox, oy, zmax: 90 + zr });
  // slice into 16 px columns; pivot = line base at the strip centre (x local = dx from origin)
  const name = 'LINE_FE_' + L.id; const cv = r.cv; const sprites = []; const strips = [];
  // vertical crop of the whole sheet
  let y0 = 1e9, y1 = -1; for (let y = 0; y < cv.h; y++) for (let x = 0; x < cv.w; x++) if (cv.a(x, y)) { y0 = Math.min(y0, y); y1 = Math.max(y1, y); }
  let xMinAll = 1e9, xMaxAll = -1; for (let y = 0; y < cv.h; y++) for (let x = 0; x < cv.w; x++) if (cv.a(x, y)) { xMinAll = Math.min(xMinAll, x); xMaxAll = Math.max(xMaxAll, x); }
  const sheet = new Cv(xMaxAll - xMinAll + 1, y1 - y0 + 1); for (let y = 0; y < sheet.h; y++) for (let x = 0; x < sheet.w; x++) { const c = cv.get(x + xMinAll, y + y0); if (c) sheet.set(x, y, c, c[3]); }
  const SW = 16; let k = 0;
  for (let sx = 0; sx < sheet.w; sx += SW, k++) {
    const w = Math.min(SW, sheet.w - sx);
    let any = false, ty = 1e9, by = -1; for (let y = 0; y < sheet.h; y++) for (let x = sx; x < sx + w; x++) if (sheet.a(x, y)) { any = true; ty = Math.min(ty, y); by = Math.max(by, y); }
    if (!any) continue;
    const cxLocal = sx + w / 2 + xMinAll - ox;         // screen dx from the line origin (px)
    // base point of the line at that dx: axis x: a = dx, y_up = dx/2 ; axis y: a = -dx, y_up = -dx/2 ; plus step height
    const a = L.axis === 'x' ? cxLocal : -cxLocal; const aC = Math.min(Math.max(a, 0), len);
    const g = L.from + fl(aC / 32); const zb = zoff(Math.min(g, L.to - 1));
    const baseUp = aC / 2 + zb;                         // px above origin
    // pivot in sheet coords (y-down): origin row = oy - y0
    const pivYdown = (oy - y0) - baseUp;
    const rectY = sheet.h - 1 - by;                     // rect bottom (y-up)
    const rh = by - ty + 1;
    const pyUp = (sheet.h - pivYdown) - rectY;          // pivot from rect bottom
    sprites.push({ name: name + '_' + k, x: sx, y: rectY, w, h: rh, px: +((w / 2) / w).toFixed(6), py: +(pyUp / rh).toFixed(6) });
    strips.push({ sprite: name + '_' + k, dx: cxLocal, dy: baseUp, a: aC });
  }
  sheet.save(OUTP + '/' + name + '.png');
  // line origin in grid: axis x -> (from, at) ; axis y -> (at, from); origin height h0
  const og = L.axis === 'x' ? [L.from, L.at] : [L.at, L.from];
  man[name] = { file: name, w: sheet.w, h: sheet.h, sprites, line: { id: L.id, kind: L.kind, axis: L.axis, origin: og, h0, strips } };
  console.log(name, sheet.w + 'x' + sheet.h, sprites.length, 'strips');
}
for (const L of M.LINES) lineSprite(L);

// =============================================================== GATES
// garden gate leaves (iron): Closed along gx (2 leaves, 64 px gap), Open: leaves along gy at hinges
function gateLeaf(u0, u1, v0, v1, z0, hgt, alongU) {
  const objs = []; const L = alongU ? u1 - u0 : v1 - v0;
  const B = (a0, a1, z0_, z1_, t = 0.8) => alongU ? box(u0 + a0, u0 + a1, v0 - t, v0 + t, z0_, z1_) : box(u0 - t, u0 + t, v0 + a0, v0 + a1, z0_, z1_);
  for (let a = 1; a < L - 1; a += 4) objs.push({ sdf: B(a, a + 1.6, z0 + 2, z0 + hgt - 2 + (Math.abs(a - L / 2) < L / 3 ? 2 : 0)), mat: (p, n) => ramp4(n, ['#4E5A55', '#3B4642', '#2B3431', '#20272A']) });
  objs.push({ sdf: B(0, L, z0 + hgt - 4, z0 + hgt - 2), mat: (p, n) => ramp4(n, ['#5C6863', '#45514C', '#333C38', '#252C2B']) });
  objs.push({ sdf: B(0, L, z0 + 2, z0 + 4), mat: (p, n) => ramp4(n, ['#5C6863', '#45514C', '#333C38', '#252C2B']) });
  objs.push({ sdf: B(0, L, z0 + 13, z0 + 14.5), mat: (p, n) => ramp4(n, ['#5C6863', '#45514C', '#333C38', '#252C2B']) });
  objs.push({ sdf: B(0, 2, z0 + 1, z0 + hgt - 1, 1.1), mat: (p, n) => ramp4(n, ['#5C6863', '#45514C', '#333C38', '#252C2B']) });
  objs.push({ sdf: B(L - 2, L, z0 + 1, z0 + hgt - 1, 1.1), mat: (p, n) => ramp4(n, ['#5C6863', '#45514C', '#333C38', '#252C2B']) });
  return objs;
}
function gateSprite(name, objs, pivotGrid) { // pivotGrid: local (u,v) px of the sort pivot (z=0)
  const r = rend(objs, { W: 220, H: 170, ox: 110, oy: 140, zmax: 90 });
  const [pu, pv] = pivotGrid; saveSingle(name, r.cv, r.ox + (pu - pv), r.oy - (pu + pv) / 2, 'pivot at grid local ' + pivotGrid);
}
// GardenGate: local origin = (78, 21) cell corner; gap u 0..64
gateSprite('PROP_FE_GardenGate_Closed', [...gateLeaf(1, 32, 0, 0, 0, 30, true), ...gateLeaf(32, 63, 0, 0, 0, 30, true),
  { sdf: box(30, 34, -2, 2, 14, 20), mat: () => '#C9A24A' }], [32, 0]);
gateSprite('PROP_FE_GardenGate_OpenL', gateLeaf(0, 0, 1, 31, 0, 30, false), [0, 16]);
gateSprite('PROP_FE_GardenGate_OpenR', gateLeaf(64, 64, 1, 31, 0, 30, false), [64, 16]);
// AltPassage latch gate (wooden, small): gap 42..44 at gy 19
const WOOD = ['#9A6A40', '#7E5232', '#64401F', '#4A2C18'];
function woodLeaf(u0, v0, L, alongU, hgt) {
  const objs = []; const B = (a0, a1, z0, z1, t = 1.2) => alongU ? box(u0 + a0, u0 + a1, v0 - t, v0 + t, z0, z1) : box(u0 - t, u0 + t, v0 + a0, v0 + a1, z0, z1);
  for (let a = 0; a < L - 0.5; a += 5) objs.push({ sdf: B(a + 0.4, a + 4.4, 2, hgt - (hash(a, 1) < 0.5 ? 0 : 1)), mat: (p, n) => ramp4(n, WOOD) });
  objs.push({ sdf: B(0, L, 6, 8, 1.8), mat: (p, n) => ramp4(n, ['#7E5232', '#64401F', '#4A2C18', '#3A2212']) });
  objs.push({ sdf: B(0, L, hgt - 8, hgt - 6, 1.8), mat: (p, n) => ramp4(n, ['#7E5232', '#64401F', '#4A2C18', '#3A2212']) });
  return objs;
}
gateSprite('PROP_FE_AltGate_Closed', [...woodLeaf(1, 0, 62, true, 24), { sdf: box(56, 60, -2.5, 2.5, 12, 15), mat: () => '#C9A24A' }], [32, 0]);
gateSprite('PROP_FE_AltGate_Open', woodLeaf(0, 1, 30, false, 24), [0, 16]);
// CodeDoor: steel double door in the 64 px gap, 44 px tall; Open: leaves along gy
function steelLeaf(u0, v0, L, alongU, hgt) {
  const B = (a0, a1, z0, z1, t = 1.5) => alongU ? box(u0 + a0, u0 + a1, v0 - t, v0 + t, z0, z1) : box(u0 - t, u0 + t, v0 + a0, v0 + a1, z0, z1);
  const al = p => alongU ? p.u - u0 : p.v - v0;
  return [{ sdf: B(0, L, 0, hgt), mat: (p, n, c) => { if (c.face === 'top') return '#8C98A4'; const lit = lambert(n) > 0.3; const a = al(p);
    if (a < 2 || a > L - 2 || p.z > hgt - 3 || p.z < 2) return lit ? '#4A5562' : '#3A434E';
    if (Math.abs(p.z - hgt * 0.55) < 1) return lit ? '#56616E' : '#444D58';
    if (p.z > hgt - 14 && p.z < hgt - 6 && a > 5 && a < L - 5) return lit ? '#3C5A6E' : '#2E4656';   // küçük pencere
    return lit ? '#6F7C8A' : '#5A6672'; } }];
}
gateSprite('PROP_FE_CodeDoor_Closed', [...steelLeaf(1, 0, 31, true, 44), ...steelLeaf(32, 0, 31, true, 44),
  { sdf: box(27, 30, -2.5, -1, 18, 26), mat: () => '#B8C2CC' }, { sdf: box(34, 37, -2.5, -1, 18, 26), mat: () => '#B8C2CC' }], [32, 0]);
gateSprite('PROP_FE_CodeDoor_OpenL', steelLeaf(0, 1, 30, false, 44), [0, 16]);
gateSprite('PROP_FE_CodeDoor_OpenR', steelLeaf(64, 1, 30, false, 44), [64, 16]);
// keypad panel (on right pillar face, facing -gy), turquoise keys
{ const objs = [{ sdf: box(0, 9, -1.5, 0, 0, 13), mat: (p, n, c) => { if (c.face !== 'right' && c.face !== 'left') return '#3A424C'; const kx = fl((p.u - 1.5) / 2.2), kz = fl((p.z - 1.5) / 2.6);
  if (p.z > 10.5) return mod(fl(p.u), 3) === 0 ? '#7FE0A0' : '#1E2A30';                 // ekran
  if (p.u > 1.5 && p.u < 8.1 && p.z > 1.5 && p.z < 9.3 && mod(p.u - 1.5, 2.2) < 1.4 && mod(p.z - 1.5, 2.6) < 1.6) return '#4FC3C8';
  return '#2A3138'; } }];
  const r = rend(objs, { W: 40, H: 40, ox: 15, oy: 30, zmax: 40 }); saveSingle('PROP_FE_Keypad', r.cv, r.ox + 4, r.oy - 2, 'keypad, pivot near base centre'); }

// =============================================================== BRANCH
{ const objs = [];
  const br = (a, b, r) => objs.push({ sdf: capsule(V(...a), V(...b), r), mat: (p, n) => ramp4(n, ['#8A5A36', '#6E4428', '#56331C', '#3F2412']) });
  br([0, -56, 3], [0, 56, 4], 3.2); br([0, -10, 5], [14, -30, 3], 1.8); br([0, 20, 5], [-12, 40, 2.5], 1.6); br([0, 34, 5], [10, 50, 3], 1.4);
  for (const [u, v, z, r] of [[14, -32, 5, 7], [-13, 42, 5, 6.5], [10, 52, 5, 6], [3, -50, 6, 5], [-4, 12, 7, 5], [16, -22, 6, 5]])
    objs.push({ sdf: ellipsoid(u, v, z, r, r, r * 0.7), mat: (p, n) => { const h = hash(fl(p.u / 2), fl(p.v / 2), fl(p.z / 2)); if (h < 0.12) return '#7A9A4A'; return ramp4(n, ['#5C7D3A', '#476A30', '#355226', '#263D1C'], (h - 0.5) * 0.2); } });
  const r = rend(objs, { W: 180, H: 110, ox: 90, oy: 60, zmax: 40, shadow: (u, v) => (Math.abs(u) < 6 && Math.abs(v) < 58) ? 70 : 0 });
  saveSingle('PROP_FE_Branch', r.cv, r.ox, r.oy, 'fallen branch lying along gy, pivot = centre'); }

// =============================================================== BILLBOARD SIGNS (flat, readable)
function postSign(name, lines, opt) {
  const bw = opt.w || Math.max(...lines.map(l => textW(l.t) + (l.icon ? 0 : 0))) + 10;
  const bh = opt.h; const post = opt.post || 34; const W = bw + 4, Hh = bh + post + 4;
  const cv = new Cv(W, Hh);
  // post(s)
  const posts = opt.twoPosts ? [4, W - 7] : [fl(W / 2) - 1];
  for (const px of posts) { cv.rect(px, bh + 1, 3, post, '#7D8794'); cv.rect(px + 2, bh + 1, 1, post, '#5F6977'); }
  // board
  cv.rect(1, 1, bw + 2, bh, opt.border || '#F2F2EA'); cv.rect(2, 2, bw, bh - 2, opt.bg);
  if (opt.stripe) for (let x = 2; x < bw + 2; x++) for (let y = 2; y < 5; y++) cv.set(x, y, ((x + y) >> 1) & 1 ? '#F2C230' : '#14141F');
  for (const l of lines) {
    if (l.draw) { l.draw(cv); continue; }
    const tw = textW(l.t); const x0 = l.x !== undefined ? l.x : fl(1 + (bw + 2 - tw) / 2);
    drawText(cv, l.t, x0, l.y, l.c || '#F2F2EA');
  }
  cv.outline(OL);
  saveSingle(name, cv, posts.length === 1 ? posts[0] + 1 : fl(W / 2), Hh - 3, opt.note || 'billboard sign, pivot = post foot');
}
const arrowNW = (cv, x, y, c) => { for (let i = 0; i < 7; i++) { cv.set(x + i, y + i, c); cv.set(x + i + 1, y + i, c); } for (let i = 0; i < 5; i++) { cv.set(x + i, y, c); cv.set(x, y + i, c); cv.set(x + i, y + 1, c); cv.set(x + 1, y + i, c); } };
const arrowNE = (cv, x, y, c) => { for (let i = 0; i < 7; i++) { cv.set(x + 7 - i, y + i, c); cv.set(x + 6 - i, y + i, c); } for (let i = 0; i < 5; i++) { cv.set(x + 7 - i, y, c); cv.set(x + 7, y + i, c); cv.set(x + 7 - i, y + 1, c); cv.set(x + 6, y + i, c); } };
const hillIcon = (cv, x, y, c) => { for (let i = 0; i < 6; i++) for (let j = -i; j <= i; j++) cv.set(x + 6 + j, y + i, c); for (let j = -1; j <= 1; j++) cv.set(x + 6 + j, y - 2, c); cv.set(x + 6, y - 3, c); cv.set(x + 6, y - 1, c); };
const GREEN = '#1E8A4C';
const arrowSE = (cv, x, y, c) => { for (let i = 0; i < 7; i++) { cv.set(x + i, y + i, c); cv.set(x + i + 1, y + i, c); } for (let i = 0; i < 5; i++) { cv.set(x + 7 - i, y + 7, c); cv.set(x + 7, y + 7 - i, c); cv.set(x + 7 - i, y + 6, c); cv.set(x + 6, y + 7 - i, c); } };
postSign('PROP_FE_Sign_UpRoad', [
  { draw: cv => arrowNW(cv, 5, 5, '#F2F2EA') }, { draw: cv => hillIcon(cv, 22, 7, '#F2F2EA') },
  { t: 'YUKARI YOL', y: 17 }, { t: 'GÜVENLİ', y: 24, c: '#BFF0CF' }], { bg: GREEN, h: 32, w: 50, post: 30 });
postSign('PROP_FE_Sign_ShortRoad', [
  { draw: cv => { for (let i = 0; i < 9; i++) { cv.set(5 + i, 7 + i, '#F2F2EA'); cv.set(6 + i, 7 + i, '#F2F2EA'); cv.set(13 - i, 7 + i, '#F2F2EA'); cv.set(14 - i, 7 + i, '#F2F2EA'); } } },
  { draw: cv => { for (let w = 0; w < 2; w++) for (let x = 0; x < 12; x++) cv.set(20 + x, 10 + w * 4 + ((x >> 1) & 1), '#9FD8F0'); } },
  { draw: cv => arrowNE(cv, 37, 6, '#F2F2EA') },
  { t: 'KISA YOL', y: 21 }, { t: 'SU BASKINI', y: 28, c: '#FFD6CF' }], { bg: '#C0392B', h: 36, w: 48, post: 26, stripe: true, twoPosts: true });
postSign('PROP_FE_Sign_Safe_NE', [
  { draw: cv => arrowNE(cv, 39, 5, '#F2F2EA') }, { draw: cv => hillIcon(cv, 22, 7, '#F2F2EA') },
  { t: 'GÜVENLİ ALAN', y: 17 }], { bg: GREEN, h: 25, w: 50, post: 30 });
postSign('PROP_FE_Sign_Safe_SE', [
  { draw: cv => arrowSE(cv, 39, 4, '#F2F2EA') }, { draw: cv => hillIcon(cv, 22, 7, '#F2F2EA') },
  { t: 'GÜVENLİ ALAN', y: 17 }], { bg: GREEN, h: 25, w: 50, post: 30 });
postSign('PROP_FE_Sign_Alt', [
  { draw: cv => arrowNW(cv, 4, 4, '#F2F2EA') }, { t: 'ALTERNATİF', y: 15 }, { t: 'GEÇİT', y: 22 }], { bg: GREEN, h: 30, w: 44, post: 18 });
// safe zone big sign (two posts)
postSign('PROP_FE_Sign_SafeZone', [
  { draw: cv => hillIcon(cv, 28, 8, '#F2F2EA') }, { draw: cv => { for (const [x, y] of [[24, 13], [27, 12], [30, 12], [33, 12], [36, 13]]) { cv.rect(x, y, 2, 3, '#F2F2EA'); cv.set(x, y - 1, '#F2F2EA'); } } },
  { t: 'GÜVENLİ', y: 19 }, { t: 'YÜKSEK ALAN', y: 26 }, { t: 'TOPLANMA', y: 33, c: '#BFF0CF' }], { bg: GREEN, h: 41, w: 62, post: 30, twoPosts: true });
// notice board (mahalle duyuru panosu): header, papers, a tiny route map with a red X and green arrow
{ const W = 52, bh = 34, post = 28; const cv = new Cv(W + 4, bh + post + 4);
  for (const px of [6, W - 5]) { cv.rect(px, bh, 3, post + 2, '#6E4428'); cv.rect(px + 2, bh, 1, post + 2, '#4A2C18'); }
  cv.rect(1, 1, W + 2, bh, '#7A4A2A'); cv.rect(3, 3, W - 2, bh - 4, '#B88A5A');
  for (let x = 3; x < W + 1; x++) for (let y = 3; y < bh - 1; y++) if (hash(x, y, 3) < 0.08) cv.set(x, y, '#A57A4C');
  cv.rect(3, 3, W - 2, 8, '#2F5DA8'); drawText(cv, 'DUYURU', fl((W + 4 - textW('DUYURU')) / 2), 4, '#F2F2EA');
  cv.rect(6, 13, 18, 18, '#EEF0EC'); for (let y = 15; y < 30; y += 3) cv.rect(8, y, 12 - (y % 2) * 3, 1, '#7D8794');
  cv.rect(27, 13, 22, 18, '#F6F1DC'); // route map
  cv.rect(29, 26, 18, 2, '#7D8794'); cv.rect(38, 16, 2, 10, '#7D8794');
  cv.rect(41, 26, 5, 2, '#5FB0E0'); for (let i = 0; i < 4; i++) { cv.set(41 + i, 23 + i, '#C0392B'); cv.set(44 - i, 23 + i, '#C0392B'); }
  cv.rect(37, 15, 4, 1, GREEN); cv.set(39, 14, GREEN); cv.set(38, 14, GREEN); cv.rect(39, 16, 1, 9, GREEN);
  cv.rect(15, 11, 2, 3, '#C0392B'); cv.rect(36, 11, 2, 3, '#C0392B');
  cv.rect(0, 0, W + 4, 2, '#5C3820'); cv.outline(OL);
  saveSingle('PROP_FE_NoticeBoard', cv, fl((W + 4) / 2), bh + post + 2, 'neighbourhood notice board (billboard), pivot = between the posts'); }

// =============================================================== GROUND ARROWS (decals on the path)
function groundArrow(name, dirU, dirV) { // flat chevron in grid plane, painted (semi transparent)
  const cv = new Cv(64, 40);
  for (let y = 0; y < 40; y++) for (let x = 0; x < 64; x++) {
    const X = x + 0.5 - 32, Y = 20 - (y + 0.5); const u = (Y / 16 + X / 32) / 2 * 32, v = (Y / 16 - X / 32) / 2 * 32; // px in grid units
    const a = u * dirU + v * dirV, b = -u * dirV + v * dirU;  // along / across
    const inChev = (Math.abs(b) < 11 && Math.abs(a - (8 - Math.abs(b) * 0.9)) < 3) || (Math.abs(b) < 11 && Math.abs(a - (-4 - Math.abs(b) * 0.9)) < 3);
    if (inChev) cv.set(x, y, hash(x, y, 2) < 0.15 ? '#9FDDB0' : '#7FD39A', 200);
  }
  saveSingle(name, cv, 32, 20, 'ground decal (order -90), pivot = centre');
}
groundArrow('DEC_FE_Arrow_GX', 1, 0); groundArrow('DEC_FE_Arrow_GY', 0, 1); groundArrow('DEC_FE_Arrow_GYm', 0, -1);

// ---- v3 ek sprite'lar: kavşak tabelaları (KISA YOL / UZUN YOL), yol kapalı tabelası, yıkıntı yığınları
const arrowW = (cv, x, y, c) => { for (let i = 0; i < 9; i++) { cv.set(x + i, y + 4, c); cv.set(x + i, y + 5, c); } for (let i = 0; i < 5; i++) { cv.set(x + i, y + 4 - i, c); cv.set(x + i, y + 5 + i, c); cv.set(x + i + 1, y + 4 - i, c); cv.set(x + i + 1, y + 5 + i, c); } };
const arrowE = (cv, x, y, c) => { for (let i = 0; i < 9; i++) { cv.set(x + i, y + 4, c); cv.set(x + i, y + 5, c); } for (let i = 0; i < 5; i++) { cv.set(x + 8 - i, y + 4 - i, c); cv.set(x + 8 - i, y + 5 + i, c); cv.set(x + 7 - i, y + 4 - i, c); cv.set(x + 7 - i, y + 5 + i, c); } };
postSign('PROP_FE_Sign_Kisa', [
  { t: 'KISA YOL', x: 5, y: 7 }, { draw: cv => arrowE(cv, 47, 5, '#F2F2EA') }], { bg: '#B8322A', border: '#F2F2EA', h: 18, w: 58, post: 30 });
postSign('PROP_FE_Sign_Uzun', [
  { draw: cv => arrowW(cv, 4, 5, '#F2F2EA') }, { t: 'UZUN YOL', x: 17, y: 7 }], { bg: '#1E8A4C', border: '#F2F2EA', h: 18, w: 58, post: 30 });
postSign('PROP_FE_Sign_Closed', [
  { t: 'YOL', y: 7 }, { t: 'KAPALI', y: 14 }], { bg: '#B8322A', h: 24, w: 34, post: 22, stripe: true });
// yıkıntı: beton blok + tuğla + eğik levha + inşaat demiri; 3 parça (her biri gy boyunca ~1.7 hücre), pivot = ön-sol kenar ortası
{
  const CON = ['#B9B4A8', '#9C978C', '#7E7A71', '#5E5B55'], CON2 = ['#A8A296', '#8A857A', '#6D695F', '#504D47'], BRK = ['#B0603E', '#924B2E', '#743A23', '#552918'];
  const tilt = (cu, cv_, cz, hu, hv, hz, ang) => { const c = Math.cos(ang), s = Math.sin(ang); return p => { const du = p.u - cu, dz = p.z - cz; const ru = du * c + dz * s, rz = -du * s + dz * c; return E.sdBox(V(ru, p.v - cv_, rz), V(0, 0, 0), V(hu, hv, hz)); }; };
  const conMat = pal => (p, n, ctx) => { const h = hash(fl(p.u / 3), fl(p.v / 3), fl(p.z / 3)); if (h < 0.06) return '#4A4741'; return ramp4(n, pal, (h - 0.5) * 0.15); };
  const brkMat = (p, n) => { const row = fl(p.z / 3); if (mod(p.z, 3) < 0.6 || mod(p.u + p.v + (row & 1) * 3, 6) < 0.6) return '#8E8A80'; return ramp4(n, BRK); };
  const rebar = (a, b) => ({ sdf: capsule(V(...a), V(...b), 0.7), mat: (p, n) => ramp4(n, ['#8A5A3A', '#6E4428', '#56331C', '#3A2212']) });
  const pieces = {
    A: [{ sdf: rbox(4, 30, 4, 30, 0, 16, 1.5), mat: conMat(CON) }, { sdf: tilt(36, 22, 9, 16, 12, 3, 0.55), mat: conMat(CON2) },
        { sdf: rbox(10, 22, 30, 44, 0, 7, 1), mat: brkMat }, { sdf: rbox(28, 40, 34, 46, 0, 5, 1), mat: brkMat }, rebar([18, 10, 15], [26, 4, 28]), rebar([22, 16, 15], [32, 14, 26])],
    B: [{ sdf: tilt(20, 26, 12, 20, 18, 4, -0.5), mat: conMat(CON) }, { sdf: rbox(26, 50, 6, 24, 0, 13, 1.5), mat: conMat(CON2) },
        { sdf: rbox(4, 18, 36, 50, 0, 9, 1), mat: brkMat }, { sdf: rbox(34, 48, 32, 48, 0, 6, 1), mat: conMat(CON) }, rebar([30, 30, 13], [40, 36, 30])],
    C: [{ sdf: rbox(6, 26, 8, 34, 0, 20, 1.5), mat: conMat(CON2) }, { sdf: tilt(36, 30, 8, 14, 16, 3, 0.7), mat: conMat(CON) },
        { sdf: rbox(26, 44, 4, 16, 0, 8, 1), mat: brkMat }, { sdf: rbox(8, 24, 38, 50, 0, 6, 1), mat: brkMat }, rebar([14, 20, 19], [10, 28, 34])],
  };
  for (const k in pieces) {
    const r = rend(pieces[k], { W: 140, H: 120, ox: 60, oy: 100, zmax: 60, shadow: (u, v) => (u > 0 && u < 54 && v > 0 && v < 54) ? 60 : 0 });
    // pivot: ön-sol kenarın ortası (u=0, v=27)
    saveSingle('PROP_FE_Rubble_' + k, r.cv, r.ox + (0 - 27), r.oy - (0 + 27) / 2, 'rubble pile; pivot = front-left edge middle (u0,v27)');
  }
}

// =============================================================== SKY (band + fills), evening deep blue
{ const W = 320, Hh = 160; const cv = new Cv(W, Hh);
  const top = rgb('#2A3C78'), mid = rgb('#3E4F8E'), low = rgb('#6A6C9E'), glow = rgb('#8C7C9C');
  for (let y = 0; y < Hh; y++) { const t = y / (Hh - 1); let c;
    if (t < 0.55) { const k = t / 0.55; c = top.map((v, i) => v + (mid[i] - v) * k); } else if (t < 0.85) { const k = (t - 0.55) / 0.3; c = mid.map((v, i) => v + (low[i] - v) * k); } else { const k = (t - 0.85) / 0.15; c = low.map((v, i) => v + (glow[i] - v) * k); }
    for (let x = 0; x < W; x++) { const d = hash(x, y, 1) < 0.5 && ((x + y) & 1) ? 3 : 0; cv.set(x, y, c.map(v => v - d)); } }
  // clouds (dark bands)
  for (const [cy, amp, col, ph] of [[30, 5, '#24346A', 0], [52, 4, '#2E3F7C', 2], [78, 3, '#3A4A86', 5]]) for (let x = 0; x < W; x++) { const yy = cy + Math.round(Math.sin((x + ph * 40) / 23) * amp + Math.sin((x + ph * 13) / 9) * 1.5); for (let k = 0; k < 6; k++) cv.set(x, yy + k, k === 0 ? mix(col, '#5A6AA6', 0.4) : col); }
  // stars
  for (let i = 0; i < 40; i++) { const x = fl(hash(i, 1, 9) * W), y = fl(hash(i, 2, 9) * 40); cv.set(x, y, '#C8D2F0'); }
  // distant skyline with lit windows + minaret
  const sk = rgb('#1C2448');
  for (let x = 0; x < W; x++) { const bH = 10 + fl(hash(fl(x / 9), 4, 4) * 16) + (hash(fl(x / 23), 5) < 0.3 ? 8 : 0); for (let y = Hh - bH; y < Hh; y++) cv.set(x, y, sk);
    for (let y = Hh - bH + 3; y < Hh - 2; y += 4) if (mod(x, 3) === 1 && hash(x, y, 7) < 0.22) cv.set(x, y, '#E8B85E'); }
  for (let y = Hh - 48; y < Hh; y++) { cv.set(212, y, sk); cv.set(213, y, sk); } cv.rect(211, Hh - 40, 4, 2, sk); cv.set(212, Hh - 50, sk);
  for (let dx = -8; dx <= 8; dx++) for (let dy = 0; dy < 9 - Math.abs(dx) / 1.2; dy++) cv.set(186 + dx, Hh - 26 - dy, sk);
  cv.save(OUTP + '/PROP_FE_Sky.png');
  man['PROP_FE_Sky'] = { file: 'PROP_FE_Sky', w: W, h: Hh, sprites: [
    { name: 'PROP_FE_Sky', x: 0, y: 0, w: W, h: Hh, px: 0.5, py: 0 },
    { name: 'PROP_FE_Sky_Fill', x: 0, y: 150, w: 32, h: 10, px: 0.5, py: 0 },
    { name: 'PROP_FE_Sky_FillBottom', x: 0, y: 0, w: 32, h: 8, px: 0.5, py: 1 }], note: 'same layout as PROP_Street_Sky (Band/Fill/FillBottom)' };
  console.log('sky'); }

fs.writeFileSync(C.OUT + '/props.json', JSON.stringify(man, null, 1));
