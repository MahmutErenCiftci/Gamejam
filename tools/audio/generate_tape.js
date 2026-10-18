// Bant (koli bandı) çekme + yapıştırma sesi: SFX_EQ_Tape.wav (kitaplık mini oyunu, "Dekoratif bant").
// Yapışkan bant açılırken stick-slip çıtırtısı (hızlı genlik titreşimi) + bant kopma "rip" + avuçla bastırma.
const fs = require('fs'), path = require('path');
const SR = 44100, dur = 1.15, n = Math.floor(SR * dur), out = new Float32Array(n);
let seed = 7; const rnd = () => ((seed = (seed * 1664525 + 1013904223) >>> 0) / 4294967296) * 2 - 1;
// basit iki kutuplu bant geçiren
function bp(x, f, q) { const w = 2 * Math.PI * f / SR, a = Math.sin(w) / (2 * q), c = Math.cos(w);
  const b0 = a, b2 = -a, a0 = 1 + a, a1 = -2 * c, a2 = 1 - a; let x1 = 0, x2 = 0, y1 = 0, y2 = 0;
  return x.map(v => { const y = (b0 * v + b2 * x2 - a1 * y1 - a2 * y2) / a0; x2 = x1; x1 = v; y2 = y1; y1 = y; return y; }); }
// 1) çekme: 0.05–0.75 sn, stick-slip çıtırtı (yükselen hız)
const pull = new Float32Array(n);
let phase = 0;
for (let i = 0; i < n; i++) { const t = i / SR; if (t < 0.05 || t > 0.78) continue;
  const p = (t - 0.05) / 0.73, rate = 90 + 260 * p;          // çıtırtı sıklığı artar
  phase += rate / SR; const tick = Math.pow(Math.max(0, Math.sin(phase * 2 * Math.PI)), 12);
  const env = Math.min(1, (t - 0.05) / 0.04) * (t > 0.72 ? Math.max(0, (0.78 - t) / 0.06) : 1);
  pull[i] = rnd() * (0.25 + 0.75 * tick) * env; }
const pullF = bp(Array.from(pull), 3200, 0.9);
// 2) kopma: 0.78 sn kısa geniş bant patlama
const rip = new Float32Array(n);
for (let i = 0; i < n; i++) { const t = i / SR - 0.78; if (t < 0 || t > 0.06) continue; rip[i] = rnd() * Math.exp(-t * 70); }
const ripF = bp(Array.from(rip), 2400, 0.7);
// 3) bastırma: 0.9 sn yumuşak avuç vuruşu + kısa hışırtı
const press = new Float32Array(n);
for (let i = 0; i < n; i++) { const t = i / SR - 0.9; if (t < 0 || t > 0.2) continue;
  press[i] = (Math.sin(2 * Math.PI * 140 * t) * 0.6 + rnd() * 0.4) * Math.exp(-t * 28); }
const pressF = bp(Array.from(press), 600, 0.8);
for (let i = 0; i < n; i++) out[i] = pullF[i] * 1.1 + ripF[i] * 1.4 + pressF[i] * 0.9;
let peak = 0; for (const v of out) peak = Math.max(peak, Math.abs(v));
const g = 0.7 / peak; const buf = Buffer.alloc(44 + n * 2);
buf.write('RIFF', 0); buf.writeUInt32LE(36 + n * 2, 4); buf.write('WAVEfmt ', 8); buf.writeUInt32LE(16, 16);
buf.writeUInt16LE(1, 20); buf.writeUInt16LE(1, 22); buf.writeUInt32LE(SR, 24); buf.writeUInt32LE(SR * 2, 28);
buf.writeUInt16LE(2, 32); buf.writeUInt16LE(16, 34); buf.write('data', 36); buf.writeUInt32LE(n * 2, 40);
let rms = 0; for (let i = 0; i < n; i++) { const s = Math.max(-1, Math.min(1, out[i] * g)); rms += s * s; buf.writeInt16LE(Math.round(s * 32767), 44 + i * 2); }
const file = path.join(__dirname, '../../Assets/Resources/Audio/SFX/SFX_EQ_Tape.wav');
if (fs.existsSync(file) && !process.argv.includes('--force')) { console.log('var, atlandı (--force)'); process.exit(0); }
fs.writeFileSync(file, buf);
console.log(`SFX_EQ_Tape.wav ${dur}s peak 0.70 rms ${(10 * Math.log10(rms / n)).toFixed(1)} dB`);
