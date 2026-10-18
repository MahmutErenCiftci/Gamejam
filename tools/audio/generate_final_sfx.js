/* Son ek sesler (özgün, prosedürel; 18 Eki). Sadece Node.js standart kütüphanesi.
 * Çalıştır: node tools/audio/generate_final_sfx.js            (var olan dosyaların üzerine YAZMAZ; zorlamak için --force)
 *           AUDIO_OUT=<klasör> ile denemede başka yere yazılır (SFX/ ve Ambience/ alt klasörleri olmalı).
 * Çıktı (44,1 kHz, 16 bit, mono; .meta'ları Unity üretir):
 *   Assets/Resources/Audio/Ambience/
 *     AMB_EQ_Sirens_Distant_Loop       DÖNGÜ (20 sn): uzaktan iki ambulans/itfaiye sireni (yavaş yükselip alçalan), şehir uğultusu
 *     AMB_FE_Neighbourhood_Night_Loop  DÖNGÜ (30 sn): yağmurlu akşam mahalle zemini (uzak trafik, uzak köpek, oluk damlaları), alçak-orta
 *   Assets/Resources/Audio/SFX/
 *     SFX_EQ_Car_Alarm        karşı sokaktan araba alarmı (iki ton → yükselen "vuup" → hızlı titreşim), oda yankısı
 *     SFX_UI_Phone_Vibrate    ahşap masada titreyen telefon: iki "bzzt"
 *     SFX_FE_Lightning_Crack  yakına düşen yıldırım: sert çatırtı → yırtılma → kısa gürleme (tepe ≤ 0,8)
 *     SFX_EQ_Building_Creak   binanın zorlanma gıcırtısı: alçak inilti + küçük tıkırtılar
 *     SFX_EQ_Dust_Debris      sıva parçaları ve toz yere dökülür: minik tıkırtılar + hafif hışırtı
 * Döngüler "dairesel" üretilir: olaylar sona taşarsa başa sarılır, süzgeç/yankı döngü üzerinde iki tur çalıştırılır,
 * sirenlerin faz toplamı tam sayı devire ayarlanır — böylece ek yeri kesintisizdir (çapraz geçiş gerekmez).
 */
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const ROOT = process.env.AUDIO_OUT || path.resolve(__dirname, '../../Assets/Resources/Audio');
const FORCE = process.argv.includes('--force');
const SR = 44100, TAU = Math.PI * 2, PEAK = 10 ** (-1 / 20);
let seed = 77213;
function random() { seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5; return (seed >>> 0) / 4294967296; }
const noise = () => random() * 2 - 1;
const buf = seconds => new Float64Array(Math.round(seconds * SR));
const clamp01 = x => Math.max(0, Math.min(1, x));
const db = x => 20 * Math.log10(x);

// opts: { loop, gain (tepe = -1 dBFS × gain), rms (dB; verilirse tepe yerine RMS'e göre ölçeklenir), drive (tanh sıkıştırma) }
function encode(a, { loop = false, gain = 1, rms = null, drive = 0 } = {}) {
  let mean = 0; for (let i = 0; i < a.length; i++) mean += a[i]; mean /= a.length;
  for (let i = 0; i < a.length; i++) a[i] -= mean;
  if (drive > 0) { let p = 0; for (const v of a) p = Math.max(p, Math.abs(v)); for (let i = 0; i < a.length; i++) a[i] = Math.tanh(a[i] / p * drive) / Math.tanh(drive); }
  if (!loop) { const fi = Math.round(SR * .004), fo = Math.round(SR * .03);
    for (let i = 0; i < a.length; i++) { const k = clamp01(Math.min(i / fi, (a.length - 1 - i) / fo)); a[i] *= .5 - .5 * Math.cos(Math.PI * k); } }
  let peak = 0, sq = 0; for (const v of a) { peak = Math.max(peak, Math.abs(v)); sq += v * v; }
  let scale = PEAK * gain / peak;
  if (rms != null) { scale = 10 ** (rms / 20) / Math.sqrt(sq / a.length); if (peak * scale > PEAK) throw new Error('RMS hedefi tepeyi aşıyor'); }
  const wav = Buffer.alloc(44 + a.length * 2);
  wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(SR, 24); wav.writeUInt32LE(SR * 2, 28); wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
  wav.write('data', 36); wav.writeUInt32LE(a.length * 2, 40);
  for (let i = 0; i < a.length; i++) wav.writeInt16LE(Math.max(-32767, Math.min(32767, Math.round(a[i] * scale * 32767))), 44 + i * 2);
  return wav;
}
const written = [], skipped = [];
function save(sub, name, a, opts) {
  const file = path.join(ROOT, sub, name + '.wav');
  if (fs.existsSync(file) && !FORCE) { skipped.push(name); return; }
  fs.writeFileSync(file, encode(a, opts)); written.push(name);
}

