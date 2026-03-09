from pathlib import Path

from unity_processor.cli import main


def test_processor_cli_writes_metadata(tmp_path: Path, monkeypatch) -> None:
    output = tmp_path / "out"
    monkeypatch.chdir(tmp_path)
    monkeypatch.setattr(
        "sys.argv",
        ["unity-processor", "sample.unitypackage", "--output", str(output)],
    )

    code = main()

    assert code == 0
    assert (output / "metadata.json").exists()
