/* Apartman finali sesleri (özgün, prosedürel). Sadece Node.js standart kütüphanesi.
 * Çalıştır: node tools/sel_aksam_final/world/generate_final_sfx.js
 * Çıktı (Assets/Resources/Audio/SFX/): SFX_Final_DoorCreak (mandal tıkı + menteşe gıcırtısı), SFX_Final_DoorOpen (kanat savrulması),
 * SFX_Final_DoorClose (yavaş kapanış + mandal oturması), SFX_Final_Mailbox (metal kapak + kâğıt hışırtısı), SFX_Final_LockBeep (kısa bip).
 * .meta'ları Unity üretir. Sabit tohum: her çalıştırmada aynı dosyalar.
 */
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const OUT = process.env.SFX_OUT || path.resolve(__dirname, '../../../Assets/Resources/Audio/SFX');
const SR = 44100, TAU = Math.PI * 2, PEAK = Math.floor(32767 * 10 ** (-1 / 20));
let seed = 52417;
function random() { seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5; return (seed >>> 0) / 4294967296; }
const noise = () => random() * 2 - 1;
const buf = s => new Float64Array(Math.round(s * SR));
const env = (t, a, d) => t < 0 ? 0 : (t < a ? t / a : Math.exp(-(t - a) / d));

// iki kutuplu rezonans (bant geçiren) filtre — durumlu
function resonator(f, q) { let y1 = 0, y2 = 0; return x => { const w = TAU * f / SR, r = Math.exp(-w / (2 * q)); const y = x + 2 * r * Math.cos(w) * y1 - r * r * y2; y2 = y1; y1 = y; return y * (1 - r); }; }
function lowpass(fc) { let y = 0; const a = 1 - Math.exp(-TAU * fc / SR); return x => (y += a * (x - y)); }

// mandal / klik: kısa geniş bant darbe + iki metal kip
function click(a, at, gain = 1, tone = 2600) {
  const r1 = resonator(tone, 18), r2 = resonator(tone * 1.73, 22), lp = lowpass(5000);
  for (let i = Math.round(at * SR); i < Math.min(a.length, Math.round((at + 0.08) * SR)); i++) {
    const t = i / SR - at; const x = noise() * Math.exp(-t / 0.0025);
    a[i] += gain * (lp(x) * 0.6 + (r1(x) + r2(x)) * 4 * Math.exp(-t / 0.02));
  }
}
// menteşe gıcırtısı: yapış-kay darbeleri (perde yavaşça değişir) → gövde rezonansları
function creak(a, at, dur, f0, f1, gain = 1) {
  const bodies = [resonator(420, 9), resonator(910, 12), resonator(1650, 14), resonator(2900, 18)];
  let phase = 0;
  for (let i = Math.round(at * SR); i < Math.min(a.length, Math.round((at + dur) * SR)); i++) {
    const t = (i / SR - at) / dur; const f = f0 + (f1 - f0) * t + 18 * Math.sin(TAU * 3.1 * t * dur) + 9 * noise();
    phase += f / SR; let x = 0;
    if (phase >= 1) { phase -= 1; x = 1 + 0.4 * noise(); }
    const amp = Math.sin(Math.PI * Math.min(1, t * 1.15)) ** 0.7 * (0.75 + 0.25 * Math.sin(TAU * 1.7 * t * dur));
    let y = 0; bodies.forEach((b, k) => { y += b(x) * [1.0, 0.8, 0.55, 0.3][k]; });
    a[i] += gain * amp * y * 6;
  }
}
// hava savrulması (kanat): alçak geçiren gürültü kabarması
function whoosh(a, at, dur, gain = 1, fc0 = 300, fc1 = 900) {
  let y = 0;
  for (let i = Math.round(at * SR); i < Math.min(a.length, Math.round((at + dur) * SR)); i++) {
    const t = (i / SR - at) / dur; const fc = fc0 + (fc1 - fc0) * Math.sin(Math.PI * t); const al = 1 - Math.exp(-TAU * fc / SR);
    y += al * (noise() - y); a[i] += gain * y * Math.sin(Math.PI * t) ** 1.5;
  }
}
// tok vuruş (kapı kasaya oturur)
function thump(a, at, gain = 1) {
  for (let i = Math.round(at * SR); i < Math.min(a.length, Math.round((at + 0.4) * SR)); i++) {
    const t = i / SR - at; a[i] += gain * (Math.sin(TAU * (78 + 30 * Math.exp(-t / 0.03)) * t) * env(t, 0.002, 0.07) + Math.sin(TAU * 143 * t) * 0.4 * env(t, 0.001, 0.04));
  }
  const lp = lowpass(1800); for (let i = Math.round(at * SR); i < Math.min(a.length, Math.round((at + 0.05) * SR)); i++) { const t = i / SR - at; a[i] += gain * 0.5 * lp(noise()) * Math.exp(-t / 0.008); }
}
// metal kapak çınlaması
function clank(a, at, gain = 1) {
  const modes = [[1180, 0.18], [2710, 0.12], [4130, 0.08], [5600, 0.05]];
  for (let i = Math.round(at * SR); i < Math.min(a.length, Math.round((at + 0.6) * SR)); i++) {
    const t = i / SR - at; let y = 0; for (const [f, d] of modes) y += Math.sin(TAU * f * t + f) * Math.exp(-t / d) / modes.length;
    a[i] += gain * (y * 1.6 + noise() * Math.exp(-t / 0.004) * 0.6);
  }
}
// kâğıt hışırtısı: kısa bant geçiren gürültü patlamaları
function rustle(a, at, dur, gain = 1) {
  const bp = resonator(3200, 1.2), bp2 = resonator(6500, 1.5);
  let next = at;
  while (next < at + dur) {
    const len = 0.02 + random() * 0.06, g = 0.4 + random() * 0.6;
    for (let i = Math.round(next * SR); i < Math.min(a.length, Math.round((next + len) * SR)); i++) { const t = (i / SR - next) / len; const x = noise(); a[i] += gain * g * (bp(x) * 1.4 + bp2(x)) * Math.sin(Math.PI * t); }
    next += len * (0.6 + random() * 0.8);
  }
}
function encode(a) {
  const fade = Math.round(SR * 0.008);
  let mean = 0; for (let i = 0; i < a.length; i++) mean += a[i]; mean /= a.length;
  let peak = 0; for (let i = 0; i < a.length; i++) { const k = Math.min(1, i / fade, (a.length - 1 - i) / fade); a[i] = (a[i] - mean) * k; peak = Math.max(peak, Math.abs(a[i])); }
  const wav = Buffer.alloc(44 + a.length * 2);
  wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(SR, 24); wav.writeUInt32LE(SR * 2, 28); wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
  wav.write('data', 36); wav.writeUInt32LE(a.length * 2, 40);
  for (let i = 0; i < a.length; i++) wav.writeInt16LE(Math.round(a[i] / peak * PEAK), 44 + i * 2);
  return wav;
}
fs.mkdirSync(OUT, { recursive: true });
const write = (name, a) => { fs.writeFileSync(path.join(OUT, name + '.wav'), encode(a)); console.log(name + '.wav', (a.length / SR).toFixed(2) + ' sn'); };

