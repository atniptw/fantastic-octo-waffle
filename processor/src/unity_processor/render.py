from __future__ import annotations

import hashlib
from io import BytesIO
import math
from pathlib import Path
import tarfile
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


def _parse_obj_mesh(
    obj_text: str,
) -> tuple[
    list[tuple[float, float, float]],
    list[tuple[float, float]],
    list[tuple[int, int, int]],
    list[tuple[int, int, int] | None],
]:
    vertices: list[tuple[float, float, float]] = []
    uvs: list[tuple[float, float]] = []
    faces: list[tuple[int, int, int]] = []
    face_uvs: list[tuple[int, int, int] | None] = []

    for line in obj_text.splitlines():
        if line.startswith("v "):
            parts = line.split()
            if len(parts) >= 4:
                vertices.append((float(parts[1]), float(parts[2]), float(parts[3])))
            continue

        if line.startswith("vt "):
            parts = line.split()
            if len(parts) >= 3:
                uvs.append((float(parts[1]), float(parts[2])))
            continue

        if not line.startswith("f "):
            continue

        indices: list[int] = []
        uv_indices: list[int] = []
        for token in line.split()[1:]:
            chunks = token.split("/")
            base = chunks[0]
            if not base:
                continue
            idx = int(base)
            if idx > 0:
                indices.append(idx - 1)
            uv_idx = -1
            if len(chunks) > 1 and chunks[1]:
                parsed = int(chunks[1])
                if parsed > 0:
                    uv_idx = parsed - 1
            uv_indices.append(uv_idx)

        for i in range(1, len(indices) - 1):
            faces.append((indices[0], indices[i], indices[i + 1]))
            if len(uv_indices) == len(indices):
                i0, i1, i2 = uv_indices[0], uv_indices[i], uv_indices[i + 1]
                if i0 >= 0 and i1 >= 0 and i2 >= 0:
                    face_uvs.append((i0, i1, i2))
                else:
                    face_uvs.append(None)
            else:
                face_uvs.append(None)

    return vertices, uvs, faces, face_uvs


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


def _to_rgb(color: list[float] | None) -> tuple[int, int, int]:
    if not color or len(color) < 3:
        return (155, 155, 155)
    return (
        max(0, min(255, int(color[0] * 255))),
        max(0, min(255, int(color[1] * 255))),
        max(0, min(255, int(color[2] * 255))),
    )


def _sample_texture(texture: Image.Image, u: float, v: float) -> tuple[int, int, int]:
    width, height = texture.size
    if width <= 0 or height <= 0:
        return (160, 160, 160)

    # Wrap UVs for tiled materials and flip V to image space.
    uu = u % 1.0
    vv = 1.0 - (v % 1.0)

    x = uu * (width - 1)
    y = vv * (height - 1)
    x0 = int(math.floor(x))
    y0 = int(math.floor(y))
    x1 = min(width - 1, x0 + 1)
    y1 = min(height - 1, y0 + 1)
    tx = x - x0
    ty = y - y0

    c00 = texture.getpixel((x0, y0))
    c10 = texture.getpixel((x1, y0))
    c01 = texture.getpixel((x0, y1))
    c11 = texture.getpixel((x1, y1))

    def lerp(a: float, b: float, t: float) -> float:
        return a + (b - a) * t

    rgb = []
    for channel in range(3):
        r0 = lerp(c00[channel], c10[channel], tx)
        r1 = lerp(c01[channel], c11[channel], tx)
        rgb.append(int(max(0, min(255, lerp(r0, r1, ty)))))
    return (rgb[0], rgb[1], rgb[2])


def _edge(ax: float, ay: float, bx: float, by: float, px: float, py: float) -> float:
    return (px - ax) * (by - ay) - (py - ay) * (bx - ax)


def _resolve_local_texture(env: object) -> tuple[Image.Image | None, str | None]:
    texture_objects = [o for o in env.objects if getattr(o.type, "name", "") == "Texture2D"]
    for texture_obj in texture_objects:
        try:
            texture = texture_obj.read()
            image = texture.image.convert("RGB")
            name = getattr(texture, "m_Name", "") or None
            return image, name
        except Exception:
            continue
    return None, None


