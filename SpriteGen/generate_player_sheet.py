#!/usr/bin/env python3
"""
generate_player_sheet.py
Unfilial - Hoja de sprites pixel art del protagonista (semidiós).

Genera:
  - player_sheet.png    : hoja 8 columnas x 11 filas, frames de 64x64, fondo transparente
  - player_sheet.json   : nombre, x, y, ancho y alto de cada frame (+ animaciones y pivote)
  - player_preview.png  : previsualización a 4x con vecino más cercano

Requisitos: Python 3.8+ y Pillow  (pip install pillow)

Convenciones:
  - El personaje mira a la DERECHA, centrado en X=32.
  - El contorno inferior de los pies está en y=60 (desde arriba).
  - Sin antialiasing: todo se pinta píxel a píxel con una paleta cerrada.
  - Cada pieza (pierna, capa, cabeza, espada, escudo) se dibuja en su propia
    capa, recibe su contorno de 1 px (#0A0A12) y luego se compone. Así las
    piezas que se solapan quedan separadas por una línea oscura legible.
"""

import json
import math
from PIL import Image

# ---------------------------------------------------------------------------
# CONFIGURACIÓN GENERAL
# ---------------------------------------------------------------------------
FRAME = 64
COLS = 8
CX = 32          # centro horizontal del personaje
GROUND = 59      # última fila de relleno del pie (el contorno cae en y=60)
PREVIEW_SCALE = 4

# Paleta cerrada (12 colores + transparente; límite: 16)
PALETTE = {
    "OUT":      (0x0A, 0x0A, 0x12),  # contorno y ojos
    "CAPE":     (0x23, 0x26, 0x38),  # capa azul noche
    "CAPE_SH":  (0x17, 0x19, 0x26),  # sombra de la capa / pierna trasera
    "CAPE_HI":  (0x34, 0x39, 0x52),  # luz de la capa / pierna delantera / mano
    "MASK":     (0xF2, 0xEE, 0xE4),  # máscara blanco hueso
    "MASK_SH":  (0xC4, 0xBD, 0xAE),  # sombra de la máscara
    "GOLD":     (0xFF, 0xD9, 0x8A),  # brillo dorado divino
    "GOLD_SH":  (0xC9, 0x9A, 0x4A),  # oro en sombra
    "GLOW":     (0xFF, 0xF4, 0xD1),  # destello dorado muy claro
    "SWORD":    (0xD9, 0xDF, 0xF2),  # plata de la espada y el escudo
    "SWORD_SH": (0x8E, 0x97, 0xB5),  # plata en sombra
    "WHITE":    (0xFF, 0xFF, 0xFF),  # estelas de corte y destellos
}

# Orden de filas: (nombre, nº de frames, fps sugeridos, loop)
ANIMATIONS = [
    ("idle", 4, 6, True),
    ("run", 8, 12, True),
    ("jump", 3, 10, False),
    ("fall", 3, 10, True),
    ("attack", 5, 18, False),
    ("attack_up", 4, 16, False),
    ("attack_down", 4, 16, False),
    ("block", 3, 12, False),
    ("dash", 4, 16, False),
    ("hurt", 2, 10, False),
    ("death", 6, 8, False),
]


