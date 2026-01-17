# Testing Strategy

 

## Fixtures
- Sample .hhh files
- Reference JSON from UnityPy
- Known counts/bounds

## Unit Tests
- Binary reader
- PackedBitVector
- Mesh field parsing
- Vertex/index extraction

## Integration Tests
- Bundle → mesh extraction
- ZIP → asset discovery
- Three.js export

## Validation Approach
Compare C# outputs to Python UnityPy; JSON diff for critical fields.