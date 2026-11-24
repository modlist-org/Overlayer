#!/usr/bin/env python3
"""
Pull remade 128x128 icon SVGs from Figma Desktop MCP (or canonicalize local raw SVGs)
into clean flattened path-only SVGs + icons.json manifests for O5Kit and Overlayer.

Usage:
  uv run --with skia-pathops --with skia-python --with pillow \
      python3 tool/pull_figma_icons.py --from-raw /tmp/figma_raw_svgs

  # Or fetch directly from running figma-linux-next MCP at http://127.0.0.1:3845/mcp:
  uv run --with skia-pathops --with skia-python \
      python3 tool/pull_figma_icons.py
"""

from __future__ import annotations

import argparse
import json
import math
import re
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path

import pathops
import skia

# Canonical glyph codepoints (must remain stable across builds)
O5KIT_ICONS: list[tuple[str, int]] = [
    ("Circle", 0xE001),
    ("ToggleCircle", 0xE002),
    ("Triangle", 0xE003),
    ("X", 0xE004),
]

OVERLAYER_ICONS: list[tuple[str, int]] = [
    ("AliBaseline", 0xE100),
    ("AliCapline", 0xE101),
    ("AliCenter", 0xE102),
    ("AliFlush", 0xE103),
    ("AliGeometry", 0xE104),
    ("AliJustified", 0xE105),
    ("AliLeft", 0xE106),
    ("AliMiddle", 0xE107),
    ("AliMidline", 0xE108),
    ("AliTop", 0xE109),
    ("AnchorCorner", 0xE10A),
    ("AnchorSide", 0xE10B),
    ("Book", 0xE10C),
    ("Box", 0xE10D),
    ("Canvas", 0xE10E),
    ("Check", 0xE10F),
    ("Clone", 0xE110),
    ("CodeBlock", 0xE111),
    ("Cube", 0xE112),
    ("DotInCircle", 0xE113),
    ("Download", 0xE114),
    ("Ease", 0xE115),
    ("Expand", 0xE116),
    ("EyeHide", 0xE117),
    ("EyeOpen", 0xE118),
    ("F", 0xE119),
    ("Gear", 0xE11A),
    ("Grid", 0xE11B),
    ("Image", 0xE11C),
    ("LockOff", 0xE11D),
    ("LockOn", 0xE11E),
    ("MagnifyingGlass", 0xE11F),
    ("Monitor", 0xE120),
    ("Ping", 0xE121),
    ("Plus", 0xE122),
    ("Power", 0xE123),
    ("Ruler", 0xE124),
    ("Star", 0xE125),
    ("Text", 0xE126),
    ("TurnArrow", 0xE127),
    ("Upload", 0xE128),
]

FIGMA_FILE_KEY = "WWR1cgkgKEoH0GGuIjWFY5"
FIGMA_MIN_X = 1900.0
_NUM_RE = r"[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?"


def local_tag(tag: str) -> str:
    return tag.split("}", 1)[-1]


def parse_transform(spec: str | None) -> skia.Matrix:
    mat = skia.Matrix()
    if not spec:
        return mat
    for m in re.finditer(r"([a-zA-Z]+)\s*\(([^)]*)\)", spec):
        fn = m.group(1)
        nums = [float(x) for x in re.findall(_NUM_RE, m.group(2))]
        step = skia.Matrix()
        if fn == "translate":
            tx = nums[0] if len(nums) > 0 else 0.0
            ty = nums[1] if len(nums) > 1 else 0.0
            step.setTranslate(tx, ty)
        elif fn == "scale":
            sx = nums[0] if len(nums) > 0 else 1.0
            sy = nums[1] if len(nums) > 1 else sx
            step.setScale(sx, sy)
        elif fn == "rotate":
            deg = nums[0]
            if len(nums) >= 3:
                step.setRotate(deg, nums[1], nums[2])
            else:
                step.setRotate(deg)
        elif fn == "matrix" and len(nums) == 6:
            a, b, c, d, e, f = nums
            step.setAll(a, c, e, b, d, f, 0.0, 0.0, 1.0)
        elif fn == "skewX":
            step.setSkew(math.tan(math.radians(nums[0])), 0.0)
        elif fn == "skewY":
            step.setSkew(0.0, math.tan(math.radians(nums[0])))
        else:
            raise ValueError(f"Unsupported SVG transform: {fn}({m.group(2)})")
        mat = skia.Matrix.Concat(mat, step)
    return mat


