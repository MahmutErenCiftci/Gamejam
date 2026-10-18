// SCHOOL ROUTE art (F07 risk view + F08 safe route): flooded Okul Caddesi (animated water overlay), stuck car (hazard
// lights), barriers + cones, danger sign, footbridge (deck + rail segments), school fence/gate pillars/flag, school facade.
// Ground rows reuse the street tiles. Run: node route_gen.js [preview]
const L = require('./lib.js');
const { Img, worldSprite, hash, E, PROJ, fl, mod, chk, H, p2g, g2p, ramp, makeManifest, emitTo, renderProp, scr, blend, text7 } = L;
const { V, box, rbox, sphere, ellipsoid, cylZ, coneZ, capsule, U, SUB, INT, SU, lambert } = E;
const OUT = process.env.ROUTE_OUT || (PROJ + '/Assets/Art/Props/SchoolRoute');
const man = makeManifest('SCHOOL ROUTE art (F07/F08). PPU 64. Route origin = grid (0,0) of the route root. Rows: 0..2 road, 3 curb, 4 sidewalk, 5 curb (front, faces the flooded street), 6..9 Okul Caddesi under water, 10 curb, 11..12 school sidewalk, 13 yard, school facade line gy 14. Footbridge gx 8.0..9.6. Props: pivot = footprint centre unless noted.');
const emit = (img, file, pivot, extra) => emitTo(man, img, file, pivot, extra);
const OL = '#14141F';
const W = { d1: '#6E6752', d2: '#5E5844', d3: '#4F4A39', d4: '#423D2F', hi: '#A39D82', foam: '#C4BDA0', foamD: '#9C9579', edge: '#2B2820' };

// ------------------------------------------------------------------ 3x5 caps font (signs)
const F = { A: ['.#.', '#.#', '###', '#.#', '#.#'], B: ['##.', '#.#', '##.', '#.#', '##.'], C: ['.##', '#..', '#..', '#..', '.##'], D: ['##.', '#.#', '#.#', '#.#', '##.'],
  E: ['###', '#..', '##.', '#..', '###'], G: ['.##', '#..', '#.#', '#.#', '.##'], H: ['#.#', '#.#', '###', '#.#', '#.#'], I: ['#', '#', '#', '#', '#'], İ: ['#', '#', '#', '#', '#'],
  K: ['#.#', '#.#', '##.', '#.#', '#.#'], L: ['#..', '#..', '#..', '#..', '###'], N: ['#..#', '##.#', '#.##', '#..#', '#..#'], O: ['###', '#.#', '#.#', '#.#', '###'],
  P: ['##.', '#.#', '##.', '#..', '#..'], R: ['##.', '#.#', '##.', '#.#', '#.#'], S: ['###', '#..', '###', '..#', '###'], T: ['###', '.#.', '.#.', '.#.', '.#.'],
  U: ['#.#', '#.#', '#.#', '#.#', '###'], Y: ['#.#', '#.#', '.#.', '.#.', '.#.'], Ş: ['###', '#..', '###', '..#', '###'], Ü: ['#.#', '#.#', '#.#', '#.#', '###'],
  M: ['#...#', '##.##', '#.#.#', '#...#', '#...#'], '.': ['.', '.', '.', '.', '#'], '!': ['#', '#', '#', '.', '#'], ' ': ['..', '..', '..', '..', '..'] };
