// Deprem — annenin Çök–Kapan–Tutun pozu (güneye bakar, 2 kare: hafif titreme). Annenin güney yürüyüş karesinden (kare 0)
// türetilir: baş öne eğik aşağı iner, gövde kısalır, dizler bükülü, iki kol başın ve ensenin üstünü kapatır.
// Renkler yalnız annenin kendi paletinden; 96x96 kare, PPU 64, pivot (0.5, 0.1875) yürüyüşle aynı → poz değişince kaymaz.
//   node tools/deprem_anne/build_mother_cover.js   → Assets/Art/Characters/CHR_Mother_Cover_South_0/1.png (.meta Unity'den)
'use strict';
const fs = require('fs'), path = require('path');
const C = require('../sel_aksam_harita/common.js');
const ROOT = path.resolve(__dirname, '../..').split(path.sep).join('/');
const SRC = ROOT + '/Assets/Art/Characters/CHR_Mother_Walk_South.png';
const DST = process.env.MOTHER_OUT || (ROOT + '/Assets/Art/Characters');
const sheet = C.decodePng(fs.readFileSync(SRC));
const src = (x, y) => { if (x < 0 || y < 0 || x >= 96 || y >= 96) return null; const o = (y * sheet.w + x) * 4; return sheet.rgba[o + 3] ? [sheet.rgba[o], sheet.rgba[o + 1], sheet.rgba[o + 2]] : null; };

// palet (kaynak karedeki renkler)
const P = {
  ol: [0, 0, 0], ol2: [11, 4, 24],
  red: [232, 90, 77], redL: [201, 68, 69], redM: [178, 57, 68], redD: [164, 51, 67], redDD: [116, 31, 58], redX: [85, 15, 43],
  cuff: [232, 217, 216], cuffD: [216, 195, 202],
  skin: [248, 194, 157], skinM: [226, 150, 119], skinD: [218, 137, 110], skinDD: [190, 105, 84],
  pant: [57, 66, 87], pantM: [50, 56, 78], pantD: [46, 48, 72], pantDD: [29, 22, 49], pantX: [39, 34, 61],
};

function frame(dx) {
  const cv = new C.Cv(96, 96);
  const put = (x, y, c) => { if (c) cv.set(x + dx, y, c); };
  const blitRows = (y0, y1, x0, x1, dy, ddx = 0) => { for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) put(x + ddx, y + dy, src(x, y)); };

  // 1) ayakkabılar (yürüyüş satırı 69–76) → çömelmiş ayaklar 72–76, iki ayak yan yana
  blitRows(69, 73, 39, 46, 3, -1);   // sol ayak
  blitRows(70, 76, 48, 54, 0, 1);    // sağ ayak
  // 2) bükülü bacaklar / dizler: lacivert yamuk (satır 63–72), dizler yanlara taşar
  for (let y = 63; y <= 72; y++) {
    const half = y < 66 ? 10 + (y - 63) : (y < 70 ? 13 : 12 - (y - 70));
    for (let x = 48 - half; x <= 48 + half; x++) {
      const e = Math.abs(x - 48);
      let c = P.pant;
      if (e >= half - 1) c = P.pantDD; else if (e >= half - 3) c = P.pantD; else if (x > 48 && e > 3) c = P.pantM;
      if (y >= 70 && e < 2) c = P.pantX; // bacak arası gölge
      put(x, y, c);
    }
  }
  // 3) gövde: gömlek (kaynak 39–50) öne eğik, aşağıda (satır 54–64), kolların yerini sonra kollar kaplar
  blitRows(39, 50, 38, 58, 15);
  // 4) baş: kaynak 17–37 → 7 satır aşağı ve öne eğik (yüz kısmen kolların arasında)
  blitRows(17, 37, 37, 59, 21);
  // 5) kollar: omuzdan (gövde yanı) dirseğe yukarı, önkol başın üstünde birleşir (eller ensede/başta)
  const thick = (x0, y0, x1, y1, w, col) => {
    const n = Math.max(Math.abs(x1 - x0), Math.abs(y1 - y0)) * 2 + 1;
    for (let i = 0; i <= n; i++) { const t = i / n, x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
      for (let oy = 0; oy < w; oy++) for (let ox = 0; ox < w; ox++) put(Math.round(x - w / 2 + ox), Math.round(y - w / 2 + oy), col(ox, oy)); }
  };
  const sleeve = (ox) => ox === 0 ? P.redD : (ox >= 3 ? P.redM : P.red);
  const skinC = (ox, oy) => oy === 0 ? P.skin : (ox >= 2 ? P.skinD : P.skinM);
  // üst kol (kırmızı kol) — sol / sağ
  thick(38, 58, 35, 47, 5, sleeve);
  thick(58, 58, 61, 47, 5, (ox) => sleeve(4 - ox));
  // dirsek manşetleri
  thick(35, 48, 35, 46, 4, () => P.cuff); thick(61, 48, 61, 46, 4, () => P.cuffD);
  // önkollar başın üstüne (saç tepesi ≈ satır 38–42) ve eller ortada
  thick(36, 46, 44, 42, 4, skinC);
  thick(60, 46, 52, 42, 4, skinC);
  for (let x = 44; x <= 52; x++) for (let y = 40; y <= 43; y++) put(x, y, y === 40 ? P.skin : (y === 43 ? P.skinDD : (x === 48 ? P.skinD : P.skinM)));
  // parmak çizgileri
  put(46, 41, P.skinDD); put(50, 41, P.skinDD);
  // 6) dış hat (saydam pikselin komşusu → siyah)
  cv.outline('#000000');
  return cv;
}

fs.mkdirSync(DST, { recursive: true });
[0, 1].forEach(i => { const cv = frame(i); const f = `${DST}/CHR_Mother_Cover_South_${i}.png`; cv.save(f); console.log('yazıldı', f); });
