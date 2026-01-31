#!/usr/bin/env python3
import struct
import sys

# Simple debugging of the real bundle - let's see what UnityPy reads at offset 65
bundle_path = "Tests/UnityAssetParser.Tests/Fixtures/RealBundles/Cigar_neck.hhh"

with open(bundle_path, 'rb') as f:
    # Read header
    sig = f.read(8)  # UnityFS + null + version (4 bytes big-endian)
    sig_str = sig[:8].decode('ascii').rstrip('\x00')
    print(f"Signature: {sig_str}")
    
    # Skip version, player version, engine version strings
    version = struct.unpack('>I', sig[8:] + f.read(0))[0] if len(sig) > 8 else 0
    
    # Read null-terminated strings
    def read_cstring(f):
        chars = []
        while True:
            c = f.read(1)
            if c == b'\x00':
                break
            chars.append(c)
        return b''.join(chars).decode('ascii')
    
    f.seek(4)  # Skip back to version
    version_bytes = f.read(4)
    version = struct.unpack('>I', version_bytes)[0]
    player_ver = read_cstring(f)
    engine_ver = read_cstring(f)
    
    bundle_size = struct.unpack('>Q', f.read(8))[0]
    compressed_size = struct.unpack('>I', f.read(4))[0]
    uncompressed_size = struct.unpack('>I', f.read(4))[0]
    flags = struct.unpack('>I', f.read(4))[0]
    
    print(f"Version: {version}")
    print(f"Player: {player_ver}")
    print(f"Engine: {engine_ver}")
    print(f"Bundle size: {bundle_size}")
    print(f"Compressed size: {compressed_size}")
    print(f"Uncompressed size: {uncompressed_size}")
    print(f"Flags: 0x{flags:08X}")
    
    # Check if BlocksInfo is at the end
    if flags & 0x80:
        print("BlocksInfo at end of file")
        f.seek(-compressed_size, 2)  # Seek from end
    
    pos_before = f.tell()
    compressed_data = f.read(compressed_size)
    print(f"Read {len(compressed_data)} bytes of compressed data from offset {pos_before}")
    print(f"First 80 bytes (hex): {compressed_data[:80].hex()}")
    
    # Try to decompress with LZ4
    import lz4.block
    try:
        decompressed = lz4.block.decompress(compressed_data, uncompressed_size=uncompressed_size)
        print(f"\nDecompressed to {len(decompressed)} bytes")
        print(f"First 100 bytes (hex):")
        for i in range(0, min(100, len(decompressed)), 16):
            hex_str = decompressed[i:i+16].hex(' ')
            ascii_str = ''.join(chr(b) if 32 <= b < 127 else '.' for b in decompressed[i:i+16])
            print(f"{i:04x}: {hex_str:47s} {ascii_str}")
        
        # Now parse the tables
        print("\n=== Parsing BlocksInfo ===")
        offset = 0
        
        # Hash (16 bytes)
        hash_val = decompressed[offset:offset+16]
        print(f"Hash: {hash_val.hex()}")
        offset += 16
        
        # BlocksInfo count
        block_count = struct.unpack('>I', decompressed[offset:offset+4])[0]
        print(f"Block count: {block_count}")
        offset += 4
        
        # Blocks
        for i in range(block_count):
            uncomp = struct.unpack('>I', decompressed[offset:offset+4])[0]
            comp = struct.unpack('>I', decompressed[offset+4:offset+8])[0]
            flags_block = struct.unpack('>H', decompressed[offset+8:offset+10])[0]
            print(f"  Block {i}: uncomp={uncomp}, comp={comp}, flags=0x{flags_block:04X}")
            offset += 10  # 4 + 4 + 2 bytes
        
        # Nodes count
        node_count = struct.unpack('>I', decompressed[offset:offset+4])[0]
        print(f"Node count: {node_count}")
        offset += 4
        
        # Nodes
        for i in range(node_count):
            node_offset = struct.unpack('>Q', decompressed[offset:offset+8])[0]
            node_size = struct.unpack('>Q', decompressed[offset+8:offset+16])[0]
            node_flags = struct.unpack('>I', decompressed[offset+16:offset+20])[0]
            offset += 20
            
            # Read null-terminated path
            path_bytes = []
            start_offset = offset
            while offset < len(decompressed) and decompressed[offset] != 0:
                path_bytes.append(decompressed[offset])
                offset += 1
            offset += 1  # Skip null terminator
            
            path = bytes(path_bytes).decode('utf-8')
            print(f"  Node {i}: offset={node_offset}, size={node_size}, flags=0x{node_flags:08X}, path='{path}'")
            print(f"    (path read from offset {start_offset} to {offset-1}, {len(path_bytes)} bytes)")
        
        print(f"\nTotal bytes consumed: {offset} of {len(decompressed)}")
        
    except Exception as e:
        print(f"Decompression failed: {e}")
        import traceback
        traceback.print_exc()
