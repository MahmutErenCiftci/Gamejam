// ENV (akşam mahalle) ortak yardımcılar: yollar, PNG, renk, 3x5 Türkçe font, .meta yazıcı.
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const SCR = path.resolve(__dirname, 'lib');
const { encodePng, decodePng } = require(SCR + '/roomB_png.js');
const E = require(SCR + '/roomB_engine.js');
const PROJ = path.resolve(__dirname, '../..').split(path.sep).join('/');
const ART = PROJ + '/Assets/Art';
const DST = ART + '/FloodEvening/ENV';
const OUT = __dirname + '/out';
fs.mkdirSync(OUT, { recursive: true });

const rgb = h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)];
const toHex = a => '#' + a.map(x => Math.max(0, Math.min(255, Math.round(x))).toString(16).padStart(2, '0')).join('');
const mix = (a, b, t) => toHex(rgb(a).map((x, i) => x + (rgb(b)[i] - x) * t));
const fl = Math.floor, mod = (n, m) => ((n % m) + m) % m;
const hash = (a, b, c = 0) => { let h = (fl(a) * 374761393 + fl(b) * 668265263 + fl(c) * 2147483647) | 0; h = (h ^ (h >>> 13)) * 1274126177 | 0; h ^= h >>> 16; return ((h >>> 0) % 100000) / 100000; };
const OL = '#14141F';

// RGBA canvas, y-DOWN (row 0 = top), simple and fast
class Cv {
  constructor(w, h) { this.w = w; this.h = h; this.d = new Uint8Array(w * h * 4); }
  set(x, y, c, a = 255) { x = fl(x); y = fl(y); if (x < 0 || y < 0 || x >= this.w || y >= this.h || !c) return; const o = (y * this.w + x) * 4; const v = typeof c === 'string' ? rgb(c) : c; this.d[o] = v[0]; this.d[o + 1] = v[1]; this.d[o + 2] = v[2]; this.d[o + 3] = a; }
  get(x, y) { if (x < 0 || y < 0 || x >= this.w || y >= this.h) return null; const o = (y * this.w + x) * 4; return this.d[o + 3] ? [this.d[o], this.d[o + 1], this.d[o + 2], this.d[o + 3]] : null; }
  a(x, y) { if (x < 0 || y < 0 || x >= this.w || y >= this.h) return 0; return this.d[(y * this.w + x) * 4 + 3]; }
  rect(x0, y0, w, h, c) { for (let y = y0; y < y0 + h; y++) for (let x = x0; x < x0 + w; x++) this.set(x, y, c); }
  outline(col = OL) { const add = []; for (let y = 0; y < this.h; y++) for (let x = 0; x < this.w; x++) { if (this.a(x, y)) continue; if (this.a(x - 1, y) === 255 || this.a(x + 1, y) === 255 || this.a(x, y - 1) === 255 || this.a(x, y + 1) === 255) add.push([x, y]); } for (const [x, y] of add) this.set(x, y, col); return this; }
  blit(src, x0, y0) { for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) { const c = src.get(x, y); if (c) this.set(x0 + x, y0 + y, c, c[3]); } }
  save(file) { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, encodePng(this.w, this.h, this.d)); }
  static load(file) { const im = decodePng(fs.readFileSync(file)); const c = new Cv(im.w, im.h); c.d.set(im.rgba); return c; }
  crop() { let x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1; for (let y = 0; y < this.h; y++) for (let x = 0; x < this.w; x++) if (this.a(x, y)) { x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); } const n = new Cv(x1 - x0 + 1, y1 - y0 + 1); for (let y = 0; y < n.h; y++) for (let x = 0; x < n.w; x++) { const c = this.get(x + x0, y + y0); if (c) n.set(x, y, c, c[3]); } return { cv: n, x0, y0 }; }
}