def svg_path_to_skpath(d: str) -> skia.Path:
    tokens = re.findall(rf"[MmLlHhVvCcSsQqTtAaZz]|{_NUM_RE}", d)
    p = skia.Path()
    i = 0
    cmd = ""
    cx = cy = sx = sy = 0.0
    prev_c2x = prev_c2y = 0.0
    prev_qx = prev_qy = 0.0
    prev_cmd = ""

    def next_f() -> float:
        nonlocal i
        v = float(tokens[i])
        i += 1
        return v

    def is_num() -> bool:
        return i < len(tokens) and not tokens[i].isalpha()

    while i < len(tokens):
        if tokens[i].isalpha():
            cmd = tokens[i]
            i += 1
            if cmd in ("Z", "z"):
                p.close()
                cx, cy = sx, sy
                prev_cmd = cmd
                continue
        if not cmd:
            break

        if cmd in ("M", "m"):
            x, y = next_f(), next_f()
            if cmd == "m":
                x += cx
                y += cy
            p.moveTo(x, y)
            cx, cy = x, y
            sx, sy = x, y
            prev_cmd = cmd
            cmd = "L" if cmd == "M" else "l"
        elif cmd in ("L", "l"):
            while is_num():
                x, y = next_f(), next_f()
                if cmd == "l":
                    x += cx
                    y += cy
                p.lineTo(x, y)
                cx, cy = x, y
                prev_cmd = cmd
        elif cmd in ("H", "h"):
            while is_num():
                x = next_f() + (cx if cmd == "h" else 0.0)
                p.lineTo(x, cy)
                cx = x
                prev_cmd = cmd
        elif cmd in ("V", "v"):
            while is_num():
                y = next_f() + (cy if cmd == "v" else 0.0)
                p.lineTo(cx, y)
                cy = y
                prev_cmd = cmd
        elif cmd in ("C", "c"):
            while is_num():
                x1, y1, x2, y2, x, y = next_f(), next_f(), next_f(), next_f(), next_f(), next_f()
                if cmd == "c":
                    x1 += cx; y1 += cy; x2 += cx; y2 += cy; x += cx; y += cy
                p.cubicTo(x1, y1, x2, y2, x, y)
                prev_c2x, prev_c2y = x2, y2
                cx, cy = x, y
                prev_cmd = cmd
        elif cmd in ("S", "s"):
            while is_num():
                if prev_cmd in ("C", "c", "S", "s"):
                    x1, y1 = 2 * cx - prev_c2x, 2 * cy - prev_c2y
                else:
                    x1, y1 = cx, cy
                x2, y2, x, y = next_f(), next_f(), next_f(), next_f()
                if cmd == "s":
                    x2 += cx; y2 += cy; x += cx; y += cy
                p.cubicTo(x1, y1, x2, y2, x, y)
                prev_c2x, prev_c2y = x2, y2
                cx, cy = x, y
                prev_cmd = cmd
        elif cmd in ("Q", "q"):
            while is_num():
                x1, y1, x, y = next_f(), next_f(), next_f(), next_f()
                if cmd == "q":
                    x1 += cx; y1 += cy; x += cx; y += cy
                p.quadTo(x1, y1, x, y)
                prev_qx, prev_qy = x1, y1
                cx, cy = x, y
                prev_cmd = cmd
        elif cmd in ("T", "t"):
            while is_num():
                if prev_cmd in ("Q", "q", "T", "t"):
                    x1, y1 = 2 * cx - prev_qx, 2 * cy - prev_qy
                else:
                    x1, y1 = cx, cy
                x, y = next_f(), next_f()
                if cmd == "t":
                    x += cx; y += cy
                p.quadTo(x1, y1, x, y)
                prev_qx, prev_qy = x1, y1
                cx, cy = x, y
                prev_cmd = cmd
        elif cmd in ("A", "a"):
            while is_num():
                rx, ry, rot, large, sweep, x, y = (
                    next_f(), next_f(), next_f(), int(next_f()), int(next_f()), next_f(), next_f()
                )
                if cmd == "a":
                    x += cx; y += cy
                arc_dir = (
                    skia.PathDirection.kCW if sweep else skia.PathDirection.kCCW
                )
                arc_size = (
                    skia.Path.ArcSize.kLarge_ArcSize if large else skia.Path.ArcSize.kSmall_ArcSize
                )
                p.arcTo(rx, ry, rot, arc_size, arc_dir, x, y)
                cx, cy = x, y
                prev_cmd = cmd
        else:
            raise ValueError(f"Unsupported SVG path command: {cmd!r}")
    return p


