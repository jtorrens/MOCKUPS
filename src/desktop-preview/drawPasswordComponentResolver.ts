import { asRecord, parseObject, requiredNumber, requiredNumberPair, requiredString } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { DrawPasswordDesignContract, DrawPasswordState } from "./drawPasswordComponentContract.js";

export function resolveDrawPasswordComponent(payload: DesignPreviewPayload): DrawPasswordDesignContract {
  return resolveDrawPasswordComponentFromRecords(parseObject(payload.configJson), parseObject(payload.designPreviewJson), "component.drawPassword");
}

export function resolveDrawPasswordComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  id: string,
): DrawPasswordDesignContract {
  const draw = asRecord(config.drawPassword);
  const grid = requiredNumberPair(draw, "grid", `${id}.grid`);
  const columns = positiveInteger(grid.first, `${id}.grid.columns`);
  const rows = positiveInteger(grid.second, `${id}.grid.rows`);
  if (columns * rows > 9) throw new Error(`${id}.grid supports at most nine nodes`);
  const pattern = parsePattern(requiredString(inputs, "pattern", `${id}.runtime.pattern`), columns * rows, id);
  const visibleCount = nonNegativeInteger(requiredNumber(inputs, "visibleCount", `${id}.runtime.visibleCount`), `${id}.runtime.visibleCount`);
  if (visibleCount > pattern.length) throw new Error(`${id}.runtime.visibleCount must not exceed pattern length`);
  const state = drawState(requiredString(inputs, "state", `${id}.runtime.state`));
  const stateConfig = asRecord(asRecord(draw.states)[state]);
  const nodeSize = requiredNumber(draw, "nodeSize", `${id}.nodeSize`);
  const lineWidth = requiredNumber(draw, "lineWidth", `${id}.lineWidth`);
  if (nodeSize <= 0 || lineWidth <= 0) throw new Error(`${id} nodeSize and lineWidth must be positive`);
  return {
    id, state, pattern, visibleCount,
    grid: { columns, rows },
    nodeSize,
    columnGapToken: requiredString(draw, "columnGapToken", `${id}.columnGapToken`),
    rowGapToken: requiredString(draw, "rowGapToken", `${id}.rowGapToken`),
    lineWidth,
    nodeColorToken: requiredString(stateConfig, "nodeColorToken", `${id}.states.${state}.nodeColorToken`),
    lineColorToken: requiredString(stateConfig, "lineColorToken", `${id}.states.${state}.lineColorToken`),
  };
}

function parsePattern(value: string, nodeCount: number, id: string) {
  if (!/^\d+$/u.test(value)) throw new Error(`${id}.runtime.pattern must contain node digits only`);
  const pattern = [...value].map(Number);
  if (pattern.some((node) => node < 1 || node > nodeCount)) throw new Error(`${id}.runtime.pattern contains an unavailable node`);
  if (new Set(pattern).size !== pattern.length) throw new Error(`${id}.runtime.pattern must not repeat nodes`);
  return pattern;
}

function drawState(value: string): DrawPasswordState {
  if (value === "initial" || value === "active" || value === "correct" || value === "incorrect") return value;
  throw new Error(`Unsupported Draw Password state ${value}`);
}

function positiveInteger(value: number, path: string) {
  if (!Number.isInteger(value) || value < 1) throw new Error(`${path} must be a positive integer`);
  return value;
}

function nonNegativeInteger(value: number, path: string) {
  if (!Number.isInteger(value) || value < 0) throw new Error(`${path} must be a non-negative integer`);
  return value;
}
