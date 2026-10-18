// Akşam sel — rüzgâr koridoru görselleri. Referans sayfalardan (visuals/concept/ruzgar_ref_*.png) oyun ölçeğinde sprite üretir
// ve .meta'larıyla Assets/Art/FloodEvening/Wind/ altına yazar.
//   node build_art.js        → yaz
// Cenk kareleri: oyunun Cenk paletine (NoBag yürüyüş + pozlar) oturtulur, 96x96 kare, ayak pivotu yürüyüşle aynı (0.5, 0.1979).
// Arayüz: yerel piksel çözünürlüğü (referansta 1 piksel = 6 px), blok çoğunluğu + ızgara kayması otomatik.
const fs = require('fs'), path = require('path');
const s = require('./seg.js');
const C = require('../sel_aksam_harita/common.js');
const { decodePng, encodePng } = require('../sel_aksam_harita/lib/roomB_png.js');
const ROOT = path.resolve(__dirname, '../..').split(path.sep).join('/');
const REF = ROOT + '/visuals/concept/';
const DST = ROOT + '/Assets/Art/FloodEvening/Wind';

function write(sub, name, w, h, rgba, sprites) {
  const dir = DST + '/' + sub; fs.mkdirSync(dir, { recursive: true });
  const file = dir + '/' + name + '.png'; fs.writeFileSync(file, encodePng(w, h, rgba));
  fs.writeFileSync(file + '.meta', C.metaText(C.guidFor(file + '.meta'), name, w, h, sprites));
  return file;
}
const single = (name, w, h, px = 0.5, py = 0.5) => [{ name: name + '_0', x: 0, y: 0, w, h, px, py }];

