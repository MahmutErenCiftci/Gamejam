# "EVE DÖNÜŞ" sel açılış kapağının piksel çizimleri (320x180; kapakta 640x360 tuvale 2 kat, Point filtre).
# Saf Python (zlib + struct), ek kütüphane gerekmez. Çalıştır:  python make_flood_title.py <çıkış klasörü>
#   FT_Background.png   : gökyüzü, uzak binalar, okul + kapı, otobüs durağı, lambalar, kaldırım, yol (sabit)
#   FT_Puddles_0..7.png : büyüyen su birikintileri + suda lamba yansımaları (şeffaf, kare kare)
#   FT_Child.png        : ön planda sırt çantalı çocuk silüeti (şeffaf)
#   FT_Flash.png        : uzak şimşekte aydınlanan bulut kenarları (şeffaf; kapak alfasıyla oynatır)
# Hepsi aynı 320x180 tuvalde çizilir, kapakta üst üste tam ekran gerilir (yerleşim dosyada gömülü).
import os, struct, sys, zlib, math

W, H = 320, 180
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def C(h, a=255):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def dith(x, y, t):
    """t (0..1) oranında piksel doldurur: 4x4 Bayer düzeni (piksel sanatı geçişi)."""
    return (BAYER[y & 3][x & 3] + 0.5) / 16.0 < t


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3)) + (a[3],)


class Img:
    def __init__(self, fill=(0, 0, 0, 0)):
        self.p = [[fill] * W for _ in range(H)]

    def set(self, x, y, c):
        if 0 <= x < W and 0 <= y < H:
            self.p[y][x] = c

    def get(self, x, y):
        return self.p[y][x]

    def rect(self, x0, y0, x1, y1, c):  # [x0,x1) [y0,y1)
        for y in range(max(0, y0), min(H, y1)):
            row = self.p[y]
            for x in range(max(0, x0), min(W, x1)):
                row[x] = c

    def hline(self, x0, x1, y, c):
        self.rect(x0, y, x1, y + 1, c)

    def vline(self, x, y0, y1, c):
        self.rect(x, y0, x + 1, y1, c)

    def glow(self, cx, cy, r, col, strength, ymin=0, ymax=H, squash=1.0):
        """Işık halesi: iç içe düz halkalar (piksel sanatı kademeleri; yumuşatma ve benek yok)."""
        for y in range(max(ymin, int(cy - r)), min(ymax, int(cy + r) + 1)):
            for x in range(max(0, int(cx - r * squash)), min(W, int(cx + r * squash) + 1)):
                d = math.hypot((x - cx) / squash, y - cy) / r
                if d >= 1:
                    continue
                base = self.p[y][x]
                if base[3] == 0:
                    continue
                step = 3 if d < 0.35 else 2 if d < 0.65 else 1
                self.p[y][x] = mix(base, col, strength * (0.07, 0.14, 0.24)[step - 1])

    def save(self, path):
        raw = b''.join(b'\x00' + bytes(v for px in row for v in px) for row in self.p)

        def chunk(t, d):
            return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)

        with open(path, 'wb') as f:
            f.write(b'\x89PNG\r\n\x1a\n')
            f.write(chunk(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 6, 0, 0, 0)))
            f.write(chunk(b'IDAT', zlib.compress(raw, 9)))
            f.write(chunk(b'IEND', b''))


def noise(i, seed=0):
    """Deterministik 0..1 gürültü (her çalıştırmada aynı çizim)."""
    v = math.sin(i * 12.9898 + seed * 78.233) * 43758.5453
    return v - math.floor(v)