# ---------------------------------------------------------------------------
# LIENZO Y PRIMITIVAS (sin antialiasing)
# ---------------------------------------------------------------------------
class Canvas:
    """Lienzo de 64x64 que guarda nombres de color (o None = transparente)."""

    def __init__(self, w=FRAME, h=FRAME):
        self.w, self.h = w, h
        self.px = [[None] * w for _ in range(h)]

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[y][x]
        return None

    def put(self, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[y][x] = c

    def stamp(self, x, y, c, w):
        """Pincel: 1 px, cuadrado 2x2 o disco para anchos >= 3."""
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
        for y in range(int(min(y0, y1)), int(max(y0, y1)) + 1):
            for x in range(int(min(x0, x1)), int(max(x0, x1)) + 1):
                self.put(x, y, c)

    def ellipse(self, cx, cy, rx, ry, c):
        rx, ry = max(rx, 0.01), max(ry, 0.01)
        for y in range(int(math.floor(cy - ry)) - 1, int(math.ceil(cy + ry)) + 2):
            for x in range(int(math.floor(cx - rx)) - 1, int(math.ceil(cx + rx)) + 2):
                dx, dy = (x - cx) / rx, (y - cy) / ry
                if dx * dx + dy * dy <= 1.0:
                    self.put(x, y, c)

    def line(self, x0, y0, x1, y1, c, w=1):
        """Bresenham con pincel de ancho w."""
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

    def paste(self, other):
        """Compone otra capa encima (solo píxeles no transparentes)."""
        for y in range(self.h):
            row, orow = self.px[y], other.px[y]
            for x in range(self.w):
                if orow[x] is not None:
                    row[x] = orow[x]


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
    """Contorno de 1 px (vecindad de 4) alrededor de todo lo pintado."""
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
    """Ruido determinista en [0,1) para disolución y partículas."""
    n = (x * 374761393 + y * 668265263 + s * 982451653) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFFFFFF) / 4294967296.0


# ---------------------------------------------------------------------------
# PIEZAS DEL PERSONAJE
# ---------------------------------------------------------------------------
def draw_shield(L, x, y, rx, ry, glint=False):
    """Escudo redondo plateado con emblema dorado en forma de rombo."""
    L.ellipse(x, y, rx, ry, "SWORD_SH")                 # aro exterior
    L.ellipse(x, y, rx - 1, ry - 1, "SWORD")            # cara
    L.put(x - rx + 2, y - ry + 2, "WHITE")              # reflejo
    L.put(x - rx + 2, y - ry + 3, "WHITE")
    # Emblema dorado (rombo)
    for ox, oy in ((0, -2), (-1, -1), (0, -1), (1, -1), (-2, 0), (-1, 0),
                   (1, 0), (2, 0), (-1, 1), (0, 1), (1, 1), (0, 2)):
        L.put(x + ox, y + oy, "GOLD")
    L.put(x, y, "GLOW")
    L.put(x + 1, y + 1, "GOLD_SH")
    L.put(x, y + 2, "GOLD_SH")
    if glint:                                           # destello divino
        L.put(x + rx - 1, y - ry + 2, "GLOW")
        L.put(x + rx - 2, y - ry + 1, "GLOW")


def draw_leg(L, hip, cx, leg, color):
    """Pierna en dos segmentos (muslo, espinilla) + bota."""
    (kx, ky), (fx, fy) = leg
    knee = (cx + kx, GROUND + ky)
    foot = (cx + fx, GROUND + fy)
    L.line(hip[0], hip[1], knee[0], knee[1], color, 3)
    L.line(knee[0], knee[1], foot[0], foot[1] - 1, color, 3)    # el pincel no pasa de y=59
    L.rect(foot[0] - 1, foot[1] - 1, foot[0] + 2, foot[1], "CAPE_SH")  # bota


def draw_cape(L, cx, neck_x, neck_y, hem_y, back, lift, front, front_lift, wave):
    """Cuerpo-capa: polígono que se deforma con la acción.

    back       -> cuánto se estira la capa hacia atrás (carrera, dash)
    lift       -> cuánto se levanta el borde trasero (caída)
    front      -> cuánto se adelanta el borde delantero (golpe recibido)
    front_lift -> cuánto se levanta el borde delantero
    wave       -> ondulación de ±1-2 px en el borde inferior
    """
    pts = [
        (neck_x - 5, neck_y),                                  # cuello trasero
        (neck_x + 5, neck_y),                                  # cuello delantero
        (neck_x + 7, neck_y + 5),                              # hombro delantero
        (cx + 7 + front, hem_y - front_lift),                  # borde delantero
        (cx + 1 - back * 0.3, hem_y + 1 - lift * 0.4),         # centro del borde
        (cx - 4 - back * 0.6, hem_y - lift * 0.6 + wave),      # ondulación
        (cx - 8 - back, hem_y - 1 - lift),                     # borde trasero
        (cx - 10 - back * 1.2, hem_y - 4 - lift * 1.1 - wave), # punta que ondea
        (neck_x - 8 - back * 0.5, neck_y + 10 - lift * 0.3),   # espalda
        (neck_x - 6, neck_y + 3),                              # hombro trasero
    ]
    pix = set(poly_pixels(pts))
    shade_x = cx - 4 - back * 0.3
    for (x, y) in pix:
        c = "CAPE"
        if x <= shade_x:
            c = "CAPE_SH"                                      # pliegue en sombra
        if (x + 1, y) not in pix and y < hem_y - 1:
            c = "CAPE_HI"                                      # luz del borde delantero
        if (x, y + 1) not in pix:
            c = "GOLD"                                         # ribete dorado
        L.put(x, y, c)
    # broche dorado en el cuello
    L.put(neck_x + 3, neck_y + 2, "GOLD")
    L.put(neck_x + 4, neck_y + 2, "GOLD_SH")


