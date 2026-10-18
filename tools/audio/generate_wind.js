/* Akşam sel rüzgâr koridoru sesleri (özgün, prosedürel). Sadece Node.js standart kütüphanesi.
 * Çalıştır: node tools/audio/generate_wind.js
 * Çıktı: Assets/Resources/Audio/Ambience/AMB_Wind_Gust_Loop.wav (10 sn döngü), Assets/Resources/Audio/SFX/SFX_Wind_Gust.wav (1,4 sn esinti)
 * generate_audio.js'e dokunmaz (onun sabit tohumlu çıktıları değişmesin diye ayrı dosya). .meta'ları Unity üretir.
 */
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const ROOT = path.resolve(__dirname, '../../Assets/Resources/Audio');
const SR = 44100, TAU = Math.PI * 2, PEAK = Math.floor(32767 * 10 ** (-1 / 20));
let seed = 91357;
function random() { seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5; return (seed >>> 0) / 4294967296; }
const noise = () => random() * 2 - 1;
const buf = seconds => new Float64Array(Math.round(seconds * SR));

// Rüzgâr: iki kutuplu bant geçiren gürültü; merkez frekansı ve genlik yavaşça dalgalanır (uğultu + ıslık).
function wind(seconds, gustAt, base = 0.35) {
  const a = buf(seconds); let lp1 = 0, lp2 = 0, hp = 0, prev = 0;
  for (let i = 0; i < a.length; i++) {
    const t = i / SR;
    let g = base + 0.18 * Math.sin(TAU * 0.13 * t) + 0.1 * Math.sin(TAU * 0.31 * t + 1.3);
    for (const [c, w, h] of gustAt) { const d = (t - c) / w; g += h * Math.exp(-d * d); }
    const fc = 260 + 900 * Math.max(0, Math.min(1, g)), al = 1 - Math.exp(-TAU * fc / SR);
    lp1 += al * (noise() - lp1); lp2 += al * (lp1 - lp2);
    hp = 0.995 * (hp + lp2 - prev); prev = lp2;
    a[i] = hp * Math.max(0.05, g);
  }
  return a;
}
function seamless(raw, seconds) {
  const n = Math.round(seconds * SR), c = raw.length - n, out = raw.slice(0, n);
  for (let i = 0; i < c; i++) { const k = .5 - .5 * Math.cos(Math.PI * i / (c - 1)); out[i] = raw[n + i] * (1 - k) + raw[i] * k; }
  return out;
}
function encode(a, loop) {
  const fade = Math.round(SR * (loop ? .001 : .02));
  const env = i => { const k = Math.min(1, i / fade, (a.length - 1 - i) / fade); return .5 - .5 * Math.cos(Math.PI * k); };
  let mean = 0; for (let i = 0; i < a.length; i++) mean += a[i]; mean /= a.length;
  let peak = 0; for (let i = 0; i < a.length; i++) { a[i] = (a[i] - mean) * env(i); peak = Math.max(peak, Math.abs(a[i])); }
  const wav = Buffer.alloc(44 + a.length * 2);
  wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(SR, 24); wav.writeUInt32LE(SR * 2, 28); wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
  wav.write('data', 36); wav.writeUInt32LE(a.length * 2, 40);
  for (let i = 0; i < a.length; i++) wav.writeInt16LE(Math.round(a[i] / peak * PEAK), 44 + i * 2);
  return wav;
}
const loop = seamless(wind(11.2, [[2.2, .7, .35], [6.1, .9, .45], [9.0, .6, .3]]), 10);
fs.writeFileSync(path.join(ROOT, 'Ambience', 'AMB_Wind_Gust_Loop.wav'), encode(loop, true));
const gust = wind(1.4, [[0.55, .28, .9]], 0.05);
for (let i = 0; i < gust.length; i++) { const t = i / SR; gust[i] *= Math.min(1, t / 0.15) * Math.min(1, (1.4 - t) / 0.5); }
fs.writeFileSync(path.join(ROOT, 'SFX', 'SFX_Wind_Gust.wav'), encode(gust, false));
console.log('AMB_Wind_Gust_Loop.wav (10 sn), SFX_Wind_Gust.wav (1,4 sn) yazıldı');
