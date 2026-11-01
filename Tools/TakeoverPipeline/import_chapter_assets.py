#!/usr/bin/env python3
"""Import first-chapter Takeover assets exported by FFDec into YooAsset."""

from __future__ import annotations

import argparse
import csv
import json
import re
import shutil
from collections import defaultdict
from pathlib import Path


FACTION = {
    "西利亚公国": "Green",
    "卡若米森部落": "Red",
    "艾斯得雷邪教": "Blue",
    "瑞维迪斯帝国（AI）": "Purple",
    "中立/强盗": "Gray",
}
UNIT_TYPE = {
    "步兵": "Infantry",
    "弓兵": "Archers",
    "骑兵": "Cavalry",
    "特殊/法术": "Magic",
    "攻城器械": "Siege",
}
BUILDING_CLASS = {
    1: "NodeTown{color}Class",
    2: "NodeCastle{color}Class",
    3: "NodeCamp{color}Class",
    4: "NodeMageTower{color}Class",
    5: "Village{color}Class",
    6: "Mine{color}Class",
    7: "Obelisk{color}Class",
}
COLORS = ("Green", "Red", "Blue", "Purple")


def slug(value: str) -> str:
    value = re.sub(r"[^a-z0-9]+", "_", value.strip().lower())
    return value.strip("_") or "unknown"


def load_symbols(path: Path) -> dict[str, int]:
    symbols: dict[str, int] = {}
    with path.open(encoding="utf-8") as stream:
        for line in stream:
            character_id, raw_name = line.rstrip("\n").split(";", 1)
            name = raw_name.strip().strip('"')
            if name:
                symbols[name] = int(character_id)
    return symbols


def sprite_dir(sprite_root: Path, character_id: int) -> Path | None:
    matches = sorted(sprite_root.glob(f"DefineSprite_{character_id}_*"))
    if matches:
        return matches[0]
    fallback = sprite_root / f"DefineSprite_{character_id}"
    return fallback if fallback.is_dir() else None


def frames(folder: Path) -> list[Path]:
    return sorted(
        (p for p in folder.glob("*.png") if p.stem.isdigit()),
        key=lambda p: int(p.stem),
    )


def copy_frame(source: Path, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)