# ---------------------------------------------------------------- palet (gece, yağmur, düşük kontrast)
SKY = [C('#0b1220'), C('#0f1828'), C('#141f32'), C('#19263b'), C('#1e2c42')]
CLOUD = C('#172235'); CLOUD_HI = C('#1d2a40'); CLOUD_LIT = C('#33445f'); CLOUD_LIT_HI = C('#4b5f80')
CITY = C('#121c2c'); CITY_WIN = C('#1f2b40'); CITY_WARM = C('#4a412f')
WALL = C('#46413f'); WALL_SH = C('#3b3735'); ROOF = C('#4a2624'); ROOF_HI = C('#5d2f2b'); PLINTH = C('#3a2522')
WIN_DARK = C('#1d283a'); WIN_LIT = C('#7a6640'); WIN_LIT_HI = C('#8f7849'); WIN_FRAME = C('#57524d')
SIGN = C('#25466d'); SIGN_TXT = C('#8fa6c2')
BRICK = C('#55302a'); BRICK_SH = C('#45261f'); CAP = C('#5b5c63')
LAMP_WARM = C('#f0cf86'); LAMP_GLOW = C('#d9b56c'); LAMP_POLE = C('#273142'); LAMP_HEAD = C('#384356')
IRON = C('#1a2433')
WALK_FAR = C('#262f3e'); WALK_FAR_HI = C('#2e3848'); CURB_TOP = C('#394456'); CURB_FACE = C('#192030')
ROAD = C('#121925'); ROAD_WET = C('#18202e'); ROAD_LINE = C('#353d4c')
WALK_NEAR = C('#212a37'); WALK_NEAR_J = C('#1b222e'); WALK_NEAR_HI = C('#28313f')
STOP_FRAME = C('#3a495c'); STOP_ROOF = C('#45556a'); STOP_GLASS = C('#1c2c41'); STOP_GLASS_HI = C('#2a405b')
AD_LIT = C('#2f6a6c'); AD_LIT_HI = C('#3f8584'); BUS_SIGN = C('#2c63a0'); BUS_SIGN_TXT = C('#c9d6e4')
WATER = C('#1b2a40'); WATER_SKY = C('#22344e'); WATER_EDGE = C('#2f425d')
REF_WARM = C('#b4975a'); REF_WARM_LO = C('#6f6346'); REF_TEAL = C('#3c8282'); REF_TEAL_LO = C('#2a5458')
KID = C('#080c13'); KID_RIM = C('#253349'); KID_BAG = C('#0c121c')

HORIZON = 116      # uzak kaldırımın üstü
ROAD_TOP = 124
ROAD_BOT = 156

# Işık kaynakları (yansımalar için): x, renk, ışığın yüksekliği
LAMP_A = 198       # durak önündeki sokak lambası (uzak kaldırım)
LAMP_B = 304
GATE_L, GATE_R = 124, 149


def draw_sky(img):
    for y in range(0, HORIZON):
        t = y / (HORIZON - 1) * (len(SKY) - 1)
        i = min(int(t), len(SKY) - 2)
        f = t - i
        for x in range(W):
            img.set(x, y, SKY[i + 1] if dith(x, y, f) else SKY[i])


CLOUD_LAYERS = [C('#111a2a'), C('#141e30'), C('#172236')]
CLOUD_BELLY = C('#1d2a3f')
RAIN_FAR = C('#1c283c')


def cloud_mask():
    """Üstte ağır yağmur bulutları: alt kenarı yuvarlak tümsekli katmanlar. Döner: {(x,y): katman}."""
    m = {}
    layers = [(16, 0, 7), (26, 1, 6), (34, 2, 5)]
    for base, li, count in layers:
        for x in range(W):
            # farklı boyda, düzensiz aralıklı tümseklerin zarfı: kenar tırtıklı değil, perde gibi tekrar etmez
            edge = base
            for b in range(count):
                bx = int(noise(b * 7 + 1, li + 21) * (W + 60)) - 30
                br = 14 + 26 * noise(b, li + 3)
                dx = (x - bx) / br
                if abs(dx) < 1:
                    edge = max(edge, base + int(br * 0.55 * math.sqrt(1 - dx * dx)))
            for y in range(0, edge):
                m[(x, y)] = max(m.get((x, y), -1), li)
    return m


def draw_clouds(img, flash=None):
    m = cloud_mask()
    for (x, y), li in m.items():
        below = m.get((x, y + 1), -1)
        c = CLOUD_LAYERS[li]
        if below < li:
            c = CLOUD_BELLY  # bulut alt kenarı: 1 piksel açık çizgi
        img.set(x, y, c)
        if flash is not None:
            if below < li:
                flash.set(x, y, CLOUD_LIT_HI)
            elif m.get((x, y + 2), -1) < li or m.get((x, y + 3), -1) < li:
                flash.set(x, y, CLOUD_LIT)
            elif li == 2:
                flash.set(x, y, (CLOUD_LIT[0], CLOUD_LIT[1], CLOUD_LIT[2], 110))
    # uzakta bulutlardan inen yağmur perdeleri: ince, sağa yatık, gökyüzüyle neredeyse aynı ton
    for i in range(46):
        x0 = int(noise(i, 11) * (W + 60)) - 30
        y0 = 30 + int(noise(i, 12) * 40)
        ln = 8 + int(noise(i, 13) * 14)
        for k in range(ln):
            x, y = x0 + k // 3, y0 + k
            if 0 <= x < W and y < HORIZON and m.get((x, y), -1) < 0:
                img.set(x, y, RAIN_FAR)