// ------------------------------------------------------------------ yapı taşları (generate_polish_sfx.js ile aynı)
function svf(q = 0.7) { let lp = 0, bp = 0; return (x, fc) => { const f = 2 * Math.sin(Math.PI * Math.min(fc, SR / 6) / SR); const hp = x - lp - q * bp; bp += f * hp; lp += f * bp; return { lp, bp, hp }; }; }
function onepole(fc) { const a = 1 - Math.exp(-TAU * fc / SR); let y = 0; return x => (y += a * (x - y)); }
function highpass(fc) { const lp = onepole(fc); return x => x - lp(x); }
const ad = (t, at, dec) => t < 0 ? 0 : (t < at ? t / at : Math.exp(-(t - at) / dec));
function tone(a, t0, dur, f0, f1, g = 1, at = 0.006, rel = 0.05, harm = 0.15) {
  const i0 = Math.round(t0 * SR), n = Math.round(dur * SR); let ph = 0;
  for (let k = 0; k < n && i0 + k < a.length; k++) {
    const t = k / SR, f = f0 + (f1 - f0) * (k / n); ph += TAU * f / SR;
    const e = Math.min(1, t / at, (dur - t) / rel);
    a[i0 + k] += (Math.sin(ph) + harm * Math.sin(2 * ph) + harm * 0.4 * Math.sin(3 * ph)) * g * clamp01(e);
  }
}
function thump(a, t0, f0, f1, g = 1, dec = 0.12, noiseG = 0.5, nfc = 400) {
  const i0 = Math.round(t0 * SR); let ph = 0; const lp = onepole(nfc), lp2 = onepole(nfc);
  for (let i = i0; i < a.length; i++) { const t = (i - i0) / SR; if (t > dec * 8) break; const f = f1 + (f0 - f1) * Math.exp(-t / 0.04); ph += TAU * f / SR;
    a[i] += (Math.sin(ph) * Math.exp(-t / dec) + lp2(lp(noise())) * 4 * noiseG * Math.exp(-t / (dec * 0.4))) * g * Math.min(1, t / 0.002); }
}
function knock(a, t0, fc, g = 1, dec = 0.03, q = 0.08) {
  const i0 = Math.round(t0 * SR); const f = svf(q);
  for (let i = i0; i < a.length; i++) { const t = (i - i0) / SR; if (t > dec * 10) break; const ex = t < 0.0015 ? noise() : noise() * 0.05; a[i] += f(ex, fc).bp * g * Math.exp(-t / dec); }
}
function creak(a, t0, dur, fA, fB, fc, g = 1, q = 0.12) {
  const i0 = Math.round(t0 * SR), n = Math.round(dur * SR); let ph = 0; const f = svf(q), f2 = svf(0.3);
  for (let k = 0; k < n && i0 + k < a.length; k++) { const t = k / SR, x = k / n; const fr = fA + (fB - fA) * x + 6 * Math.sin(TAU * 3.3 * t);
    const prev = ph; ph += fr / SR; const pulse = Math.floor(ph) !== Math.floor(prev) ? 1 : 0;
    const e = Math.sin(Math.PI * clamp01(x)) ** 0.7;
    const y = f(pulse * (0.7 + 0.6 * random()), fc).bp; a[i0 + k] += (y + f2(y, fc * 2.1).bp * 0.4) * g * e; }
}
// Freeverb benzeri mono yankı (8 tarak + 4 tüm geçiren). size: gecikme ölçeği, room: geri besleme, damp: tiz sönümü
function reverb({ size = 1, room = 0.84, damp = 0.3, wet = 0.4, dry = 1 } = {}) {
  const combs = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617].map(d => ({ b: new Float64Array(Math.max(8, Math.round(d * size))), i: 0, s: 0 }));
  const alls = [556, 441, 341, 225].map(d => ({ b: new Float64Array(Math.max(8, Math.round(d * size))), i: 0 }));
  return x => {
    const inp = x * 0.015; let out = 0;
    for (const c of combs) { const y = c.b[c.i]; c.s = y * (1 - damp) + c.s * damp; c.b[c.i] = inp + c.s * room; c.i = (c.i + 1) % c.b.length; out += y; }
    for (const p of alls) { const bo = p.b[p.i]; const y = -out + bo; p.b[p.i] = out + bo * 0.5; p.i = (p.i + 1) % p.b.length; out = y; }
    return x * dry + out * wet * 3;
  };
}
// doğrusal işlem: f'yi dizi boyunca uygula
function run(a, f) { for (let i = 0; i < a.length; i++) a[i] = f(a[i], i); return a; }
// dairesel işlem (döngüler için): durumlu f'yi döngü üzerinde 'passes' tur çalıştır, son turu tut → ek yeri kesintisiz
function circ(a, f, passes = 3) { const out = new Float64Array(a.length); for (let p = 0; p < passes; p++) for (let i = 0; i < a.length; i++) { const y = f(a[i], i); if (p === passes - 1) out[i] = y; } return out; }
// döngü sonundan taşan kısmı başa ekle (olaylar sınırda kesilmesin)
function fold(raw, seconds) { const n = Math.round(seconds * SR), out = raw.slice(0, n); for (let i = n; i < raw.length; i++) out[i % n] += raw[i]; return out; }
const white = n => { const a = new Float64Array(n); for (let i = 0; i < n; i++) a[i] = noise(); return a; };
const bp = (fc, q) => { const f = svf(q); return x => f(x, fc).bp; };
const lp2 = fc => { const a = onepole(fc), b = onepole(fc); return x => b(a(x)); };