// ---------- 3x5 font (caps + Turkish), glyph rows top->bottom; accents drawn above (row -2)
const F = {
  A: ['.#.', '#.#', '###', '#.#', '#.#'], B: ['##.', '#.#', '##.', '#.#', '##.'], C: ['.##', '#..', '#..', '#..', '.##'], D: ['##.', '#.#', '#.#', '#.#', '##.'],
  E: ['###', '#..', '##.', '#..', '###'], F: ['###', '#..', '##.', '#..', '#..'], G: ['.##', '#..', '#.#', '#.#', '.##'], H: ['#.#', '#.#', '###', '#.#', '#.#'],
  I: ['###', '.#.', '.#.', '.#.', '###'], J: ['..#', '..#', '..#', '#.#', '.#.'], K: ['#.#', '#.#', '##.', '#.#', '#.#'], L: ['#..', '#..', '#..', '#..', '###'],
  M: ['#...#', '##.##', '#.#.#', '#...#', '#...#'], N: ['#..#', '##.#', '#.##', '#..#', '#..#'], O: ['.#.', '#.#', '#.#', '#.#', '.#.'], P: ['##.', '#.#', '##.', '#..', '#..'],
  R: ['##.', '#.#', '##.', '#.#', '#.#'], S: ['.##', '#..', '.#.', '..#', '##.'], T: ['###', '.#.', '.#.', '.#.', '.#.'], U: ['#.#', '#.#', '#.#', '#.#', '###'],
  V: ['#.#', '#.#', '#.#', '#.#', '.#.'], Y: ['#.#', '#.#', '.#.', '.#.', '.#.'], Z: ['###', '..#', '.#.', '#..', '###'], W: ['#...#', '#...#', '#.#.#', '##.##', '#...#'],
  '0': ['###', '#.#', '#.#', '#.#', '###'], '1': ['.#', '##', '.#', '.#', '.#'], '2': ['##.', '..#', '.#.', '#..', '###'], '3': ['##.', '..#', '.#.', '..#', '##.'],
  '4': ['#.#', '#.#', '###', '..#', '..#'], '5': ['###', '#..', '##.', '..#', '##.'], '6': ['.##', '#..', '###', '#.#', '###'], '7': ['###', '..#', '.#.', '.#.', '.#.'],
  '8': ['###', '#.#', '###', '#.#', '###'], '9': ['###', '#.#', '###', '..#', '##.'], ' ': ['.', '.', '.', '.', '.'], '.': ['.', '.', '.', '.', '#'], ':': ['.', '#', '.', '#', '.'],
  '!': ['#', '#', '#', '.', '#'], '-': ['..', '..', '##', '..', '..'], '/': ['..#', '..#', '.#.', '#..', '#..'],
};
const ACC = { 'İ': ['I', [[1, -2]]], 'Ü': ['U', [[0, -2], [2, -2]]], 'Ö': ['O', [[0, -2], [2, -2]]], 'Ç': ['C', [[1, 5]]], 'Ş': ['S', [[0, 5]]], 'Ğ': ['G', [[0, -2], [1, -2], [2, -2]]] };
// returns {px:[[x,y]], w} with y relative to cap top (accents at -2 / descender at 5)
function text3(str, spacing = 1) {
  const px = []; let x = 0;
  for (const ch of str) {
    let g = F[ch], acc = [];
    if (!g && ACC[ch]) { g = F[ACC[ch][0]]; acc = ACC[ch][1]; }
    if (!g) throw new Error('glyph ' + ch);
    g.forEach((r, y) => [...r].forEach((c, i) => { if (c === '#') px.push([x + i, y]); }));
    for (const [ax, ay] of acc) px.push([x + ax, ay]);
    x += g[0].length + spacing;
  }
  return { px, w: x - spacing };
}
function drawText(cv, str, x0, y0, col, spacing = 1) { const t = text3(str, spacing); for (const [x, y] of t.px) cv.set(x0 + x, y0 + y, col); return t.w; }
const textW = (s, sp = 1) => text3(s, sp).w;

// ---------- .meta writer (Point, uncompressed, PPU 64)
const tplSingle = fs.readFileSync(ART + '/Props/Street/PROP_Street_Tree.png.meta', 'utf8');
function guidFor(metaPath) { if (fs.existsSync(metaPath)) { const m = fs.readFileSync(metaPath, 'utf8').match(/guid: (\w+)/); if (m) return m[1]; } return crypto.randomBytes(16).toString('hex'); }
const iid = (name) => (BigInt('0x' + crypto.createHash('md5').update(name).digest('hex').slice(0, 16)) & 0x3fffffffffffffffn).toString();
const sid = (name) => crypto.createHash('md5').update('sid' + name).digest('hex');
// sprites: [{name, x, y, w, h, px, py}] rect y from bottom; pivot normalized within rect
function metaText(guid, base, w, h, sprites) {
  if (sprites.length === 1 && sprites[0].name === base + '_0') {
    const s = sprites[0];
    return tplSingle.replace(/guid: \w+/, 'guid: ' + guid).split('PROP_Street_Tree_0').join(s.name).split('8906378047221654889').join(iid(s.name))
      .replace(/spritePivot: \{x: [\d.]+, y: [\d.]+\}/, `spritePivot: {x: ${s.px}, y: ${s.py}}`)
      .replace(/width: 71\n/, `width: ${w}\n`).replace(/height: 136\n/, `height: ${h}\n`)
      .replace('spriteID: 965ebfb55afc99b70800000000000000', 'spriteID: ' + sid(s.name))
      .replace(/maxTextureSize: 2048/g, 'maxTextureSize: 4096').replace(/textureCompression: 1/g, 'textureCompression: 0');
  }
  // multiple
  let t = tplSingle.replace(/guid: \w+/, 'guid: ' + guid).replace('spriteMode: 1', 'spriteMode: 2')
    .replace(/maxTextureSize: 2048/g, 'maxTextureSize: 4096').replace(/textureCompression: 1/g, 'textureCompression: 0');
  const table = sprites.map(s => `  - first:\n      213: ${iid(s.name)}\n    second: ${s.name}\n`).join('');
  t = t.replace(/  internalIDToNameTable:\n(  - first:\n      213: -?\d+\n    second: .*\n)+/, '  internalIDToNameTable:\n' + table);
  const sp = sprites.map(s => `    - serializedVersion: 2
      name: ${s.name}
      rect:
        serializedVersion: 2
        x: ${s.x}
        y: ${s.y}
        width: ${s.w}
        height: ${s.h}
      alignment: 9
      pivot: {x: ${s.px}, y: ${s.py}}
      border: {x: 0, y: 0, z: 0, w: 0}
      customData:
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: ${sid(s.name)}
      internalID: ${iid(s.name)}
      vertices: []
      indices:
      edges: []
      weights: []
`).join('');
  t = t.replace(/    sprites:\n    - serializedVersion: 2\n[\s\S]*?      weights: \[\]\n/, '    sprites:\n' + sp);
  t = t.replace(/    nameFileIdTable:\n      .*\n/, '    nameFileIdTable:\n' + sprites.map(s => `      ${s.name}: ${iid(s.name)}\n`).join(''));
  return t;
}

module.exports = { fs, path, SCR, PROJ, ART, DST, OUT, E, encodePng, decodePng, rgb, toHex, mix, fl, mod, hash, OL, Cv, text3, drawText, textW, metaText, guidFor, iid };
