export function stringifyJsonObject(
  value: Record<string, unknown>,
  fieldName: string,
): string {
  if (value === null || Array.isArray(value) || typeof value !== "object") {
    throw new Error(`${fieldName} must be a JSON object`);
  }
  try {
    return JSON.stringify(value);
  } catch (error) {
    throw new Error(`Could not stringify ${fieldName}`, { cause: error });
  }
}

export function parseJsonObject(
  value: unknown,
  fieldName: string,
): Record<string, unknown> {
  if (typeof value !== "string") {
    throw new Error(`${fieldName} must be stored as JSON TEXT`);
  }
  let parsed: unknown;
  try {
    parsed = JSON.parse(value);
  } catch (error) {
    throw new Error(`Invalid JSON in ${fieldName}`, { cause: error });
  }
  if (parsed === null || Array.isArray(parsed) || typeof parsed !== "object") {
    throw new Error(`${fieldName} must contain a JSON object`);
  }
  return parsed as Record<string, unknown>;
}

export function readRequiredJson(
  row: Record<string, unknown>,
  fieldName: string,
): Record<string, unknown> {
  return parseJsonObject(row[fieldName], fieldName);
}

export function readNullableJson(
  row: Record<string, unknown>,
  fieldName: string,
): Record<string, unknown> | null {
  return row[fieldName] === null
    ? null
    : parseJsonObject(row[fieldName], fieldName);
}

export function readOptionalJson(
  row: Record<string, unknown>,
  fieldName: string,
): Record<string, unknown> | undefined {
  return row[fieldName] === null || row[fieldName] === undefined
    ? undefined
    : parseJsonObject(row[fieldName], fieldName);
}
