// FLOOD ENDING art helpers (bus interior + school route). Same engine/palette conventions as the street/kitchen art.
const path = require('path'), fs = require('fs');
// roomA_lib.js / roomB_engine.js / street_common.js repoda yok; ART_LIB ile göster (varsayılan: üretildiği oturumun geçici klasörü)
const SP = process.env.ART_LIB || 'C:/Users/mahmu/AppData/Local/Temp/claude/C--Users-mahmu-Gamejam/d033b3a8-4973-4fbd-974a-b7640f61e1f1/scratchpad';
const { hex, Img, worldSprite, overlay, hash } = require(SP + '/roomA_lib.js');
const E = require(SP + '/roomB_engine.js');
const SC = require(SP + '/street_common.js');
const PROJ = 'C:/Users/mahmu/Gamejam';
const fl = Math.floor, mod = (n, m) => ((n % m) + m) % m;
const chk = (x, y) => ((fl(x) + fl(y)) & 1) === 0;
const H = h => hex(h);
// world pixel (y up) <-> grid
const g2p = (gx, gy) => [(gx - gy) * 32, (gx + gy) * 16];
const p2g = (X, Y) => [(X / 32 + Y / 16) / 2, (Y / 16 - X / 32) / 2];
const ramp = (n, cols, bias = 0) => { const l = E.lambert(n) + bias; return l > 0.85 ? cols[0] : l > 0.55 ? cols[1] : l > 0.25 ? cols[2] : cols[3]; };

