# reassembles baked characters from their layers and metadata, the way the game does
import os, sys
from PIL import Image
D = sys.argv[1]; names = sys.argv[3:]
tiles = []
for n in names:
    meta = {}
    for line in open(os.path.join(D, n + ".txt")):
        parts = line.split()
        meta[parts[0]] = parts[1:]
    w, h = map(int, meta["size"])
    im = Image.new("RGBA", (w, h), (40, 70, 55, 255))
    for key in ("legL", "legR", "armL", "armR", "torso", "head"):
        x, y = float(meta[key][0]), float(meta[key][1])
        im.alpha_composite(Image.open(os.path.join(D, f"{n}_{key}.png")), (round(x), round(y)))
    mouth = meta["mouth"]
    if mouth[2] != "None":
        m = Image.open(os.path.join(D, f"mouth_{mouth[2]}.png"))
        im.alpha_composite(m, (round(float(mouth[0]) - m.width / 2), round(float(mouth[1]) - m.height / 2)))
    tiles.append(im)
cols = 8
sheet = Image.new("RGBA", (tiles[0].width * min(cols, len(tiles)), tiles[0].height * ((len(tiles) + cols - 1) // cols)), (30, 30, 30, 255))
for i, t in enumerate(tiles): sheet.alpha_composite(t, ((i % cols) * t.width, (i // cols) * t.height))
sheet.save(sys.argv[2])
