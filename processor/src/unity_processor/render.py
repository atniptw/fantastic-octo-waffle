from __future__ import annotations

import hashlib
import math
from pathlib import Path
from zipfile import ZipFile

import UnityPy

from PIL import Image, ImageDraw


def _safe_slug(value: str) -> str:
    keep = []
    for ch in value.lower():
        keep.append(ch if ch.isalnum() else "-")
    return "".join(keep).strip("-") or "mod"


def _entry_hash(entry_path: str) -> str:
    return hashlib.sha1(entry_path.encode("utf-8")).hexdigest()[:10]


def _write_placeholder_png(target: Path, title: str, subtitle: str, details: str) -> None:
    image = Image.new("RGB", (768, 768), color=(58, 58, 58))
    draw = ImageDraw.Draw(image)
    draw.rectangle((24, 24, 744, 744), outline=(180, 180, 180), width=4)
    draw.text((44, 44), "MVP PREVIEW", fill=(220, 220, 220))
    draw.text((44, 88), title, fill=(235, 235, 235))
    draw.text((44, 124), subtitle, fill=(205, 205, 205))
    draw.text((44, 160), details, fill=(185, 185, 185))
    image.save(target, format="PNG")


def _parse_obj_mesh(obj_text: str) -> tuple[list[tuple[float, float, float]], list[tuple[int, int, int]]]:
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int]] = []

    for line in obj_text.splitlines():
        if line.startswith("v "):
            parts = line.split()
            if len(parts) >= 4:
                vertices.append((float(parts[1]), float(parts[2]), float(parts[3])))
            continue

        if not line.startswith("f "):
            continue

        indices: list[int] = []
        for token in line.split()[1:]:
            base = token.split("/")[0]
            if not base:
                continue
            idx = int(base)
            if idx > 0:
                indices.append(idx - 1)

        for i in range(1, len(indices) - 1):
            faces.append((indices[0], indices[i], indices[i + 1]))

    return vertices, faces


def _normalize(v: tuple[float, float, float]) -> tuple[float, float, float]:
    length = math.sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2])
    if length == 0:
        return (0.0, 0.0, 0.0)
    return (v[0] / length, v[1] / length, v[2] / length)


def _cross(a: tuple[float, float, float], b: tuple[float, float, float]) -> tuple[float, float, float]:
    return (
        a[1] * b[2] - a[2] * b[1],
        a[2] * b[0] - a[0] * b[2],
        a[0] * b[1] - a[1] * b[0],
    )