// ------------------------------------------------------------------ 1) AMB_EQ_Sirens_Distant_Loop (20 sn)
{
  const S = 20, N = S * SR;
  // mekanik siren eğrisi: hızlı kalkış + plato, yavaş iniş (0..1)
  const wail = (p, r = 0.42) => { p = ((p % 1) + 1) % 1; if (p < r) { const x = p / r; return (1 - Math.exp(-3.5 * x)) / (1 - Math.exp(-3.5)); } const x = (p - r) / (1 - r); return (Math.exp(-2.6 * x) - Math.exp(-2.6)) / (1 - Math.exp(-2.6)); };
  const sirens = [
    { fmin: 640, fmax: 1280, cycles: 4, off: 0.0, dop: 0.012, dph: 2.4, amp: t => 0.75 + 0.25 * Math.sin(TAU * t / S + 2.4) },
    { fmin: 575, fmax: 1160, cycles: 3, off: 0.37, dop: 0.02, dph: 0.6, amp: t => 0.25 + 0.75 * (0.5 + 0.5 * Math.sin(TAU * t / S + 0.6)) ** 1.3 },
  ];
  const harm = [[1, 1], [2, 0.42], [3, 0.3], [4, 0.1], [5, 0.08], [6, 0.03]];
  const dry = new Float64Array(N);
  for (const s of sirens) {
    const f = new Float64Array(N); let cyc = 0;
    for (let i = 0; i < N; i++) { const t = i / SR;
      f[i] = (s.fmin + (s.fmax - s.fmin) * wail(t * s.cycles / S + s.off)) * (1 + s.dop * Math.sin(TAU * t / S + s.dph))   // Doppler benzeri kayma (yaklaşıp uzaklaşma)
           * (1 + 0.003 * Math.sin(TAU * 17 * t / S + s.off * 9));                                                         // hava türbülansı titremesi
      cyc += f[i] / SR; }
    const k = Math.round(cyc) / cyc; let ph = 0;                                                                         // tam sayı devir → kesintisiz döngü
    for (let i = 0; i < N; i++) { const t = i / SR; ph += TAU * f[i] * k / SR;
      let y = 0; for (const [h, g] of harm) y += Math.sin(h * ph) * g;
      const flutter = 1 + 0.18 * Math.sin(TAU * 7 * t / S + s.dph) + 0.1 * Math.sin(TAU * 13 * t / S + 1.7 * s.dph) + 0.06 * Math.sin(TAU * 31 * t / S + s.off);
      dry[i] += y * s.amp(t) * flutter; }
  }
  let sir = circ(dry, lp2(1100));                                                     // uzaklık: tizler yutulur
  sir = circ(sir, highpass(280));
  sir = circ(sir, reverb({ size: 1.6, room: 0.9, damp: 0.55, wet: 1.0, dry: 0.22 }));  // binalar arası yankı / bulaşma
  sir = circ(sir, lp2(1700));
  // şehir uğultusu: alçak kahverengi gürültü + uzak trafik bandı
  const n1 = circ(white(N), lp2(110)), n2 = circ(white(N), bp(260, 0.8));
  let r1 = 0, r2 = 0, r3 = 0; for (let i = 0; i < N; i++) { r1 += n1[i] ** 2; r2 += n2[i] ** 2; r3 += sir[i] ** 2; }
  const sr = Math.sqrt(r3 / N), g1 = sr * 0.32 / Math.sqrt(r1 / N), g2 = sr * 0.14 / Math.sqrt(r2 / N);
  const a = new Float64Array(N);
  for (let i = 0; i < N; i++) { const t = i / SR; a[i] = sir[i] + n1[i] * g1 + n2[i] * g2 * (0.8 + 0.2 * Math.sin(TAU * 2 * t / S)); }
  save('Ambience', 'AMB_EQ_Sirens_Distant_Loop', a, { loop: true, rms: -24 });
}

