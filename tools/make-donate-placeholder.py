#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""生成赞赏码占位图 assets/donate.png（把真实收款码替换成这个文件即可）。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent.parent / "assets"
OUT.mkdir(parents=True, exist_ok=True)

W = H = 640
img = Image.new("RGBA", (W, H), (255, 255, 255, 255))
d = ImageDraw.Draw(img)

# 虚线圆角边框
dash = 18
gap = 12
pad = 28
def dashed_line(x1, y1, x2, y2):
    import math
    length = math.hypot(x2 - x1, y2 - y1)
    ux, uy = (x2 - x1) / length, (y2 - y1) / length
    t = 0.0
    while t < length:
        e = min(t + dash, length)
        d.line([(x1 + ux * t, y1 + uy * t), (x1 + ux * e, y1 + uy * e)],
               fill=(210, 214, 222, 255), width=4)
        t += dash + gap

dashed_line(pad, pad, W - pad, pad)
dashed_line(W - pad, pad, W - pad, H - pad)
dashed_line(W - pad, H - pad, pad, H - pad)
dashed_line(pad, H - pad, pad, pad)

font_path = r"C:\Windows\Fonts\msyh.ttc"
def font(size, bold=False):
    try:
        idx = 1 if bold else 0
        return ImageFont.truetype(font_path, size, index=idx)
    except Exception:
        return ImageFont.load_default()

lines = [
    ("把赞赏码放这里", font(44, True), (40, 44, 52)),
    ("assets/donate.png", font(26), (120, 126, 140)),
    ("", font(12), (0, 0, 0)),
    ("微信 / 支付宝 收款码截图", font(24), (120, 126, 140)),
    ("保存覆盖本文件即可", font(24), (120, 126, 140)),
]
total = 0
sizes = []
for text, f, color in lines:
    if not text:
        sizes.append((text, f, color, 20))
        total += 20
        continue
    bbox = d.textbbox((0, 0), text, font=f)
    h = bbox[3] - bbox[1]
    sizes.append((text, f, color, h + 16))
    total += h + 16

y = (H - total) / 2
for text, f, color, h in sizes:
    if text:
        bbox = d.textbbox((0, 0), text, font=f)
        w = bbox[2] - bbox[0]
        d.text(((W - w) / 2, y), text, font=f, fill=color)
    y += h

img.save(OUT / "donate.png")
print("OK", OUT / "donate.png")
