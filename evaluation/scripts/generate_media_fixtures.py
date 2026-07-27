#!/usr/bin/env python3
"""Generate small reviewed media fixtures for AI-service evaluation."""

from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DATASETS = ROOT / "datasets"
MEDIA = ROOT / "media"


def load_json(path: Path) -> list[dict[str, object]]:
    return json.loads(path.read_text(encoding="utf-8"))


def generate_audio() -> None:
    say = shutil.which("say")
    if say is None:
        print("Skipping audio generation: macOS 'say' command was not found.")
        return

    afconvert = shutil.which("afconvert")

    for case in load_json(DATASETS / "audio_cases.json"):
        file_path = case.get("filePath")
        transcript = case.get("referenceTranscript")
        if not isinstance(file_path, str) or not isinstance(transcript, str):
            continue

        output = ROOT / file_path
        output.parent.mkdir(parents=True, exist_ok=True)
        if output.suffix.lower() == ".wav" and afconvert is not None:
            temporary_aiff = output.with_suffix(".aiff")
            subprocess.run([say, "-v", "Samantha", "-o", str(temporary_aiff), transcript], check=True)
            subprocess.run(
                [afconvert, "-f", "WAVE", "-d", "LEI16", "-r", "16000", str(temporary_aiff), str(output)],
                check=True,
            )
            temporary_aiff.unlink(missing_ok=True)
        else:
            subprocess.run([say, "-v", "Samantha", "-o", str(output), transcript], check=True)
        print(f"Generated {output.relative_to(ROOT)}")


def generate_images() -> None:
    for case in load_json(DATASETS / "image_cases.json"):
        file_path = case.get("filePath")
        fixture = case.get("fixture")
        if not isinstance(file_path, str) or not isinstance(fixture, str):
            continue

        output = ROOT / file_path
        output.parent.mkdir(parents=True, exist_ok=True)
        write_ppm(output, build_image(fixture))
        print(f"Generated {output.relative_to(ROOT)}")


def build_image(fixture: str) -> list[list[tuple[int, int, int]]]:
    width = 360
    height = 240
    pixels = [[(238, 242, 239) for _ in range(width)] for _ in range(height)]

    if fixture == "road_damage_pothole":
        fill_rect(pixels, 0, 70, width, height, (91, 96, 98))
        draw_lane_markings(pixels)
        fill_ellipse(pixels, 180, 145, 62, 32, (28, 31, 32))
        fill_ellipse(pixels, 180, 145, 42, 20, (67, 70, 72))
        draw_crack(pixels, [(118, 132), (95, 118), (72, 115), (54, 98)])
        draw_crack(pixels, [(222, 150), (248, 158), (276, 157), (300, 172)])
    elif fixture == "flooding_standing_water":
        fill_rect(pixels, 0, 78, width, height, (117, 123, 124))
        fill_ellipse(pixels, 180, 155, 132, 48, (74, 139, 188))
        fill_ellipse(pixels, 200, 150, 94, 28, (117, 178, 213))
        fill_rect(pixels, 136, 112, 220, 154, (58, 65, 68))
        for x in range(146, 214, 14):
            draw_line(pixels, x, 116, x, 150, (202, 211, 207), 2)
        for y in range(122, 150, 12):
            draw_line(pixels, 140, y, 216, y, (202, 211, 207), 2)
    elif fixture == "streetlight_outage":
        fill_rect(pixels, 0, 0, width, height, (32, 43, 55))
        fill_rect(pixels, 0, 172, width, height, (76, 83, 85))
        draw_line(pixels, 176, 170, 176, 44, (118, 123, 124), 8)
        draw_line(pixels, 176, 46, 226, 46, (118, 123, 124), 5)
        fill_ellipse(pixels, 238, 49, 18, 14, (96, 102, 103))
        draw_line(pixels, 228, 34, 248, 64, (236, 82, 82), 4)
        draw_line(pixels, 248, 34, 228, 64, (236, 82, 82), 4)
    elif fixture == "sanitation_illegal_dumping":
        fill_rect(pixels, 0, 104, width, height, (167, 170, 166))
        fill_rect(pixels, 38, 55, width, 108, (208, 214, 210))
        fill_ellipse(pixels, 130, 162, 34, 40, (33, 42, 48))
        fill_ellipse(pixels, 182, 165, 38, 44, (35, 93, 75))
        fill_rect(pixels, 218, 133, 296, 184, (146, 113, 82))
        draw_line(pixels, 52, 102, 328, 102, (112, 124, 119), 3)
    elif fixture == "tree_hazard_fallen_branch":
        fill_rect(pixels, 0, 124, width, height, (186, 190, 181))
        fill_rect(pixels, 0, 0, width, 124, (148, 185, 143))
        draw_line(pixels, 58, 92, 310, 178, (97, 64, 40), 18)
        draw_line(pixels, 136, 118, 116, 65, (97, 64, 40), 10)
        draw_line(pixels, 210, 142, 246, 94, (97, 64, 40), 10)
        fill_ellipse(pixels, 110, 58, 44, 25, (55, 130, 81))
        fill_ellipse(pixels, 250, 88, 42, 28, (55, 130, 81))

    return pixels


