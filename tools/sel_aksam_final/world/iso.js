// Apartman finali — küçük izometrik çizim motoru (2:1, PPU 64). Dünya görselleri bununla çizilir ki cephe, kapı kanadı,
// hol, posta kutusu ve saksılar aynı izdüşümde birbirine otursun.
// Koordinat: u = +gx (hücre), d = +gy (hücre, duvarın içine doğru), h = piksel (yukarı). Tuval y-aşağı.
//   X = ox + 32(u - d),  Y = oy - 16(u + d) - h     (ox, oy: (u=0, d=0, h=0) noktasının tuvaldeki sürekli konumu)
// Derinlik: s = u + d (küçük = kameraya yakın). Dörtgenler piksel merkezinden ters eşlemeyle (afin) taranır → keskin kenarlar.
'use strict';
const path = require('path');
const C = require('../../sel_aksam_harita/common.js');

const rgb = h => typeof h === 'string' ? [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)] : h;
const clamp = (v, a = 0, b = 255) => Math.max(a, Math.min(b, v));
const shade = (c, k) => rgb(c).map(v => clamp(Math.round(v * k)));
const lerpC = (a, b, t) => { a = rgb(a); b = rgb(b); return a.map((v, i) => clamp(Math.round(v + (b[i] - v) * t))); };
function hash(a, b, c = 0) { let h = (Math.floor(a) * 374761393 + Math.floor(b) * 668265263 + Math.floor(c) * 1274126177) | 0; h = (h ^ (h >>> 13)) * 1274126177 | 0; h ^= h >>> 16; return ((h >>> 0) % 100000) / 100000; }

