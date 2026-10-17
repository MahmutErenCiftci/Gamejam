// contact sheet: node sheet.js out.png cols a.png b.png ... (half size)
const C = require('./common.js'); const { fs, decodePng, encodePng } = C;
const [out, colsS, ...files] = process.argv.slice(2); const cols = +colsS;
const ims = files.map(f => decodePng(fs.readFileSync(f))); const w = Math.ceil(ims[0].w / 2), h = Math.ceil(ims[0].h / 2);
const rows = Math.ceil(ims.length / cols); const W = w * cols, H = h * rows; const o = new Uint8Array(W * H * 4);
ims.forEach((im, k) => { const ox = (k % cols) * w, oy = Math.floor(k / cols) * h;
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const s = ((y * 2) * im.w + x * 2) * 4; o.set(im.rgba.subarray(s, s + 4), ((oy + y) * W + ox + x) * 4); }
  for (let x = 0; x < w; x++) o.set([255, 255, 0, 255], ((oy) * W + ox + x) * 4); });
fs.writeFileSync(out, encodePng(W, H, o));