def import_sprite(
    symbols: dict[str, int],
    sprite_root: Path,
    class_name: str,
    location: str,
    output: Path,
    mapping: list[dict[str, object]],
    missing: list[str],
    animation_frames: int = 3,
) -> bool:
    character_id = symbols.get(class_name)
    folder = sprite_dir(sprite_root, character_id) if character_id is not None else None
    images = frames(folder) if folder else []
    if not images:
        missing.append(f"{class_name}: DefineSprite_{character_id or '?'}")
        return False

    copy_frame(images[0], output / f"{location}.png")
    if animation_frames > 0:
        count = min(animation_frames, len(images))
        indexes = (
            sorted({round(i * (len(images) - 1) / max(1, count - 1)) for i in range(count)})
            if len(images) > 1 else [0]
        )
        while len(indexes) < animation_frames:
            indexes.append(indexes[-1])
        for index, image_index in enumerate(indexes):
            copy_frame(images[image_index], output / f"{location}_f{index}.png")
    mapping.append(
        {
            "category": "sprite",
            "class_name": class_name,
            "character_id": character_id,
            "frame_count": len(images),
            "unity_location": location,
            "source": str(images[0]),
        }
    )
    return True


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=Path("GameData/extracted/takeover104"))
    parser.add_argument("--project", type=Path, default=Path("UnityProject"))
    parser.add_argument(
        "--manifest",
        type=Path,
        default=Path("Tools/TakeoverPipeline/imported-assets.json"),
    )
    args = parser.parse_args()

    symbols = load_symbols(args.source / "symbolClass" / "symbols.csv")
    sprite_root = args.source / "sprites"
    asset_raw = args.project / "Assets" / "AssetRaw"
    sprite_out = asset_raw / "Actor" / "Takeover" / "Sprites"
    map_out = asset_raw / "Scenes" / "Takeover" / "Maps"
    audio_out = asset_raw / "Audios" / "Takeover"
    font_out = asset_raw / "Fonts" / "Takeover"
    raw_out = asset_raw / "UIRaw" / "Raw" / "Takeover"
    for directory in (sprite_out, map_out, audio_out, font_out, raw_out):
        directory.mkdir(parents=True, exist_ok=True)

    mapping: list[dict[str, object]] = []
    missing: list[str] = []

    units_path = Path("Docs/城邦争霸数值/units.csv")
    with units_path.open(encoding="utf-8-sig", newline="") as stream:
        for unit_id, row in enumerate(csv.DictReader(stream), start=1):
            color = FACTION[row["阵营"]]
            unit_type = UNIT_TYPE[row["兵种"]]
            tier = int(row["阶"])
            tier_suffix = "" if tier == 1 else str(tier)
            location = f"takeover_unit_{unit_id}_{slug(row['英文名'])}"
            class_name = f"{color}{unit_type}{tier_suffix}Up_Go"
            if row["英文名"] == "Ogre":
                class_name = "NodeDefenderOgreClass"
            elif color == "Gray" and class_name not in symbols:
                # The three bandit squad symbols are not present in 1.0.4's
                # art library; use the original neutral caster sheet so every
                # configured unit still has an authentic non-placeholder visual.
                class_name = "GrayMagicUp_Go"
            import_sprite(symbols, sprite_root, class_name, location, sprite_out, mapping, missing)

    for building_id, template in BUILDING_CLASS.items():
        for color in COLORS:
            class_name = template.format(color=color)
            location = f"takeover_building_{building_id}_{color.lower()}"
            import_sprite(symbols, sprite_root, class_name, location, sprite_out, mapping, missing)
        import_sprite(
            symbols,
            sprite_root,
            template.format(color="Green"),
            f"takeover_building_{building_id}",
            sprite_out,
            mapping,
            missing,
        )

    for class_name, location in (
        ("NodeBuildHereClass", "takeover_node_buildspot"),
        ("NodeChestClass", "takeover_node_chest"),
        ("NodeDefenderOgreClass", "takeover_node_ogre"),
    ):
        import_sprite(symbols, sprite_root, class_name, location, sprite_out, mapping, missing, 0)

    for level_id in range(1, 9):
        import_sprite(
            symbols,
            sprite_root,
            f"Level{level_id}Class",
            f"takeover_map_{level_id}",
            map_out,
            mapping,
            missing,
            0,
        )

    for sound in sorted((args.source / "sounds_wav").glob("*.wav")):
        stem = sound.stem
        parts = stem.split("_", 2)
        name = parts[1] if len(parts) > 1 else stem
        shutil.copyfile(sound, audio_out / f"takeover_sfx_{sound.stem}.wav")
        mapping.append(
            {
                "category": "audio",
                "class_name": name,
                "character_id": int(parts[0]) if parts[0].isdigit() else 0,
                "unity_location": f"takeover_sfx_{sound.stem}",
                "source": str(sound),
            }
        )

    for font in sorted((args.source / "fonts").glob("*.ttf")):
        destination = font_out / f"takeover_font_{font.stem.replace(' ', '_')}.ttf"
        shutil.copyfile(font, destination)
        mapping.append(
            {
                "category": "font",
                "unity_location": destination.stem,
                "source": str(font),
            }
        )

    for image in sorted((args.source / "images").glob("*")):
        if image.suffix.lower() not in {".png", ".jpg", ".jpeg", ".gif"}:
            continue
        destination = raw_out / f"takeover_raw_{slug(image.stem)}{image.suffix.lower()}"
        shutil.copyfile(image, destination)
        mapping.append(
            {
                "category": "raw-image",
                "unity_location": destination.stem,
                "source": str(image),
            }
        )

    report = {
        "source_classes": len(symbols),
        "imported": len(mapping),
        "missing": missing,
        "assets": mapping,
    }
    args.manifest.parent.mkdir(parents=True, exist_ok=True)
    args.manifest.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Imported {len(mapping)} assets; missing={len(missing)}")
    for item in missing:
        print("MISSING", item)
    return 0 if not missing else 2


if __name__ == "__main__":
    raise SystemExit(main())
