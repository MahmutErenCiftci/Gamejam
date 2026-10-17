// Çevrim dışı kamera önizlemesi: node preview.js <tag> <stage 0..3> gx,gy [gx,gy ...]  → shots/pv_<tag>_<i>.png (480x270 x2)
const C = require('./common.js');
const { fs, decodePng, encodePng, fl } = C;
const L = require('./layout.js'); const M = require('./map.js');
const T = JSON.parse(fs.readFileSync(C.OUT + '/terrain.json', 'utf8'));
const PR = JSON.parse(fs.readFileSync(C.OUT + '/props.json', 'utf8'));
const [tag, stageS, ...pts] = process.argv.slice(2); const stage = +stageS;
const lit = !process.env.NOLIGHT;
const cache = {};
function spr(s) {
  if (cache[s]) return cache[s];
  let im, px, py;
  if (s.startsWith('N:')) { const n = s.slice(2); const m = PR[n]; im = decodePng(fs.readFileSync(C.OUT + '/props/' + m.file + '.png')); px = m.sprites[0].px * im.w; py = m.sprites[0].py * im.h; }
  else { const f = C.ART + '/' + s.slice(2) + '.png'; im = decodePng(fs.readFileSync(f)); const meta = fs.readFileSync(f + '.meta', 'utf8'); const mm = meta.match(/spritePivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}/); px = +mm[1] * im.w; py = +mm[2] * im.h;
    if (/spriteMode: 2/.test(meta)) { const r = meta.match(/rect:\s*\n\s*serializedVersion: 2\s*\n\s*x: (\d+)\s*\n\s*y: (\d+)\s*\n\s*width: (\d+)\s*\n\s*height: (\d+)[\s\S]*?pivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}/); if (r) { const [x, y, w, h] = [+r[1], +r[2], +r[3], +r[4]]; const sub = { w, h, rgba: new Uint8Array(w * h * 4) }; for (let j = 0; j < h; j++) sub.rgba.set(im.rgba.subarray(((im.h - y - h + j) * im.w + x) * 4, ((im.h - y - h + j) * im.w + x + w) * 4), j * w * 4); im = sub; px = +r[5] * w; py = +r[6] * h; } } }
  return cache[s] = { im, px, py };
}
function subSprite(file, sp) { const key = file + '#' + sp.name; if (cache[key]) return cache[key]; const big = cache['F:' + file] || (cache['F:' + file] = decodePng(fs.readFileSync(C.OUT + '/props/' + file + '.png')));
  const { x, y, w, h } = sp; const im = { w, h, rgba: new Uint8Array(w * h * 4) }; for (let j = 0; j < h; j++) im.rgba.set(big.rgba.subarray(((big.h - y - h + j) * big.w + x) * 4, ((big.h - y - h + j) * big.w + x + w) * 4), j * w * 4);
  return cache[key] = { im, px: sp.px * w, py: sp.py * h }; }
const world = (gx, gy, h) => [(gx - gy) / 2, (gx + gy) / 4 + h / 64];
const items = [];
for (const c of T.chunks) items.push({ im: decodePng(fs.readFileSync(C.OUT + '/terrain/' + c.name + '.png')), px: 0, py: 0, x: c.x / 64, y: c.y / 64, order: -100 });
if (stage > 0) for (const w of T.water) if (w.stage <= stage) items.push({ im: decodePng(fs.readFileSync(C.OUT + '/water/' + w.name + '.png')), px: 0, py: 0, x: w.x / 64, y: w.y / 64, order: -99 + w.stage * 0, alpha: 0.85 });
for (const o of L.objs) { const s = spr(o.s); const [x, y] = world(o.gx, o.gy, o.h); items.push({ ...s, x, y, order: o.order, flip: o.flip, light: o.light }); }
for (const n in PR) { const m = PR[n]; if (!m.line) continue; const [ox, oy] = world(m.line.origin[0], m.line.origin[1], m.line.h0);
  m.line.strips.forEach((st, i) => { const sp = m.sprites.find(q => q.name === st.sprite); const s = subSprite(m.file, sp); items.push({ ...s, x: ox + st.dx / 64, y: oy + st.dy / 64, order: 0 }); }); }
const open = process.env.OPEN === '1';
for (const g of L.gates) { const hb = g.axis === 'y' ? Math.max(M.H(fl(g.gx), fl(g.gy)), M.H(fl(g.gx) - 1, fl(g.gy))) : Math.max(M.H(fl(g.gx), fl(g.gy)), M.H(fl(g.gx), fl(g.gy) - 1)); const [x, y] = world(g.gx, g.gy, hb);
  if (!open) items.push({ ...spr(g.closed), x, y, order: 0, flip: g.flip });
  else for (const [s, du, dv] of g.open) { const [x2, y2] = world(g.gx + du, g.gy + dv, hb); items.push({ ...spr(s), x: x2, y: y2, order: 0, flip: g.flip }); } }