def draw_head(L, hx, hy, eyes):
    """Máscara blanca hueso, dos cuernos curvos y dos grandes ojos negros."""
    # Cuernos (detrás y delante), se curvan hacia arriba y ligeramente afuera
    back_horn = [(hx - 3, hy - 4), (hx - 5, hy - 8), (hx - 6, hy - 11),
                 (hx - 5, hy - 14), (hx - 4, hy - 15)]
    front_horn = [(hx + 3, hy - 4), (hx + 4, hy - 8), (hx + 5, hy - 11),
                  (hx + 5, hy - 13), (hx + 4, hy - 14)]
    L.polyline(back_horn[:3], "MASK_SH", 2)
    L.polyline(back_horn[2:], "MASK_SH", 1)
    L.polyline(front_horn[:3], "MASK", 2)
    L.polyline(front_horn[2:], "MASK", 1)
    # Máscara
    L.ellipse(hx, hy, 7.5, 7, "MASK")
    for y in range(int(hy) - 8, int(hy) + 9):
        for x in range(int(hx) - 9, int(hx) + 10):
            if L.get(x, y) == "MASK":
                if y >= hy + 5 or x <= hx - 6:
                    L.put(x, y, "MASK_SH")                     # sombra mandíbula / nuca
    # Ojos (desplazados hacia la derecha porque mira a la derecha)
    eye_pos = [(hx + 0.5, hy + 1, 1.6, 2.6), (hx + 4.6, hy + 1, 1.4, 2.4)]
    for ex, ey, rx, ry in eye_pos:
        if eyes == "open":
            L.ellipse(ex, ey, rx, ry, "OUT")
        else:                                                  # ojos cerrados (dolor)
            L.line(ex - 1, ey + 1, ex + 1, ey + 1, "OUT", 1)
            L.put(ex - 1, ey, "OUT")


def draw_arm_and_sword(L, shoulder, hand, angle, length):
    """Brazo de la capa, mano, empuñadura dorada y hoja plateada fina."""
    a = math.radians(angle)
    ux, uy = math.cos(a), math.sin(a)
    px_, py_ = -uy, ux                                         # perpendicular
    hx, hy = hand
    # Brazo (manga oscura)
    L.line(shoulder[0], shoulder[1], hx, hy, "CAPE", 3)
    # Hoja
    b0 = (hx + 3 * ux, hy + 3 * uy)
    b1 = (hx + (3 + length) * ux, hy + (3 + length) * uy)
    L.line(b0[0], b0[1], b1[0], b1[1], "SWORD", 1)
    L.line(b0[0] + px_, b0[1] + py_, b1[0] - 2 * ux + px_, b1[1] - 2 * uy + py_,
           "SWORD_SH", 1)
    L.put(b1[0], b1[1], "WHITE")                               # brillo de la punta
    # Guarda dorada
    g = (hx + 2 * ux, hy + 2 * uy)
    L.line(g[0] - 2.4 * px_, g[1] - 2.4 * py_, g[0] + 2.4 * px_, g[1] + 2.4 * py_,
           "GOLD", 1)
    # Empuñadura y pomo
    L.line(hx - 2 * ux, hy - 2 * uy, hx + ux, hy + uy, "GOLD_SH", 1)
    L.put(hx - 3 * ux, hy - 3 * uy, "GOLD")
    # Mano
    L.ellipse(hx, hy, 1.2, 1.2, "CAPE_HI")


