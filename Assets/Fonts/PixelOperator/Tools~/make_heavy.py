# Pixel Operator TR Bold -> Pixel Operator TR Heavy: her glifin dikey çizgileri 1 font pikseli sağa kalınlaşır
# (Regular -> Bold'un da yöntemi: 1 px -> 2 px; burada 2 px -> 3 px). Glifler 100 birimlik ızgarada dikdörtgenlerden
# oluştuğu için sonuç yine tam piksel ızgarasındadır. Boşluk hariç her glifin genişliği 1 piksel artar (harf arası korunur).
import sys
sys.path.insert(0, sys.argv[1])
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen

src, dst = sys.argv[2], sys.argv[3]
P = 100
font = TTFont(src)
gs = font.getGlyphSet()
glyf, hmtx = font['glyf'], font['hmtx']

def polys(name):
    pen = DecomposingRecordingPen(gs)
    gs[name].draw(pen)
    out, cur = [], []
    for op, args in pen.value:
        if op == 'moveTo': cur = [args[0]]
        elif op == 'lineTo': cur.append(args[0])
        elif op in ('qCurveTo', 'curveTo'): raise ValueError(name + ': eğri var')
        elif op in ('closePath', 'endPath'):
            if cur: out.append(cur)
            cur = []
    return out

def inside(x, y, ps):
    w = 0
    for p in ps:
        n = len(p)
        for i in range(n):
            x1, y1 = p[i]; x2, y2 = p[(i + 1) % n]
            if y1 <= y < y2 and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1): w += 1
            elif y2 <= y < y1 and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1): w -= 1
    return w != 0

def pixels(name):
    ps = polys(name)
    pts = [pt for p in ps for pt in p]
    if not pts: return set()
    for x, y in pts:
        assert x % P == 0 and y % P == 0, (name, x, y)
    x0, x1 = int(min(p[0] for p in pts)) // P, int(max(p[0] for p in pts)) // P
    y0, y1 = int(min(p[1] for p in pts)) // P, int(max(p[1] for p in pts)) // P
    return {(cx, cy) for cy in range(y0, y1) for cx in range(x0, x1) if inside(cx * P + 50, cy * P + 50, ps)}

def build(px):
    pen = TTGlyphPen(None)
    rows = {}
    for x, y in px: rows.setdefault(y, set()).add(x)
    for y in sorted(rows):
        xs = sorted(rows[y]); i = 0
        while i < len(xs):
            j = i
            while j + 1 < len(xs) and xs[j + 1] == xs[j] + 1: j += 1
            a, b = xs[i] * P, (xs[j] + 1) * P
            pen.moveTo((a, (y + 1) * P)); pen.lineTo((b, (y + 1) * P)); pen.lineTo((b, y * P)); pen.lineTo((a, y * P)); pen.closePath()
            i = j + 1
    return pen.glyph()

def heavy(px):
    """Dikey çizgileri sağa 1 piksel kalınlaştırır. Kalınlaşma 1 px'lik bir boşluğu kapatacaksa (m, w, k, 0 ...)
    o sütun kopyalanır: çizgi yine 3 px olur, boşluk korunur, glif 1 px genişler. Döner: (pikseller, eklenen sütun)."""
    px = set(px)
    done = set()  # sütun kopyalanarak zaten kalınlaşmış çizgi uçları
    added = 0
    while True:
        blocked = [x for x, y in px if (x + 1, y) not in px and (x + 2, y) in px and (x, y) not in done]
        if not blocked: break
        c = max(blocked)
        old = px
        px = {(x + 1 if x > c else x, y) for x, y in old} | {(c + 1, y) for x, y in old if x == c}
        done = {(x + 1 if x > c else x, y) for x, y in done} | {(c + 1, y) for x, y in old if x == c}
        added += 1
    grown = {(x + 1, y) for x, y in px if (x + 1, y) not in px and (x, y) not in done and (x + 2, y) not in px}
    return px | grown, added

new = {}
widened = {}
for name in font.getGlyphOrder():
    px = pixels(name)
    if not px: continue
    hpx, added = heavy(px)
    new[name] = build(hpx)
    widened[name] = added
for name, g in new.items():
    glyf[name] = g
    g.recalcBounds(glyf)
    adv, _ = hmtx[name]
    hmtx[name] = (adv + P * (1 + widened[name]), g.xMin)
print('sütun eklenen glif:', sum(1 for v in widened.values() if v))

os2 = font['OS/2']
os2.usWeightClass = 900
for rec in font['name'].names:
    s = rec.toUnicode()
    s2 = s.replace('PixelOperatorTR-Bold', 'PixelOperatorTR-Heavy').replace('Pixel Operator TR Bold', 'Pixel Operator TR Heavy')
    if rec.nameID == 2: s2 = 'Regular' if s == 'Bold' else s2
    if rec.nameID == 1: s2 = 'Pixel Operator TR Heavy'
    if rec.nameID == 5: s2 = s + '; Heavy: strokes +1px'
    if s2 != s: rec.string = s2
font.save(dst)
chk = TTFont(dst)
print('glif:', len(new), 'ad:', chk['name'].getDebugName(1), '/', chk['name'].getDebugName(4))
