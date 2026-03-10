import json
import tarfile
from io import BytesIO
from pathlib import Path
from zipfile import ZipFile

from unity_processor.cli import main


def _write_bytes_member(archive: tarfile.TarFile, name: str, content: bytes) -> None:
    info = tarfile.TarInfo(name=name)
    info.size = len(content)
    archive.addfile(info, BytesIO(content))


def test_processor_cli_writes_metadata(tmp_path: Path, monkeypatch) -> None:
    mod_zip = tmp_path / "mod.zip"
    with ZipFile(mod_zip, "w") as archive:
        archive.writestr("assets/model_a.hhh", b"dummy")
        archive.writestr("assets/model_b.hhh", b"dummy")
        archive.writestr("assets/readme.txt", b"ignore")

    output = tmp_path / "data" / "outputs"
    monkeypatch.chdir(tmp_path)
    monkeypatch.setattr(
        "sys.argv",
        ["unity-processor", str(mod_zip), "--output", str(output)],
    )

    code = main()

    assert code == 0
    metadata_path = output / "metadata.json"
    assert metadata_path.exists()

    data = json.loads(metadata_path.read_text(encoding="utf-8"))
    assert data["schema_version"] == 2
    assert data["model_count"] >= 0
    assert data["hhh_count"] == 2
    assert data["hhh_inspected"] == 2
    assert data["hhh_parse_errors"] == 0
    assert "hhh_with_materials" in data
    assert "hhh_with_local_textures" in data
    assert len(data["images"]) == 2
    for item in data["images"]:
        if item.get("model"):
            assert (output / item["model"]).exists()
        assert "mesh" in item
        assert "model_format" in item
        assert "texture_source" in item
        assert "export_status" in item
        assert "mesh_bounds" in item
        assert "primary_material_color" in item


def test_processor_cli_inspects_dependency_unitypackage(tmp_path: Path, monkeypatch) -> None:
    mod_zip = tmp_path / "mod.zip"
    with ZipFile(mod_zip, "w") as archive:
        archive.writestr("assets/model_a.hhh", b"dummy")

    dependency = tmp_path / "dependency.unitypackage"
    with tarfile.open(dependency, "w:gz") as archive:
        _write_bytes_member(archive, "abc123/pathname", b"Assets/Textures/a.png")
        _write_bytes_member(archive, "def456/pathname", b"Assets/Meshes/a.asset")

    output = tmp_path / "data" / "outputs"
    monkeypatch.chdir(tmp_path)
    monkeypatch.setattr(
        "sys.argv",
        [
            "unity-processor",
            str(mod_zip),
            "--dependency-unitypackage",
            str(dependency),
            "--output",
            str(output),
        ],
    )

    code = main()

    assert code == 0
    metadata_path = output / "metadata.json"
    data = json.loads(metadata_path.read_text(encoding="utf-8"))
    summary = data["dependency_summary"]
    assert summary["pathname_count"] == 2
    assert summary["texture_count"] == 1
    assert summary["material_count"] == 0
    assert "Assets/Textures/a.png" in summary["sample_pathnames"]
    assert "a" in summary["texture_name_map"]
    assert data["hhh_inspected"] == 1
