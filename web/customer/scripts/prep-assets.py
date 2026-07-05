"""
Batch-prep raw landmark/hero images into web-ready assets for the landing page.
Run from web/customer:  python scripts/prep-assets.py

- Strips EXIF, honours orientation.
- City cards: center-cropped 3:4 portrait @ 800x1067 (for the scattered-cities section).
- City wide: max 1600w landscape (flexible use / backgrounds).
- Hero: max 1920w + a 1280w variant for responsive srcset.
Sources live on the Desktop; edit DESK/SOURCES if the files move.
"""

import os
from PIL import Image, ImageOps

DESK = r"C:/Users/loaek/OneDrive/Desktop"
OUT = "public/assets"

# (source filename on Desktop, output base name)
CITIES = [
    ("Old_city_Damascus_distant_minaret_202607031606.jpeg", "damascus"),
    ("Citadel_of_Aleppo_golden_hour_202607031606.jpeg", "aleppo"),
    ("Ancient_norias_turning_on_river_202607031607.jpeg", "hama"),
]
# Primary hero: the Damascus sword monument with the city at golden hour.
HERO_SRC = "Sword_of_Damascus_monument_at_202607031619.jpeg"
BUS_SRC = "Bus_driving_on_highway_2K_202607031608.jpeg"


def load(fn):
    img = ImageOps.exif_transpose(Image.open(os.path.join(DESK, fn)))
    return img.convert("RGB")


def save_jpg(img, path, quality=82, max_w=None):
    if max_w and img.width > max_w:
        h = round(img.height * max_w / img.width)
        img = img.resize((max_w, h), Image.LANCZOS)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "JPEG", quality=quality, optimize=True)  # no exif arg => stripped
    return os.path.getsize(path)


def portrait_crop(img, w=800, h=1067):
    target = w / h
    if img.width / img.height > target:      # too wide -> crop sides
        nw = round(img.height * target)
        left = (img.width - nw) // 2
        img = img.crop((left, 0, left + nw, img.height))
    else:                                    # too tall -> crop top/bottom
        nh = round(img.width / target)
        top = (img.height - nh) // 2
        img = img.crop((0, top, img.width, top + nh))
    return img.resize((w, h), Image.LANCZOS)


def kb(n):
    return f"{n / 1024:.0f} KB"


for fn, name in CITIES:
    img = load(fn)
    w = save_jpg(img.copy(), f"{OUT}/cities/{name}-wide.jpg", 82, max_w=1600)
    p = save_jpg(portrait_crop(img.copy()), f"{OUT}/cities/{name}.jpg", 82)
    print(f"{name:10} card={kb(p)}  wide={kb(w)}")

hero = load(HERO_SRC)
h1 = save_jpg(hero.copy(), f"{OUT}/hero/hero.jpg", 84, max_w=1920)
h2 = save_jpg(hero.copy(), f"{OUT}/hero/hero-1280.jpg", 82, max_w=1280)
h3 = save_jpg(hero.copy(), f"{OUT}/hero/hero-768.jpg", 80, max_w=768)
print(f"hero(sword) 1920={kb(h1)}  1280={kb(h2)}  768={kb(h3)}")

bus = load(BUS_SRC)
b1 = save_jpg(bus.copy(), f"{OUT}/hero/hero-bus.jpg", 84, max_w=1920)
print(f"bus        1920={kb(b1)}")
print("done.")
