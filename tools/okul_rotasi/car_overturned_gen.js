// SEL OKUL ROTASI: akıntıya kapılıp dereye düşmüş, TERS DÖNMÜŞ araba (köprü kararı için görsel uyarı).
// Aynı kırmızı araba modeli/paleti (route_gen.js "stuck car"), tavanı suyun altında, tekerlekleri yukarıda; su çizgisinin
// altı koyu (batık), çevresinde köpük halkası, akıntı yönünde (ekranın sol altı) iz çizgileri. 3 kare köpük animasyonu.
// Dik araba sprite'larına (PROP_Route_StuckCar_*) dokunmaz. Run: node car_overturned_gen.js [preview]
const L = require('./lib.js');
const { E, PROJ, fl, mod, H, ramp, hash, scr } = L;
const { V, box, rbox, U, INT } = E;
const OUT = process.env.ROUTE_OUT || (PROJ + '/Assets/Art/Props/SchoolRoute');
const OL = '#14141F';
const W = { d1: '#6E6752', d2: '#5E5844', d3: '#4F4A39', d4: '#423D2F', hi: '#A39D82', foam: '#C4BDA0', foamD: '#9C9579', edge: '#2B2820' };
const body = '#B83A2E', bodyL = '#D2503F', bodyD = '#8E2A20', bodyDD = '#6E2018';
const WL = 20;                       // su çizgisi (z)
const YAW = 28 * Math.PI / 180;     // akıntıyla açılı durmuş
const PITCH = 5 * Math.PI / 180;    // burnu suya gömülmüş (ön = +u yerel)
const ROLL = 7 * Math.PI / 180;     // bir yana yatık
const ZC = 14;

// dünya noktası -> araba yerel noktası (önce yaw, sonra pitch/roll ZC etrafında)
function toLocal(p) {
  const c = Math.cos(YAW), s = Math.sin(YAW);
  let u = c * p.u + s * p.v, v = -s * p.u + c * p.v, z = p.z - ZC;
  const cp = Math.cos(PITCH), sp = Math.sin(PITCH);
  [u, z] = [cp * u - sp * z, sp * u + cp * z];
  const cr = Math.cos(ROLL), sr = Math.sin(ROLL);
  [v, z] = [cr * v - sr * z, sr * v + cr * z];
  return V(u, v, z + ZC);
}
const X = f => p => f(toLocal(p));
// v eksenli silindir (tekerlek)
const cylV = (cu, cz, r, v0, v1) => p => { const d = Math.hypot(p.u - cu, p.z - cz) - r, dv = Math.abs(p.v - (v0 + v1) / 2) - (v1 - v0) / 2; return Math.min(Math.max(d, dv), 0) + Math.hypot(Math.max(d, 0), Math.max(dv, 0)); };
// u eksenli silindir (egzoz)
const cylU = (cv, cz, r, u0, u1) => p => { const d = Math.hypot(p.v - cv, p.z - cz) - r, du = Math.abs(p.u - (u0 + u1) / 2) - (u1 - u0) / 2; return Math.min(Math.max(d, du), 0) + Math.hypot(Math.max(d, 0), Math.max(du, 0)); };

const paint = (p, n) => ramp(n, [bodyL, body, bodyD, bodyDD]);
const chassis = (p, n, c) => {
  const q = toLocal(p);
  if (c.face === 'top') { // aracın altı (şimdi yukarı bakıyor): koyu şasi, çamur lekeleri, yan boyunca kırmızı kenar
    if (Math.abs(q.v) > 10.5 || Math.abs(q.u) > 24) return bodyD;
    if (hash(fl(q.u / 3), fl(q.v / 2), 5) < 0.12) return '#5A4A34';
    return mod(fl(q.u / 6), 2) ? '#3C3A40' : '#35333A';
  }
  return paint(p, n);
};
const glassM = (p, n, c) => { const q = toLocal(p); if (c.face === 'top') return '#62778A'; const s = mod(fl(q.u * 0.7 - q.z), 14); return s < 2 ? '#9DB0BF' : ramp(n, ['#7F93A3', '#62778A', '#4C5F70', '#3C4C5A']); };
const cabinM = (p, n, c) => { const q = toLocal(p); return (q.z > 2 && q.z < 11 && Math.abs(q.u - 12) > 1.5 && c.face !== 'top') ? glassM(p, n, c) : paint(p, n); };
const tire = (p, n, c) => { const q = toLocal(p); const hub = Math.min(...[[-16, 1], [16, 1]].map(([cu]) => Math.hypot(q.u - cu, q.z - 32))); if (c.face !== 'top' && hub < 2.6) return hub < 1.2 ? '#5F6977' : '#9AA4B0'; return ramp(n, ['#4A4A52', '#34343A', '#26262C', '#1C1C22']); };

