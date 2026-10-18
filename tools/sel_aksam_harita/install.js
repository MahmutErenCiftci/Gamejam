// PNG + .meta'ları Assets/Art/FloodEvening/ENV altına kopyalar ve prefab kurulum verisini (build_data.txt) yazar.
// node install.js [--png]
const C = require('./common.js');
const { fs, path, fl } = C;
const M = require('./map.js'); const L = require('./layout.js');
const T = JSON.parse(fs.readFileSync(C.OUT + '/terrain.json', 'utf8'));
const PR = JSON.parse(fs.readFileSync(C.OUT + '/props.json', 'utf8'));
const doPng = process.argv.includes('--png');
const REL = 'Assets/Art/FloodEvening/ENV';
const used = [];
function install(srcFile, sub, name, w, h, sprites) {
  const dir = C.DST + '/' + sub; fs.mkdirSync(dir, { recursive: true });
  const dst = dir + '/' + name + '.png';
  if (doPng) { fs.copyFileSync(srcFile, dst); const mp = dst + '.meta'; fs.writeFileSync(mp, C.metaText(C.guidFor(mp), name, w, h, sprites)); }
  used.push(dst);
  return REL + '/' + sub + '/' + name + '.png';
}
const png = f => { const b = fs.readFileSync(f); return [b.readUInt32BE(16), b.readUInt32BE(20)]; };
const lines = [];
const W = (x, y) => [x.toFixed(4), y.toFixed(4)];
const world = (gx, gy, h) => [(gx - gy) / 2, (gx + gy) / 4 + h / 64];
const hAt = (gx, gy) => M.H(fl(gx), fl(gy));
// ---- terrain chunks
for (const c of T.chunks) { const p = install(C.OUT + '/terrain/' + c.name + '.png', 'Terrain', c.name, c.w, c.h, [{ name: c.name + '_0', x: 0, y: 0, w: c.w, h: c.h, px: 0, py: 0 }]);
  lines.push(['SPR', 'Ground', c.name, p, c.name + '_0', ...W(c.x / 64, c.y / 64), -100, 0].join('|')); }
// ---- anchors (before children)
const anchorPos = {};
for (const [k, [gx, gy]] of Object.entries(L.anchors)) { const [x, y] = world(gx, gy, hAt(gx, gy)); anchorPos[k] = [x, y]; lines.push(['ANC', k, ...W(x, y)].join('|')); }
{ const [x, y] = world(42.5, 1, 0); lines.push(['ANC', 'LowRoad/Water', ...W(x, y)].join('|')); }
lines.push(['ANC', 'WaterAreas', '0', '0'].join('|'));
// ---- water stages
for (const w of T.water) {
  const p = install(C.OUT + '/water/' + w.name + '.png', 'Water', w.name, w.w, w.h, [{ name: w.name + '_0', x: 0, y: 0, w: w.w, h: w.h, px: 0, py: 0 }]);
  const parent = w.area === 'LowRoad' ? 'LowRoad/Water' : 'WaterAreas/' + w.area;
  lines.push(['SPR', parent, 'Stage' + w.stage + '_' + w.name.replace('FX_FE_Water_', ''), p, w.name + '_0', ...W(w.x / 64, w.y / 64), -100 + w.stage, 0].join('|'));
}
// ---- new prop sprites (install all)
const protoPath = {};
for (const n in PR) { const m = PR[n]; const sub = m.line ? 'Lines' : n.startsWith('PROP_FE_Sky') ? 'Sky' : 'Props'; protoPath[n] = install(C.OUT + '/props/' + m.file + '.png', sub, m.file, m.w, m.h, m.sprites); }
function resolve(s) {
  if (s.startsWith('N:')) { const n = s.slice(2); return [protoPath[n], PR[n].sprites[0].name]; }
  const rel = 'Assets/Art/' + s.slice(2) + '.png'; const meta = fs.readFileSync(C.PROJ + '/' + rel + '.meta', 'utf8');
  const nm = /spriteMode: 2/.test(meta) ? meta.match(/sprites:\s*\n\s*- serializedVersion: 2\s*\n\s*name: (.*)/)[1].trim() : path.basename(rel, '.png') + '_0';
  const n2 = (meta.match(/nameFileIdTable:\s*\n\s*(\S+):/) || [])[1];
  return [rel, /spriteMode: 2/.test(meta) ? nm : (n2 || nm)];
}
// ---- layout objects
for (const o of L.objs) {
  // wx/wy: doğrudan dünya konumu, sn: çoklu sprite dosyasında belirli dilim (tel dilimleri, hazards.js)
  const [x, y] = o.wx !== undefined ? [o.wx, o.wy] : world(o.gx, o.gy, o.h); const [p, sn0] = resolve(o.s); const sn = o.sn || sn0;
  const parent = o.parent || o.group;
  lines.push(['SPR', parent, o.name, p, sn, ...W(x, y), o.order, o.flip ? 1 : 0].join('|'));
  if (o.col) { const pts = o.col.map(([du, dv]) => [(x + (du - dv) / 2).toFixed(4), (y + (du + dv) / 4).toFixed(4)].join(',')).join(';'); lines.push(['COL', parent + '/' + o.name, 'solid', pts].join('|')); }
  if (o.light) lines.push(['LIGHT', 'Lights', 'Glow_' + o.name, ...W(x + 0.22, y - 0.1)].join('|'));
  if (o.frameLoop) lines.push(['FRAMELOOP', parent + '/' + o.name].join('|'));
}
// ---- line strips
for (const n in PR) { const m = PR[n]; if (!m.line) continue; const [ox, oy] = world(m.line.origin[0], m.line.origin[1], m.line.h0);
  for (const st of m.line.strips) lines.push(['SPR', 'Lines/' + m.line.id, st.sprite.replace('LINE_FE_', ''), protoPath[n], st.sprite, ...W(ox + st.dx / 64, oy + st.dy / 64), 0, 0].join('|')); }
