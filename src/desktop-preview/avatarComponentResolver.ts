import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  defaultLabelComponentConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import type { LabelDesignContract } from "./labelComponentResolver.js";
import { resolveLabelComponentFromRecords } from "./labelComponentResolver.js";

export interface AvatarDesignContract {
  id: "component.avatar";
  size: number;
  cornerRadiusToken: string;
  labelSlot: {
    showLabel: boolean;
    showSubtext: boolean;
    position: "top" | "bottom" | "left" | "right";
    gap: number;
    label?: LabelDesignContract;
  };
  surface: {
    shadowEnabled: boolean;
    reliefEnabled: boolean;
    borderWidth: number;
    borderColorToken: string;
    reliefAngle: number;
    reliefExtent: number;
    reliefSpread: number;
    reliefTopIntensity: number;
    reliefBottomIntensity: number;
  };
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function parseObject(json: string | undefined) {
  return asRecord(JSON.parse(json || "{}"));
}

function requiredString(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "string" && raw.trim()) return raw;
  throw new Error(`Missing string component value ${path}`);
}

function requiredBoolean(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "boolean") return raw;
  throw new Error(`Missing boolean component value ${path}`);
}

function requiredNumber(
  value: Record<string, unknown>,
  key: string,
  path: string,
) {
  const raw = value[key];
  if (typeof raw === "number" && Number.isFinite(raw)) return raw;
  if (typeof raw === "string") {
    const parsed = Number(raw.replace(",", "."));
    if (Number.isFinite(parsed)) return parsed;
  }
  throw new Error(`Missing numeric component value ${path}`);
}

function labelPreview(
  preview: Record<string, unknown>,
  showSubtext: boolean,
): Record<string, unknown> {
  return {
    ...preview,
    sampleSubtext: showSubtext ? preview.sampleSubtext : "",
  };
}

export function resolveAvatarComponent(
  payload: DesignPreviewPayload,
): AvatarDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const avatar = asRecord(config.avatar);
  const labelSlot = asRecord(avatar.labelSlot);
  const style = asRecord(config.style);
  const position = requiredString(
    labelSlot,
    "position",
    "component.avatar.label.position",
  );
  if (!["top", "bottom", "left", "right"].includes(position)) {
    throw new Error(`Unsupported avatar label position ${position}`);
  }

  const showLabel = requiredBoolean(
    labelSlot,
    "showLabel",
    "component.avatar.label.showLabel",
  );
  const showSubtext = requiredBoolean(
    labelSlot,
    "showSubtext",
    "component.avatar.label.showSubtext",
  );
  const overrides = asRecord(labelSlot.overrides);
  const embeddedLabelConfig = mergeComponentDefaults(
    defaultLabelComponentConfig(),
    overrides,
  );

  return {
    id: "component.avatar",
    size: requiredNumber(avatar, "defaultSize", "component.avatar.defaultSize"),
    cornerRadiusToken: requiredString(
      avatar,
      "cornerRadiusToken",
      "component.avatar.cornerRadiusToken",
    ),
    labelSlot: {
      showLabel,
      showSubtext,
      position: position as "top" | "bottom" | "left" | "right",
      gap: requiredNumber(labelSlot, "gap", "component.avatar.label.gap"),
      label: showLabel
        ? resolveLabelComponentFromRecords(
            embeddedLabelConfig,
            labelPreview(preview, showSubtext),
            "component.avatar.label",
          )
        : undefined,
    },
    surface: {
      shadowEnabled: requiredBoolean(
        style,
        "shadowEnabled",
        "component.style.shadowEnabled",
      ),
      reliefEnabled: requiredBoolean(
        style,
        "reliefEnabled",
        "component.style.reliefEnabled",
      ),
      borderWidth: requiredNumber(style, "borderWidth", "component.style.borderWidth"),
      borderColorToken: requiredString(
        style,
        "borderColorToken",
        "component.style.borderColorToken",
      ),
      reliefAngle: requiredNumber(style, "reliefAngle", "component.style.reliefAngle"),
      reliefExtent: requiredNumber(style, "reliefExtent", "component.style.reliefExtent"),
      reliefSpread: requiredNumber(style, "reliefSpread", "component.style.reliefSpread"),
      reliefTopIntensity: requiredNumber(
        style,
        "reliefTopIntensity",
        "component.style.reliefTopIntensity",
      ),
      reliefBottomIntensity: requiredNumber(
        style,
        "reliefBottomIntensity",
        "component.style.reliefBottomIntensity",
      ),
    },
  };
}
