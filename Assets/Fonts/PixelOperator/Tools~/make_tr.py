# Pixel Operator Bold (CC0) -> Pixel Operator TR Bold: eksik Türkçe harfleri (Ğ ğ İ Ş ş)
# fontun kendi parçalarından (S/s/G/g/I + kendi cedilla/breve glifleri) üretir. Başka hiçbir glif değişmez.
import sys
sys.path.insert(0, sys.argv[1])
from fontTools.ttLib import TTFont
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.transformPen import TransformPen

src, dst = sys.argv[2], sys.argv[3]
font = TTFont(src)
gs = font.getGlyphSet()
cmap = font.getBestCmap()
P = 100  # 1 piksel = 100 birim (upm 1600 = 16 px)

def rect(pen, x0, y0, x1, y1):
    # Ö'deki nokta dikdörtgenleriyle aynı yön (saat yönü)
    pen.moveTo((x0, y1)); pen.lineTo((x1, y1)); pen.lineTo((x1, y0)); pen.lineTo((x0, y0)); pen.closePath()

def build(base, parts):
    pen = TTGlyphPen(gs)
    gs[base].draw(pen)
    for kind, arg, dx, dy in parts:
        if kind == 'glyph':
            gs[arg].draw(TransformPen(pen, (1, 0, 0, 1, dx, dy)))
        else:
            rect(pen, *arg)
    return pen.glyph()

C, BREVE = 'cedilla', 'breve'
# Ç: cedilla, C'nin altında x+100'de (bkz. Ccedilla konturu). Ş/ş aynı yerleşimi kullanır.
# Ö: nokta büyük harfte y 1000-1100 (bir boş satır); ö: y 800-900. breve 2 satır, kendi glifi y 700-900.
new = {
    0x015E: ('Scedilla', 'S', [('glyph', C, P, 0)]),
    0x015F: ('scedilla', 's', [('glyph', C, P, 0)]),
    0x011E: ('Gbreve', 'G', [('glyph', BREVE, 0, 3 * P)]),   # y 1000-1200, É'nin akut işaretiyle aynı yükseklik
    0x011F: ('gbreve', 'g', [('glyph', BREVE, 0, 1 * P)]),   # y 800-1000, x-yüksekliğinden bir boş satır
    0x0130: ('Idotaccent', 'I', [('rect', (2 * P, 10 * P, 4 * P, 11 * P), 0, 0)]),  # Ö noktasıyla aynı satır
}

# Arayüzde görünen ama fontta olmayan simgeler: piksel çizim (üst satır en üstte, '#' = 1 piksel, alt satır y=base_row).
icons = {
    0x2192: ('arrowright', 8, ['....#...',
                               '....##..',
                               '.######.',
                               '.######.',
                               '....##..',
                               '....#...'], 1),
    0x2190: ('arrowleft', 8, ['...#....',
                              '..##....',
                              '.######.',
                              '.######.',
                              '..##....',
                              '...#....'], 1),
    0x25B6: ('triagrt', 6, ['.#....',
                            '.##...',
                            '.###..',
                            '.####.',
                            '.###..',
                            '.##...',
                            '.#....'], 0),
    0x25BC: ('triagdn', 9, ['.#######.',
                            '..#####..',
                            '...###...',
                            '....#....'], 2),
    0x2713: ('checkmark', 9, ['.......##',
                              '......##.',
                              '.##..##..',
                              '..####...',
                              '...##....'], 1),
    0x2717: ('ballotx', 8, ['.##...##',
                            '..##.##.',
                            '...###..',
                            '..##.##.',
                            '.##...##'], 1),
}

def pixel_glyph(rows, base_row):
    pen = TTGlyphPen(None)
    top = base_row + len(rows) - 1
    for i, row in enumerate(rows):
        y = (top - i) * P
        x = 0
        while x < len(row):
            if row[x] == '#':
                x1 = x
                while x1 < len(row) and row[x1] == '#': x1 += 1
                rect(pen, x * P, y, x1 * P, y + P)
                x = x1
            else:
                x += 1
    return pen.glyph()

glyf, hmtx = font['glyf'], font['hmtx']
order = list(font.getGlyphOrder())
for cp, (name, base, parts) in new.items():
    assert cp not in cmap, f'U+{cp:04X} zaten var'
    g = build(base, parts)
    glyf[name] = g
    g.recalcBounds(glyf)
    hmtx[name] = (hmtx[base][0], g.xMin)
    order.append(name)
for cp, (name, width, rows, base_row) in icons.items():
    assert cp not in cmap, f'U+{cp:04X} zaten var'
    g = pixel_glyph(rows, base_row)
    glyf[name] = g
    g.recalcBounds(glyf)
    hmtx[name] = (width * P, g.xMin)
    order.append(name)
    new[cp] = (name, None, None)
font.setGlyphOrder(order)
glyf.setGlyphOrder(order)
for table in font['cmap'].tables:
    if table.isUnicode():
        for cp, (name, _, _) in new.items():
            table.cmap[cp] = name

# OS/2: Latin Extended-A (ulUnicodeRange1 bit 3) ve Türkçe kod sayfası 1254 (ulCodePageRange1 bit 4)
os2 = font['OS/2']
os2.ulUnicodeRange1 |= 1 << 3
if hasattr(os2, 'ulCodePageRange1'):
    os2.ulCodePageRange1 |= 1 << 4

# Ad: orijinalle karışmasın diye "Pixel Operator TR"
for rec in font['name'].names:
    s = rec.toUnicode()
    s2 = s.replace('PixelOperator-Bold', 'PixelOperatorTR-Bold').replace('Pixel Operator', 'Pixel Operator TR')
    if rec.nameID == 5:
        s2 = s + '; TR: Gbreve gbreve Idotaccent Scedilla scedilla + UI icons added (2026)'
    if s2 != s:
        rec.string = s2
font.save(dst)

# Doğrulama
chk = TTFont(dst)
cm = chk.getBestCmap()
need = 'ÇçĞğİıÖöŞşÜü'
print('eksik:', [c for c in need if ord(c) not in cm] or 'yok')
print('ad:', chk['name'].getDebugName(1), '/', chk['name'].getDebugName(4))