function t35(str) { const px = []; let x = 0; for (const ch of str) { const g = F[ch]; if (!g) throw new Error('glyph ' + ch); g.forEach((r, y) => [...r].forEach((c, i) => { if (c === '#') px.push([x + i, y]); })); if (ch === 'İ') px.push([x, -2]); if (ch === 'Ü') px.push([x, -2], [x + 2, -2]); if (ch === 'Ş') px.push([x + 1, 5]); x += g[0].length + 1; } return { px, w: x - 1 }; }
// draw a screen-facing plate on an engine render result (canvas y down)
function plate(r, cx, top, w, h, fill, border, lines) {
  const x0 = cx - Math.floor(w / 2);
  for (let y = -1; y <= h; y++) for (let x = -1; x <= w; x++) { const o = ((top + y) * r.W + x0 + x) * 4; if (o < 0 || x0 + x < 0 || x0 + x >= r.W) continue;
    const col = (x < 0 || y < 0 || x >= w || y >= h) ? OL : (x === 0 || y === 0 || x === w - 1 || y === h - 1) ? border : fill; r.rgba.set(H(col), o); }
  for (const ln of lines) { const t = t35(ln.text); const tx = x0 + Math.floor((w - t.w) / 2); for (const [x, y] of t.px) { const o = ((top + ln.y + y) * r.W + tx + x) * 4; r.rgba.set(H(ln.col), o); } }
}

// ------------------------------------------------------------------ WATER overlay (3 frames), pivot = route origin
const WGX0 = -14, WGX1 = 28, WGY0 = 5.72, WGY1 = 10.28;
for (let f = 0; f < 3; f++) {
  const [ax] = g2p(WGX0, WGY1), [bx] = g2p(WGX1, WGY0); const minY = Math.floor(g2p(WGX0, WGY0)[1]) - 1, maxY = Math.ceil(g2p(WGX1, WGY1)[1]) + 1;
  const minX = Math.floor(ax) - 1, maxX = Math.ceil(bx) + 1;
  const inW = (gx, gy) => gx >= WGX0 && gx <= WGX1 && gy >= WGY0 + 0.02 * Math.sin(gx * 3.1 + f * 2.1) && gy <= WGY1 - 0.02 * Math.sin(gx * 2.7 + f * 1.7);
  const img = worldSprite(minX, minY, maxX, maxY, (Xc, Yc, X, Y) => {
    const [gx, gy] = p2g(Xc, Yc); if (!inW(gx, gy)) return null;
    const [ux, uy] = p2g(Xc, Yc + 1), [dx, dy] = p2g(Xc, Yc - 1);
    if (!inW(dx, dy)) return H(W.edge);
    if (!inW(ux, uy)) return H(W.foamD);
    const de = Math.min(gy - WGY0, WGY1 - gy);
    // foam lines along both curbs (animated)
    if (de < 0.16) { const k = mod(fl(gx * 9 + f * 3 + (gy > 8 ? 5 : 0)), 7); return H(k < 3 ? W.foam : k < 5 ? W.hi : W.d1); }
    if (de < 0.26 && hash(fl(Xc + 2000), fl(Yc + 2000), 3 + f) < 0.25) return H(W.d1);
    // current flowing along +X: dashes on lines of constant gy (screen slope 1/2)
    const lane = fl((Yc - Xc / 2) / 6), ph = mod(Xc + f * 5.3 + hash(lane, 7, 1) * 90, 46);
    if (mod(fl(Yc - Xc / 2), 6) === 0 && ph < 9) return H(ph < 2 ? W.foam : W.hi);
    // floating leaves/debris
    if (hash(fl((Xc + f * 4) / 3), fl(Yc / 2), 11) < 0.004) return H('#8A5A2A');
    const h = hash(fl(Xc + 2000), fl(Yc + 2000), 9);
    if (h < 0.05) return H(W.d3);
    if (h > 0.97) return H(W.d1);
    return H(chk(Xc / 3, Yc / 2) ? W.d2 : W.d2);
  });
  emit(img, `${OUT}/FX_Route_FloodWater_${f}.png`, [-minX, -minY], { kind: 'overlay', order: -95, note: 'muddy flood water over Okul Caddesi (gx -14..28, gy 5.72..10.28), frame ' + f + '/3 (~0.18 s). Pivot = route origin.' });
}

