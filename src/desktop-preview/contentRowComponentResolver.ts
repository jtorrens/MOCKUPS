import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import {
  parseObject,
  requiredBoolean,
  requiredNumberPair,
  requiredPossiblyEmptyString,
  requiredRecord,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { resolveButtonComponentFromRecords } from "./buttonComponentResolver.js";
import { literalLabelPreview, resolveLabelComponentFromRecords } from "./labelComponentResolver.js";
import type {
  ContentRowDesignContract,
  ContentRowSlotContract,
  ContentRowSlotKind,
  ContentRowVerticalAlignment,
} from "./contentRowComponentContract.js";

export function resolveContentRowComponent(
  payload: DesignPreviewPayload,
): ContentRowDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const owner = requiredRecord(config, "contentRow", "component.contentRow");
  const rawSize = requiredNumberPair(preview, "viewportSize", "component.contentRow.runtime.viewportSize");
  const padding = requiredStringPair(owner, "padding", "component.contentRow.padding");
  const verticalAlignment = alignment(requiredString(owner, "verticalAlignment", "component.contentRow.verticalAlignment"));
  const slotConfigs = requiredObjectArray(owner, "slots", "component.contentRow.slots");
  const slotInputs = requiredObjectArray(preview, "slotInputs", "component.contentRow.runtime.slotInputs");
  const runtimeById = new Map(slotInputs.map((item, index) => [requiredString(item, "id", `component.contentRow.runtime.slotInputs[${index}].id`), item]));
  const slots = slotConfigs.map((slot, index) => {
    const id = requiredString(slot, "id", `component.contentRow.slots[${index}].id`);
    const runtime = runtimeById.get(id);
    if (!runtime) throw new Error(`component.contentRow runtime is missing slot '${id}'`);
    runtimeById.delete(id);
    return resolveSlot(payload, slot, runtime, bases, id, index);
  });
  if (runtimeById.size) throw new Error(`component.contentRow runtime has unknown slots: ${[...runtimeById.keys()].join(", ")}`);
  return {
    id: "component.contentRow",
    size: { width: positive(rawSize.first, "width"), height: positive(rawSize.second, "height") },
    padding: { xToken: padding.first, yToken: padding.second },
    verticalAlignment,
    showSeparator: requiredBoolean(owner, "showSeparator", "component.contentRow.showSeparator"),
    slots,
  };
}

function resolveSlot(
  payload: DesignPreviewPayload,
  slot: Record<string, unknown>,
  runtime: Record<string, unknown>,
  bases: Record<string, unknown>,
  id: string,
  index: number,
): ContentRowSlotContract {
  const path = `component.contentRow.slots[${index}]`;
  const kind = slotKind(requiredString(slot, "kind", `${path}.kind`));
  if (kind === "none") return { id, order: index, kind };
  const label = requiredPossiblyEmptyString(runtime, "label", `${path}.runtime.label`);
  const sublabel = requiredPossiblyEmptyString(runtime, "sublabel", `${path}.runtime.sublabel`);
  if (kind === "label") {
    return {
      id, order: index,
      kind,
      content: resolveLabelComponentFromRecords(
        embeddedComponentConfig(bases, requiredRecord(slot, "labelSlot", `${path}.labelSlot`), "label", `${path}.labelSlot`),
        literalLabelPreview(label, sublabel),
        bases,
        `${path}.label`,
        { localFrame: payload.localFrame, frameRate: payload.frameRate },
      ),
    };
  }
  if (kind === "icon") {
    const buttonConfig = embeddedComponentConfig(bases, requiredRecord(slot, "iconSlot", `${path}.iconSlot`), "button", `${path}.iconSlot`);
    const buttonOwner = requiredRecord(buttonConfig, "button", `${path}.button`);
    buttonOwner.contentMode = "icon";
    return {
      id, order: index,
      kind,
      content: resolveButtonComponentFromRecords(buttonConfig, {
        state: requiredString(runtime, "state", `${path}.runtime.state`),
        pushTrigger: false,
        sampleText: label,
        iconSizeToken: requiredString(slot, "iconSizeToken", `${path}.iconSizeToken`),
        showBadge: false,
      }, bases, `${path}.icon`),
    };
  }
  const actor = structuredClone(requiredRecord(runtime, "actor", `${path}.runtime.actor`));
  if (label.trim()) actor.displayName = label;
  return {
    id, order: index,
    kind,
    content: resolveAvatarComponentFromRecords(
      embeddedComponentConfig(bases, requiredRecord(slot, "avatarSlot", `${path}.avatarSlot`), "avatar", `${path}.avatarSlot`),
      { actor, sampleText: label, sampleSubtext: sublabel, showBadge: false },
      bases,
      `${path}.avatar`,
    ),
  };
}

function requiredObjectArray(owner: Record<string, unknown>, key: string, path: string): Record<string, unknown>[] {
  const value = owner[key];
  if (!Array.isArray(value)) throw new Error(`${path} must be an array`);
  return value.map((item, index) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) throw new Error(`${path}[${index}] must be an object`);
    return item as Record<string, unknown>;
  });
}

function slotKind(value: string): ContentRowSlotKind {
  if (value === "none" || value === "avatar" || value === "icon" || value === "label") return value;
  throw new Error(`Unsupported Content Row slot kind '${value}'`);
}

function alignment(value: string): ContentRowVerticalAlignment {
  if (value === "top" || value === "center" || value === "bottom") return value;
  throw new Error(`Unsupported Content Row vertical alignment '${value}'`);
}

function positive(value: number, name: string) {
  if (!Number.isFinite(value) || value <= 0) throw new Error(`Content Row ${name} must be greater than zero`);
  return value;
}
