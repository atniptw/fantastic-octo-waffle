/**
 * Shared type definitions for the pipeline package
 */

/**
 * Preview of a single cosmetic ready for display
 */
export interface CosmeticPreview {
  /** Blob image data (PNG/JPEG) */
  image: Blob;
  /** Human-readable name (e.g., "Cool Hat") */
  displayName: string;
  /** Inferred cosmetic type (e.g., "hat", "glasses") */
  type: string;
  /** SHA256 hash of original .hhh file */
  hash: string;
  /** Original filename */
  filename: string;
  /** Original internal path in ZIP */
  internalPath: string;
}

/**
 * Progress update during analysis
 */
export interface AnalyzeProgress {
  /** Current stage of analysis */
  stage: 'download' | 'extract' | 'convert' | 'complete';
  /** Percentage 0-100 */
  percent: number;
  /** Human-readable detail */
  detail?: string;
}

/**
 * Options for analyze function
 */
export interface AnalyzeOptions {
  /** Progress callback */
  onProgress?: (progress: AnalyzeProgress) => void;
  /** Abort signal for cancellation */
  signal?: AbortSignal;
  /** Skip image conversion, return metadata only */
  metadataOnly?: boolean;
}

/**
 * Result of mod analysis
 */
export interface AnalyzeResult {
  /** Array of cosmetic previews ready to display */
  cosmetics: CosmeticPreview[];
  /** Mod metadata */
  mod: {
    name: string;
    author: string;
    version: string;
    icon?: Blob;
  };
  /** Any non-fatal errors encountered */
  warnings: string[];
}

/**
 * Result from useModAnalyzer hook
 */
export interface UseModAnalyzerResult {
  /**
   * Analyze a mod by downloading, scanning, and converting cosmetics
   * @param modUrl - Full URL to mod ZIP file (e.g., from Thunderstore)
   * @param namespace - Mod namespace (e.g., 'AuthorName')
   * @param name - Mod name (e.g., 'ModName')
   */
  analyze: (
    modUrl: string,
    namespace: string,
    name: string,
    options?: AnalyzeOptions
  ) => Promise<AnalyzeResult>;

  /** True while analysis is in progress */
  isAnalyzing: boolean;

  /** Error message if analysis failed */
  error: string | null;

  /** Cancel the current analysis */
  cancel: () => void;
}