def element_to_skpath(el: ET.Element) -> skia.Path:
    tag = local_tag(el.tag)
    p = skia.Path()
    if tag == "path":
        p = svg_path_to_skpath(el.attrib.get("d", ""))
    elif tag == "rect":
        x = float(el.attrib.get("x", "0"))
        y = float(el.attrib.get("y", "0"))
        w = float(el.attrib.get("width", "0"))
        h = float(el.attrib.get("height", "0"))
        rx_attr = el.attrib.get("rx")
        ry_attr = el.attrib.get("ry")
        rx = float(rx_attr) if rx_attr is not None else (float(ry_attr) if ry_attr is not None else 0.0)
        ry = float(ry_attr) if ry_attr is not None else rx
        rx = min(max(rx, 0.0), w / 2.0)
        ry = min(max(ry, 0.0), h / 2.0)
        rect = skia.Rect.MakeXYWH(x, y, w, h)
        if rx > 0 or ry > 0:
            p.addRoundRect(rect, rx, ry)
        else:
            p.addRect(rect)
    elif tag == "circle":
        cx = float(el.attrib.get("cx", "0"))
        cy = float(el.attrib.get("cy", "0"))
        r = float(el.attrib.get("r", "0"))
        p.addCircle(cx, cy, r)
    elif tag == "ellipse":
        cx = float(el.attrib.get("cx", "0"))
        cy = float(el.attrib.get("cy", "0"))
        rx = float(el.attrib.get("rx", "0"))
        ry = float(el.attrib.get("ry", "0"))
        p.addOval(skia.Rect.MakeXYWH(cx - rx, cy - ry, 2.0 * rx, 2.0 * ry))
    elif tag in ("polygon", "polyline"):
        pts = [float(v) for v in re.findall(_NUM_RE, el.attrib.get("points", ""))]
        if len(pts) >= 4:
            p.moveTo(pts[0], pts[1])
            for idx in range(2, len(pts) - 1, 2):
                p.lineTo(pts[idx], pts[idx + 1])
            if tag == "polygon":
                p.close()
    else:
        raise ValueError(f"Unsupported SVG shape tag: <{tag}>")

    t = el.attrib.get("transform")
    if t:
        p.transform(parse_transform(t))
    return p


def skpath_to_pathops(skp: skia.Path) -> pathops.Path:
    po = pathops.Path()
    pen = po.getPen()
    it = skia.Path.RawIter(skp)
    closed = True
    while True:
        verb, pts = it.next()
        if verb == skia.Path.kDone_Verb:
            break
        elif verb == skia.Path.kMove_Verb:
            if not closed:
                pen.endPath()
            pen.moveTo((pts[0].x(), pts[0].y()))
            closed = False
        elif verb == skia.Path.kLine_Verb:
            pen.lineTo((pts[1].x(), pts[1].y()))
        elif verb == skia.Path.kQuad_Verb:
            pen.qCurveTo((pts[1].x(), pts[1].y()), (pts[2].x(), pts[2].y()))
        elif verb == skia.Path.kConic_Verb:
            quads = skia.Path.ConvertConicToQuads(pts[0], pts[1], pts[2], it.conicWeight(), 2)
            # quads returns [p0, c0, p1, c1, p2, ...] -> 1 + 2*4 points for pow2=2
            for k in range(1, len(quads), 2):
                pen.qCurveTo((quads[k].x(), quads[k].y()), (quads[k + 1].x(), quads[k + 1].y()))
        elif verb == skia.Path.kCubic_Verb:
            pen.curveTo(
                (pts[1].x(), pts[1].y()),
                (pts[2].x(), pts[2].y()),
                (pts[3].x(), pts[3].y()),
            )
        elif verb == skia.Path.kClose_Verb:
            pen.closePath()
            closed = True
    if not closed:
        pen.closePath()
    return po


