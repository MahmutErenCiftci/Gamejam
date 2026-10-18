// Yükseklik haritası izometrik zemin çizici: arazi parçaları (PNG), su kademeleri, yürüme maskesi → kenar poligonları.
// node terrain.js  → out/terrain/*.png, out/water/*.png, out/terrain.json, out/map_small.png
const C = require('./common.js');
const { fs, Cv, rgb, mix, fl, mod, hash, OL, decodePng, encodePng } = C;
const M = require('./map.js');
const { G, H, WALK, VOID, SURF, FACE, WATER, WATER_LEVELS, LINES } = M;

// ------------------------------------------------ tile samplers
const TILE_FILES = {
  Road_A: 'Tiles/Street/TILE_Street_Road_A', Road_B: 'Tiles/Street/TILE_Street_Road_B', Road_Lane: 'Tiles/Street/TILE_Street_Road_Lane',
  Road_Gutter: 'Tiles/Street/TILE_Street_Road_Gutter', Curb: 'Tiles/Street/TILE_Street_Curb', Sidewalk_A: 'Tiles/Street/TILE_Street_Sidewalk_A',
  Sidewalk_B: 'Tiles/Street/TILE_Street_Sidewalk_B', GrassS: 'Tiles/Street/TILE_Street_Grass', GrassA: 'Tiles/Assembly/TILE_Assembly_Grass_A',
  GrassB: 'Tiles/Assembly/TILE_Assembly_Grass_B', GrassC: 'Tiles/Assembly/TILE_Assembly_Grass_C', PathA: 'Tiles/Assembly/TILE_Assembly_Path_A', PathB: 'Tiles/Assembly/TILE_Assembly_Path_B',
};
const tiles = {};
for (const k in TILE_FILES) { const im = decodePng(fs.readFileSync(C.ART + '/' + TILE_FILES[k] + '.png')); tiles[k] = im; }
function tilePx(name, tx, tyDown) { const im = tiles[name]; if (tx < 0 || tyDown < 0 || tx >= im.w || tyDown >= im.h) return null; const o = (tyDown * im.w + tx) * 4; if (im.rgba[o + 3] < 128) return null; return [im.rgba[o], im.rgba[o + 1], im.rgba[o + 2]]; }
const MUD = rgb('#4A4034');
function mudTint(c, k = 0.55) { return [c[0] * (1 - k) + MUD[0] * k, c[1] * (1 - k) + MUD[1] * k * 1.02, c[2] * (1 - k) + MUD[2] * k]; }

// ------------------------------------------------ procedural surfaces (u,v = continuous cell coords)
const COB = ['#9A9283', '#8B8475', '#7C7569', '#A9A190'].map(rgb), COB_G = rgb('#57524A'), COB_HI = rgb('#BDB6A6');
function cobble(u, v, gxCell) {
  const n = 4; const r = fl(v * n); const uu = u * n + (r & 1) * 0.5; const s = fl(uu);
  const fu = uu - s, fv = v * n - r;
  if (fu < 0.13 || fv < 0.17) return COB_G;
  const hsh = hash(s, r, 3);
  let c = COB[fl(hsh * 4)];
  if (fu > 0.8 || fv > 0.82) c = c.map(x => x * 0.88);            // alt/sağ gölge kenarı
  else if (fu < 0.3 && fv < 0.4 && hash(s, r, 9) < 0.5) c = COB_HI; // ıslak parlama
  return c;
}
const STEP = { top: rgb('#B3AC9C'), nose: rgb('#CFC9BA'), riser: rgb('#8E877A'), riserD: rgb('#6E685E') };
const MUDC = ['#4B4136', '#433A30', '#55493C'].map(rgb);
function mud(u, v) { const h = hash(fl(u * 6), fl(v * 6), 5); return MUDC[h < 0.6 ? 0 : h < 0.85 ? 1 : 2]; }