// ------------------------------------------------------------------ 2) SFX_EQ_Car_Alarm (~4,3 sn)
{
  const D = 3.85, a = buf(D + 0.45); let ph = 0;
  const freq = t => {
    if (t < 1.25) return Math.floor(t / 0.125) % 2 ? 1080 : 1520;                                       // iki ton (hi-lo)
    if (t < 2.65) { const x = ((t - 1.25) % 0.28) / 0.28; return x < 0.85 ? 560 + 1380 * (x / 0.85) ** 0.8 : 1940; }   // yükselen "vuup"
    return 1400 + 420 * Math.sin(TAU * 10 * (t - 2.65));                                               // hızlı titreşim (yelp)
  };
  for (let i = 0; i < Math.round(D * SR); i++) {
    const t = i / SR, f = freq(t); ph += TAU * f / SR;
    let y = 0; for (let h = 1; h <= 9; h += 2) if (h * f < 9000) y += Math.sin(h * ph) / h;                // piezo kare dalgası (tek harmonikler)
    y += 0.15 * Math.sin(2 * ph);
    const gate = t >= 1.25 && t < 2.65 ? clamp01(Math.min(((t - 1.25) % 0.28) / 0.01, (0.28 - (t - 1.25) % 0.28) / 0.012)) : 1;
    a[i] = y * gate * Math.min(1, t / 0.01, (D - t) / 0.01);
  }
  // karşı sokaktan: bant sınırlı, bina yüzeyinden yansıma (~95 ms), sokak/oda yankısı
  run(a, highpass(320)); run(a, lp2(2300));
  const echo = new Float64Array(a.length), dl = Math.round(0.095 * SR); for (let i = dl; i < a.length; i++) echo[i] = a[i - dl] * 0.28;
  for (let i = 0; i < a.length; i++) a[i] += echo[i];
  run(a, reverb({ size: 0.75, room: 0.78, damp: 0.45, wet: 0.55, dry: 0.75 }));
  run(a, lp2(3800));
  save('SFX', 'SFX_EQ_Car_Alarm', a, { rms: -17, drive: 1.2 });
}