// ------------------------------------------------------------------ Cenk kareleri
{
  const im = s.load(REF + 'ruzgar_ref_cenk.png'); const W = im.w, H = im.h; const bg = s.bgMask(im); const fg = bg.map(v => 1 - v);
  const { lab, comps } = s.comps(fg, W, H);
  const figs = comps.filter(c => c.n > 2500 && !((c.y1 - c.y0) < 50 && (c.x1 - c.x0) > 150));
  const smalls = comps.filter(c => c.n > 30 && c.n <= 2500);
  const rows = [[60, 280], [340, 565], [600, 815], [865, 1080]];
  const byRow = rows.map(([a, b]) => figs.filter(c => c.y0 >= a - 10 && c.y1 <= b).sort((p, q) => p.x0 - q.x0));
  const pal = new Map(); const CEN = ROOT + '/Assets/Art/Characters/Cenk/';
  for (const f of ['NoBag/CHR_Cenk_Walk_South_NoBag.png', 'NoBag/CHR_Cenk_Walk_East_NoBag.png', 'NoBag/CHR_Cenk_Walk_North_NoBag.png', 'Poses/CHR_Cenk_Hold_SouthEast.png', 'Poses/CHR_Cenk_Startled_SouthEast.png']) {
    const p = decodePng(fs.readFileSync(CEN + f)); for (let i = 0; i < p.w * p.h; i++) if (p.rgba[i * 4 + 3] > 200) { const k = (p.rgba[i * 4] << 16) | (p.rgba[i * 4 + 1] << 8) | p.rgba[i * 4 + 2]; pal.set(k, (pal.get(k) || 0) + 1); } }
  const palette = [...pal.entries()].filter(([, n]) => n >= 3).map(([k]) => [k >> 16 & 255, k >> 8 & 255, k & 255]);
  const nearest = c => { let best = null, bd = 1e9; for (const p of palette) { const dr = c[0] - p[0], dg = c[1] - p[1], db = c[2] - p[2]; const d = dr * dr * 0.3 + dg * dg * 0.59 + db * db * 0.11; if (d < bd) { bd = d; best = p; } } return best; };
  const SC = 59 / 200, step = 1 / SC;
  function frame(c) {
    const ids = new Set([c.id]); for (const m of smalls) if (m.x1 > c.x0 - 40 && m.x0 < c.x1 + 40 && m.y1 > c.y0 - 40 && m.y0 < c.y1 + 10) ids.add(m.id);
    const cell = new Uint8Array(96 * 96 * 4); const cx = (c.x0 + c.x1) / 2, y1 = c.y1;
    for (let ty = 0; ty < 96; ty++) for (let tx = 0; tx < 96; tx++) {
      const sx0 = cx + (tx - 48) * step, sy0 = y1 - (77 - ty) * step - step + 1; let n = 0, tot = 0; const buckets = new Map();
      for (let sy = Math.floor(sy0); sy < Math.floor(sy0 + step); sy++) for (let sx = Math.floor(sx0); sx < Math.floor(sx0 + step); sx++) {
        tot++; if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue; const i = sy * W + sx; if (!fg[i] || !ids.has(lab[i])) continue; n++;
        const r = im.rgba[i * 4], g = im.rgba[i * 4 + 1], b = im.rgba[i * 4 + 2]; const k = (r >> 4) << 8 | (g >> 4) << 4 | (b >> 4); const e = buckets.get(k) || [0, 0, 0, 0]; e[0] += r; e[1] += g; e[2] += b; e[3]++; buckets.set(k, e); }
      if (!tot || n / tot < 0.5) continue; let best = null; for (const e of buckets.values()) if (!best || e[3] > best[3]) best = e;
      cell.set([...nearest([best[0] / best[3], best[1] / best[3], best[2] / best[3]]), 255], (ty * 96 + tx) * 4);
    }
    return cell;
  }
  const mirror = cell => { const o = new Uint8Array(cell.length); for (let y = 0; y < 96; y++) for (let x = 0; x < 96; x++) o.set(cell.subarray((y * 96 + x) * 4, (y * 96 + x) * 4 + 4), (y * 96 + 95 - x) * 4); return o; };
  // L = referanstaki gibi (rüzgâr soldan gelir, Cenk sola eğilir); R = aynası
  ['Brace', 'Stumble', 'Recover', 'React'].forEach((nm, ri) => {
    const cells = byRow[ri].map(frame);
    for (const side of ['L', 'R']) {
      const cs = side === 'L' ? cells : cells.map(mirror); const w = 96 * cs.length, out = new Uint8Array(w * 96 * 4);
      cs.forEach((cell, k) => { for (let y = 0; y < 96; y++) out.set(cell.subarray(y * 96 * 4, (y + 1) * 96 * 4), (y * w + k * 96) * 4); });
      const name = 'CHR_Cenk_Wind_' + nm + '_' + side;
      write('Cenk', name, w, 96, out, cs.map((_, k) => ({ name: name + '_' + k, x: k * 96, y: 0, w: 96, h: 96, px: 0.5, py: 0.19791667 })));
    }
    console.log('Cenk', nm, cells.length, 'kare');
  });
}