function makeManifest(note) { return { note, ppu: 64, files: [] }; }
function emitTo(manifest, img, file, pivot, extra) {
  img.save(file);
  manifest.files.push(Object.assign({ file: path.relative(PROJ, file).split(path.sep).join('/'), size: [img.w, img.h], pivotPx: pivot,
    pivotNorm: [+(pivot[0] / img.w).toFixed(6), +(pivot[1] / img.h).toFixed(6)] }, extra || {}));
  return img;
}
function renderProp(objs, opt = {}) {
  const W = opt.W || 220, Hh = opt.H || 240, ox = opt.ox || 110, oy = opt.oy || 200;
  const r = E.render(objs, W, Hh, ox, oy, Object.assign({ zmax: opt.zmax || 170 }, opt));
  if (opt.post) opt.post(r);
  return SC.cropImg(Object.assign(SC.toImg(r), { keepPivot: true }));
}
const scr = (r, u, v, z) => [Math.floor(r.ox + (u - v)), Math.floor(r.oy - ((u + v) / 2 + z))];
// alpha blend colour c (rgba array) over existing pixel of Img at (i, jUp)
function blend(img, i, j, c) {
  if (i < 0 || j < 0 || i >= img.w || j >= img.h) return;
  const o = img.idx(i, j), a = c[3] / 255, ba = img.d[o + 3] / 255, oa = a + ba * (1 - a);
  if (oa <= 0) return;
  for (let k = 0; k < 3; k++) img.d[o + k] = Math.round((c[k] * a + img.d[o + k] * ba * (1 - a)) / oa);
  img.d[o + 3] = Math.round(oa * 255);
}
// 5x7 font (street G5 + extra capitals). Returns pixel list relative to the top-left of the cap box (y down).
const G = {};
function g(ch, top, rows) { G[ch] = { top, rows }; }
g('A', 2, ['.###.', '#...#', '#...#', '#####', '#...#', '#...#', '#...#']);
g('B', 2, ['####.', '#...#', '#...#', '####.', '#...#', '#...#', '####.']);
g('C', 2, ['.####', '#....', '#....', '#....', '#....', '#....', '.####']);
g('Ç', 2, ['.####', '#....', '#....', '#....', '#....', '#....', '.####', '..#..', '.##..']);
g('D', 2, ['####.', '#...#', '#...#', '#...#', '#...#', '#...#', '####.']);
g('E', 2, ['#####', '#....', '#....', '####.', '#....', '#....', '#####']);
g('F', 2, ['#####', '#....', '#....', '####.', '#....', '#....', '#....']);
g('G', 2, ['.####', '#....', '#....', '#.###', '#...#', '#...#', '.###.']);
g('Ğ', 0, ['.###.', '.....', '.####', '#....', '#....', '#.###', '#...#', '#...#', '.###.']);
g('H', 2, ['#...#', '#...#', '#...#', '#####', '#...#', '#...#', '#...#']);
g('I', 2, ['###', '.#.', '.#.', '.#.', '.#.', '.#.', '###']);
g('İ', 0, ['.#.', '...', '###', '.#.', '.#.', '.#.', '.#.', '.#.', '###']);
g('K', 2, ['#...#', '#..#.', '#.#..', '##...', '#.#..', '#..#.', '#...#']);
g('L', 2, ['#....', '#....', '#....', '#....', '#....', '#....', '#####']);
g('M', 2, ['#...#', '##.##', '#.#.#', '#.#.#', '#...#', '#...#', '#...#']);
g('N', 2, ['#...#', '##..#', '#.#.#', '#..##', '#...#', '#...#', '#...#']);
g('O', 2, ['.###.', '#...#', '#...#', '#...#', '#...#', '#...#', '.###.']);
g('Ö', 0, ['.#.#.', '.....', '.###.', '#...#', '#...#', '#...#', '#...#', '#...#', '.###.']);
g('P', 2, ['####.', '#...#', '#...#', '####.', '#....', '#....', '#....']);
g('R', 2, ['####.', '#...#', '#...#', '####.', '#.#..', '#..#.', '#...#']);
g('S', 2, ['.####', '#....', '#....', '.###.', '....#', '....#', '####.']);
g('Ş', 2, ['.####', '#....', '#....', '.###.', '....#', '....#', '####.', '..#..', '.##..']);
g('T', 2, ['#####', '..#..', '..#..', '..#..', '..#..', '..#..', '..#..']);
g('U', 2, ['#...#', '#...#', '#...#', '#...#', '#...#', '#...#', '.###.']);
g('Ü', 0, ['.#.#.', '.....', '#...#', '#...#', '#...#', '#...#', '#...#', '#...#', '.###.']);
g('V', 2, ['#...#', '#...#', '#...#', '#...#', '.#.#.', '.#.#.', '..#..']);
g('Y', 2, ['#...#', '#...#', '.#.#.', '..#..', '..#..', '..#..', '..#..']);
g('Z', 2, ['#####', '....#', '...#.', '..#..', '.#...', '#....', '#####']);
g('!', 2, ['#', '#', '#', '#', '#', '.', '#']);
g('.', 8, ['#']);
g(' ', 8, ['..']);
g('-', 5, ['###']);
g(':', 4, ['#', '.', '.', '#']);
for (let d = 0; d <= 9; d++) g(String(d), 2, [
  ['.###.', '#...#', '#..##', '#.#.#', '##..#', '#...#', '.###.'], ['..#..', '.##..', '..#..', '..#..', '..#..', '..#..', '.###.'],
  ['.###.', '#...#', '....#', '...#.', '..#..', '.#...', '#####'], ['####.', '....#', '....#', '.###.', '....#', '....#', '####.'],
  ['...#.', '..##.', '.#.#.', '#..#.', '#####', '...#.', '...#.'], ['#####', '#....', '####.', '....#', '....#', '#...#', '.###.'],
  ['.###.', '#....', '#....', '####.', '#...#', '#...#', '.###.'], ['#####', '....#', '...#.', '..#..', '.#...', '.#...', '.#...'],
  ['.###.', '#...#', '#...#', '.###.', '#...#', '#...#', '.###.'], ['.###.', '#...#', '#...#', '.####', '....#', '....#', '.###.']][d]);
function text7(str, spacing = 1) {
  const px = []; let x = 0;
  for (const ch of str) { const gg = G[ch]; if (!gg) throw new Error('glyph ' + ch); gg.rows.forEach((r, y) => [...r].forEach((c, i) => { if (c === '#') px.push([x + i, gg.top + y]); })); x += gg.rows[0].length + spacing; }
  return { px, w: x - spacing, h: 11 };
}
module.exports = { path, fs, hex, Img, worldSprite, overlay, hash, E, SC, PROJ, fl, mod, chk, H, g2p, p2g, ramp, makeManifest, emitTo, renderProp, scr, blend, text7, text3: SC.text3 };