// ------------------------------------------------------------------ 3) SFX_UI_Phone_Vibrate (1,2 sn)
{
  const a = buf(1.2); const ex = new Float64Array(a.length);
  const pulses = [[0.02, 0.42], [0.66, 0.42]];
  let ph = 0, prevSin = 0;
  const motor = new Float64Array(a.length);
  for (let i = 0; i < a.length; i++) {
    const t = i / SR; let spin = 0;
    for (const [t0, d] of pulses) { const x = t - t0; if (x >= 0 && x < d) spin = Math.max(spin, 1 - Math.exp(-x / 0.035)); else if (x >= d) spin = Math.max(spin, Math.exp(-(x - d) / 0.045)); }
    const f = 172 * (0.55 + 0.45 * spin) * (1 + 0.006 * Math.sin(TAU * 9 * t));
    ph += TAU * f / SR; const s = Math.sin(ph), A = spin * spin;
    motor[i] = (s + 0.5 * Math.sin(2 * ph + 0.4) + 0.25 * Math.sin(3 * ph)) * A;                      // gövde vızıltısı
    if (prevSin < 0.8 && s >= 0.8 && A > 0.12) ex[i] = (A - 0.1) * (0.75 + 0.5 * random());            // her devirde telefon masaya çarpar
    if (prevSin > -0.6 && s <= -0.6 && A > 0.3) ex[i] += (A - 0.28) * 0.4 * (0.6 + 0.8 * random());
    prevSin = s;
  }
  // ahşap masa rezonansları (çarpma dürtüleriyle uyarılır)
  const modes = [[185, 0.03, 1.0], [330, 0.04, 0.7], [610, 0.05, 0.55], [1150, 0.07, 0.35], [2350, 0.1, 0.18], [3900, 0.15, 0.08]].map(([fc, q, g]) => ({ f: svf(q), fc, g }));
  const click = svf(0.6), mlp = lp2(1800);
  for (let i = 0; i < a.length; i++) {
    let y = 0; for (const m of modes) y += m.f(ex[i], m.fc).bp * m.g;
    a[i] = y * 0.9 + click(ex[i] * (0.5 + 0.5 * random()), 3000).bp * 0.25 + mlp(motor[i]) * 0.22;
  }
  run(a, highpass(90));
  run(a, reverb({ size: 0.35, room: 0.6, damp: 0.5, wet: 0.12, dry: 1 }));
  save('SFX', 'SFX_UI_Phone_Vibrate', a, { rms: -16, drive: 1.6 });
}

// ------------------------------------------------------------------ 4) SFX_FE_Lightning_Crack (~2,1 sn)
{
  const a = buf(2.1);
  // ilk çatırtı: N-dalgası (şok cephesi) + geniş bantlı gürültü patlaması
  for (let i = 0; i < Math.round(0.004 * SR); i++) { const x = i / (0.004 * SR); a[i] += (1 - 2 * x) * 0.9; }
  { const h = highpass(500); for (let i = 0; i < Math.round(0.06 * SR); i++) { const t = i / SR; a[i] += h(noise()) * 1.1 * Math.exp(-t / 0.006); } }
  // yırtılma: sıklığı azalan çatırtı dürtüleri, kayan bant geçiren süzgeçten + "yırtılan kumaş" gürültüsü
  { const f = svf(0.5), f2 = svf(0.9); let rw = 0; const ex = new Float64Array(a.length);
    for (let t = 0.004; t < 0.75;) { const rate = 2600 * Math.exp(-t / 0.08) + 90 * Math.exp(-t / 0.35) + 12; t += -Math.log(1 - random()) / rate;
      const i0 = Math.round(t * SR), len = Math.round((0.0004 + random() * 0.0018) * SR), g = (0.3 + random() * 0.7) * Math.exp(-t / 0.22);
      for (let k = 0; k < len && i0 + k < a.length; k++) ex[i0 + k] += noise() * g * (1 - k / len); }
    for (let i = 0; i < Math.round(1.0 * SR); i++) { const t = i / SR; rw = 0.995 * rw + 0.1 * noise(); const am = Math.min(1, Math.abs(rw) * 1.6);
      const fc = 1300 + 4200 * Math.exp(-t / 0.12);
      a[i] += f(ex[i], fc).bp * 1.6 + f2(noise(), 900 + 2400 * Math.exp(-t / 0.18)).bp * am * 0.35 * ad(t, 0.004, 0.17); } }
  // gümleme: derin darbe + topaklı alçak gürleme
  thump(a, 0.012, 75, 34, 0.7, 0.35, 0.8, 180);
  { const l = lp2(140), l2 = lp2(320); let rw = 0;
    for (let i = 0; i < a.length; i++) { const t = i / SR; rw = 0.9997 * rw + 0.02 * noise(); const lump = 0.65 + Math.min(0.7, Math.abs(rw) * 4);
      a[i] += (l(noise()) * 7 + l2(noise()) * 1.2) * (1 - Math.exp(-t / 0.09)) * Math.exp(-t / 0.65) * lump * 0.8; } }
  run(a, reverb({ size: 1.3, room: 0.72, damp: 0.4, wet: 0.22, dry: 1 }));
  run(a, lp2(9000));
  save('SFX', 'SFX_FE_Lightning_Crack', a, { gain: 0.89, drive: 2.2 });   // tepe ≈ 0,79
}

