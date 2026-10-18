// Unity'siz önizleme: cephe + gövde + manifest katmanları + Cenk, gece ışığıyla. Görselleri yinelemek için.
//   node preview_world.js out.png [anahtar=değer ...]
//   angle=0..90  hall=0|1  lid=0|1|2  keypad=0..3  handle=0|1  cenk=u,d  cenkSprite=dosya.png#kare  scale=3  focus=u,d  hl=mail|lock
'use strict';
const fs = require('fs'), path = require('path');
const C = require('../../sel_aksam_harita/common.js');
const ROOT = path.resolve(__dirname, '../../..').split(path.sep).join('/');
const ART = process.env.ART_OUT || (ROOT + '/Assets/Art/FloodEvening/Final/World');
const M = JSON.parse(fs.readFileSync(__dirname + '/out/manifest.json', 'utf8'));
const args = Object.fromEntries(process.argv.slice(3).map(a => a.split('=')));
const out = process.argv[2];
const num = (k, d) => args[k] !== undefined ? +args[k] : d;
const angle = num('angle', 0), hall = num('hall', 0), lid = num('lid', 0), keypad = num('keypad', 0), handle = num('handle', 0), SC = num('scale', 3);
const VW = 300, VH = 190;
const [fu, fd] = (args.focus || '1.6,-1.2').split(',').map(Number);
// dünya ↔ ekran (çapa = (0,0)); 1 birim = 64 px
const wl = (u, d) => [(u - d) / 2, (u + d) / 4];
const [cxw, cyw] = wl(fu, fd);
const scr = (wx, wy) => [VW / 2 + (wx - cxw) * 64, VH / 2 - (wy - cyw) * 64];
const buf = new Float32Array(VW * VH * 3);
const lights = [];
// ışık: global akşam + kapı + lamba
const GL = [0.50, 0.54, 0.75];
const anchors = M.anchors;
const open01 = Math.min(1, angle / 90);
lights.push({ p: anchors.LampLight, y: M.lampHeight, r: 1.7, c: [1.0, 0.82, 0.55], k: 0.9 });
if (hall) lights.push({ p: anchors.DoorLight, y: 0.35, r: 2.2, c: [1.0, 0.78, 0.45], k: 1.3 * (0.15 + 0.85 * Math.pow(open01, 0.6)) });
function lightAt(wx, wy) { const m = [...GL]; for (const l of lights) { const d = Math.hypot(wx - l.p[0], wy - (l.p[1] + l.y * 0.3)); if (d < l.r) { const f = Math.pow(1 - d / l.r, 1.4) * l.k; for (let i = 0; i < 3; i++) m[i] += f * l.c[i]; } } return m; }
function load(file) { const im = C.decodePng(fs.readFileSync(file)); return im; }
function sub(im, k, w, h) { const o = { w, h, rgba: new Uint8Array(w * h * 4) }; for (let y = 0; y < h; y++) o.rgba.set(im.rgba.subarray((y * im.w + k * w) * 4, (y * im.w + k * w + w) * 4), y * w * 4); return o; }
const draws = [];
// zemin (avlu taşları)
{ const g = { w: VW, h: VH, rgba: new Uint8Array(VW * VH * 4) }; for (let y = 0; y < VH; y++) for (let x = 0; x < VW; x++) { const wx = (x - VW / 2) / 64 + cxw, wy = (VH / 2 - y) / 64 + cyw; const gx = wx + 2 * wy, gy = 2 * wy - wx; const a = ((Math.floor(gx * 2) + Math.floor(gy * 2)) & 1); const c = a ? [150, 146, 138] : [138, 134, 127]; g.rgba.set([...c, 255], (y * VW + x) * 4); } draws.push({ im: g, x: 0, y: 0, order: -200, lit: true, screen: true }); }
function add(name, frame = 0, opt = {}) {
  const it = M.items[name]; if (!it) return; const im = load(ART + '/' + it.file); const w = it.rect.w, h = it.rect.h; const s = it.sprites.length > 1 ? sub(im, frame, w, h) : im;
  const pivotX = 0, pivotY = 0; const [lx, ly] = it.local; // sol-alt köşe (pivotAt olanlarda pivot noktası)
  let bx = lx, by = ly; if (it.sprites.length === 1 && name.includes('Plant')) { const meta = fs.readFileSync(ART + '/' + it.file + '.meta', 'utf8'); const pm = meta.match(/pivot: \{x: ([-\d.e]+), y: ([-\d.e]+)\}/); bx = lx - (+pm[1]) * w / 64; by = ly - (+pm[2]) * h / 64; }
  draws.push({ im: s, wx: bx, wy: by, order: it.order, lit: !it.unlit, alpha: opt.alpha === undefined ? 1 : opt.alpha, sortY: opt.sortY });
}
// gövde (-62) ve cephe (-50)
{
  const body = load(ROOT + '/Assets/Art/Props/Street/Surroundings/PROP_StreetBG_Body_Home.png'); draws.push({ im: body, wx: -143 / 64, wy: 0, order: -62, lit: true });
  const facFile = ART + '/PROP_FE_Apt_Facade.png'; const fac = load(facFile); draws.push({ im: fac, wx: -9 / 64, wy: -1 / 64, order: -50, lit: true });
}
const ai = M.items.PROP_FE_Apt_Door.angles; let fi = 0; ai.forEach((a, i) => { if (Math.abs(a - angle) < Math.abs(ai[fi] - angle)) fi = i; });
add('PROP_FE_Apt_Interior', 0, { alpha: 1 });
if (!hall) draws[draws.length - 1].tint = [0.10, 0.12, 0.20];
add('PROP_FE_Apt_Door', fi); add('PROP_FE_Apt_DoorGlow', fi, { alpha: hall ? 1 : 0 });
add('PROP_FE_Apt_Recess');
if (fi === 0) { add('PROP_FE_Apt_Handle', handle); add('PROP_FE_Apt_Keypad', keypad); }
add('PROP_FE_Apt_Mailbox'); add('PROP_FE_Apt_MailLid', lid); if (args.tip) add('PROP_FE_Apt_EnvelopeTip');
add('PROP_FE_Apt_Lamp'); add('PROP_FE_Apt_LampBulb'); add('PROP_FE_Apt_Mat');
add('PROP_FE_Apt_Plant_A', 0, { sortY: wl(0.18, -0.30)[1] }); add('PROP_FE_Apt_Plant_B', 0, { sortY: wl(3.18, -0.30)[1] });
if (hall && angle > 0) add('FX_FE_Apt_SpillGlow', 0, { alpha: Math.min(1, open01 * 1.2) * 0.5 });
if (args.hl === 'mail') add('PROP_FE_Apt_MailHighlight'); if (args.hl === 'lock') add('PROP_FE_Apt_LockHighlight');
// Cenk
if (args.cenk) {
  const [cu, cd] = args.cenk.split(',').map(Number); const [wx, wy] = wl(cu, cd);
  const [f, k] = (args.cenkSprite || ROOT + '/Assets/Art/Characters/Cenk/NoBag/CHR_Cenk_Walk_NorthWest_NoBag.png#0').split('#');
  const im = load(f); const s = sub(im, +k || 0, 96, 96);
  draws.push({ im: s, wx: wx - 48 / 64, wy: wy - 19 / 64, order: +(args.cenkOrder || 0), lit: true, sortY: wy, alpha: args.cenkAlpha !== undefined ? +args.cenkAlpha : 1 });
}
draws.sort((a, b) => a.order - b.order || ((b.sortY !== undefined ? b.sortY : 0) - (a.sortY !== undefined ? a.sortY : 0)));
for (const d of draws) {
  let x0, y0; if (d.screen) { x0 = 0; y0 = 0; } else { const [sx, sy] = scr(d.wx, d.wy); x0 = Math.round(sx); y0 = Math.round(sy) - d.im.h; }
  for (let j = 0; j < d.im.h; j++) { const yy = y0 + j; if (yy < 0 || yy >= VH) continue; for (let i = 0; i < d.im.w; i++) { const xx = x0 + i; if (xx < 0 || xx >= VW) continue;
    const o = (j * d.im.w + i) * 4; const a = d.im.rgba[o + 3] / 255 * (d.alpha === undefined ? 1 : d.alpha); if (a <= 0) continue;
    let c = [d.im.rgba[o], d.im.rgba[o + 1], d.im.rgba[o + 2]]; if (d.tint) c = c.map((v, q) => v * d.tint[q]);
    if (d.lit) { const wx = (xx - VW / 2) / 64 + cxw, wy = (VH / 2 - yy) / 64 + cyw; const m = lightAt(wx, wy); c = c.map((v, q) => Math.min(255, v * m[q])); }
    const q = (yy * VW + xx) * 3; for (let k = 0; k < 3; k++) buf[q + k] = buf[q + k] * (1 - a) + c[k] * a; } }
}
const o = new Uint8Array(VW * SC * VH * SC * 4);
for (let y = 0; y < VH * SC; y++) for (let x = 0; x < VW * SC; x++) { const q = (Math.floor(y / SC) * VW + Math.floor(x / SC)) * 3; o.set([buf[q], buf[q + 1], buf[q + 2], 255], (y * VW * SC + x) * 4); }
fs.writeFileSync(out, C.encodePng(VW * SC, VH * SC, o));
console.log('ok', out);