const parts = [
  { sdf: X(rbox(-26, 26, -12.5, 12.5, 12, 30, 3)), mat: chassis },                    // ters gövde
  { sdf: X(rbox(-14, 13, -11, 11, -6, 14, 3)), mat: cabinM },                         // ters kabin (suyun altında)
  ...[-16, 16].flatMap(cu => [-10.5, 10.5].map(cv => ({ sdf: X(cylV(cu, 32, 6.5, cv - 2.6, cv + 2.6)), mat: tire }))),
  ...[-16, 16].map(cu => ({ sdf: X(box(cu - 1, cu + 1, -8, 8, 30, 32)), mat: () => '#26262C' })),           // akslar
  { sdf: X(cylU(-6, 31, 1.2, -27, 8)), mat: (p, n) => ramp(n, ['#B9C0C8', '#8E96A0', '#6A727C', '#4E555E']) }, // egzoz
  ...[-10, 10].map(cv => ({ sdf: X(box(-26.8, -24, cv - 2.5, cv + 2.5, 21, 24)), mat: () => '#A8302A' })),  // söndü stop
  ...[-10, 10].map(cv => ({ sdf: X(box(24, 26.8, cv - 3.5, cv + 3.5, 20, 24)), mat: () => '#B9B39A' })),    // far (çamurlu)
];
const cut = box(-90, 90, -90, 90, WL, 200);
const above = parts.map(o => ({ sdf: INT(o.sdf, cut), mat: o.mat }));

// su düzleminde (z=WL) ayak izi uzaklığı (yerel 2B yuvarlatılmış dikdörtgen, yalnız yaw)
function footD(u, v) {
  const c = Math.cos(YAW), s = Math.sin(YAW); const lu = c * u + s * v, lv = -s * u + c * v;
  const qx = Math.abs(lu) - 25, qy = Math.abs(lv) - 12; return Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) + Math.min(Math.max(qx, qy), 0);
}
const RGBA = (h, a) => { const c = H(h); c[3] = a; return c; };