{ // mandal tıkı → kısa gıcırtı (kapı aralanır)
  const a = buf(1.15); click(a, 0.01, 1.0, 2300); click(a, 0.07, 0.5, 1900); creak(a, 0.2, 0.85, 95, 60, 0.9); write('SFX_Final_DoorCreak', a);
}
{ // kanat açılır: savrulma + uzun alçak gıcırtı
  const a = buf(1.35); whoosh(a, 0.0, 1.1, 0.55, 260, 700); creak(a, 0.05, 0.9, 70, 115, 0.55); write('SFX_Final_DoorOpen', a);
}
{ // yavaş kapanış: gıcırtı + savrulma, sonunda mandal oturur
  const a = buf(1.95); creak(a, 0.0, 1.2, 120, 80, 0.5); whoosh(a, 0.3, 1.1, 0.4, 220, 520); thump(a, 1.5, 1.0); click(a, 1.51, 0.7, 2100); write('SFX_Final_DoorClose', a);
}
{ // posta kutusu: küçük menteşe ciyaklaması + metal kapak + zarf hışırtısı
  const a = buf(1.0); creak(a, 0.0, 0.18, 260, 340, 0.35); clank(a, 0.14, 0.8); rustle(a, 0.42, 0.45, 0.55); write('SFX_Final_Mailbox', a);
}
{ // kilit bipi
  const a = buf(0.16); for (let i = 0; i < a.length; i++) { const t = i / SR; const sq = Math.sign(Math.sin(TAU * 2200 * t)) * 0.6 + Math.sin(TAU * 2200 * t) * 0.4; a[i] = sq * (t < 0.11 ? Math.min(1, t / 0.004) : Math.max(0, 1 - (t - 0.11) / 0.03)); }
  const lp = lowpass(6000); for (let i = 0; i < a.length; i++) a[i] = lp(a[i]); write('SFX_Final_LockBeep', a);
}
