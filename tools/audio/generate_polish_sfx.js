/* Son cila sesleri (özgün, prosedürel; POLISH-ASSETS 18 Eki). Sadece Node.js standart kütüphanesi.
 * Çalıştır: node tools/audio/generate_polish_sfx.js            (var olan dosyaların üzerine YAZMAZ; zorlamak için --force)
 * Çıktı (44,1 kHz, 16 bit, mono, tepe ≈ -1 dBFS × kazanç; .meta'ları Unity üretir):
 *   Assets/Resources/Audio/SFX/
 *     SFX_EQ_Shelf_Drill      matkap: kalkış → duvara giriş → duruş (yumuşatılmış motor, tiz kısık)
 *     SFX_EQ_Shelf_Fasten     cırcır tık-tık-tık + metal "tink" (vida/kayış sıkıldı)
 *     SFX_EQ_Shelf_Wobble     ahşap gıcırtı + raftaki eşyaların hafif tıkırtısı
 *     SFX_EQ_Shelf_Crash      ağır mobilya devrilmesi: gıcırtı → tok gümleme → ahşap/kitap takırtısı (tiz patlama yok)
 *     SFX_EQ_Injured          yumuşak boğuk darbe + inen alçak ton ("ah" hissi; kanlı/ürkütücü değil)
 *     SFX_EQ_Protect_Step     Çök-Kapan-Tutun adımı doğru: kısa iki notalı yükselen blip
 *     SFX_EQ_Protect_Release  tutunmayı bıraktın uyarısı: iki kez inen yumuşak çift ton
 *     SFX_EQ_Inspect          kart/kâğıt açılması: hafif hışırtı + kâğıt fiskesi
 *     SFX_EQ_Hazard_Warn      tehlike bölgesi uyarısı: iki kısa yumuşak "dıt-dıt"
 *     SFX_EQ_Door_Locked      kilitli kapı: kol basma + metal takırdama
 *     SFX_EQ_Door_Open        mandal tık + kısa gıcırtı + hava
 *     SFX_EQ_Bag_Wear         fermuar + askı hışırtısı + toka tık
 *     SFX_EQ_Question_Open    soru kartı açılır: yükselen yumuşak "pop"
 *     SFX_EQ_Assembly_Arrive  toplanma alanına varış: rahatlatıcı çan arpeji (Do majör)
 *     SFX_FE_Branch_Push      DÖNGÜ (1,6 sn): dal/yaprak sürtünmesi
 *     SFX_FE_Passage_Open     dal kenara itildi: yaprak savrulması + tok ses + iki notalı olumlu ton
 *     SFX_FE_SafeZone         güvenli bölge: sıcak, rahatlatıcı çan akoru (Sol majör)
 *     SFX_FE_Sweep_Splash     suya düşme/sürüklenme şıpırtısı (büyük ama yumuşak)
 *     SFX_FE_Sweep_Current    DÖNGÜ (3 sn): akan su uğultusu + kabarcıklar
 *     SFX_FE_Tree_Crack       ağaç çatırdaması (gövde gıcırtısı + birkaç kuru çıt)
 *     SFX_FE_Tree_Fall        ağaç devrilmesi: yaprak hışırtısı → tok gümleme + su sıçraması
 *     SFX_UI_Confirm          onay: iki notalı yükselen kısa ton
 *     SFX_UI_Dialogue_Next    diyalog ilerle: çok kısa, yumuşak blip
 *   Assets/Resources/Audio/Ambience/
 *     AMB_EQ_Assembly_Outdoor_Loop  DÖNGÜ (20 sn): uzaktan kalabalık mırıltısı + seyrek kuş cıvıltısı + hafif rüzgâr
 */
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const ROOT = process.env.AUDIO_OUT || path.resolve(__dirname, '../../Assets/Resources/Audio');   // denemede AUDIO_OUT=geçici klasör (SFX/ ve Ambience/ alt klasörleri olmalı)
const FORCE = process.argv.includes('--force');
const SR = 44100, TAU = Math.PI * 2, PEAK = Math.floor(32767 * 10 ** (-1 / 20));
let seed = 51123;
function random() { seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5; return (seed >>> 0) / 4294967296; }
const noise = () => random() * 2 - 1;
const buf = seconds => new Float64Array(Math.round(seconds * SR));
const clamp01 = x => Math.max(0, Math.min(1, x));

