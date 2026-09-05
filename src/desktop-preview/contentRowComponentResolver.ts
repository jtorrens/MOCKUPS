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
  const slots = [1, 2, 3, 4, 5].map((index) => resolveSlot(payload, owner, preview, bases, index)) as ContentRowDesignContract["slots"];
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
  owner: Record<string, unknown>,
  preview: Record<string, unknown>,
  bases: Record<string, unknown>,
  index: number,
): ContentRowSlotContract {
  const prefix = `slot${index}`;
  const path = `component.contentRow.${prefix}`;
  const kind = slotKind(requiredString(owner, `${prefix}Kind`, `${path}.kind`));
  if (kind === "none") return { index, kind };
  const label = requiredPossiblyEmptyString(preview, `${prefix}Label`, `${path}.runtime.label`);
  const sublabel = requiredPossiblyEmptyString(preview, `${prefix}Sublabel`, `${path}.runtime.sublabel`);
  if (kind === "label") {
    return {
      index,
      kind,
      content: resolveLabelComponentFromRecords(
        embeddedComponentConfig(bases, requiredRecord(owner, `${prefix}LabelSlot`, `${path}.labelSlot`), "label", `${path}.labelSlot`),
        literalLabelPreview(label, sublabel),
        bases,
        `${path}.label`,
        { localFrame: payload.localFrame, frameRate: payload.frameRate },
      ),
    };
  }
  if (kind === "icon") {
    const buttonConfig = embeddedComponentConfig(bases, requiredRecord(owner, `${prefix}IconSlot`, `${path}.iconSlot`), "button", `${path}.iconSlot`);
    const buttonOwner = requiredRecord(buttonConfig, "button", `${path}.button`);
    buttonOwner.contentMode = "icon";
    return {
      index,
      kind,
      content: resolveButtonComponentFromRecords(buttonConfig, {
        state: requiredString(preview, `${prefix}State`, `${path}.runtime.state`),
        pushTrigger: false,
        sampleText: label,
        iconSizeToken: requiredString(owner, `${prefix}IconSizeToken`, `${path}.iconSizeToken`),
        showBadge: false,
      }, bases, `${path}.icon`),
    };
  }
  const actor = structuredClone(requiredRecord(preview, `${prefix}Actor`, `${path}.runtime.actor`));
  if (label.trim()) actor.displayName = label;
  return {
    index,
    kind,
    content: resolveAvatarComponentFromRecords(
      embeddedComponentConfig(bases, requiredRecord(owner, `${prefix}AvatarSlot`, `${path}.avatarSlot`), "avatar", `${path}.avatarSlot`),
      { actor, sampleText: label, sampleSubtext: sublabel, showBadge: false },
      bases,
      `${path}.avatar`,
    ),
  };
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