// ------------------------------------------------------------------ 5) SFX_EQ_Building_Creak (2,5 sn)
{
  const a = buf(2.5);
  // yapış-kay inilti: yavaş dürtü dizisi alçak yapı rezonanslarını uyarır
  { const modes = [[62, 0.04, 1.0], [97, 0.05, 0.8], [168, 0.06, 0.55], [290, 0.08, 0.35], [520, 0.12, 0.15]].map(([fc, q, g]) => ({ f: svf(q), fc, g }));
    const rate = t => 9 + 14 * Math.sin(Math.PI * clamp01(t / 1.1)) ** 2 + 8 * clamp01((t - 1.3) / 0.5) * clamp01((2.2 - t) / 0.3);
    let ph = 0;
    for (let i = 0; i < a.length; i++) { const t = i / SR; const prev = ph; ph += rate(t) * (1 + 0.15 * Math.sin(TAU * 2.1 * t)) / SR;
      const env = clamp01(t / 0.15) * clamp01((2.35 - t) / 0.4) * (0.75 + 0.25 * Math.sin(TAU * 0.9 * t));
      const p = Math.floor(ph) !== Math.floor(prev) ? (0.6 + 0.8 * random()) * env : 0;
      let y = 0; for (const m of modes) y += m.f(p + noise() * 0.004 * env, m.fc * (1 + 0.04 * Math.sin(TAU * 0.6 * t))).bp * m.g;
      a[i] += y * 1.4; } }
  creak(a, 0.05, 1.0, 14, 26, 230, 0.55, 0.1);                 // ahşap/kiriş gıcırtısı
  creak(a, 1.25, 1.05, 22, 11, 190, 0.6, 0.1);
  { const l = lp2(90); for (let i = 0; i < a.length; i++) { const t = i / SR; a[i] += l(noise()) * 2.2 * Math.sin(Math.PI * clamp01(t / 2.5)); } }   // beton gövde uğultusu
  // küçük çıtırtılar (sıva/derz)
  [0.31, 0.58, 0.66, 1.02, 1.37, 1.44, 1.81, 2.05].forEach(t => { knock(a, t + random() * 0.03, 1300 + random() * 1700, 0.08 + random() * 0.08, 0.008, 0.12); });
  [0.72, 1.6].forEach(t => thump(a, t, 160, 95, 0.12, 0.05, 0.4, 600));
  run(a, reverb({ size: 0.8, room: 0.7, damp: 0.5, wet: 0.25, dry: 1 }));
  run(a, lp2(3500)); run(a, highpass(30));
  save('SFX', 'SFX_EQ_Building_Creak', a, { rms: -18, drive: 1.5 });
}

// ------------------------------------------------------------------ 6) SFX_EQ_Dust_Debris (1,5 sn)
{
  const a = buf(1.5);
  thump(a, 0.0, 190, 110, 0.35, 0.05, 0.6, 800);                                        // küçük sıva topağı
  for (let k = 0; k < 9; k++) { const t = 0.01 + random() * 0.32; knock(a, t, 700 + random() * 1300, 0.25 + random() * 0.25, 0.01 + random() * 0.01, 0.15); }
  for (let t = 0; t < 1.4;) {                                                           // minik parçalar: sıklığı azalan tıkırtılar
    const rate = 320 * Math.exp(-t / 0.3) + 45 * Math.exp(-t / 0.9) + 4; t += -Math.log(1 - random()) / rate; if (t > 1.4) break;
    knock(a, t, 2200 + random() * 5000, (0.04 + random() * 0.18) * (1 - t / 1.6), 0.0015 + random() * 0.004, 0.2);
  }
  { const f = svf(1.1), g = svf(0.8);                                                   // dökülen toz/kum: hafif hışırtı + tane tane doku
    for (let i = 0; i < a.length; i++) { const t = i / SR; const grain = random() < 0.08 ? noise() : 0;
      a[i] += (f(noise(), 3200 - 900 * clamp01(t / 1.2)).bp * 0.18 + g(grain, 5200).bp * 0.5) * ad(t, 0.04, 0.42); } }
  run(a, reverb({ size: 0.45, room: 0.65, damp: 0.45, wet: 0.18, dry: 1 }));
  run(a, lp2(9000)); run(a, highpass(60));
  save('SFX', 'SFX_EQ_Dust_Debris', a, { rms: -20.5, drive: 2.2 });
}