// faces: a = position along face (px, absolute), z = absolute height (px), side 'L' (-gx, lit) | 'R' (-gy, shade)
const STONE_L = ['#8E8C86', '#827F78', '#9B9891', '#77746D'].map(rgb), STONE_R = ['#6B6963', '#615F59', '#74726B', '#58564F'].map(rgb), MORT_L = rgb('#5C5A55'), MORT_R = rgb('#46443F');
function stoneFace(a, z, side) {
  const row = fl((z + 512) / 8); const aa = a + (row & 1) * 6; const blk = fl(aa / 12);
  const L = side === 'L';
  if (mod(z, 8) < 1 || mod(aa, 12) < 1) return L ? MORT_L : MORT_R;
  const c = (L ? STONE_L : STONE_R)[fl(hash(blk, row, 4) * 4)];
  return c;
}
const CONC_L = rgb('#8A8F94'), CONC_R = rgb('#6A6F75'), CONC_S = rgb('#5A5650');
function concreteFace(a, z, side, zTop) {
  let c = side === 'L' ? CONC_L : CONC_R;
  if (mod(a, 32) < 1) c = c.map(x => x * 0.82);
  if (hash(fl(a / 2), fl(z / 3), 8) < 0.08) c = c.map(x => x * 0.92);
  if (z < -26 && hash(fl(a / 3), fl(z), 2) < 0.5) c = [c[0] * 0.8 + CONC_S[0] * 0.2, c[1] * 0.8 + CONC_S[1] * 0.2, c[2] * 0.8 + CONC_S[2] * 0.2]; // su izi
  return c;
}
function stepFace(a, z, side) { return side === 'L' ? STEP.riser : STEP.riserD; }

// ------------------------------------------------ buffers
const cells = [];
for (let gx = G.gx0; gx < G.gx1; gx++) for (let gy = G.gy0; gy < G.gy1; gy++) if (!VOID(gx, gy)) cells.push({ gx, gy, h: H(gx, gy) });
cells.sort((a, b) => (b.gx + b.gy) - (a.gx + a.gy) || a.gx - b.gx);
let Xmin = 1e9, Xmax = -1e9, Ymin = 1e9, Ymax = -1e9;
for (const c of cells) { const X = (c.gx - c.gy) * 32, Y = (c.gx + c.gy) * 16 + c.h; Xmin = Math.min(Xmin, X - 32); Xmax = Math.max(Xmax, X + 32); Ymin = Math.min(Ymin, Y - 140); Ymax = Math.max(Ymax, Y + 32); }
const W = Xmax - Xmin, HH = Ymax - Ymin;
console.log('buffer', W, 'x', HH);
const idx = (x, y) => (y - Ymin) * W + (x - Xmin);   // y-up
const cellIndex = new Map(); cells.forEach((c, i) => cellIndex.set(c.gx + ',' + c.gy, i));
const hAt = (gx, gy) => VOID(gx, gy) ? -9999 : H(gx, gy);