// ------------------------------------------------------------------ FOOTBRIDGE deck (gx 8.0..9.6, gy 5.4..10.6). Deck top flush with the ground.
{
  const u0 = 0, u1 = 1.6 * 32, v0 = 0, v1 = 5.2 * 32, T = 7; // local: origin = bridge near-left corner (gx 8.0, gy 5.4)
  const deck = (p, n, c) => {
    if (c.face === 'top') { const k = mod(fl(p.v), 6); if (k === 0) return '#5B4433'; const h = hash(fl(p.v / 6), fl(p.u / 13), 3); return h < 0.3 ? '#8A6A4C' : h < 0.7 ? '#9A7856' : '#A68461'; }
    if (c.face === 'left') return p.z > T - 2 ? '#6E7782' : '#5A626C';
    return p.z > T - 2 ? '#646C77' : '#4C535C';
  };
  const objs = [
    { sdf: box(u0, u1, v0, v1, 0, T), mat: deck },
    { sdf: box(u0 + 2, u1 - 2, v1 / 2 - 4, v1 / 2 + 4, 0, T), mat: (p, n, c) => c.face === 'left' ? '#6A717C' : '#555C66' },
  ];
  const r = E.render(objs, 320, 260, 150, 220, { zmax: 40 });
  const res = L.SC.cropImg(Object.assign(L.SC.toImg(r), { keepPivot: true }));
  // pivot: the deck top must sit on the ground plane -> move the pivot T px down; reference point = grid (8.0, 5.4)
  emit(res.img, `${OUT}/PROP_Route_BridgeDeck.png`, [res.pivot[0], res.pivot[1] + T], { kind: 'deck', order: -80, anchorGrid: [8.0, 5.4], note: 'footbridge deck, pivot = its near-left corner on the ground (grid 8.0, 5.4); top surface flush with the ground, side faces drop into the water.' });
}
// rail segment (1 cell along grid Y), posts at both ends
{
  const steel = (p, n) => ramp(n, ['#8FB39A', '#6F9A7E', '#557E64', '#3E604B']);
  const objs = [
    { sdf: box(-1.2, 1.2, -16, -13.6, 0, 26), mat: steel }, { sdf: box(-1.2, 1.2, 13.6, 16, 0, 26), mat: steel },
    { sdf: capsule(V(0, -16, 26), V(0, 16, 26), 1.3), mat: steel }, { sdf: capsule(V(0, -16, 15), V(0, 16, 15), 0.9), mat: steel },
    { sdf: box(-0.5, 0.5, -1, 1, 0, 26), mat: steel },
  ];
  const r = renderProp(objs, { W: 120, H: 120, ox: 60, oy: 90 });
  emit(r.img, `${OUT}/PROP_Route_BridgeRail.png`, r.pivot, { kind: 'prop', footprint: [0.08, 1.0], note: 'bridge railing segment along grid Y (1 cell). Place at gx 8.0 and gx 9.6, centres every 1 cell. order 0.' });
}
// barrier (red/white A-frame), 1.2 cells along X
{
  const red = '#C0392B', redD = '#8E2A20', wht = '#EEF0EC', whtD = '#C9CED4';
  const stripe = (p, n, c) => { const k = mod(fl((p.u + p.z * 0.8) / 6), 2); const dark = c.face !== 'top' && c.face === 'right'; return k ? (dark ? redD : red) : (dark ? whtD : wht); };
  const leg = (p, n) => ramp(n, ['#7D8794', '#5F6977', '#454E5A', '#323944']);
  const objs = [
    { sdf: box(-19, 19, -1.2, 1.2, 14, 24), mat: stripe }, { sdf: box(-19, 19, -1, 1, 3, 7), mat: stripe },
    { sdf: capsule(V(-17, -4, 0), V(-17, 0, 24), 1.1), mat: leg }, { sdf: capsule(V(-17, 4, 0), V(-17, 0, 24), 1.1), mat: leg },
    { sdf: capsule(V(17, -4, 0), V(17, 0, 24), 1.1), mat: leg }, { sdf: capsule(V(17, 4, 0), V(17, 0, 24), 1.1), mat: leg },
    { sdf: sphere(-17, 0, 27, 2.2), mat: () => '#F2A33A' },
  ];
  const r = renderProp(objs, { W: 140, H: 120, ox: 70, oy: 90, shadow: (u, v) => (Math.abs(u) < 20 && Math.abs(v) < 5 ? 50 : 0) });
  emit(r.img, `${OUT}/PROP_Route_Barrier.png`, r.pivot, { kind: 'prop', footprint: [1.2, 0.25], note: 'road barrier (red/white), long side along grid X, amber lamp. order 0. Collider = footprint.' });
}
// traffic cone
{
  const objs = [{ sdf: box(-5, 5, -5, 5, 0, 1.6), mat: () => '#3A3F48' }, { sdf: coneZ(0, 0, 4.2, 0.8, 1.6, 17), mat: (p, n) => (p.z > 7 && p.z < 10.5) ? ramp(n, ['#FFFFFF', '#EEF0EC', '#C9CED4', '#A9AEB5']) : ramp(n, ['#FF9A4D', '#F27A2A', '#C95E1E', '#9A4614']) }];
  const r = renderProp(objs, { W: 60, H: 60, ox: 30, oy: 45 });
  emit(r.img, `${OUT}/PROP_Route_Cone.png`, r.pivot, { kind: 'prop', footprint: [0.3, 0.3], note: 'traffic cone. order 0.' });
}
// danger sign on a post: street name plate (OKUL CD.) + yellow warning board (screen-facing)
{
  const objs = [{ sdf: box(-1.2, 1.2, -1.2, 1.2, 0, 58), mat: (p, n) => ramp(n, ['#9AA4B0', '#7D8794', '#5F6977', '#454E5A']) }, { sdf: box(-3, 3, -3, 3, 0, 2), mat: () => '#3A3F48' }];
  const r = E.render(objs, 120, 140, 60, 120, { zmax: 100 });
  const [cx, cy] = scr(r, 0, 0, 58);
  plate(r, cx, cy - 1, 31, 9, '#2F5DA8', '#EEF0EC', [{ text: 'OKUL CD.', y: 2, col: '#FFFFFF' }]);
  plate(r, cx, cy + 9, 59, 23, '#F2C230', OL, [{ text: 'SU BİRİKİNTİSİ', y: 4, col: '#14141F' }, { text: 'TEHLİKELİDİR!', y: 13, col: '#A82418' }]);
  const res = L.SC.cropImg(Object.assign(L.SC.toImg(r), { keepPivot: true }));
  emit(res.img, `${OUT}/PROP_Route_DangerSign.png`, res.pivot, { kind: 'prop', footprint: [0.2, 0.2], note: '"SU BİRİKİNTİSİ TEHLİKELİDİR!" board + OKUL CD. street plate on a post (screen-facing). order 0.' });
}
// stuck car in the water (hazard lights off/on), long side along X, waterline at z=10
for (const lights of [0, 1]) {
  const body = '#B83A2E', bodyL = '#D2503F', bodyD = '#8E2A20', bodyDD = '#6E2018';
  const paint = (p, n, c) => { if (c.face === 'top') return p.z > 30 ? bodyL : bodyL; return ramp(n, [bodyL, body, bodyD, bodyDD]); };
  const glassM = (p, n, c) => { if (c.face === 'top') return '#8A9AA8'; const s = mod(fl(p.u * 0.7 + p.z), 14); return s < 2 ? '#C9D6E0' : ramp(n, ['#9DB0BF', '#7F93A3', '#62778A', '#4C5F70']); };
  const cut = box(-80, 80, -80, 80, 10, 200);
  const objs = [
    { sdf: INT(rbox(-26, 26, -12.5, 12.5, 4, 22, 3), cut), mat: paint },
    { sdf: INT(rbox(-14, 13, -11, 11, 18, 34, 3), cut), mat: (p, n, c) => (p.z > 21 && p.z < 32 && c.face !== 'top' && Math.abs(p.u - 12) > 1.5) ? glassM(p, n, c) : paint(p, n, c) },
    { sdf: box(24, 26.6, -10, -6, 15, 19), mat: () => lights ? '#FFB23E' : '#9A6A2A' }, { sdf: box(24, 26.6, 6, 10, 15, 19), mat: () => lights ? '#FFB23E' : '#9A6A2A' },
    { sdf: box(-26.6, -24, -10, -6, 15, 19), mat: () => lights ? '#FF8A3A' : '#8E3A20' }, { sdf: box(-26.6, -24, 6, 10, 15, 19), mat: () => lights ? '#FF8A3A' : '#8E3A20' },
    { sdf: box(-8, 8, -12.8, -12.2, 12, 13), mat: () => '#5A2018' },
  ];
  const r = E.render(objs, 200, 160, 100, 120, { zmax: 100, post: null });
  // waterline: ripples/foam ring at z=10 around the body (semi-transparent), drawn on empty pixels
  for (let y = 0; y < r.H; y++) for (let x = 0; x < r.W; x++) { const o = (y * r.W + x) * 4; if (r.rgba[o + 3]) continue;
    const sx = x + 0.5 - r.ox, sy = r.oy - (y + 0.5) - 10; const u = sy + sx / 2, v = sy - sx / 2;
    const d = Math.max(Math.abs(u) / 30, Math.abs(v) / 15.5); if (d < 1.0) { r.rgba.set([196, 189, 160, 235], o); } else if (d < 1.16) { const k = mod(fl(u * 0.5 + v), 3); if (k === 0) r.rgba.set([163, 157, 130, 200], o); } }
  // glow around the hazard lights when on
  if (lights) for (const [u, v] of [[25.5, -8], [25.5, 8], [-25.5, -8], [-25.5, 8]]) { const [hx, hy] = scr(r, u, v, 17); for (let y = -5; y <= 5; y++) for (let x = -5; x <= 5; x++) { const o = ((hy + y) * r.W + hx + x) * 4; if (r.rgba[o + 3] === 255) continue; const dd = Math.hypot(x, y * 1.2); if (dd < 5) r.rgba.set([255, 178, 62, dd < 3 ? 120 : 60], o); } }
  const res = L.SC.cropImg(Object.assign(L.SC.toImg(r), { keepPivot: true }));
  emit(res.img, `${OUT}/PROP_Route_StuckCar_${lights}.png`, res.pivot, { kind: 'prop', footprint: [1.7, 0.85], note: 'red car stuck in the flood (lower body under water), hazard lights ' + (lights ? 'ON' : 'off') + ' (blink 0.5 s). order 0.' });
}
// school fence segment (1 cell along X), gate pillar, flag pole
{
  const iron = (p, n) => ramp(n, ['#5E8C6A', '#467354', '#355B42', '#26422F']);
  const objs = [
    { sdf: box(-16, 16, -2, 2, 0, 6), mat: (p, n, c) => c.face === 'top' ? '#C9C6BD' : c.face === 'left' ? '#A9A69C' : '#8E8B82' },
    { sdf: box(-16, 16, -0.6, 0.6, 30, 32), mat: iron }, { sdf: box(-16, 16, -0.6, 0.6, 12, 14), mat: iron },
  ];
  for (let u = -14; u <= 14; u += 4) objs.push({ sdf: box(u - 0.6, u + 0.6, -0.6, 0.6, 6, 36), mat: iron });
  const r = renderProp(objs, { W: 100, H: 100, ox: 50, oy: 75 });
  emit(r.img, `${OUT}/PROP_Route_SchoolFence.png`, r.pivot, { kind: 'prop', footprint: [1.0, 0.12], note: 'school fence, 1 cell along grid X. order 0.' });
  const brick = (p, n, c) => { const row = fl(p.z / 4), pu = mod(fl((c.face === 'left' ? p.v : p.u) + (row & 1) * 3), 6); if (mod(fl(p.z), 4) === 3 || pu === 0) return '#6E3B30'; return ramp(n, ['#C46A50', '#A85540', '#8E4636', '#74382B']); };
  const objs2 = [{ sdf: box(-6, 6, -6, 6, 0, 50), mat: brick }, { sdf: box(-7.5, 7.5, -7.5, 7.5, 50, 54), mat: (p, n, c) => c.face === 'top' ? '#D9D4C6' : '#B5B0A2' }, { sdf: sphere(0, 0, 57, 3.2), mat: (p, n) => ramp(n, ['#FFE07A', '#F2C230', '#C99A1E', '#8E6A14']) }];
  const r2 = renderProp(objs2, { W: 80, H: 110, ox: 40, oy: 90 });
  emit(r2.img, `${OUT}/PROP_Route_GatePillar.png`, r2.pivot, { kind: 'prop', footprint: [0.38, 0.38], note: 'brick gate pillar. order 0.' });
  // flag pole with a red flag (white crescent + star, simplified)
  const objs3 = [{ sdf: cylZ(0, 0, 1.2, 0, 120), mat: (p, n) => ramp(n, ['#D0D6DD', '#B2BAC4', '#8E98A4', '#6A7480']) }, { sdf: sphere(0, 0, 121, 2), mat: () => '#F2C230' }, { sdf: box(-3, 3, -3, 3, 0, 3), mat: () => '#7D8794' }];
  const r3 = E.render(objs3, 120, 200, 40, 180, { zmax: 160 });
  const [fx, fy] = scr(r3, 0, 0, 117);
  for (let y = 0; y < 18; y++) for (let x = 1; x < 30; x++) { const wave = Math.round(Math.sin((x + 2) * 0.35) * 1.5); const o = ((fy + y + wave) * r3.W + fx + x) * 4;
    let col = '#E30A17'; if (y === 0 || y === 17 || x === 29) col = '#A80812';
    const cxr = x - 9, cyr = y - 8.5; const d1 = Math.hypot(cxr, cyr), d2 = Math.hypot(cxr - 2, cyr);
    if (d1 < 5.2 && d2 >= 4.2) col = '#FFFFFF';
    if (Math.abs(x - 16) + Math.abs(y - 8.5) * 1.1 < 2.2) col = '#FFFFFF';
    r3.rgba.set(H(col), o); }
  const res3 = L.SC.cropImg(Object.assign(L.SC.toImg(r3), { keepPivot: true }));
  emit(res3.img, `${OUT}/PROP_Route_FlagPole.png`, res3.pivot, { kind: 'prop', footprint: [0.15, 0.15], note: 'school flag pole. order 0.' });
}
// school facade: 2 storeys, along grid X (front face toward -Y), length 14 cells, entrance at local gx 6.0..7.6
{
  const LEN = 14 * 32, D = 24, HH = 150;
  const cream = ['#F0D9B5', '#E6CBA2', '#D8BB8E', '#C7A87B'];
  const front = (u, z) => {
    if (z < 10) { const row = fl(z / 4), pu = mod(fl(u) + (row & 1) * 4, 8); if (mod(fl(z), 4) === 3 || pu === 0) return '#6E3B30'; return hash(fl(u / 8), row, 4) < 0.2 ? '#8E4636' : '#A85540'; }
    if (z >= HH - 8) return z >= HH - 3 ? '#C0392B' : '#A82E22';
    // name board (u 186..250 = canopy width; text "GÜNEŞ İLKOKULU" needs 52 px)
    if (u >= 186 && u < 250 && z >= 66 && z < 84) { if (u < 187 || u >= 249 || z < 67 || z >= 83) return '#1F3F7A'; return '#2F5DA8'; }
    // entrance (double door + canopy) at u 192..243
    if (u >= 190 && u < 246) {
      if (z < 64) { if (u < 193 || u >= 243 || z >= 61) return '#5A3A28'; if (Math.abs(u - 218) < 1) return '#3E2A1E'; if (z > 18 && z < 56) return (mod(fl(u + z), 13) < 2) ? '#C9D6E0' : '#7F93A3'; return '#6E4A34'; }
      if (z >= 66 && z < 84) { // name board
        if (u < 196 || u >= 240 || z < 67 || z >= 83) return '#1F3F7A'; return '#2F5DA8'; }
    }
    // windows: 2 rows
    for (const [z0, z1] of [[22, 58], [92, 128]]) {
      const k = fl((u - 12) / 44), lu = mod(u - 12, 44);
      if (u >= 12 && u < LEN - 12 && lu < 30 && z >= z0 && z < z1 && !(u >= 182 && u < 254 && z0 < 60)) {
        if (lu < 2 || lu >= 28 || z < z0 + 2 || z >= z1 - 2) return '#F4F1EA';
        if (Math.abs(lu - 15) < 1) return '#F4F1EA';
        if (z < z0 + 4) return '#9AA4B0';
        const lit = hash(k, z0, 5) < 0.45; const s = mod(fl(lu + z * 0.7), 17);
        if (s < 2) return '#D6E2EC';
        return lit ? (z > z1 - 10 ? '#F2D58A' : '#E8C26A') : (z > z1 - 10 ? '#8FA6BA' : '#6F879C');
      }
      if (u >= 12 && u < LEN - 12 && lu < 30 && z >= z0 - 3 && z < z0 && !(u >= 182 && u < 254 && z0 < 60)) return '#E8E4DA'; // sill
    }
    if (z >= 84 && z < 87) return '#D8BB8E';  // storey band
    return null;
  };
  const objs = [
    { sdf: box(0, LEN, 0, D, 0, HH), mat: (p, n, c) => { if (c.face === 'right') { const f = front(p.u, p.z); if (f) return f; return hash(fl(p.u), fl(p.z), 8) < 0.02 ? cream[2] : cream[1]; } if (c.face === 'top') return '#B8432F'; return cream[0]; } },
    { sdf: box(186, 250, -14, 0, 64, 67), mat: (p, n, c) => c.face === 'top' ? '#9AA4B0' : '#6A7480' },          // entrance canopy
    { sdf: box(188, 190, -13, -11, 0, 64), mat: (p, n) => ramp(n, ['#9AA4B0', '#7D8794', '#5F6977', '#454E5A']) },
    { sdf: box(246, 248, -13, -11, 0, 64), mat: (p, n) => ramp(n, ['#9AA4B0', '#7D8794', '#5F6977', '#454E5A']) },
  ];
  const Wc = LEN + D + 40, Hc = HH + (LEN + D) / 2 + 40;
  const r = E.render(objs, Wc, Hc, D + 20, Hc - 20, { zmax: 200, innerThr: 6 });
  // name board text (screen-facing, on the sheared board: draw per column following the wall slope)
  // Harfler tek tek, bütün olarak yerleştirilir (sütun sütun kaydırma 3x5 harfleri bozuyordu: N→H, Ü/Ş/İ işaretleri yoktu).
  {
    const txt = 'GÜNEŞ İLKOKULU'; const t = t35(txt); let gx = Math.round(218 - t.w / 2);
    for (const ch of txt) {
      const g = t35(ch); const gw = F[ch][0].length; const uc = gx + gw / 2;     // harf merkezi
      const [sx0] = scr(r, gx, 0, 0); const [, syc] = scr(r, uc, 0, 79);        // satır 0 (büyük harf üstü) z = 79
      for (const [x, y] of g.px) r.rgba.set(H('#FFFFFF'), ((syc + y) * r.W + sx0 + x) * 4);
      gx += gw + 1;
    }
  }
  const res = L.SC.cropImg(Object.assign(L.SC.toImg(r), { keepPivot: true }));
  emit(res.img, `${OUT}/PROP_Route_SchoolFacade.png`, res.pivot, { kind: 'facade', order: -50, length: 14, note: 'school building, pivot = front-face left corner on the ground (local u=0, v=0). Place at grid (gx0, 14). Entrance doors at local cells 6.0..7.6 (i.e. gx0+6..gx0+7.6), name board "GÜNEŞ İLKOKULU".' });
}

