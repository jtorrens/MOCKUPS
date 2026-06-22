export function readString(
  source: Record<string, unknown>,
  key: string,
  fallback: string,
): string {
  return typeof source[key] === "string" ? source[key] : fallback;
}

export function readNumber(
  source: Record<string, unknown>,
  key: string,
  fallback: number,
): number {
  return typeof source[key] === "number" ? source[key] : fallback;
}

export function readFontWeight(
  source: Record<string, unknown>,
  key: string,
  fallback: string | number,
): string | number {
  const value = source[key];
  return typeof value === "string" || typeof value === "number"
    ? value
    : fallback;
}

export function readObject(
  source: Record<string, unknown>,
  key: string,
): Record<string, unknown> {
  const value = source[key];
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}
