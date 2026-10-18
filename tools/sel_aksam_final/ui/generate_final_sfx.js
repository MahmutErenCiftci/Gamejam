/* Apartman finali yakın plan sesleri (özgün, prosedürel). Sadece Node.js standart kütüphanesi.
 * Çalıştır: node tools/sel_aksam_final/ui/generate_final_sfx.js
 * Çıktı (Assets/Resources/Audio/SFX/, GameAudio.PlaySfx("SFX_Final_...") ile çalınır; .meta'ları Unity üretir):
 *   SFX_Final_Key        tuş takırtısı + kısa bip            (~0,11 sn)
 *   SFX_Final_PinOk      iki yükselen bip + sürgü motoru      (~0,65 sn)
 *   SFX_Final_PinWrong   iki kısa alçak vızıltı               (~0,42 sn)
 *   SFX_Final_HandleTick kolun minik metal tıkı               (~0,05 sn)
 *   SFX_Final_Latch      mandalın çekilmesi: tok vuruş + klik (~0,45 sn)
 *   SFX_Final_Paper      kâğıt hışırtısı (zarf/mektup)        (~0,45 sn)
 * Sabit tohum: her çalıştırmada aynı dosyalar.
 */
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const OUT = path.resolve(__dirname, '../../../Assets/Resources/Audio/SFX');
const SR = 44100, TAU = Math.PI * 2;
let seed = 47291;
function random() { seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5; return (seed >>> 0) / 4294967296; }
const noise = () => random() * 2 - 1;
const buf = s => new Float64Array(Math.round(s * SR));
const env = (t, a, d) => (t < 0 ? 0 : t < a ? t / a : Math.exp(-(t - a) / d));

/** Tek kutuplu alçak geçiren (yerinde). */
function lowpass(a, fc) { const k = 1 - Math.exp(-TAU * fc / SR); let y = 0; for (let i = 0; i < a.length; i++) { y += k * (a[i] - y); a[i] = y; } return a; }
/** Basit yüksek geçiren (yerinde). */
function highpass(a, fc) { const k = Math.exp(-TAU * fc / SR); let y = 0, px = 0; for (let i = 0; i < a.length; i++) { y = k * (y + a[i] - px); px = a[i]; a[i] = y; } return a; }
/** İki kutuplu rezonanslı bant geçiren (durum değişkenli filtre). */
function bandpass(a, fc, q = 4) { let lp = 0, bp = 0; const f = 2 * Math.sin(Math.PI * Math.min(fc, SR / 6) / SR), d = 1 / q; for (let i = 0; i < a.length; i++) { const hp = a[i] - lp - d * bp; bp += f * hp; lp += f * bp; a[i] = bp; } return a; }
function mixInto(dst, src, at = 0, gain = 1) { const o = Math.round(at * SR); for (let i = 0; i < src.length && o + i < dst.length; i++) dst[o + i] += src[i] * gain; return dst; }
function beep(seconds, hz, a = 0.003, d = 0.05, square = 0.25) {
  const b = buf(seconds); let ph = 0;
  for (let i = 0; i < b.length; i++) { const t = i / SR; ph += TAU * hz / SR; const s = Math.sin(ph); const sq = Math.sign(s) * Math.min(1, Math.abs(s) * 3); b[i] = ((1 - square) * s + square * sq) * env(t, a, d); }
  return b;
}
function click(seconds, fc, d = 0.004, q = 3) { const b = buf(seconds); for (let i = 0; i < b.length; i++) b[i] = noise() * env(i / SR, 0.0004, d); return bandpass(b, fc, q); }

function encode(a, peakDb) {
  const fade = Math.round(SR * 0.004);
  const w = i => { const k = Math.min(1, i / fade, (a.length - 1 - i) / fade); return 0.5 - 0.5 * Math.cos(Math.PI * Math.max(0, k)); };
  let mean = 0; for (let i = 0; i < a.length; i++) mean += a[i]; mean /= a.length;
  let peak = 1e-9; for (let i = 0; i < a.length; i++) { a[i] = (a[i] - mean) * w(i); peak = Math.max(peak, Math.abs(a[i])); }
  const target = 32767 * 10 ** (peakDb / 20);
  const wav = Buffer.alloc(44 + a.length * 2);
  wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(SR, 24); wav.writeUInt32LE(SR * 2, 28); wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
  wav.write('data', 36); wav.writeUInt32LE(a.length * 2, 40);
  for (let i = 0; i < a.length; i++) wav.writeInt16LE(Math.max(-32768, Math.min(32767, Math.round(a[i] / peak * target))), 44 + i * 2);
  return wav;
}
const out = [];
function save(name, a, peakDb) { fs.writeFileSync(path.join(OUT, name + '.wav'), encode(a, peakDb)); out.push(`${name}.wav (${(a.length / SR).toFixed(2)} sn)`); }

