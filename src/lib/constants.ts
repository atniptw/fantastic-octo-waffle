/**
 * Application-wide constants.
 * Centralized to avoid magic numbers and hardcoded values.
 */

export const TIMING = {
  /** Auto-dismiss status message after 3 seconds */
  STATUS_AUTO_DISMISS_MS: 3_000,

  /** Maximum time allowed for file scan operations */
  FILE_SCAN_TIMEOUT_MS: 30_000,
} as const;

export const PROGRESS = {
  /** Initial progress value when scan starts */
  SCAN_START: 10,

  /** Progress value when scan is almost complete */
  SCAN_COMPLETE: 90,

  /** Final progress when results are sent */
  RESULT_SENT: 100,
} as const;

export const FILE_LIMITS = {
  /** Maximum allowed file size in megabytes */
  MAX_FILE_SIZE_MB: 100,

  /** Maximum number of parallel scans allowed */
  MAX_CONCURRENT_SCANS: 5,
} as const;

export const PATTERNS = {
  /** Matches cosmetic plugin decoration files */
  COSMETIC_PATH: /\/plugins\/[^/]+\/Decorations\/[^/]+\.hhh$/i,
} as const;