def _load_dependency_texture_cache(parsed: dict) -> dict[str, Image.Image]:
    dep_path = parsed.get("dependency_unitypackage")
    summary = parsed.get("dependency_summary") or {}
    asset_members = summary.get("texture_asset_members") or {}
    if not dep_path or not asset_members:
        return {}

    cache: dict[str, Image.Image] = {}
    try:
        with tarfile.open(dep_path, "r:*") as archive:
            for texture_path, member_name in asset_members.items():
                extracted = archive.extractfile(member_name)
                if extracted is None:
                    continue
                raw = extracted.read()
                try:
                    image = Image.open(BytesIO(raw)).convert("RGB")
                    cache[texture_path] = image
                except Exception:
                    continue
    except (tarfile.TarError, OSError):
        return {}

    return cache


def _render_mesh_png(
    payload: bytes,
    target: Path,
    base_color: tuple[int, int, int],
    dependency_texture: Image.Image | None = None,
) -> tuple[bool, str | None, str]:
    try:
        env = UnityPy.load(payload)
    except Exception as exc:  # pragma: no cover - runtime data dependent
        return False, f"UnityPy load failed during render: {exc}", "none"

    mesh_objects = [o for o in env.objects if getattr(o.type, "name", "") == "Mesh"]
    if not mesh_objects:
        return False, "No Mesh object found for render.", "none"

    try:
        obj_text = mesh_objects[0].read().export()
    except Exception as exc:  # pragma: no cover - runtime data dependent
        return False, f"Mesh export failed during render: {exc}", "none"

    if not isinstance(obj_text, str) or not obj_text.strip():
        return False, "Mesh export returned empty OBJ data.", "none"

    vertices, uvs, faces, face_uvs = _parse_obj_mesh(obj_text)
    if not vertices or not faces:
        return False, "OBJ data does not contain renderable faces.", "none"

    local_texture, _ = _resolve_local_texture(env)
    texture = local_texture or dependency_texture
    texture_source = "local" if local_texture is not None else ("dependency" if dependency_texture is not None else "none")

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

    tris: list[
        tuple[
            float,
            list[tuple[float, float]],
            tuple[int, int, int],
            tuple[tuple[float, float], tuple[float, float], tuple[float, float]] | None,
        ]
    ] = []
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
        shade_color = (
            max(20, min(255, int(base_color[0] * shade))),
            max(20, min(255, int(base_color[1] * shade))),
            max(20, min(255, int(base_color[2] * shade))),
        )

        s0 = screen[i0]
        s1 = screen[i1]
        s2 = screen[i2]
        depth = (s0[2] + s1[2] + s2[2]) / 3.0
        uv_pack = None
        uv_idx = face_uvs[len(tris)] if len(face_uvs) > len(tris) else None
        if uv_idx is not None and all(0 <= idx < len(uvs) for idx in uv_idx):
            uv_pack = (uvs[uv_idx[0]], uvs[uv_idx[1]], uvs[uv_idx[2]])
        tris.append((depth, [(s0[0], s0[1]), (s1[0], s1[1]), (s2[0], s2[1])], shade_color, uv_pack))

    if not tris:
        return False, "No triangles available after projection.", "none"

    image = Image.new("RGB", (size, size), color=(42, 42, 42))
    draw = ImageDraw.Draw(image)
    depth_buffer = [[float("inf")] * size for _ in range(size)]
    tris.sort(key=lambda t: t[0])
    textured_used = False
    for depth, points, shade, uv_pack in tris:
        if texture is None or uv_pack is None:
            draw.polygon(points, fill=shade, outline=(28, 28, 28))
            continue

        textured_used = True
        (x0, y0), (x1, y1), (x2, y2) = points
        (uv0, uv1, uv2) = uv_pack

        min_px = max(0, int(math.floor(min(x0, x1, x2))))
        max_px = min(size - 1, int(math.ceil(max(x0, x1, x2))))
        min_py = max(0, int(math.floor(min(y0, y1, y2))))
        max_py = min(size - 1, int(math.ceil(max(y0, y1, y2))))

        area = _edge(x0, y0, x1, y1, x2, y2)
        if area == 0:
            continue

        for py in range(min_py, max_py + 1):
            for px in range(min_px, max_px + 1):
                cx = px + 0.5
                cy = py + 0.5
                w0 = _edge(x1, y1, x2, y2, cx, cy)
                w1 = _edge(x2, y2, x0, y0, cx, cy)
                w2 = _edge(x0, y0, x1, y1, cx, cy)
                if area > 0:
                    inside = (w0 >= 0) and (w1 >= 0) and (w2 >= 0)
                else:
                    inside = (w0 <= 0) and (w1 <= 0) and (w2 <= 0)
                if not inside:
                    continue

                if depth >= depth_buffer[py][px]:
                    continue
                depth_buffer[py][px] = depth

                inv_area = 1.0 / area
                b0 = w0 * inv_area
                b1 = w1 * inv_area
                b2 = w2 * inv_area
                u = b0 * uv0[0] + b1 * uv1[0] + b2 * uv2[0]
                v = b0 * uv0[1] + b1 * uv1[1] + b2 * uv2[1]
                tex_color = _sample_texture(texture, u, v)
                lit = (
                    max(0, min(255, int(tex_color[0] * (shade[0] / 255.0)))),
                    max(0, min(255, int(tex_color[1] * (shade[1] / 255.0)))),
                    max(0, min(255, int(tex_color[2] * (shade[2] / 255.0)))),
                )
                image.putpixel((px, py), lit)

    if not textured_used:
        texture_source = "none"

    draw.rectangle((2, 2, size - 2, size - 2), outline=(156, 156, 156), width=2)
    image.save(target, format="PNG")
    return True, None, texture_source


