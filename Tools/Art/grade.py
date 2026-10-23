# turns a photo into a room backdrop: crop to the play area's shape, soft blur, darken, grade, vignette
import sys
from PIL import Image, ImageFilter, ImageEnhance, ImageDraw, ImageChops
src, out, tint = sys.argv[1], sys.argv[2], sys.argv[3]
blur = float(sys.argv[4]) if len(sys.argv) > 4 else 4
W, H = 1400, 1000
im = Image.open(src).convert("RGB")
scale = max(W / im.width, H / im.height)
im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
left, top = (im.width - W) // 2, (im.height - H) // 2
im = im.crop((left, top, left + W, top + H))
im = im.filter(ImageFilter.GaussianBlur(blur))
im = ImageEnhance.Color(im).enhance(0.75)
im = ImageEnhance.Brightness(im).enhance(0.5)
t = tuple(int(tint[i:i + 2], 16) for i in (0, 2, 4))
grade = Image.new("RGB", (W, H), t)
im = Image.blend(im, ImageChops.multiply(im, grade), 0.55)
# vignette: darker toward the edges and the bottom
mask = Image.new("L", (W, H), 0)
d = ImageDraw.Draw(mask)
for i in range(60):
    k = i / 60
    d.ellipse((-W * 0.25 + k * W * 0.3, -H * 0.3 + k * H * 0.3, W * 1.25 - k * W * 0.3, H * 1.15 - k * H * 0.3), fill=int(255 * k))
mask = mask.filter(ImageFilter.GaussianBlur(40))
im = Image.composite(im, Image.new("RGB", (W, H), (8, 6, 10)), mask)
im.save(out, quality=84, optimize=True)