// ---- gates
for (const g of L.gates) {
  const hb = g.axis === 'y' ? Math.max(hAt(g.gx, g.gy), hAt(g.gx - 1, g.gy)) : Math.max(hAt(g.gx, g.gy), hAt(g.gx, g.gy - 1)); const [x, y] = world(g.gx, g.gy, hb);
  const [p, sn] = resolve(g.closed); lines.push(['SPR', g.name + '/Closed', g.name + '_Closed', p, sn, ...W(x, y), 0, g.flip ? 1 : 0].join('|'));
  for (const [s, du, dv] of g.open) { const [p2, sn2] = resolve(s); const [x2, y2] = world(g.gx + du, g.gy + dv, hb); lines.push(['SPR', g.name + '/Open', path.basename(p2, '.png').replace('PROP_FE_', ''), p2, sn2, ...W(x2, y2), 0, g.flip ? 1 : 0].join('|')); }
  const q = (g.axis === 'y' ? [[g.gx - 0.22, g.gy - g.half], [g.gx + 0.22, g.gy - g.half], [g.gx + 0.22, g.gy + g.half], [g.gx - 0.22, g.gy + g.half]] : [[g.gx - g.half, g.gy - 0.22], [g.gx + g.half, g.gy - 0.22], [g.gx + g.half, g.gy + 0.22], [g.gx - g.half, g.gy + 0.22]]).map(([a, b]) => world(a, b, hb));
  const target = g.colliderOnRoot ? g.name : g.name + '/Closed'; const tp = g.colliderOnRoot ? anchorPos[g.name] || [x, y] : [x, y];
  lines.push(['COL', target, 'solid', q.map(([a, b]) => a.toFixed(4) + ',' + b.toFixed(4)).join(';')].join('|'));
  lines.push(['INACTIVE', g.name + '/Open'].join('|'));
}
// ---- areas
for (const [k, a] of Object.entries(L.areas)) {
  if (a.box) { // rotated box along gy direction
    const [x0, y0] = world((a.from[0] + a.to[0]) / 2, (a.from[1] + a.to[1]) / 2, 0); const len = Math.abs(a.to[1] - a.from[1]) * Math.hypot(0.5, 0.25) + 0.2;
    const thick = Math.abs(a.to[0] - a.from[0]) * Math.hypot(0.5, 0.25) * 0.9; // perpendicular-ish
    lines.push(['BOX', k, ...W(x0, y0), len.toFixed(4), thick.toFixed(4), (-26.565).toFixed(3)].join('|'));
    continue;
  }
  const h = hAt(a.poly[0][0], a.poly[0][1]); const pw = a.poly.map(([u, v]) => world(u, v, h)); const ap = anchorPos[k] || [0, 0];
  if (!anchorPos[k]) { const [x, y] = world(a.poly[0][0], a.poly[0][1], h); lines.push(['ANC', k, ...W(...(anchorPos[k.split('/')[0]] || [0, 0]))].join('|')); }
  const base = anchorPos[k] || anchorPos[k.split('/')[0]] || [0, 0];
  lines.push(['COL', k, a.trigger ? 'trigger' : 'solid', pw.map(([x, y]) => x.toFixed(4) + ',' + y.toFixed(4)).join(';')].join('|'));
}
// ---- global light, walk bounds, camera bounds, sky
lines.push(['GLOBAL', 'Lights/GlobalEvening'].join('|'));
for (const lp of T.loops) lines.push(['EDGE', lp.map(([x, y]) => x.toFixed(4) + ',' + y.toFixed(4)).join(';')].join('|'));
lines.push(['CAM', T.cam.cx.toFixed(4), T.cam.cy.toFixed(4), T.cam.w.toFixed(4), T.cam.h.toFixed(4)].join('|'));
lines.push(['SKY', protoPath['PROP_FE_Sky']].join('|'));
fs.writeFileSync(__dirname + '/build_data.txt', lines.join('\n') + '\n');
fs.writeFileSync(__dirname + '/installed_files.txt', used.join('\n') + '\n');
console.log('lines', lines.length, 'files', used.length, doPng ? '(png+meta copied)' : '(dry)');
