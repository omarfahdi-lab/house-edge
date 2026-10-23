# Bakes House Edge's characters from Kenney's Modular Characters (CC0) into animation layers.
# Each character becomes: legL, legR, armL, armR, torso, head (no mouth), plus a metadata file with
# every layer's box and pivot, and where the mouth sits on the head. Mouths are shared images so the
# game can swap expressions. Accessories (hats, shades, cigars...) are drawn here in the same flat style.
import os, sys
from collections import Counter
from PIL import Image, ImageDraw

# the PNG folder of Kenney's Modular Characters pack (https://kenney.nl/assets/modular-characters, CC0)
P = os.environ.get("KENNEY_PNG", os.path.join(os.path.dirname(__file__), "modular", "PNG")) + "/"
OUT = sys.argv[1]
SCALE = 0.5
W, H = 420, 640
CX, TORSO, HIP = 210, 230, 380
SX, SY, ROT = 48, -6, -28


def load(path): return Image.open(P + path).convert("RGBA")
def mirror(im): return im.transpose(Image.FLIP_LEFT_RIGHT)
def hexc(h, a=255): h = h.lstrip("#"); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def recolor(im, target):
    # tint a grey part to a colour, keeping its shading
    px = [p for p in im.getdata() if p[3] > 200]
    ref = Counter(px).most_common(1)[0][0]
    tr, tg, tb = target[:3]
    out = Image.new("RGBA", im.size)
    data = []
    for r, g, b, a in im.getdata():
        k = (r + g + b) / max(1, sum(ref[:3]))
        data.append((min(255, int(tr * k)), min(255, int(tg * k)), min(255, int(tb * k)), a))
    out.putdata(data)
    return out


def canvas(): return Image.new("RGBA", (W, H), (0, 0, 0, 0))


class Look:
    def __init__(self, skin=2, shirt="#8a9696", style=1, pants="#3a3f4a", shoes=("Black", "blackShoe1"),
                 hair=None, eyes="eyeBlack_large", brows="black", brow=1, nose=1, mouth="glad",
                 hat=None, hat_color="#1a1a1e", band="#a8201e", face=(), neck=None, hand=None, female=False, bald=False):
        self.__dict__.update({k: v for k, v in locals().items() if k != "self"})


