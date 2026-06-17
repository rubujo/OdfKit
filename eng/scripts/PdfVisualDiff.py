"""Compare two PDF files by rasterized pixel difference percentage."""
import sys

import numpy as np
from PIL import Image
import pypdfium2 as pdfium


def render(path):
    document = pdfium.PdfDocument(path)
    pages = []
    try:
        for index in range(len(document)):
            page = document[index]
            try:
                bitmap = page.render(scale=96 / 72)
                pages.append(bitmap.to_pil().convert("RGB"))
            finally:
                page.close()
    finally:
        document.close()
    return pages


def pad(image, size):
    if image.size == size:
        return image
    canvas = Image.new("RGB", size, "white")
    canvas.paste(image, (0, 0))
    return canvas


def main():
    if len(sys.argv) != 3:
        print("usage: PdfVisualDiff.py <expected.pdf> <actual.pdf>", file=sys.stderr)
        raise SystemExit(2)

    expected = render(sys.argv[1])
    actual = render(sys.argv[2])
    page_count = max(len(expected), len(actual))
    if page_count == 0:
        print("0", end="")
        raise SystemExit(0)

    different = 0
    total = 0
    for index in range(page_count):
        expected_page = expected[index] if index < len(expected) else Image.new("RGB", (1, 1), "white")
        actual_page = actual[index] if index < len(actual) else Image.new("RGB", (1, 1), "white")
        width = max(expected_page.width, actual_page.width)
        height = max(expected_page.height, actual_page.height)
        expected_array = np.asarray(pad(expected_page, (width, height)), dtype=np.int16)
        actual_array = np.asarray(pad(actual_page, (width, height)), dtype=np.int16)
        delta = np.abs(expected_array - actual_array)
        changed = np.any(delta > 24, axis=2)
        different += int(np.count_nonzero(changed))
        total += changed.size

    percent = (different / total) * 100 if total else 0
    print(f"{percent:.6f}", end="")


if __name__ == "__main__":
    main()