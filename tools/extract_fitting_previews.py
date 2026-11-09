from __future__ import annotations

import argparse
import re
import subprocess
import tempfile
from pathlib import Path

import pdfplumber
from PIL import Image, ImageChops, ImageDraw, ImageFont, ImageOps


def expected_codes() -> list[str]:
    category_counts = {1: 8, 2: 12, 3: 15, 4: 7, 5: 35, 6: 11, 7: 15}
    return [f"{category}-{number}" for category, count in category_counts.items() for number in range(1, count + 1)]


def locate_headings(pdf_path: Path) -> dict[str, tuple[int, float, float]]:
    expected = set(expected_codes())
    found: dict[str, tuple[int, float, float]] = {}
    with pdfplumber.open(pdf_path) as pdf:
        for page_index, page in enumerate(pdf.pages):
            for word in page.extract_words(use_text_flow=True):
                text = word["text"]
                if text in expected and word["x0"] < 105 and text not in found:
                    found[text] = (page_index, float(word["top"]), float(word["x0"]))
    missing = sorted(expected - set(found))
    if missing:
        raise RuntimeError(f"Could not locate ASHRAE fitting headings: {', '.join(missing)}")
    return found


def render_pages(pdf_path: Path, pdftoppm_path: Path, output_dir: Path, dpi: int) -> None:
    prefix = output_dir / "page"
    subprocess.run(
        [str(pdftoppm_path), "-r", str(dpi), "-png", str(pdf_path), str(prefix)],
        check=True,
    )


def trim_to_content(image: Image.Image, threshold: int = 248, padding: int = 14) -> Image.Image:
    gray = image.convert("L")
    background = Image.new("L", gray.size, 255)
    difference = ImageChops.difference(gray, background).point(lambda value: 255 if value > 255 - threshold else 0)
    bbox = difference.getbbox()
    if bbox is None:
        return image
    left = max(0, bbox[0] - padding)
    top = max(0, bbox[1] - padding)
    right = min(image.width, bbox[2] + padding)
    bottom = min(image.height, bbox[3] + padding)
    return image.crop((left, top, right, bottom))


def normalize_section(image: Image.Image, maximum_width: int = 1200) -> Image.Image:
    image = trim_to_content(image).convert("L").convert("RGB")
    if image.width > maximum_width:
        height = round(image.height * maximum_width / image.width)
        image = image.resize((maximum_width, height), Image.Resampling.LANCZOS)
    return image


def build_contact_sheets(image_dir: Path, contact_dir: Path, codes: list[str]) -> None:
    contact_dir.mkdir(parents=True, exist_ok=True)
    font = ImageFont.load_default(size=18)
    tile_size = 220
    columns = 5
    rows = 4
    per_sheet = columns * rows
    for sheet_index in range((len(codes) + per_sheet - 1) // per_sheet):
        sheet = Image.new("RGB", (columns * tile_size, rows * (tile_size + 28)), "#dfe5eb")
        draw = ImageDraw.Draw(sheet)
        for item_index, code in enumerate(codes[sheet_index * per_sheet:(sheet_index + 1) * per_sheet]):
            row, column = divmod(item_index, columns)
            preview = Image.open(image_dir / f"{code}.png").convert("RGB")
            preview.thumbnail((tile_size - 12, tile_size - 12), Image.Resampling.LANCZOS)
            x = column * tile_size + (tile_size - preview.width) // 2
            y = row * (tile_size + 28) + 6
            sheet.paste(preview, (x, y))
            draw.text((column * tile_size + 8, row * (tile_size + 28) + tile_size + 4), code, fill="#10263b", font=font)
        sheet.save(contact_dir / f"fitting-previews-{sheet_index + 1}.png", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser(description="Extract ASHRAE fitting diagram previews from the supplied PDF.")
    parser.add_argument("pdf", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("pdftoppm", type=Path)
    parser.add_argument("--contacts", type=Path)
    parser.add_argument("--dpi", type=int, default=240)
    args = parser.parse_args()

    codes = expected_codes()
    headings = locate_headings(args.pdf)
    args.output.mkdir(parents=True, exist_ok=True)

    page_headings: dict[int, list[tuple[str, float]]] = {}
    for code, (page_index, top, _) in headings.items():
        page_headings.setdefault(page_index, []).append((code, top))
    for page_items in page_headings.values():
        page_items.sort(key=lambda item: item[1])

    scale = args.dpi / 72.0
    with tempfile.TemporaryDirectory(prefix="ductsizer-fitting-pages-") as temp_dir_name:
        temp_dir = Path(temp_dir_name)
        render_pages(args.pdf, args.pdftoppm, temp_dir, args.dpi)
        rendered_pages = sorted(temp_dir.glob("page-*.png"))
        if len(rendered_pages) != 61:
            raise RuntimeError(f"Expected 61 rendered pages, found {len(rendered_pages)}")

        ordered = sorted(codes, key=lambda code: (headings[code][0], headings[code][1]))
        for code_index, code in enumerate(ordered):
            page_index, heading_top, _ = headings[code]
            if code_index + 1 < len(ordered):
                next_page_index, next_heading_top, _ = headings[ordered[code_index + 1]]
            else:
                next_page_index, next_heading_top = len(rendered_pages) - 1, 750.0

            sections: list[Image.Image] = []
            for section_page_index in range(page_index, next_page_index + 1):
                page_image = Image.open(rendered_pages[section_page_index]).convert("RGB")
                crop_top = heading_top - 5.0 if section_page_index == page_index else 55.0
                crop_bottom = next_heading_top - 5.0 if section_page_index == next_page_index else 750.0
                if crop_bottom <= crop_top:
                    continue
                sections.append(page_image.crop(
                    (
                        round(72.0 * scale),
                        round(max(55.0, crop_top) * scale),
                        round(540.0 * scale),
                        round(min(750.0, crop_bottom) * scale),
                    )
                ))

            if not sections:
                raise RuntimeError(f"No source section was captured for ASHRAE fitting {code}")
            separator = max(4, round(4 * scale))
            stitched_height = sum(section.height for section in sections) + separator * (len(sections) - 1)
            stitched = Image.new("RGB", (max(section.width for section in sections), stitched_height), "white")
            y = 0
            for section in sections:
                stitched.paste(section, (0, y))
                y += section.height + separator
            normalize_section(stitched).save(args.output / f"{code}.png", optimize=True)

    generated = sorted(args.output.glob("*.png"))
    if len(generated) != len(codes):
        raise RuntimeError(f"Expected {len(codes)} previews, generated {len(generated)}")
    if args.contacts:
        build_contact_sheets(args.output, args.contacts, codes)
    print(f"Generated {len(generated)} fitting previews in {args.output}")


if __name__ == "__main__":
    main()
