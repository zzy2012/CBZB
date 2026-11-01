#!/usr/bin/env python3
"""Create the deterministic Unity location list for the first-chapter assets."""

from __future__ import annotations

import argparse
import csv
import re
from pathlib import Path


def slug(value: str) -> str:
    value = re.sub(r"[^a-z0-9]+", "_", value.strip().lower())
    return value.strip("_") or "unknown"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--units",
        type=Path,
        default=Path("Docs/城邦争霸数值/units.csv"),
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("Tools/TakeoverPipeline/asset-locations.csv"),
    )
    args = parser.parse_args()

    buildings = [
        (1, "TOWN", "城镇", "takeover_building_town"),
        (2, "CASTLE", "要塞", "takeover_building_castle"),
        (3, "CAMP", "营地", "takeover_building_camp"),
        (4, "MAGIC TOWER", "魔法塔", "takeover_building_magetower"),
        (5, "FARM", "农场", "takeover_building_farm"),
        (6, "MINE", "矿场", "takeover_building_mine"),
        (7, "OBELISK", "英雄碑", "takeover_building_obelisk"),
    ]
    nodes = [
        ("EMPTY", "空地节点", "takeover_node_empty"),
        ("BUILD SPOT", "可建造点", "takeover_node_buildspot"),
        ("CHEST", "宝箱", "takeover_node_chest"),
        ("OGRE", "食人魔", "takeover_node_ogre"),
    ]

    rows: list[dict[str, object]] = []
    with args.units.open(encoding="utf-8-sig", newline="") as stream:
        for index, unit in enumerate(csv.DictReader(stream), start=1):
            rows.append(
                {
                    "category": "unit",
                    "id": index,
                    "display_name": unit["中文名"],
                    "english_name": unit["英文名"],
                    "unity_location": f"takeover_unit_{slug(unit['英文名'])}",
                    "source_symbol": "",
                    "status": "pending-original-import",
                }
            )
    for building_id, english, chinese, location in buildings:
        rows.append(
            {
                "category": "building",
                "id": building_id,
                "display_name": chinese,
                "english_name": english,
                "unity_location": location,
                "source_symbol": "",
                "status": "pending-original-import",
            }
        )
    for node_id, (english, chinese, location) in enumerate(nodes, start=1):
        rows.append(
            {
                "category": "node",
                "id": node_id,
                "display_name": chinese,
                "english_name": english,
                "unity_location": location,
                "source_symbol": "",
                "status": "pending-original-import",
            }
        )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    print(f"Wrote {len(rows)} asset locations to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