def write_ppm(path: Path, pixels: list[list[tuple[int, int, int]]]) -> None:
    height = len(pixels)
    width = len(pixels[0]) if height else 0
    with path.open("wb") as output:
        output.write(f"P6\n{width} {height}\n255\n".encode("ascii"))
        for row in pixels:
            for red, green, blue in row:
                output.write(bytes((red, green, blue)))


def fill_rect(
    pixels: list[list[tuple[int, int, int]]],
    x1: int,
    y1: int,
    x2: int,
    y2: int,
    color: tuple[int, int, int],
) -> None:
    height = len(pixels)
    width = len(pixels[0])
    for y in range(max(0, y1), min(height, y2)):
        for x in range(max(0, x1), min(width, x2)):
            pixels[y][x] = color


def fill_ellipse(
    pixels: list[list[tuple[int, int, int]]],
    center_x: int,
    center_y: int,
    radius_x: int,
    radius_y: int,
    color: tuple[int, int, int],
) -> None:
    for y in range(center_y - radius_y, center_y + radius_y + 1):
        for x in range(center_x - radius_x, center_x + radius_x + 1):
            if ((x - center_x) / radius_x) ** 2 + ((y - center_y) / radius_y) ** 2 <= 1:
                set_pixel(pixels, x, y, color)


def draw_lane_markings(pixels: list[list[tuple[int, int, int]]]) -> None:
    for x in range(24, 360, 78):
        fill_rect(pixels, x, 150, x + 42, 156, (230, 218, 130))


def draw_crack(pixels: list[list[tuple[int, int, int]]], points: list[tuple[int, int]]) -> None:
    for left, right in zip(points, points[1:], strict=False):
        draw_line(pixels, left[0], left[1], right[0], right[1], (24, 27, 28), 3)


def draw_line(
    pixels: list[list[tuple[int, int, int]]],
    x1: int,
    y1: int,
    x2: int,
    y2: int,
    color: tuple[int, int, int],
    thickness: int,
) -> None:
    dx = abs(x2 - x1)
    sx = 1 if x1 < x2 else -1
    dy = -abs(y2 - y1)
    sy = 1 if y1 < y2 else -1
    error = dx + dy
    x = x1
    y = y1

    while True:
        for offset_y in range(-(thickness // 2), thickness // 2 + 1):
            for offset_x in range(-(thickness // 2), thickness // 2 + 1):
                set_pixel(pixels, x + offset_x, y + offset_y, color)

        if x == x2 and y == y2:
            break

        doubled = 2 * error
        if doubled >= dy:
            error += dy
            x += sx
        if doubled <= dx:
            error += dx
            y += sy


def set_pixel(pixels: list[list[tuple[int, int, int]]], x: int, y: int, color: tuple[int, int, int]) -> None:
    if 0 <= y < len(pixels) and 0 <= x < len(pixels[0]):
        pixels[y][x] = color


def main() -> None:
    MEDIA.mkdir(parents=True, exist_ok=True)
    generate_audio()
    generate_images()


if __name__ == "__main__":
    main()
