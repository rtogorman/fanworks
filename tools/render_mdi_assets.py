"""Render the locally vendored Material Design SVGs used by DuctSizer.

The source SVG package is @mdi/svg 7.4.47.  Its Pictogrammers license is
kept beside the generated PNG assets.  This script is a release-maintenance
helper; the application only embeds the generated PNG files.
"""

from __future__ import annotations

import io
from pathlib import Path

import resvg_py
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSET_ROOT = ROOT / "src" / "DuctSizer.WinForms" / "Assets" / "OpenSourceIcons"
SOURCE_ROOT = ASSET_ROOT / "svg-source"
OUTPUT_ROOT = ASSET_ROOT / "png"

ICONS = {
    "space-source": "home-export-outline",
    "space-sink": "home-import-outline",
    "fan": "fan",
    "coil": "heating-coil",
    "filter": "air-filter",
    "elbow": "pipe-elbow-cc0",
    "transition": "reducer-iso",
    "diverging": "call-split",
    "converging": "call-merge",
    "damper": "valve",
    "terminal": "range-hood-google",
    "grd-grille": "view-grid",
    "grd-diffuser": "grid",
    "grd-register": "view-dashboard",
    "other": "rhombus-outline",
    "sensor-pressure": "gauge",
    "sensor-temperature": "thermometer",
    "sensor-velocity": "speedometer",
    "sensor-airflow": "alpha-q-circle-outline",
    "duct": "pipe",
}


def render_icon(source_name: str, output_name: str, size: int = 192) -> None:
    source = SOURCE_ROOT / f"{source_name}.svg"
    if not source.exists():
        raise FileNotFoundError(source)
    svg = source.read_text(encoding="utf-8")
    png_bytes = resvg_py.svg_to_bytes(svg_string=svg, width=size, height=size)
    with Image.open(io.BytesIO(png_bytes)) as image:
        image.convert("RGBA").save(OUTPUT_ROOT / f"{output_name}.png", optimize=True)


def render_app_icon() -> None:
    source = OUTPUT_ROOT / "fan.png"
    with Image.open(source) as glyph:
        glyph = glyph.convert("RGBA")
        canvas = Image.new("RGBA", (256, 256), (18, 54, 80, 255))
        draw = ImageDraw.Draw(canvas)
        draw.rounded_rectangle((8, 8, 247, 247), radius=54, fill=(18, 54, 80, 255))
        glyph = glyph.resize((188, 188), Image.Resampling.LANCZOS)
        # The source is black; use its alpha as a mask for the DuctSizer cyan.
        colored = Image.new("RGBA", glyph.size, (68, 190, 214, 255))
        colored.putalpha(glyph.getchannel("A"))
        canvas.alpha_composite(colored, (34, 34))
        canvas.save(
            ROOT / "src" / "DuctSizer.WinForms" / "Assets" / "DuctSizerHvacIcon.ico",
            sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
        )


def main() -> None:
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    for output_name, source_name in ICONS.items():
        render_icon(source_name, output_name)
    render_app_icon()


if __name__ == "__main__":
    main()
