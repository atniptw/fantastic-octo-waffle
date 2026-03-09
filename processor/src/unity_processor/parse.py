from __future__ import annotations

import tarfile
from pathlib import Path
from zipfile import ZipFile

import UnityPy


def _is_hhh(path: str) -> bool:
    return path.lower().endswith(".hhh")


def _inspect_unitypackage(path: Path) -> dict:
    """Read a unitypackage tar archive and summarize dependency pathname entries."""
    pathnames: list[str] = []
    with tarfile.open(path, "r:*") as archive:
        for member in archive.getmembers():
            if not member.isfile() or not member.name.endswith("/pathname"):
                continue

            extracted = archive.extractfile(member)
            if extracted is None:
                continue
            raw = extracted.read().decode("utf-8", errors="replace").strip()
            if raw:
                pathnames.append(raw)

    return {
        "path": str(path),
        "pathname_count": len(pathnames),
        "sample_pathnames": sorted(pathnames)[:5],
    }


def _inspect_hhh_bytes(entry: str, payload: bytes) -> dict:
    """Inspect a .hhh UnityFS payload and return lightweight diagnostics."""
    result: dict = {
        "hhh_entry": entry,
        "byte_size": len(payload),
        "inspect_status": "ok",
        "object_count": 0,
        "mesh_count": 0,
        "primary_mesh": None,
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

    return {
        "input": str(mod_zip_path),
        "dependency_unitypackage": str(dependency_unitypackage) if dependency_unitypackage else None,
        "dependency_summary": dependency_summary,
        "hhh_entries": sorted(hhh_entries),
        "hhh_assets": hhh_assets,
        "warnings": warnings + ([] if hhh_entries else ["No .hhh entries found in input zip."]),
    }
