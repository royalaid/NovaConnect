from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSET_PATH = ROOT / "src" / "NovaConnect.App" / "Assets" / "NovaConnect.ico"
PREVIEW_PATH = ROOT / "artifacts" / "design" / "NovaConnect-icon.png"
SHEET_PATH = ROOT / "artifacts" / "design" / "NovaConnect-icon-sheet.png"
ICON_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]


def blend(start: tuple[int, int, int], end: tuple[int, int, int], amount: float) -> tuple[int, int, int, int]:
    return tuple(round(a + ((b - a) * amount)) for a, b in zip(start, end)) + (255,)


def render_icon(size: int) -> Image.Image:
    scale = 4
    canvas_size = size * scale
    image = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    mask = Image.new("L", image.size, 0)
    mask_draw = ImageDraw.Draw(mask)
    margin = max(scale, round(canvas_size * 0.055))
    radius = round(canvas_size * 0.22)
    mask_draw.rounded_rectangle(
        (margin, margin, canvas_size - margin, canvas_size - margin),
        radius=radius,
        fill=255,
    )

    gradient = Image.new("RGBA", image.size, (0, 0, 0, 0))
    gradient_draw = ImageDraw.Draw(gradient)
    top = (18, 119, 232)
    bottom = (5, 78, 177)
    for y in range(canvas_size):
        gradient_draw.line(
            (0, y, canvas_size, y),
            fill=blend(top, bottom, y / (canvas_size - 1)),
        )
    image.paste(gradient, mask=mask)

    draw = ImageDraw.Draw(image)
    white = (248, 250, 252, 255)
    stroke = max(scale * 2, round(canvas_size * 0.115))
    left_x = round(canvas_size * 0.31)
    right_x = round(canvas_size * 0.70)
    top_y = round(canvas_size * 0.29)
    bottom_y = round(canvas_size * 0.72)
    draw.line((left_x, bottom_y, left_x, top_y), fill=white, width=stroke)
    draw.line((left_x, top_y, right_x, bottom_y), fill=white, width=stroke)
    draw.line((right_x, bottom_y, right_x, top_y), fill=white, width=stroke)
    radius = stroke // 2
    for x, y in ((left_x, bottom_y), (left_x, top_y), (right_x, bottom_y), (right_x, top_y)):
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=white)

    return image.resize((size, size), Image.Resampling.LANCZOS)


def render_sheet(frames: dict[int, Image.Image]) -> Image.Image:
    tile = 112
    gap = 8
    sheet = Image.new("RGB", ((tile + gap) * len(ICON_SIZES) + gap, (tile + gap) * 2 + gap), "white")
    draw = ImageDraw.Draw(sheet)
    backgrounds = [(242, 245, 248), (31, 35, 41)]
    for row, background in enumerate(backgrounds):
        for column, size in enumerate(ICON_SIZES):
            x = gap + column * (tile + gap)
            y = gap + row * (tile + gap)
            draw.rounded_rectangle((x, y, x + tile, y + tile), radius=12, fill=background)
            preview_size = min(80, max(48, size * 3))
            preview = frames[size].resize((preview_size, preview_size), Image.Resampling.NEAREST)
            sheet.paste(
                preview,
                (x + (tile - preview_size) // 2, y + (tile - preview_size) // 2),
                preview,
            )
    return sheet


def main() -> None:
    ASSET_PATH.parent.mkdir(parents=True, exist_ok=True)
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    frames = {size: render_icon(size) for size in ICON_SIZES}
    frames[256].save(
        ASSET_PATH,
        format="ICO",
        sizes=[(size, size) for size in ICON_SIZES],
        append_images=[frames[size] for size in ICON_SIZES if size != 256],
    )
    frames[256].save(PREVIEW_PATH)
    render_sheet(frames).save(SHEET_PATH)
    print(ASSET_PATH)
    print(PREVIEW_PATH)
    print(SHEET_PATH)


if __name__ == "__main__":
    main()