def draw_city(img):
    # Uzak şehir: okulun arkasında ve sağda alçak bloklar (ortadaki başlığın arkası sakin kalsın)
    blocks = [(0, 22, 88), (20, 40, 92), (118, 136, 98), (136, 170, 94), (170, 188, 100), (188, 222, 90),
              (222, 246, 96), (246, 268, 86), (268, 296, 93), (296, 320, 84)]
    for i, (x0, x1, top) in enumerate(blocks):
        img.rect(x0, top, x1, HORIZON, CITY)
        for wy in range(top + 3, HORIZON - 4, 5):
            for wx in range(x0 + 2, x1 - 2, 4):
                n = noise(wx * 31 + wy * 7, i)
                if n < 0.18:
                    img.set(wx, wy, CITY_WARM)
                elif n < 0.55:
                    img.set(wx, wy, CITY_WIN)
        # çatı ayrıntısı: su deposu / anten
        if i % 3 == 1:
            img.rect(x0 + 4, top - 3, x0 + 8, top, CITY)
        if i % 4 == 2:
            img.vline(x1 - 5, top - 6, top, CITY)


def draw_school(img):
    x0, x1 = 6, 118
    roof_y, wall_y, base_y = 72, 77, HORIZON
    img.rect(x0, wall_y, x1, base_y, WALL)
    for y in range(wall_y, base_y, 4):  # sıva bantları
        img.hline(x0, x1, y, WALL_SH) if y % 8 == 1 else None
    img.rect(x0 - 2, roof_y, x1 + 2, wall_y, ROOF)
    img.hline(x0 - 2, x1 + 2, roof_y, ROOF_HI)
    img.hline(x0, x1, wall_y, WALL_SH)
    img.rect(x0, base_y - 5, x1, base_y, PLINTH)
    img.hline(x0, x1, base_y - 5, BRICK_SH)
    # iki kat pencere
    lit = {(0, 1), (0, 4), (1, 2), (1, 6), (0, 7), (1, 0)}
    for row, wy in enumerate((81, 95)):
        for col in range(8):
            wx = x0 + 6 + col * 13
            if 46 <= wx <= 70 and row == 1:
                continue  # giriş kapısının üstü
            img.rect(wx - 1, wy - 1, wx + 9, wy + 9, WIN_FRAME)
            on = (row, col) in lit
            img.rect(wx, wy, wx + 8, wy + 8, WIN_LIT if on else WIN_DARK)
            img.vline(wx + 4, wy, wy + 8, WIN_FRAME)
            img.hline(wx, wx + 8, wy + 3, WIN_FRAME)
            if on:
                img.rect(wx, wy + 4, wx + 4, wy + 8, WIN_LIT_HI)
                img.glow(wx + 4, wy + 4, 9, WIN_LIT, 0.5, ymax=HORIZON)
    # giriş: tabela + kapı + saçak
    img.rect(46, 79, 72, 85, SIGN)
    for i, x in enumerate(range(48, 70)):
        if noise(x, 5) < 0.6:
            img.set(x, 82 if i % 3 else 81, SIGN_TXT)
    img.rect(50, 101, 68, base_y - 1, WIN_FRAME)
    img.rect(52, 103, 66, base_y - 1, WIN_LIT)
    img.vline(59, 103, base_y - 1, WIN_FRAME)
    img.rect(46, 98, 72, 100, LAMP_HEAD)
    img.glow(59, 108, 14, WIN_LIT, 0.7, ymax=HORIZON + 6)


