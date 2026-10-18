// Bina sprite'larının (cephe/gövde/apartman) arkasında kalan yürünebilir hücreleri örtüp örtmediğini bulur.
const C = require('./common.js'); const { fs, decodePng, fl } = C; const M = require('./map.js'); const L = require('./layout.js');
const world = (gx, gy, h) => [(gx - gy) / 2, (gx + gy) / 4 + h / 64];
const cache = {};
function spr(s) { if (cache[s]) return cache[s]; const f = C.ART + '/' + s.slice(2) + '.png'; const im = decodePng(fs.readFileSync(f)); const meta = fs.readFileSync(f + '.meta', 'utf8'); const mm = meta.match(/spritePivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}/); return cache[s] = { im, px: +mm[1] * im.w, py: +mm[2] * im.h }; }
const walk = []; for (let gx = M.G.gx0; gx < M.G.gx1; gx++) for (let gy = M.G.gy0; gy < M.G.gy1; gy++) if (M.WALK(gx, gy)) walk.push([gx + 0.5, gy + 0.5]);
const bad = {};
for (const o of L.objs) { if (o.group !== 'Buildings' || !o.s.startsWith('A:')) continue; const s = spr(o.s); const [x, y] = world(o.gx, o.gy, o.h);
  for (const [cx, cy] of walk) { const [wx, wy] = world(cx, cy, 0); if (wy <= y + 0.05) continue;             // hücre binanın önünde → sorun değil
    for (const dz of [0]) { let lx = (wx - x) * 64; if (o.flip) lx = -lx; const px = Math.round(s.px + lx), pyUp = Math.round(s.py + (wy + dz - y) * 64);
      if (px < 0 || px >= s.im.w || pyUp < 0 || pyUp >= s.im.h) continue; const a = s.im.rgba[((s.im.h - 1 - pyUp) * s.im.w + px) * 4 + 3]; if (a > 128) { const k = o.name; (bad[k] = bad[k] || new Set()).add(M.corridorOf(fl(cx), fl(cy)) + '@' + fl(cx) + ',' + fl(cy)); break; } } } }
for (const k in bad) console.log(k, bad[k].size, [...bad[k]].slice(0, 6).join(' '));
console.log('done', Object.keys(bad).length);
// dekor objelerinin taban noktası bir binanın arkasında ve silüetinin içindeyse (çatı üstünde ağaç gibi)
for (const p of L.objs) { if (p.group === 'Buildings' || p.group === 'Decals') continue; const [wx, wy] = world(p.gx, p.gy, p.h);
  for (const o of L.objs) { if (o.group !== 'Buildings' || !o.s.startsWith('A:')) continue; const s = spr(o.s); const [x, y] = world(o.gx, o.gy, o.h); if (wy <= y + 0.05) continue;
    let lx = (wx - x) * 64; if (o.flip) lx = -lx; const px = Math.round(s.px + lx), pyUp = Math.round(s.py + (wy - y) * 64);
    if (px < 0 || px >= s.im.w || pyUp < 0 || pyUp >= s.im.h) continue; if (s.im.rgba[((s.im.h - 1 - pyUp) * s.im.w + px) * 4 + 3] > 128) { console.log('PROP ON ROOF', p.name, 'behind', o.name); break; } } }
