import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import {
  asRecord,
  optionalString,
  parseObject,
  requiredNumberPair,
  requiredString,
} from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type {
  IconBarDesignContract,
  IconBarState,
  IconBarZone,
} from "./iconBarComponentContract.js";
import { resolveIconRowComponentFromRecords } from "./iconRowComponentResolver.js";

export function resolveIconBarComponent(
  payload: DesignPreviewPayload,
): IconBarDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  return resolveIconBarComponentFromRecords(
    config,
    preview,
    componentBaseConfigs,
    "component.iconBar",
  );
}

export function resolveIconBarComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
): IconBarDesignContract {
  const iconBar = asRecord(config.iconBar);
  const state = iconBarState(optionalString(inputs, "state") || "idle");
  const sizePair = requiredNumberPair(inputs, "size", "component.iconBar.input.size");
  const sizeSource = requiredString(iconBar, "sizeSource", "component.iconBar.sizeSource");
  if (sizeSource !== "shared" && sizeSource !== "perRow") throw new Error(`Unsupported icon bar size source ${sizeSource}`);
  const sharedSizes = sizeSource === "shared"
    ? {
        iconSizeToken: requiredString(iconBar, "iconSizeToken", "component.iconBar.iconSizeToken"),
        textSizeToken: requiredString(iconBar, "textSizeToken", "component.iconBar.textSizeToken"),
      }
    : undefined;

  return {
    id,
    state,
    size: {
      width: Math.max(1, sizePair.first),
      height: Math.max(1, sizePair.second),
    },
    edgePaddingToken: requiredString(
      iconBar,
      "edgePadding",
      "component.iconBar.edgePadding",
    ),
    rows: {
      left: resolveIconBarRow(
        iconBar,
        state,
        "left",
        componentBaseConfigs,
        `${id}.${state}.left`,
        sharedSizes,
      ),
      center: resolveIconBarRow(
        iconBar,
        state,
        "center",
        componentBaseConfigs,
        `${id}.${state}.center`,
        sharedSizes,
      ),
      right: resolveIconBarRow(
        iconBar,
        state,
        "right",
        componentBaseConfigs,
        `${id}.${state}.right`,
        sharedSizes,
      ),
    },
  };
}

function resolveIconBarRow(
  iconBar: Record<string, unknown>,
  state: IconBarState,
  zone: IconBarZone,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
  sharedSizes?: { iconSizeToken: string; textSizeToken: string },
) {
  const slotKey = `${state}${capitalize(zone)}IconRowSlot`;
  const inputsKey = `${state}${capitalize(zone)}IconRowInputs`;
  const slot = asRecord(iconBar[slotKey]);
  const inputs = asRecord(iconBar[inputsKey]);
  const config = mergeComponentDefaults(
    componentPresetConfig(
      componentBaseConfigs,
      "iconRow",
      requiredString(slot, "presetId", `component.iconBar.${slotKey}.presetId`),
    ),
    asRecord(slot.overrides),
  );
  return resolveIconRowComponentFromRecords(config, { ...inputs, ...sharedSizes }, componentBaseConfigs, id);
}

function iconBarState(value: string): IconBarState {
  if (value === "idle" || value === "active") return value;
  throw new Error(`Unsupported icon bar state ${value}`);
}

function capitalize(value: string) {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
