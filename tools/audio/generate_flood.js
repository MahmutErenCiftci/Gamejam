/* Akşam sel 120 sn sayacı sesleri (özgün, prosedürel). Sadece Node.js standart kütüphanesi.
 * Çalıştır: node tools/audio/generate_flood.js
 * Çıktı: Assets/Resources/Audio/Ambience/AMB_Flood_Water_Loop.wav (8 sn döngü: sokakta akan su, çağıltı + köpüren hışırtı),
 *        Assets/Resources/Audio/SFX/SFX_Flood_Warn.wav (%75 / %90 uyarısı: iki kısa yumuşak bip),
 *        Assets/Resources/Audio/SFX/SFX_Flood_Critical.wav (%100: alçalan uyarı tonu + su dalgası; sert siren değil).
 * generate_audio.js / generate_wind.js'e dokunmaz. .meta'ları Unity üretir.
 */
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const ROOT = path.resolve(__dirname, '../../Assets/Resources/Audio');
const SR = 44100, TAU = Math.PI * 2, PEAK = Math.floor(32767 * 10 ** (-1 / 20));
let seed = 52711;
function random() { seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5; return (seed >>> 0) / 4294967296; }
const noise = () => random() * 2 - 1;
const buf = seconds => new Float64Array(Math.round(seconds * SR));

// Akan su: iki bantlı gürültü (alçak gürleme + orta çağıltı) ve rastgele "kabarcık" tıklamaları (kısa sinüs çırpıntıları).
function water(seconds, rough = 1) {
  const a = buf(seconds); let l1 = 0, l2 = 0, m1 = 0, m2 = 0, hp = 0, prev = 0;
  const bubbles = [];
  for (let t = 0; t < seconds; t += 0.004 + random() * 0.03) bubbles.push([t, 300 + random() * 900, 0.01 + random() * 0.03, 0.15 + random() * 0.35]);
  for (let i = 0; i < a.length; i++) {
    const t = i / SR;
    const sw = 0.75 + 0.18 * Math.sin(TAU * 0.21 * t) + 0.09 * Math.sin(TAU * 0.57 * t + 0.7);
    const n = noise();
    l1 += 0.02 * (n - l1); l2 += 0.02 * (l1 - l2);                     // ~140 Hz gürleme
    const fc = 900 + 500 * Math.sin(TAU * 0.33 * t) ** 2, al = 1 - Math.exp(-TAU * fc / SR);
    m1 += al * (n - m1); m2 += al * (m1 - m2);
    hp = 0.97 * (hp + m2 - prev); prev = m2;                             // çağıltı bandı
    a[i] = (l2 * 2.2 + hp * 0.9 * rough) * sw;
  }
  for (const [t0, f, d, g] of bubbles) {
    const s = Math.round(t0 * SR), n = Math.round(d * SR);
    for (let k = 0; k < n && s + k < a.length; k++) {
      const u = k / n, fr = f * (1 + 0.8 * u);
      a[s + k] += Math.sin(TAU * fr * k / SR) * g * 0.12 * Math.sin(Math.PI * u) ** 2;
    }
  }
  return a;
}
function seamless(raw, seconds) {
  const n = Math.round(seconds * SR), c = raw.length - n, out = raw.slice(0, n);
  for (let i = 0; i < c; i++) { const k = .5 - .5 * Math.cos(Math.PI * i / (c - 1)); out[i] = raw[n + i] * (1 - k) + raw[i] * k; }
  return out;
}
function tone(a, t0, dur, f0, f1, gain, shape = 'sine') {
  const s = Math.round(t0 * SR), n = Math.round(dur * SR);
  let ph = 0;
  for (let k = 0; k < n && s + k < a.length; k++) {
    const u = k / n, f = f0 + (f1 - f0) * u;
    ph += TAU * f / SR;
    const env = Math.min(1, k / (0.008 * SR)) * Math.min(1, (n - k) / (0.06 * SR));
    const v = shape === 'tri' ? (2 / Math.PI) * Math.asin(Math.sin(ph)) : Math.sin(ph) * 0.8 + Math.sin(ph * 2) * 0.12;
    a[s + k] += v * gain * env;
  }
}
function encode(a, loop) {
  // Döngü: seamless() ek yerini zaten sürekli yapar; uçlarda sıfıra inen zarf her turda küçük bir "tık" bırakıyordu (QA 18 Eki).
  const fade = Math.round(SR * .02);
  const env = i => { if (loop) return 1; const k = Math.min(1, i / fade, (a.length - 1 - i) / fade); return .5 - .5 * Math.cos(Math.PI * k); };
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

const loop = seamless(water(9.2), 8);
fs.writeFileSync(path.join(ROOT, 'Ambience', 'AMB_Flood_Water_Loop.wav'), encode(loop, true));

const warn = buf(0.62);
tone(warn, 0.0, 0.16, 880, 860, 0.5, 'tri'); tone(warn, 0.24, 0.22, 660, 640, 0.5, 'tri');
fs.writeFileSync(path.join(ROOT, 'SFX', 'SFX_Flood_Warn.wav'), encode(warn, false));

const crit = buf(2.2);
tone(crit, 0.0, 0.5, 740, 520, 0.45, 'tri'); tone(crit, 0.55, 0.5, 740, 520, 0.4, 'tri'); tone(crit, 1.1, 0.9, 700, 330, 0.38, 'tri');
const wave = water(2.2, 1.6);
for (let i = 0; i < crit.length; i++) { const t = i / SR; crit[i] += wave[i] * 0.55 * Math.min(1, t / 0.9) * Math.min(1, (2.2 - t) / 0.6); }
fs.writeFileSync(path.join(ROOT, 'SFX', 'SFX_Flood_Critical.wav'), encode(crit, false));
console.log('AMB_Flood_Water_Loop.wav (8 sn), SFX_Flood_Warn.wav, SFX_Flood_Critical.wav yazıldı');