function paint(withWater, level, waterArea) {
  const col = new Uint8Array(W * HH * 3), own = new Int32Array(W * HH).fill(-1), kind = new Uint8Array(W * HH); // kind 1 top 2 faceL 3 faceR 4 water
  const put = (x, y, c, ci, k) => { const i = idx(x, y); col[i * 3] = c[0]; col[i * 3 + 1] = c[1]; col[i * 3 + 2] = c[2]; own[i] = ci; kind[i] = k; };
  cells.forEach((c, ci) => {
    const { gx, gy, h } = c; const Xc = (gx - gy) * 32, Yb = (gx + gy) * 16 + h;
    const surf = SURF(gx, gy); const [base, tint] = surf.split('~');
    // top diamond
    for (let y = Yb; y < Yb + 32; y++) for (let x = Xc - 32; x < Xc + 32; x++) {
      const Xp = x + 0.5, Yp = y + 0.5 - h; const u = (Yp / 16 + Xp / 32) / 2, v = (Yp / 16 - Xp / 32) / 2;
      if (u < gx || u >= gx + 1 || v < gy || v >= gy + 1) continue;
      let p;
      if (base === 'cobble') p = cobble(u, v);
      else if (base === 'step') { const fv = v - gy; p = fv < 0.18 ? STEP.nose : STEP.top; if (hash(fl(u * 8), fl(v * 8), 1) < 0.06) p = p.map(q => q * 0.93); }
      else if (base === 'mud') p = mud(u, v);
      else if (base === 'pavers') p = tilePx('Sidewalk_A', x - (Xc - 32), 31 - (y - Yb)) || tilePx('Sidewalk_A', 32, 16);
      else { p = tilePx(base, x - (Xc - 32), 31 - (y - Yb)); if (!p) p = tilePx(base, 32, 16); }
      if (tint === 'mud') p = mudTint(p, h < -20 ? 0.6 : 0.4);
      put(x, y, p, ci, 1);
    }
    // faces
    const fm = FACE(gx, gy);
    for (const side of ['L', 'R']) {
      const hn = side === 'L' ? hAt(gx - 1, gy) : hAt(gx, gy - 1);
      const drop = h - Math.max(hn, h - 400);
      if (drop <= 0) continue;
      const dz = Math.min(drop, 160);
      for (let x = (side === 'L' ? Xc - 32 : Xc); x < (side === 'L' ? Xc : Xc + 32); x++) {
        const Xp = x + 0.5; const yTop = side === 'L' ? Yb + (Xc - Xp) / 2 : Yb + (Xp - Xc) / 2;
        for (let y = Math.floor(yTop - dz); y < yTop; y++) {
          const Yp = y + 0.5; if (Yp >= yTop || Yp < yTop - dz) continue;
          const z = h - (yTop - Yp);                     // mutlak yükseklik
          const a = side === 'L' ? (gy + (Xc - Xp) / 32) * 32 : (gx + (Xp - Xc) / 32) * 32;
          let p = fm === 'concrete' ? concreteFace(a, z, side) : fm === 'step' ? stepFace(a, z, side) : stoneFace(a, z, side);
          if (yTop - Yp < 2 && fm !== 'step') p = p.map(q => Math.min(255, q * 1.18)); // kapak taşı
          if (Yp - (yTop - dz) < 1 && !(dz <= 4 && SURF(gx, gy) === 'cobble')) p = rgb(OL);   // dip çizgisi (rampa basamağında yok)
          if (dz <= 4 && SURF(gx, gy) === 'cobble') p = cobble((Xp / 32 + (yTop - Yp) / 16) / 2 + gx, gy + 0.05).map(q => q * 0.8);
          put(x, y, p, ci, side === 'L' ? 2 : 3);
        }
      }
    }
    // water surface
    if (withWater && WATER(gx, gy) === waterArea && h < level) {
      const Yw = (gx + gy) * 16 + level;
      for (let y = Yw; y < Yw + 32; y++) for (let x = Xc - 32; x < Xc + 32; x++) {
        const Xp = x + 0.5, Yp = y + 0.5 - level; const u = (Yp / 16 + Xp / 32) / 2, v = (Yp / 16 - Xp / 32) / 2;
        if (u < gx || u >= gx + 1 || v < gy || v >= gy + 1) continue;
        put(x, y, waterCol(x, y, level - h), ci, 4);
      }
    }
  });
  return { col, own, kind };
}
const WAT = ['#6E5C43', '#61503A', '#7B694F', '#564733'].map(rgb), WAT_HI = rgb('#A08C6C'), WAT_FOAM = rgb('#C6B79C');
function waterCol(x, y, depth) {
  const band = mod(fl(x * 0.5 + y * 1.0 + hash(fl(x / 9), fl(y / 3), 2) * 4), 13);
  let c = WAT[band < 6 ? 0 : band < 9 ? 1 : band < 11 ? 2 : 3];
  if (hash(fl(x / 3), fl(y), 6) < 0.035) c = WAT_HI;
  if (depth < 8) c = [c[0] * 0.75 + MUD[0] * 0.25, c[1] * 0.75 + MUD[1] * 0.25, c[2] * 0.75 + MUD[2] * 0.25];
  return c;
}

