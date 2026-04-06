#!/usr/bin/env python3
"""
Gera um arquivo .ico simples para uso no build do WM-player.

Uso:
  python scripts/generate_icon_ico.py <saida.ico>
"""

from __future__ import annotations

import struct
import sys
import zlib
from pathlib import Path


def _png_chunk(tag: bytes, data: bytes) -> bytes:
    return (
        struct.pack(">I", len(data))
        + tag
        + data
        + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    )


def _create_png(size: int = 256) -> bytes:
    w = h = size
    rows = bytearray()

    for y in range(h):
        rows.append(0)  # filtro PNG
        for x in range(w):
            r, g, b, a = 25, 118, 210, 255

            # Triângulo "play" simples
            if 78 <= x <= 188 and 46 <= y <= 210:
                if y > (-1.45 * (x - 78) + 210) and y < (1.45 * (x - 78) + 46):
                    r, g, b = 245, 245, 245

            rows.extend((r, g, b, a))

    signature = b"\x89PNG\r\n\x1a\n"
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    idat = zlib.compress(bytes(rows), 9)
    return (
        signature
        + _png_chunk(b"IHDR", ihdr)
        + _png_chunk(b"IDAT", idat)
        + _png_chunk(b"IEND", b"")
    )


def create_ico(output_path: Path) -> None:
    png = _create_png(size=256)

    icon_dir = struct.pack("<HHH", 0, 1, 1)
    icon_entry = struct.pack("<BBBBHHII", 0, 0, 0, 0, 1, 32, len(png), 6 + 16)
    ico_data = icon_dir + icon_entry + png

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(ico_data)


def main() -> int:
    if len(sys.argv) != 2:
        print("Uso: python scripts/generate_icon_ico.py <saida.ico>")
        return 1

    output_path = Path(sys.argv[1])
    create_ico(output_path)
    print(f"Ícone gerado em: {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
