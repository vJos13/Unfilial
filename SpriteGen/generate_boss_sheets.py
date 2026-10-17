#!/usr/bin/env python3
"""
generate_boss_sheets.py
Unfilial - Hojas de sprites pixel art de los 3 jefes finales.

Genera, para cada jefe:
  - boss_<id>.png          : hoja 8 columnas x 7 filas, frames de 128x128, fondo transparente
  - boss_<id>.json         : nombre, x, y, ancho y alto de cada frame (+ animaciones y pivote)
  - boss_<id>_preview.png  : previsualización a 3x con vecino más cercano

Jefes:
  guardian -> "Guardián de las Ruinas"  (Nivel 1)
  ceniza   -> "Bestia de Ceniza"        (Nivel 2)
  heraldo  -> "Heraldo Celestial"       (Nivel 3)

Uso:
  python generate_boss_sheets.py            # genera los 3
  python generate_boss_sheets.py guardian   # solo uno

Requisitos: Python 3.8+ y Pillow  (pip install pillow)

Convenciones:
  - Cada jefe se dibuja internamente mirando a la DERECHA y al exportar se
    espeja horizontalmente, así queda mirando a la IZQUIERDA (hacia el jugador)
    centrado en X=64.
  - Guardián y Bestia: el contorno inferior de los pies está en y=124.
  - Heraldo: flota; el centro de su cuerpo está en y=64.
  - Contorno de 1 px, paleta cerrada (<16 colores por jefe), sin antialiasing.
  - Los frames de AVISO (preparación) usan un cambio de pose grande + un
    destello en el ojo o en el arma para que el jugador pueda reaccionar.
"""

import json
import math
import random
import sys
from PIL import Image

# ---------------------------------------------------------------------------
# CONFIGURACIÓN GENERAL
# ---------------------------------------------------------------------------
FRAME = 128
COLS = 8
LX = 63          # centro local; al espejar (x -> 127 - x) queda en X=64
GROUND = 123     # última fila de relleno de los pies (el contorno cae en y=124)
PREVIEW_SCALE = 3

ANIMATIONS = [   # (nombre, frames, fps sugeridos, loop)
    ("idle", 4, 6, True),
    ("move", 6, 8, True),
    ("attack_melee", 6, 10, False),
    ("attack_special", 6, 10, False),
    ("hurt", 2, 10, False),
    ("phase2", 4, 8, False),
    ("death", 8, 8, False),
]


# ---------------------------------------------------------------------------
# LIENZO Y PRIMITIVAS (sin antialiasing)
# ---------------------------------------------------------------------------
class Canvas:
    """Lienzo que guarda nombres de color (None = transparente).
    Si clip=True, solo se pinta sobre píxeles ya ocupados (para grietas/detalles)."""

    def __init__(self, w=FRAME, h=FRAME):
        self.w, self.h = w, h
        self.px = [[None] * w for _ in range(h)]
        self.clip = False

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[y][x]
        return None

    def put(self, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            if self.clip and self.px[y][x] is None:
                return
            self.px[y][x] = c

    def stamp(self, x, y, c, w):
        if w <= 1:
            self.put(x, y, c)
        elif w == 2:
            for oy in (0, 1):
                for ox in (0, 1):
                    self.put(x + ox, y + oy, c)
        else:
            r = (w - 1) / 2.0
            ri = int(math.ceil(r))
            for oy in range(-ri, ri + 1):
                for ox in range(-ri, ri + 1):
                    if ox * ox + oy * oy <= r * r + 0.5:
                        self.put(x + ox, y + oy, c)

    def rect(self, x0, y0, x1, y1, c):
        for y in range(int(round(min(y0, y1))), int(round(max(y0, y1))) + 1):
            for x in range(int(round(min(x0, x1))), int(round(max(x0, x1))) + 1):
                self.put(x, y, c)

    def ellipse(self, cx, cy, rx, ry, c):
        rx, ry = max(rx, 0.01), max(ry, 0.01)
        for y in range(int(math.floor(cy - ry)) - 1, int(math.ceil(cy + ry)) + 2):
            for x in range(int(math.floor(cx - rx)) - 1, int(math.ceil(cx + rx)) + 2):
                dx, dy = (x - cx) / rx, (y - cy) / ry
                if dx * dx + dy * dy <= 1.0:
                    self.put(x, y, c)

    def ring(self, cx, cy, r0, r1, c, skip=None):
        """Anillo entre radios r0 y r1. skip(angulo) -> True omite ese píxel."""
        for y in range(int(cy - r1) - 1, int(cy + r1) + 2):
            for x in range(int(cx - r1) - 1, int(cx + r1) + 2):
                d = math.hypot(x - cx, y - cy)
                if r0 <= d <= r1:
                    if skip and skip(math.degrees(math.atan2(y - cy, x - cx))):
                        continue
                    self.put(x, y, c)

    def line(self, x0, y0, x1, y1, c, w=1):
        x0, y0, x1, y1 = (int(round(v)) for v in (x0, y0, x1, y1))
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx = 1 if x0 < x1 else -1
        sy = 1 if y0 < y1 else -1
        err = dx + dy
        while True:
            self.stamp(x0, y0, c, w)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def polyline(self, pts, c, w=1):
        for a, b in zip(pts, pts[1:]):
            self.line(a[0], a[1], b[0], b[1], c, w)

    def polygon(self, pts, c):
        for x, y in poly_pixels(pts):
            self.put(x, y, c)

    def paste(self, other):
        for y in range(self.h):
            row, orow = self.px[y], other.px[y]
            for x in range(self.w):
                if orow[x] is not None:
                    row[x] = orow[x]

    def mirrored(self):
        out = Canvas(self.w, self.h)
        for y in range(self.h):
            out.px[y] = list(reversed(self.px[y]))
        return out


def point_in_poly(px, py, pts):
    inside = False
    j = len(pts) - 1
    for i in range(len(pts)):
        xi, yi = pts[i]
        xj, yj = pts[j]
        if (yi > py) != (yj > py):
            if px < (xj - xi) * (py - yi) / (yj - yi) + xi:
                inside = not inside
        j = i
    return inside


def poly_pixels(pts):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    out = []
    for y in range(int(math.floor(min(ys))), int(math.ceil(max(ys))) + 1):
        for x in range(int(math.floor(min(xs))), int(math.ceil(max(xs))) + 1):
            if point_in_poly(x + 0.01, y + 0.01, pts):
                out.append((x, y))
    return out


def outline(cv, color="OUT"):
    """Contorno de 1 px (vecindad de 4)."""
    add = []
    for y in range(cv.h):
        for x in range(cv.w):
            if cv.px[y][x] is None:
                for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    if cv.get(x + ox, y + oy) is not None:
                        add.append((x, y))
                        break
    for x, y in add:
        cv.px[y][x] = color


def hash01(x, y, s=0):
    n = (x * 374761393 + y * 668265263 + s * 982451653) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFFFFFF) / 4294967296.0


def make_cracks(seed, n, x0, y0, x1, y1, steps=6, step=4.0):
    """Grietas como caminatas aleatorias deterministas (coordenadas relativas)."""
    rnd = random.Random(seed)
    out = []
    for _ in range(n):
        x, y = rnd.uniform(x0, x1), rnd.uniform(y0, y1)
        a = rnd.uniform(0, 2 * math.pi)
        pts = [(x, y)]
        for _ in range(steps):
            a += rnd.uniform(-0.9, 0.9)
            x += math.cos(a) * step
            y += math.sin(a) * step
            pts.append((x, y))
        out.append(pts)
    return out


def unit(angle_deg):
    a = math.radians(angle_deg)
    return math.cos(a), math.sin(a)


class Composer:
    """Dibuja cada pieza en su capa, le pone contorno y la compone en orden."""

    def __init__(self):
        self.main = Canvas()

    def layer(self, fn, with_outline=True):
        L = Canvas()
        fn(L)
        if with_outline:
            outline(L)
        self.main.paste(L)


# ---------------------------------------------------------------------------
# EFECTOS (sin contorno)
# ---------------------------------------------------------------------------
def fx_crescent(cv, cx, cy, r_in, thick, a0, a1, core, edge):
    """Estela de tajo en media luna. Ángulos de pantalla (0 = adelante, 90 = abajo)."""
    R = r_in + thick + 1
    for y in range(int(cy - R), int(cy + R) + 1):
        for x in range(int(cx - R), int(cx + R) + 1):
            r = math.hypot(x - cx, y - cy)
            a = math.degrees(math.atan2(y - cy, x - cx))
            while a < a0:
                a += 360
            while a >= a0 + 360:
                a -= 360
            if a > a1:
                continue
            f = (a - a0) / (a1 - a0)
            t = thick * math.sin(math.pi * (f ** 0.8))
            if t >= 0.7 and r_in <= r <= r_in + t:
                cv.put(x, y, edge if r < r_in + 1.5 else core)


