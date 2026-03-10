from __future__ import annotations

import hashlib
from io import BytesIO
import logging
import numpy as np
from pathlib import Path
import tarfile
from zipfile import ZipFile

import UnityPy
from PIL import Image
import trimesh
from trimesh.visual.color import ColorVisuals
from trimesh.visual.material import SimpleMaterial
from trimesh.visual.texture import TextureVisuals

LOGGER = logging.getLogger(__name__)


def _safe_slug(value: str) -> str:
    keep = []
    for ch in value.lower():
        keep.append(ch if ch.isalnum() else "-")
    return "".join(keep).strip("-") or "mod"


def _entry_hash(entry_path: str) -> str:
    return hashlib.sha1(entry_path.encode("utf-8")).hexdigest()[:10]


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




def _to_rgba(color: list[float] | None) -> tuple[int, int, int, int]:
    if not color or len(color) < 3:
        return (155, 155, 155, 255)
    alpha = color[3] if len(color) >= 4 else 1.0
    return (
        max(0, min(255, int(color[0] * 255))),
        max(0, min(255, int(color[1] * 255))),
        max(0, min(255, int(color[2] * 255))),
        max(0, min(255, int(alpha * 255))),
    )


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


def _load_primary_mesh(payload: bytes) -> tuple[trimesh.Trimesh | None, str | None]:
    try:
        env = UnityPy.load(payload)
    except Exception as exc:  # pragma: no cover - runtime data dependent
        return None, f"UnityPy load failed during model export: {exc}"

    mesh_objects = [o for o in env.objects if getattr(o.type, "name", "") == "Mesh"]
    if not mesh_objects:
        return None, "No Mesh object found for model export."

    try:
        obj_text = mesh_objects[0].read().export()
    except Exception as exc:  # pragma: no cover - runtime data dependent
        return None, f"Mesh export failed during model export: {exc}"

    if not isinstance(obj_text, str) or not obj_text.strip():
        return None, "Mesh export returned empty OBJ data."

    try:
        mesh_or_scene = trimesh.load(BytesIO(obj_text.encode("utf-8")), file_type="obj", process=False)
    except Exception as exc:  # pragma: no cover - runtime data dependent
        return None, f"OBJ conversion to trimesh failed: {exc}"

    if isinstance(mesh_or_scene, trimesh.Scene):
        if not mesh_or_scene.geometry:
            return None, "OBJ conversion produced an empty scene."
        mesh = trimesh.util.concatenate(tuple(mesh_or_scene.geometry.values()))
    else:
        mesh = mesh_or_scene

    if not isinstance(mesh, trimesh.Trimesh) or mesh.vertices is None or len(mesh.vertices) == 0:
        return None, "Trimesh conversion produced no vertices."

    return mesh, None


def _apply_visuals(
    mesh: trimesh.Trimesh,
    base_color: tuple[int, int, int, int],
    texture: Image.Image | None,
) -> str:
    uv = getattr(mesh.visual, "uv", None)
    if texture is not None and uv is not None and len(uv) == len(mesh.vertices):
        try:
            material = SimpleMaterial(image=texture)
            mesh.visual = TextureVisuals(uv=np.asarray(uv, dtype=float), image=texture, material=material)
            return "textured"
        except Exception as exc:  # pragma: no cover - runtime data dependent
            LOGGER.warning("Texture visual assignment failed, falling back to flat color: %s", exc)

    vertex_count = len(mesh.vertices)
    color_array = np.tile(np.array(base_color, dtype=np.uint8), (vertex_count, 1))
    mesh.visual = ColorVisuals(mesh=mesh, vertex_colors=color_array)
    return "flat-color"


def _export_mesh_glb(
    payload: bytes,
    target: Path,
    base_color: tuple[int, int, int, int],
    dependency_texture: Image.Image | None = None,
) -> tuple[bool, str | None, str, list[list[float]] | None]:
    mesh, load_warning = _load_primary_mesh(payload)
    if mesh is None:
        return False, load_warning, "none", None

    try:
        env = UnityPy.load(payload)
    except Exception as exc:  # pragma: no cover - runtime data dependent
        return False, f"UnityPy reload failed during model export: {exc}", "none", None

    local_texture, _ = _resolve_local_texture(env)
    texture = local_texture or dependency_texture
    texture_source = "local" if local_texture is not None else ("dependency" if dependency_texture is not None else "none")
    render_mode = _apply_visuals(mesh, base_color, texture)
    if render_mode != "textured":
        texture_source = "none"

    bounds_raw = mesh.bounds if hasattr(mesh, "bounds") else None
    bounds: list[list[float]] | None = None
    if bounds_raw is not None and len(bounds_raw) == 2:
        bounds = [bounds_raw[0].tolist(), bounds_raw[1].tolist()]

    try:
        glb_data = mesh.export(file_type="glb")
    except Exception as exc:  # pragma: no cover - runtime data dependent
        return False, f"GLB export failed: {exc}", texture_source, bounds

    if isinstance(glb_data, str):
        glb_data = glb_data.encode("utf-8")

    target.write_bytes(glb_data)
    return True, None, texture_source, bounds


def render_preview_images(parsed: dict, output_dir: Path) -> list[dict]:
    """Generate one deterministic GLB model export per .hhh entry with warnings."""
    output_dir.mkdir(parents=True, exist_ok=True)

    mod_name = _safe_slug(Path(parsed["input"]).stem)
    results: list[dict] = []
    dependency_texture_cache = _load_dependency_texture_cache(parsed)

    source_zip = Path(parsed["input"])
    with ZipFile(source_zip, "r") as archive:
        for asset in parsed.get("hhh_assets", []):
            entry = asset["hhh_entry"]
            digest = _entry_hash(entry)
            file_name = f"{mod_name}-{digest}.glb"
            target = output_dir / file_name
            mesh_info = asset.get("primary_mesh") or {}
            color = _to_rgba(asset.get("primary_material_color"))

            item_warnings = list(asset.get("warnings", []))
            status = "ok"
            model_file: str | None = file_name
            texture_source = "none"
            mesh_bounds: list[list[float]] | None = None

            try:
                payload = archive.read(entry)
                dependency_texture = None
                for texture_path in asset.get("dependency_texture_candidates", []):
                    if texture_path in dependency_texture_cache:
                        dependency_texture = dependency_texture_cache[texture_path]
                        break

                exported, export_warning, used_texture_source, mesh_bounds = _export_mesh_glb(
                    payload,
                    target,
                    color,
                    dependency_texture=dependency_texture,
                )
                if not exported:
                    status = "export-failed"
                    model_file = None
                    if target.exists():
                        target.unlink()
                    if export_warning:
                        item_warnings.append(export_warning)
                    item_warnings.append("Model export failed for this asset.")
                else:
                    texture_source = used_texture_source
                    if used_texture_source in {"local", "dependency"}:
                        item_warnings.append(f"Textured GLB exported using {used_texture_source} texture source.")
                    else:
                        item_warnings.append("Flat-color GLB exported from primary material color.")
            except KeyError:
                status = "export-failed"
                model_file = None
                item_warnings.append("Zip entry missing during model export.")

            results.append(
                {
                    "hhh_entry": entry,
                    "model": model_file,
                    "model_format": "glb",
                    "mesh": mesh_info,
                    "mesh_bounds": mesh_bounds,
                    "object_count": asset.get("object_count", 0),
                    "texture_source": texture_source,
                    "primary_material_color": asset.get("primary_material_color"),
                    "export_status": status,
                    "warnings": item_warnings,
                }
            )

    return results
