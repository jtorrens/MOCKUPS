export function requireRecord<T>(
  record: T | undefined,
  entity: string,
  id: string,
): T {
  if (!record) {
    throw new Error(`${entity} not found: ${id}`);
  }
  return record;
}

export function clamp(value: number, min = 0, max = 1): number {
  return Math.min(max, Math.max(min, value));
}
