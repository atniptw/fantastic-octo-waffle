#!/usr/bin/env python3
import json
from pathlib import Path

import UnityPy


def _mesh_diag(mesh_obj):
    stream = getattr(mesh_obj, "m_StreamData", None)
    vertex_data = getattr(mesh_obj, "m_VertexData", None)

    stream_path = getattr(stream, "path", None) if stream else None
    stream_offset = int(getattr(stream, "offset", 0) or 0) if stream else 0
    stream_size = int(getattr(stream, "size", 0) or 0) if stream else 0
    has_stream = bool(stream_path) and stream_size > 0

    vertex_count = int(getattr(vertex_data, "m_VertexCount", 0) or 0) if vertex_data else 0
    vertex_data_len = 0
    if vertex_data and getattr(vertex_data, "m_DataSize", None):
        vertex_data_len = len(vertex_data.m_DataSize)

    channels_count = len(getattr(vertex_data, "m_Channels", []) or []) if vertex_data else 0
    index_buffer_len = len(getattr(mesh_obj, "m_IndexBuffer", []) or [])

    index_format = getattr(mesh_obj, "m_IndexFormat", None)
    if index_format is not None:
        index_format = int(index_format)

    use_16_bit_indices = getattr(mesh_obj, "m_Use16BitIndices", None)
    if use_16_bit_indices is not None:
        use_16_bit_indices = bool(use_16_bit_indices)

    renderable_hint = vertex_count > 0 and index_buffer_len > 0 and (vertex_data_len > 0 or has_stream)

    return {
        "vertexCount": vertex_count,
        "indexBufferLength": index_buffer_len,
        "vertexDataLength": vertex_data_len,
        "channelsCount": channels_count,
        "indexFormat": index_format,
        "use16BitIndices": use_16_bit_indices,
        "streamData": {
            "hasStream": has_stream,
            "path": stream_path if has_stream else None,
            "offset": stream_offset if has_stream else 0,
            "size": stream_size if has_stream else 0,
        },
        "renderableHint": renderable_hint,
    }


def _load_unitypy_meshes(fixture_path: Path):
    env = UnityPy.load(str(fixture_path))
    meshes = {}
    for obj in env.objects:
        if obj.type.name != "Mesh":
            continue

        mesh = obj.read()
        meshes[int(obj.path_id)] = _mesh_diag(mesh)

    return meshes


def main():
    repo_root = Path(__file__).resolve().parents[3]
    fixtures_root = repo_root / "tests" / "UnityAssetParser.Tests" / "fixtures" / "MoreHead-UnityAssets"
    index_path = fixtures_root / "index.json"

    with index_path.open("r", encoding="utf-8") as handle:
        index_data = json.load(handle)

    selected = index_data["selectedFixtures"]

    for fixture_info in selected:
        fixture_name = fixture_info["name"]
        snapshot_name = fixture_info["snapshotFile"]

        fixture_path = fixtures_root / fixture_name
        snapshot_path = fixtures_root / snapshot_name

        with snapshot_path.open("r", encoding="utf-8") as handle:
            snapshot = json.load(handle)

        unitypy_meshes = _load_unitypy_meshes(fixture_path)

        for mesh in snapshot.get("meshes", []):
            path_id = int(mesh["pathId"])
            oracle = unitypy_meshes.get(path_id, {
                "vertexCount": int(mesh.get("vertexCount", 0)),
                "indexBufferLength": int(mesh.get("bufferLengths", {}).get("indices", 0)),
                "vertexDataLength": 0,
                "channelsCount": 0,
                "indexFormat": None,
                "use16BitIndices": None,
                "streamData": {
                    "hasStream": False,
                    "path": None,
                    "offset": 0,
                    "size": 0,
                },
                "renderableHint": False,
            })

            mesh["oracle"] = oracle

            positions_likely_missing = int(mesh.get("bufferLengths", {}).get("vertexData", 0)) == 0 and int(mesh.get("vertexCount", 0)) > 0
            stream_present = bool(oracle["streamData"]["hasStream"])
            mesh["parserGap"] = {
                "positionsLikelyMissingFromParser": positions_likely_missing,
                "streamDataPresentInOracle": stream_present,
                "externalStreamLikelyCause": positions_likely_missing and stream_present,
                "decodedPositionsMissingInParser": positions_likely_missing,
            }

        snapshot["schemaVersion"] = 3
        snapshot["oracle"] = {
            "source": "UnityPy",
            "version": getattr(UnityPy, "__version__", "unknown"),
        }

        with snapshot_path.open("w", encoding="utf-8") as handle:
            json.dump(snapshot, handle, indent=2, ensure_ascii=False, sort_keys=False)
            handle.write("\n")

        print(f"Updated {snapshot_name}")

    index_data["schemaVersion"] = 3
    with index_path.open("w", encoding="utf-8") as handle:
        json.dump(index_data, handle, indent=2, ensure_ascii=False, sort_keys=False)
        handle.write("\n")

    print("Updated index.json")


if __name__ == "__main__":
    main()
