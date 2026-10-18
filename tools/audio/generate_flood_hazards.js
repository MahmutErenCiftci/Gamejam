/* Akşam sel çevre tehlikeleri sesleri (özgün, prosedürel; FLOOD-MECH 18 Eki). Sadece Node.js standart kütüphanesi.
 * Çalıştır: node tools/audio/generate_flood_hazards.js
 * Çıktı: Assets/Resources/Audio/SFX/SFX_FE_Thunder_1..3.wav (uzak gök gürültüsü: yumuşak başlayan, yuvarlanan alçak gürleme; sert çatırtı yok),
 *        Assets/Resources/Audio/SFX/SFX_FE_Shock.wav (kısa elektrik çarpması: vızıltı + çıtırtı, 0,5 sn),
 *        Assets/Resources/Audio/SFX/SFX_FE_Debris_Bump.wav (suda yüzen tahtanın çarpması: boğuk tok ses + şıpırtı),
 *        Assets/Resources/Audio/Ambience/AMB_FE_Electric_Crackle_Loop.wav (4 sn döngü: sarkan kablonun çıtırtısı + hafif uğultu).
 * .meta'ları Unity üretir.
 */
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const ROOT = path.resolve(__dirname, '../../Assets/Resources/Audio');
const SR = 44100, TAU = Math.PI * 2, PEAK = Math.floor(32767 * 10 ** (-1 / 20));
let seed = 90817;
function random() { seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5; return (seed >>> 0) / 4294967296; }
const noise = () => random() * 2 - 1;
const buf = seconds => new Float64Array(Math.round(seconds * SR));

function encode(a, loop, gain = 1) {
  const fade = Math.round(SR * .02);
  const env = i => { if (loop) return 1; const k = Math.min(1, i / fade, (a.length - 1 - i) / fade); return .5 - .5 * Math.cos(Math.PI * k); };
  let mean = 0; for (let i = 0; i < a.length; i++) mean += a[i]; mean /= a.length;
  let peak = 0; for (let i = 0; i < a.length; i++) { a[i] = (a[i] - mean) * env(i); peak = Math.max(peak, Math.abs(a[i])); }
  const wav = Buffer.alloc(44 + a.length * 2);
  wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(SR, 24); wav.writeUInt32LE(SR * 2, 28); wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
  wav.write('data', 36); wav.writeUInt32LE(a.length * 2, 40);
  for (let i = 0; i < a.length; i++) wav.writeInt16LE(Math.round(a[i] / peak * PEAK * gain), 44 + i * 2);
  return wav;
}
function seamless(raw, seconds) {
  const n = Math.round(seconds * SR), c = raw.length - n, out = raw.slice(0, n);
  for (let i = 0; i < c; i++) { const k = .5 - .5 * Math.cos(Math.PI * i / (c - 1)); out[i] = raw[n + i] * (1 - k) + raw[i] * k; }
  return out;
}

// Uzak gök gürültüsü: 2 kutuplu alçak geçiren gürültü (~60–180 Hz), birkaç yuvarlanan tepe, yumuşak yükseliş, uzun sönüş.
function thunder(seconds, rolls) {
  const a = buf(seconds); let l1 = 0, l2 = 0, l3 = 0, m1 = 0, m2 = 0;
  for (let i = 0; i < a.length; i++) {
    const t = i / SR, n = noise();
    l1 += 0.012 * (n - l1); l2 += 0.012 * (l1 - l2); l3 += 0.02 * (l2 - l3);          // gürleme
    m1 += 0.05 * (n - m1); m2 += 0.05 * (m1 - m2);                                    // biraz daha parlak doku (yalnız tepelerde)
    let env = 0, bright = 0;
    for (const [t0, g, w] of rolls) { const x = t - t0; if (x < 0) continue; const e = (1 - Math.exp(-x / 0.12)) * Math.exp(-x / w); env += g * e; bright += g * e * Math.exp(-x / 0.25); }
    a[i] = l3 * 9 * env + m2 * 1.2 * bright;
  }
  return a;
}
const variants = [
  [5.0, [[0.05, 1, 1.4], [0.6, 0.7, 1.1], [1.5, 0.55, 1.4]]],
  [4.6, [[0.08, 0.8, 0.9], [0.35, 1, 1.5], [1.9, 0.4, 1.0]]],
  [5.4, [[0.1, 0.9, 1.6], [1.1, 0.8, 1.3], [2.3, 0.5, 1.5]]],
];
variants.forEach(([s, r], k) => fs.writeFileSync(path.join(ROOT, 'SFX', 'SFX_FE_Thunder_' + (k + 1) + '.wav'), encode(thunder(s, r), false, 0.9)));