class Layer {
  constructor(w, h, ox, oy) { this.w = w; this.h = h; this.ox = ox; this.oy = oy; this.d = new Uint8Array(w * h * 4); this.z = new Float32Array(w * h).fill(1e9); }
  proj(u, d, h) { return [this.ox + 32 * (u - d), this.oy - 16 * (u + d) - h]; }
  put(x, y, c, a = 255, z = -1e9) {
    if (x < 0 || y < 0 || x >= this.w || y >= this.h || !c) return; const i = y * this.w + x; if (z > this.z[i]) return; this.z[i] = z;
    const o = i * 4; const v = rgb(c); this.d[o] = v[0]; this.d[o + 1] = v[1]; this.d[o + 2] = v[2]; this.d[o + 3] = c.length > 3 ? c[3] : a;
  }
  get(x, y) { if (x < 0 || y < 0 || x >= this.w || y >= this.h) return null; const o = (y * this.w + x) * 4; return this.d[o + 3] ? [this.d[o], this.d[o + 1], this.d[o + 2], this.d[o + 3]] : null; }
  alpha(x, y) { if (x < 0 || y < 0 || x >= this.w || y >= this.h) return 0; return this.d[(y * this.w + x) * 4 + 3]; }
  clear(x, y) { if (x < 0 || y < 0 || x >= this.w || y >= this.h) return; const i = y * this.w + x; this.d.fill(0, i * 4, i * 4 + 4); this.z[i] = 1e9; }
  // A: köşe (u,d,h), U ve V kenar vektörleri. fn(s, t, p) → renk [r,g,b(,a)] ya da null. s,t ∈ [0,1).
  quad(A, U, V, fn, opt = {}) {
    const [x0, y0] = this.proj(A[0], A[1], A[2]);
    const ux = 32 * (U[0] - U[1]), uy = -16 * (U[0] + U[1]) - U[2];
    const vx = 32 * (V[0] - V[1]), vy = -16 * (V[0] + V[1]) - V[2];
    const det = ux * vy - uy * vx; if (Math.abs(det) < 1e-6) return;
    const xs = [x0, x0 + ux, x0 + vx, x0 + ux + vx], ys = [y0, y0 + uy, y0 + vy, y0 + uy + vy];
    const bx0 = Math.max(0, Math.floor(Math.min(...xs)) - 1), bx1 = Math.min(this.w - 1, Math.ceil(Math.max(...xs)) + 1);
    const by0 = Math.max(0, Math.floor(Math.min(...ys)) - 1), by1 = Math.min(this.h - 1, Math.ceil(Math.max(...ys)) + 1);
    const bias = opt.bias || 0;
    for (let y = by0; y <= by1; y++) for (let x = bx0; x <= bx1; x++) {
      const px = x + 0.5 - x0, py = y + 0.5 - y0;
      const s = (px * vy - py * vx) / det, t = (ux * py - uy * px) / det;
      if (s < 0 || s >= 1 || t < 0 || t >= 1) continue;
      const p = [A[0] + s * U[0] + t * V[0], A[1] + s * U[1] + t * V[1], A[2] + s * U[2] + t * V[2]];
      const c = fn(s, t, p); if (!c) continue;
      this.put(x, y, c, 255, p[0] + p[1] + bias);
    }
  }
  // Eksen hizalı kutu [u0,u1]×[d0,d1]×[h0,h1]: görünen üç yüz (üst, ön -gy, sol -gx). faces: {top, front, left} → fn ya da renk
  box(u0, u1, d0, d1, h0, h1, faces, opt = {}) {
    const f = k => typeof faces[k] === 'function' ? faces[k] : (faces[k] ? () => faces[k] : null);
    if (f('front')) this.quad([u0, d0, h0], [u1 - u0, 0, 0], [0, 0, h1 - h0], (s, t, p) => f('front')(s, 1 - t, p), opt);
    if (f('left')) this.quad([u0, d0, h0], [0, d1 - d0, 0], [0, 0, h1 - h0], (s, t, p) => f('left')(s, 1 - t, p), opt);
    if (f('top')) this.quad([u0, d0, h1], [u1 - u0, 0, 0], [0, d1 - d0, 0], f('top'), opt);
  }
  // Saydam olmayan piksellerin saydam komşularına çizgi (yalnız bu katmanda)
  outline(col = '#14141F', onlyWhere = null) {
    const add = [];
    for (let y = 0; y < this.h; y++) for (let x = 0; x < this.w; x++) {
      if (this.alpha(x, y)) continue; if (onlyWhere && !onlyWhere(x, y)) continue;
      if (this.alpha(x - 1, y) > 128 || this.alpha(x + 1, y) > 128 || this.alpha(x, y - 1) > 128 || this.alpha(x, y + 1) > 128) add.push([x, y]);
    }
    for (const [x, y] of add) { const o = (y * this.w + x) * 4; const v = rgb(col); this.d.set([v[0], v[1], v[2], 255], o); }
    return this;
  }
  blit(src, dx = 0, dy = 0) { for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) { const c = src.get(x, y); if (c) { const o = ((y + dy) * this.w + x + dx) * 4; if (x + dx < 0 || y + dy < 0 || x + dx >= this.w || y + dy >= this.h) continue; const a = c[3] / 255; const b = this.d.subarray(o, o + 4); for (let k = 0; k < 3; k++) b[k] = Math.round(b[k] * (1 - a) + c[k] * a); b[3] = Math.max(b[3], c[3]); } } }
  bbox() { let x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1; for (let y = 0; y < this.h; y++) for (let x = 0; x < this.w; x++) if (this.alpha(x, y)) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; } return x1 < 0 ? null : { x: x0, y: y0, w: x1 - x0 + 1, h: y1 - y0 + 1 }; }
  crop(r) { const o = new Uint8Array(r.w * r.h * 4); for (let y = 0; y < r.h; y++) o.set(this.d.subarray(((r.y + y) * this.w + r.x) * 4, ((r.y + y) * this.w + r.x + r.w) * 4), y * r.w * 4); return o; }
  static fromPng(file, ox, oy) { const im = C.decodePng(C.fs.readFileSync(file)); const L = new Layer(im.w, im.h, ox, oy); L.d.set(im.rgba); return L; }
}

module.exports = { Layer, rgb, clamp, shade, lerpC, hash, C };
