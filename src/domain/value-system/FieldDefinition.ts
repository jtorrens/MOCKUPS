import {
  ValueRegistry,
  type ValueKind,
} from "./ValueRegistry.js";

export interface FieldEditorMetadata {
  readonly label?: string;
  readonly description?: string;
  readonly options?: readonly string[];
  readonly min?: number;
  readonly max?: number;
  readonly step?: number | "any";
  readonly allowMultiple?: boolean;
  readonly allowEmpty?: boolean;
  readonly semanticTokenGroup?: string;
  readonly tableId?: string;
  readonly labelColumn?: string;
  readonly fileKind?: "file" | "directory";
  readonly accept?: readonly string[];
  readonly lockFontFamily?: boolean;
  readonly hidden?: boolean;
  readonly multiline?: boolean;
  readonly rows?: number;
  readonly pair?: {
    readonly id: string;
    readonly label: string;
    readonly role: string;
  };
}

export interface FieldDefinition {
  readonly id: string;
  readonly kind: ValueKind;
  readonly defaultValue?: unknown;
  readonly ui?: FieldEditorMetadata;
}

export function defineField(definition: FieldDefinition): FieldDefinition {
  ValueRegistry.definition(definition.kind);
  if (definition.defaultValue !== undefined) {
    ValueRegistry.assert(definition.kind, definition.defaultValue);
  }
  return definition;
}

export function defineFields<const T extends Record<string, FieldDefinition>>(
  definitions: T,
): T {
  for (const definition of Object.values(definitions)) {
    defineField(definition);
  }
  return definitions;
}

export interface FieldResolutionTraceEntry {
  readonly source: string;
  readonly value: unknown;
}

export interface ResolvedFieldValue {
  readonly field: FieldDefinition;
  readonly value: unknown;
  readonly source: string;
  readonly trace: readonly FieldResolutionTraceEntry[];
}

export function resolveFieldValue(
  field: FieldDefinition,
  candidates: readonly FieldResolutionTraceEntry[],
): ResolvedFieldValue | undefined {
  const trace: FieldResolutionTraceEntry[] = [];
  for (const candidate of candidates) {
    trace.push(candidate);
    if (
      candidate.value !== undefined &&
      !ValueRegistry.isInherited(candidate.value)
    ) {
      return {
        field,
        value: ValueRegistry.assert(field.kind, candidate.value),
        source: candidate.source,
        trace,
      };
    }
  }

  if (field.defaultValue !== undefined) {
    return {
      field,
      value: ValueRegistry.assert(field.kind, field.defaultValue),
      source: "default",
      trace: [
        ...trace,
        {
          source: "default",
          value: field.defaultValue,
        },
      ],
    };
  }

  return undefined;
}
