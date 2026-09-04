import {
  requireComponentVariantType,
} from "./componentPreviewDefaults.js";
import {
  requiredBoolean,
  requiredComponentVariantSlot,
  requiredRecord,
  requiredString,
  stringValue,
} from "./componentResolverCommon.js";
import type {
  ModuleRow,
  ModuleRowComponentSlot,
  ModuleRowSlot,
  ModuleRowSlotKind,
  ModuleRowVerticalAlignment,
} from "./moduleRowSectionContract.js";
import { optionalObject } from "./previewJsonHelpers.js";

export function requiredRuntimeRows(
  preview: Record<string, unknown>,
  key: string,
  idPrefix: "row" | "footerRow",
  ownerId: string,
): [Record<string, unknown>, Record<string, unknown>] {
  const value = preview[key];
  if (!Array.isArray(value) || value.length !== 2) {
    throw new Error(
      `${ownerId} Runtime collection '${key}' must contain exactly ${idPrefix}1 and ${idPrefix}2`,
    );
  }
  const rows = value.map((item, index) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) {
      throw new Error(`${ownerId} ${key}[${index}] must be an object`);
    }
    const row = item as Record<string, unknown>;
    const expectedId = `${idPrefix}${index + 1}`;
    if (requiredString(row, "id", `${ownerId}.${key}[${index}].id`) !== expectedId) {
      throw new Error(`${ownerId} ${key}[${index}] must have id '${expectedId}'`);
    }
    return row;
  });
  return [rows[0]!, rows[1]!];
}

export function requiredRows(
  owner: Record<string, unknown>,
  key: string,
  idPrefix: "row" | "footerRow",
  ownerId: string,
): [Record<string, unknown>, Record<string, unknown>] {
  const value = owner[key];
  if (!Array.isArray(value) || value.length !== 2) {
    throw new Error(
      `${ownerId}.${key} must contain exactly ${idPrefix}1 and ${idPrefix}2`,
    );
  }
  const rows = value.map((item, index) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) {
      throw new Error(`${ownerId}.${key}[${index}] must be an object`);
    }
    const row = item as Record<string, unknown>;
    const expectedId = `${idPrefix}${index + 1}`;
    if (requiredString(row, "id", `${ownerId}.${key}.${expectedId}.id`) !== expectedId) {
      throw new Error(`${ownerId}.${key}[${index}] must have id '${expectedId}'`);
    }
    return row;
  });
  return [rows[0]!, rows[1]!];
}

export function resolveRow(
  ownerId: string,
  section: "header" | "footer",
  row: 1 | 2,
  rowConfig: Record<string, unknown>,
  runtimeRow: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
): ModuleRow {
  const owner = `${ownerId}.${section}.row${row}`;
  const alignment = requiredString(
    rowConfig,
    "verticalAlignment",
    `${owner}.verticalAlignment`,
  );
  if (alignment !== "top" && alignment !== "center" && alignment !== "bottom") {
    throw new Error(`Unsupported row alignment '${alignment}'`);
  }
  return {
    id: `row${row}`,
    padding: requiredString(rowConfig, "padding", `${owner}.padding`),
    verticalAlignment: alignment as ModuleRowVerticalAlignment,
    showSeparator: requiredBoolean(rowConfig, "showSeparator", `${owner}.showSeparator`),
    slots: [1, 2, 3, 4, 5].map((index) => resolveRowSlot(
      ownerId,
      section,
      row,
      index,
      rowConfig,
      runtimeRow,
      componentBaseConfigs,
    )),
  };
}

function resolveRowSlot(
  ownerId: string,
  section: "header" | "footer",
  row: 1 | 2,
  index: number,
  rowConfig: Record<string, unknown>,
  runtimeRow: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
): ModuleRowSlot {
  const owner = `${ownerId}.${section}.row${row}`;
  const configPrefix = `slot${index}`;
  const runtimePrefix = `slot${index}`;
  const kind = requiredString(rowConfig, `${configPrefix}Kind`, `${owner}.${configPrefix}Kind`);
  if (kind !== "none" && kind !== "avatar" && kind !== "icon" && kind !== "label") {
    throw new Error(`Unsupported module row slot kind '${kind}'`);
  }

  const avatarSlot = requiredTypedSlot(rowConfig, componentBaseConfigs, `${configPrefix}AvatarSlot`, "avatar", ownerId);
  const iconSlot = requiredTypedSlot(rowConfig, componentBaseConfigs, `${configPrefix}IconSlot`, "button", ownerId);
  const labelSlot = requiredTypedSlot(rowConfig, componentBaseConfigs, `${configPrefix}LabelSlot`, "label", ownerId);
  if (kind === "none") return { index, kind, inputs: {} };
  if (kind === "icon") {
    return {
      index,
      kind,
      componentType: "button",
      componentSlot: iconSlotWithIconContent(iconSlot),
      inputs: {
        state: "normal",
        sampleText: "",
        iconSizeToken: requiredString(rowConfig, `${configPrefix}IconSizeToken`, `${owner}.${configPrefix}IconSizeToken`),
        showBadge: false,
      },
    };
  }
  const label = stringValue(runtimeRow[`${runtimePrefix}Label`]);
  const sublabel = stringValue(runtimeRow[`${runtimePrefix}Sublabel`]);
  if (kind === "label") {
    return {
      index,
      kind,
      componentType: "label",
      componentSlot: labelSlot,
      inputs: literalLabelInputs(label, sublabel),
    };
  }

  const actor = structuredClone(requiredRecord(runtimeRow, `${runtimePrefix}Actor`, `${owner}.${runtimePrefix}Actor`));
  if (label.trim()) actor.displayName = label;
  return {
    index,
    kind: kind as ModuleRowSlotKind,
    componentType: "avatar",
    componentSlot: avatarSlot,
    inputs: {
      actorId: requiredString(runtimeRow, `${runtimePrefix}ActorId`, `${owner}.${runtimePrefix}ActorId`),
      actor,
      sampleText: label,
      sampleSubtext: sublabel,
      showBadge: false,
    },
  };
}

function iconSlotWithIconContent(slot: ModuleRowComponentSlot): ModuleRowComponentSlot {
  const buttonOverrides = optionalObject(slot.overrides, "button", "moduleRow.iconSlot.overrides");
  return {
    variantReference: slot.variantReference,
    overrides: {
      ...slot.overrides,
      button: { ...buttonOverrides, contentMode: "icon" },
    },
  };
}

function literalLabelInputs(sampleText: string, sampleSubtext: string) {
  return {
    sampleText,
    textMode: "literal",
    textSizeMultiplier: 1,
    sampleSubtext,
    subtextMode: "literal",
    subtextSizeMultiplier: 1,
  };
}

function requiredTypedSlot(
  owner: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  key: string,
  componentType: string,
  ownerId: string,
): ModuleRowComponentSlot {
  const path = `${ownerId}.${key}`;
  const componentSlot = requiredComponentVariantSlot(owner, key, path);
  requireComponentVariantType(componentBaseConfigs, componentSlot, componentType, path);
  return componentSlot;
}