// ------------------------------------------------ base pass
const base = paint(false);
// walk mask
const walk = new Uint8Array(W * HH);
for (let i = 0; i < W * HH; i++) {
  const ci = base.own[i]; if (ci < 0) continue; const c = cells[ci]; const k = base.kind[i];
  if (!WALK(c.gx, c.gy)) continue;
  if (k === 1) walk[i] = 1;
  else if (k === 2 || k === 3) { const ngx = k === 2 ? c.gx - 1 : c.gx, ngy = k === 2 ? c.gy : c.gy - 1; if (WALK(ngx, ngy) && c.h - H(ngx, ngy) <= 8) walk[i] = 1; }
}
// subtract blocking wall strips (thickness ±0.32 cell around the line, at the line's height)
function stripPoly(L, a, b) { // grid-space quad corners for segment [a,b) of a line
  const t = 0.32;
  return L.axis === 'x' ? [[a, L.at - t], [b, L.at - t], [b, L.at + t], [a, L.at + t]] : [[L.at - t, a], [L.at + t, a], [L.at + t, b], [L.at - t, b]];
}
function fillGridPoly(poly, hpx, fn) { // rasterize a grid-space convex quad at height hpx into buffer pixels
  const pts = poly.map(([u, v]) => [(u - v) * 32, (u + v) * 16 + hpx]);
  const xs = pts.map(p => p[0]), ys = pts.map(p => p[1]);
  for (let y = fl(Math.min(...ys)); y < Math.max(...ys); y++) for (let x = fl(Math.min(...xs)); x < Math.max(...xs); x++) {
    const Xp = x + 0.5, Yp = y + 0.5; let inside = true;
    for (let k = 0; k < pts.length; k++) { const [x1, y1] = pts[k], [x2, y2] = pts[(k + 1) % pts.length]; if ((x2 - x1) * (Yp - y1) - (y2 - y1) * (Xp - x1) < 0) { inside = false; break; } }
    if (inside && x >= Xmin && x < Xmax && y >= Ymin && y < Ymax) fn(idx(x, y));
  }
}
const segsOf = L => { const out = []; let a = L.from; for (const [g0, g1] of (L.gaps || []).slice().sort((p, q) => p[0] - q[0])) { if (g0 > a) out.push([a, g0]); a = g1; } if (L.to > a) out.push([a, L.to]); return out; };
for (const L of LINES) if (L.block) for (const [a, b] of segsOf(L)) {
  const hpx = L.axis === 'x' ? H(fl(a), L.at) : H(L.at, fl(a));
  fillGridPoly(stripPoly(L, a, b), hpx, i => { walk[i] = 0; });
}

// ------------------------------------------------ contour tracing (pixel edges) + Douglas-Peucker
function traceLoops(mask) {
  // directed boundary edges with walkable on the left; vertices at pixel corners (x,y) in buffer px
  const key = (x, y) => x + ',' + y; const next = new Map();
  const add = (x1, y1, x2, y2) => { const k = key(x1, y1); if (!next.has(k)) next.set(k, []); next.get(k).push([x2, y2]); };
  const m = (x, y) => x >= Xmin && x < Xmax && y >= Ymin && y < Ymax && mask[idx(x, y)] === 1;
  for (let y = Ymin; y < Ymax; y++) for (let x = Xmin; x < Xmax; x++) {
    if (!m(x, y)) continue;
    if (!m(x, y - 1)) add(x, y, x + 1, y);           // bottom edge, going +x (walkable above = left)
    if (!m(x + 1, y)) add(x + 1, y, x + 1, y + 1);   // right edge going +y
    if (!m(x, y + 1)) add(x + 1, y + 1, x, y + 1);   // top edge going -x
    if (!m(x - 1, y)) add(x, y + 1, x, y);           // left edge going -y
  }
  const loops = [];
  while (next.size) {
    const [k0] = next.keys(); let [x, y] = k0.split(',').map(Number); const loop = [[x, y]]; let prevDir = null;
    for (let guard = 0; guard < 1e7; guard++) {
      const k = key(x, y); const lst = next.get(k); if (!lst) break;
      let pick = 0;
      if (lst.length > 1 && prevDir) { // at a pinch prefer turning left
        const cand = lst.map((p, i) => { const d = [p[0] - x, p[1] - y]; const cr = prevDir[0] * d[1] - prevDir[1] * d[0]; return [cr, i]; }).sort((a, b) => b[0] - a[0]); pick = cand[0][1]; }
      const [nx, ny] = lst.splice(pick, 1)[0]; if (!lst.length) next.delete(k);
      prevDir = [nx - x, ny - y]; x = nx; y = ny;
      if (x === loop[0][0] && y === loop[0][1]) break;
      loop.push([x, y]);
    }
    if (loop.length > 8) loops.push(loop);
  }
  return loops;
}
function dp(pts, eps) {
  if (pts.length < 3) return pts;
  let dmax = 0, ix = 0; const [a, b] = [pts[0], pts[pts.length - 1]];
  const L = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1e-9;
  for (let i = 1; i < pts.length - 1; i++) { const d = Math.abs((b[0] - a[0]) * (a[1] - pts[i][1]) - (a[0] - pts[i][0]) * (b[1] - a[1])) / L; if (d > dmax) { dmax = d; ix = i; } }
  if (dmax > eps) { const l = dp(pts.slice(0, ix + 1), eps), r = dp(pts.slice(ix), eps); return l.slice(0, -1).concat(r); }
  return [a, b];
}
function simplifyLoop(loop, eps) { // split closed loop at the farthest point pair
  let far = 0, fd = -1; for (let i = 0; i < loop.length; i++) { const d = Math.hypot(loop[i][0] - loop[0][0], loop[i][1] - loop[0][1]); if (d > fd) { fd = d; far = i; } }
  const a = dp(loop.slice(0, far + 1), eps), b = dp(loop.slice(far).concat([loop[0]]), eps);
  return a.slice(0, -1).concat(b.slice(0, -1));
}
const loops = traceLoops(walk).map(l => simplifyLoop(l, 1.2));
console.log('walk loops', loops.length, loops.map(l => l.length).join(','));