# ---------------------------------------------------------------------------
# EFECTOS (sin contorno, se pintan encima)
# ---------------------------------------------------------------------------
def fx_crescent(cv, cx, cy, r_in, thick, a0, a1):
    """Estela de tajo en forma de media luna: núcleo blanco, borde interior dorado.
    Ángulos en grados en coordenadas de pantalla (0 = derecha, 90 = abajo)."""
    R = r_in + thick + 1
    for y in range(int(cy - R), int(cy + R) + 1):
        for x in range(int(cx - R), int(cx + R) + 1):
            dx, dy = x - cx, y - cy
            r = math.hypot(dx, dy)
            a = math.degrees(math.atan2(dy, dx))
            while a < a0:
                a += 360
            while a >= a0 + 360:
                a -= 360
            if a > a1:
                continue
            f = (a - a0) / (a1 - a0)
            t = thick * math.sin(math.pi * (f ** 0.8))         # más grueso cerca del final
            if t < 0.7:
                continue
            if r_in <= r <= r_in + t:
                cv.put(x, y, "GOLD" if r < r_in + 1 else "WHITE")


def fx_spark(cv, x, y, size, c="WHITE"):
    """Destello en forma de estrella de 4 puntas."""
    cv.put(x, y, c)
    for i in range(1, size + 1):
        for ox, oy in ((i, 0), (-i, 0), (0, i), (0, -i)):
            cv.put(x + ox, y + oy, c if i < size else "GOLD")


def fx_streaks(cv, streaks):
    """Líneas de velocidad horizontales: (x0, x1, y, color)."""
    for x0, x1, y, c in streaks:
        cv.line(x0, y, x1, y, c, 1)


def fx_motes(cv, motes):
    """Partículas sueltas: (x, y, color)."""
    for x, y, c in motes:
        cv.put(x, y, c)


def apply_dissolve(cv, amount, seed):
    """Desvanece el cuerpo de arriba abajo y lo convierte en partículas doradas."""
    removed = []
    for y in range(cv.h):
        for x in range(cv.w):
            if cv.px[y][x] is None:
                continue
            yn = (y - 10) / 50.0                                # 0 arriba, 1 abajo
            if hash01(x, y, seed) < amount * 1.45 - yn * 0.45:
                removed.append((x, y))
    for x, y in removed:
        cv.px[y][x] = None
    # Partículas que suben
    for x, y in removed:
        h = hash01(x, y, seed + 7)
        if h < 0.16:
            rise = 2 + int(hash01(x, y, seed + 11) * (6 + amount * 12))
            drift = int(round(math.sin((x + y) * 0.9) * 1.5))
            c = "GLOW" if h < 0.05 else "GOLD"
            cv.put(x + drift, y - rise, c)


# ---------------------------------------------------------------------------
# PERSONAJE COMPLETO
# ---------------------------------------------------------------------------
LEGS_STAND = (((-3, -6), (-3, 0)), ((3, -6), (3, 0)))   # (trasera, delantera)

DEFAULT_POSE = dict(
    dx=0, bob=0, lean=0, head_dx=0, head_dy=0,
    cape_back=0, cape_lift=0, cape_front=0, front_lift=0, wave=0,
    legs=LEGS_STAND,
    sword=dict(hand=(6, 39), angle=75, length=17),
    shield="back",
    eyes="open",
    fx=(),
    dissolve=0.0,
    seed=1,
)


