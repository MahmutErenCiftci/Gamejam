// Referans sayfasını parçalara ayırır: arka plan (açık gri/beyaz) kenardan flood-fill ile silinir, satır bantları ve figürler bulunur.
const { decodePng, encodePng } = require('../sel_aksam_harita/lib/roomB_png.js'); const fs = require('fs');
function load(f) { return decodePng(fs.readFileSync(f)); }
function bgMask(im, thr = 226, sat = 22) {
  const { w, h, rgba } = im; const cand = new Uint8Array(w * h), bg = new Uint8Array(w * h);
  for (let i = 0; i < w * h; i++) { const r = rgba[i * 4], g = rgba[i * 4 + 1], b = rgba[i * 4 + 2]; const mx = Math.max(r, g, b), mn = Math.min(r, g, b); if (mn > thr && mx - mn < sat) cand[i] = 1; }
  const st = []; for (let x = 0; x < w; x++) { st.push(x, (h - 1) * w + x); } for (let y = 0; y < h; y++) { st.push(y * w, y * w + w - 1); }
  while (st.length) { const i = st.pop(); if (bg[i] || !cand[i]) continue; bg[i] = 1; const x = i % w, y = (i / w) | 0; if (x > 0) st.push(i - 1); if (x < w - 1) st.push(i + 1); if (y > 0) st.push(i - w); if (y < h - 1) st.push(i + w); }
  return bg;
}
function bands(fg, w, h, minGap = 4) { const rows = []; for (let y = 0; y < h; y++) { let n = 0; for (let x = 0; x < w; x++) n += fg[y * w + x]; rows.push(n); } const out = []; let s = -1, gap = 0;
  for (let y = 0; y < h; y++) { if (rows[y] > 2) { if (s < 0) s = y; gap = 0; } else if (s >= 0) { gap++; if (gap >= minGap) { out.push([s, y - gap]); s = -1; gap = 0; } } } if (s >= 0) out.push([s, h - 1]); return out; }
function cols(fg, w, y0, y1, minGap = 5) { const c = []; for (let x = 0; x < w; x++) { let n = 0; for (let y = y0; y <= y1; y++) n += fg[y * w + x]; c.push(n); } const out = []; let s = -1, gap = 0;
  for (let x = 0; x < w; x++) { if (c[x] > 1) { if (s < 0) s = x; gap = 0; } else if (s >= 0) { gap++; if (gap >= minGap) { out.push([s, x - gap]); s = -1; gap = 0; } } } if (s >= 0) out.push([s, w - 1]); return out; }
module.exports = { load, bgMask, bands, cols, encodePng };
if (require.main === module) {
  const im = load(process.argv[2]); const bg = bgMask(im); const fg = bg.map(v => 1 - v);
  for (const [a, b] of bands(fg, im.w, im.h)) { const cs = cols(fg, im.w, a, b); console.log('band', a, b, 'h', b - a + 1, 'parts', cs.length, cs.map(([x0, x1]) => x0 + '-' + x1).join(' ')); }
}
// bağlı bileşenler (8 komşu): [{x0,y0,x1,y1,n,id}], label dizisi
function comps(fg, w, h) { const lab = new Int32Array(w * h).fill(-1); const out = []; const st = [];
  for (let i = 0; i < w * h; i++) { if (!fg[i] || lab[i] >= 0) continue; const id = out.length; const c = { id, x0: 1e9, y0: 1e9, x1: -1, y1: -1, n: 0 }; st.push(i); lab[i] = id;
    while (st.length) { const j = st.pop(); const x = j % w, y = (j / w) | 0; c.n++; if (x < c.x0) c.x0 = x; if (x > c.x1) c.x1 = x; if (y < c.y0) c.y0 = y; if (y > c.y1) c.y1 = y;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue; const k = yy * w + xx; if (fg[k] && lab[k] < 0) { lab[k] = id; st.push(k); } } }
    out.push(c); }
  return { lab, comps: out }; }
module.exports.comps = comps;
