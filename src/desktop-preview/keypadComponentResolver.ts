import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  parseObject,
  requiredAlpha,
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
  return resolveKeypadComponentFromRecords(
    parseObject(payload.configJson),
    parseObject(payload.designPreviewJson),
    parseObject(payload.componentBaseConfigsJson),
    "component.keypad",
  );
}

export function resolveKeypadComponentFromRecords(
  config: Record<string, unknown>,
  preview: Record<string, unknown>,
  bases: Record<string, unknown>,
  id: string,
): KeypadDesignContract {
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
  const pushedKey = requiredPossiblyEmptyString(preview, "pushedKey", "component.keypad.runtime.pushedKey");
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
    if (kind !== "spacer" && value && !values.add(value)) {
      throw new Error(`Duplicate keypad key value ${value}`);
    }
    const text = requiredPossiblyEmptyString(key, "text", `component.keypad.keys[${index}].text`);
    const subtext = requiredPossiblyEmptyString(key, "subtext", `component.keypad.keys[${index}].subtext`);
    const iconToken = requiredPossiblyEmptyString(key, "iconToken", `component.keypad.keys[${index}].iconToken`);
    if (kind === "icon" && !iconToken.trim()) {
      throw new Error(`Missing component.keypad.keys[${index}].iconToken`);
    }
    const disabled = requiredBoolean(key, "disabled", `component.keypad.keys[${index}].disabled`);
    const state: KeypadKeyState = !enabled || disabled
      ? "disabled"
      : pushedKey && (pushedKey === id || pushedKey === value)
        ? "pushed"
        : activeKey && (activeKey === id || activeKey === value)
          ? "active"
          : "normal";
    const stateStyle = kind === "spacer"
      ? undefined
      : resolveKeyStateStyle(
          keypad,
          state,
          kind === "text" ? text : "",
          kind === "text" ? subtext : "",
          bases,
          index,
        );
    return {
      id,
      kind,
      value,
      text,
      subtext,
      iconToken,
      state,
      backgroundColorToken: stateStyle?.backgroundColorToken,
      textColorToken: stateStyle?.textColorToken,
      backgroundAlpha: stateStyle?.backgroundAlpha,
      borderAlpha: stateStyle?.borderAlpha,
      label: stateStyle?.label,
    };
  });

  return {
    id,
    sizingMode: sizingMode as KeypadSizingMode,
    availableWidth: requiredNumber(preview, "availableWidth", "component.keypad.runtime.availableWidth"),
    columns,
    keySize: { width: keySizePair.first, height: keySizePair.second },
    padding: { xToken: paddingPair.first, yToken: paddingPair.second },
    columnGapToken: requiredString(keypad, "columnGapToken", "component.keypad.columnGapToken"),
    rowGapToken: requiredString(keypad, "rowGapToken", "component.keypad.rowGapToken"),
    iconSizeToken: requiredString(keypad, "iconSizeToken", "component.keypad.iconSizeToken"),
    keys,
  };
}

function resolveKeyStateStyle(
  keypad: Record<string, unknown>,
  state: KeypadKeyState,
  text: string,
  subtext: string,
  bases: Record<string, unknown>,
  index: number,
) {
  const states = asRecord(keypad.states);
  const style = asRecord(states[state]);
  const labelSlot = asRecord(keypad.labelSlot);
  return {
    backgroundColorToken: requiredString(
      style,
      "backgroundColorToken",
      `component.keypad.states.${state}.backgroundColorToken`,
    ),
    textColorToken: requiredString(
      style,
      "textColorToken",
      `component.keypad.states.${state}.textColorToken`,
    ),
    backgroundAlpha: requiredAlpha(
      style,
      "backgroundAlpha",
      `component.keypad.states.${state}.backgroundAlpha`,
    ),
    borderAlpha: requiredAlpha(
      style,
      "borderAlpha",
      `component.keypad.states.${state}.borderAlpha`,
    ),
    label: resolveLabelComponentFromRecords(
      mergeComponentDefaults(
        componentPresetConfig(
          bases,
          "label",
          requiredString(labelSlot, "presetId", "component.keypad.labelSlot.presetId"),
        ),
        asRecord(labelSlot.overrides),
      ),
      literalLabelPreview(text, subtext),
      bases,
      `component.keypad.key.${index}.label`,
      staticLabelFrameContext,
    ),
  };
}

function keypadKeyKind(value: string): KeypadKeyKind {
  if (value === "text" || value === "icon" || value === "spacer") return value;
  throw new Error(`Unsupported keypad key kind ${value}`);
}
