import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  parseObject,
  requiredNumber,
  requiredNumberPair,
  requiredString,
} from "./componentResolverCommon.js";
import type {
  CodeIndicatorDesignContract,
  CodeIndicatorState,
} from "./codeIndicatorComponentContract.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

export function resolveCodeIndicatorComponent(
  payload: DesignPreviewPayload,
): CodeIndicatorDesignContract {
  return resolveCodeIndicatorComponentFromRecords(
    parseObject(payload.configJson),
    parseObject(payload.designPreviewJson),
    parseObject(payload.componentBaseConfigsJson),
    "component.codeIndicator",
  );
}

export function resolveCodeIndicatorComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  bases: Record<string, unknown>,
  id: string,
): CodeIndicatorDesignContract {
  const indicator = asRecord(config.codeIndicator);
  const count = positiveInteger(requiredNumber(inputs, "count", `${id}.runtime.count`), "count");
  const filledCount = nonNegativeInteger(
    requiredNumber(inputs, "filledCount", `${id}.runtime.filledCount`),
    "filledCount",
  );
  if (filledCount > count) throw new Error(`${id}.runtime.filledCount must not exceed count`);
  const state = codeIndicatorState(requiredString(inputs, "state", `${id}.runtime.state`));
  const size = requiredNumberPair(indicator, "glyphSize", `${id}.glyphSize`);
  if (size.first <= 0 || size.second <= 0) throw new Error(`${id}.glyphSize must be positive`);
  const stateConfig = asRecord(asRecord(indicator.states)[state]);
  return {
    id,
    count,
    filledCount,
    state,
    glyphSize: { width: size.first, height: size.second },
    gapToken: requiredString(indicator, "gapToken", `${id}.gapToken`),
    emptySurface: resolveGlyphSurface(stateConfig, "emptySurfaceSlot", size, bases, `${id}.${state}.empty`),
    filledSurface: resolveGlyphSurface(stateConfig, "filledSurfaceSlot", size, bases, `${id}.${state}.filled`),
  };
}

function resolveGlyphSurface(
  state: Record<string, unknown>,
  slotKey: string,
  size: { first: number; second: number },
  bases: Record<string, unknown>,
  id: string,
) {
  const slot = asRecord(state[slotKey]);
  const config = mergeComponentDefaults(
    componentPresetConfig(
      bases,
      "surface",
      requiredString(slot, "presetId", `${id}.presetId`),
    ),
    asRecord(slot.overrides),
  );
  return resolveSurfaceComponentAtSize(config, { width: size.first, height: size.second }, id);
}

function codeIndicatorState(value: string): CodeIndicatorState {
  if (value === "initial" || value === "correct" || value === "incorrect") return value;
  throw new Error(`Unsupported Code Indicator state ${value}`);
}

function positiveInteger(value: number, field: string) {
  if (!Number.isInteger(value) || value < 1) throw new Error(`Code Indicator ${field} must be a positive integer`);
  return value;
}

function nonNegativeInteger(value: number, field: string) {
  if (!Number.isInteger(value) || value < 0) throw new Error(`Code Indicator ${field} must be a non-negative integer`);
  return value;
}
