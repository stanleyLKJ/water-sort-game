from __future__ import annotations

import json
import math
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageOps


ROOT = Path(__file__).resolve().parents[1]
SRC = Path(r"C:\Users\wuton\Downloads\图片_20260603131759_38_19.jpg")
OUT = ROOT / "art" / "layer_extraction" / "picture_20260603131759_38_19"


def canvas_mask(size: tuple[int, int]) -> Image.Image:
    return Image.new("L", size, 0)


def poly_mask(size: tuple[int, int], points: list[tuple[int, int]], value: int = 255) -> Image.Image:
    mask = canvas_mask(size)
    ImageDraw.Draw(mask).polygon(points, fill=value)
    return mask


def ellipse_mask(size: tuple[int, int], box: tuple[int, int, int, int], value: int = 255) -> Image.Image:
    mask = canvas_mask(size)
    ImageDraw.Draw(mask).ellipse(box, fill=value)
    return mask


def rect_mask(size: tuple[int, int], box: tuple[int, int, int, int], value: int = 255) -> Image.Image:
    mask = canvas_mask(size)
    ImageDraw.Draw(mask).rectangle(box, fill=value)
    return mask


def union_masks(size: tuple[int, int], masks: list[Image.Image]) -> Image.Image:
    out = canvas_mask(size)
    for mask in masks:
        out = ImageChops_lighter(out, mask)
    return out


def ImageChops_lighter(a: Image.Image, b: Image.Image) -> Image.Image:
    return Image.fromarray(np.maximum(np.asarray(a), np.asarray(b)).astype(np.uint8), "L")


def subtract_mask(a: Image.Image, b: Image.Image) -> Image.Image:
    return Image.fromarray(np.clip(np.asarray(a, dtype=np.int16) - np.asarray(b, dtype=np.int16), 0, 255).astype(np.uint8), "L")


def intersect_mask(a: Image.Image, b: Image.Image) -> Image.Image:
    return Image.fromarray(np.minimum(np.asarray(a), np.asarray(b)).astype(np.uint8), "L")


def threshold_mask(arr: np.ndarray, expr: np.ndarray) -> Image.Image:
    return Image.fromarray(np.where(expr, 255, 0).astype(np.uint8), "L")


def soften(mask: Image.Image, radius: float = 2.0, expand: int = 1) -> Image.Image:
    if expand:
        mask = mask.filter(ImageFilter.MaxFilter(expand * 2 + 1))
    return mask.filter(ImageFilter.GaussianBlur(radius))


def apply_alpha(rgb: Image.Image, mask: Image.Image) -> Image.Image:
    layer = rgb.convert("RGBA")
    layer.putalpha(mask)
    return layer


def normalized_fill(rgb: Image.Image, hole_mask: Image.Image) -> Image.Image:
    """Simple content-aware style fill using multiscale normalized blurs."""
    source = np.asarray(rgb).astype(np.float32)
    holes = np.asarray(hole_mask) > 0
    valid = (~holes).astype(np.float32)
    filled = source.copy()

    # Feather the hole edge before filling so the generated base stays painterly.
    valid_img = Image.fromarray((valid * 255).astype(np.uint8), "L")
    radii = [3, 7, 15, 31, 63, 127, 191]
    remaining = holes.copy()

    for radius in radii:
        weight = np.asarray(valid_img.filter(ImageFilter.GaussianBlur(radius))).astype(np.float32) / 255.0
        channels = []
        for c in range(3):
            weighted = Image.fromarray(np.clip(source[:, :, c] * valid, 0, 255).astype(np.uint8), "L")
            channels.append(np.asarray(weighted.filter(ImageFilter.GaussianBlur(radius))).astype(np.float32))
        denom = np.maximum(weight, 1e-3)
        candidate = np.stack([ch / denom for ch in channels], axis=2)
        use = remaining & (weight > 0.01)
        filled[use] = candidate[use]
        remaining = remaining & ~use

    if remaining.any():
        mean_color = source[~holes].mean(axis=0)
        filled[remaining] = mean_color

    fill_img = Image.fromarray(np.clip(filled, 0, 255).astype(np.uint8), "RGB")
    fill_img = fill_img.filter(ImageFilter.GaussianBlur(0.7))
    original = rgb.convert("RGB")
    blend_mask = hole_mask.filter(ImageFilter.GaussianBlur(8))
    return Image.composite(fill_img, original, blend_mask)