const Wd = 260, Hd = 170, ox = 130, oy = 112;
const full = E.render(parts, Wd, Hd, ox, oy, { zmax: 120, outline: false });
const frames = [];
for (let f = 0; f < 3; f++) {
  const r = E.render(above, Wd, Hd, ox, oy, { zmax: 120 });
  const aboveA = new Uint8Array(r.W * r.H); for (let k = 0; k < aboveA.length; k++) aboveA[k] = r.rgba[k * 4 + 3];
  const hullAbove = (x, y) => (y - 1 >= 0 && aboveA[(y - 1) * r.W + x]) || (y - 2 >= 0 && aboveA[(y - 2) * r.W + x]);
  for (let y = 0; y < r.H; y++) for (let x = 0; x < r.W; x++) {
    const o = (y * r.W + x) * 4; if (r.rgba[o + 3]) continue;
    const sx = x + 0.5 - ox, sy = oy - (y + 0.5) - WL; const u = sy + sx / 2, v = sy - sx / 2;
    const h = full.hits[y * r.W + x];
    // batık kısım: su altında koyu, bulanık
    if (h && h.p.z < WL) {
      const depth = WL - h.p.z; const red = h.o.mat === cabinM || h.o.mat === chassis;
      if (depth < 2.2) { r.rgba.set(RGBA(mod(fl(u * 0.7 + f * 2), 4) ? W.foam : W.hi, 245), o); continue; }
      if (hullAbove(x, y)) { r.rgba.set(RGBA(mod(fl(x / 2) + f, 3) ? W.foam : W.hi, 245), o); continue; }
      const col = depth > 7 ? (red ? '#4C4034' : '#423F34') : (red ? '#584034' : '#4A473A');
      const ripple = mod(fl(u * 0.35 + v * 0.2 + f * 2), 9) === 0 && hash(x, y, 31) < 0.6;
      r.rgba.set(RGBA(ripple ? W.d1 : col, 235), o); continue;
    }
    const d = footD(u, v);
    const up = u > 0; // akıntı +u'dan (ekran sağ üst) gelir, -u'ya (sol alt) gider
    // kenar köpüğü: akıntıya bakan tarafta kalın birikme
    const band = up ? 7 : 4.5;
    if (d < 2.6) { r.rgba.set(RGBA(W.foam, 245), o); continue; }
    if (d < band) { const k = mod(fl(u * 0.6 + v * 0.9 + f * 2 + hash(fl(u / 3), fl(v / 3), 7) * 4), 5); if (k < 2) r.rgba.set(RGBA(W.foam, 235), o); else if (k < 4) r.rgba.set(RGBA(W.hi, 210), o); continue; }
    // dalga halkası (genişleyen)
    const ring = 7 + f * 2.2;
    if (Math.abs(d - ring) < 0.7 && hash(fl(u / 2), fl(v / 2), 13 + f) < 0.55) { r.rgba.set(RGBA(W.hi, 170), o); continue; }
    // akıntı yönünde iz: yukarı (+u) doğru bakınca arabaya çarpan noktalar
    if (!up || true) {
      let s0 = -1; for (let s = 0; s <= 46; s += 2) if (footD(u + s, v) < 2) { s0 = s; break; }
      if (s0 > 4) {
        const lane = fl(v / 3);
        if (mod(fl(v), 3) === 0) { const ph = mod(fl(u * 0.5 + f * 3 + hash(lane, 3, 1) * 20), 8); const fade = 1 - s0 / 46;
          if (ph < 4 && hash(x, y, 41 + f) < 0.35 + fade * 0.6) r.rgba.set(RGBA(ph < 1 ? W.foam : W.hi, Math.round(110 + 120 * fade)), o); }
      }
    }
  }
  const res = L.SC.cropImg(Object.assign(L.SC.toImg(r), { keepPivot: true }));
  frames.push(res);
}
// ortak kırpma (kareler aynı boyutta/aynı pivotta olsun): en büyük kutuya göre yeniden yerleştir
const minX = Math.min(...frames.map(fr => -fr.pivot[0])), maxX = Math.max(...frames.map(fr => fr.img.w - fr.pivot[0]));
const minY = Math.min(...frames.map(fr => -fr.pivot[1])), maxY = Math.max(...frames.map(fr => fr.img.h - fr.pivot[1]));
frames.forEach((fr, i) => {
  const img = new L.Img(maxX - minX, maxY - minY);
  img.blit(fr.img, fr.pivot[0] * -1 - minX, fr.pivot[1] * -1 - minY);
  const pivot = [-minX, -minY];
  const file = `${OUT}/PROP_Route_Car_Overturned${i === 0 ? '' : '_' + i}.png`;
  img.save(file);
  console.log(JSON.stringify({ file, size: [img.w, img.h], pivotPx: pivot, pivotNorm: [+(pivot[0] / img.w).toFixed(6), +(pivot[1] / img.h).toFixed(6)] }));
  if (process.argv[2] === 'preview') { // su renginde zemin üstünde 5x önizleme
    const bg = new L.Img(img.w + 20, img.h + 20); for (let j = 0; j < bg.h; j++) for (let k = 0; k < bg.w; k++) bg.set(k, j, H(mod(fl(k / 3) + fl(j / 2), 7) ? W.d2 : W.d3));
    for (let j = 0; j < img.h; j++) for (let k = 0; k < img.w; k++) { const c = img.get(k, j); if (c) L.blend(bg, k + 10, j + 10, c); }
    bg.scaled(5).save(`${process.env.PREVIEW_DIR || __dirname}/car_overturned_preview_${i}.png`); }
});