def draw_player(pose):
    P = dict(DEFAULT_POSE)
    P.update(pose)
    cx = CX + P["dx"]
    bob = P["bob"]
    neck_x, neck_y = cx + P["lean"], 25 + bob
    hip_y, hem_y = 47 + bob, 51 + bob
    hx, hy = neck_x + 1 + P["head_dx"], 18 + bob + P["head_dy"]

    main = Canvas()

    def layer(fn):
        L = Canvas()
        fn(L)
        outline(L)
        main.paste(L)

    # 1) Escudo colgado en la espalda (detrás de todo)
    if P["shield"] == "back":
        layer(lambda L: draw_shield(L, neck_x - 8, neck_y + 11, 5, 5))
    # 2) Pierna trasera y 3) pierna delantera
    back_leg, front_leg = P["legs"]
    layer(lambda L: draw_leg(L, (cx - 2, hip_y), cx, back_leg, "CAPE_SH"))
    layer(lambda L: draw_leg(L, (cx + 2, hip_y), cx, front_leg, "CAPE_HI"))
    # 4) Capa / cuerpo
    layer(lambda L: draw_cape(L, cx, neck_x, neck_y, hem_y, P["cape_back"],
                              P["cape_lift"], P["cape_front"], P["front_lift"],
                              P["wave"]))
    # 5) Cabeza
    layer(lambda L: draw_head(L, hx, hy, P["eyes"]))
    # 6) Brazo derecho + espada (lado cercano a la cámara)
    if P["sword"]:
        s = P["sword"]
        hand = (cx + s["hand"][0], s["hand"][1] + bob)
        shoulder = (neck_x + 3, neck_y + 6)
        layer(lambda L: draw_arm_and_sword(L, shoulder, hand, s["angle"], s["length"]))
    # 7) Escudo al frente (bloqueo)
    if isinstance(P["shield"], dict):
        sh = P["shield"]
        layer(lambda L: draw_shield(L, cx + sh["x"], sh["y"] + bob, sh["rx"], sh["ry"],
                                    sh.get("glint", False)))
    # 8) Disolución (muerte)
    if P["dissolve"] > 0:
        apply_dissolve(main, P["dissolve"], P["seed"])
    # 9) Efectos sin contorno
    for fx in P["fx"]:
        kind, args = fx[0], fx[1:]
        if kind == "crescent":
            fx_crescent(main, *args)
        elif kind == "spark":
            fx_spark(main, *args)
        elif kind == "streaks":
            fx_streaks(main, *args)
        elif kind == "motes":
            fx_motes(main, *args)
    return main


def sword(x, y, angle, length=17):
    return dict(hand=(x, y), angle=angle, length=length)


def legs(bk, bf, fk, ff):
    return ((bk, bf), (fk, ff))


# ---------------------------------------------------------------------------
# DEFINICIÓN DE POSES POR ANIMACIÓN
# ---------------------------------------------------------------------------
def poses_idle():
    # Respiración suave: el cuerpo baja 1 px y la capa ondea hacia atrás.
    out = []
    for i in range(4):
        out.append(dict(
            bob=[0, 0, 1, 1][i],
            cape_back=[0, 1, 2, 1][i],
            wave=[0, 1, 0, -1][i],
            sword=sword(6, 39 - [0, 0, 1, 1][i] + [0, 0, 1, 1][i], 75),
        ))
    return out


def poses_run():
    # Ciclo de carrera: piernas en fase opuesta, el pie que avanza se levanta.
    out = []
    for i in range(8):
        t = 2 * math.pi * i / 8

        def leg_at(phi, base):
            fx_ = base + 6 * math.sin(phi)
            lift = 3.5 * max(0.0, math.cos(phi))
            kx = fx_ * 0.5 + 2.5
            return ((round(kx), round(-6 - lift * 0.8)), (round(fx_), round(-lift)))

        out.append(dict(
            bob=[-1, 0, 1, 0, -1, 0, 1, 0][i],
            lean=2, head_dx=1,
            cape_back=5 + [0, 1, 2, 1, 0, 1, 2, 1][i],
            cape_lift=2 + [0, 1, 0, -1, 0, 1, 0, -1][i],
            wave=[0, 1, 0, -1, 0, 1, 0, -1][i],
            legs=(leg_at(t + math.pi, -1), leg_at(t, 1)),
            sword=sword(1, 39, 160),
        ))
    return out


