#!/usr/bin/env python3
"""清理原版建筑贴图里被 FFDec 一起导出的红色底座与蓝色字母。

背景：`import_chapter_assets.py` 取的是 `NodeTown/NodeCastle/NodeCamp/NodeMageTower`
四个 SWF 符号的第 0 帧，而这些帧里带着两块“作者态”元素：

* 一块纯红 (#FF0000) 的底座/旗帜占位图，原版运行时由 ActionScript 染色，导出后固定成红色；
* 蓝色 (#0000FF) 的类型字母（T / C / TOWE），同样只在作者态可见。

游戏里这两块东西会一直压在建筑正面，看起来像贴图坏了。此脚本就地修改贴图：

* 红色底座（含 3px 抗锯齿描边）→ 换成该贴图自身外圈干净像素的中值色并做轻微明暗渐变，
  相当于把被遮住的部分补成一段实心墙体；
* 蓝色字母（含 2px 描边）→ 用同一行左右最近的干净像素颜色填掉；两边都取不到（悬空在
  地图上的字母）就挖成透明。

注意：底座是不透明地盖在建筑本体上的，导出的 PNG 里底座下面没有像素，所以这一步是
“按周边颜色补”的retouch，不是还原。要做到完全还原，只能用原始 SWF 重新挑帧导出。

用法：
    python Tools/TakeoverPipeline/clean_building_baseplates.py
    python Tools/TakeoverPipeline/clean_building_baseplates.py --root <sprites 目录>
"""
from __future__ import annotations

import argparse
import glob
import os

import numpy as np
from PIL import Image

DEFAULT_ROOT = os.path.join("UnityProject", "Assets", "AssetRaw", "Actor", "Takeover", "Sprites")
BUILDING_IDS = (1, 2, 3, 4)  # 城镇 / 城堡 / 营地 / 法师塔：只有这四类带底座与字母
LETTER_CUT_DISTANCE = 2       # 字母像素左右 2px 内找不到干净像素时直接挖空


def dilate(mask: np.ndarray, radius: int) -> np.ndarray:
    out = mask.copy()
    for _ in range(radius):
        grown = out.copy()
        grown[1:, :] |= out[:-1, :]
        grown[:-1, :] |= out[1:, :]
        grown[:, 1:] |= out[:, :-1]
        grown[:, :-1] |= out[:, 1:]
        out = grown
    return out


def build_masks(a: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    alpha = a[:, :, 3]
    r, g, b = (a[:, :, i].astype(int) for i in range(3))
    red_core = (r >= 190) & (g <= 80) & (b <= 80) & (alpha > 0)
    blue_core = (b >= 190) & (r <= 80) & (g <= 80) & (alpha > 0)
    red = dilate(red_core, 3) & (alpha > 0)
    blue = dilate(blue_core, 2) & (alpha > 0) & ~red
    return red, blue


def clean_pixels(a: np.ndarray, red: np.ndarray, blue: np.ndarray) -> np.ndarray:
    alpha = a[:, :, 3]
    r, g, b = (a[:, :, i].astype(int) for i in range(3))
    reddish = (r - np.maximum(g, b) > 25) & (r > 110)
    blueish = (b - np.maximum(r, g) > 25) & (b > 110)
    return ~red & ~blue & (alpha > 120) & ~reddish & ~blueish


def process(path: str) -> tuple[int, int, int]:
    a = np.array(Image.open(path).convert("RGBA"))
    red, blue = build_masks(a)
    if not (red.any() or blue.any()):
        return 0, 0, 0

    rgb = a[:, :, :3].astype(np.int16)
    alpha = a[:, :, 3]
    clean = clean_pixels(a, red, blue)
    out = a.copy()
    out_rgb = rgb.copy()
    out_alpha = alpha.copy()

    if red.any():
        ys, xs = np.nonzero(red)
        y0, y1 = int(ys.min()), int(ys.max())
        ring = dilate(red, 2) & ~red & clean
        base = np.median(rgb[ring], axis=0) if ring.any() else np.array([112.0, 119.0, 78.0])
        for y, x in zip(ys, xs):
            t = (y - y0) / max(1, y1 - y0)
            shade = np.array([1.12, 1.06, 0.93]) * (1.0 - 0.18 * t)
            out_rgb[y, x] = np.clip(base * shade, 0, 255)

    cut = 0
    for y, x in zip(*np.nonzero(blue)):
        row = np.nonzero(clean[y, :])[0]
        left, right = row[row < x], row[row > x]
        cand = []
        if len(left):
            cand.append((x - left[-1], left[-1]))
        if len(right):
            cand.append((right[0] - x, right[0]))
        # 左右都够不到干净像素（字母悬在地图/天空上）就直接挖空，避免留下一块突兀色块
        if not cand or min(cand)[0] > LETTER_CUT_DISTANCE:
            out_alpha[y, x] = 0
            cut += 1
            continue
        out_rgb[y, x] = rgb[y, min(cand)[1]]

    out[:, :, :3] = out_rgb.astype(np.uint8)
    out[:, :, 3] = out_alpha
    Image.fromarray(out, "RGBA").save(path)
    return int(red.sum()), int(blue.sum()), cut


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=DEFAULT_ROOT)
    args = parser.parse_args()

    pattern = os.path.join(args.root, "takeover_building_[1-4]*.png")
    targets = [p for p in sorted(glob.glob(pattern)) if "_f" not in os.path.basename(p)]
    if not targets:
        print(f"没有找到需要处理的贴图：{pattern}")
        return 1

    changed = 0
    for path in targets:
        red, blue, cut = process(path)
        name = os.path.basename(path)
        if red or blue:
            changed += 1
            print(f"{name:32s} 底座={red:5d} 字母={blue:4d} 挖空={cut:3d}")
        else:
            print(f"{name:32s} 已是干净贴图，跳过")
    print(f"完成：{changed}/{len(targets)} 张有改动")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
