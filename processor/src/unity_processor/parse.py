from __future__ import annotations

import tarfile
from pathlib import Path
from zipfile import ZipFile

import UnityPy


def _is_hhh(path: str) -> bool:
    return path.lower().endswith(".hhh")


def _inspect_unitypackage(path: Path) -> dict:
    """Read a unitypackage tar archive and summarize dependency path entries."""
    pathnames: list[str] = []
    texture_paths: list[str] = []
    material_paths: list[str] = []
    texture_asset_members: dict[str, str] = {}
    with tarfile.open(path, "r:*") as archive:
        members = {m.name: m for m in archive.getmembers() if m.isfile()}
        for member in archive.getmembers():
            if not member.isfile() or not member.name.endswith("/pathname"):
                continue

            extracted = archive.extractfile(member)
            if extracted is None:
                continue
            raw = extracted.read().decode("utf-8", errors="replace").strip()
            if raw:
                pathnames.append(raw)
                ext = Path(raw).suffix.lower()
                if ext in {".png", ".jpg", ".jpeg", ".tga", ".dds", ".bmp", ".exr"}:
                    texture_paths.append(raw)
                    root = member.name.rsplit("/", 1)[0]
                    asset_member = f"{root}/asset"
                    if asset_member in members:
                        texture_asset_members[raw] = asset_member
                elif ext == ".mat":
                    material_paths.append(raw)

    texture_name_map: dict[str, str] = {}
    for texture_path in texture_paths:
        stem = Path(texture_path).stem.lower()
        if stem and stem not in texture_name_map:
            texture_name_map[stem] = texture_path

    return {
        "path": str(path),
        "pathname_count": len(pathnames),
        "sample_pathnames": sorted(pathnames)[:5],
        "texture_count": len(texture_paths),
        "material_count": len(material_paths),
        "sample_textures": sorted(texture_paths)[:5],
        "sample_materials": sorted(material_paths)[:5],
        "texture_name_map": texture_name_map,
        "texture_asset_members": texture_asset_members,
    }


def _color_tuple(color: object) -> list[float]:
    return [
        float(getattr(color, "r", 1.0)),
        float(getattr(color, "g", 1.0)),
        float(getattr(color, "b", 1.0)),
        float(getattr(color, "a", 1.0)),
    ]


def _extract_material_data(env: object) -> tuple[list[dict], dict[int, dict], list[float] | None]:
    external_file_map: dict[int, str] = {}
    for _, file_ref in (getattr(env, "files", {}) or {}).items():
        assets_file = getattr(file_ref, "assets_file", None)
        if assets_file is None:
            continue
        externals = list(getattr(assets_file, "externals", []) or [])
        for idx, ext in enumerate(externals, start=1):
            ext_name = getattr(ext, "name", "") or ""
            if ext_name:
                external_file_map[idx] = ext_name

    texture_objects = [o for o in env.objects if getattr(o.type, "name", "") == "Texture2D"]
    local_texture_by_id: dict[int, dict] = {}
    for texture_obj in texture_objects:
        try:
            texture = texture_obj.read()
            local_texture_by_id[int(texture_obj.path_id)] = {
                "path_id": int(texture_obj.path_id),
                "name": getattr(texture, "m_Name", "") or "",
                "width": int(getattr(texture, "m_Width", 0) or 0),
                "height": int(getattr(texture, "m_Height", 0) or 0),
            }
        except Exception:
            continue

    materials: list[dict] = []
    primary_color: list[float] | None = None
    material_objects = [o for o in env.objects if getattr(o.type, "name", "") == "Material"]
    for material_obj in material_objects:
        try:
            material = material_obj.read()
        except Exception:
            continue

        shader_name = ""
        shader_ptr = getattr(material, "m_Shader", None)
        if shader_ptr is not None:
            try:
                shader_name = getattr(shader_ptr.read(), "m_Name", "") or ""
            except Exception:
                shader_name = ""

        saved = getattr(material, "m_SavedProperties", None)
        colors_raw = list(getattr(saved, "m_Colors", []) or []) if saved is not None else []
        tex_envs_raw = list(getattr(saved, "m_TexEnvs", []) or []) if saved is not None else []

        color_entries: list[dict] = []
        for key, value in colors_raw:
            rgba = _color_tuple(value)
            color_entries.append({"property": str(key), "rgba": rgba})

        preferred_color = None
        for key_name in ("_BaseColor", "_Color"):
            for color_entry in color_entries:
                if color_entry["property"] == key_name:
                    preferred_color = color_entry["rgba"]
                    break
            if preferred_color is not None:
                break

        if preferred_color is None and color_entries:
            preferred_color = color_entries[0]["rgba"]
        if primary_color is None and preferred_color is not None:
            primary_color = preferred_color

        texture_bindings: list[dict] = []
        for prop, tex_env in tex_envs_raw:
            pptr = getattr(tex_env, "m_Texture", None)
            path_id = int(getattr(pptr, "path_id", 0) or 0)
            file_id = int(getattr(pptr, "file_id", 0) or 0)
            local_texture = local_texture_by_id.get(path_id)
            texture_bindings.append(
                {
                    "property": str(prop),
                    "path_id": path_id,
                    "file_id": file_id,
                    "external_name": external_file_map.get(file_id),
                    "texture_name": (local_texture or {}).get("name"),
                }
            )

        materials.append(
            {
                "path_id": int(material_obj.path_id),
                "name": getattr(material, "m_Name", "") or "",
                "shader": shader_name,
                "primary_color": preferred_color,
                "colors": color_entries,
                "texture_bindings": texture_bindings,
            }
        )

    return materials, local_texture_by_id, primary_color