def poses_jump():
    return [
        # 0 Impulso: se agacha, rodillas flexionadas
        dict(bob=3, lean=1, cape_lift=-1,
             legs=legs((-1, -4), (-4, 0), (5, -5), (3, 0)),
             sword=sword(5, 41, 140)),
        # 1 Subida: piernas estiradas hacia abajo, capa colgando
        dict(bob=-2, lean=1, cape_back=2, cape_lift=-2,
             legs=legs((-3, -4), (-4, 0), (3, -6), (2, -2)),
             sword=sword(5, 39, 130)),
        # 2 Apogeo: piernas recogidas, capa flotando
        dict(bob=-3, cape_back=3, cape_lift=3, wave=1,
             legs=legs((-1, -6), (-4, -3), (5, -7), (3, -4)),
             sword=sword(5, 37, 105)),
    ]


def poses_fall():
    out = []
    for i in range(3):
        out.append(dict(
            # Caída: piernas colgando abiertas, capa levantada por el aire
            bob=-2, cape_back=2, cape_lift=[10, 12, 11][i], front_lift=3,
            wave=[1, -1, 1][i],
            legs=legs((-4, -5), (-5, -1), (4, -5), (4, -1)),
            sword=sword(6, 37, [-150, -155, -152][i]),
        ))
    return out


def poses_attack():
    return [
        # 0 Preparación: espada atrás sobre el hombro
        dict(bob=1, lean=-1, cape_back=1,
             legs=legs((-3, -5), (-4, 0), (4, -6), (5, 0)),
             sword=sword(1, 33, -150)),
        # 1 Golpe: tajo horizontal con estela completa
        dict(bob=1, lean=2, head_dx=1, cape_back=4, wave=1,
             legs=legs((-3, -5), (-5, 0), (6, -6), (7, 0)),
             sword=sword(9, 36, 0, 16),
             fx=[("crescent", CX + 3, 37, 14, 7, -80, 45)]),
        # 2 Estela: la hoja baja, la estela se adelgaza
        dict(bob=1, lean=2, cape_back=3,
             legs=legs((-3, -5), (-5, 0), (6, -6), (7, 0)),
             sword=sword(8, 40, 35, 16),
             fx=[("crescent", CX + 3, 37, 15, 4, -15, 60)]),
        # 3 Recuperación 1
        dict(bob=1, lean=1, cape_back=2, wave=-1,
             legs=legs((-3, -5), (-4, 0), (5, -6), (6, 0)),
             sword=sword(6, 41, 60),
             fx=[("motes", [(CX + 20, 48, "WHITE"), (CX + 17, 52, "GOLD")])]),
        # 4 Recuperación 2 (vuelve a idle)
        dict(bob=0, lean=0, cape_back=1,
             legs=LEGS_STAND, sword=sword(6, 39, 72)),
    ]


def poses_attack_up():
    return [
        # 0 Preparación: espada abajo y atrás, cuerpo agachado
        dict(bob=2, lean=1,
             legs=legs((-2, -5), (-4, 0), (4, -5), (4, 0)),
             sword=sword(5, 42, 140)),
        # 1 Golpe hacia arriba con estela sobre la cabeza
        dict(bob=-1, lean=1, cape_back=1, cape_lift=-1,
             legs=legs((-3, -6), (-3, 0), (3, -6), (3, 0)),
             sword=sword(4, 25, -95, 15),
             fx=[("crescent", CX + 1, 27, 12, 7, -170, -15)]),
        # 2 Estela
        dict(bob=-1, lean=1, cape_back=1,
             legs=legs((-3, -6), (-3, 0), (3, -6), (3, 0)),
             sword=sword(5, 27, -60, 15),
             fx=[("crescent", CX + 1, 27, 13, 4, -110, -25)]),
        # 3 Recuperación
        dict(bob=0, lean=0,
             legs=LEGS_STAND, sword=sword(6, 37, 30)),
    ]