// drive > 0: yumuşak sıkıştırma (tanh) — kısa vuruşlu seslerin ortalama seviyesini diğer SFX'lere yaklaştırır, kırpma olmaz
function encode(a, loop, gain = 1, drive = 0) {
  if (drive > 0) { let p = 0; for (let i = 0; i < a.length; i++) p = Math.max(p, Math.abs(a[i])); for (let i = 0; i < a.length; i++) a[i] = Math.tanh(a[i] / p * drive) / Math.tanh(drive); }
  const fade = Math.round(SR * .006);
  const env = i => { if (loop) return 1; const k = Math.min(1, i / fade, (a.length - 1 - i) / (fade * 3)); return .5 - .5 * Math.cos(Math.PI * clamp01(k)); };
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
const written = [], skipped = [];
function save(sub, name, a, loop, gain, drive = 0) {
  const file = path.join(ROOT, sub, name + '.wav');
  if (fs.existsSync(file) && !FORCE) { skipped.push(name); return; }
  fs.writeFileSync(file, encode(a, loop, gain, drive)); written.push(name);
}

// ------------------------------------------------------------------ yapı taşları
// Chamberlin durum-değişken süzgeci (fc her örnekte değişebilir). q: sönüm (küçük = rezonanslı)
function svf(q = 0.7) { let lp = 0, bp = 0; return (x, fc) => { const f = 2 * Math.sin(Math.PI * Math.min(fc, SR / 6) / SR); const hp = x - lp - q * bp; bp += f * hp; lp += f * bp; return { lp, bp, hp }; }; }
function onepole(fc) { const a = 1 - Math.exp(-TAU * fc / SR); let y = 0; return x => (y += a * (x - y)); }
// zarf: atak (sn) + üstel sönüş (sn)
const ad = (t, at, dec) => t < 0 ? 0 : (t < at ? t / at : Math.exp(-(t - at) / dec));
// çan: kısmi tonlar (oran, kazanç, sönüş katsayısı)
function bell(a, t0, f, g = 1, dec = 0.8, partials = [[1, 1, 1], [2, 0.35, 0.6], [3, 0.12, 0.4], [4.2, 0.06, 0.25]]) {
  const i0 = Math.round(t0 * SR);
  for (let i = i0; i < a.length; i++) { const t = (i - i0) / SR; let s = 0; for (const [r, pg, pd] of partials) s += Math.sin(TAU * f * r * t) * pg * Math.exp(-t / (dec * pd)); a[i] += s * g * Math.min(1, t / 0.004); }
}
// yumuşak ton (sinüs + biraz 2./3. harmonik), sabit ya da kayan frekans
function tone(a, t0, dur, f0, f1, g = 1, at = 0.006, rel = 0.05, harm = 0.15) {
  const i0 = Math.round(t0 * SR), n = Math.round(dur * SR); let ph = 0;
  for (let k = 0; k < n && i0 + k < a.length; k++) {
    const t = k / SR, f = f0 + (f1 - f0) * (k / n); ph += TAU * f / SR;
    const e = Math.min(1, t / at, (dur - t) / rel);
    a[i0 + k] += (Math.sin(ph) + harm * Math.sin(2 * ph) + harm * 0.4 * Math.sin(3 * ph)) * g * clamp01(e);
  }
}
// tok darbe: frekansı düşen sinüs + alçak geçiren gürültü
function thump(a, t0, f0, f1, g = 1, dec = 0.12, noiseG = 0.5, nfc = 400) {
  const i0 = Math.round(t0 * SR); let ph = 0; const lp = onepole(nfc), lp2 = onepole(nfc);
  for (let i = i0; i < a.length; i++) { const t = (i - i0) / SR; if (t > dec * 8) break; const f = f1 + (f0 - f1) * Math.exp(-t / 0.04); ph += TAU * f / SR;
    a[i] += (Math.sin(ph) * Math.exp(-t / dec) + lp2(lp(noise())) * 4 * noiseG * Math.exp(-t / (dec * 0.4))) * g * Math.min(1, t / 0.002); }
}
// ahşap/metal "tık": rezonanslı band geçiren gürültü patlaması
function knock(a, t0, fc, g = 1, dec = 0.03, q = 0.08) {
  const i0 = Math.round(t0 * SR); const f = svf(q);
  for (let i = i0; i < a.length; i++) { const t = (i - i0) / SR; if (t > dec * 10) break; const ex = t < 0.0015 ? noise() : noise() * 0.05; a[i] += f(ex, fc).bp * g * Math.exp(-t / dec); }
}
// gıcırtı: yapış-kay darbe dizisi (frekans fA→fB) band geçiren süzgeçten
function creak(a, t0, dur, fA, fB, fc, g = 1, q = 0.12) {
  const i0 = Math.round(t0 * SR), n = Math.round(dur * SR); let ph = 0; const f = svf(q), f2 = svf(0.3);
  for (let k = 0; k < n && i0 + k < a.length; k++) { const t = k / SR, x = k / n; const fr = fA + (fB - fA) * x + 6 * Math.sin(TAU * 3.3 * t);
    const prev = ph; ph += fr / SR; const pulse = Math.floor(ph) !== Math.floor(prev) ? 1 : 0;
    const e = Math.sin(Math.PI * clamp01(x)) ** 0.7;
    const y = f(pulse * (0.7 + 0.6 * random()), fc).bp; a[i0 + k] += (y + f2(y, fc * 2.1).bp * 0.4) * g * e; }
}
// band geçiren gürültü hışırtısı (zarf fonksiyonu ile)
function swish(a, t0, dur, fc0, fc1, g, envFn, q = 0.9) {
  const i0 = Math.round(t0 * SR), n = Math.round(dur * SR); const f = svf(q);
  for (let k = 0; k < n && i0 + k < a.length; k++) { const x = k / n; a[i0 + k] += f(noise(), fc0 + (fc1 - fc0) * x).bp * g * envFn(x); }
}
const hump = (p = 1) => x => Math.sin(Math.PI * x) ** p;

// ------------------------------------------------------------------ DEPREM — kitaplık
{ // matkap
  const a = buf(1.35); let ph = 0; const body = svf(0.5), bite = svf(0.35), lp = onepole(2600);
  for (let i = 0; i < a.length; i++) {
    const t = i / SR;
    const spin = t < 0.18 ? t / 0.18 : (t > 1.12 ? Math.max(0, 1 - (t - 1.12) / 0.2) : 1);   // kalkış / duruş
    const load = t > 0.35 && t < 1.05 ? 0.82 + 0.05 * Math.sin(TAU * 7 * t) : 1;              // duvara girerken devir düşer
    const f = (70 + 120 * spin) * load; ph += f / SR;
    const saw = 2 * (ph % 1) - 1;
    let y = body(saw, 900).lp * 0.8 + Math.sin(TAU * ph) * 0.35;
    if (t > 0.35 && t < 1.08) y += bite(noise(), 1700 + 300 * Math.sin(TAU * 11 * t)).bp * 0.5 * Math.min(1, (t - 0.35) / 0.06) * (0.7 + 0.3 * Math.sin(TAU * 23 * t));
    a[i] = lp(y) * (0.25 + 0.75 * spin) * Math.min(1, t / 0.03);
  }
  save('SFX', 'SFX_EQ_Shelf_Drill', a, false, 0.85);
}
{ // sıkma: cırcır + tink
  const a = buf(0.62);
  [0, 0.075, 0.15, 0.225].forEach((t, k) => { knock(a, t, 2300 + k * 120, 0.9, 0.012, 0.06); knock(a, t + 0.004, 900, 0.5, 0.02, 0.15); });
  thump(a, 0.31, 260, 150, 0.35, 0.05, 0.2, 900);
  bell(a, 0.31, 1760, 0.45, 0.35, [[1, 1, 1], [2.76, 0.4, 0.5], [5.4, 0.15, 0.25]]);
  save('SFX', 'SFX_EQ_Shelf_Fasten', a, false, 0.9, 2);
}
{ // sallanma: gıcırtı + tıkırtı
  const a = buf(1.1);
  creak(a, 0.0, 0.5, 38, 55, 620, 1.0); creak(a, 0.45, 0.55, 50, 34, 540, 0.9);
  for (const t of [0.12, 0.2, 0.33, 0.52, 0.61, 0.78, 0.9]) knock(a, t + random() * 0.02, 1200 + random() * 900, 0.35, 0.02, 0.1);
  save('SFX', 'SFX_EQ_Shelf_Wobble', a, false, 0.9, 3);
}
{ // devrilme
  const a = buf(1.9);
  creak(a, 0.0, 0.42, 30, 70, 480, 0.8, 0.15);
  swish(a, 0.25, 0.3, 300, 900, 0.25, hump(2), 1.2);                            // devrilirken hava
  thump(a, 0.52, 95, 42, 1.0, 0.22, 0.9, 260);                                  // ana gümleme
  thump(a, 0.68, 120, 60, 0.45, 0.12, 0.6, 320);                                // sekme
  const clatter = [0.56, 0.6, 0.63, 0.7, 0.74, 0.8, 0.86, 0.95, 1.05, 1.18, 1.3];
  clatter.forEach((t, k) => { knock(a, t, 420 + random() * 500, 0.5 * Math.exp(-k / 6), 0.04, 0.1); thump(a, t + 0.01, 180 + random() * 80, 110, 0.18 * Math.exp(-k / 5), 0.05, 0.4, 700); });
  const lp = onepole(3000); for (let i = 0; i < a.length; i++) a[i] = lp(a[i]);   // tiz yumuşatma
  save('SFX', 'SFX_EQ_Shelf_Crash', a, false, 0.95, 1.5);
}
{ // yaralanma: yumuşak darbe + inen ton
  const a = buf(0.75);
  thump(a, 0.0, 150, 70, 1.0, 0.09, 0.6, 350);
  tone(a, 0.06, 0.55, 300, 200, 0.35, 0.03, 0.25, 0.1);
  const lp = onepole(2200); for (let i = 0; i < a.length; i++) a[i] = lp(a[i]);
  save('SFX', 'SFX_EQ_Injured', a, false, 0.85, 1.2);
}
// ------------------------------------------------------------------ DEPREM — koruma / keşif / arayüz
{ const a = buf(0.26); tone(a, 0, 0.09, 784, 784, 0.8, 0.004, 0.04); tone(a, 0.08, 0.17, 1047, 1047, 0.8, 0.004, 0.12); save('SFX', 'SFX_EQ_Protect_Step', a, false, 0.7); }
{ const a = buf(0.7);
  for (const t0 of [0, 0.3]) { tone(a, t0, 0.13, 880, 880, 0.7, 0.005, 0.05, 0.35); tone(a, t0 + 0.12, 0.16, 659, 659, 0.7, 0.005, 0.08, 0.35); }
  const lp = onepole(3500); for (let i = 0; i < a.length; i++) a[i] = lp(a[i]);
  save('SFX', 'SFX_EQ_Protect_Release', a, false, 0.75); }
{ const a = buf(0.38);
  swish(a, 0, 0.26, 2500, 5200, 0.9, x => x < 0.3 ? x / 0.3 : Math.exp(-(x - 0.3) * 5), 1.1);
  knock(a, 0.2, 3200, 0.4, 0.008, 0.2);
  save('SFX', 'SFX_EQ_Inspect', a, false, 0.8, 1.5); }
{ const a = buf(0.48);
  for (const t0 of [0, 0.2]) tone(a, t0, 0.13, 740, 740, 0.8, 0.005, 0.06, 0.45);
  save('SFX', 'SFX_EQ_Hazard_Warn', a, false, 0.7); }
{ const a = buf(0.62);
  knock(a, 0, 1500, 0.8, 0.015, 0.1); thump(a, 0.0, 220, 140, 0.35, 0.04, 0.3, 900);   // kol basıldı
  [0.09, 0.16, 0.22, 0.3, 0.37].forEach((t, k) => { knock(a, t, 1900 + (k % 2) * 400, 0.75, 0.018, 0.05); thump(a, t, 180, 120, 0.3, 0.03, 0.3, 700); });
  save('SFX', 'SFX_EQ_Door_Locked', a, false, 0.9, 2.5); }
{ const a = buf(1.0);
  knock(a, 0, 1700, 0.8, 0.012, 0.08); knock(a, 0.05, 1100, 0.5, 0.02, 0.1);           // mandal
  creak(a, 0.12, 0.45, 90, 150, 950, 0.45, 0.1);
  swish(a, 0.15, 0.7, 400, 250, 0.35, hump(1.5), 1.4);                                   // hava
  save('SFX', 'SFX_EQ_Door_Open', a, false, 0.9, 2.5); }
{ const a = buf(1.0);
  { const i0 = 0, n = Math.round(0.42 * SR); let ph = 0; const f = svf(0.25);               // fermuar: hızlanan diş tıkırtısı
    for (let k = 0; k < n; k++) { const x = k / n; const prev = ph; ph += (90 + 160 * x) / SR; const p = Math.floor(ph) !== Math.floor(prev) ? 1 : 0;
      a[i0 + k] += (f(p + noise() * 0.06, 3200 + 900 * x).bp) * 0.9 * Math.sin(Math.PI * clamp01(x * 1.05)) ** 0.4; } }
  swish(a, 0.45, 0.4, 700, 1300, 0.6, hump(1.5), 1.3);                                   // askı/kumaş
  knock(a, 0.86, 2600, 0.8, 0.01, 0.07); knock(a, 0.865, 1300, 0.4, 0.02, 0.12);         // toka
  save('SFX', 'SFX_EQ_Bag_Wear', a, false, 0.9, 3); }
{ const a = buf(0.36); tone(a, 0, 0.16, 520, 880, 0.8, 0.004, 0.05, 0.1); bell(a, 0.12, 1320, 0.25, 0.25); save('SFX', 'SFX_EQ_Question_Open', a, false, 0.7); }
{ const a = buf(1.9); [523.25, 659.25, 783.99, 1046.5].forEach((f, k) => bell(a, k * 0.13, f, 0.55, 0.9)); save('SFX', 'SFX_EQ_Assembly_Arrive', a, false, 0.75); }

// ------------------------------------------------------------------ AKŞAM SEL
{ // dal itme döngüsü
  const S = 1.6, a = buf(S + 0.3); const leaf = svf(1.0), lp = onepole(1800);
  for (let i = 0; i < a.length; i++) { const t = i / SR; const m = 0.6 + 0.25 * Math.sin(TAU * t / S * 2) + 0.15 * Math.sin(TAU * t / S * 5 + 1);
    a[i] += lp(leaf(noise(), 2600 + 900 * Math.sin(TAU * t / S * 3)).bp) * 0.9 * m; }
  for (let k = 0; k < 26; k++) knock(a, random() * S, 1800 + random() * 2500, 0.25, 0.01, 0.15);   // yaprak çıtırtıları
  creak(a, 0.1, 0.7, 28, 40, 380, 0.45, 0.2); creak(a, 0.85, 0.75, 36, 26, 340, 0.4, 0.2);         // dal sürtünmesi
  save('SFX', 'SFX_FE_Branch_Push', seamless(a, S), true, 0.8, 1.5);
}
{ const a = buf(1.1);
  swish(a, 0, 0.45, 1400, 3200, 0.7, hump(1.2), 1.0);
  thump(a, 0.32, 170, 95, 0.5, 0.08, 0.5, 600);
  tone(a, 0.45, 0.14, 659, 659, 0.45, 0.005, 0.06); tone(a, 0.57, 0.3, 988, 988, 0.45, 0.005, 0.2);
  save('SFX', 'SFX_FE_Passage_Open', a, false, 0.75); }
{ const a = buf(2.0); [392, 493.88, 587.33].forEach((f, k) => bell(a, k * 0.09, f, 0.5, 1.2)); bell(a, 0.35, 783.99, 0.35, 1.0);
  save('SFX', 'SFX_FE_SafeZone', a, false, 0.75); }
{ // sürüklenme sıçraması
  const a = buf(1.0); const f = svf(0.7), lp = onepole(4500);
  for (let i = 0; i < a.length; i++) { const t = i / SR; const e = ad(t, 0.015, 0.16) + 0.3 * ad(t - 0.12, 0.02, 0.25); a[i] += lp(f(noise(), 900 + 1800 * Math.exp(-t / 0.15)).bp) * e; }
  thump(a, 0.0, 120, 60, 0.6, 0.12, 0.5, 300);
  for (let k = 0; k < 14; k++) { const t0 = 0.08 + random() * 0.7, f0 = 500 + random() * 700; tone(a, t0, 0.04 + random() * 0.04, f0, f0 * 1.9, 0.18 * (1 - t0), 0.003, 0.02, 0); }   // kabarcık
  save('SFX', 'SFX_FE_Sweep_Splash', a, false, 0.9, 1.5);
}
{ // akıntı döngüsü
  const S = 3, a = buf(S + 0.5); const lo = svf(0.6), mid = svf(0.9), lp = onepole(2500);
  for (let i = 0; i < a.length; i++) { const t = i / SR; const n = noise();
    a[i] += lo(n, 220 + 60 * Math.sin(TAU * t / S * 2)).bp * 1.6 + lp(mid(n, 900 + 400 * Math.sin(TAU * t / S * 3 + 0.7)).bp) * 0.7 * (0.75 + 0.25 * Math.sin(TAU * t / S * 4)); }
  for (let k = 0; k < 40; k++) { const t0 = random() * S, f0 = 300 + random() * 500; tone(a, t0, 0.03 + random() * 0.05, f0, f0 * 1.7, 0.12, 0.004, 0.02, 0); }
  save('SFX', 'SFX_FE_Sweep_Current', seamless(a, S), true, 0.8);
}
{ // ağaç çatırdaması
  const a = buf(0.95);
  creak(a, 0, 0.75, 18, 60, 420, 1.0, 0.1);
  [0.18, 0.37, 0.5, 0.6, 0.66].forEach((t, k) => { knock(a, t, 900 + random() * 700, 0.55 + k * 0.05, 0.02, 0.12); thump(a, t, 200, 120, 0.25, 0.04, 0.4, 800); });
  const lp = onepole(3200); for (let i = 0; i < a.length; i++) a[i] = lp(a[i]);
  save('SFX', 'SFX_FE_Tree_Crack', a, false, 0.9, 2.5); }
{ // ağaç devrilmesi
  const a = buf(2.3);
  swish(a, 0, 0.95, 1200, 3200, 0.55, x => x ** 1.6, 1.0);                         // yapraklar savrulur (giderek artar)
  thump(a, 0.9, 85, 40, 1.0, 0.25, 0.9, 240);                                       // gövde yere/suya
  { const f = svf(0.7); for (let i = Math.round(0.9 * SR); i < a.length; i++) { const t = i / SR - 0.9; a[i] += f(noise(), 700 + 1500 * Math.exp(-t / 0.2)).bp * 0.5 * ad(t, 0.02, 0.3); } }   // su sıçraması
  swish(a, 1.0, 1.2, 2400, 1400, 0.3, x => Math.exp(-x * 3), 1.0);                  // yaprak hışırtısı söner
  const lp = onepole(3500); for (let i = 0; i < a.length; i++) a[i] = lp(a[i]);
  save('SFX', 'SFX_FE_Tree_Fall', a, false, 0.95, 1.5); }

// ------------------------------------------------------------------ ARAYÜZ
{ const a = buf(0.24); tone(a, 0, 0.08, 659, 659, 0.8, 0.003, 0.03, 0.2); tone(a, 0.07, 0.16, 880, 880, 0.8, 0.003, 0.11, 0.2); save('SFX', 'SFX_UI_Confirm', a, false, 0.75); }
{ const a = buf(0.06); tone(a, 0, 0.055, 1180, 1000, 0.8, 0.002, 0.045, 0.05); save('SFX', 'SFX_UI_Dialogue_Next', a, false, 0.5); }

// ------------------------------------------------------------------ AMBİYANS: toplanma alanı (açık hava)
{
  const S = 20, a = buf(S + 1.5);
  const voices = [];
  for (let v = 0; v < 9; v++) voices.push({ f1: svf(0.5), f2: svf(0.6), c1: 380 + random() * 350, c2: 1100 + random() * 700, rate: 2.5 + random() * 2.5, ph: random() * 10, g: 0.5 + random() * 0.5, gate: random() * 10 });
  const wind = onepole(300), wind2 = onepole(300), lpAll = onepole(2400);
  for (let i = 0; i < a.length; i++) {
    const t = i / SR; let s = 0;
    for (const v of voices) {                               // her "ses": hece hızında açılıp kapanan biçimlendirici gürültü + konuşma/sessizlik kapısı
      const syl = Math.max(0, Math.sin(TAU * v.rate * t + v.ph + 0.8 * Math.sin(TAU * 0.7 * t + v.ph))) ** 1.5;
      const talk = clamp01(0.5 + 1.5 * Math.sin(TAU * t / (6 + v.gate) + v.gate));
      const n = noise(); s += (v.f1(n, v.c1 + 120 * Math.sin(TAU * 1.3 * t + v.ph)).bp + 0.5 * v.f2(n, v.c2).bp) * syl * talk * v.g;
    }
    a[i] = lpAll(s) * 0.5 + wind2(wind(noise())) * 6 * (0.6 + 0.4 * Math.sin(TAU * t / S * 3));
  }
  // kuşlar: seyrek cıvıltı öbekleri (2–3 notalı yukarı/aşağı süzülmeler)
  for (let k = 0; k < 16; k++) {
    let t0 = random() * (S - 0.6); const base = 2600 + random() * 1600, notes = 2 + Math.floor(random() * 3);
    for (let j = 0; j < notes; j++) { const up = random() < 0.6; const d = 0.05 + random() * 0.05; tone(a, t0, d, up ? base : base * 1.3, up ? base * 1.35 : base * 0.95, 0.09, 0.006, 0.025, 0.05); t0 += d + 0.03 + random() * 0.05; }
  }
  save('Ambience', 'AMB_EQ_Assembly_Outdoor_Loop', seamless(a, S), true, 0.7, 1.2);
}

console.log('yazıldı:', written.length ? written.join(', ') : '-');
if (skipped.length) console.log('zaten vardı (atlandı):', skipped.join(', '));