def render_preview_images(parsed: dict, output_dir: Path) -> list[dict]:
    """Generate one deterministic flat render per .hhh entry with fallback handling."""
    output_dir.mkdir(parents=True, exist_ok=True)

    mod_name = _safe_slug(Path(parsed["input"]).stem)
    results: list[dict] = []
    dependency_texture_cache = _load_dependency_texture_cache(parsed)

    source_zip = Path(parsed["input"])
    with ZipFile(source_zip, "r") as archive:
        for asset in parsed.get("hhh_assets", []):
            entry = asset["hhh_entry"]
            digest = _entry_hash(entry)
            file_name = f"{mod_name}-{digest}.png"
            target = output_dir / file_name
            mesh_info = asset.get("primary_mesh") or {}
            color = _to_rgb(asset.get("primary_material_color"))

            item_warnings = list(asset.get("warnings", []))
            status = "rendered"
            render_mode = "flat-color"
            texture_source = "none"

            try:
                payload = archive.read(entry)
                dependency_texture = None
                for texture_path in asset.get("dependency_texture_candidates", []):
                    if texture_path in dependency_texture_cache:
                        dependency_texture = dependency_texture_cache[texture_path]
                        break

                rendered, render_warning, used_texture_source = _render_mesh_png(
                    payload,
                    target,
                    color,
                    dependency_texture=dependency_texture,
                )
                if not rendered:
                    status = "rendered-fallback"
                    render_mode = "fallback"
                    subtitle = mesh_info.get("name", "No mesh")
                    details = f"v={mesh_info.get('vertex_count', 0)} tri={mesh_info.get('triangle_count', 0)}"
                    _write_placeholder_png(target, Path(entry).name, subtitle, details)
                    if render_warning:
                        item_warnings.append(render_warning)
                    item_warnings.append("Rendered using placeholder fallback for this asset.")
                else:
                    texture_source = used_texture_source
                    if used_texture_source in {"local", "dependency"}:
                        render_mode = "textured"
                        item_warnings.append(
                            f"Textured mesh render generated using {used_texture_source} texture source."
                        )
                    else:
                        item_warnings.append("Flat-color mesh render generated from primary material color.")
            except KeyError:
                status = "rendered-fallback"
                render_mode = "fallback"
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
                    "render_mode": render_mode,
                    "texture_source": texture_source,
                    "primary_material_color": asset.get("primary_material_color"),
                    "status": status,
                    "warnings": item_warnings,
                }
            )

    return results