// Cenk
const cenkSheet = decodePng(fs.readFileSync(C.ART + '/Characters/Cenk/CHR_Cenk_Walk_East.png')); const cenk = { w: 96, h: 96, rgba: new Uint8Array(96 * 96 * 4) };
for (let y = 0; y < 96; y++) cenk.rgba.set(cenkSheet.rgba.subarray(y * cenkSheet.w * 4, (y * cenkSheet.w + 96) * 4), y * 96 * 4);
const sky = spr('N:PROP_FE_Sky');
const lamps = items.filter(i => i.light);
const G = [0.62 * 0.8, 0.68 * 0.8, 0.94 * 0.8];
fs.mkdirSync(__dirname + '/shots', { recursive: true });
pts.forEach((p, idx) => {
  const [gx, gy] = p.split(',').map(Number); const h = M.H(fl(gx), fl(gy)); const [px0, py0] = world(gx, gy, h);
  const Wv = 480, Hv = 270, hw = Wv / 128, hh = Hv / 128; let cx = px0, cy = py0 + 0.5; const cam = T.cam;
  cx = Math.min(Math.max(cx, cam.cx - cam.w / 2 + hw), cam.cx + cam.w / 2 - hw); cy = Math.min(Math.max(cy, cam.cy - cam.h / 2 + hh), cam.cy + cam.h / 2 - hh);
  cx = Math.round(cx * 64) / 64; cy = Math.round(cy * 64) / 64;
  const cv = new Float32Array(Wv * Hv * 3);
  // sky band: bottom at cam y - 2.1, tiled
  const bandBottom = cy - 2.1; const top = Math.round((cy + hh - (bandBottom + sky.im.h / 64)) * 64);
  for (let y = 0; y < Hv; y++) for (let x = 0; x < Wv; x++) { const sy = Math.min(sky.im.h - 1, Math.max(0, y - top)); const sx = ((x + Math.round(cx * 64 * 0.7)) % sky.im.w + sky.im.w) % sky.im.w; const o = (sy * sky.im.w + sx) * 4; for (let k = 0; k < 3; k++) cv[(y * Wv + x) * 3 + k] = sky.im.rgba[o + k]; }
  const list = [...items, { im: cenk, px: 48, py: 19, x: px0, y: py0, order: 0 }];
  list.sort((a, b) => a.order - b.order || b.y - a.y);
  for (const it of list) { const s = it; const x0 = Math.round((it.x - cx) * 64 + Wv / 2 - (it.flip ? s.im.w - s.px : s.px)), yTop = Math.round(Hv / 2 - ((it.y - cy) * 64 + (s.im.h - s.py)));
    if (x0 > Wv || x0 + s.im.w < 0 || yTop > Hv || yTop + s.im.h < 0) continue;
    for (let j = 0; j < s.im.h; j++) { const yy = yTop + j; if (yy < 0 || yy >= Hv) continue; for (let i = 0; i < s.im.w; i++) { const xx = x0 + i; if (xx < 0 || xx >= Wv) continue; const si = it.flip ? s.im.w - 1 - i : i; const o = (j * s.im.w + si) * 4; const a = s.im.rgba[o + 3] / 255 * (it.alpha || 1); if (!a) continue; const q = (yy * Wv + xx) * 3; for (let k = 0; k < 3; k++) cv[q + k] = cv[q + k] * (1 - a) + s.im.rgba[o + k] * a; } } }
  // lighting
  const out = new Uint8Array(Wv * 2 * Hv * 2 * 4);
  for (let y = 0; y < Hv; y++) for (let x = 0; x < Wv; x++) { const q = (y * Wv + x) * 3; let m = lit ? [...G] : [1, 1, 1];
    if (lit) { const wx = cx + (x - Wv / 2) / 64, wy = cy + (Hv / 2 - y) / 64; for (const l of lamps) { const d = Math.hypot(wx - (l.x + 0.22), wy - (l.y - 0.1)); if (d < 2.0) { const f = (1 - d / 2.0) ** 1.3 * 0.9; m[0] += f * 1.0; m[1] += f * 0.84; m[2] += f * 0.6; } } }
    const c = [0, 1, 2].map(k => Math.min(255, cv[q + k] * m[k]));
    for (let a = 0; a < 2; a++) for (let b = 0; b < 2; b++) out.set([c[0], c[1], c[2], 255], (((y * 2 + a) * Wv * 2) + x * 2 + b) * 4); }
  fs.writeFileSync(__dirname + `/shots/pv_${tag}_${idx}.png`, encodePng(Wv * 2, Hv * 2, out));
});
console.log('ok', pts.length);