def fx_star(cv, x, y, size, core, mid, tip):
    """Destello de 4 puntas + diagonales cortas (aviso de ataque / impacto)."""
    cv.ellipse(x, y, 1.2, 1.2, core)
    for i in range(1, size + 1):
        c = core if i <= size // 3 else (mid if i <= 2 * size // 3 else tip)
        for ox, oy in ((i, 0), (-i, 0), (0, i), (0, -i)):
            cv.put(x + ox, y + oy, c)
    for i in range(1, max(2, size // 2)):
        c = mid if i < size // 3 else tip
        for ox, oy in ((i, i), (-i, i), (i, -i), (-i, -i)):
            cv.put(x + ox, y + oy, c)


def fx_motes(cv, motes):
    for x, y, c in motes:
        cv.put(x, y, c)


def fx_streaks(cv, streaks):
    for x0, y0, x1, y1, c in streaks:
        cv.line(x0, y0, x1, y1, c, 1)


def fx_ring(cv, cx, cy, r, c, c2=None):
    cv.ring(cx, cy, r - 0.6, r + 0.6, c)
    if c2:
        cv.ring(cx, cy, r - 1.6, r - 0.6, c2)


def fx_wave(cv, x, w, h, outer, mid, inner):
    """Onda de choque: arco que sale del suelo."""
    for dx in range(-w, w + 1):
        k = 1 - (dx / float(w)) ** 2
        if k < 0:
            continue
        top = GROUND + 1 - h * math.sqrt(k)
        for t, c in ((0, inner), (1, mid), (2, outer)):
            y = int(round(top + t))
            if y <= GROUND + 1:
                cv.put(x + dx, y, c)


def fx_bolt(cv, x, y, ang, length, core, edge, trail):
    """Proyectil de luz en rombo con estela."""
    ux, uy = unit(ang)
    px, py = -uy, ux
    pts = [(x + ux * length / 2, y + uy * length / 2),
           (x + px * 3, y + py * 3),
           (x - ux * length / 2, y - uy * length / 2),
           (x - px * 3, y - py * 3)]
    cv.polygon(pts, edge)
    cv.line(x - ux * length / 3, y - uy * length / 3, x + ux * length / 3,
            y + uy * length / 3, core, 2)
    for k in range(1, 5):
        cv.put(x - ux * (length / 2 + k * 3), y - uy * (length / 2 + k * 3) + (k % 2),
               trail)


def fx_ball(cv, x, y, r, outer, mid, core, trail=None):
    """Bola de lava con estela de gotas."""
    if trail:
        for k, (dx, dy) in enumerate(trail):
            cv.ellipse(x + dx, y + dy, max(1, r - 2 - k), max(1, r - 2 - k), outer)
    cv.ellipse(x, y, r, r, outer)
    cv.ellipse(x - 0.5, y - 0.5, r - 1.2, r - 1.2, mid)
    cv.ellipse(x - 1, y - 1, max(0.8, r - 3), max(0.8, r - 3), core)


def fx_embers(cv, seed, n, x0, y0, x1, y1, cols):
    for k in range(n):
        x = int(x0 + hash01(k, 1, seed) * (x1 - x0))
        y = int(y0 + hash01(k, 2, seed) * (y1 - y0))
        c = cols[k % len(cols)]
        cv.put(x, y, c)
        if hash01(k, 3, seed) < 0.5:
            cv.put(x, y - 1, cols[-1])


def fx_puff(cv, x, y, r, c, c2):
    cv.ellipse(x, y, r, r * 0.8, c)
    cv.ellipse(x - 1, y - 1, r * 0.5, r * 0.4, c2)


# ---------------------------------------------------------------------------
# POST-PROCESOS
# ---------------------------------------------------------------------------
def flash(cv, mapping):
    """Destello de impacto: aclara toda la paleta."""
    for y in range(cv.h):
        for x in range(cv.w):
            c = cv.px[y][x]
            if c is not None and c in mapping:
                cv.px[y][x] = mapping[c]


def dissolve(cv, amount, seed, colors):
    """Disolución de abajo hacia arriba en partículas de luz que suben."""
    removed = []
    for y in range(cv.h):
        for x in range(cv.w):
            if cv.px[y][x] is None:
                continue
            yn = (110 - y) / 90.0           # 0 abajo, 1 arriba
            if hash01(x, y, seed) < amount * 1.45 - yn * 0.45:
                removed.append((x, y))
    for x, y in removed:
        cv.px[y][x] = None
    for x, y in removed:
        h = hash01(x, y, seed + 7)
        if h < 0.12:
            rise = 3 + int(hash01(x, y, seed + 11) * (10 + amount * 24))
            drift = int(round(math.sin((x + y) * 0.7) * 2))
            cv.put(x + drift, y - rise, colors[int(h * 100) % len(colors)])


def crumble(cv, amount, seed, block=5, dust=()):
    """Se desmorona: el sprite se parte en bloques que caen y se apilan en el suelo."""
    blocks = []
    for by in range(0, cv.h, block):
        for bx in range(0, cv.w, block):
            pix = []
            for y in range(by, min(by + block, cv.h)):
                for x in range(bx, min(bx + block, cv.w)):
                    if cv.px[y][x] is not None:
                        pix.append((x, y, cv.px[y][x]))
            if pix:
                blocks.append((bx, by, pix))
    blocks.sort(key=lambda b: -b[1])                  # primero los de abajo
    height = [GROUND + 2] * cv.w                      # superficie del montón
    out = Canvas()
    motes = []
    for bx, by, pix in blocks:
        r = hash01(bx, by, seed)
        want = max(0.0, amount * 1.7 - r * 0.7) * 130
        spread = (hash01(bx, by, seed + 1) - 0.5) * amount * 44
        drift = int(round(spread * min(1.0, want / 40.0)))   # se esparce al caer
        if amount > 0.7 and hash01(bx, by, seed + 3) < (amount - 0.7) * 2.6:
            if dust:
                motes.append((bx + 2 + drift, GROUND, dust[0]))
            continue                                          # se volvió polvo
        cols = [min(cv.w - 1, max(0, x + drift)) for x, _, _ in pix]
        limit = min(height[c] - 1 - y for c, (_, y, _) in zip(cols, pix))
        dy = int(min(want, max(0, limit)))
        if dy >= limit:                                       # aterrizó: se apila
            for c, (_, y, _) in zip(cols, pix):
                height[c] = min(height[c], y + dy)
        for x, y, c in pix:
            out.put(x + drift, y + dy, c)
    for x, y, c in motes:
        out.put(x, y, c)
    return out


# ===========================================================================
# JEFE 1: GUARDIÁN DE LAS RUINAS
# ===========================================================================
G_PALETTE = {
    "OUT":      (0x0A, 0x0A, 0x12),
    "CRACK":    (0x1A, 0x22, 0x2E),  # grieta apagada
    "STONE_D":  (0x2E, 0x3A, 0x4C),
    "STONE":    (0x4A, 0x5A, 0x70),
    "STONE_L":  (0x6E, 0x80, 0x9A),
    "STONE_HL": (0x9A, 0xAA, 0xC0),
    "MOSS":     (0x4E, 0x6B, 0x5A),  # musgo en las ruinas
    "CYAN_D":   (0x1F, 0xA8, 0xC8),
    "CYAN":     (0x5F, 0xF4, 0xFF),
    "CYAN_L":   (0xD8, 0xFF, 0xFF),
    "WHITE":    (0xFF, 0xFF, 0xFF),
}
G_FLASH = {"CRACK": "STONE", "STONE_D": "STONE_L", "STONE": "STONE_HL",
           "STONE_L": "WHITE", "STONE_HL": "WHITE", "MOSS": "STONE_HL",
           "OUT": "STONE_D"}
G_CRACK_COL = {0: "CRACK", 1: "CYAN_D", 2: "CYAN", 3: "CYAN_L"}
G_TORSO_CRACKS = make_cracks(11, 6, -14, 4, 10, 40, steps=5, step=4)
G_HELM_CRACKS = make_cracks(12, 2, -6, -6, 2, 4, steps=3, step=3)

G_LEGS = (((-8, -18), (-9, 0)), ((8, -18), (9, 0)))   # (trasera, delantera)
G_DEFAULT = dict(dx=0, bob=0, lean=0, head_dx=0, head_dy=0, legs=G_LEGS,
                 hand=(24, 72), angle=92, length=42, bhand=(-20, 84),
                 cracks=0, eye=1, eye_star=0, glint=False, gem=1,
                 flash=False, crumble=0.0, seed=1, fx=())


def draw_crack_set(L, sets, ox, oy, level):
    L.clip = True
    for pts in sets:
        p = [(ox + x, oy + y) for x, y in pts]
        if level >= 2:
            L.polyline(p, "CYAN_D", 2)
        L.polyline(p, G_CRACK_COL[level], 1)
    L.clip = False


def g_stone_sword(L, hand, ang, length, crack_level):
    ux, uy = unit(ang)
    px, py = -uy, ux
    hx, hy = hand
    # Empuñadura y pomo
    L.line(hx - 7 * ux, hy - 7 * uy, hx + 4 * ux, hy + 4 * uy, "STONE_D", 4)
    L.ellipse(hx - 8 * ux, hy - 8 * uy, 3, 3, "STONE")
    # Guarda ancha
    gx, gy = hx + 5 * ux, hy + 5 * uy
    L.line(gx - 8 * px, gy - 8 * py, gx + 8 * px, gy + 8 * py, "STONE", 4)
    # Hoja enorme
    b0x, b0y = hx + 7 * ux, hy + 7 * uy
    bl = length
    pts = [(b0x + 5 * px, b0y + 5 * py),
           (b0x + (bl - 9) * ux + 4.5 * px, b0y + (bl - 9) * uy + 4.5 * py),
           (b0x + bl * ux, b0y + bl * uy),
           (b0x + (bl - 9) * ux - 4.5 * px, b0y + (bl - 9) * uy - 4.5 * py),
           (b0x - 5 * px, b0y - 5 * py)]
    for x, y in poly_pixels(pts):
        side = (x - b0x) * px + (y - b0y) * py
        c = "STONE_L" if side > 0.6 else "STONE"
        if abs(side) <= 0.6:
            c = "STONE_HL"                                   # arista central
        L.put(x, y, c)
    # Grietas de la hoja
    L.clip = True
    for t, w in ((0.3, 1), (0.62, -1)):
        cx_, cy_ = b0x + bl * t * ux, b0y + bl * t * uy
        p = [(cx_ + 4 * px * w, cy_ + 4 * py * w), (cx_ + ux * 3, cy_ + uy * 3),
             (cx_ - 2 * px * w + ux * 6, cy_ - 2 * py * w + uy * 6)]
        L.polyline(p, G_CRACK_COL[crack_level], 1)
    L.clip = False
    return (b0x + bl * ux, b0y + bl * uy)                    # punta


def draw_guardian(pose):
    P = dict(G_DEFAULT)
    P.update(pose)
    cx, bob, lean = LX + P["dx"], P["bob"], P["lean"]
    sy, hip = 50 + bob, 86 + bob
    hx, hy = cx + 3 + lean + P["head_dx"], 36 + bob + P["head_dy"]
    C = Composer()

    # 1) Brazo trasero
    bsh = (cx - 15 + lean, sy + 7)
    bh = (cx + P["bhand"][0], P["bhand"][1] + bob)

    def back_arm(L):
        L.line(bsh[0], bsh[1], bh[0], bh[1], "STONE_D", 8)
        L.ellipse(bh[0], bh[1], 4, 4, "STONE")
    C.layer(back_arm)
    # 2) Hombrera trasera
    C.layer(lambda L: L.ellipse(cx - 15 + lean, sy + 3, 9, 7, "STONE_D"))

    # 3) Piernas (trasera oscura, delantera clara)
    def leg(L, hipp, lg, col, kcol):
        (kx, ky), (fx, fy) = lg
        knee = (cx + kx, GROUND + ky)
        foot = (cx + fx, GROUND + fy)
        L.line(hipp[0], hipp[1], knee[0], knee[1], col, 10)
        L.line(knee[0], knee[1], foot[0], foot[1] - 5, col, 9)
        L.ellipse(knee[0] + 1, knee[1], 4, 4, kcol)              # rodillera
        L.polygon([(foot[0] - 6, foot[1] - 6), (foot[0] + 5, foot[1] - 6),
                   (foot[0] + 10, foot[1] + 0.9), (foot[0] - 6, foot[1] + 0.9)], col)
    back_leg, front_leg = P["legs"]
    C.layer(lambda L: leg(L, (cx - 7, hip), back_leg, "STONE_D", "STONE"))
    C.layer(lambda L: leg(L, (cx + 7, hip), front_leg, "STONE", "STONE_L"))

    # 4) Torso con placas, gema y grietas
    def torso(L):
        pts = [(cx - 17 + lean, sy), (cx + 15 + lean, sy),
               (cx + 13 + lean * 0.5, sy + 28), (cx + 11, hip + 7),
               (cx - 14, hip + 7), (cx - 16 + lean * 0.5, sy + 28)]
        pix = set(poly_pixels(pts))
        for x, y in pix:
            c = "STONE"
            if x <= cx - 7 + lean * 0.5:
                c = "STONE_D"
            if (x + 1, y) not in pix:
                c = "STONE_L"
            if (x, y - 1) not in pix:
                c = "STONE_HL"
            L.put(x, y, c)
        L.clip = True
        L.line(cx - 17, hip - 1, cx + 13, hip - 1, "CRACK")          # cinturón
        L.line(cx - 17, hip + 3, cx + 13, hip + 3, "STONE_D")        # faldón
        L.line(cx - 1 + lean, sy + 3, cx, hip - 3, "STONE_D")        # quilla del peto
        for k in range(4):                                           # musgo
            L.put(cx - 12 + k * 2, hip + 5 - (k % 2), "MOSS")
        L.clip = False
        draw_crack_set(L, G_TORSO_CRACKS, cx, sy, P["cracks"])
        gem = {0: "STONE_D", 1: "CYAN_D", 2: "CYAN", 3: "CYAN_L"}[P["gem"]]
        L.ellipse(cx + 5 + lean * 0.5, sy + 15, 2.2, 2.6, gem)        # gema del pecho
        if P["gem"] >= 1:
            L.put(cx + 4 + lean * 0.5, sy + 14, "CYAN_L")
    C.layer(torso)

    # 5) Casco con un único ojo cian
    def head(L):
        L.polygon([(hx - 7, hy - 7), (hx - 3, hy - 16), (hx + 5, hy - 15),
                   (hx + 6, hy - 7)], "STONE_L")                     # cresta
        L.ellipse(hx, hy, 9, 10, "STONE")
        for y in range(hy - 11, hy + 11):
            for x in range(hx - 10, hx + 11):
                if L.get(x, y) == "STONE":
                    if x < hx - 4:
                        L.put(x, y, "STONE_D")
                    elif y < hy - 7:
                        L.put(x, y, "STONE_L")
        draw_crack_set(L, G_HELM_CRACKS, hx, hy, min(P["cracks"], 2))
        L.rect(hx - 2, hy - 1, hx + 9, hy + 1, "OUT")                # visera
        if P["eye"] == 0:
            L.rect(hx + 5, hy - 1, hx + 7, hy, "STONE_D")            # ojo apagado
        else:
            L.rect(hx + 5, hy - 1, hx + 7, hy + 1, "CYAN")
            L.put(hx + 6, hy, "CYAN_L")
            if P["eye"] >= 2:
                L.put(hx + 4, hy, "CYAN_D")
                L.put(hx + 8, hy, "CYAN_D")
                L.put(hx + 6, hy - 1, "CYAN_L")
    C.layer(head)

    # 6) Espada enorme + brazo delantero + hombrera delantera
    hand = (cx + P["hand"][0], P["hand"][1] + bob)
    tip = [None]

    def sword_arm(L):
        tip[0] = g_stone_sword(L, hand, P["angle"], P["length"], min(P["cracks"], 3))
        fsh = (cx + 13 + lean, sy + 8)
        L.line(fsh[0], fsh[1], hand[0], hand[1], "STONE", 8)
        L.ellipse(hand[0], hand[1], 4.5, 4.5, "STONE_L")              # guantelete
    C.layer(sword_arm)

    def front_pauldron(L):
        px0, py0 = cx + 13 + lean, sy + 4
        L.ellipse(px0, py0, 10, 8, "STONE")
        for y in range(py0 - 9, py0 + 9):
            for x in range(px0 - 11, px0 + 11):
                if L.get(x, y) == "STONE" and y < py0 - 3:
                    L.put(x, y, "STONE_L")
        L.line(px0 - 8, py0 + 2, px0 + 8, py0 + 2, "STONE_D")
    C.layer(front_pauldron)

    M = C.main
    if P["flash"]:
        flash(M, G_FLASH)
    if P["crumble"] > 0:
        M = crumble(M, P["crumble"], P["seed"], block=5, dust=("STONE_L",))
    # Efectos
    if P["eye_star"]:
        fx_star(M, hx + 6, hy, P["eye_star"], "CYAN_L", "CYAN", "CYAN_D")
    if P["glint"] and tip[0]:
        fx_star(M, int(tip[0][0]), int(tip[0][1]), 5, "WHITE", "CYAN_L", "CYAN")
    apply_fx(M, P["fx"], G_FX)
    return M


G_FX = dict(core="CYAN_L", edge="CYAN", mid="CYAN", dark="CYAN_D",
            dust="STONE_L", debris="STONE", white="WHITE")


def apply_fx(M, fxs, pal):
    for fx in fxs:
        k, a = fx[0], fx[1:]
        if k == "crescent":
            fx_crescent(M, *a, pal["core"], pal["edge"])
        elif k == "crescent2":                         # versión tenue
            fx_crescent(M, *a, pal["edge"], pal["dark"])
        elif k == "star":
            fx_star(M, a[0], a[1], a[2], pal["white"], pal["core"], pal["edge"])
        elif k == "wave":
            fx_wave(M, a[0], a[1], a[2], pal["dark"], pal["edge"], pal["core"])
        elif k == "motes":
            fx_motes(M, a[0])
        elif k == "streaks":
            fx_streaks(M, a[0])
        elif k == "ring":
            fx_ring(M, a[0], a[1], a[2], pal["edge"], pal["dark"])
        elif k == "debris":                           # trozos de roca saltando
            x, y, n, seed = a
            for i in range(n):
                dx = int((hash01(i, 0, seed) - 0.3) * 24)
                dy = int(hash01(i, 1, seed) * 14)
                M.rect(x + dx, y - dy, x + dx + 1, y - dy + 1,
                       pal["debris"] if i % 2 else pal["dust"])
        elif k == "embers":
            fx_embers(M, *a)
        elif k == "puff":
            fx_puff(M, *a)
        elif k == "ball":
            fx_ball(M, *a)
        elif k == "bolt":
            fx_bolt(M, *a)


def guardian_poses():
    braced = (((-10, -16), (-14, 0)), ((10, -18), (12, 0)))
    lunge = (((-10, -16), (-15, 0)), ((14, -18), (17, 0)))
    crouch = (((-11, -14), (-15, 0)), ((13, -15), (14, 0)))
    kneel = (((-10, -6), (-22, 0)), ((12, -16), (14, 0)))

    idle = [dict(bob=[0, 0, 1, 1][i], eye=[1, 1, 2, 1][i]) for i in range(4)]

    move = []
    for i in range(6):
        t = 2 * math.pi * i / 6

        def lg(phi, base):
            fx_ = base + 7 * math.sin(phi)
            lift = 5 * max(0.0, math.cos(phi))
            return ((round(fx_ * 0.6 + (2 if base > 0 else -2)), round(-18 - lift)),
                    (round(fx_), round(-lift)))
        move.append(dict(bob=[0, 1, 1, 0, 1, 1][i], lean=2,
                         legs=(lg(t + math.pi, -8), lg(t, 8)),
                         hand=(22, 72), angle=110, length=40))

    melee = [
        # 0 AVISO 1: alza la espada atrás, el ojo destella
        dict(lean=-3, bob=1, legs=braced, hand=(8, 46), angle=-125, length=40,
             eye=2, eye_star=7, bhand=(-18, 70)),
        # 1 AVISO 2: máxima tensión, destello en la punta
        dict(lean=-4, bob=2, legs=braced, hand=(6, 44), angle=-140, length=40,
             eye=2, eye_star=10, glint=True, bhand=(-20, 68)),
        # 2 GOLPE: tajo descendente con estela cian
        dict(lean=4, bob=2, legs=lunge, hand=(18, 64), angle=30, length=34,
             eye=2, bhand=(-16, 80),
             fx=[("crescent", LX + 8, 62, 28, 10, -85, 40)]),
        # 3 IMPACTO: la hoja toca el suelo
        dict(lean=4, bob=3, legs=lunge, hand=(22, 76), angle=62, length=34,
             bhand=(-16, 82),
             fx=[("crescent2", LX + 8, 62, 30, 4, -15, 60),
                 ("star", LX + 43, 118, 5), ("debris", LX + 43, 120, 8, 3)]),
        # 4 RECUPERACIÓN 1
        dict(lean=2, bob=1, legs=braced, hand=(22, 74), angle=80, length=40),
        # 5 RECUPERACIÓN 2
        dict(),
    ]

    special = [
        # 0 AVISO 1: ambas manos arriba, grietas se encienden
        dict(lean=-1, hand=(6, 48), angle=-95, length=38, bhand=(0, 50),
             cracks=1, eye=2, eye_star=6, gem=2),
        # 1 AVISO 2: se eleva, brillo máximo
        dict(lean=-2, bob=-1, hand=(6, 46), angle=-92, length=38, bhand=(0, 48),
             cracks=2, eye=2, eye_star=9, gem=3, glint=True,
             fx=[("motes", [(LX - 6, 20, "CYAN"), (LX + 18, 14, "CYAN_L"),
                            (LX - 12, 34, "CYAN_D"), (LX + 22, 30, "CYAN")])]),
        # 2 GOLPE AL SUELO
        dict(lean=3, bob=4, legs=crouch, hand=(20, 86), angle=90, length=26,
             bhand=(14, 88), cracks=2, eye=2, gem=3,
             fx=[("star", LX + 20, 115, 8), ("debris", LX + 22, 120, 10, 5),
                 ("wave", LX + 32, 6, 8)]),
        # 3 ONDA 1
        dict(lean=3, bob=4, legs=crouch, hand=(20, 86), angle=90, length=26,
             bhand=(14, 88), cracks=2, eye=2, gem=2,
             fx=[("wave", LX + 40, 8, 12), ("debris", LX + 40, 112, 6, 6)]),
        # 4 ONDA 2
        dict(lean=2, bob=3, legs=crouch, hand=(20, 85), angle=90, length=26,
             bhand=(14, 88), cracks=1, eye=2, gem=2,
             fx=[("wave", LX + 52, 9, 14), ("wave", LX + 36, 5, 6)]),
        # 5 ONDA 3 + se reincorpora
        dict(lean=1, bob=2, legs=braced, hand=(20, 80), angle=85, length=30,
             cracks=1, gem=1, fx=[("wave", LX + 60, 7, 10)]),
    ]

    hurt = [
        dict(dx=-3, lean=-3, eye=2, flash=True,
             fx=[("star", LX + 16, 62, 7)]),
        dict(dx=-2, lean=-2, eye=2, fx=[("motes", [(LX + 18, 58, "WHITE"),
                                                   (LX + 21, 66, "CYAN_L")])]),
    ]

    phase2 = [
        # 0 Se encorva, primeras grietas encendidas
        dict(bob=4, lean=-1, legs=crouch, hand=(22, 78), angle=95, length=34,
             cracks=1, eye=1),
        # 1 Alza la cabeza, el ojo arde
        dict(bob=2, lean=-3, head_dy=-1, hand=(22, 76), angle=95, length=36,
             cracks=2, eye=2, eye_star=7, gem=2,
             fx=[("motes", [(LX - 20, 40, "CYAN"), (LX + 26, 44, "CYAN_L"),
                            (LX - 8, 24, "CYAN")])]),
        # 2 Estallido de energía (aura)
        dict(bob=0, lean=-2, cracks=3, eye=2, eye_star=10, gem=3,
             fx=[("ring", LX, 72, 50), ("ring", LX, 72, 40)]),
        # 3 Nuevo aspecto de fase 2 (grietas brillantes permanentes)
        dict(cracks=2, eye=2, gem=3),
    ]

    death = [
        # 0 Tambaleo
        dict(dx=-2, lean=-3, eye=1, cracks=1, hand=(16, 80), angle=100, length=34),
        # 1 Cae de rodillas, clava la espada
        dict(bob=10, lean=2, head_dy=3, legs=kneel, hand=(22, 84), angle=92,
             length=21, eye=1, cracks=1),
        # 2 La luz se escapa por las grietas
        dict(bob=10, lean=2, head_dy=4, legs=kneel, hand=(22, 84), angle=92,
             length=21, eye=0, cracks=2,
             fx=[("motes", [(LX - 4, 50, "CYAN"), (LX + 6, 44, "CYAN_L"),
                            (LX - 10, 58, "CYAN_D")])]),
    ]
    # 3-7 Se desmorona en bloques de piedra
    for amount, seed in ((0.12, 3), (0.3, 4), (0.52, 5), (0.78, 6), (1.0, 7)):
        death.append(dict(bob=10, lean=2, head_dy=4, legs=kneel, hand=(22, 84),
                          angle=92, length=21, eye=0, cracks=0, gem=0,
                          crumble=amount, seed=seed))

    return dict(idle=idle, move=move, attack_melee=melee, attack_special=special,
                hurt=hurt, phase2=phase2, death=death)


# ===========================================================================
# JEFE 2: BESTIA DE CENIZA
# ===========================================================================
B_PALETTE = {
    "OUT":     (0x0A, 0x0A, 0x12),
    "ROCK_D":  (0x15, 0x13, 0x1A),
    "ROCK":    (0x26, 0x22, 0x2C),
    "ROCK_L":  (0x3E, 0x38, 0x44),
    "ROCK_HL": (0x5A, 0x52, 0x60),
    "LAVA_D":  (0xC2, 0x40, 0x1A),
    "LAVA":    (0xFF, 0x7A, 0x2E),
    "LAVA_L":  (0xFF, 0xC1, 0x5E),
    "EMBER":   (0xFF, 0xF0, 0xB8),
    "HORN":    (0xD8, 0xCB, 0xB8),
    "HORN_SH": (0x9C, 0x8E, 0x7C),
    "SMOKE":   (0x6A, 0x5E, 0x66),
    "SMOKE_L": (0x8E, 0x82, 0x8A),
}
B_FLASH = {"ROCK_D": "ROCK_L", "ROCK": "ROCK_HL", "ROCK_L": "HORN_SH",
           "ROCK_HL": "HORN", "OUT": "ROCK_L", "LAVA_D": "LAVA_L",
           "LAVA": "EMBER", "HORN_SH": "EMBER", "HORN": "EMBER"}
B_LAVA = {  # (borde, núcleo) por nivel de brillo; -1 = lava enfriada (muerte)
    -1: ("ROCK_L", "SMOKE"), 0: ("ROCK_D", "LAVA_D"), 1: ("LAVA_D", "LAVA"),
    2: ("LAVA", "LAVA_L"), 3: ("LAVA_L", "EMBER")}
B_SHELL_CRACKS = make_cracks(21, 7, -30, -18, 24, 8, steps=6, step=4.5)
B_FX = dict(core="LAVA_L", edge="LAVA", mid="LAVA", dark="LAVA_D",
            dust="SMOKE_L", debris="ROCK_L", white="EMBER")

B_FEET = dict(fn=(20, 0), ff=(14, 0), bn=(-24, 0), bf=(-30, 0))
B_DEFAULT = dict(dx=0, bob=0, head_dx=0, head_dy=0, jaw=0, glow=1, feet=B_FEET,
                 flames=0, embers=3, eye_star=0, mouth=0, flash=False,
                 crumble=0.0, seed=1, fx=())


def draw_ceniza(pose):
    P = dict(B_DEFAULT)
    P.update(pose)
    cx, bob = LX + P["dx"], P["bob"]
    bx, by = cx - 6, 80 + bob                        # centro del caparazón
    hx, hy = cx + 34 + P["head_dx"], 88 + bob + P["head_dy"]
    lava_edge, lava_core = B_LAVA[P["glow"]]
    feet = dict(B_FEET)
    feet.update(P["feet"])
    C = Composer()

    def leg(L, attach, foot_rel, col, front):
        fx_, fy = cx + foot_rel[0], GROUND + foot_rel[1]
        ax, ay = attach
        knee = ((ax + fx_) / 2 + (3 if front else -3), (ay + fy) / 2)
        L.line(ax, ay, knee[0], knee[1], col, 11)
        L.line(knee[0], knee[1], fx_, fy - 5, col, 9)
        L.polygon([(fx_ - 6, fy - 6), (fx_ + 5, fy - 6), (fx_ + 8, fy + 0.9),
                   (fx_ - 6, fy + 0.9)], col)
        for k in (2, 5, 8):                                     # garras
            L.put(fx_ + k - 1, fy, "HORN")
        L.put(fx_ + 8, fy - 1, "HORN_SH")

    # 1) Cola
    C.layer(lambda L: L.polygon([(bx - 32, by + 2), (bx - 46, by + 12),
                                 (bx - 52, by + 20), (bx - 34, by + 12)], "ROCK_D"))
    # 2) Patas lejanas (más oscuras)
    C.layer(lambda L: leg(L, (cx - 33, 94 + bob), feet["bf"], "ROCK_D", False))
    C.layer(lambda L: leg(L, (cx + 11, 94 + bob), feet["ff"], "ROCK_D", True))
    # 3) Cuello
    C.layer(lambda L: L.line(bx + 26, by + 6, hx - 4, hy, "ROCK_D", 14))

    # 4) Caparazón de roca negra con grietas de lava y púas
    def shell(L):
        rx, ry = 37, 25
        pix = set()
        for y in range(by - ry - 1, by + 15):
            for x in range(bx - rx - 1, bx + rx + 2):
                dx, dy = (x - bx) / rx, (y - by) / ry
                if dx * dx + dy * dy <= 1:
                    pix.add((x, y))
        # púas sobre el lomo
        for k in range(5):
            a = math.radians(-150 + k * 27)
            ex, ey = bx + rx * math.cos(a), by + ry * math.sin(a)
            nx, ny = math.cos(a), math.sin(a)
            tx, ty = -ny, nx
            tri = [(ex + tx * 4, ey + ty * 4), (ex + nx * 10, ey + ny * 10 - 1),
                   (ex - tx * 4, ey - ty * 4)]
            for p in poly_pixels(tri):
                pix.add(p)
        for x, y in pix:
            c = "ROCK"
            if y < by - 14:
                c = "ROCK_L"
            if y > by + 8:
                c = "ROCK_D"
            if (x, y - 1) not in pix:
                c = "ROCK_HL"
            L.put(x, y, c)
        # puntas de las púas al rojo en fase 2
        if P["glow"] >= 3:
            for k in range(5):
                a = math.radians(-150 + k * 27)
                L.put(bx + (rx + 9) * math.cos(a), by + (ry + 8) * math.sin(a), "LAVA_L")
        # grietas de lava
        L.clip = True
        for pts in B_SHELL_CRACKS:
            p = [(bx + x, by + y) for x, y in pts]
            L.polyline(p, lava_edge, 2)
            L.polyline(p, lava_core, 1)
        L.clip = False
    C.layer(shell)

    # 5) Patas cercanas
    C.layer(lambda L: leg(L, (cx - 28, 94 + bob), feet["bn"], "ROCK", False))
    C.layer(lambda L: leg(L, (cx + 16, 94 + bob), feet["fn"], "ROCK", True))

    # 6) Cabeza con cuernos
    def head(L):
        # cuerno trasero
        L.polyline([(hx - 7, hy - 9), (hx - 12, hy - 20)], "HORN_SH", 6)
        L.polyline([(hx - 12, hy - 20), (hx - 8, hy - 30)], "HORN_SH", 4)
        L.polyline([(hx - 8, hy - 30), (hx + 2, hy - 35)], "HORN_SH", 2)
        pts = [(hx - 10, hy - 11), (hx + 8, hy - 9), (hx + 17, hy - 1),
               (hx + 16, hy + 5), (hx - 6, hy + 8), (hx - 12, hy + 2)]
        pix = set(poly_pixels(pts))
        for x, y in pix:
            c = "ROCK"
            if x < hx - 4:
                c = "ROCK_D"
            if (x, y - 1) not in pix:
                c = "ROCK_L"
            L.put(x, y, c)
        L.line(hx - 2, hy - 6, hx + 10, hy - 4, "ROCK_HL")              # ceja
        L.rect(hx + 5, hy - 4, hx + 7, hy - 3, "LAVA_L")                # ojo
        L.put(hx + 6, hy - 4, "EMBER")
        L.clip = True
        L.line(hx - 7, hy - 2, hx + 1, hy + 3, lava_edge, 2)            # grieta facial
        L.line(hx - 7, hy - 2, hx + 1, hy + 3, lava_core, 1)
        L.clip = False
        # cuerno delantero
        L.polyline([(hx - 2, hy - 9), (hx - 4, hy - 21)], "HORN", 6)
        L.polyline([(hx - 4, hy - 21), (hx + 4, hy - 29)], "HORN", 4)
        L.polyline([(hx + 4, hy - 29), (hx + 14, hy - 31)], "HORN", 2)
        L.line(hx + 1, hy - 10, hx - 1, hy - 20, "HORN_SH")
    C.layer(head)

    # 7) Mandíbula (y boca incandescente)
    def jaw(L):
        j = P["jaw"]
        if j >= 2:
            mouth_col = {0: "LAVA", 1: "LAVA_L", 2: "EMBER"}[P["mouth"]]
            L.polygon([(hx - 4, hy + 4), (hx + 15, hy + 3), (hx + 15, hy + 5 + j * 0.7),
                       (hx - 4, hy + 9)], "LAVA_D")
            L.polygon([(hx - 2, hy + 5), (hx + 12, hy + 4), (hx + 12, hy + 4 + j * 0.6),
                       (hx - 2, hy + 8)], mouth_col)
        L.polygon([(hx - 6, hy + 5 + j * 0.2), (hx + 15, hy + 5 + j * 0.8),
                   (hx + 13, hy + 10 + j), (hx - 4, hy + 10)], "ROCK_D")
        for k in (4, 8, 12):                                           # dientes
            L.put(hx + k, hy + 5 + j * (0.2 + 0.6 * k / 15.0), "HORN")
    C.layer(jaw)

    M = C.main
    if P["flash"]:
        flash(M, B_FLASH)
    if P["crumble"] > 0:
        M = crumble(M, P["crumble"], P["seed"], block=5, dust=("SMOKE_L",))
    # Efectos
    if P["flames"]:
        for k in range(5):
            a = math.radians(-150 + k * 27)
            fx0 = bx + 37 * math.cos(a)
            fy0 = by + 25 * math.sin(a)
            h = 6 + P["flames"] * 4 + int(hash01(k, P["seed"], 9) * 6)
            M.polygon([(fx0 - 3, fy0), (fx0 + 1, fy0 - h), (fx0 + 4, fy0)], "LAVA")
            M.polygon([(fx0 - 1, fy0), (fx0 + 1, fy0 - h * 0.6), (fx0 + 2, fy0)], "LAVA_L")
            M.put(fx0 + 1, fy0 - 1, "EMBER")
    if P["eye_star"]:
        fx_star(M, hx + 6, hy - 4, P["eye_star"], "EMBER", "LAVA_L", "LAVA")
    if P["embers"]:
        fx_embers(M, P["seed"] * 13 + 5, P["embers"] * 3, bx - 40, by - 40,
                  bx + 50, by + 40, ["LAVA", "LAVA_D", "LAVA_L"])
    apply_fx(M, P["fx"], B_FX)
    return M


def ceniza_poses():
    braced = dict(fn=(16, 0), ff=(10, 0), bn=(-26, 0), bf=(-32, 0))
    charge = dict(fn=(28, -2), ff=(22, 0), bn=(-30, 0), bf=(-36, -1))
    buckled = dict(fn=(22, 0), ff=(16, 0), bn=(-30, 0), bf=(-36, 0))

    idle = [dict(bob=[0, 0, 1, 1][i], glow=[1, 1, 2, 1][i], seed=i + 1)
            for i in range(4)]

    move = []
    for i in range(6):
        t = 2 * math.pi * i / 6

        def foot(phi, base):
            return (round(base + 6 * math.sin(phi)),
                    -round(4 * max(0.0, math.cos(phi))))
        move.append(dict(bob=[0, 1, 1, 0, 1, 1][i], head_dy=[0, 1, 2, 1, 0, 1][i],
                         seed=i + 10,
                         feet=dict(fn=foot(t, 20), bf=foot(t, -30),
                                   ff=foot(t + math.pi, 14),
                                   bn=foot(t + math.pi, -24))))

    melee = [
        # 0 AVISO 1: se echa atrás, alza la cabeza, ojo destella, humo
        dict(dx=-4, head_dy=-6, jaw=3, mouth=1, glow=2, eye_star=7, feet=braced,
             fx=[("puff", LX + 52, 68, 3, "SMOKE", "SMOKE_L")]),
        # 1 AVISO 2: baja los cuernos, la lava brilla al máximo
        dict(dx=-7, bob=3, head_dy=5, glow=3, eye_star=10, feet=braced, embers=5,
             seed=3),
        # 2 EMBESTIDA
        dict(dx=6, bob=1, head_dy=4, glow=3, feet=charge, embers=4, seed=4,
             fx=[("streaks", [(4, 70, 24, 70, "LAVA_D"), (8, 84, 30, 84, "LAVA"),
                              (2, 98, 20, 98, "LAVA_D")])]),
        # 3 IMPACTO
        dict(dx=7, bob=1, head_dy=6, glow=3, feet=charge, seed=5,
             fx=[("star", LX + 60, 92, 8), ("debris", LX + 54, 120, 8, 2)]),
        # 4 RECUPERACIÓN 1
        dict(dx=3, head_dy=3, glow=2),
        # 5 RECUPERACIÓN 2
        dict(),
    ]

    special = [
        # 0 AVISO 1: alza la cabeza, la garganta se ilumina
        dict(bob=-1, head_dy=-6, head_dx=-2, jaw=3, mouth=1, glow=2, eye_star=6),
        # 1 AVISO 2: boca abierta incandescente
        dict(bob=-1, head_dy=-9, head_dx=-3, jaw=5, mouth=2, glow=3, eye_star=9,
             fx=[("star", LX + 40, 86, 6)]),
        # 2 ESCUPE: la bola de lava sale de la boca
        dict(head_dy=2, head_dx=3, jaw=9, mouth=2, glow=3,
             fx=[("ball", LX + 57, 97, 5, "LAVA_D", "LAVA", "LAVA_L",
                  [(-4, 1), (-7, 2)])]),
        # 3 PROYECTIL en vuelo
        dict(head_dy=1, head_dx=2, jaw=6, mouth=1, glow=2,
             fx=[("ball", LX + 59, 82, 6, "LAVA_D", "LAVA", "EMBER",
                  [(-4, 5), (-7, 9), (-9, 12)]),
                 ("motes", [(LX + 52, 104, "LAVA"), (LX + 50, 108, "LAVA_D")])]),
        # 4 Gotas de lava cayendo de la boca
        dict(head_dy=1, jaw=3, mouth=1, glow=2,
             fx=[("motes", [(LX + 48, 104, "LAVA"), (LX + 49, 109, "LAVA_L"),
                            (LX + 46, 114, "LAVA_D"), (LX + 64, 70, "LAVA")])]),
        # 5 RECUPERACIÓN
        dict(glow=1),
    ]

    hurt = [
        dict(dx=-4, glow=2, flash=True, fx=[("star", LX + 30, 68, 8)]),
        dict(dx=-2, glow=2, fx=[("motes", [(LX + 32, 66, "EMBER"),
                                           (LX + 28, 72, "LAVA_L")])]),
    ]

    phase2 = [
        # 0 Se agazapa
        dict(bob=4, head_dy=6, glow=2, embers=6, seed=31),
        # 1 Rugido: boca abierta, ojo y lava al máximo
        dict(head_dy=-8, jaw=9, mouth=2, glow=3, embers=10, eye_star=9, seed=32),
        # 2 El caparazón estalla en llamas
        dict(jaw=5, mouth=1, glow=3, flames=2, embers=12, seed=33),
        # 3 Nuevo aspecto de fase 2
        dict(glow=3, flames=1, embers=8, seed=34),
    ]

    death = [
        # 0 Tambaleo
        dict(dx=-3, head_dy=-3, jaw=4, mouth=0, glow=2),
        # 1 Se desploma, la cabeza toca el suelo
        dict(bob=8, head_dy=13, jaw=2, glow=1, feet=buckled, embers=2),
        # 2 La lava se enfría, sale humo
        dict(bob=9, head_dy=13, glow=0, feet=buckled, embers=0,
             fx=[("puff", LX - 10, 50, 5, "SMOKE", "SMOKE_L"),
                 ("puff", LX + 10, 56, 4, "SMOKE", "SMOKE_L")]),
    ]
    # 3-7 Roca fría que se desmorona entre humo
    for amount, seed in ((0.12, 3), (0.3, 4), (0.52, 5), (0.78, 6), (1.0, 7)):
        puffs = [("puff", LX - 14 + int(amount * 6), int(60 - amount * 30), 4 + int(amount * 3),
                  "SMOKE", "SMOKE_L")] if amount < 1.0 else []
        death.append(dict(bob=9, head_dy=13, glow=-1, feet=buckled, embers=0,
                          crumble=amount, seed=seed, fx=puffs))

    return dict(idle=idle, move=move, attack_melee=melee, attack_special=special,
                hurt=hurt, phase2=phase2, death=death)


# ===========================================================================
# JEFE 3: HERALDO CELESTIAL
# ===========================================================================
H_PALETTE = {
    "OUT":     (0x0A, 0x0A, 0x12),
    "ROBE_D":  (0x2E, 0x25, 0x52),
    "ROBE":    (0x5B, 0x4A, 0x9E),
    "ROBE_L":  (0x82, 0x70, 0xC8),
    "GOLD_D":  (0xC9, 0x9A, 0x4A),
    "GOLD":    (0xFF, 0xD9, 0x8A),
    "GOLD_L":  (0xFF, 0xF4, 0xD1),
    "WING_SH": (0xA9, 0xA2, 0xCC),
    "WING":    (0xE8, 0xE4, 0xF8),
    "HALO":    (0xFF, 0xE9, 0xA8),
    "LIGHT":   (0xFF, 0xFF, 0xFF),
}
H_FLASH = {"ROBE_D": "ROBE_L", "ROBE": "WING_SH", "ROBE_L": "WING",
           "GOLD_D": "GOLD_L", "GOLD": "LIGHT", "WING_SH": "LIGHT",
           "WING": "LIGHT", "OUT": "ROBE"}
H_FX = dict(core="LIGHT", edge="HALO", mid="GOLD_L", dark="GOLD",
            dust="GOLD_L", debris="HALO", white="LIGHT")

H_DEFAULT = dict(dx=0, bob=0, head_dy=0, flap=0, back=0, wave=0,
                 hand=(16, 66), angle=-75, spear=True, bhand=(-10, 72),
                 halo=1, rays=0, phase2=False, eye=0, spear_star=0,
                 orbs=(), bolts=(), halo_broken=0,
                 flash=False, dissolve=0.0, seed=1, fx=())


def h_wing(L, ax, ay, angles, lengths, col, shade, gold_tips):
    """Ala de plumas largas en abanico."""
    for ang, ln in zip(angles, lengths):
        ux, uy = unit(ang)
        px, py = -uy, ux
        w = 3.5
        tip = (ax + ux * ln, ay + uy * ln)
        pts = [(ax + px * w, ay + py * w),
               (ax + ux * ln * 0.7 + px * w, ay + uy * ln * 0.7 + py * w),
               tip,
               (ax + ux * ln * 0.7 - px * w, ay + uy * ln * 0.7 - py * w),
               (ax - px * w, ay - py * w)]
        L.polygon(pts, col)
        L.line(ax + ux * 3 - px * 2, ay + uy * 3 - py * 2,
               ax + ux * ln * 0.85 - px * 1, ay + uy * ln * 0.85 - py * 1, shade)
        if gold_tips:
            L.put(tip[0], tip[1], "GOLD")
            L.put(tip[0] - ux * 1.5, tip[1] - uy * 1.5, "GOLD")


def draw_heraldo(pose):
    P = dict(H_DEFAULT)
    P.update(pose)
    cx, bob = LX + P["dx"], P["bob"]
    sy = 48 + bob
    mx, my = cx + 3, 37 + bob + P["head_dy"]       # máscara
    C = Composer()
    flap = P["flap"]

    # 1) Halo (luz pura, sin contorno)
    def halo(L):
        if P["halo"] <= 0:
            return
        hcx, hcy = cx + 1, 30 + bob
        r = 15 + (2 if P["halo"] >= 2 else 0)
        broken = P["halo_broken"]
        skip = None
        if broken:
            skip = (lambda a: (-60 < a < -20) or (100 < a < 150)) if broken == 1 else \
                   (lambda a: (-90 < a < 0) or (80 < a < 170))
        L.ring(hcx, hcy, r - 1.5, r + 0.5, "HALO", skip)
        L.ring(hcx, hcy, r - 2.5, r - 1.5, "GOLD_L", skip)
        rays = P["rays"] + (1 if P["phase2"] else 0)
        if rays:
            for k in range(12):
                ux, uy = unit(k * 30 + 15)
                L.line(hcx + ux * (r + 3), hcy + uy * (r + 3),
                       hcx + ux * (r + 3 + rays * 4), hcy + uy * (r + 3 + rays * 4),
                       "HALO" if k % 2 else "GOLD_L")
    C.layer(halo, with_outline=False)

    # 2) Ala trasera
    C.layer(lambda L: h_wing(L, cx - 6, sy + 2,
                             [a - flap for a in (-170, -150, -130, -112, -95, -80)],
                             [32, 42, 50, 50, 42, 32], "WING_SH", "ROBE_L",
                             P["phase2"]))
    # 3) Brazo trasero
    bh = (cx + P["bhand"][0], P["bhand"][1] + bob)

    def back_arm(L):
        L.line(cx - 6, sy + 5, bh[0], bh[1], "ROBE_D", 5)
        L.ellipse(bh[0], bh[1], 2, 2, "GOLD_D")
    C.layer(back_arm)

    # 4) Túnica violeta flotante con ribete dorado
    def robe(L):
        back, wave = P["back"], P["wave"]
        pts = [(cx - 10, sy - 2), (cx + 10, sy - 2), (cx + 14, sy + 10),
               (cx + 16, sy + 40), (cx + 8, sy + 48 + wave),
               (cx - 2 - back * 0.5, sy + 56 - wave),
               (cx - 10 - back, sy + 62), (cx - 14 - back * 0.5, sy + 40),
               (cx - 14, sy + 10)]
        pix = set(poly_pixels(pts))
        trim = "GOLD_L" if P["phase2"] else "GOLD"
        for x, y in pix:
            c = "ROBE"
            if x < cx - 5:
                c = "ROBE_D"
            if (x + 1, y) not in pix:
                c = "ROBE_L"
            if (x, y + 1) not in pix:
                c = trim
            L.put(x, y, c)
        L.clip = True
        L.line(cx - 14, sy + 18, cx + 14, sy + 18, "GOLD_D", 2)        # cinturón
        L.line(cx + 6, sy + 2, cx + 10, sy + 44, trim, 2)               # estola
        L.clip = False
    C.layer(robe)

    # 5) Ala delantera
    C.layer(lambda L: h_wing(L, cx - 2, sy + 6,
                             [a - flap * 1.1 for a in (-175, -155, -135, -118, -102, -88)],
                             [34, 44, 52, 52, 44, 34], "WING", "WING_SH",
                             P["phase2"]))

    # 6) Capucha + máscara dorada
    C.layer(lambda L: L.ellipse(cx, 36 + bob + P["head_dy"], 10, 11, "ROBE_D"))

    def mask(L):
        for (x0, y0, x1, y1) in ((mx - 3, my - 8, mx - 4, my - 13),
                                 (mx, my - 9, mx, my - 15),
                                 (mx + 3, my - 8, mx + 4, my - 13)):
            L.line(x0, y0, x1, y1, "GOLD", 1)                          # corona
        L.ellipse(mx, my, 7, 9, "GOLD")
        for y in range(my - 10, my + 10):
            for x in range(mx - 8, mx + 9):
                if L.get(x, y) == "GOLD":
                    if x < mx - 3 or y > my + 5:
                        L.put(x, y, "GOLD_D")
                    elif y < my - 5 and x > mx:
                        L.put(x, y, "GOLD_L")
        L.line(mx - 1, my - 1, mx + 2, my, "OUT")                       # ojos rasgados
        L.line(mx + 4, my, mx + 6, my - 1, "OUT")
        if P["eye"]:
            L.put(mx + 1, my, "LIGHT")
            L.put(mx + 5, my, "LIGHT")
    C.layer(mask)

    # 7) Lanza de luz + brazo delantero
    hand = (cx + P["hand"][0], P["hand"][1] + bob)
    head_pos = [None]

    def spear_arm(L):
        if P["spear"]:
            ux, uy = unit(P["angle"])
            px, py = -uy, ux
            butt = (hand[0] - ux * 24, hand[1] - uy * 24)
            tip = (hand[0] + ux * 30, hand[1] + uy * 30)
            L.line(butt[0], butt[1], tip[0], tip[1], "HALO", 2)
            L.line(butt[0], butt[1], tip[0], tip[1], "LIGHT", 1)
            L.ellipse(butt[0], butt[1], 1.5, 1.5, "GOLD")
            L.polygon([tip, (tip[0] + ux * 5 + px * 4, tip[1] + uy * 5 + py * 4),
                       (tip[0] + ux * 13, tip[1] + uy * 13),
                       (tip[0] + ux * 5 - px * 4, tip[1] + uy * 5 - py * 4)], "HALO")
            L.line(tip[0] + ux * 2, tip[1] + uy * 2, tip[0] + ux * 10,
                   tip[1] + uy * 10, "LIGHT", 1)
            head_pos[0] = (tip[0] + ux * 6, tip[1] + uy * 6)
        L.line(cx + 7, sy + 4, hand[0], hand[1], "ROBE", 6)
        L.ellipse(hand[0], hand[1], 2, 2, "GOLD_L")
    C.layer(spear_arm)

    M = C.main
    if P["flash"]:
        flash(M, H_FLASH)
    if P["dissolve"] > 0:
        dissolve(M, P["dissolve"], P["seed"], ["LIGHT", "HALO", "GOLD_L", "GOLD"])
    # Efectos
    if P["spear_star"] and head_pos[0]:
        fx_star(M, int(head_pos[0][0]), int(head_pos[0][1]), P["spear_star"],
                "LIGHT", "HALO", "GOLD")
    for ox, oy, r in P["orbs"]:
        M.ellipse(cx + ox, oy + bob, r + 1, r + 1, "HALO")
        M.ellipse(cx + ox, oy + bob, r, r, "LIGHT")
        if r >= 3:
            fx_star(M, cx + ox, oy + bob, r + 3, "LIGHT", "HALO", "GOLD")
    for ox, oy, ang, ln in P["bolts"]:
        fx_bolt(M, cx + ox, oy, ang, ln, "LIGHT", "HALO", "GOLD_L")
    apply_fx(M, P["fx"], H_FX)
    return M


def heraldo_poses():
    idle = [dict(bob=[0, -1, -2, -1][i], flap=[0, 8, 14, 8][i],
                 wave=[0, 1, 0, -1][i]) for i in range(4)]

    move = [dict(bob=[0, -2, -3, -2, 0, 1][i], flap=[0, 20, 35, 20, 0, -15][i],
                 dx=[0, 1, 2, 1, 0, -1][i], back=[3, 4, 5, 4, 3, 2][i],
                 wave=[1, 0, -1, 0, 1, 0][i], hand=(14, 66), angle=-60)
            for i in range(6)]

    melee = [
        # 0 AVISO 1: echa la lanza atrás, la punta se ilumina
        dict(dx=-4, flap=-15, back=2, hand=(4, 62), angle=-8, spear_star=8, eye=1),
        # 1 AVISO 2: máxima tensión, destello grande + halo intenso
        dict(dx=-7, flap=-22, back=3, hand=(0, 60), angle=-6, spear_star=12,
             eye=1, halo=2),
        # 2 ESTOCADA
        dict(dx=5, flap=25, back=7, hand=(16, 62), angle=0, eye=1,
             fx=[("streaks", [(LX - 10, 60, LX + 30, 60, "HALO"),
                              (LX - 4, 64, LX + 26, 64, "GOLD_L"),
                              (LX - 16, 57, LX + 10, 57, "GOLD")])]),
        # 3 IMPACTO
        dict(dx=5, flap=15, back=6, hand=(16, 62), angle=0, eye=1,
             fx=[("star", LX + 61, 62, 7)]),
        # 4 RECUPERACIÓN 1
        dict(dx=2, flap=5, back=3, hand=(14, 64), angle=-30),
        # 5 RECUPERACIÓN 2
        dict(),
    ]

    special = [
        # 0 AVISO 1: lanza al cielo, el halo se enciende, se forman orbes
        dict(flap=-20, hand=(10, 48), angle=-88, halo=2, rays=1, eye=1,
             orbs=[(-22, 46, 2), (26, 40, 2), (30, 72, 2)]),
        # 1 AVISO 2: orbes cargados, rayos del halo, alas extendidas
        dict(flap=-28, bob=-2, hand=(10, 48), angle=-88, halo=2, rays=2, eye=1,
             spear_star=6, orbs=[(-24, 44, 4), (28, 38, 4), (32, 72, 4)]),
        # 2 LANZA: los orbes salen disparados como rayos
        dict(flap=10, hand=(14, 58), angle=-25, rays=1, eye=1,
             bolts=[(34, 42, 12, 12), (40, 62, 6, 12), (34, 80, 0, 12)]),
        # 3 Rayos en vuelo
        dict(flap=4, hand=(14, 60), angle=-35, eye=1,
             bolts=[(46, 45, 12, 12), (52, 64, 6, 12), (46, 81, 0, 12)]),
        # 4 Rayos alejándose
        dict(flap=0, hand=(14, 62), angle=-50,
             bolts=[(56, 47, 12, 12), (60, 65, 6, 12), (54, 81, 0, 12)]),
        # 5 RECUPERACIÓN
        dict(),
    ]

    hurt = [
        dict(dx=-4, flap=25, flash=True, fx=[("star", LX + 14, 56, 8)]),
        dict(dx=-2, flap=10, fx=[("motes", [(LX + 16, 54, "LIGHT"),
                                            (LX + 12, 62, "HALO")])]),
    ]

    phase2 = [
        # 0 Se envuelve con las alas
        dict(flap=40, bob=3, halo=1),
        # 1 Se abre de golpe, el halo estalla en rayos
        dict(flap=-35, bob=-2, halo=2, rays=3, eye=1,
             fx=[("ring", LX, 60, 50), ("ring", LX, 60, 42)]),
        # 2 Plumas doradas, túnica radiante
        dict(flap=-20, halo=2, rays=2, phase2=True, eye=1,
             fx=[("motes", [(LX - 30, 30, "LIGHT"), (LX + 34, 50, "HALO"),
                            (LX - 20, 90, "GOLD_L"), (LX + 26, 96, "LIGHT")])]),
        # 3 Nuevo aspecto de fase 2
        dict(flap=0, halo=2, phase2=True, eye=1),
    ]

    death = [
        # 0 Retrocede, el halo se agrieta
        dict(dx=-3, flap=30, halo_broken=1),
        # 1 Cae inclinado, alas caídas, suelta la lanza
        dict(bob=6, head_dy=2, flap=45, halo_broken=2, spear=False,
             hand=(10, 74), fx=[("motes", [(LX + 30, 100, "HALO"),
                                           (LX + 34, 108, "LIGHT")])]),
        # 2 El halo desaparece
        dict(bob=8, head_dy=3, flap=50, halo=0, spear=False, hand=(10, 76)),
    ]
    # 3-7 Se disuelve en luz
    for amount, seed in ((0.15, 3), (0.35, 4), (0.58, 5), (0.8, 6), (1.0, 7)):
        death.append(dict(bob=8, head_dy=3, flap=50, halo=0, spear=False,
                          hand=(10, 76), dissolve=amount, seed=seed))

    return dict(idle=idle, move=move, attack_melee=melee, attack_special=special,
                hurt=hurt, phase2=phase2, death=death)


# ---------------------------------------------------------------------------
# REGISTRO DE JEFES Y EXPORTACIÓN
# ---------------------------------------------------------------------------
BOSSES = {
    "guardian": dict(title="Guardián de las Ruinas", level=1, palette=G_PALETTE,
                     draw=draw_guardian, poses=guardian_poses,
                     pivot=(0.5, round(3 / FRAME, 6)),
                     pivot_note="Pivote en los pies: X=64, contorno inferior en y=124."),
    "ceniza": dict(title="Bestia de Ceniza", level=2, palette=B_PALETTE,
                   draw=draw_ceniza, poses=ceniza_poses,
                   pivot=(0.5, round(3 / FRAME, 6)),
                   pivot_note="Pivote en los pies: X=64, contorno inferior en y=124."),
    "heraldo": dict(title="Heraldo Celestial", level=3, palette=H_PALETTE,
                    draw=draw_heraldo, poses=heraldo_poses,
                    pivot=(0.5, 0.5),
                    pivot_note="Flota: pivote en el centro del cuerpo (X=64, y=64)."),
}


def canvas_to_image(cv, palette):
    img = Image.new("RGBA", (FRAME, FRAME), (0, 0, 0, 0))
    pix = img.load()
    for y in range(FRAME):
        for x in range(FRAME):
            c = cv.px[y][x]
            if c is not None:
                r, g, b = palette[c]
                pix[x, y] = (r, g, b, 255)
    return img


def build(boss_id):
    spec = BOSSES[boss_id]
    assert len(spec["palette"]) <= 16, "Paleta > 16 colores"
    all_poses = spec["poses"]()
    sheet = Image.new("RGBA", (COLS * FRAME, len(ANIMATIONS) * FRAME), (0, 0, 0, 0))
    frames_meta, anims_meta = [], {}
    for row, (name, count, fps, loop) in enumerate(ANIMATIONS):
        poses = all_poses[name]
        assert len(poses) == count, f"{boss_id}/{name}: {len(poses)} != {count}"
        anims_meta[name] = {"row": row, "frames": count, "fps": fps, "loop": loop}
        for i, pose in enumerate(poses):
            cv = spec["draw"](pose).mirrored()          # mira a la IZQUIERDA
            x, y = i * FRAME, row * FRAME
            sheet.paste(canvas_to_image(cv, spec["palette"]), (x, y))
            frames_meta.append({"name": f"boss_{boss_id}_{name}_{i}",
                                "animation": name, "index": i,
                                "x": x, "y": y, "width": FRAME, "height": FRAME})
    png = f"boss_{boss_id}.png"
    sheet.save(png)
    meta = {
        "boss": spec["title"], "level": spec["level"], "image": png,
        "size": {"width": sheet.width, "height": sheet.height},
        "frame_size": {"width": FRAME, "height": FRAME},
        "columns": COLS, "facing": "left",
        "coordinate_origin": "top-left (en Unity: y_unity = altura_hoja - y - alto)",
        "pivot_normalized": {"x": spec["pivot"][0], "y": spec["pivot"][1]},
        "pivot_note": spec["pivot_note"],
        "telegraph_frames": {"attack_melee": [0, 1], "attack_special": [0, 1]},
        "palette": {k: "#%02X%02X%02X" % v for k, v in spec["palette"].items()},
        "animations": anims_meta,
        "frames": frames_meta,
    }
    with open(f"boss_{boss_id}.json", "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
    sheet.resize((sheet.width * PREVIEW_SCALE, sheet.height * PREVIEW_SCALE),
                 Image.NEAREST).save(f"boss_{boss_id}_preview.png")
    print(f"OK: {png} ({sheet.width}x{sheet.height}), {len(frames_meta)} frames")


def main():
    ids = sys.argv[1:] or list(BOSSES)
    for b in ids:
        if b not in BOSSES:
            raise SystemExit(f"Jefe desconocido: {b}. Opciones: {', '.join(BOSSES)}")
        build(b)


if __name__ == "__main__":
    main()
