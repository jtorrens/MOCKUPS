import { resolveBehaviorTimingFrames } from "./behaviorTiming.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredString,
} from "./componentResolverCommon.js";
import { resolveCodeIndicatorComponentFromRecords } from "./codeIndicatorComponentResolver.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveIconBarComponentFromRecords } from "./iconBarComponentResolver.js";
import { resolveKeypadComponentFromRecords } from "./keypadComponentResolver.js";
import { resolveFingerprintComponentFromRecords } from "./fingerprintComponentResolver.js";
import { resolveFaceRecognitionComponentFromRecords } from "./faceRecognitionComponentResolver.js";
import { resolveDrawPasswordComponentFromRecords } from "./drawPasswordComponentResolver.js";
import {
  literalLabelPreview,
  resolveLabelComponentFromRecords,
  staticLabelFrameContext,
} from "./labelComponentResolver.js";
import type { PasswordDesignContract, PasswordInputContract, PasswordMode, PasswordState } from "./passwordComponentContract.js";
import { renderScale } from "./componentRenderableCommon.js";

export function resolvePasswordComponent(payload: DesignPreviewPayload): PasswordDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const themeTokens = parseObject(payload.themeTokensJson);
  const password = asRecord(config.password);
  const mode = passwordMode(requiredString(password, "mode", "component.password.mode"));
  const expected = passwordCredential(
    requiredString(preview, "expectedPassword", "component.password.runtime.expectedPassword"),
    "expectedPassword",
    mode,
  );
  const attempt = passwordCredential(
    requiredString(preview, "attemptPassword", "component.password.runtime.attemptPassword"),
    "attemptPassword",
    mode,
  );
  if (mode === "pin" && attempt.length !== expected.length) {
    throw new Error("component.password.runtime.attemptPassword must match expectedPassword length");
  }
  const enabled = requiredBoolean(preview, "enabled", "component.password.runtime.enabled");
  const trigger = requiredBoolean(preview, "entryTrigger", "component.password.runtime.entryTrigger");
  const entryFrame = nonNegativeFrame(requiredNumber(preview, "entryFrame", "component.password.runtime.entryFrame"));
  const inputDefinitions = Array.isArray(preview.inputs) ? preview.inputs.map(asRecord) : [];
  const timingDefinition = inputDefinitions.find((definition) => definition.id === "entryTiming");
  if (!timingDefinition) throw new Error("Missing component.password entryTiming runtime contract");
  const durationFrames = resolveBehaviorTimingFrames(preview, timingDefinition, inputDefinitions, themeTokens);
  const progress = passwordProgress(enabled && trigger, entryFrame, durationFrames, attempt, expected);
  const availableWidth = payload.previewFrame.screenWidth / renderScale(payload);

  const labelSlot = asRecord(password[`${progress.state}LabelSlot`]);
  const labelConfig = embeddedConfig(labelSlot, "label", bases, `component.password.${progress.state}LabelSlot`);
  const labelText = requiredString(password, `${progress.state}Text`, `component.password.${progress.state}Text`);
  const indicatorConfig = embeddedConfig(asRecord(password.indicatorSlot), "codeIndicator", bases, "component.password.indicatorSlot");
  const input = resolvePasswordInput(mode, password, attempt, progress, enabled, availableWidth, bases);
  const iconBarHeight = requiredNumber(password, "iconBarHeight", "component.password.iconBarHeight");
  if (iconBarHeight <= 0) throw new Error("component.password.iconBarHeight must be positive");

  return {
    id: "component.password",
    mode,
    state: progress.state,
    durationFrames,
    filledCount: progress.filledCount,
    pushedKey: progress.pushedKey,
    upperAnchor: verticalAnchor(password, "upperAnchor"),
    lowerAnchor: verticalAnchor(password, "lowerAnchor"),
    labelIndicatorGapToken: requiredString(password, "labelIndicatorGapToken", "component.password.labelIndicatorGapToken"),
    startGapToken: requiredString(password, "startGapToken", "component.password.startGapToken"),
    upperGapToken: requiredString(password, "upperGapToken", "component.password.upperGapToken"),
    lowerGapToken: requiredString(password, "lowerGapToken", "component.password.lowerGapToken"),
    endGapToken: requiredString(password, "endGapToken", "component.password.endGapToken"),
    label: resolveLabelComponentFromRecords(
      labelConfig,
      literalLabelPreview(labelText, ""),
      bases,
      `component.password.label.${progress.state}`,
      staticLabelFrameContext,
    ),
    indicator: resolveCodeIndicatorComponentFromRecords(
      indicatorConfig,
      { count: expected.length, filledCount: Math.min(expected.length, progress.filledCount), state: progress.state },
      bases,
      "component.password.indicator",
    ),
    input,
    iconBar: resolveIconBarComponentFromRecords(
      embeddedConfig(asRecord(password.iconBarSlot), "iconBar", bases, "component.password.iconBarSlot"),
      { state: progress.state === "initial" ? "idle" : "active", size: `${availableWidth}|${iconBarHeight}` },
      bases,
      "component.password.iconBar",
    ),
  };
}