def _dot(a: tuple[float, float, float], b: tuple[float, float, float]) -> float:
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _render_mesh_png(payload: bytes, target: Path) -> tuple[bool, str | None]:
    try:
        env = UnityPy.load(payload)
    except Exception as exc:  # pragma: no cover - runtime data dependent
        return False, f"UnityPy load failed during render: {exc}"

    mesh_objects = [o for o in env.objects if getattr(o.type, "name", "") == "Mesh"]
    if not mesh_objects:
        return False, "No Mesh object found for render."

    try:
        obj_text = mesh_objects[0].read().export()
    except Exception as exc:  # pragma: no cover - runtime data dependent
        return False, f"Mesh export failed during render: {exc}"

    if not isinstance(obj_text, str) or not obj_text.strip():
        return False, "Mesh export returned empty OBJ data."

    vertices, faces = _parse_obj_mesh(obj_text)
    if not vertices or not faces:
        return False, "OBJ data does not contain renderable faces."

    yaw = math.radians(35.0)
    pitch = math.radians(-20.0)
    cosy, siny = math.cos(yaw), math.sin(yaw)
    cosp, sinp = math.cos(pitch), math.sin(pitch)

    transformed: list[tuple[float, float, float]] = []
    proj_2d: list[tuple[float, float]] = []
    for x, y, z in vertices:
        x1 = cosy * x + siny * z
        z1 = -siny * x + cosy * z
        y2 = cosp * y - sinp * z1
        z2 = sinp * y + cosp * z1
        transformed.append((x1, y2, z2))
        proj_2d.append((x1, -y2))

    xs = [p[0] for p in proj_2d]
    ys = [p[1] for p in proj_2d]
    min_x, max_x = min(xs), max(xs)
    min_y, max_y = min(ys), max(ys)

    span_x = max(max_x - min_x, 1e-9)
    span_y = max(max_y - min_y, 1e-9)
    size = 768
    margin = 42
    scale = min((size - 2 * margin) / span_x, (size - 2 * margin) / span_y)
    cx = 0.5 * (min_x + max_x)
    cy = 0.5 * (min_y + max_y)

    screen: list[tuple[float, float, float]] = []
    for (u, v), (_, _, z2) in zip(proj_2d, transformed):
        sx = (u - cx) * scale + size * 0.5
        sy = (v - cy) * scale + size * 0.5
        screen.append((sx, sy, z2))

    tris: list[tuple[float, list[tuple[float, float]], int]] = []
    light_dir = _normalize((0.45, 0.65, 1.0))
    for i0, i1, i2 in faces:
        if i0 >= len(screen) or i1 >= len(screen) or i2 >= len(screen):
            continue

        p0 = transformed[i0]
        p1 = transformed[i1]
        p2 = transformed[i2]
        e1 = (p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2])
        e2 = (p2[0] - p0[0], p2[1] - p0[1], p2[2] - p0[2])
        normal = _normalize(_cross(e1, e2))
        shade = max(0.22, _dot(normal, light_dir))
        shade_color = int(70 + 165 * shade)

        s0 = screen[i0]
        s1 = screen[i1]
        s2 = screen[i2]
        depth = (s0[2] + s1[2] + s2[2]) / 3.0
        tris.append((depth, [(s0[0], s0[1]), (s1[0], s1[1]), (s2[0], s2[1])], shade_color))

    if not tris:
        return False, "No triangles available after projection."

    image = Image.new("RGB", (size, size), color=(42, 42, 42))
    draw = ImageDraw.Draw(image)
    tris.sort(key=lambda t: t[0])
    for _, points, shade in tris:
        draw.polygon(points, fill=(shade, shade, shade), outline=(28, 28, 28))

    draw.rectangle((2, 2, size - 2, size - 2), outline=(156, 156, 156), width=2)
    image.save(target, format="PNG")
    return True, None


def render_preview_images(parsed: dict, output_dir: Path) -> list[dict]:
    """Generate one deterministic flat render per .hhh entry with fallback handling."""
    output_dir.mkdir(parents=True, exist_ok=True)

    mod_name = _safe_slug(Path(parsed["input"]).stem)
    results: list[dict] = []

    source_zip = Path(parsed["input"])
    with ZipFile(source_zip, "r") as archive:
        for asset in parsed.get("hhh_assets", []):
            entry = asset["hhh_entry"]
            digest = _entry_hash(entry)
            file_name = f"{mod_name}-{digest}.png"
            target = output_dir / file_name
            mesh_info = asset.get("primary_mesh") or {}

            item_warnings = list(asset.get("warnings", []))
            status = "rendered"

            try:
                payload = archive.read(entry)
                rendered, render_warning = _render_mesh_png(payload, target)
                if not rendered:
                    status = "rendered-fallback"
                    subtitle = mesh_info.get("name", "No mesh")
                    details = f"v={mesh_info.get('vertex_count', 0)} tri={mesh_info.get('triangle_count', 0)}"
                    _write_placeholder_png(target, Path(entry).name, subtitle, details)
                    if render_warning:
                        item_warnings.append(render_warning)
                    item_warnings.append("Rendered using placeholder fallback for this asset.")
                else:
                    item_warnings.append("Flat-gray mesh render generated from UnityPy mesh export.")
            except KeyError:
                status = "rendered-fallback"
                subtitle = mesh_info.get("name", "No mesh")
                details = "entry missing from zip at render time"
                _write_placeholder_png(target, Path(entry).name, subtitle, details)
                item_warnings.append("Zip entry missing during render; fallback image created.")

            results.append(
                {
                    "hhh_entry": entry,
                    "image": file_name,
                    "mesh": mesh_info,
                    "object_count": asset.get("object_count", 0),
                    "status": status,
                    "warnings": item_warnings,
                }
            )

    return results
