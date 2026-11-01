#!/usr/bin/env python3
"""Extract first-chapter road waypoints from FFDec's SWF XML."""

from __future__ import annotations

import argparse
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Matrix:
    a: float = 1.0
    b: float = 0.0
    c: float = 0.0
    d: float = 1.0
    tx: float = 0.0
    ty: float = 0.0

    def then(self, child: "Matrix") -> "Matrix":
        return Matrix(
            a=self.a * child.a + self.c * child.b,
            b=self.b * child.a + self.d * child.b,
            c=self.a * child.c + self.c * child.d,
            d=self.b * child.c + self.d * child.d,
            tx=self.a * child.tx + self.c * child.ty + self.tx,
            ty=self.b * child.tx + self.d * child.ty + self.ty,
        )

    def point(self, x: float = 0.0, y: float = 0.0) -> tuple[float, float]:
        return self.a * x + self.c * y + self.tx, self.b * x + self.d * y + self.ty


def parse_matrix(node: ET.Element | None) -> Matrix:
    if node is None:
        return Matrix()
    return Matrix(
        a=float(node.attrib.get("scaleX", 1.0)),
        b=float(node.attrib.get("rotateSkew0", 0.0)),
        c=float(node.attrib.get("rotateSkew1", 0.0)),
        d=float(node.attrib.get("scaleY", 1.0)),
        tx=float(node.attrib.get("translateX", 0.0)),
        ty=float(node.attrib.get("translateY", 0.0)),
    )


def child_places(sprite: ET.Element | None) -> list[ET.Element]:
    if sprite is None:
        return []
    sub_tags = sprite.find("subTags")
    if sub_tags is None:
        return []
    return [
        item
        for item in sub_tags
        if item.tag == "item"
        and item.attrib.get("type") in {"PlaceObject2Tag", "PlaceObject3Tag"}
        and item.attrib.get("placeFlagHasCharacter") == "true"
    ]


def symbol_ids(root: ET.Element) -> dict[str, int]:
    result: dict[str, int] = {}
    for tag in root.iter("item"):
        if tag.attrib.get("type") != "SymbolClassTag":
            continue
        tags = tag.find("tags")
        names = tag.find("names")
        if tags is None or names is None:
            continue
        ids = [int(item.text) for item in tags if item.tag == "item" and item.text]
        class_names = [item.text or "" for item in names if item.tag == "item"]
        result.update(zip(class_names, ids))
    return result


def sprite_definitions(root: ET.Element) -> dict[int, ET.Element]:
    return {
        int(tag.attrib["spriteId"]): tag
        for tag in root.iter("item")
        if tag.attrib.get("type") == "DefineSpriteTag" and "spriteId" in tag.attrib
    }


def extract_level(
    level: int,
    root_sprite: ET.Element,
    sprites: dict[int, ET.Element],
) -> dict[tuple[str, str], list[tuple[float, float]]]:
    result: dict[tuple[str, str], list[tuple[float, float]]] = {}
    for edge_place in child_places(root_sprite):
        name = edge_place.attrib.get("name", "")
        if not name.startswith("ed_"):
            continue
        parts = name[3:].split("_", 1)
        if len(parts) != 2:
            continue
        node_a, node_b = parts
        edge_matrix = parse_matrix(edge_place.find("matrix"))
        edge_sprite = sprites.get(int(edge_place.attrib.get("characterId", "0")))
        points: list[tuple[float, float]] = []
        for waypoint_place in child_places(edge_sprite):
            world = edge_matrix.then(parse_matrix(waypoint_place.find("matrix")))
            raw_x, raw_y = world.point()
            # FFDec exports the 800x600 stage at x20 resolution. LevelVisual
            # converts the resulting display Y back to doubled logical space.
            points.append((round(raw_x / 20.0, 4), round(raw_y / 20.0 * 2.0, 4)))
        result[(node_a, node_b)] = points
    return result


def format_float(value: float) -> str:
    return f"{value:.4f}".rstrip("0").rstrip(".")


def write_csharp(path: Path, levels: dict[int, dict[tuple[str, str], list[tuple[float, float]]]]) -> None:
    lines = [
        "using System.Collections.Generic;",
        "using UnityEngine;",
        "",
        "namespace GameLogic.Takeover",
        "{",
        "    /// <summary>Generated from FFDec takeover.xml Level1-8GraphMarkersClass.</summary>",
        "    public static class OriginalMapWaypoints",
        "    {",
        "        private static readonly Dictionary<string, Vector2[]> Entries =",
        "            new Dictionary<string, Vector2[]>",
        "            {",
    ]
    for level in sorted(levels):
        for (node_a, node_b), points in sorted(levels[level].items()):
            if not points:
                continue
            key = f"{level}:{node_a}:{node_b}"
            values = ", ".join(
                f"new Vector2({format_float(x)}f, {format_float(y)}f)" for x, y in points
            )
            lines.append(f'                ["{key}"] = new[] {{ {values} }},')
    lines.extend(
        [
            "            };",
            "",
            "        public static IReadOnlyList<Vector2> For(int level, string nodeA, string nodeB)",
            "        {",
            "            string key = level + \":\" + nodeA + \":\" + nodeB;",
            "            return Entries.TryGetValue(key, out Vector2[] points) ? points : System.Array.Empty<Vector2>();",
            "        }",
            "    }",
            "}",
        ]
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--swf-xml", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    root = ET.parse(args.swf_xml).getroot()
    symbols = symbol_ids(root)
    sprites = sprite_definitions(root)
    levels: dict[int, dict[tuple[str, str], list[tuple[float, float]]]] = {}
    for level in range(1, 9):
        class_name = f"Level{level}GraphMarkersClass"
        sprite_id = symbols[class_name]
        levels[level] = extract_level(level, sprites[sprite_id], sprites)
    write_csharp(args.output, levels)
    edge_count = sum(len(entries) for entries in levels.values())
    point_count = sum(len(points) for entries in levels.values() for points in entries.values())
    print(f"levels={len(levels)} edges={edge_count} waypoints={point_count}")


if __name__ == "__main__":
    main()