// ------------------------------------------------ visibility (camera reach) on 16px blocks
const B = 16, BW = Math.ceil(W / B), BH = Math.ceil(HH / B);
const wb = new Uint8Array(BW * BH); for (let y = Ymin; y < Ymax; y++) for (let x = Xmin; x < Xmax; x++) if (walk[idx(x, y)]) wb[fl((y - Ymin) / B) * BW + fl((x - Xmin) / B)] = 1;
const vis = new Uint8Array(BW * BH);
const rx = Math.ceil((240 + 40) / B), ryDown = Math.ceil((135 - 32 + 40) / B), ryUp = Math.ceil((135 + 32 + 40) / B);
for (let by = 0; by < BH; by++) for (let bx = 0; bx < BW; bx++) if (wb[by * BW + bx])
  for (let yy = Math.max(0, by - ryDown); yy <= Math.min(BH - 1, by + ryUp); yy++) for (let xx = Math.max(0, bx - rx); xx <= Math.min(BW - 1, bx + rx); xx++) vis[yy * BW + xx] = 1;
const visible = (x, y) => vis[fl((y - Ymin) / B) * BW + fl((x - Xmin) / B)] === 1;

// ------------------------------------------------ outputs
fs.mkdirSync(C.OUT + '/terrain', { recursive: true }); fs.mkdirSync(C.OUT + '/water', { recursive: true });
for (const f of fs.readdirSync(C.OUT + '/terrain')) fs.unlinkSync(C.OUT + '/terrain/' + f);
for (const f of fs.readdirSync(C.OUT + '/water')) fs.unlinkSync(C.OUT + '/water/' + f);
const CH = 1024; const chunks = [];
function writeRegion(buf, x0, y0, w, h, file, keep) { // buffer (y-up) region -> PNG (y-down)
  const out = new Uint8Array(w * h * 4); let any = false;
  for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) { const x = x0 + i, y = y0 + (h - 1 - j); if (x >= Xmax || y >= Ymax) continue; const k = idx(x, y); if (!keep(k, x, y)) continue;
    out.set([buf.col[k * 3], buf.col[k * 3 + 1], buf.col[k * 3 + 2], 255], (j * w + i) * 4); any = true; }
  if (any) fs.writeFileSync(file, encodePng(w, h, out));
  return any;
}
// trim to visible bbox
let vx0 = 1e9, vx1 = -1e9, vy0 = 1e9, vy1 = -1e9;
for (let by = 0; by < BH; by++) for (let bx = 0; bx < BW; bx++) if (vis[by * BW + bx]) { vx0 = Math.min(vx0, Xmin + bx * B); vx1 = Math.max(vx1, Xmin + bx * B + B); vy0 = Math.min(vy0, Ymin + by * B); vy1 = Math.max(vy1, Ymin + by * B + B); }
vx0 = Math.max(vx0, Xmin); vy0 = Math.max(vy0, Ymin); vx1 = Math.min(vx1, Xmax); vy1 = Math.min(vy1, Ymax);
for (let cy = vy0, j = 0; cy < vy1; cy += CH, j++) for (let cx = vx0, i = 0; cx < vx1; cx += CH, i++) {
  const w = Math.min(CH, vx1 - cx), h = Math.min(CH, vy1 - cy); const name = `TER_FE_Ground_${j}_${i}`;
  if (writeRegion(base, cx, cy, w, h, C.OUT + '/terrain/' + name + '.png', (k, x, y) => base.own[k] >= 0 && visible(x, y))) chunks.push({ name, x: cx, y: cy, w, h });
}
console.log('chunks', chunks.length);
// water stages: per area & level, keep only water pixels
const water = [];
for (const area of Object.keys(WATER_LEVELS)) WATER_LEVELS[area].forEach((lv, s) => {
  const r = paint(true, lv, area);
  let x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
  for (let y = vy0; y < vy1; y++) for (let x = vx0; x < vx1; x++) { const k = idx(x, y); if (r.kind[k] === 4 && visible(x, y)) { x0 = Math.min(x0, x); x1 = Math.max(x1, x + 1); y0 = Math.min(y0, y); y1 = Math.max(y1, y + 1); } }
  if (x1 < x0) return;
  // foam: water pixel whose upper neighbour is not water
  const fo = []; for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) { const k = idx(x, y); if (r.kind[k] !== 4) continue; const up = y + 1 < Ymax ? r.kind[idx(x, y + 1)] : 0; if (up !== 4) fo.push(k); }
  for (const k of fo) r.col.set(WAT_FOAM, k * 3);
  for (let cy = y0, j = 0; cy < y1; cy += CH, j++) for (let cx = x0, i = 0; cx < x1; cx += CH, i++) {
    const w = Math.min(CH, x1 - cx), h = Math.min(CH, y1 - cy); const name = `FX_FE_Water_${area}_S${s + 1}_${j}_${i}`;
    if (writeRegion(r, cx, cy, w, h, C.OUT + '/water/' + name + '.png', (k, x, y) => r.kind[k] === 4 && visible(x, y))) water.push({ name, area, stage: s + 1, level: lv, x: cx, y: cy, w, h });
  }
});
console.log('water sprites', water.length);
// world-unit loops
const loopsW = loops.map(l => l.map(([x, y]) => [+(x / 64).toFixed(4), +(y / 64).toFixed(4)]));
// camera bounds = walk bbox + half view
let wx0 = 1e9, wx1 = -1e9, wy0 = 1e9, wy1 = -1e9; for (const l of loopsW) for (const [x, y] of l) { wx0 = Math.min(wx0, x); wx1 = Math.max(wx1, x); wy0 = Math.min(wy0, y); wy1 = Math.max(wy1, y); }
const cam = { cx: (wx0 + wx1) / 2, cy: (wy0 + wy1 + 0.5) / 2, w: (wx1 - wx0) + 7.5 + 0.2, h: (wy1 - wy0) + 4.22 + 0.5 + 0.2 };
const visBlocks = { x0: Xmin, y0: Ymin, B, BW, BH, bits: Buffer.from(vis).toString('base64') };
fs.writeFileSync(C.OUT + '/terrain.json', JSON.stringify({ chunks, water, loops: loopsW, cam, walkBBox: [wx0, wy0, wx1, wy1], visible: [vx0, vy0, vx1, vy1], visBlocks }, null, 0));
// small overview (1/4)
{ const s = 4, w = Math.ceil((vx1 - vx0) / s), h = Math.ceil((vy1 - vy0) / s), o = new Uint8Array(w * h * 4);
  for (let j = 0; j < h; j++) for (let i = 0; i < w; i++) { const x = vx0 + i * s, y = vy1 - 1 - j * s; const k = idx(x, y); let c = [30, 34, 60];
    if (base.own[k] >= 0 && visible(x, y)) c = [base.col[k * 3], base.col[k * 3 + 1], base.col[k * 3 + 2]]; if (walk[k]) c = [c[0] * 0.6 + 90, c[1] * 0.6 + 40, c[2] * 0.6];
    o.set([...c, 255], (j * w + i) * 4); }
  fs.writeFileSync(C.OUT + '/map_small.png', encodePng(w, h, o)); }
// export walk mask + base for preview compositor
fs.writeFileSync(C.OUT + '/walk_meta.json', JSON.stringify({ Xmin, Ymin, W, HH }));
fs.writeFileSync(C.OUT + '/walk.bin', walk);
console.log('cam', JSON.stringify(cam));
