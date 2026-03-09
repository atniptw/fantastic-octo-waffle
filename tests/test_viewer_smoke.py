from pathlib import Path


def test_viewer_has_placeholder_shell() -> None:
    html = Path("viewer/index.html").read_text(encoding="utf-8")
    assert "Unity Preview Viewer" in html
    assert "app.js" in html
    assert "id=\"summary\"" in html
    assert "id=\"gallery\"" in html
