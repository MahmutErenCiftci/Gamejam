/* Original procedural audio. License: project owner. Node.js standard library only.
 * Run: node tools/audio/generate_audio.js [--check]
 * Fixed PRNG seed; no samples, downloads, packages or Unity metadata generation.
 */
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const ROOT = path.resolve(__dirname, '../../Assets/Resources/Audio');
const SR = 44100, TAU = Math.PI * 2, PEAK = Math.floor(32767 * 10 ** (-1 / 20));
let seed = 473821;
function random() { seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5; return (seed >>> 0) / 4294967296; }
const noise = () => random() * 2 - 1;
const hz = midi => 440 * 2 ** ((midi - 69) / 12);
const buf = seconds => new Float64Array(Math.round(seconds * SR));
function add(out, clip, start, gain = 1, wrap = false) {
  const offset = Math.round(start * SR);
  for (let i = 0; i < clip.length; i++) {
    const j = offset + i;
    if (wrap) out[j % out.length] += clip[i] * gain;
    else if (j < out.length) out[j] += clip[i] * gain;
  }
}
function note(freq, seconds, color = 'bell') {
  const a = buf(seconds);
  for (let i = 0; i < a.length; i++) {
    const t = i / SR, p = TAU * freq * t;
    const attack = Math.min(1, t / 0.012), release = Math.min(1, (seconds - t) / 0.07);
    const wave = color === 'soft' ? Math.sin(p) + .12 * Math.sin(p * 2) : Math.sin(p) + .21 * Math.sin(p * 3) + .07 * Math.sin(p * 5);
    a[i] = wave * attack * release * Math.exp(-t * (color === 'soft' ? 1.4 : 5));
  }
  return a;
}
function texture(seconds, cutoff = 900) {
  const a = buf(seconds), alpha = 1 - Math.exp(-TAU * cutoff / SR);
  let low = 0;
  for (let i = 0; i < a.length; i++) { low += alpha * (noise() - low); a[i] = low; }
  return a;
}
function impact(seconds, freq, wet = false) {
  const a = texture(seconds, wet ? 2400 : 750);
  for (let i = 0; i < a.length; i++) {
    const t = i / SR;
    a[i] = (.58 * a[i] + .3 * Math.sin(TAU * freq * t) * Math.exp(-t * 35)) * Math.exp(-t * (wet ? 15 : 22));
  }
  return a;
}
function melody(seconds, notes, spacing = .17) {
  const a = buf(seconds);
  notes.forEach((m, i) => add(a, note(hz(m), Math.min(.7, seconds - i * spacing)), i * spacing));
  return a;
}
const assets = [];
function asset(folder, name, a, loop, usage) { assets.push({ folder, name, a, loop, usage }); }
const sfx = (name, a, usage, loop = false) => asset('SFX', 'SFX_' + name, a, loop, usage);