L.fs.writeFileSync(__dirname + '/route_manifest.json', JSON.stringify(man, null, 1));
console.log('route art ok', man.files.length);

// ------------------------------------------------------------------ preview
if (process.argv.includes('preview')) {
  const { decodePng } = require(L.path.resolve(__dirname, '../../roomB_png.js'));
  const load = f => { const d = decodePng(L.fs.readFileSync(f)); const im = new Img(d.w, d.h); for (let j = 0; j < d.h; j++) for (let i = 0; i < d.w; i++) { const s = ((d.h - 1 - j) * d.w + i) * 4; if (d.rgba[s + 3]) im.set(i, j, [...d.rgba.subarray(s, s + 4)]); } return im; };
  const Wc = 900, Hc = 560, ox = 330, oy = 60;
  const canvas = new Img(Wc, Hc); canvas.fill([60, 68, 80, 255]);
  const rec = f => man.files.find(x => x.file.endsWith(f));
  const T = PROJ + '/Assets/Art/Tiles/Street/';
  const tiles = {}; for (const k of ['Sidewalk_A', 'Curb', 'Road_A', 'Road_Lane', 'Road_Gutter', 'Grass']) tiles[k] = load(T + 'TILE_Street_' + k + '.png');
  const putTile = (k, gx, gy, rot) => { const im = tiles[k]; const [X, Y] = g2p(gx + 0.5, gy + 0.5); for (let j = 0; j < im.h; j++) for (let i = 0; i < im.w; i++) { const c = rot ? im.get(im.w - 1 - i, im.h - 1 - j) : im.get(i, j); if (c) blend(canvas, ox + X - im.w / 2 + i, oy + Y - im.h / 2 + j, c); } };
  for (let s = 40; s >= -20; s--) for (let gx = -10; gx <= 24; gx++) { const gy = s - gx; if (gy < -3 || gy > 13) continue;
    const k = gy <= -1 ? 'Grass' : gy === 0 ? 'Road_A' : gy === 1 ? 'Road_Lane' : gy === 2 ? 'Road_Gutter' : gy === 3 ? 'Curb' : gy === 4 ? 'Sidewalk_A' : gy === 5 ? 'Curb' : gy <= 9 ? 'Road_A' : gy === 10 ? 'Curb' : 'Sidewalk_A';
    putTile(k, gx, gy, gy === 5); }
  const draw = (f, gx, gy) => { const r = rec(f), im = load(PROJ + '/' + r.file); const [X, Y] = g2p(gx, gy).map(Math.round);
    for (let j = 0; j < im.h; j++) for (let i = 0; i < im.w; i++) { const c = im.get(i, j); if (c) blend(canvas, ox + X - r.pivotPx[0] + i, oy + Y - r.pivotPx[1] + j, c); } };
  draw('PROP_Route_SchoolFacade.png', 2, 14);
  draw('FX_Route_FloodWater_0.png', 0, 0);
  draw('PROP_Route_BridgeDeck.png', 8.0, 5.4);
  const props = [];
  for (let gx = 2; gx < 16; gx++) if (gx < 7.6 || gx > 9.6) props.push(['PROP_Route_SchoolFence.png', gx + 0.5, 13.2]);
  props.push(['PROP_Route_GatePillar.png', 7.7, 13.2], ['PROP_Route_GatePillar.png', 9.9, 13.2], ['PROP_Route_FlagPole.png', 11.0, 13.6]);
  for (const gy of [5.9, 6.9, 7.9, 8.9, 9.9]) { props.push(['PROP_Route_BridgeRail.png', 8.0, gy], ['PROP_Route_BridgeRail.png', 9.6, gy]); }
  props.push(['PROP_Route_Barrier.png', 2.9, 5.35], ['PROP_Route_Barrier.png', 4.2, 5.35], ['PROP_Route_Cone.png', 2.0, 5.3], ['PROP_Route_Cone.png', 5.1, 5.3]);
  props.push(['PROP_Route_DangerSign.png', 5.7, 4.9], ['PROP_Route_StuckCar_1.png', 3.7, 8.0]);
  props.sort((a, b) => (b[1] + b[2]) - (a[1] + a[2]));
  for (const p of props) draw(p[0], p[1], p[2]);
  canvas.scaled(2).save(__dirname + '/route_preview.png');
  console.log('preview saved');
}
