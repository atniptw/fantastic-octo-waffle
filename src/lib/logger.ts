const debugEnabled = Boolean((import.meta as any).env?.DEV || (import.meta as any).env?.VITE_DEBUG_LOG === 'true');

/** Debug logger that only prints when debug is enabled. */
export function debugLog(...args: unknown[]): void {
  if (!debugEnabled) return;
  console.debug(...args);
}

export function isDebugEnabled(): boolean {
  return debugEnabled;
}