def extract_url_id(val: str | None) -> str | None:
    if not val:
        return None
    m = re.match(r"^\s*url\(#([^)]+)\)\s*$", val)
    return m.group(1) if m else None


def po_union(paths: list[pathops.Path]) -> pathops.Path:
    non_empty = [p for p in paths if p.contours]
    if not non_empty:
        return pathops.Path()
    if len(non_empty) == 1:
        return non_empty[0]
    out = pathops.Path()
    pathops.union(non_empty, out.getPen())
    return out


def po_intersect(a: pathops.Path, b: pathops.Path) -> pathops.Path:
    if not a.contours or not b.contours:
        return pathops.Path()
    return pathops.op(a, b, pathops.PathOp.INTERSECTION)


def po_difference(a: pathops.Path, b: pathops.Path) -> pathops.Path:
    if not a.contours:
        return pathops.Path()
    if not b.contours:
        return a
    return pathops.op(a, b, pathops.PathOp.DIFFERENCE)


def flatten_container_shapes(container: ET.Element) -> pathops.Path:
    parts = []
    for child in container:
        tag = local_tag(child.tag)
        if tag in ("path", "rect", "circle", "ellipse", "polygon", "polyline"):
            parts.append(skpath_to_pathops(element_to_skpath(child)))
        elif tag == "g":
            parts.append(flatten_container_shapes(child))
    return po_union(parts)


def collect_defs(root: ET.Element) -> tuple[dict[str, pathops.Path], dict[str, pathops.Path]]:
    clips: dict[str, pathops.Path] = {}
    masks: dict[str, pathops.Path] = {}
    for el in root.iter():
        tag = local_tag(el.tag)
        eid = el.attrib.get("id")
        if not eid:
            continue
        if tag == "clipPath":
            clips[eid] = flatten_container_shapes(el)
        elif tag == "mask":
            masks[eid] = flatten_container_shapes(el)
    return clips, masks


def normalize_grid_svg() -> pathops.Path:
    """12px rounded outer border (r=4) + lighter solid 6px inner 3x3 dividers."""
    frame = skia.Path()
    frame.addRoundRect(skia.Rect.MakeXYWH(0, 0, 128, 128), 4, 4)
    hole = skia.Path()
    hole.addRect(skia.Rect.MakeXYWH(12, 12, 104, 104))

    parts = [po_difference(skpath_to_pathops(frame), skpath_to_pathops(hole))]
    for rect_xywh in [
        (42, 12, 6, 104),
        (80, 12, 6, 104),
        (12, 42, 104, 6),
        (12, 80, 104, 6),
    ]:
        rp = skia.Path()
        rp.addRect(skia.Rect.MakeXYWH(*rect_xywh))
        parts.append(skpath_to_pathops(rp))
    return po_union(parts)