def poses_attack_down():
    tucked = legs((-1, -7), (-4, -4), (5, -8), (3, -5))
    return [
        # 0 Preparación en el aire: espada arriba
        dict(bob=-4, cape_lift=4, cape_back=1, legs=tucked,
             sword=sword(4, 32, -80, 15)),
        # 1 Golpe hacia abajo (pogo) con estela bajo los pies
        dict(bob=-4, cape_lift=6, cape_back=2, wave=1, legs=tucked,
             sword=sword(5, 42, 90, 13),
             fx=[("crescent", CX + 2, 44, 9, 6, 15, 165)]),
        # 2 Estela
        dict(bob=-4, cape_lift=5, cape_back=2, legs=tucked,
             sword=sword(6, 42, 70, 13),
             fx=[("crescent", CX + 2, 44, 10, 3, 45, 150)]),
        # 3 Recuperación
        dict(bob=-3, cape_lift=3, cape_back=1, legs=tucked,
             sword=sword(6, 38, 30, 15)),
    ]


def poses_block():
    braced = legs((-3, -4), (-5, 0), (5, -5), (5, 0))
    return [
        # 0 El escudo pasa al frente
        dict(bob=1, lean=0, legs=braced,
             shield=dict(x=8, y=37, rx=3, ry=5),
             sword=sword(1, 41, 150)),
        # 1 Escudo arriba, guardia firme
        dict(bob=2, lean=-1, legs=braced,
             shield=dict(x=10, y=34, rx=4, ry=7),
             sword=sword(0, 41, 150)),
        # 2 Mantener (destello dorado en el escudo)
        dict(bob=2, lean=-1, legs=braced,
             shield=dict(x=10, y=34, rx=4, ry=7, glint=True),
             sword=sword(0, 41, 150)),
    ]


def poses_dash():
    return [
        # 0 Anticipación: se agacha y se inclina
        dict(bob=2, lean=2, cape_back=3, cape_lift=1,
             legs=legs((-3, -4), (-5, 0), (5, -5), (4, 0)),
             sword=sword(0, 41, 170)),
        # 1 Embestida: capa estirada atrás + líneas de velocidad
        dict(bob=3, lean=4, head_dx=1, cape_back=12, cape_lift=4,
             legs=legs((-5, -4), (-9, -2), (4, -5), (2, -2)),
             sword=sword(-1, 41, 175),
             fx=[("streaks", [(3, 14, 31, "CAPE_HI"), (1, 10, 38, "CAPE_HI"),
                              (5, 16, 45, "CAPE_SH")]),
                 ("motes", [(8, 34, "GOLD"), (4, 42, "GOLD_SH")])]),
        # 2 Embestida (ondulación alternada)
        dict(bob=3, lean=4, head_dx=1, cape_back=13, cape_lift=5, wave=1,
             legs=legs((-5, -4), (-9, -2), (4, -5), (2, -2)),
             sword=sword(-1, 41, 175),
             fx=[("streaks", [(1, 12, 33, "CAPE_HI"), (3, 13, 40, "CAPE_HI"),
                              (0, 8, 46, "CAPE_SH")]),
                 ("motes", [(6, 29, "GOLD"), (10, 43, "GOLD")])]),
        # 3 Frenado: la capa se balancea hacia delante
        dict(bob=1, lean=-1, cape_back=5, cape_lift=6, front_lift=1,
             legs=legs((-2, -5), (-3, 0), (5, -5), (6, 0)),
             sword=sword(2, 40, 160),
             fx=[("streaks", [(4, 9, 36, "CAPE_HI")])]),
    ]


def poses_hurt():
    return [
        # 0 Retroceso por el impacto, ojos cerrados, destello frontal
        dict(dx=-2, lean=-3, head_dx=-1, cape_front=3, front_lift=2, cape_back=-1,
             eyes="closed",
             legs=legs((-4, -5), (-5, 0), (3, -6), (2, -2)),
             sword=sword(5, 33, -40, 15),
             fx=[("spark", CX + 13, 30, 4, "WHITE"), ("spark", CX + 9, 41, 2, "WHITE")]),
        # 1 Se recupera
        dict(dx=-1, lean=-2, cape_front=1, eyes="closed",
             legs=legs((-4, -5), (-4, 0), (3, -6), (3, 0)),
             sword=sword(6, 37, 20, 16),
             fx=[("spark", CX + 12, 33, 2, "WHITE")]),
    ]