let a = buf(2);
for (const t of [0, .24, 1, 1.24]) add(a, note(880, .15), t);
sfx('Alarm_Clock', a, '`F01_WAKE` — `AlarmClock.alarmAudio` (loop otomatik).', true);
a = buf(3);
for (const t of [0, .28, .56, 1.5, 1.78, 2.06]) add(a, melody(.25, [76, 81], .1), t, .65);
for (let i = 0; i < a.length; i++) a[i] += .08 * Math.sin(TAU * 90 * i / SR) * (.5 + .5 * Math.sin(TAU * 7 * i / SR));
sfx('Phone_Ring', a, '`EQH_WAKE` — `PhoneAlertController.ringAudio`.', true);
sfx('Phone_Notify', melody(.7, [79, 84], .13), '`EQH_WAKE` — `PhoneAlertController.onNotification` → AudioSource.Play.');
sfx('TV_NewsJingle', melody(3.5, [60, 64, 67, 72, 67, 74, 72], .34), '`F03_TV_JINGLE` — `FloodChapterController.tvJingleAudio` veya `onTvJingle`; ikisine birden bağlamayın.');
sfx('Water_Splash', impact(.65, 180, true), '`F05_PUDDLE_WARNING` — `PuddleHazard.OnTriggered`.');
function bus(seconds, departing) {
  const a = texture(seconds, 360);
  let phase = 0;
  for (let i = 0; i < a.length; i++) {
    const t = i / SR, k = t / seconds, f = departing ? 48 + 32 * k : 82 - 34 * k;
    phase += TAU * f / SR;
    const env = Math.sin(Math.PI * k) ** .6;
    a[i] = (.4 * a[i] + .3 * Math.sin(phase) + .1 * Math.sin(phase * 2)) * env;
  }
  if (!departing) add(a, texture(.8, 1700), seconds - .85, .17);
  return a;
}
sfx('Bus_Arrive', bus(4, false), '`F05_BUS_ARRIVE` — `BusController.OnArriving`.');
a = texture(1.25, 2200);
for (let i = 0; i < a.length; i++) a[i] *= Math.sin(Math.PI * i / a.length) ** 2;
add(a, impact(.2, 150), 1.02, .45);
sfx('Bus_Door', a, '`F05_BOARD` — `BusController.OnDoorsOpened` / `OnDoorsClosed`.');
sfx('Bus_Depart', bus(4, true), '`F05_BOARD` — `BusController.OnDeparting`.');
a = texture(8, 125);
for (let i = 0; i < a.length; i++) { const t = i / SR; a[i] = (a[i] + .12 * Math.sin(TAU * 47 * t) + .05 * Math.sin(TAU * 73 * t)) * Math.sin(Math.PI * t / 8) ** 1.5; }
sfx('Quake_Rumble', a, '`EQH_QUAKE` — `EarthquakeEventController.rumbleAudio`; controller loop=true yapar, 8 sn sonunda durdurur.');
a = buf(2.4);
for (let i = 0; i < 17; i++) add(a, impact(.16, 260 + random() * 250), i * .13 + random() * .025, .3 + random() * .3);
sfx('Quake_ObjectsRattle', a, '`EQH_QUAKE` — `EarthquakeEventController.onRumbleStart`; tek sefer, düşük seviye.');
a = buf(1.2);
for (const t of [0, .21, .48, .64]) add(a, impact(.4, 100 + random() * 40), t);
sfx('Books_Fall', a, '`EQH_QUAKE` — `onRumbleStart` için ek AudioSource; nesne düşme zamanlamasını sahip bağlar.');
a = buf(.8);
for (const t of [0, .07, .16]) add(a, note(1200 + random() * 800, .4), t, .25);
add(a, impact(.4, 450, true), 0, .3);
sfx('Glass_Crack', a, '`EQH_QUAKE` — `onRumbleStart` için ek AudioSource; yumuşak stilize çatlama.');
sfx('Countdown_Tick', note(660, .085, 'soft'), '`Prep` son 10 sn — `CountdownView.onTick`; ikinci sayaç tik kancasını aynı anda bağlamayın.');
sfx('Penalty', melody(.6, [62, 59], .18), '`Q1–Q4` — `EmergencyCountdownManager.OnPenalty` (C# adapter gerekir); yanlış sesle üst üste çalmayın.');
sfx('Correct', melody(.65, [72, 76, 79], .14), '`Q1–Q4` — `TimedDecisionController.onCorrect`; Sel F_D1/F_D2 için karar-sonuç adapter gerekir.');
sfx('Wrong', melody(.65, [67, 65], .19), '`Q1–Q4` — `TimedDecisionController.onWrong`; nazik yeniden deneme işareti.');
sfx('Objective_Complete', melody(.8, [76, 79, 84], .15), 'Yeni ObjectiveHudView.onObjectiveCompleted(int) planlanan kanca; bu dev tabanında henüz yok, HUD PR sonrası bağlanır.');
sfx('Objective_Bonus', melody(.9, [79, 84, 88, 91], .12), 'Yeni ObjectiveHudView.onBonusCompleted planlanan kanca; HUD PR sonrası bağlanır.');
sfx('Badge', melody(1.6, [72, 76, 79, 84, 88], .18), '`Report` / F09_RESULT — rozet gösteriminde tek sefer; hazır ses eventi yok, sahip adapter ekler.');
sfx('UI_Hover', note(740, .075, 'soft'), 'Menü / `EqButton.HoverChanged(bool)` true olduğunda; C# adapter gerekir.');
sfx('UI_Click', melody(.12, [76, 79], .045), 'Menü / `EqButton.Clicked`, Unity Button.onClick; birini bağlayın.');
sfx('UI_Back', melody(.25, [72, 67], .08), 'Menüye dönüş butonu / Button.onClick; GameSceneRouter çağrısından önce.');
for (let i = 1; i <= 3; i++) {
  sfx('Footstep_Indoor_0' + i, impact(.27, 115 + i * 13), 'F01_WAKE/F02_WASH ve EQH iç mekân yürüyüşü; ayak basma/animasyon adapter gerekir, hazır ses kancası yok.');
  sfx('Footstep_Wet_0' + i, impact(.32, 140 + i * 19, true), 'F05_STREET güvenli kaldırım yürüyüşü; ayak basma adapter gerekir; suya girme ödülü olarak kullanılmaz.');
}
// Crossfade a continuation over the beginning. Result retains exact requested duration.
function seamless(raw, seconds) {
  const n = Math.round(seconds * SR), c = raw.length - n;
  const out = raw.slice(0, n);
  for (let i = 0; i < c; i++) { const k = .5 - .5 * Math.cos(Math.PI * i / (c - 1)); out[i] = raw[n + i] * (1 - k) + raw[i] * k; }
  return out;
}
for (const heavy of [false, true]) {
  a = texture(20.2, heavy ? 3200 : 2200);
  for (let i = 0; i < a.length; i++) a[i] *= .7 + .12 * Math.sin(TAU * .19 * i / SR) + .07 * Math.sin(TAU * .37 * i / SR);
  for (let i = 0; i < (heavy ? 130 : 65); i++) add(a, impact(.09, 650 + random() * 900, true), random() * 20, heavy ? .025 : .05);
  asset('Ambience', 'AMB_Rain_' + (heavy ? 'Heavy' : 'Light') + '_Loop', seamless(a, 20), true,
    heavy ? '`F05_DOWNPOUR` — RainController.OnDownpourStarted; ikinci loop kaynağına geçiş/crossfade sahip tarafından bağlanır.' : '`F05_EXIT`/F05_STREET — RainController.rainAudio (loop otomatik).');
}
a = texture(20.2, 160);
for (let i = 0; i < a.length; i++) a[i] *= .25;
for (const start of [1.8, 2.2, 5.6, 8.7, 9, 14.3, 17.4]) {
  const bird = buf(.22); let phase = 0;
  for (let i = 0; i < bird.length; i++) { const t = i / SR; phase += TAU * (1700 + 650 * Math.sin(Math.PI * t / .22)) / SR; bird[i] = .10 * Math.sin(phase) * Math.sin(Math.PI * t / .22) ** 2; }
  add(a, bird, start);
}
asset('Ambience', 'AMB_Room_Morning_Loop', seamless(a, 20), true, '`F01_WAKE`/F02_WASH ve `EQH_WAKE` — oda AudioSource; oda değişiminde stop/crossfade adapter gerekir.');
function music(kind) {
  // 16 bars, 4/4 at 100 BPM = 38.4 s. Sustains wrap across the loop boundary.
  const seconds = 38.4, beat = .6, a = buf(seconds);
  const chords = kind === 'tension' ? [[57,60,64],[53,57,60],[55,59,62],[52,57,59]] : [[60,64,67],[57,60,64],[53,57,60],[55,59,62]];
  for (let bar = 0; bar < 16; bar++) {
    const c = chords[Math.floor(bar / 2) % 4], start = bar * beat * 4;
    for (const m of c) add(a, note(hz(m - 12), 3.1, 'soft'), start, .22, true);
    const pattern = kind === 'tension' ? [0,1,0,2,0,1,2,1] : [0,1,2,1,0,2,1,2];
    for (let s = 0; s < 8; s++) add(a, note(hz(c[pattern[s]] + (kind === 'calm' ? 0 : 12)), .6), start + s * beat / 2, kind === 'tension' ? .12 : .17, true);
    if (kind === 'menu' && bar % 2 === 0) for (let s = 0; s < 4; s++) add(a, note(hz(c[[2,1,0,2][s]] + 12), .9, 'soft'), start + s * beat, .22, true);
  }
  return a;
}
asset('Music', 'MUS_Menu_Loop', music('menu'), true, 'MainMenu intro sonrası; yeni AudioSource loop=true; menü sahibi Berk bağlar.');
asset('Music', 'MUS_Calm_Loop', music('calm'), true, 'F01_WAKE–F04_PREPARE / EQH_AFTER_MOTHER–EQH_ASSEMBLY; faz/oda adapter gerekir.');
asset('Music', 'MUS_Tension_Loop', music('tension'), true, '`EarthquakeModeController.Phase == Prep` (120 sn eğitim sayacı); 38.4 sn loop, faz adapter gerekir. Quake başlayınca azalt/durdur.');

