// Deprem kitaplık mini oyunu: düşen kitap sprite'ları (BookshelfAnchorView.fallingBooks). 8x6 px, pivot merkez (mevcut .meta korunur).
//   node tools/deprem_kitaplik/build_books.js [çıktı klasörü]   → UI_EQ_Shelf_Book_A/B/C.png (yalnız PNG yazar, .meta'ya dokunmaz)
// Eski hâli düz renkli dikdörtgendi (çerçeve 8x6'nın dışına taştığı için kırpılıyordu). Yeni: koyu kontur, yuvarlatılmış köşe,
// 3 kapak tonu + sayfa bloğu / sırt şeritleri. A = kırmızı (sayfa tarafı), B = mavi (sırt, altın şeritler), C = yeşil (sayfa + ayraç).
'use strict';
const fs = require('fs'), path = require('path');
const { encodePng } = require('../sel_aksam_harita/lib/roomB_png.js');
const ROOT = path.resolve(__dirname, '../..');
const OUT = process.argv[2] || path.join(ROOT, 'Assets/Art/UI/Earthquake/Bookshelf');
const hex = h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16), 255];
const PAL = {
  O: hex('#14141F'), P: hex('#E9E4D8'), Q: hex('#C8C2B4'), Y: hex('#F2C230'), R: hex('#C0392B'),
};
const COVERS = { A: ['#E0604F', '#C0392B', '#8E2A20'], B: ['#5B86CC', '#2F5DA8', '#1F3F7A'], C: ['#7CC77E', '#3E8E41', '#2E6B31'] };
// L = açık, M = ana, D = koyu kapak tonu; '.' saydam
const ART = {
  A: ['.OOOOOO.',
      'OLLLLLLO',
      'OMPPPPPO',
      'OMQQQQQO',
      'ODDDDDDO',
      '.OOOOOO.'],
  B: ['.OOOOOO.',
      'OLLLLLLO',
      'OMYMMYMO',
      'OMYMMYMO',
      'ODDDDDDO',
      '.OOOOOO.'],
  C: ['.OOOOOO.',
      'OLLLLLLO',
      'OPPPRPMO',
      'OQQQRQMO',
      'ODDDDDDO',
      '.OOOOOO.'],
};
for (const k of Object.keys(ART)) {
  const rows = ART[k], w = rows[0].length, h = rows.length, d = new Uint8Array(w * h * 4);
  const [L, M, D] = COVERS[k].map(hex);
  const col = { L, M, D, ...PAL };
  rows.forEach((r, y) => [...r].forEach((ch, x) => { if (ch === '.') return; d.set(col[ch], (y * w + x) * 4); }));
  const file = path.join(OUT, 'UI_EQ_Shelf_Book_' + k + '.png');
  fs.writeFileSync(file, encodePng(w, h, d)); console.log('yazıldı', file);
}