def poses_death():
    kneel = legs((-3, -1), (-8, 0), (5, -4), (6, 0))
    base = dict(bob=7, lean=1, head_dy=2, eyes="closed", legs=kneel,
                sword=sword(9, 37, 90, 12))
    out = [
        # 0 Tambaleo
        dict(dx=-1, lean=-2, bob=1, eyes="closed",
             legs=legs((-4, -5), (-5, 0), (3, -6), (3, 0)),
             sword=sword(6, 42, 80, 12)),
        # 1 Cae de rodillas, la espada se clava en el suelo
        dict(base),
        # 2 Cabeza inclinada, empiezan a brotar motas doradas
        dict(base, head_dy=3,
             fx=[("motes", [(CX - 6, 30, "GOLD"), (CX + 3, 24, "GLOW"),
                            (CX - 2, 20, "GOLD"), (CX + 8, 28, "GOLD_SH")])]),
    ]
    # 3-5 Se desvanece en partículas doradas
    for amount, seed in ((0.30, 3), (0.62, 4), (0.95, 5)):
        out.append(dict(base, head_dy=3, dissolve=amount, seed=seed))
    return out


POSE_BUILDERS = {
    "idle": poses_idle, "run": poses_run, "jump": poses_jump, "fall": poses_fall,
    "attack": poses_attack, "attack_up": poses_attack_up,
    "attack_down": poses_attack_down, "block": poses_block, "dash": poses_dash,
    "hurt": poses_hurt, "death": poses_death,
}


# ---------------------------------------------------------------------------
# EXPORTACIÓN
# ---------------------------------------------------------------------------
def canvas_to_image(cv):
    img = Image.new("RGBA", (FRAME, FRAME), (0, 0, 0, 0))
    pix = img.load()
    for y in range(FRAME):
        for x in range(FRAME):
            c = cv.px[y][x]
            if c is not None:
                r, g, b = PALETTE[c]
                pix[x, y] = (r, g, b, 255)
    return img


def main():
    rows = len(ANIMATIONS)
    sheet = Image.new("RGBA", (COLS * FRAME, rows * FRAME), (0, 0, 0, 0))
    frames_meta = []
    anims_meta = {}

    for row, (name, count, fps, loop) in enumerate(ANIMATIONS):
        poses = POSE_BUILDERS[name]()
        assert len(poses) == count, f"{name}: {len(poses)} poses, se esperaban {count}"
        anims_meta[name] = {"row": row, "frames": count, "fps": fps, "loop": loop}
        for i, pose in enumerate(poses):
            img = canvas_to_image(draw_player(pose))
            x, y = i * FRAME, row * FRAME
            sheet.paste(img, (x, y))
            frames_meta.append({
                "name": f"player_{name}_{i}",
                "animation": name,
                "index": i,
                "x": x, "y": y, "width": FRAME, "height": FRAME,
            })

    sheet.save("player_sheet.png")

    meta = {
        "image": "player_sheet.png",
        "size": {"width": sheet.width, "height": sheet.height},
        "frame_size": {"width": FRAME, "height": FRAME},
        "columns": COLS,
        "coordinate_origin": "top-left (en Unity el origen del rect es bottom-left: "
                             "y_unity = altura_hoja - y - alto)",
        "pivot_normalized": {"x": 0.5, "y": round(3 / FRAME, 6)},
        "pivot_note": "Pivote en los pies: X=32, borde inferior del contorno en y=60.",
        "palette": {k: "#%02X%02X%02X" % v for k, v in PALETTE.items()},
        "animations": anims_meta,
        "frames": frames_meta,
    }
    with open("player_sheet.json", "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)

    preview = sheet.resize((sheet.width * PREVIEW_SCALE, sheet.height * PREVIEW_SCALE),
                           Image.NEAREST)
    preview.save("player_preview.png")

    print(f"OK: player_sheet.png ({sheet.width}x{sheet.height}), "
          f"{len(frames_meta)} frames, player_sheet.json, player_preview.png")


if __name__ == "__main__":
    main()