function encode(asset) {
  const a = asset.a;
  const fade = Math.round(SR * (asset.loop ? .001 : .008));
  const envelope = i => { const k = Math.min(1, i / fade, (a.length - 1 - i) / fade); return .5 - .5 * Math.cos(Math.PI * k); };
  let mean = 0, weight = 0;
  for (let i = 0; i < a.length; i++) { const w = envelope(i); mean += a[i] * w; weight += w; }
  mean /= weight;
  let peak = 0;
  for (let i = 0; i < a.length; i++) {
    // Zero endpoints and smooth slopes. Loop seam dip is only 1 ms per side.
    a[i] = (a[i] - mean) * envelope(i);
    peak = Math.max(peak, Math.abs(a[i]));
  }
  const wav = Buffer.alloc(44 + a.length * 2);
  wav.write('RIFF'); wav.writeUInt32LE(wav.length - 8, 4); wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(SR, 24); wav.writeUInt32LE(SR * 2, 28); wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34);
  wav.write('data', 36); wav.writeUInt32LE(a.length * 2, 40);
  for (let i = 0; i < a.length; i++) wav.writeInt16LE(Math.round(a[i] / peak * PEAK), 44 + i * 2);
  return wav;
}
function inspect(wav, asset) {
  if (wav.toString('ascii', 0, 4) !== 'RIFF' || wav.readUInt32LE(24) !== SR || wav.readUInt16LE(34) !== 16 || wav.readUInt16LE(22) !== 1) throw Error('Invalid format: ' + asset.name);
  let peak = 0, square = 0, dc = 0;
  for (let i = 44; i < wav.length; i += 2) { const x = wav.readInt16LE(i); peak = Math.max(peak, Math.abs(x)); square += x * x; dc += x; }
  const n = (wav.length - 44) / 2;
  if (peak !== PEAK || wav.readInt16LE(44) !== 0 || wav.readInt16LE(wav.length - 2) !== 0 || n !== asset.a.length) throw Error('Peak/duration/seam failed: ' + asset.name);
  if (Math.abs(dc / n / 32767) > 0.00001) throw Error('DC offset failed: ' + asset.name);
  return { seconds: n / SR, peak: 20 * Math.log10(peak / 32767), rms: 20 * Math.log10(Math.sqrt(square / n) / 32767), dc: dc / n / 32767 };
}
const checking = process.argv.includes('--check');
let total = 0;
const rows = [];
for (const item of assets) {
  const file = path.join(ROOT, item.folder, item.name + '.wav'), generated = encode(item);
  if (!checking) { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, generated); }
  const stored = fs.readFileSync(file), info = inspect(stored, item);
  if (!stored.equals(generated)) throw Error('Reproducibility failed: ' + item.name);
  total += stored.length;
  rows.push(`| ${item.folder}/${item.name}.wav | ${info.seconds.toFixed(3)} | ${item.loop ? 'Evet' : 'Hayır'} | ${info.rms.toFixed(1)} | ${item.usage} |`);
  console.log(`${item.name}: ${info.seconds.toFixed(3)}s peak=${info.peak.toFixed(3)}dBFS RMS=${info.rms.toFixed(1)}dBFS DC=${info.dc.toFixed(6)} sha256=${crypto.createHash('sha256').update(stored).digest('hex').slice(0,16)}`);
}
if (assets.length !== 34 || total >= 40_000_000) throw Error('Count/size requirement failed');
// AUDIO_LIST.md is maintained separately; generation never overwrites integration notes or CC0 overrides.
console.log(`PASS: ${assets.length} files, ${total} bytes, no clipping, zero endpoints, deterministic content.`);