def pascal_name(name: str) -> bytes:
    raw = name.encode("macroman", errors="replace")[:255]
    data = bytes([len(raw)]) + raw
    while len(data) % 4:
        data += b"\0"
    return data


def pack_section(payload: bytes) -> bytes:
    if len(payload) % 2:
        payload += b"\0"
    return struct.pack(">I", len(payload)) + payload


def write_psd(path: Path, composite: Image.Image, layers: list[tuple[str, Image.Image]]) -> None:
    """Write a simple 8-bit RGB PSD with full-canvas RGBA layers."""
    composite_rgb = composite.convert("RGB")
    width, height = composite_rgb.size
    header = (
        b"8BPS"
        + struct.pack(">H", 1)
        + b"\0" * 6
        + struct.pack(">HIIHH", 3, height, width, 8, 3)
    )

    layer_records = bytearray()
    channel_payloads: list[bytes] = []
    for name, layer in layers:
        rgba = np.asarray(layer.convert("RGBA"))
        channels = [rgba[:, :, 0], rgba[:, :, 1], rgba[:, :, 2], rgba[:, :, 3]]
        lengths = [2 + width * height for _ in channels]
        layer_records += struct.pack(">iiiiH", 0, 0, height, width, 4)
        for channel_id, length in zip([0, 1, 2, -1], lengths):
            layer_records += struct.pack(">hI", channel_id, length)
        layer_records += b"8BIM" + b"norm"
        layer_records += struct.pack(">BBBB", 255, 0, 8, 0)
        extra = struct.pack(">I", 0) + struct.pack(">I", 0) + pascal_name(name)
        layer_records += struct.pack(">I", len(extra)) + extra
        for channel in channels:
            channel_payloads.append(struct.pack(">H", 0) + channel.astype(np.uint8).tobytes(order="C"))

    layer_info = struct.pack(">h", len(layers)) + bytes(layer_records) + b"".join(channel_payloads)
    if len(layer_info) % 2:
        layer_info += b"\0"
    layer_mask_info = struct.pack(">I", len(layer_info)) + layer_info + struct.pack(">I", 0)
    layer_and_mask_section = pack_section(layer_mask_info)

    comp_arr = np.asarray(composite_rgb)
    image_data = struct.pack(">H", 0) + comp_arr[:, :, 0].tobytes() + comp_arr[:, :, 1].tobytes() + comp_arr[:, :, 2].tobytes()

    tmp_path = path.with_suffix(path.suffix + ".tmp")
    with tmp_path.open("wb") as f:
        f.write(header)
        f.write(struct.pack(">I", 0))
        f.write(struct.pack(">I", 0))
        f.write(layer_and_mask_section)
        f.write(image_data)
    tmp_path.replace(path)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    layers_dir = OUT / "png_layers"
    layers_dir.mkdir(parents=True, exist_ok=True)

    rgb = Image.open(SRC).convert("RGB")
    size = rgb.size
    width, height = size
    arr = np.asarray(rgb)
    r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
    y_grid, x_grid = np.mgrid[0:height, 0:width]

    warm = (r > g * 0.95) & (g > b * 0.72) & (r > 125)
    green = (g > r * 0.78) & (g > b * 0.88) & (g > 85)
    yellow_flower = (r > 175) & (g > 135) & (b < 125)
    blue_white = ((b > 135) & (g > 110) & (r < 235) & (b >= r * 0.82)) | ((r > 205) & (g > 205) & (b > 195))
    dark_green = green & (r < 140) & (g < 160) & (b < 150)

    sign_region = union_masks(size, [
        poly_mask(size, [(820, 1405), (1445, 1285), (1535, 1425), (1435, 1615), (865, 1665)]),
        poly_mask(size, [(795, 1740), (1475, 1810), (1535, 1950), (1425, 2105), (815, 2020), (755, 1905)]),
        poly_mask(size, [(1080, 1320), (1195, 1305), (1235, 2727), (1088, 2727)]),
    ])
    sign_color = threshold_mask(arr, warm & (x_grid > 720) & (y_grid > 1180) & (y_grid < 2727))
    sign_mask = soften(intersect_mask(sign_region.filter(ImageFilter.MaxFilter(15)), sign_color.filter(ImageFilter.MaxFilter(23))), 2.0, 2)

    building_region = union_masks(size, [
        poly_mask(size, [(-50, 0), (455, 0), (600, 850), (520, 1160), (-50, 1440)]),
        poly_mask(size, [(255, 410), (675, 240), (910, 420), (910, 955), (555, 980), (365, 760)]),
        poly_mask(size, [(-40, 790), (1050, 915), (1045, 1010), (-40, 1035)]),
        poly_mask(size, [(345, 910), (1015, 955), (990, 1565), (360, 1595), (330, 1210)]),
        poly_mask(size, [(385, 0), (540, 0), (555, 460), (310, 540)]),
    ])
    building_color = threshold_mask(arr, ((r > 145) & (g > 135) & (b > 120) & (x_grid < 1080) & (y_grid < 1660)) | ((b > 120) & (r > 110) & (g > 120) & (x_grid < 760) & (y_grid > 780) & (y_grid < 1650)))
    building_mask = soften(intersect_mask(building_region, building_color.filter(ImageFilter.MaxFilter(29))), 2.0, 1)

    windows = union_masks(size, [
        ellipse_mask(size, (36, 215, 242, 660)),
        ellipse_mask(size, (-30, 935, 115, 1305)),
        ellipse_mask(size, (105, 940, 285, 1315)),
        ellipse_mask(size, (642, 525, 835, 955)),
        rect_mask(size, (482, 1145, 665, 1585)),
        rect_mask(size, (650, 1115, 790, 1545)),
    ])
    window_color = threshold_mask(arr, (b > 120) & (r > 90) & (g > 100) & (x_grid < 900) & (y_grid < 1650))
    doors_windows_mask = soften(intersect_mask(windows.filter(ImageFilter.MaxFilter(11)), window_color.filter(ImageFilter.MaxFilter(17))), 1.6, 1)
    building_body_mask = subtract_mask(building_mask, doors_windows_mask.filter(ImageFilter.GaussianBlur(5)))

    top_tree_region = poly_mask(size, [(680, 0), (1536, 0), (1536, 700), (1275, 715), (940, 585), (760, 335)])
    top_tree_mask = soften(intersect_mask(top_tree_region, threshold_mask(arr, (green | yellow_flower) & (y_grid < 760)).filter(ImageFilter.MaxFilter(19))), 2.5, 1)

    right_tree_region = poly_mask(size, [(915, 720), (1536, 760), (1536, 1830), (950, 1700), (835, 1200)])
    right_tree_mask = soften(intersect_mask(right_tree_region, threshold_mask(arr, green & (x_grid > 720) & (y_grid > 650) & (y_grid < 1850)).filter(ImageFilter.MaxFilter(21))), 3.0, 1)

    left_bush_region = poly_mask(size, [(-20, 610), (620, 675), (665, 1785), (-20, 1870)])
    left_bush_color = threshold_mask(arr, (green | yellow_flower) & (x_grid < 735) & (y_grid > 590) & (y_grid < 1870))
    left_bush_mask = soften(intersect_mask(left_bush_region, left_bush_color.filter(ImageFilter.MaxFilter(23))), 2.2, 1)

    foreground_region = union_masks(size, [
        poly_mask(size, [(0, 1765), (460, 1840), (595, 2727), (0, 2727)]),
        poly_mask(size, [(940, 1785), (1536, 1645), (1536, 2727), (825, 2727)]),
        poly_mask(size, [(430, 2315), (890, 2270), (900, 2727), (320, 2727)]),
    ])
    foreground_leaf = green & (r < 150) & (g < 175) & (b < 150) & ((x_grid < 560) | (x_grid > 880) | (y_grid > 2180))
    foreground_color = threshold_mask(arr, (yellow_flower | foreground_leaf) & (y_grid > 1625))
    foreground_flowers_mask = soften(intersect_mask(foreground_region, foreground_color.filter(ImageFilter.MaxFilter(27))), 2.2, 1)

    lawn_region = poly_mask(size, [(0, 1535), (535, 1505), (1040, 1585), (1536, 1530), (1536, 2460), (0, 2440)])
    lawn_color = threshold_mask(arr, (green | ((r > 145) & (g > 145) & (b < 145))) & (y_grid > 1420))
    lawn_mask = soften(intersect_mask(lawn_region, lawn_color.filter(ImageFilter.MaxFilter(21))), 4.0, 1)
    lawn_mask = subtract_mask(lawn_mask, sign_mask.filter(ImageFilter.GaussianBlur(8)))
    lawn_mask = subtract_mask(lawn_mask, foreground_flowers_mask.filter(ImageFilter.GaussianBlur(8)))

    path_region = union_masks(size, [
        poly_mask(size, [(515, 1455), (760, 1510), (705, 1735), (430, 1780), (395, 1665)]),
        poly_mask(size, [(475, 1610), (760, 1605), (700, 1685), (445, 1698)]),
    ])
    path_color = threshold_mask(arr, (r > 145) & (g > 130) & (b > 100) & (np.abs(r.astype(int) - b.astype(int)) < 85))
    path_mask = soften(intersect_mask(path_region, path_color.filter(ImageFilter.MaxFilter(23))), 2.5, 1)

    shadow_region = poly_mask(size, [(25, 1670), (1360, 1570), (1495, 2220), (0, 2295)])
    shadow_mask = soften(intersect_mask(shadow_region, threshold_mask(arr, dark_green & (y_grid > 1500)).filter(ImageFilter.MaxFilter(31))), 7.0, 1)
    shadow_mask = subtract_mask(shadow_mask, sign_mask.filter(ImageFilter.GaussianBlur(10)))

    all_foreground_for_sky = union_masks(size, [
        building_mask, doors_windows_mask, sign_mask, top_tree_mask, right_tree_mask, left_bush_mask, foreground_flowers_mask
    ])
    sky_region = poly_mask(size, [(0, 0), (1536, 0), (1536, 1320), (1110, 1175), (790, 790), (510, 780), (330, 620), (0, 710)])
    sky_mask = soften(intersect_mask(sky_region, threshold_mask(arr, blue_white & (y_grid < 1320)).filter(ImageFilter.MaxFilter(23))), 3.0, 1)
    sky_mask = subtract_mask(sky_mask, all_foreground_for_sky.filter(ImageFilter.GaussianBlur(6)))

    layer_specs = [
        ("01_sky_clouds", "天空和云", sky_mask),
        ("02_building_body_roof_porch", "建筑主体/屋顶/门廊", building_body_mask),
        ("03_doors_windows", "门窗", doors_windows_mask),
        ("04_tree_canopy_top_right", "右上树冠", top_tree_mask),
        ("05_background_trees_bushes", "远景树木灌木", right_tree_mask),
        ("06_left_rose_bush", "左侧花丛", left_bush_mask),
        ("07_lawn_grass", "草地", lawn_mask),
        ("08_path_steps", "道路和台阶", path_mask),
        ("09_grass_shadows", "草地阴影", shadow_mask),
        ("10_wooden_sign_text", "木牌和文字", sign_mask),
        ("11_foreground_roses_plants", "前景玫瑰和叶片", foreground_flowers_mask),
    ]

    removal = union_masks(size, [spec[2] for spec in layer_specs[1:]])
    removal = removal.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(3))
    background = normalized_fill(rgb, removal)
    background_path = OUT / "00_content_filled_background.png"
    background.save(background_path)

    exported_layers: list[tuple[str, Image.Image]] = []
    manifest_layers = [{
        "file": background_path.name,
        "name": "00_content_filled_background",
        "zh_name": "内容识别补全背景",
        "transparent": False,
        "notes": "Background with the movable foreground elements filled in."
    }]

    for filename, zh_name, mask in layer_specs:
        layer = apply_alpha(rgb, mask)
        path = layers_dir / f"{filename}.png"
        layer.save(path)
        exported_layers.append((filename, layer))
        manifest_layers.append({
            "file": f"png_layers/{filename}.png",
            "name": filename,
            "zh_name": zh_name,
            "transparent": True,
            "canvas_size": [width, height],
        })

    preview = background.convert("RGBA")
    # Paint broad base layers first and foreground/sign last.
    order = [
        "01_sky_clouds",
        "07_lawn_grass",
        "08_path_steps",
        "09_grass_shadows",
        "02_building_body_roof_porch",
        "03_doors_windows",
        "05_background_trees_bushes",
        "04_tree_canopy_top_right",
        "06_left_rose_bush",
        "10_wooden_sign_text",
        "11_foreground_roses_plants",
    ]
    by_name = dict(exported_layers)
    psd_layers = [("00_content_filled_background", background.convert("RGBA"))]
    for name in order:
        preview.alpha_composite(by_name[name])
        psd_layers.append((name, by_name[name]))

    preview_path = OUT / "composite_preview.png"
    preview.save(preview_path)

    original_preview = rgb.copy()
    compare = Image.new("RGB", (width * 2, height), "white")
    compare.paste(original_preview, (0, 0))
    compare.paste(preview.convert("RGB"), (width, 0))
    compare = compare.resize((900, int(900 * height / (width * 2))), Image.Resampling.LANCZOS)
    compare_path = OUT / "comparison_original_vs_layers.jpg"
    compare.save(compare_path, quality=92)

    psd_path = OUT / "segmented_scene_layers.psd"
    write_psd(psd_path, preview.convert("RGB"), psd_layers)

    manifest = {
        "source": str(SRC),
        "output_dir": str(OUT),
        "canvas_size": [width, height],
        "outputs": {
            "psd": psd_path.name,
            "background": background_path.name,
            "preview": preview_path.name,
            "comparison": compare_path.name,
        },
        "layers": manifest_layers,
        "notes": [
            "All transparent PNG layers keep the original canvas size for direct Photoshop import and movement.",
            "The PSD is a simple 8-bit RGB PSD with full-canvas RGBA layers and English layer names.",
            "Masks are generated from local region and color rules, then feathered to preserve the watercolor edge style.",
        ],
    }
    (OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

    # A compact visual sheet of layer alphas for quick QA.
    thumbs = []
    for name, layer in psd_layers:
        alpha = layer.getchannel("A")
        thumb = ImageOps.autocontrast(alpha).convert("RGB")
        thumb.thumbnail((220, 390), Image.Resampling.LANCZOS)
        tile = Image.new("RGB", (240, 430), "white")
        tile.paste(thumb, ((240 - thumb.width) // 2, 10))
        draw = ImageDraw.Draw(tile)
        draw.text((10, 405), name[:30], fill=(0, 0, 0))
        thumbs.append(tile)
    cols = 4
    rows = math.ceil(len(thumbs) / cols)
    sheet = Image.new("RGB", (cols * 240, rows * 430), "white")
    for idx, thumb in enumerate(thumbs):
        sheet.paste(thumb, ((idx % cols) * 240, (idx // cols) * 430))
    sheet.save(OUT / "layer_alpha_contact_sheet.jpg", quality=92)

    print(json.dumps(manifest["outputs"], ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
