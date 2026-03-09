from datetime import datetime, timezone


def build_metadata(parsed: dict, images: list[str]) -> dict:
    """Placeholder metadata function for Phase 1."""
    return {
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "source": parsed.get("input"),
        "image_count": len(images),
        "images": images,
    }
