from __future__ import annotations

import json
import math
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


SOURCE = Path(r"C:\Users\wuton\Downloads\图片_20260603131759_38_19.jpg")
OUT_DIR = Path(r"C:\Users\wuton\Downloads\ps_layers_20260603_131759")


def polygon_mask(size: tuple[int, int], points: list[tuple[int, int]]) -> np.ndarray:
    width, height = size
    mask = np.zeros((height, width), dtype=np.uint8)
    cv2.fillPoly(mask, [np.array(points, dtype=np.int32)], 255)
    return mask


def circle_mask(size: tuple[int, int], circles: list[tuple[int, int, int]]) -> np.ndarray:
    width, height = size
    mask = np.zeros((height, width), dtype=np.uint8)
    for x, y, radius in circles:
        cv2.circle(mask, (x, y), radius, 255, -1)
    return mask


def feather(mask: np.ndarray, radius: int = 3) -> np.ndarray:
    if radius <= 0:
        return mask
    kernel = max(3, radius * 2 + 1)
    if kernel % 2 == 0:
        kernel += 1
    blurred = cv2.GaussianBlur(mask, (kernel, kernel), 0)
    return np.clip(blurred, 0, 255).astype(np.uint8)


def hsv_range(rgb: np.ndarray, lower: tuple[int, int, int], upper: tuple[int, int, int]) -> np.ndarray:
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    return cv2.inRange(hsv, np.array(lower, dtype=np.uint8), np.array(upper, dtype=np.uint8))


def save_layer(rgb: np.ndarray, alpha: np.ndarray, path: Path) -> None:
    rgba = np.dstack([rgb, alpha])
    Image.fromarray(rgba, "RGBA").save(path)


def combine(*masks: np.ndarray) -> np.ndarray:
    out = np.zeros_like(masks[0])
    for mask in masks:
        out = np.maximum(out, mask)
    return out


def subtract(mask: np.ndarray, *others: np.ndarray) -> np.ndarray:
    out = mask.copy()
    for other in others:
        out = cv2.subtract(out, other)
    return out