def _extract_renderer_material_bindings(env: object) -> list[dict]:
    bindings: list[dict] = []
    renderer_objects = [
        o for o in env.objects if getattr(o.type, "name", "") in {"MeshRenderer", "SkinnedMeshRenderer"}
    ]
    for renderer_obj in renderer_objects:
        try:
            renderer = renderer_obj.read()
        except Exception:
            continue
        material_refs = []
        for pptr in list(getattr(renderer, "m_Materials", []) or []):
            material_refs.append(
                {
                    "path_id": int(getattr(pptr, "path_id", 0) or 0),
                    "file_id": int(getattr(pptr, "file_id", 0) or 0),
                }
            )
        bindings.append(
            {
                "renderer_type": renderer.__class__.__name__,
                "renderer_path_id": int(renderer_obj.path_id),
                "material_refs": material_refs,
            }
        )
    return bindings


def _apply_dependency_texture_candidates(asset: dict, dependency_summary: dict | None) -> None:
    if not dependency_summary:
        return

    texture_name_map = dependency_summary.get("texture_name_map") or {}
    all_paths = set((dependency_summary.get("texture_asset_members") or {}).keys())
    candidates: list[str] = []
    for material in asset.get("materials", []):
        for binding in material.get("texture_bindings", []):
            file_id = int(binding.get("file_id", 0) or 0)
            path_id = int(binding.get("path_id", 0) or 0)
            # Strict mode: only resolve through explicit external references.
            if file_id <= 0:
                continue

            external_name = (binding.get("external_name") or "").strip()
            if external_name and external_name in all_paths:
                candidates.append(external_name)
                continue

            # Optional strict secondary: exact stem match from explicit texture name if present.
            tex_name = (binding.get("texture_name") or "").lower().strip()
            if tex_name and tex_name in texture_name_map and path_id > 0:
                candidates.append(texture_name_map[tex_name])

    # Keep deterministic order with dedupe.
    seen = set()
    ordered = []
    for item in candidates:
        if item in seen:
            continue
        seen.add(item)
        ordered.append(item)
    asset["dependency_texture_candidates"] = ordered


