#!/usr/bin/env python3
"""
Generate reference JSON outputs from UnityFS bundle files using UnityPy.
This script parses bundle files and outputs their metadata in JSON format
for validation against the C# BundleFile implementation.

Usage:
    python generate_reference_json.py <bundle_file> [output_json]
    python generate_reference_json.py --all  # Process all fixtures

Requirements:
    pip install UnityPy
"""

import sys
import json
import argparse
from pathlib import Path
from typing import Dict, Any, List

try:
    import UnityPy
except ImportError:
    print("Error: UnityPy not installed. Install with: pip install UnityPy", file=sys.stderr)
    sys.exit(1)


def parse_bundle_to_json(bundle_path: Path) -> Dict[str, Any]:
    """
    Parse a UnityFS bundle file using UnityPy and extract metadata.
    
    Args:
        bundle_path: Path to the .hhh bundle file
        
    Returns:
        Dictionary containing bundle metadata matching the C# BundleMetadata schema
    """
    # Load bundle with UnityPy
    env = UnityPy.load(str(bundle_path))
    
    # Access the BundleFile object
    if not hasattr(env, 'file') or not hasattr(env.file, 'bundle'):
        raise ValueError(f"Not a valid UnityFS bundle: {bundle_path}")
    
    bundle = env.file.bundle
    
    # Extract header metadata
    header = {
        "signature": bundle.signature,
        "version": bundle.format_version,
        "unity_version": bundle.version_player,
        "unity_revision": bundle.version_engine,
        "size": bundle.file_size,
        "compressed_blocks_info_size": bundle.compressed_blocks_info_size,
        "uncompressed_blocks_info_size": bundle.uncompressed_blocks_info_size,
        "flags": bundle.flags
    }
    
    # Extract storage blocks metadata
    storage_blocks = []
    for block in bundle.storage_blocks:
        storage_blocks.append({
            "uncompressed_size": block.uncompressed_size,
            "compressed_size": block.compressed_size,
            "flags": block.flags
        })
    
    # Extract nodes metadata
    nodes = []
    for node in bundle.nodes:
        nodes.append({
            "offset": node.offset,
            "size": node.size,
            "flags": node.flags,
            "path": node.path
        })
    
    # Compute data offset
    # For embedded layout (flags & 0x80 == 0):
    #   data_offset = aligned_header_end + compressed_blocks_info_size
    # For streamed layout (flags & 0x80 != 0):
    #   data_offset = aligned_header_end
    data_offset = bundle.data_offset if hasattr(bundle, 'data_offset') else 0
    
    # Construct metadata matching C# schema
    metadata = {
        "header": header,
        "storage_blocks": storage_blocks,
        "nodes": nodes,
        "data_offset": data_offset
    }
    
    return metadata


def process_bundle(bundle_path: Path, output_path: Path = None) -> None:
    """
    Process a bundle file and write JSON output.
    
    Args:
        bundle_path: Path to input bundle file
        output_path: Path to output JSON file (default: <bundle>_expected.json)
    """
    if not bundle_path.exists():
        print(f"Error: Bundle file not found: {bundle_path}", file=sys.stderr)
        sys.exit(1)
    
    # Default output path
    if output_path is None:
        output_path = bundle_path.parent / f"{bundle_path.stem}_expected.json"
    
    print(f"Processing: {bundle_path}")
    
    try:
        # Parse bundle
        metadata = parse_bundle_to_json(bundle_path)
        
        # Write JSON
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(metadata, f, indent=2, ensure_ascii=False)
        
        print(f"✓ Wrote: {output_path}")
        
        # Print summary
        print(f"  Header: {metadata['header']['signature']} v{metadata['header']['version']}")
        print(f"  Storage blocks: {len(metadata['storage_blocks'])}")
        print(f"  Nodes: {len(metadata['nodes'])}")
        print(f"  Data offset: {metadata['data_offset']}")
        
    except Exception as e:
        print(f"✗ Error processing {bundle_path}: {e}", file=sys.stderr)
        raise


def process_all_fixtures(fixtures_dir: Path) -> None:
    """
    Process all .hhh files in the fixtures directory.
    
    Args:
        fixtures_dir: Path to fixtures directory
    """
    if not fixtures_dir.exists():
        print(f"Error: Fixtures directory not found: {fixtures_dir}", file=sys.stderr)
        sys.exit(1)
    
    # Find all .hhh files
    bundle_files = list(fixtures_dir.glob("*.hhh"))
    
    if not bundle_files:
        print(f"Warning: No .hhh files found in {fixtures_dir}", file=sys.stderr)
        return
    
    print(f"Found {len(bundle_files)} bundle file(s) in {fixtures_dir}\n")
    
    for bundle_path in sorted(bundle_files):
        process_bundle(bundle_path)
        print()  # Blank line between files


def main():
    parser = argparse.ArgumentParser(
        description="Generate reference JSON from UnityFS bundles for validation"
    )
    parser.add_argument(
        "bundle",
        nargs="?",
        type=Path,
        help="Path to bundle file (.hhh)"
    )
    parser.add_argument(
        "-o", "--output",
        type=Path,
        help="Output JSON path (default: <bundle>_expected.json)"
    )
    parser.add_argument(
        "--all",
        action="store_true",
        help="Process all bundles in Tests/UnityAssetParser.Tests/Fixtures/"
    )
    parser.add_argument(
        "--fixtures-dir",
        type=Path,
        default=Path(__file__).parent.parent / "Tests" / "UnityAssetParser.Tests" / "Fixtures",
        help="Fixtures directory path (default: Tests/UnityAssetParser.Tests/Fixtures/)"
    )
    
    args = parser.parse_args()
    
    if args.all:
        # Process all fixtures
        process_all_fixtures(args.fixtures_dir)
    elif args.bundle:
        # Process single bundle
        process_bundle(args.bundle, args.output)
    else:
        parser.print_help()
        sys.exit(1)


if __name__ == "__main__":
    main()