// ------------------------------------------------------------------ arayüz
{
  const im = s.load(REF + 'ruzgar_ref_ui.png'); const W = im.w; const bg = s.bgMask(im, 215, 30); const F = 6;
  const CROPS = {
    UI_FE_Wind_Banner: [50, 100, 990, 385], UI_FE_Wind_ArrowR: [1035, 110, 1395, 310], UI_FE_Wind_ArrowL: [1035, 318, 1395, 515],
    UI_FE_Key_A: [195, 400, 480, 675], UI_FE_Key_D: [542, 400, 828, 675],
    UI_FE_Wind_ChevR: [1050, 540, 1370, 642], UI_FE_Wind_ChevL: [1050, 673, 1370, 775],
    UI_FE_Wind_Swirl1: [78, 705, 262, 835], UI_FE_Wind_Swirl2: [312, 705, 505, 830], UI_FE_Wind_Swirl3: [545, 705, 770, 815], UI_FE_Wind_Swirl4: [800, 705, 985, 808],
    UI_FE_Wind_HintA: [55, 845, 578, 1030], UI_FE_Wind_HintD: [602, 845, 1065, 1030], UI_FE_Wind_Alert: [1122, 846, 1293, 1006],
  };
  const px = (x, y) => { const i = y * W + x; return bg[i] ? null : [im.rgba[i * 4], im.rgba[i * 4 + 1], im.rgba[i * 4 + 2]]; };
  function build([x0, y0, x1, y1], ox, oy) {
    const w = Math.floor((x1 - x0 - ox) / F), h = Math.floor((y1 - y0 - oy) / F); const out = new Uint8Array(w * h * 4); let err = 0;
    for (let ty = 0; ty < h; ty++) for (let tx = 0; tx < w; tx++) { const bx = x0 + ox + tx * F, by = y0 + oy + ty * F; const bk = new Map(); let n = 0;
      for (let y = by; y < by + F; y++) for (let x = bx; x < bx + F; x++) { const c = px(x, y); if (!c) continue; n++; const k = (c[0] >> 5) << 6 | (c[1] >> 5) << 3 | (c[2] >> 5); const e = bk.get(k) || [0, 0, 0, 0]; e[0] += c[0]; e[1] += c[1]; e[2] += c[2]; e[3]++; bk.set(k, e); }
      if (n < F * F * 0.5) { err += n; continue; } let best = null; for (const e of bk.values()) if (!best || e[3] > best[3]) best = e; err += n - best[3];
      out.set([best[0] / best[3] | 0, best[1] / best[3] | 0, best[2] / best[3] | 0, 255], (ty * w + tx) * 4); }
    return { w, h, out, err };
  }
  for (const [name, r] of Object.entries(CROPS)) {
    let best = null; for (let oy = 0; oy < F; oy++) for (let ox = 0; ox < F; ox++) { const b = build(r, ox, oy); if (!best || b.err < best.err) best = b; }
    let a = 1e9, b2 = -1, c = 1e9, d = -1; for (let y = 0; y < best.h; y++) for (let x = 0; x < best.w; x++) if (best.out[(y * best.w + x) * 4 + 3]) { a = Math.min(a, x); b2 = Math.max(b2, x); c = Math.min(c, y); d = Math.max(d, y); }
    const w = b2 - a + 1, h = d - c + 1, o = new Uint8Array(w * h * 4); for (let y = 0; y < h; y++) o.set(best.out.subarray(((y + c) * best.w + a) * 4, ((y + c) * best.w + a + w) * 4), y * w * 4);
    if (name === 'UI_FE_Wind_Banner') {
      // alt satır (KUVVETLİ RÜZGÂR) referansta bozuk çizilmiş: zemin rengiyle silinir, oyunda yazıyla basılır
      const bgc = o.slice((40 * w + 70) * 4, (40 * w + 70) * 4 + 4);
      for (let y = 26; y <= 37; y++) for (let x = 40; x <= 132; x++) o.set(bgc, (y * w + x) * 4);
    }
    write('UI', name, w, h, o, single(name, w, h)); console.log('UI', name, w + 'x' + h);
  }
}

// ------------------------------------------------------------------ efekt: yaprak (4 tür) + rüzgâr çizgisi
{
  const LEAF = [['#E07A2E', '#B4521C'], ['#F2A63A', '#C27A1E'], ['#C9472A', '#8E2F1C'], ['#D9B23C', '#A47F22']];
  const shapes = [['.aa.', 'abba', '.aa.'], ['..a', '.ab', 'ab.', 'a..'], ['aab', 'abb'], ['.a.', 'aba', 'ab.']];
  shapes.forEach((sh, k) => { const h = sh.length, w = sh[0].length, o = new Uint8Array(w * h * 4); const [ca, cb] = LEAF[k].map(t => [parseInt(t.slice(1, 3), 16), parseInt(t.slice(3, 5), 16), parseInt(t.slice(5, 7), 16)]);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const ch = sh[y][x]; if (ch === '.') continue; o.set([...(ch === 'a' ? ca : cb), 255], (y * w + x) * 4); }
    write('FX', 'FX_FE_Wind_Leaf' + (k + 1), w, h, o, single('FX_FE_Wind_Leaf' + (k + 1), w, h)); });
  { const w = 28, h = 2, o = new Uint8Array(w * h * 4); for (let x = 0; x < w; x++) { const t = x / (w - 1); const a = Math.round(255 * Math.sin(Math.PI * t) * 0.8); o.set([226, 236, 255, a], x * 4); if (x > 6 && x < 22) o.set([200, 214, 240, Math.round(a * 0.6)], (w + x) * 4); }
    write('FX', 'FX_FE_Wind_Streak', w, h, o, single('FX_FE_Wind_Streak', w, h)); }
  console.log('FX ok');
}
