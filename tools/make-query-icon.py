#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""生成「立即查电量」快捷方式图标：assets/query.ico
蓝色圆角方块 + 白色电池轮廓（里面一道闪电），表示"看一眼电量"。
"""
from pathlib import Path
from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent.parent / "assets"
OUT.mkdir(parents=True, exist_ok=True)
S, F = 1024, 4
N = S * F


def plate(top, bottom):
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pad = int(N * 0.06)
    radius = int(N * 0.22)
    for y in range(pad, N - pad):
        t = (y - pad) / max(1, (N - pad * 2))
        col = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
        d.line([(pad, y), (N - pad, y)], fill=col)
    mask = Image.new("L", (N, N), 0)
    ImageDraw.Draw(mask).rounded_rectangle([pad, pad, N - pad, N - pad], radius=radius, fill=255)
    img.putalpha(mask)
    gloss = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    ImageDraw.Draw(gloss).rounded_rectangle(
        [pad, pad, N - pad, int(N * 0.52)], radius=radius, fill=(255, 255, 255, 34)
    )
    img.alpha_composite(gloss)
    return img


img = plate((92, 176, 255), (18, 92, 214))
d = ImageDraw.Draw(img)

# 电池外壳
bw, bh = N * 0.30, N * 0.46
cx, cy = N * 0.50, N * 0.52
left, right = cx - bw / 2, cx + bw / 2
top, bottom = cy - bh / 2, cy + bh / 2
r = N * 0.035
d.rounded_rectangle([left, top, right, bottom], radius=r, outline=(255, 255, 255, 255), width=int(N * 0.028))
# 电池正极帽
cap_w, cap_h = N * 0.07, N * 0.075
d.rounded_rectangle(
    [cx - cap_w / 2, top - cap_h, cx + cap_w / 2, top + N * 0.005],
    radius=int(N * 0.012),
    fill=(255, 255, 255, 255),
)
# 电池内部闪电
bolt_h = bh * 0.62
bolt_w = bw * 0.62
pts = [
    (cx + bolt_w * 0.16, cy - bolt_h * 0.50),
    (cx - bolt_w * 0.40, cy + bolt_h * 0.06),
    (cx - bolt_w * 0.02, cy + bolt_h * 0.06),
    (cx - bolt_w * 0.18, cy + bolt_h * 0.50),
    (cx + bolt_w * 0.42, cy - bolt_h * 0.08),
    (cx + bolt_w * 0.04, cy - bolt_h * 0.08),
]
d.polygon(pts, fill=(255, 255, 255, 255))

base = img.resize((256, 256), Image.LANCZOS)
base.save(OUT / "query.ico", format="ICO", sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])
base.save(OUT / "query.png")
print("OK", OUT / "query.ico")