// ------------------------------------------------------------------ 7) AMB_FE_Neighbourhood_Night_Loop (30 sn)
{
  const S = 30, N = S * SR;
  const cw = (t, c, w) => { let d = Math.abs(t - c) % S; d = Math.min(d, S - d); return Math.exp(-((d / w) ** 2)); };   // dairesel Gauss tümsek
  // uzak trafik: alçak uğultu + ıslak yolda geçen arabaların hışırtısı (kabarmalar)
  const hum = circ(white(N), lp2(120)), road = circ(white(N), bp(380, 0.7)), tyre = circ(circ(white(N), bp(1100, 0.9)), lp2(1700));
  const cars = [[3.5, 2.6, 1.0], [11.0, 3.4, 0.7], [17.5, 2.2, 0.85], [24.8, 3.0, 0.6]];
  const rmsOf = x => Math.sqrt(x.reduce((s, v) => s + v * v, 0) / x.length);
  const gh = 1 / rmsOf(hum), gr = 1 / rmsOf(road), gt = 1 / rmsOf(tyre);
  const a = new Float64Array(N);
  for (let i = 0; i < N; i++) { const t = i / SR; let sw = 0; for (const [c, w, g] of cars) sw += g * cw(t, c, w);
    a[i] = hum[i] * gh * 0.55 + road[i] * gr * (0.18 + 0.32 * sw) + tyre[i] * gt * 0.12 * sw * (1 + 0.1 * Math.sin(TAU * 3 * t / S)); }
  // olaylar (köpek, damlalar) ayrı tampona; taşanlar başa sarılır
  const ev = buf(S + 2);
  const bark = (t0, f0, g) => {
    const dur = 0.13 + random() * 0.05, i0 = Math.round(t0 * SR), n = Math.round((dur + 0.02) * SR);
    const F = [[620, 0.25, 1.0], [1150, 0.3, 0.7], [2300, 0.4, 0.2]].map(([fc, q, gg]) => ({ f: svf(q), fc, gg })); let ph = 0;
    for (let k = 0; k < n; k++) { const t = k / SR; const f = f0 * (1.08 - 0.3 * clamp01(t / dur)) * (1 + 0.03 * noise()); ph += f / SR;
      const src = (2 * (ph % 1) - 1) * 0.8 + noise() * (0.35 + 0.6 * Math.exp(-t / 0.015));
      const env = Math.min(1, t / 0.008) * Math.exp(-Math.max(0, t - 0.03) / (dur * 0.45));
      let y = 0; for (const m of F) y += m.f(src, m.fc).bp * m.gg; ev[i0 + k] += y * env * g; }
  };
  bark(7.9, 430, 0.4); bark(8.32, 455, 0.34); bark(21.6, 410, 0.28);                   // uzak köpek: "hav-hav" ... "hav"
  // oluk damlaları: iki kaynak (biri düzenli, biri seyrek), boğuk "tıp" sesi
  const drip = (t0, f0, g) => { tone(ev, t0, 0.035, f0, f0 * 1.55, g, 0.002, 0.03, 0.05); knock(ev, t0, f0 * 0.8, g * 0.35, 0.01, 0.2); };
  for (let t = 0.3; t < S; t += 0.62 + random() * 0.22) drip(t, 640 + random() * 60, 0.05 + random() * 0.02);
  for (let t = 1.1; t < S; t += 1.4 + random() * 1.1) drip(t, 880 + random() * 120, 0.03 + random() * 0.015);
  for (let t = 4.0; t < S; t += 5 + random() * 4) { tone(ev, t, 0.12, 1180, 1180, 0.02, 0.002, 0.11, 0.3); }   // metal oluğa düşen damla ("tink", boğuk)
  let e = fold(ev, S);
  e = circ(e, lp2(1900));
  const mix = new Float64Array(N); const ga = 1 / rmsOf(a);
  for (let i = 0; i < N; i++) mix[i] = a[i] * ga * 0.05 + e[i];
  let out = circ(mix, reverb({ size: 1.4, room: 0.85, damp: 0.6, wet: 0.5, dry: 0.7 }));
  out = circ(out, lp2(2400)); out = circ(out, highpass(35));
  save('Ambience', 'AMB_FE_Neighbourhood_Night_Loop', out, { loop: true, rms: -28 });
}

console.log('yazıldı:', written.length ? written.join(', ') : '-');
if (skipped.length) console.log('zaten vardı (atlandı):', skipped.join(', '));
