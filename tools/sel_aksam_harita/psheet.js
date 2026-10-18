// node psheet.js out.png a.png b.png ... (1920x1080 → 480x270 kareler, 3 sütun)
const C = require('./common.js'); const { fs, decodePng, encodePng } = C;
const [out, ...files] = process.argv.slice(2); const ims = files.map(f => decodePng(fs.readFileSync(f)));
const s = 4, w = 480, h = 270, cols = 3, rows = Math.ceil(ims.length / cols), W = w * cols, H = h * rows, o = new Uint8Array(W * H * 4);
ims.forEach((im, k) => { const ox = (k % cols) * w, oy = Math.floor(k / cols) * h; for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const si = ((y * s) * im.w + x * s) * 4; o.set(im.rgba.subarray(si, si + 4), ((oy + y) * W + ox + x) * 4); } });
fs.writeFileSync(out, encodePng(W, H, o));