def head_layer(look):
    t = f"Skin/Tint {look.skin}/tint{look.skin}_"
    layer = canvas()
    head = load(t + "head.png")
    hx, hy = CX - head.width // 2, 70
    hd = head.copy()
    # face paint goes on before the eyes and brows
    if facepaint in look.face: facepaint(hd, ImageDraw.Draw(hd), 0, 0)
    F = "Face/"
    brow = load(f"{F}Eyebrows/{look.brows}Brow{look.brow}.png")
    hd.alpha_composite(mirror(brow), (33, 62))
    hd.alpha_composite(brow, (100, 62))
    eye = load(f"{F}Eyes/{look.eyes}.png")
    off = (21 - eye.width) // 2
    hd.alpha_composite(eye, (44 + off, 86 + off))
    hd.alpha_composite(eye, (108 + off, 86 + off))
    hd.alpha_composite(load(f"{F}Nose/Tint {look.skin}/tint{look.skin}Nose{look.nose}.png"), (71, 104))
    layer.alpha_composite(hd, (hx, hy))
    mouth = (hx + 87, hy + 134)
    d = ImageDraw.Draw(layer)
    for f in look.face:
        if f is not facepaint: f(layer, d, hx, hy)
    if look.hair and look.hat not in ("tophat", "tall", "fedora", "bowler", "beanie", "visor", "beret", "crown"):
        hair = load(f"Hair/{look.hair[0]}/{look.hair[1]}.png")
        layer.alpha_composite(hair, (CX - hair.width // 2, 55))
    elif look.hair:
        # under a hat only the sides and back of the hair show
        hair = load(f"Hair/{look.hair[0]}/{look.hair[1]}.png")
        layer.alpha_composite(hair, (CX - hair.width // 2, 62))
    if look.hat: HATS[look.hat](layer, ImageDraw.Draw(layer), look)
    return layer, mouth


# hats, drawn around the head (head top is at y = 70, centre x = CX)
def tophat(l, d, look, tall=False):
    top = 0 if tall else 18
    d.rounded_rectangle((CX - 58, top, CX + 58, 98), 10, fill=hexc(look.hat_color))
    d.rectangle((CX - 58, 74, CX + 58, 90), fill=hexc(look.band))
    d.ellipse((CX - 96, 86, CX + 96, 112), fill=hexc(look.hat_color))
def fedora(l, d, look):
    d.ellipse((CX - 104, 78, CX + 104, 110), fill=hexc(look.hat_color))
    d.polygon([(CX - 70, 92), (CX - 58, 26), (CX, 40), (CX + 58, 26), (CX + 70, 92)], fill=hexc(look.hat_color))
    d.rectangle((CX - 66, 70, CX + 66, 86), fill=hexc(look.band))
def bowler(l, d, look):
    d.ellipse((CX - 92, 84, CX + 92, 108), fill=hexc(look.hat_color))
    d.pieslice((CX - 70, 20, CX + 70, 160), 180, 360, fill=hexc(look.hat_color))
    d.rectangle((CX - 70, 78, CX + 70, 90), fill=hexc(look.band))
def crown(l, d, look):
    g, j = hexc("#f2c14e"), hexc("#c0262e")
    d.polygon([(CX - 66, 92), (CX - 74, 30), (CX - 38, 62), (CX, 18), (CX + 38, 62), (CX + 74, 30), (CX + 66, 92)], fill=g)
    for x in (-74, 0, 74): d.ellipse((CX + x - 9, (18 if x == 0 else 30) - 9, CX + x + 9, (18 if x == 0 else 30) + 9), fill=g)
    d.ellipse((CX - 11, 66, CX + 11, 88), fill=j)
def visor(l, d, look):
    d.pieslice((CX - 82, 34, CX + 82, 170), 180, 360, fill=hexc(look.hat_color))
    d.ellipse((CX - 30, 90, CX + 120, 118), fill=hexc(look.band))
    d.ellipse((CX - 14, 52, CX + 14, 80), fill=hexc("#f2c14e"))
def beret(l, d, look):
    d.ellipse((CX - 96, 36, CX + 80, 104), fill=hexc(look.hat_color))
    d.ellipse((CX - 8, 26, CX + 8, 42), fill=hexc(look.hat_color))
def beanie(l, d, look):
    d.pieslice((CX - 84, 30, CX + 84, 180), 180, 360, fill=hexc(look.hat_color))
    d.rounded_rectangle((CX - 88, 88, CX + 88, 112), 10, fill=hexc(look.band))
    d.ellipse((CX - 18, 12, CX + 18, 48), fill=hexc(look.band))
HATS = {"tophat": tophat, "tall": lambda l, d, k: tophat(l, d, k, tall=True), "fedora": fedora, "bowler": bowler,
        "crown": crown, "visor": visor, "beret": beret, "beanie": beanie}


# face extras, in head coordinates (hx, hy = head image top-left; eyes at y 86-107, x 44-65 and 108-129)
def shades(l, d, hx, hy):
    for x in (36, 102):
        d.rounded_rectangle((hx + x, hy + 80, hx + x + 36, hy + 108), 8, fill=hexc("#101014"))
        d.line((hx + x + 6, hy + 86, hx + x + 16, hy + 86), fill=hexc("#5a6a80"), width=3)
    d.rectangle((hx + 72, hy + 86, hx + 102, hy + 91), fill=hexc("#101014"))
def glasses(l, d, hx, hy):
    for x in (36, 100):
        d.ellipse((hx + x, hy + 78, hx + x + 38, hy + 114), outline=hexc("#2a2a2e"), width=5)
    d.line((hx + 74, hy + 94, hx + 100, hy + 94), fill=hexc("#2a2a2e"), width=4)
def monocle(l, d, hx, hy):
    d.ellipse((hx + 100, hy + 76, hx + 138, hy + 116), outline=hexc("#d9a93a"), width=5)
    d.line((hx + 136, hy + 108, hx + 150, hy + 170), fill=hexc("#d9a93a"), width=3)
def mustache(color):
    def f(l, d, hx, hy):
        d.chord((hx + 50, hy + 112, hx + 88, hy + 138), 180, 360, fill=hexc(color))
        d.chord((hx + 86, hy + 112, hx + 124, hy + 138), 180, 360, fill=hexc(color))
    return f
def beard(color):
    def f(l, d, hx, hy):
        d.chord((hx + 24, hy + 70, hx + 150, hy + 182), 20, 160, fill=hexc(color))
        d.chord((hx + 52, hy + 116, hx + 122, hy + 140), 180, 360, fill=hexc(color))
    return f
def cigar(l, d, hx, hy):
    d.rounded_rectangle((hx + 104, hy + 132, hx + 160, hy + 142), 4, fill=hexc("#6b3a1e"))
    d.rectangle((hx + 152, hy + 132, hx + 160, hy + 142), fill=hexc("#ff7a2a"))
def scar(l, d, hx, hy):
    d.line((hx + 112, hy + 70, hx + 134, hy + 120), fill=hexc("#a0525a"), width=5)
    for i in range(3): d.line((hx + 114 + i * 8, hy + 82 + i * 12, hx + 128 + i * 8, hy + 78 + i * 12), fill=hexc("#a0525a"), width=3)
def mask(l, d, hx, hy):
    d.chord((hx + 30, hy + 60, hx + 144, hy + 178), 10, 170, fill=hexc("#22252e"))
def facepaint(l, d, hx, hy):
    d.ellipse((hx + 22, hy + 40, hx + 152, hy + 166), fill=hexc("#f7f7f2"))
    for x in (54, 118): d.line((hx + x, hy + 112, hx + x, hy + 122), fill=hexc("#111111"), width=4)
def blush(l, d, hx, hy):
    for x in (36, 116): d.ellipse((hx + x, hy + 112, hx + x + 22, hy + 124), fill=hexc("#f08a8a", 140))
def gold_eyes(l, d, hx, hy):
    for x in (44, 108): d.ellipse((hx + x - 2, hy + 84, hx + x + 23, hy + 109), fill=hexc("#f2c14e"))
def red_hair_bun(l, d, hx, hy):
    d.ellipse((hx + 60, hy - 30, hx + 114, hy + 22), fill=hexc("#c2412b"))


def torso_layer(look):
    layer = canvas()
    shirt = recolor(load(f"Shirts/Grey/greyShirt{look.style}.png"), hexc(look.shirt))
    waist = recolor(load("Pants/Grey/pantsGrey1.png"), hexc(look.pants))
    neck = load(f"Skin/Tint {look.skin}/tint{look.skin}_neck.png")
    layer.alpha_composite(shirt, (CX - shirt.width // 2, TORSO))
    layer.alpha_composite(waist, (CX - waist.width // 2, HIP - 22))
    layer.alpha_composite(neck, (CX - neck.width // 2, TORSO - 12))
    d = ImageDraw.Draw(layer)
    if look.neck == "bowtie" or (isinstance(look.neck, tuple) and look.neck[0] == "bowtie"):
        c = hexc(look.neck[1] if isinstance(look.neck, tuple) else "#a8201e")
        d.polygon([(CX, TORSO + 14), (CX - 26, TORSO + 2), (CX - 26, TORSO + 28)], fill=c)
        d.polygon([(CX, TORSO + 14), (CX + 26, TORSO + 2), (CX + 26, TORSO + 28)], fill=c)
        d.ellipse((CX - 7, TORSO + 7, CX + 7, TORSO + 21), fill=c)
    elif isinstance(look.neck, tuple) and look.neck[0] == "tie":
        c = hexc(look.neck[1])
        d.polygon([(CX - 9, TORSO + 6), (CX + 9, TORSO + 6), (CX + 13, TORSO + 92), (CX, TORSO + 108), (CX - 13, TORSO + 92)], fill=c)
    elif look.neck == "stripes":
        mask_ = shirt.split()[3]
        stripes = Image.new("RGBA", shirt.size, (0, 0, 0, 0))
        sd = ImageDraw.Draw(stripes)
        for y in range(30, shirt.height, 30): sd.rectangle((0, y, shirt.width, y + 12), fill=hexc("#16161a"))
        stripes.putalpha(Image.composite(stripes.split()[3], Image.new("L", shirt.size, 0), mask_))
        layer.alpha_composite(stripes, (CX - shirt.width // 2, TORSO))
    elif look.neck == "eye":
        d.ellipse((CX - 30, TORSO + 60, CX + 30, TORSO + 96), fill=hexc("#f2c14e"))
        d.ellipse((CX - 13, TORSO + 65, CX + 13, TORSO + 91), fill=hexc("#14181f"))
    elif look.neck == "badge":
        d.regular_polygon((CX + 40, TORSO + 60, 14), 6, fill=hexc("#f2c14e"))
    elif look.neck == "vest":
        d.polygon([(CX - 60, TORSO + 10), (CX - 14, TORSO + 10), (CX - 4, TORSO + 140), (CX - 62, TORSO + 140)], fill=hexc("#1a1a1e"))
        d.polygon([(CX + 60, TORSO + 10), (CX + 14, TORSO + 10), (CX + 4, TORSO + 140), (CX + 62, TORSO + 140)], fill=hexc("#1a1a1e"))
        d.polygon([(CX, TORSO + 14), (CX - 22, TORSO + 4), (CX - 22, TORSO + 24)], fill=hexc("#a8201e"))
        d.polygon([(CX, TORSO + 14), (CX + 22, TORSO + 4), (CX + 22, TORSO + 24)], fill=hexc("#a8201e"))
    return layer


def arm_image(look):
    a = Image.new("RGBA", (260, 260), (0, 0, 0, 0))
    a.alpha_composite(load(f"Skin/Tint {look.skin}/tint{look.skin}_arm.png"), (20, 20))
    a.alpha_composite(recolor(load("Shirts/Grey/greyArm_long.png"), hexc(look.shirt)), (19, 18))
    a.alpha_composite(load(f"Skin/Tint {look.skin}/tint{look.skin}_hand.png"), (148, 118))
    if look.hand == "cards":
        d = ImageDraw.Draw(a)
        for i, ang in enumerate((-20, 0, 20)):
            card = Image.new("RGBA", (40, 56), (0, 0, 0, 0))
            cd = ImageDraw.Draw(card)
            cd.rounded_rectangle((0, 0, 39, 55), 5, fill=(250, 248, 240, 255), outline=(60, 60, 60, 255), width=2)
            cd.ellipse((13, 20, 27, 34), fill=(200, 30, 40, 255) if i % 2 == 0 else (20, 20, 25, 255))
            card = card.rotate(-ang, expand=True, resample=Image.BICUBIC)
            a.alpha_composite(card, (150 + i * 10, 100 - abs(ang) // 4))
        a.alpha_composite(load(f"Skin/Tint {look.skin}/tint{look.skin}_hand.png"), (148, 128))
    return a.rotate(ROT, center=(40, 40), resample=Image.BICUBIC)


def bake(name, look, out):
    layers = {}
    for side, key in ((1, "legR"), (-1, "legL")):
        layer = canvas()
        leg = load(f"Skin/Tint {look.skin}/tint{look.skin}_leg.png")
        pl = recolor(load("Pants/Grey/pantsGrey_long.png"), hexc(look.pants))
        shoe = load(f"Shoes/{look.shoes[0]}/{look.shoes[1]}.png")
        if side < 0: leg, pl, shoe = mirror(leg), mirror(pl), mirror(shoe)
        x = CX + 8 if side > 0 else CX - 8 - leg.width
        layer.alpha_composite(leg, (x, HIP))
        layer.alpha_composite(pl, (x, HIP))
        layer.alpha_composite(shoe, (x + (30 if side > 0 else 0), HIP + 140))
        layers[key] = (layer, (CX + 38 * side, HIP + 10))
    for side, key in ((1, "armR"), (-1, "armL")):
        a = arm_image(look) if side > 0 or look.hand != "cards" else arm_image(Look(**{**vars(look), "hand": None}))
        if side < 0: a = mirror(a)
        layer = canvas()
        x = CX + SX - 40 if side > 0 else CX - SX - (a.width - 40)
        layer.alpha_composite(a, (x, TORSO + SY))
        layers[key] = (layer, (CX + SX * side, TORSO + SY + 40))
    layers["torso"] = (torso_layer(look), (CX, HIP))
    head, mouth = head_layer(look)
    layers["head"] = (head, (CX, TORSO + 8))

    meta = [f"name {name}"]
    for key in ("legL", "legR", "armL", "armR", "torso", "head"):
        img, (px, py) = layers[key]
        box = img.getbbox()
        crop = img.crop(box)
        crop = crop.resize((max(1, round(crop.width * SCALE)), max(1, round(crop.height * SCALE))), Image.LANCZOS)
        crop.save(os.path.join(out, f"{ident(name)}_{key}.png"), optimize=True)
        meta.append(f"{key} {box[0]*SCALE:.1f} {box[1]*SCALE:.1f} {crop.width} {crop.height} {px*SCALE:.1f} {py*SCALE:.1f}")
    meta.append(f"mouth {mouth[0]*SCALE:.1f} {mouth[1]*SCALE:.1f} {look.mouth}")
    meta.append(f"size {W*SCALE:.0f} {H*SCALE:.0f}")
    open(os.path.join(out, f"{ident(name)}.txt"), "w").write("\n".join(meta) + "\n")


def ident(name): return "".join(c for c in name.lower() if c.isalnum())


def mouths(out):
    for m in ("glad", "happy", "oh", "sad", "straight", "teethLower", "teethUpper"):
        im = load(f"Face/Mouth/mouth_{m}.png")
        im = im.resize((round(im.width * SCALE), round(im.height * SCALE)), Image.LANCZOS)
        im.save(os.path.join(out, f"mouth_{m}.png"), optimize=True)


B, BR1, BR2, G, WH, BL, RD, TN = "Black", "Brown 1", "Brown 2", "Grey", "White", "Blonde", "Red", "Tan"
CHARACTERS = {
    # the Pit Bosses and the other bosses
    "The Twins": Look(skin=2, shirt="#5b2a86", style=5, pants="#1a1a22", hair=(B, "blackMan6"), hat="bowler", hat_color="#16161a", band="#5b2a86", mouth="glad", eyes="eyeBlack_small", brow=2),
    "The Iceman": Look(skin=1, shirt="#cfe6f2", style=6, pants="#dde8ee", shoes=("Grey", "greyShoe1"), hair=(WH, "whiteMan1"), brows="white", face=(shades,), mouth="straight"),
    "The Cheat": Look(skin=3, shirt="#2f7a4b", style=7, pants="#3a3f4a", hair=(BR1, "brown1Man3"), hat="visor", hat_color="#2f7a4b", band="#39b36a", eyes="eyeBlack_small", brows="brown1", brow=3, mouth="glad", hand="cards"),
    "The Mime": Look(skin=1, shirt="#f2f2ee", style=1, pants="#16161a", hat="beret", hat_color="#16161a", face=(facepaint,), eyes="eyeBlack_large", mouth="sad", neck="stripes"),
    "The Taxman": Look(skin=1, shirt="#5b5e66", style=5, pants="#4a4d55", hair=(BR2, "brown2Man1"), hat="bowler", hat_color="#1c1c1c", band="#1c1c1c", brows="brown2", face=(glasses, mustache("#4a2e1e")), mouth="straight", neck=("tie", "#2a4a3a")),
    "The Purist": Look(skin=1, shirt="#9c6fa0", style=8, pants="#4a2e4a", hair=(G, "greyWoman5"), brows="grey", face=(monocle,), mouth="straight", female=True),
    "The Ceiling": Look(skin=2, shirt="#1f2e4a", style=5, pants="#151d30", hair=(BR1, "brown1Man1"), hat="tall", hat_color="#15151a", band="#2f4c6e", brows="brown1", mouth="happy", neck="bowtie"),
    "The Sweeper": Look(skin=4, shirt="#4a6b8a", style=3, pants="#2f4a66", shoes=("Brown 2", "brown2Shoe1"), hat="beanie", hat_color="#c2412b", band="#9a2e1e", brows="brown2", face=(mustache("#3a2414"),), mouth="glad"),
    "The Silencer": Look(skin=3, shirt="#1d2028", style=6, pants="#14161c", hat="fedora", hat_color="#22252e", band="#14161c", eyes="eyeBlack_small", brow=3, face=(mask,), mouth=None),
    "The Card Mechanic": Look(skin=2, shirt="#8a1c24", style=5, pants="#2a1012", hair=(B, "blackMan1"), mouth="glad", neck=("bowtie", "#16161a"), hand="cards"),
    "The Hoarder": Look(skin=5, shirt="#6b4f2a", style=3, pants="#4a5a2a", shoes=("Brown 1", "brownShoe1"), hair=(G, "greyMan5"), brows="grey", face=(beard("#9a9a92"),), mouth="oh"),
    "The Pit Boss": Look(skin=4, shirt="#a8201e", style=5, pants="#1a1012", hair=(B, "blackMan1"), face=(shades, cigar), mouth="straight", neck=("tie", "#16161a")),
    "The Eye in the Sky": Look(skin=6, shirt="#1c2c4a", style=7, pants="#141c2e", hat="visor", hat_color="#1c2c4a", band="#14161c", face=(shades,), mouth="straight", neck="eye"),
    "The Enforcer": Look(skin=7, shirt="#16161a", style=1, pants="#16161a", face=(shades, scar), mouth="straight", bald=True),
    "The House": Look(skin=3, shirt="#0e0e12", style=5, pants="#0a0a0c", hair=(WH, "whiteMan1"), hat="crown", brows="white", face=(gold_eyes,), mouth="glad", neck=("bowtie", "#f2c14e")),
    # room hosts
    "Sal": Look(skin=2, shirt="#f2ebdd", style=5, pants="#1a1a1e", hair=(B, "blackMan1"), face=(mustache("#2a1a12"),), mouth="happy", neck="vest"),
    "Vinnie": Look(skin=4, shirt="#1a1a1e", style=6, pants="#1a1a1e", hat="fedora", hat_color="#2a2a30", band="#a8201e", eyes="eyeBlack_small", face=(cigar,), mouth="straight"),
    "Morty": Look(skin=1, shirt="#6b5a3a", style=3, pants="#4a3a24", shoes=("Brown 2", "brown2Shoe1"), hair=(G, "greyMan5"), brows="grey", face=(monocle,), mouth="glad"),
    "Finn": Look(skin=2, shirt="#5c6670", style=5, pants="#3a4048", hair=(B, "blackMan6"), face=(shades,), mouth="teethUpper", neck=("tie", "#2a8a7a")),
    "Dot": Look(skin=1, shirt="#2f6b4a", style=8, pants="#1e3a2a", hair=(BR2, "brown2Woman5"), brows="brown2", face=(glasses,), mouth="happy", female=True),
    "Gus": Look(skin=5, shirt="#6a5a44", style=3, pants="#3a3a44", hat="beanie", hat_color="#2f5a8a", band="#1e3a5a", brows="brown2", mouth="straight"),
    "Officer Reyes": Look(skin=4, shirt="#24365a", style=7, pants="#1a2638", hat="visor", hat_color="#1c2c4a", band="#14161c", face=(shades,), mouth="straight", neck="badge"),
    "Madame Rose": Look(skin=2, shirt="#8a1a40", style=4, pants="#4a0e24", hair=(B, "blackWoman3"), face=(blush,), mouth="happy", female=True),
    "Cherry": Look(skin=1, shirt="#c2282e", style=8, pants="#6a1a1a", hair=(RD, "redWoman5"), brows="red", face=(blush,), mouth="teethUpper", female=True),
    "Mr. Big": Look(skin=1, shirt="#1a1a1e", style=5, pants="#1a1a1e", hair=(WH, "whiteMan1"), hat="tophat", hat_color="#121214", band="#f2c14e", brows="white", face=(mustache("#e8e8e8"),), mouth="teethUpper", neck=("bowtie", "#f2c14e")),
    "Slick Eddie": Look(skin=3, shirt="#7a5a20", style=5, pants="#4a3a12", hat="fedora", hat_color="#5a3a12", band="#c2412b", eyes="eyeBlack_small", brow=3, face=(mustache("#2a1a0e"),), mouth="glad"),
    "Jules": Look(skin=2, shirt="#f2ebdd", style=5, pants="#1a1a1e", hair=(BR1, "brown1Man2"), brows="brown1", mouth="oh", neck="bowtie"),
    "The Coat": Look(skin=4, shirt="#2a2620", style=6, pants="#1a1814", hat="fedora", hat_color="#101010", band="#101010", face=(shades, mask), mouth=None),
    "Hank": Look(skin=4, shirt="#4a6a8a", style=2, pants="#2f4a66", shoes=("Brown 1", "brownShoe1"), hat="beanie", hat_color="#8a2a1e", band="#5a1a12", face=(mustache("#3a2414"),), mouth="glad"),
    "Flash": Look(skin=1, shirt="#3a2a5a", style=4, pants="#1e1430", hair=(BL, "blondeWoman2"), brows="blonde", hat="beret", hat_color="#c2282e", face=(glasses,), mouth="happy", female=True),
    "Security": Look(skin=6, shirt="#14161c", style=7, pants="#14161c", hat="visor", hat_color="#1c2c4a", band="#14161c", face=(shades,), mouth="straight", neck="badge"),
    "The Usher": Look(skin=2, shirt="#8a1a20", style=5, pants="#2a0e10", hair=(BR1, "brown1Man1"), hat="bowler", hat_color="#8a1a20", band="#f2c14e", brows="brown1", mouth="happy", neck=("bowtie", "#f2c14e")),
    "Zappo": Look(skin=1, shirt="#2a1a5a", style=5, pants="#140a24", hair=(B, "blackMan1"), hat="tophat", hat_color="#140a24", band="#c2282e", face=(mustache("#16161a"),), mouth="teethUpper", hand="cards", neck=("bowtie", "#c2282e")),
    # the table dealers: white shirt, black vest, a bowtie of their own
    "Rookie Ray": Look(skin=2, shirt="#f2ebdd", style=5, pants="#1a1a1e", hair=(RD, "redMan2"), brows="red", eyes="eyeBlue_large", mouth="oh", neck="vest"),
    "Silent Sofia": Look(skin=3, shirt="#1a1a1e", style=8, pants="#1a1a1e", hair=(B, "blackWoman1"), mouth="straight", female=True, neck=("bowtie", "#c2282e")),
    "Chatty Chen": Look(skin=2, shirt="#2f6b4a", style=5, pants="#1a1a1e", hair=(B, "blackMan4"), face=(glasses,), mouth="teethUpper", neck=("bowtie", "#f2c14e")),
    "Old Pete": Look(skin=1, shirt="#f2ebdd", style=5, pants="#2a2a30", hair=(WH, "whiteMan3"), brows="white", face=(mustache("#e8e8e8"),), mouth="glad", neck="vest"),
    "Vegas Val": Look(skin=1, shirt="#d9a93a", style=4, pants="#3a2a10", hair=(BL, "blondeWoman3"), brows="blonde", face=(blush,), mouth="teethUpper", female=True),
    "Lucky Lou": Look(skin=4, shirt="#2f7a4b", style=5, pants="#1a1a1e", hair=(TN, "tanMan5"), brows="tan", mouth="happy", neck=("bowtie", "#39b36a")),
    # you
    "You": Look(skin=2, shirt="#1f6b5c", style=5, pants="#2a2a30", shoes=("Brown 1", "brownShoe1"), hair=(BR1, "brown1Man5"), hat="fedora", hat_color="#3a3a42", band="#c2282e", brows="brown1", eyes="eyeBrown_large", mouth="happy", neck=("bowtie", "#c2282e")),
}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    mouths(OUT)
    only = sys.argv[2:] or list(CHARACTERS)
    open(os.path.join(os.path.dirname(__file__), "ids.txt"), "w").write(" ".join(ident(n) for n in only))
    for name in only: bake(name, CHARACTERS[name], OUT)
    print(f"baked {len(only)} characters into {OUT}")
