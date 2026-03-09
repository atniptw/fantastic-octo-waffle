from datetime import datetime, timezone


def build_metadata(parsed: dict, images: list[dict]) -> dict:
    """Build metadata for one-image-per-.hhh MVP outputs."""
    warnings = list(parsed.get("warnings", []))
    for item in images:
        warnings.extend(item.get("warnings", []))

    hhh_assets = parsed.get("hhh_assets", [])
    parse_errors = [a for a in hhh_assets if a.get("inspect_status") != "ok"]

    return {
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "source": parsed.get("input"),
        "dependency_unitypackage": parsed.get("dependency_unitypackage"),
        "dependency_summary": parsed.get("dependency_summary"),
        "hhh_count": len(parsed.get("hhh_entries", [])),
        "hhh_inspected": len(hhh_assets),
        "hhh_parse_errors": len(parse_errors),
        "image_count": len(images),
        "status": "ok" if images else "no-renderable-assets",
        "warnings": warnings,
        "images": images,
    }
