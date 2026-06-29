import type { AppRecord } from "../api/client.js";
import {
  deepEqualJson,
  isJsonObject,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import { TokenOverrideEditor } from "../components/json-editor/TokenOverrideEditor.js";
import type { JsonUiHints } from "../components/json-editor/uiHints.js";
import { jsonUiHintsFromFieldBindings } from "../components/json-editor/fieldDefinitionHints.js";
import {
  SCREEN_INSTANCE_DEVICE_STATE_BINDINGS,
  SCREEN_INSTANCE_TRANSITION_BINDINGS,
} from "../../domain/fields/screenInstanceFields.js";
import { parsedObject } from "./recordJsonUtils.js";

function stringifyJson(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

function asJsonValue(value: unknown): JsonValue {
  if (
    value === null ||
    typeof value === "string" ||
    typeof value === "number" ||
    typeof value === "boolean" ||
    Array.isArray(value)
  ) {
    return value as JsonValue;
  }
  if (value && typeof value === "object") {
    return value as JsonValue;
  }
  return null;
}

function pruneInheritedValues(
  override: Record<string, unknown>,
  inherited: Record<string, unknown>,
) {
  const next: Record<string, JsonValue> = {};
  for (const [key, value] of Object.entries(override)) {
    if (deepEqualJson(asJsonValue(value), asJsonValue(inherited[key]))) {
      continue;
    }
    next[key] = asJsonValue(value);
  }
  return next;
}

function inheritedDeviceStateForRecord(
  records: Record<string, AppRecord[]>,
  record: AppRecord | undefined,
) {
  const deviceStateId = record?.device_state_id;
  const deviceState = records.device_states?.find(
    (item) => item.id === deviceStateId,
  );
  return parsedObject(
    typeof deviceState?.state_json === "string"
      ? deviceState.state_json
      : stringifyJson(deviceState?.state_json ?? {}),
  );
}

function omitKeys(
  value: Record<string, unknown>,
  keys: string[],
): Record<string, unknown> {
  const blocked = new Set(keys);
  return Object.fromEntries(
    Object.entries(value).filter(([key]) => !blocked.has(key)),
  );
}

interface ScreenTransitionFieldsProps {
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
  drafts: Record<string, string>;
  setDraftValue: (column: string, value: string) => void;
}

export function ScreenTransitionFields({
  drafts,
  setDraftValue,
}: ScreenTransitionFieldsProps) {
  const root = parsedObject(drafts.transition_in_json ?? "{}");
  return (
    <TokenOverrideEditor
      rootValue={root as JsonValue}
      inheritedRoot={{ type: "none", durationFrames: 0 }}
      hints={jsonUiHintsFromFieldBindings(SCREEN_INSTANCE_TRANSITION_BINDINGS)}
      groupContext="transition"
      restoreMode="set"
      showInheritedValue
      onRootChange={(nextRoot) => {
        const next = isJsonObject(nextRoot) ? nextRoot : {};
        const type = String(next.type ?? "none");
        const durationFrames = Math.max(0, Number(next.durationFrames ?? 0));
        setDraftValue(
          "transition_in_json",
          stringifyJson(
            type === "none" || durationFrames <= 0
              ? { type: "none", durationFrames: 0 }
              : { ...next, type, durationFrames },
          ),
        );
      }}
    />
  );
}

interface ScreenDeviceStateFieldsProps {
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
  rawValue: string;
  onRawChange: (nextRaw: string) => void;
}

const DEVICE_STATE_HINTS: JsonUiHints = jsonUiHintsFromFieldBindings(
  SCREEN_INSTANCE_DEVICE_STATE_BINDINGS,
);

const SCREEN_ORIENTATION_HINTS: JsonUiHints = {
  orientation: DEVICE_STATE_HINTS.orientation,
};

function DeviceStateOverrideEditor({
  inherited,
  local,
  hints,
  onRawChange,
}: {
  inherited: Record<string, unknown>;
  local: Record<string, unknown>;
  hints: JsonUiHints;
  onRawChange: (nextRaw: string) => void;
}) {
  const cleanOverride = pruneInheritedValues(local, inherited);
  return (
    <TokenOverrideEditor
      rootValue={cleanOverride as JsonValue}
      inheritedRoot={asJsonValue(inherited)}
      hints={hints}
      groupContext="deviceState"
      restoreMode="remove"
      showInheritedValue
      onRootChange={(nextRoot) =>
        onRawChange(
          stringifyJson(
            pruneInheritedValues(
              isJsonObject(nextRoot) ? nextRoot : {},
              inherited,
            ),
          ),
        )
      }
    />
  );
}

interface ScreenOrientationFieldProps {
  records: Record<string, AppRecord[]>;
  record: AppRecord | undefined;
  rawValue: string;
  onRawChange: (nextRaw: string) => void;
}

export function ScreenOrientationField({
  records,
  record,
  rawValue,
  onRawChange,
}: ScreenOrientationFieldProps) {
  const inheritedState = inheritedDeviceStateForRecord(records, record);
  const rawOverride = parsedObject(rawValue);
  return (
    <DeviceStateOverrideEditor
      inherited={{ orientation: inheritedState.orientation ?? "portrait" }}
      local={
        Object.prototype.hasOwnProperty.call(rawOverride, "orientation")
          ? { orientation: rawOverride.orientation }
          : {}
      }
      hints={SCREEN_ORIENTATION_HINTS}
      onRawChange={(nextRaw) => {
        const nextOrientationOverride = parsedObject(nextRaw);
        const nextDeviceStateOverride = {
          ...omitKeys(rawOverride, ["orientation"]),
          ...nextOrientationOverride,
        };
        onRawChange(stringifyJson(nextDeviceStateOverride));
      }}
    />
  );
}

export function ScreenDeviceStateFields({
  records,
  record,
  rawValue,
  onRawChange,
}: ScreenDeviceStateFieldsProps) {
  const inherited = omitKeys(inheritedDeviceStateForRecord(records, record), [
    "orientation",
  ]);
  const rawOverride = omitKeys(parsedObject(rawValue), ["orientation"]);
  return (
    <DeviceStateOverrideEditor
      inherited={inherited}
      local={rawOverride}
      hints={DEVICE_STATE_HINTS}
      onRawChange={(nextRaw) => {
        const nextDeviceStateOverride = {
          ...omitKeys(parsedObject(rawValue), Object.keys(inherited)),
          ...parsedObject(nextRaw),
        };
        onRawChange(stringifyJson(nextDeviceStateOverride));
      }}
    />
  );
}