def pathops_to_svg_d(po: pathops.Path) -> str:
    def fmt(v: float) -> str:
        r = round(v)
        if abs(v - r) < 1e-4:
            return str(r)
        s = f"{v:.3f}".rstrip("0").rstrip(".")
        return "0" if s == "-0" else s

    parts: list[str] = []
    for verb, pts in po:
        if verb == pathops.PathVerb.MOVE:
            x, y = pts[0]
            parts.append(f"M{fmt(x)} {fmt(y)}")
        elif verb == pathops.PathVerb.LINE:
            x, y = pts[0]
            parts.append(f"L{fmt(x)} {fmt(y)}")
        elif verb == pathops.PathVerb.QUAD:
            (x1, y1), (x2, y2) = pts
            parts.append(f"Q{fmt(x1)} {fmt(y1)} {fmt(x2)} {fmt(y2)}")
        elif verb == pathops.PathVerb.CUBIC:
            (x1, y1), (x2, y2), (x3, y3) = pts
            parts.append(f"C{fmt(x1)} {fmt(y1)} {fmt(x2)} {fmt(y2)} {fmt(x3)} {fmt(y3)}")
        elif verb == pathops.PathVerb.CLOSE:
            parts.append("Z")
    return "".join(parts)


def canonicalize_svg(name: str, raw_svg_text: str) -> str:
    if name == "Grid":
        final_po = normalize_grid_svg()
    else:
        root = ET.fromstring(raw_svg_text)
        clips, masks = collect_defs(root)

        def walk(node: ET.Element, inherited_clip: pathops.Path | None) -> pathops.Path:
            tag = local_tag(node.tag)
            clip_id = extract_url_id(node.attrib.get("clip-path"))
            cur_clip = inherited_clip
            if clip_id and clip_id in clips:
                c = clips[clip_id]
                cur_clip = c if cur_clip is None else po_intersect(cur_clip, c)

            if tag in ("defs", "clipPath", "mask"):
                return pathops.Path()

            if tag in ("svg", "g"):
                parts = []
                for child in node:
                    child_po = walk(child, cur_clip)
                    if child_po.contours:
                        parts.append(child_po)
                return po_union(parts)

            if tag in ("path", "rect", "circle", "ellipse", "polygon", "polyline"):
                fill_val = (node.attrib.get("fill") or "").strip().lower()
                stroke_val = (node.attrib.get("stroke") or "").strip().lower()
                skp = element_to_skpath(node)
                parts = []

                if fill_val and fill_val != "none":
                    parts.append(skpath_to_pathops(skp))

                if stroke_val and stroke_val != "none":
                    sw = float(node.attrib.get("stroke-width", "1"))
                    paint = skia.Paint()
                    paint.setStyle(skia.Paint.kStroke_Style)
                    paint.setStrokeWidth(sw)
                    paint.setStrokeCap(skia.Paint.kButt_Cap)
                    paint.setStrokeJoin(skia.Paint.kMiter_Join)
                    paint.setStrokeMiter(4.0)
                    stroked = skia.Path()
                    paint.getFillPath(skp, stroked)
                    po_stroke = skpath_to_pathops(stroked)
                    mask_id = extract_url_id(node.attrib.get("mask"))
                    if mask_id and mask_id in masks:
                        po_stroke = po_intersect(po_stroke, masks[mask_id])
                    if po_stroke.contours:
                        parts.append(po_stroke)

                acc = po_union(parts)
                if cur_clip is not None and acc.contours:
                    acc = po_intersect(acc, cur_clip)
                return acc

            return pathops.Path()

        raw_union = walk(root, None)
        box = pathops.Path()
        bpen = box.getPen()
        bpen.moveTo((0, 0))
        bpen.lineTo((128, 0))
        bpen.lineTo((128, 128))
        bpen.lineTo((0, 128))
        bpen.closePath()
        final_po = po_intersect(raw_union, box)

    final_po.simplify(fix_winding=True)
    d = pathops_to_svg_d(final_po)
    return (
        '<svg width="128" height="128" viewBox="0 0 128 128" fill="none" '
        'xmlns="http://www.w3.org/2000/svg">\n'
        f'  <path fill-rule="evenodd" d="{d}" fill="white"/>\n'
        '</svg>\n'
    )