def draw_fence_and_gate(img):
    # okul bahçe çiti (okulun önünde) ve kapıdan sağa bir parça
    for x in list(range(0, GATE_L)) + list(range(GATE_R + 6, 176)):
        if x % 3 == 0:
            img.vline(x, 106, HORIZON, IRON)
    img.hline(0, GATE_L, 107, IRON)
    img.hline(GATE_R + 6, 176, 107, IRON)
    # tuğla kapı ayakları + taş başlık + üstte sarı lamba (okul kapısı çizimi gibi)
    for px in (GATE_L, GATE_R):
        img.rect(px, 96, px + 6, HORIZON + 1, BRICK)
        for y in range(98, HORIZON, 3):
            img.hline(px, px + 6, y, BRICK_SH)
            img.set(px + (3 if (y // 3) % 2 else 1), y + 1, BRICK_SH)
        img.rect(px - 1, 94, px + 7, 96, CAP)
        img.rect(px + 1, 91, px + 5, 94, LAMP_WARM)
        img.hline(px + 2, px + 4, 90, LAMP_WARM)
        img.glow(px + 3, 92, 11, LAMP_GLOW, 0.75, ymax=HORIZON + 8)
    # açık kapı kanatları (içeri dönük, ince demir)
    for i, x in enumerate(range(GATE_L + 6, GATE_L + 10)):
        img.vline(x, 104 - i, HORIZON, IRON)
    for i, x in enumerate(range(GATE_R - 4, GATE_R)):
        img.vline(x, 101 + i, HORIZON, IRON)
    # bahçe yolu (kapıdan okula)
    img.rect(GATE_L + 6, HORIZON - 2, GATE_R, HORIZON, WALK_FAR_HI)


def draw_lamp(img, x, top, arm_left=True):
    img.rect(x, top, x + 2, HORIZON + 2, LAMP_POLE)
    img.rect(x - 1, HORIZON - 1, x + 3, HORIZON + 2, LAMP_POLE)
    d = -1 if arm_left else 1
    ax = x - 6 if arm_left else x + 2
    img.hline(ax, ax + 6, top, LAMP_POLE)
    hx = x - 9 if arm_left else x + 5
    img.rect(hx, top - 1, hx + 6, top + 2, LAMP_HEAD)
    img.hline(hx + 1, hx + 5, top + 2, LAMP_WARM)
    img.glow(hx + 3, top + 3, 14, LAMP_GLOW, 0.75, ymax=ROAD_TOP + 10)
    return hx + 3


def draw_light_pools(img):
    """Lambaların kaldırımdaki ışık havuzu (iki düz kademe) ve ıslak asfaltta uzayan soluk yansıma çizgileri."""
    for lx, kind in LIGHTS:
        col = LAMP_GLOW if kind == 'warm' else AD_LIT_HI
        if lx not in (LAMP_A - 6, LAMP_B - 6):
            continue
        for y in range(HORIZON, ROAD_TOP - 3):
            for x in range(lx - 20, lx + 21):
                d = abs(x - lx) / 20 + (y - HORIZON) / 14
                if 0 <= x < W and d < 1:
                    img.set(x, y, mix(img.get(x, y), col, 0.16 if d < 0.55 else 0.08))
    for lx, kind in LIGHTS:
        hi = REF_WARM_LO if kind == 'warm' else REF_TEAL_LO
        for k, y in enumerate(range(ROAD_TOP, ROAD_TOP + 22)):
            if k % 3 == 2:
                continue
            fade = 0.55 * (1 - k / 22)
            for x in (lx, lx + 1):
                xx = x + (1 if k % 4 == 1 else 0)
                if 0 <= xx < W:
                    img.set(xx, y, mix(img.get(xx, y), hi, fade))


def draw_bus_stop(img):
    x0, x1, top = 226, 270, 92
    img.rect(x0 - 2, top, x1 + 2, top + 3, STOP_ROOF)
    img.hline(x0 - 2, x1 + 2, top + 3, STOP_FRAME)
    img.rect(x0, top + 4, x1, HORIZON, STOP_GLASS)
    for x in (x0, x0 + 1, x1 - 1, x1 - 2):
        img.vline(x, top + 3, HORIZON + 1, STOP_FRAME)
    for i in range(3):  # cam yansıma çizgileri
        for k in range(10):
            img.set(x0 + 5 + i * 9 + k // 2, top + 6 + k, STOP_GLASS_HI)
    # ışıklı reklam panosu (camgöbeği, turkuaz vurguyla akraba)
    img.rect(252, top + 6, 266, HORIZON - 4, STOP_FRAME)
    img.rect(253, top + 7, 265, HORIZON - 5, AD_LIT)
    img.rect(255, top + 9, 263, top + 13, AD_LIT_HI)
    img.hline(255, 261, top + 15, AD_LIT_HI)
    img.glow(259, top + 14, 12, AD_LIT_HI, 0.8, ymax=HORIZON + 6)
    # bank
    img.rect(x0 + 4, HORIZON - 7, x0 + 22, HORIZON - 5, STOP_FRAME)
    img.vline(x0 + 6, HORIZON - 5, HORIZON, STOP_FRAME)
    img.vline(x0 + 20, HORIZON - 5, HORIZON, STOP_FRAME)
    # durak tabelası: direk + yuvarlak mavi levha, içinde otobüs simgesi
    sx = 278
    img.rect(sx, 88, sx + 2, HORIZON + 1, LAMP_POLE)
    for y in range(-4, 5):
        for x in range(-4, 5):
            if x * x + y * y <= 18:
                img.set(sx + 1 + x, 84 + y, BUS_SIGN)
    img.rect(sx - 2, 82, sx + 4, 86, BUS_SIGN_TXT)
    img.hline(sx - 1, sx + 3, 83, BUS_SIGN)
    img.set(sx - 1, 86, BUS_SIGN_TXT); img.set(sx + 3, 86, BUS_SIGN_TXT)


def draw_ground(img):
    # uzak kaldırım
    img.rect(0, HORIZON, W, ROAD_TOP - 3, WALK_FAR)
    img.hline(0, W, HORIZON, WALK_FAR_HI)
    for x in range(0, W, 14):
        img.vline(x, HORIZON + 1, ROAD_TOP - 3, CURB_FACE)
    img.hline(0, W, ROAD_TOP - 3, CURB_TOP)
    img.rect(0, ROAD_TOP - 2, W, ROAD_TOP, CURB_FACE)
    # ıslak asfalt: seyrek parlak lekeler, kesik orta çizgi
    img.rect(0, ROAD_TOP, W, ROAD_BOT, ROAD)
    for y in range(ROAD_TOP, ROAD_BOT):
        for x in range(W):
            n = noise(x // 5 + y * 97, 9)
            if n < 0.22 and dith(x, y, 0.5):
                img.set(x, y, ROAD_WET)
    for x in range(4, W, 22):
        img.hline(x, x + 12, 140, ROAD_LINE)
    # yakın kaldırım
    img.hline(0, W, ROAD_BOT, CURB_TOP)
    img.rect(0, ROAD_BOT + 1, W, ROAD_BOT + 3, CURB_FACE)
    img.rect(0, ROAD_BOT + 3, W, H, WALK_NEAR)
    img.hline(0, W, ROAD_BOT + 3, WALK_NEAR_HI)
    for y in (166, 176):
        img.hline(0, W, y, WALK_NEAR_J)
    for x in range(0, W, 24):
        img.vline(x + (12 if x % 48 else 0), ROAD_BOT + 4, 166, WALK_NEAR_J)
        img.vline(x + (0 if x % 48 else 12), 167, 176, WALK_NEAR_J)


# ---------------------------------------------------------------- su birikintileri
# cx, cy, yarıçap x, yarıçap y (son halleri). Lambaların altındakiler yansıma taşır.
PUDDLES = [(194, 133, 28, 3), (137, 128, 18, 2), (258, 147, 30, 4), (300, 129, 14, 2),
           (112, 167, 34, 4), (240, 172, 22, 3), (46, 138, 18, 3)]
# Yansıyan ışıklar: x, sıcak/turkuaz
LIGHTS = [(LAMP_A - 6, 'warm'), (LAMP_B - 6, 'warm'), (GATE_L + 3, 'warm'), (GATE_R + 3, 'warm'), (259, 'teal'),
          (59, 'warm')]


def draw_puddles(img, s):
    for pi, (cx, cy, rx, ry) in enumerate(PUDDLES):
        rxs, rys = max(3.0, rx * s), max(1.0, ry * (0.6 + 0.4 * s))
        cells = set()
        for y in range(int(cy - rys) - 1, int(cy + rys) + 2):
            dy = (y - cy) / (rys + 0.5)
            if abs(dy) > 1:
                continue
            half = rxs * math.sqrt(1 - dy * dy) * (0.9 + 0.2 * noise(y * 3 + pi, 4))
            for x in range(int(cx - half), int(cx + half) + 1):
                cells.add((x, y))
        top = min(y for _, y in cells)
        for x, y in cells:
            c = WATER_SKY if y == top or (y == top + 1 and dith(x, y, 0.5)) else WATER
            if (x - 1, y) not in cells or (x + 1, y) not in cells:
                c = WATER_EDGE if y <= cy else c
            img.set(x, y, c)
        # ışık yansımaları: dikey, kesik kesik (su dalgalanması)
        for lx, kind in LIGHTS:
            hi, lo = (REF_WARM, REF_WARM_LO) if kind == 'warm' else (REF_TEAL, REF_TEAL_LO)
            for x in (lx, lx + 1):
                col = [y for (xx, y) in cells if xx == x]
                for y in col:
                    k = y - top
                    if (k + x) % 3 == 2:
                        continue
                    img.set(x + (1 if k % 2 else 0), y, hi if k < 2 else lo)


KID_REFLECT = C('#c9a646')  # çantadaki reflektör bandı: çocuk olduğu, güvenlik bilinci, sıcak bir nokta


def draw_child(img):
    """Ön planda eve yürüyen çocuk: iri kafa, kısa gövde (çocuk oranı), büyük sırt çantası, adım halinde, sağa (eve)
    yürür. Kapüşon/yüz gölgesi yok: silüet korkutucu değil. Ayaklar y=176."""
    fx = 72  # gövde ortası
    k = KID
    feet = 176
    # bacaklar (adım): 3 px kalın, kısa
    for i in range(9):
        img.rect(fx - 3 - i // 3, feet - 11 + i, fx - i // 3, feet - 10 + i, k)
        img.rect(fx + 1 + i // 3, feet - 11 + i, fx + 4 + i // 3, feet - 10 + i, k)
    img.rect(fx - 7, feet - 2, fx - 2, feet, k)   # arka ayakkabı
    img.rect(fx + 3, feet - 2, fx + 9, feet, k)   # ön ayakkabı
    # gövde (mont): omuzlar yuvarlak
    img.rect(fx - 4, feet - 24, fx + 5, feet - 10, k)
    img.rect(fx - 5, feet - 22, fx + 6, feet - 11, k)
    # kol: hafif öne sallanır, el yuvarlak
    for i in range(9):
        img.rect(fx + 3 + i // 3, feet - 22 + i, fx + 6 + i // 3, feet - 21 + i, k)
    img.rect(fx + 6, feet - 14, fx + 9, feet - 11, k)
    # sırt çantası (arkada = solda): iri, yuvarlak köşeli, kapak ve reflektör bandı
    img.rect(fx - 13, feet - 25, fx - 4, feet - 11, KID_BAG)
    img.rect(fx - 14, feet - 24, fx - 3, feet - 12, KID_BAG)
    img.hline(fx - 13, fx - 5, feet - 20, KID_RIM)          # kapak çizgisi
    img.hline(fx - 12, fx - 6, feet - 15, KID_REFLECT)      # reflektör
    img.vline(fx - 2, feet - 24, feet - 15, KID_RIM)        # omuz askısı
    # kafa: 12 px, gövdeye göre iri; saç tutamı
    for y in range(-6, 6):
        for x in range(-6, 6):
            if (x + 0.5) ** 2 + (y + 0.5) ** 2 <= 34:
                img.set(fx + 1 + x, feet - 31 + y, k)
    img.rect(fx - 1, feet - 26, fx + 3, feet - 24, k)        # boyun
    img.set(fx - 3, feet - 38, k); img.set(fx - 2, feet - 38, k); img.set(fx - 4, feet - 37, k)  # saç
    # ışık kenarı: sağdan (sokak lambaları) gelen soluk kontur; silüet zeminden ayrılsın
    rim = []
    for y in range(feet - 40, feet - 2):
        for x in range(fx - 16, fx + 12):
            if img.get(x, y)[3] == 0 or img.get(x, y) == KID_REFLECT:
                continue
            if img.get(x + 1, y)[3] == 0 or (img.get(x, y - 1)[3] == 0 and x >= fx - 2):
                rim.append((x, y))
    for x, y in rim:
        img.set(x, y, KID_RIM)


def build_background():
    bg, flash = Img(), Img()
    draw_sky(bg)
    draw_clouds(bg, flash)
    draw_city(bg)
    draw_school(bg)
    draw_fence_and_gate(bg)
    draw_bus_stop(bg)
    draw_ground(bg)
    draw_light_pools(bg)
    la = draw_lamp(bg, LAMP_A, 76)
    lb = draw_lamp(bg, LAMP_B, 80)
    assert (la, lb) == (LAMP_A - 6, LAMP_B - 6), (la, lb)
    return bg, flash


def main(out):
    os.makedirs(out, exist_ok=True)
    bg, flash = build_background()
    bg.save(os.path.join(out, 'FT_Background.png'))
    flash.save(os.path.join(out, 'FT_Flash.png'))
    frames = 8
    for f in range(frames):
        p = Img()
        draw_puddles(p, 0.4 + 0.6 * f / (frames - 1))
        p.save(os.path.join(out, 'FT_Puddles_%d.png' % f))
    kid = Img()
    draw_child(kid)
    kid.save(os.path.join(out, 'FT_Child.png'))
    print('ok', out)


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else '.')