function resolvePasswordInput(
  mode: PasswordMode,
  password: Record<string, unknown>,
  attempt: string,
  progress: ReturnType<typeof passwordProgress>,
  enabled: boolean,
  availableWidth: number,
  bases: Record<string, unknown>,
): PasswordInputContract {
  const recognitionState = progress.playing && progress.state === "initial" ? "active" : progress.state;
  if (mode === "fingerprint") {
    return {
      kind: "fingerprint",
      component: resolveFingerprintComponentFromRecords(
        embeddedConfig(asRecord(password.fingerprintSlot), "fingerprint", bases, "component.password.fingerprintSlot"),
        { state: recognitionState, progress: progress.progress },
        "component.password.fingerprint",
      ),
    };
  }
  if (mode === "faceRecognition") {
    return {
      kind: "faceRecognition",
      component: resolveFaceRecognitionComponentFromRecords(
        embeddedConfig(asRecord(password.faceRecognitionSlot), "faceRecognition", bases, "component.password.faceRecognitionSlot"),
        { state: recognitionState, progress: progress.progress },
        "component.password.faceRecognition",
      ),
    };
  }
  if (mode === "drawPassword") {
    return {
      kind: "drawPassword",
      component: resolveDrawPasswordComponentFromRecords(
        embeddedConfig(asRecord(password.drawPasswordSlot), "drawPassword", bases, "component.password.drawPasswordSlot"),
        { state: recognitionState, pattern: attempt, visibleCount: progress.visibleCount },
        "component.password.drawPassword",
      ),
    };
  }
  const keypad = resolveKeypadComponentFromRecords(
    embeddedConfig(asRecord(password.keypadSlot), "keypad", bases, "component.password.keypadSlot"),
    { availableWidth, activeKey: "", pushedKey: progress.pushedKey, enabled },
    bases,
    "component.password.keypad",
  );
  for (const digit of attempt) {
    const key = keypad.keys.find((candidate) => candidate.kind !== "spacer" && candidate.value === digit);
    if (!key || key.state === "disabled") throw new Error(`component.password attempt digit '${digit}' has no enabled Keypad key`);
  }
  return { kind: "keypad", component: keypad };
}

function verticalAnchor(password: Record<string, unknown>, key: string) {
  const value = requiredString(password, key, `component.password.${key}`);
  if (value !== "container" && value !== "input") {
    throw new Error(`component.password.${key} must be container or input`);
  }
  return value;
}

function embeddedConfig(
  slot: Record<string, unknown>,
  componentType: string,
  bases: Record<string, unknown>,
  path: string,
) {
  return mergeComponentDefaults(
    componentPresetConfig(bases, componentType, requiredString(slot, "presetId", `${path}.presetId`)),
    asRecord(slot.overrides),
  );
}

export function passwordProgress(
  playing: boolean,
  frame: number,
  durationFrames: number,
  attempt: string,
  expected: string,
): { state: PasswordState; filledCount: number; visibleCount: number; pushedKey: string; progress: number; playing: boolean } {
  if (!playing) return { state: "initial", filledCount: 0, visibleCount: 0, pushedKey: "", progress: 0, playing: false };
  if (durationFrames <= 0 || frame >= durationFrames) {
    return {
      state: attempt === expected ? "correct" : "incorrect",
      filledCount: attempt.length,
      visibleCount: attempt.length,
      pushedKey: "",
      progress: 1,
      playing: true,
    };
  }
  const digitIndex = Math.min(attempt.length - 1, Math.floor(frame * attempt.length / durationFrames));
  return {
    state: "initial",
    filledCount: digitIndex,
    visibleCount: Math.min(attempt.length, digitIndex + 1),
    pushedKey: attempt[digitIndex] ?? "",
    progress: Math.max(0, Math.min(1, frame / durationFrames)),
    playing: true,
  };
}

function passwordCredential(value: string, field: string, mode: PasswordMode) {
  if (!value) throw new Error(`component.password.runtime.${field} must not be empty`);
  if ((mode === "pin" || mode === "drawPassword") && !/^\d+$/u.test(value)) {
    throw new Error(`component.password.runtime.${field} must contain digits only`);
  }
  if (mode === "drawPassword") {
    const nodes = [...value].map(Number);
    if (nodes.some((node) => node < 1 || node > 9)) throw new Error(`component.password.runtime.${field} contains an unavailable Draw Password node`);
    if (new Set(nodes).size !== nodes.length) throw new Error(`component.password.runtime.${field} must not repeat Draw Password nodes`);
  }
  return value;
}

function passwordMode(value: string): PasswordMode {
  if (value === "pin" || value === "fingerprint" || value === "faceRecognition" || value === "drawPassword") return value;
  throw new Error(`Unsupported Password mode ${value}`);
}

function nonNegativeFrame(value: number) {
  if (!Number.isFinite(value) || value < 0) throw new Error("component.password.runtime.entryFrame must be non-negative");
  return Math.floor(value);
}