// --- tuş: plastik takırtı + 2,35 kHz bip
{ const a = buf(0.11); mixInto(a, click(0.02, 3200, 0.003, 2), 0, 0.9); mixInto(a, click(0.02, 900, 0.006, 2), 0.002, 0.5); mixInto(a, beep(0.09, 2350, 0.002, 0.03, 0.35), 0.006, 0.55); save('SFX_Final_Key', a, -2); }
// --- doğru kod: iki yükselen bip + kısa motor vınlaması + sürgü kliki
{ const a = buf(0.66); mixInto(a, beep(0.11, 1760, 0.003, 0.06, 0.3), 0, 0.6); mixInto(a, beep(0.2, 2640, 0.003, 0.09, 0.3), 0.12, 0.65);
  const m = buf(0.24); let ph = 0; for (let i = 0; i < m.length; i++) { const t = i / SR; const f = 95 + 40 * (t / 0.24); ph += TAU * f / SR; const saw = (ph / TAU) % 1 * 2 - 1; m[i] = saw * Math.min(1, t / 0.03) * Math.min(1, (0.24 - t) / 0.05) * (0.7 + 0.3 * Math.sin(TAU * 31 * t)); }
  lowpass(m, 900); mixInto(a, m, 0.33, 0.55); mixInto(a, click(0.04, 1800, 0.008, 3), 0.57, 0.8); save('SFX_Final_PinOk', a, -1); }
// --- yanlış kod: iki alçak, yumuşak kırpılmış vızıltı
{ const a = buf(0.42); for (const at of [0, 0.2]) { const b = buf(0.16); let p1 = 0, p2 = 0; for (let i = 0; i < b.length; i++) { const t = i / SR; p1 += TAU * 185 / SR; p2 += TAU * 196 / SR; const s = Math.tanh(2.5 * (Math.sin(p1) + 0.8 * Math.sin(p2))); b[i] = s * Math.min(1, t / 0.008) * Math.min(1, (0.16 - t) / 0.03); } lowpass(b, 1400); mixInto(a, b, at, 0.8); } save('SFX_Final_PinWrong', a, -4); }
// --- kol tıkı: rezonanslı metal "tink" + çok kısa takırtı
{ const a = buf(0.05); mixInto(a, click(0.05, 3600, 0.009, 14), 0, 1); mixInto(a, click(0.01, 1500, 0.0015, 2), 0, 0.5); save('SFX_Final_HandleTick', a, -5); }
// --- mandal: tok vuruş (perdesi düşen 90 Hz) + metal klik + kısa tıngırtı kuyruğu
{ const a = buf(0.45); const th = buf(0.16); let ph = 0; for (let i = 0; i < th.length; i++) { const t = i / SR; ph += TAU * (70 + 60 * Math.exp(-t / 0.03)) / SR; th[i] = Math.sin(ph) * env(t, 0.002, 0.045); }
  mixInto(a, click(0.05, 2100, 0.012, 4), 0, 0.9); mixInto(a, th, 0.012, 1); mixInto(a, click(0.08, 2900, 0.02, 18), 0.03, 0.35); mixInto(a, click(0.12, 4300, 0.05, 25), 0.05, 0.18);
  const sl = buf(0.12); for (let i = 0; i < sl.length; i++) sl[i] = noise() * Math.sin(Math.PI * i / sl.length) * 0.4; bandpass(sl, 1200, 2); mixInto(a, sl, 0.14, 0.4);
  for (let i = 0; i < a.length; i++) a[i] = Math.tanh(a[i] * 1.8);
  save('SFX_Final_Latch', a, -1); }
// --- kâğıt: birkaç hışırtı patlaması (bant geçiren gürültü, düzensiz zarf)
{ const a = buf(0.46); const bursts = [[0, 0.07, 1.0], [0.06, 0.05, 0.7], [0.12, 0.1, 0.9], [0.24, 0.06, 0.5], [0.3, 0.12, 0.65]];
  for (const [at, len, g] of bursts) { const b = buf(len); for (let i = 0; i < b.length; i++) { const t = i / SR; const crackle = random() < 0.02 ? 3 : 1; b[i] = noise() * crackle * Math.sin(Math.PI * t / len) ** 0.7; } highpass(b, 1400); bandpass(b, 3800 + random() * 1500, 1.2); mixInto(a, b, at, g); }
  save('SFX_Final_Paper', a, -1); }
console.log(out.join('\n'));
