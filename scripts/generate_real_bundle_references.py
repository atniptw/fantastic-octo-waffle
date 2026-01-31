#!/usr/bin/env python3
"""
Generate UnityPy reference snapshots for real bundle files.
These snapshots serve as ground truth for C# parser validation.
"""

import json
import sys
from pathlib import Path
import UnityPy

def extract_bundle_snapshot(bundle_path: str) -> dict:
    """Extract relevant data from bundle using UnityPy."""
    
    with open(bundle_path, 'rb') as f:
        env = UnityPy.load(f)
    
    snapshot = {
        "file": Path(bundle_path).name,
        "header": {
            "signature": "UnityFS",
        },
        "object_types": {},
        "nodes": [],
        "meshes": [],
        "extraction_errors": [],
        "summary": {
            "total_objects": 0,
            "total_nodes": 0,
            "total_meshes": 0,
            "total_extraction_errors": 0
        }
    }
    
    try:
        # Get bundle info if available
        if hasattr(env, 'signature'):
            snapshot["header"]["signature"] = env.signature
        
        # Collect all object types
        if hasattr(env, 'objects') and env.objects:
            for obj in env.objects:
                if hasattr(obj, 'type') and obj.type:
                    type_name = obj.type.name
                    snapshot["object_types"][type_name] = snapshot["object_types"].get(type_name, 0) + 1
            snapshot["summary"]["total_objects"] = len(env.objects)
        
        # Extract nodes information from container
        if hasattr(env, 'container'):
            for name, pptr in env.container.items():
                snapshot["nodes"].append({
                    "path": name,
                    "type": type(pptr).__name__
                })
        
        # Extract Mesh objects from objects list
        if hasattr(env, 'objects') and env.objects:
            for obj in env.objects:
                # Look for Mesh class (ClassID 43)
                if hasattr(obj, 'type') and obj.type and obj.type.name == "Mesh":
                    try:
                        mesh_data = obj.read()
                        mesh_name = getattr(mesh_data, 'm_Name', 'Unknown')
                        
                        # Calculate vertex count safely
                        vertices = getattr(mesh_data, 'm_Vertices', None)
                        vertex_count = len(vertices) // 3 if vertices is not None else 0
                        
                        # Calculate index count from submeshes
                        index_count = 0
                        submeshes = getattr(mesh_data, 'm_SubMeshes', [])
                        if submeshes:
                            for submesh in submeshes:
                                if hasattr(submesh, 'indexCount'):
                                    index_count += submesh.indexCount
                                elif isinstance(submesh, dict) and 'indexCount' in submesh:
                                    index_count += submesh['indexCount']
                        
                        snapshot["meshes"].append({
                            "name": mesh_name,
                            "vertex_count": vertex_count,
                            "index_count": index_count,
                            "has_normals": bool(getattr(mesh_data, 'm_Normals', None)),
                            "has_uv0": bool(getattr(mesh_data, 'm_UV0', None)),
                            "object_path": f"Object {obj.path_id}" if hasattr(obj, 'path_id') else "Unknown",
                        })
                    except Exception as e:
                        snapshot["extraction_errors"].append({
                            "type": f"Mesh extraction",
                            "error": f"{type(e).__name__}: {str(e)}"
                        })
        
        # Update summary stats
        snapshot["summary"]["total_nodes"] = len(snapshot["nodes"])
        snapshot["summary"]["total_meshes"] = len(snapshot["meshes"])
        snapshot["summary"]["total_extraction_errors"] = len(snapshot["extraction_errors"])
        
    except Exception as e:
        snapshot["extraction_errors"].append({
            "type": "General",
            "error": str(e)
        })
        snapshot["summary"]["total_extraction_errors"] = len(snapshot["extraction_errors"])
    
    return snapshot


def main():
    """Generate reference snapshots for all real bundles."""
    
    fixture_dir = Path("Tests/UnityAssetParser.Tests/Fixtures/RealBundles")
    output_dir = Path("Tests/UnityAssetParser.Tests/Fixtures/Snapshots")
    output_dir.mkdir(parents=True, exist_ok=True)
    
    bundles = [
        "Cigar_neck.hhh",
        "ClownNose_head.hhh",
        "Glasses_head.hhh",
    ]
    
    for bundle_name in bundles:
        bundle_path = fixture_dir / bundle_name
        if not bundle_path.exists():
            print(f"❌ {bundle_name}: File not found at {bundle_path}")
            continue
        
        try:
            print(f"📦 Processing {bundle_name}...")
            snapshot = extract_bundle_snapshot(str(bundle_path))
            
            output_file = output_dir / f"{bundle_name.replace('.hhh', '')}_reference.json"
            with open(output_file, 'w') as f:
                json.dump(snapshot, f, indent=2)
            
            print(f"✅ {bundle_name}: {snapshot['summary']['total_nodes']} nodes, {snapshot['summary']['total_meshes']} meshes")
            print(f"   → {output_file}")
            
        except Exception as e:
            print(f"❌ {bundle_name}: {e}")
            import traceback
            traceback.print_exc()


if __name__ == "__main__":
    main()
