/**
 * Cosmetic utilities - display names, type inference, defaults
 */

/**
 * Generates a display name from a filename
 * Example: "cool_hat.hhh" -> "Cool Hat"
 */
export function generateDisplayName(filename: string): string {
  // Trim whitespace first
  const trimmed = filename.trim();

  // Remove extension
  const nameWithoutExt = trimmed.replace(/\.hhh$/i, '');

  // Replace underscores and hyphens with spaces
  // Capitalize first letter of each word
  return nameWithoutExt
    .replace(/[_-]/g, ' ')
    .replace(/\b\w/g, (char) => char.toUpperCase())
    .trim();
}

/**
 * Infers cosmetic type from filename
 * Returns: 'head' | 'hat' | 'glasses' | 'mask' | 'accessory' | 'decoration'
 */
export function inferCosmeticType(filename: string): string {
  const lowerFilename = filename.toLowerCase();

  // Check for common type keywords using lookahead/lookbehind for word boundaries
  // Match whole words that are separated by underscores, hyphens, dots, or string boundaries
  if (/(?:^|[_\-. ])head(?:[_\-. ]|$)/.test(lowerFilename)) {
    return 'head';
  } else if (
    /(?:^|[_\-. ])hat(?:[_\-. ]|$)/.test(lowerFilename) ||
    /(?:^|[_\-. ])helmet(?:[_\-. ]|$)/.test(lowerFilename)
  ) {
    return 'hat';
  } else if (
    /(?:^|[_\-. ])glasses(?:[_\-. ]|$)/.test(lowerFilename) ||
    /(?:^|[_\-. ])goggles(?:[_\-. ]|$)/.test(lowerFilename)
  ) {
    return 'glasses';
  } else if (/(?:^|[_\-. ])mask(?:[_\-. ]|$)/.test(lowerFilename)) {
    return 'mask';
  } else if (
    /(?:^|[_\-. ])accessory(?:[_\-. ]|$)/.test(lowerFilename) ||
    /(?:^|[_\-. ])acc_(?:[_\-. ]|$)/.test(lowerFilename)
  ) {
    return 'accessory';
  }

  // Default to decoration
  return 'decoration';
}

/**
 * Creates a placeholder image for cosmetics without conversion
 * For now: return a simple 1x1 transparent PNG
 * Future: integrate with 3D rendering
 */
export function createPlaceholderImage(): Blob {
  // 1x1 transparent PNG
  const pngData = new Uint8Array([
    0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, // PNG signature
    0x00, 0x00, 0x00, 0x0d, 0x49, 0x48, 0x44, 0x52, // IHDR
    0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
    0x08, 0x06, 0x00, 0x00, 0x00, 0x1f, 0x15, 0xc4,
    0x89, 0x00, 0x00, 0x00, 0x0a, 0x49, 0x44, 0x41,
    0x54, 0x78, 0x9c, 0x63, 0x00, 0x01, 0x00, 0x00,
    0x05, 0x00, 0x01, 0x0d, 0x0a, 0x2d, 0xb4, 0x00,
    0x00, 0x00, 0x00, 0x49, 0x45, 0x4e, 0x44, 0xae,
    0x42, 0x60, 0x82,
  ]);
  return new Blob([pngData], { type: 'image/png' });
}

/**
 * Converts .hhh file content to a displayable image
 * Currently returns placeholder
 * TODO: Integrate UnityFS parsing + 3D rendering in future
 */
export async function convertHhhToImage(
  _hhhContent: Uint8Array,
  _options?: { placeholder?: boolean }
): Promise<Blob> {
  // For now, always return placeholder
  // In the future: parse .hhh UnityFS, extract mesh/texture, render with Three.js
  return createPlaceholderImage();
}