def fetch_figma_raw_svgs(out_dir: Path, mcp_url: str) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)

    def rpc(method: str, params: dict, req_id: int, sid: str | None = None):
        headers = {
            "Content-Type": "application/json",
            "Accept": "application/json, text/event-stream",
        }
        if sid:
            headers["mcp-session-id"] = sid
        req = urllib.request.Request(
            mcp_url,
            data=json.dumps({"jsonrpc": "2.0", "id": req_id, "method": method, "params": params}).encode(),
            headers=headers,
        )
        with urllib.request.urlopen(req, timeout=30) as resp:
            new_sid = resp.headers.get("mcp-session-id", sid)
            body = resp.read().decode("utf-8")
            for line in body.splitlines():
                if line.startswith("data: "):
                    return json.loads(line[6:]), new_sid
            return json.loads(body), new_sid

    _, sid = rpc(
        "initialize",
        {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "pull_figma_icons", "version": "1.0"},
        },
        1,
    )
    meta_resp, _ = rpc(
        "tools/call",
        {"name": "get_metadata", "arguments": {"fileKey": FIGMA_FILE_KEY, "nodeId": "0:1"}},
        2,
        sid,
    )
    meta_xml = meta_resp["result"]["content"][0]["text"]
    root = ET.fromstring(f"<root>{meta_xml}</root>")
    wanted = {n for n, _ in O5KIT_ICONS + OVERLAYER_ICONS}
    node_ids: list[str] = []
    for el in root.iter():
        name = el.attrib.get("name", "")
        x = float(el.attrib.get("x", "0"))
        nid = el.attrib.get("id", "")
        if x >= FIGMA_MIN_X and name in wanted and nid:
            node_ids.append(nid)
    print(f"Matched {len(node_ids)} Figma nodes (expected {len(wanted)})")
    dl_resp, _ = rpc(
        "tools/call",
        {
            "name": "download_assets",
            "arguments": {"fileKey": FIGMA_FILE_KEY, "nodeIds": node_ids, "defaultFormat": "svg"},
        },
        3,
        sid,
    )
    payload = json.loads(dl_resp["result"]["content"][0]["text"])
    for item in payload["assets"]:
        svg_path = out_dir / f"{item['name']}.svg"
        svg_path.write_text(item["svg"], encoding="utf-8")


def write_repo_bundle(
    raw_dir: Path,
    icons_dir: Path,
    specs: list[tuple[str, int]],
    font_name: str,
) -> None:
    icons_dir.mkdir(parents=True, exist_ok=True)
    glyphs_manifest = []
    for name, cp in specs:
        raw_path = raw_dir / f"{name}.svg"
        if not raw_path.exists():
            raise FileNotFoundError(f"Missing raw SVG: {raw_path}")
        canonical_svg = canonicalize_svg(name, raw_path.read_text(encoding="utf-8"))
        out_svg = icons_dir / f"{name}.svg"
        out_svg.write_text(canonical_svg, encoding="utf-8")
        glyphs_manifest.append({
            "name": name,
            "codepoint": f"U+{cp:04X}",
            "svg": f"{name}.svg",
        })
    manifest = {
        "fontName": font_name,
        "unitsPerEm": 1024,
        "ascender": 896,
        "descender": -128,
        "sourceSize": 128,
        "glyphs": glyphs_manifest,
    }
    (icons_dir / "icons.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(specs)} canonical SVGs + icons.json -> {icons_dir}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--from-raw", type=Path, help="Read raw SVGs from directory instead of Figma MCP")
    parser.add_argument("--mcp-url", default="http://127.0.0.1:3845/mcp")
    parser.add_argument("--raw-cache", type=Path, default=Path("/tmp/figma_raw_svgs"))
    args = parser.parse_args()

    raw_dir = args.from_raw or args.raw_cache
    if args.from_raw is None:
        fetch_figma_raw_svgs(raw_dir, args.mcp_url)

    script_dir = Path(__file__).resolve().parent
    overlayer_root = script_dir.parent
    o5kit_root = overlayer_root.parent / "O5Kit"

    write_repo_bundle(
        raw_dir,
        o5kit_root / "src" / "O5Kit" / "Asset" / "Icons",
        O5KIT_ICONS,
        "O5KitIcons",
    )
    write_repo_bundle(
        raw_dir,
        overlayer_root / "Overlayer" / "Resource" / "Icons",
        OVERLAYER_ICONS,
        "OverlayerIcons",
    )


if __name__ == "__main__":
    main()