def _inspect_hhh_bytes(entry: str, payload: bytes) -> dict:
    """Inspect a .hhh UnityFS payload and return lightweight diagnostics."""
    result: dict = {
        "hhh_entry": entry,
        "byte_size": len(payload),
        "inspect_status": "ok",
        "object_count": 0,
        "mesh_count": 0,
        "primary_mesh": None,
        "primary_material_color": None,
        "materials": [],
        "local_textures": [],
        "renderer_material_bindings": [],
        "dependency_texture_candidates": [],
        "warnings": [],
    }

    try:
        env = UnityPy.load(payload)
    except Exception as exc:  # pragma: no cover - defensive, depends on UnityPy internals
        result["inspect_status"] = "parse-error"
        result["warnings"].append(f"UnityPy failed to parse .hhh payload: {exc}")
        return result

    result["object_count"] = len(env.objects)
    mesh_candidates = [o for o in env.objects if getattr(o.type, "name", "") == "Mesh"]
    result["mesh_count"] = len(mesh_candidates)

    materials, local_texture_by_id, primary_color = _extract_material_data(env)
    result["materials"] = materials
    result["local_textures"] = sorted(local_texture_by_id.values(), key=lambda t: t["path_id"])
    result["renderer_material_bindings"] = _extract_renderer_material_bindings(env)
    result["primary_material_color"] = primary_color

    if mesh_candidates:
        mesh = mesh_candidates[0].read()
        sub_meshes = getattr(mesh, "m_SubMeshes", []) or []
        sub_mesh_count = len(sub_meshes)
        triangle_count = 0
        for sm in sub_meshes:
            tri = getattr(sm, "triangleCount", None)
            if tri is None:
                tri = (getattr(sm, "indexCount", 0) or 0) // 3
            triangle_count += int(tri)
        vertex_count = getattr(getattr(mesh, "m_VertexData", None), "m_VertexCount", 0) or 0
        result["primary_mesh"] = {
            "name": getattr(mesh, "m_Name", "") or "UnnamedMesh",
            "vertex_count": int(vertex_count),
            "triangle_count": triangle_count,
            "sub_mesh_count": sub_mesh_count,
        }
    else:
        result["warnings"].append("No Mesh object found in .hhh payload.")

    if primary_color is None:
        result["warnings"].append("No primary material color extracted; renderer will use neutral gray.")

    return result


def parse_unity_input(mod_zip_path: Path, dependency_unitypackage: Path | None = None) -> dict:
    """Discover .hhh entries from a mod zip for MVP rendering."""
    if not mod_zip_path.exists():
        raise FileNotFoundError(f"mod zip not found: {mod_zip_path}")

    if not mod_zip_path.is_file():
        raise ValueError(f"mod zip path must be a file: {mod_zip_path}")

    hhh_entries: list[str] = []
    hhh_assets: list[dict] = []
    warnings: list[str] = []
    with ZipFile(mod_zip_path, "r") as archive:
        for name in archive.namelist():
            if _is_hhh(name):
                hhh_entries.append(name)
        for entry in sorted(hhh_entries):
            payload = archive.read(entry)
            hhh_assets.append(_inspect_hhh_bytes(entry, payload))

    dependency_summary = None
    if dependency_unitypackage:
        if not dependency_unitypackage.exists():
            warnings.append(f"Dependency unitypackage not found: {dependency_unitypackage}")
        elif not dependency_unitypackage.is_file():
            warnings.append(f"Dependency unitypackage path is not a file: {dependency_unitypackage}")
        else:
            try:
                dependency_summary = _inspect_unitypackage(dependency_unitypackage)
            except (tarfile.TarError, OSError) as exc:
                warnings.append(f"Unable to inspect dependency unitypackage: {exc}")

    for asset in hhh_assets:
        _apply_dependency_texture_candidates(asset, dependency_summary)

    return {
        "input": str(mod_zip_path),
        "dependency_unitypackage": str(dependency_unitypackage) if dependency_unitypackage else None,
        "dependency_summary": dependency_summary,
        "hhh_entries": sorted(hhh_entries),
        "hhh_assets": hhh_assets,
        "warnings": warnings + ([] if hhh_entries else ["No .hhh entries found in input zip."]),
    }
