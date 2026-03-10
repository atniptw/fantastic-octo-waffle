from datetime import datetime, timezone


def build_metadata(parsed: dict, images: list[dict]) -> dict:
    """Build metadata for one-model-per-.hhh outputs."""
    warnings = list(parsed.get("warnings", []))
    for item in images:
        warnings.extend(item.get("warnings", []))

    hhh_assets = parsed.get("hhh_assets", [])
    parse_errors = [a for a in hhh_assets if a.get("inspect_status") != "ok"]
    assets_with_materials = [a for a in hhh_assets if a.get("materials")]
    assets_with_local_textures = [a for a in hhh_assets if a.get("local_textures")]
    assets_with_dependency_candidates = [
        a for a in hhh_assets if a.get("dependency_texture_candidates")
    ]
    exported_models = [i for i in images if i.get("model")]
    failed_exports = [i for i in images if i.get("export_status") != "ok"]

    return {
        "schema_version": 2,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "source": parsed.get("input"),
        "dependency_unitypackage": parsed.get("dependency_unitypackage"),
        "dependency_summary": parsed.get("dependency_summary"),
        "hhh_count": len(parsed.get("hhh_entries", [])),
        "hhh_inspected": len(hhh_assets),
        "hhh_parse_errors": len(parse_errors),
        "hhh_with_materials": len(assets_with_materials),
        "hhh_with_local_textures": len(assets_with_local_textures),
        "hhh_with_dependency_texture_candidates": len(assets_with_dependency_candidates),
        "model_count": len(exported_models),
        "export_failed_count": len(failed_exports),
        "status": "ok" if exported_models else "no-exported-models",
        "warnings": warnings,
        "images": images,
    }