def write_jsx(width: int, height: int, layers: list[dict[str, str]], jsx_path: Path, psd_path: Path) -> None:
    def js_string(value: str) -> str:
        return json.dumps(value.replace("\\", "/"), ensure_ascii=False)

    layer_lines = [
        f'  {{ name: {json.dumps(layer["name"], ensure_ascii=False)}, path: {js_string(layer["path"])} }}'
        for layer in layers
    ]
    jsx = f"""#target photoshop
app.displayDialogs = DialogModes.NO;
var doc = app.documents.add({width}, {height}, 72, "image_split_layers", NewDocumentMode.RGB, DocumentFill.TRANSPARENT);
var layers = [
{",\n".join(layer_lines)}
];

function importLayer(info) {{
  var source = app.open(new File(info.path));
  source.selection.selectAll();
  source.selection.copy();
  source.close(SaveOptions.DONOTSAVECHANGES);
  app.activeDocument = doc;
  var layer = doc.paste();
  layer.name = info.name;
}}

for (var i = 0; i < layers.length; i++) {{
  importLayer(layers[i]);
}}

var outFile = new File({js_string(str(psd_path))});
var opts = new PhotoshopSaveOptions();
opts.layers = true;
opts.alphaChannels = true;
opts.annotations = true;
opts.embedColorProfile = true;
doc.saveAs(outFile, opts, true, Extension.LOWERCASE);
"""
    jsx_path.write_text(jsx, encoding="utf-8-sig")


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    layer_dir = OUT_DIR / "layers"
    layer_dir.mkdir(exist_ok=True)

    image = Image.open(SOURCE).convert("RGB")
    rgb = np.array(image)
    width, height = image.size
    size = (width, height)
    full = np.full((height, width), 255, dtype=np.uint8)

    green = combine(
        hsv_range(rgb, (32, 25, 65), (105, 255, 255)),
        hsv_range(rgb, (18, 35, 90), (35, 255, 255)),
    )
    yellow_flower = hsv_range(rgb, (15, 40, 130), (34, 255, 255))
    blue_sky = hsv_range(rgb, (88, 10, 95), (118, 190, 255))
    dark_brown = cv2.inRange(rgb, np.array([45, 20, 0], dtype=np.uint8), np.array([145, 105, 85], dtype=np.uint8))
    pale_structure = cv2.inRange(rgb, np.array([145, 130, 120], dtype=np.uint8), np.array([255, 255, 255], dtype=np.uint8))

    upper_sign = polygon_mask(size, [(615, 936), (1015, 855), (1165, 936), (1080, 1063), (638, 1100)])
    lower_sign = polygon_mask(size, [(612, 1202), (1068, 1173), (1168, 1264), (1095, 1408), (630, 1415), (540, 1300)])
    sign_board_mask = combine(upper_sign, lower_sign)
    sign_text_mask = cv2.bitwise_and(dark_brown, sign_board_mask)
    upper_text = cv2.bitwise_and(sign_text_mask, upper_sign)
    lower_text = cv2.bitwise_and(sign_text_mask, lower_sign)
    screw_mask = circle_mask(size, [(839, 935, 18), (823, 1037, 19), (872, 1218, 17), (879, 1337, 18), (847, 1398, 17)])
    screw_mask = cv2.bitwise_and(screw_mask, sign_board_mask)

    post = polygon_mask(size, [(785, 858), (885, 862), (870, height), (765, height)])
    post = subtract(post, sign_board_mask)

    sky = polygon_mask(size, [(0, 0), (width, 0), (width, 710), (880, 680), (755, 545), (600, 520), (430, 120), (0, 0)])
    sky = cv2.bitwise_and(combine(blue_sky, cv2.inRange(rgb, np.array([175, 185, 180], dtype=np.uint8), np.array([255, 255, 255], dtype=np.uint8))), sky)

    top_canopy_region = polygon_mask(size, [(575, 0), (width, 0), (width, 710), (1005, 650), (820, 505), (690, 420), (680, 160)])
    top_canopy = cv2.bitwise_and(green, top_canopy_region)

    distant_trees_region = polygon_mask(size, [(610, 535), (width, 455), (width, 1265), (790, 1195), (675, 925), (705, 700)])
    distant_trees = cv2.bitwise_and(green, distant_trees_region)

    house_region = polygon_mask(
        size,
        [
            (0, 0), (520, 0), (642, 395), (795, 675), (780, 917), (660, 1010),
            (628, 1255), (470, 1390), (250, 1350), (0, 1190),
        ],
    )
    house = cv2.bitwise_and(combine(pale_structure, hsv_range(rgb, (105, 5, 70), (160, 145, 255))), house_region)
    house = cv2.dilate(house, np.ones((13, 13), np.uint8), iterations=1)
    house = cv2.bitwise_and(house, house_region)

    roof_region = polygon_mask(size, [(160, 330), (520, 190), (690, 555), (780, 682), (312, 624), (0, 575), (0, 470)])
    roof = cv2.bitwise_and(hsv_range(rgb, (118, 10, 60), (170, 120, 245)), roof_region)
    roof = cv2.dilate(roof, np.ones((9, 9), np.uint8), iterations=1)

    porch_region = polygon_mask(size, [(270, 620), (795, 670), (760, 1055), (622, 1260), (330, 1320), (260, 980)])
    porch = cv2.bitwise_and(combine(pale_structure, hsv_range(rgb, (92, 5, 75), (130, 135, 255))), porch_region)
    porch = cv2.dilate(porch, np.ones((9, 9), np.uint8), iterations=1)

    left_garden_region = polygon_mask(size, [(0, 835), (365, 850), (495, 1135), (450, 1515), (0, 1640)])
    left_garden = cv2.bitwise_and(combine(green, yellow_flower), left_garden_region)

    lawn_region = polygon_mask(size, [(285, 1210), (820, 1145), (1025, 1440), (815, height), (210, height), (0, 1615), (0, 1360)])
    lawn = cv2.bitwise_and(combine(hsv_range(rgb, (25, 20, 90), (78, 255, 255)), green), lawn_region)
    lawn = subtract(lawn, sign_board_mask, post)

    foreground_left_region = polygon_mask(size, [(0, 1415), (440, 1470), (560, height), (0, height)])
    foreground_center_region = polygon_mask(size, [(260, 1720), (715, 1695), (820, height), (240, height)])
    foreground_right_region = polygon_mask(size, [(760, 1390), (width, 1330), (width, height), (700, height)])
    foreground_left = cv2.bitwise_and(combine(green, yellow_flower), foreground_left_region)
    foreground_center = cv2.bitwise_and(combine(green, yellow_flower), foreground_center_region)
    foreground_right = cv2.bitwise_and(combine(green, yellow_flower), foreground_right_region)

    layer_specs = [
        ("00_original_reference", full, "Full original image kept as a locked-looking reference/base layer."),
        ("01_sky_and_clouds", sky, "Blue sky and pale cloud wash."),
        ("02_top_tree_canopy", top_canopy, "Top and upper-right leaf canopy."),
        ("03_distant_right_trees", distant_trees, "Right-side background trees behind the sign."),
        ("04_house_body", house, "Main cottage walls, windows, chimney, and structural light shapes."),
        ("05_roof", roof, "Purple roof areas."),
        ("06_porch_columns_and_door", porch, "Front porch, columns, steps, and door region."),
        ("07_left_garden_bush_and_flowers", left_garden, "Left flower bush beside the house."),
        ("08_lawn", lawn, "Central grass/lawn wash."),
        ("09_wood_post", post, "Vertical wooden sign post."),
        ("10_upper_start_sign_board", upper_sign, "Upper wooden arrow board."),
        ("11_upper_start_sign_text", upper_text, "Dark painted text on upper sign."),
        ("12_upper_and_lower_sign_screws", screw_mask, "Metal screw heads on both signs."),
        ("13_lower_level_sign_board", lower_sign, "Lower wooden arrow board."),
        ("14_lower_level_sign_text", lower_text, "Dark painted text on lower sign."),
        ("15_foreground_left_roses", foreground_left, "Bottom-left rose cluster and leaves."),
        ("16_foreground_center_roses", foreground_center, "Bottom-center rose cluster and leaves."),
        ("17_foreground_right_roses", foreground_right, "Bottom-right rose cluster and leaves."),
    ]

    layers: list[dict[str, str]] = []
    manifest_layers: list[dict[str, str]] = []
    for index, (name, mask, note) in enumerate(layer_specs):
        alpha = feather(mask, 2 if index else 0)
        path = layer_dir / f"{index:02d}_{name}.png"
        save_layer(rgb, alpha, path)
        layers.append({"name": name, "path": str(path)})
        coverage = int(np.count_nonzero(alpha))
        manifest_layers.append(
            {
                "name": name,
                "file": str(path),
                "coverage_pixels": coverage,
                "note": note,
            }
        )

    psd_path = OUT_DIR / "image_split_layers.psd"
    jsx_path = OUT_DIR / "build_photoshop_layers.jsx"
    write_jsx(width, height, layers, jsx_path, psd_path)

    manifest = {
        "source": str(SOURCE),
        "output_dir": str(OUT_DIR),
        "image_size": {"width": width, "height": height},
        "psd": str(psd_path),
        "jsx": str(jsx_path),
        "layers": manifest_layers,
        "method": "OpenCV semantic masks plus hand-defined element regions; output layers are editable cutouts, not original vector/source-art layers.",
    }
    (OUT_DIR / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

    contact_sheet = Image.new("RGB", (width, math.ceil(len(layer_specs) / 3) * (height // 3)), "white")
    thumb_w, thumb_h = width // 3, height // 3
    for i, layer in enumerate(layers):
        thumb = Image.open(layer["path"]).convert("RGBA")
        preview = Image.new("RGBA", image.size, (255, 255, 255, 255))
        preview.alpha_composite(thumb)
        preview = preview.convert("RGB").resize((thumb_w, thumb_h), Image.Resampling.LANCZOS)
        contact_sheet.paste(preview, ((i % 3) * thumb_w, (i // 3) * thumb_h))
    contact_sheet.save(OUT_DIR / "layer_contact_sheet.jpg", quality=92)

    print(json.dumps({"output_dir": str(OUT_DIR), "psd": str(psd_path), "jsx": str(jsx_path)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
