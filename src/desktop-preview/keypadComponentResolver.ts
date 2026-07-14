import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredNumberPair,
  requiredPossiblyEmptyString,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import type {
  KeypadDesignContract,
  KeypadKeyKind,
  KeypadKeyState,
  KeypadSizingMode,
} from "./keypadComponentContract.js";
import {
  literalLabelPreview,
  resolveLabelComponentFromRecords,
  staticLabelFrameContext,
} from "./labelComponentResolver.js";

export function resolveKeypadComponent(payload: DesignPreviewPayload): KeypadDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const keypad = asRecord(config.keypad);
  const sizingMode = requiredString(keypad, "sizingMode", "component.keypad.sizingMode");
  if (sizingMode !== "content" && sizingMode !== "fill") {
    throw new Error(`Unsupported keypad sizing mode ${sizingMode}`);
  }
  const columns = requiredNumber(keypad, "columns", "component.keypad.columns");
  if (!Number.isInteger(columns) || columns < 1) {
    throw new Error("component.keypad.columns must be a positive integer");
  }
  const keySizePair = requiredNumberPair(keypad, "keySize", "component.keypad.keySize");
  if (keySizePair.first <= 0 || keySizePair.second <= 0) {
    throw new Error("component.keypad.keySize must be positive");
  }
  const paddingPair = requiredStringPair(keypad, "padding", "component.keypad.padding");
  const activeKey = requiredPossiblyEmptyString(preview, "activeKey", "component.keypad.runtime.activeKey");
  const enabled = requiredBoolean(preview, "enabled", "component.keypad.runtime.enabled");
  if (!Array.isArray(keypad.keys)) {
    throw new Error("Missing component.keypad.keys collection");
  }
  const ids = new Set<string>();
  const values = new Set<string>();
  const keys = keypad.keys.map((rawKey, index) => {
    const key = asRecord(rawKey);
    const id = requiredString(key, "id", `component.keypad.keys[${index}].id`);
    if (!ids.add(id)) throw new Error(`Duplicate keypad key id ${id}`);
    const kind = keypadKeyKind(requiredString(key, "kind", `component.keypad.keys[${index}].kind`));
    const value = requiredPossiblyEmptyString(key, "value", `component.keypad.keys[${index}].value`);
    if (kind === "key" && value && !values.add(value)) {
      throw new Error(`Duplicate keypad key value ${value}`);
    }
    const text = requiredPossiblyEmptyString(key, "text", `component.keypad.keys[${index}].text`);
    const subtext = requiredPossiblyEmptyString(key, "subtext", `component.keypad.keys[${index}].subtext`);
    const disabled = requiredBoolean(key, "disabled", `component.keypad.keys[${index}].disabled`);
    const state: KeypadKeyState = !enabled || disabled
      ? "disabled"
      : activeKey && (activeKey === id || activeKey === value)
        ? "active"
        : "normal";
    return {
      id,
      kind,
      value,
      text,
      subtext,
      state,
      label: kind === "spacer"
        ? undefined
        : resolveKeyLabel(keypad, state, text, subtext, bases, index),
    };
  });

  return {
    id: "component.keypad",
    sizingMode: sizingMode as KeypadSizingMode,
    availableWidth: requiredNumber(preview, "availableWidth", "component.keypad.runtime.availableWidth"),
    columns,
    keySize: { width: keySizePair.first, height: keySizePair.second },
    padding: { xToken: paddingPair.first, yToken: paddingPair.second },
    columnGapToken: requiredString(keypad, "columnGapToken", "component.keypad.columnGapToken"),
    rowGapToken: requiredString(keypad, "rowGapToken", "component.keypad.rowGapToken"),
    keys,
  };
}

function resolveKeyLabel(
  keypad: Record<string, unknown>,
  state: KeypadKeyState,
  text: string,
  subtext: string,
  bases: Record<string, unknown>,
  index: number,
) {
  const slotKey = state === "active"
    ? "activeKeySlot"
    : state === "disabled" ? "disabledKeySlot" : "normalKeySlot";
  const slot = asRecord(keypad[slotKey]);
  const presetId = requiredString(slot, "presetId", `component.keypad.${slotKey}.presetId`);
  return resolveLabelComponentFromRecords(
    mergeComponentDefaults(
      componentPresetConfig(bases, "label", presetId),
      asRecord(slot.overrides),
    ),
    literalLabelPreview(text, subtext),
    bases,
    `component.keypad.key.${index}`,
    staticLabelFrameContext,
  );
}

function keypadKeyKind(value: string): KeypadKeyKind {
  if (value === "key" || value === "spacer") return value;
  throw new Error(`Unsupported keypad key kind ${value}`);
}
