// GIF -> yatay PNG sprite sheet (bağımlılıksız). Kullanım: node gif2sheet.js <girdi.gif> <çıktı.png> [tuval] [baseline]
const fs = require('fs'), zlib = require('zlib');

function decodeGif(b) {
  const W = b.readUInt16LE(6), H = b.readUInt16LE(8), f = b[10];
  let p = 13, gct = null;
  if (f & 0x80) { const n = 1 << ((f & 7) + 1); gct = b.subarray(p, p + 3 * n); p += 3 * n; }
  const canvas = new Uint8Array(W * H * 4); // başlangıç: tamamen şeffaf
  const frames = [];
  let gce = { disposal: 0, transIdx: -1, delay: 0 };
  while (p < b.length) {
    const c = b[p];
    if (c === 0x3B) break;
    if (c === 0x21) {
      if (b[p + 1] === 0xF9) {
        const pk = b[p + 3];
        gce = { disposal: (pk >> 2) & 7, transIdx: (pk & 1) ? b[p + 6] : -1, delay: b.readUInt16LE(p + 4) };
      }
      p += 2; while (b[p]) p += b[p] + 1; p++;
    } else if (c === 0x2C) {
      const fx = b.readUInt16LE(p + 1), fy = b.readUInt16LE(p + 3), fw = b.readUInt16LE(p + 5), fh = b.readUInt16LE(p + 7), lf = b[p + 9];
      p += 10;
      let ct = gct;
      if (lf & 0x80) { const n = 1 << ((lf & 7) + 1); ct = b.subarray(p, p + 3 * n); p += 3 * n; }
      const interlaced = (lf & 0x40) !== 0;
      const minCode = b[p++];
      const chunks = []; while (b[p]) { chunks.push(b.subarray(p + 1, p + 1 + b[p])); p += b[p] + 1; } p++;
      const idx = lzw(Buffer.concat(chunks), minCode, fw * fh);
      const before = gce.disposal === 3 ? canvas.slice() : null;
      const rows = interlaced ? interlaceRows(fh) : [...Array(fh).keys()];
      for (let i = 0; i < fh; i++) {
        const y = fy + rows[i];
        for (let x = 0; x < fw; x++) {
          const ci = idx[i * fw + x];
          if (ci === gce.transIdx || ci === undefined) continue;
          const X = fx + x; if (X >= W || y >= H) continue;
          const o = (y * W + X) * 4;
          canvas[o] = ct[ci * 3]; canvas[o + 1] = ct[ci * 3 + 1]; canvas[o + 2] = ct[ci * 3 + 2]; canvas[o + 3] = 255;
        }
      }
      frames.push({ rgba: canvas.slice(), delay: gce.delay * 10 });
      if (gce.disposal === 2) for (let y = fy; y < fy + fh && y < H; y++) for (let x = fx; x < fx + fw && x < W; x++) canvas.fill(0, (y * W + x) * 4, (y * W + x) * 4 + 4);
      else if (gce.disposal === 3) canvas.set(before);
      gce = { disposal: 0, transIdx: -1, delay: 0 };
    } else throw new Error('Bilinmeyen GIF bloğu ' + c);
  }
  return { W, H, frames };
}

function interlaceRows(h) {
  const order = []; for (const [s, st] of [[0, 8], [4, 8], [2, 4], [1, 2]]) for (let y = s; y < h; y += st) order.push(y);
  const rows = new Array(h); order.forEach((y, i) => rows[i] = y); return rows;
}

function lzw(data, minCode, count) {
  const clear = 1 << minCode, eoi = clear + 1, out = new Uint8Array(count);
  let size = minCode + 1, dict = [], next, op = 0, prev = null, bit = 0;
  const reset = () => { dict = []; for (let i = 0; i < clear; i++) dict[i] = [i]; dict[clear] = []; dict[eoi] = null; next = eoi + 1; size = minCode + 1; prev = null; };
  reset();
  while (bit + size <= data.length * 8 && op < count) {
    let code = 0; for (let i = 0; i < size; i++) code |= ((data[(bit + i) >> 3] >> ((bit + i) & 7)) & 1) << i;
    bit += size;
    if (code === clear) { reset(); continue; }
    if (code === eoi) break;
    let entry;
    if (code < next && dict[code]) entry = dict[code];
    else if (prev) entry = prev.concat(prev[0]);
    else break;
    for (const v of entry) if (op < count) out[op++] = v;
    if (prev && next < 4096) { dict[next++] = prev.concat(entry[0]); if (next === (1 << size) && size < 12) size++; }
    prev = entry;
  }
  return out;
}

const crcT = new Int32Array(256).map((_, n) => { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1; return c; });
const crc = buf => { let c = -1; for (const v of buf) c = crcT[(c ^ v) & 255] ^ (c >>> 8); return (c ^ -1) >>> 0; };
function chunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
  const td = Buffer.concat([Buffer.from(type), data]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td));
  return Buffer.concat([len, td, c]);
}
function encodePng(w, h, rgba) {
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) { raw[y * (w * 4 + 1)] = 0; Buffer.from(rgba.buffer, rgba.byteOffset + y * w * 4, w * 4).copy(raw, y * (w * 4 + 1) + 1); }
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}

// Tüm karelerdeki opak piksellerin sınırları (ayak hizası = maxY)
function bbox(g) {
  let minX = 1e9, minY = 1e9, maxX = -1, maxY = -1;
  for (const fr of g.frames) for (let y = 0; y < g.H; y++) for (let x = 0; x < g.W; x++) if (fr.rgba[(y * g.W + x) * 4 + 3]) { minX = Math.min(minX, x); maxX = Math.max(maxX, x); minY = Math.min(minY, y); maxY = Math.max(maxY, y); }
  return { minX, minY, maxX, maxY };
}

if (require.main === module) {
  const [, , inp, outp, cellArg, baseArg] = process.argv;
  const g = decodeGif(fs.readFileSync(inp));
  const bb = bbox(g);
  const cell = cellArg ? +cellArg : g.W;
  // Yatay: içerik ortası tuval ortasına. Dikey: ayak (maxY) verilen baseline'a, yoksa orijinal yerinde + ortalama.
  const dx = cell === g.W ? 0 : Math.round(cell / 2 - (bb.minX + bb.maxX + 1) / 2);
  const dy = baseArg !== undefined ? +baseArg - bb.maxY : Math.round((cell - g.H) / 2);
  const n = g.frames.length, W = cell * n, out = new Uint8Array(W * cell * 4);
  g.frames.forEach((fr, i) => {
    for (let y = 0; y < g.H; y++) for (let x = 0; x < g.W; x++) {
      const s = (y * g.W + x) * 4; if (!fr.rgba[s + 3]) continue;
      const X = x + dx, Y = y + dy; if (X < 0 || Y < 0 || X >= cell || Y >= cell) throw new Error('Kare tuvale sığmıyor: ' + inp);
      out.set(fr.rgba.subarray(s, s + 4), (Y * W + i * cell + X) * 4);
    }
  });
  fs.writeFileSync(outp, encodePng(W, cell, out));
  console.log(JSON.stringify({ in: inp.split(/[\\/]/).slice(-2).join('/'), src: `${g.W}x${g.H}`, frames: n, delays: [...new Set(g.frames.map(f => f.delay))], bbox: bb, out: `${W}x${cell}`, dx, dy }));
}
module.exports = { decodeGif, bbox };