// Elektrik çarpması: 0,5 sn — 110 Hz testere vızıltısı (genlik titreşimli) + yüksek frekans çıtırtılar, hızlı sönüş.
{
  const a = buf(0.55); let ph = 0, hp = 0, prev = 0;
  for (let i = 0; i < a.length; i++) {
    const t = i / SR; ph += 110 * (1 + 0.04 * Math.sin(TAU * 37 * t)) / SR; const saw = 2 * (ph % 1) - 1;
    const n = noise(); hp = 0.85 * (hp + n - prev); prev = n;
    const crack = random() < 0.012 ? 1 : 0;
    const env = Math.min(1, t / 0.008) * Math.exp(-t / 0.18);
    a[i] = (saw * 0.45 * (0.6 + 0.4 * Math.sin(TAU * 23 * t)) + hp * 0.35 + crack * noise() * 0.9) * env;
  }
  fs.writeFileSync(path.join(ROOT, 'SFX', 'SFX_FE_Shock.wav'), encode(a, false, 0.85));
}
// Yüzen tahta çarpması: boğuk tok ses (150→85 Hz) + küçük şıpırtı.
{
  const a = buf(0.45); let ph = 0, lp = 0;
  for (let i = 0; i < a.length; i++) {
    const t = i / SR; const f = 85 + 65 * Math.exp(-t / 0.05); ph += TAU * f / SR;
    const n = noise(); lp += 0.18 * (n - lp);
    a[i] = Math.sin(ph) * Math.exp(-t / 0.09) * 0.9 + Math.sin(ph * 2.7) * Math.exp(-t / 0.03) * 0.25 + lp * Math.exp(-((t - 0.05) ** 2) / 0.004) * 0.5;
  }
  fs.writeFileSync(path.join(ROOT, 'SFX', 'SFX_FE_Debris_Bump.wav'), encode(a, false, 0.8));
}
// Çıtırtı döngüsü: seyrek, kümeler halinde kısa çatırtılar + 100 Hz hafif uğultu.
{
  const raw = buf(4.6); let hp = 0, prev = 0;
  const bursts = []; for (let t = 0; t < 4.6; t += 0.05 + random() * 0.35) { const n = 1 + Math.floor(random() * 5); for (let k = 0; k < n; k++) bursts.push([t + k * (0.008 + random() * 0.02), 0.3 + random() * 0.7]); }
  for (let i = 0; i < raw.length; i++) { const t = i / SR; raw[i] = Math.sin(TAU * 100 * t) * 0.05 + Math.sin(TAU * 200 * t) * 0.02; }
  for (const [t0, g] of bursts) {
    const s = Math.round(t0 * SR), n = Math.round((0.003 + random() * 0.006) * SR);
    for (let k = 0; k < n && s + k < raw.length; k++) { const x = noise(); hp = 0.6 * (hp + x - prev); prev = x; raw[s + k] += hp * g * (1 - k / n); }
  }
  fs.writeFileSync(path.join(ROOT, 'Ambience', 'AMB_FE_Electric_Crackle_Loop.wav'), encode(seamless(raw, 4), true, 0.8));
}
console.log('SFX_FE_Thunder_1..3, SFX_FE_Shock, SFX_FE_Debris_Bump, AMB_FE_Electric_Crackle_Loop yazıldı');
