# crops a painting to a 4:5 character portrait. box is fractions: left top right bottom
import sys
from PIL import Image, ImageEnhance, ImageFilter
src, out = sys.argv[1], sys.argv[2]
l, t, r, b = (float(v) for v in sys.argv[3:7])
W, H = 480, 600
im = Image.open(src).convert("RGB")
box = [l * im.width, t * im.height, r * im.width, b * im.height]
# grow the box to exactly 4:5 around its centre
cx, cy = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
w, h = box[2] - box[0], box[3] - box[1]
if w / h > W / H: h = w * H / W
else: w = h * W / H
# keep the box inside the painting: shrink if it's too big, then slide it in
k = min(1, im.width / w, im.height / h)
w, h = w * k, h * k
cx = min(max(cx, w / 2), im.width - w / 2)
cy = min(max(cy, h / 2), im.height - h / 2)
box = [cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2]
im = im.crop([round(v) for v in box]).resize((W, H), Image.LANCZOS)
im = im.filter(ImageFilter.UnsharpMask(radius=1.2, percent=60, threshold=2))
im = ImageEnhance.Contrast(im).enhance(1.06)
im.save(out, quality=86, optimize=True)
