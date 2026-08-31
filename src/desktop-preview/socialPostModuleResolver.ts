import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { requireComponentVariantType } from "./componentPreviewDefaults.js";
import type {
  SocialPostComponentSlot,
  SocialPostHeaderRow,
  SocialPostHeaderSlot,
  SocialPostHeaderSlotKind,
  SocialPostModuleContract,
  SocialPostVerticalAlignment,
} from "./socialPostModuleContract.js";
import {
  parseObject,
  requiredBoolean,
  requiredComponentVariantSlot,
  requiredRecord,
  requiredString,
  stringValue,
} from "./componentResolverCommon.js";

export function resolveSocialPostModule(
  payload: DesignPreviewPayload,
): SocialPostModuleContract {
  const config = parseObject(payload.configJson);
  const socialPost = requiredRecord(config, "socialPost", "module.core.socialPost");
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);

  return {
    id: "module.core.socialPost",
    useAppWallpaper: requiredBoolean(
      socialPost,
      "useAppWallpaper",
      "module.core.socialPost.useAppWallpaper",
    ),
    showHeader: requiredBoolean(socialPost, "showHeader", "module.core.socialPost.showHeader"),
    showStatusBar: requiredBoolean(
      socialPost,
      "showStatusBar",
      "module.core.socialPost.showStatusBar",
    ),
    showNavigationBar: requiredBoolean(
      socialPost,
      "showNavigationBar",
      "module.core.socialPost.showNavigationBar",
    ),
    headerSurfaceSlot: requiredTypedSlot(
      socialPost,
      componentBaseConfigs,
      "headerSurfaceSlot",
      "surface",
    ),
    rowGapToken: requiredString(
      socialPost,
      "rowGapToken",
      "module.core.socialPost.rowGapToken",
    ),
    rows: [
      resolveRow(1, socialPost, preview, componentBaseConfigs),
      resolveRow(2, socialPost, preview, componentBaseConfigs),
    ],
  };
}

function resolveRow(
  row: 1 | 2,
  socialPost: Record<string, unknown>,
  preview: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
): SocialPostHeaderRow {
  const alignment = requiredString(
    socialPost,
    `row${row}VerticalAlignment`,
    `module.core.socialPost.row${row}VerticalAlignment`,
  );
  if (alignment !== "top" && alignment !== "center" && alignment !== "bottom") {
    throw new Error(`Unsupported Social Post row alignment '${alignment}'`);
  }
  return {
    id: `row${row}`,
    padding: requiredString(
      socialPost,
      `row${row}Padding`,
      `module.core.socialPost.row${row}Padding`,
    ),
    verticalAlignment: alignment as SocialPostVerticalAlignment,
    showSeparator: requiredBoolean(
      socialPost,
      `row${row}ShowSeparator`,
      `module.core.socialPost.row${row}ShowSeparator`,
    ),
    slots: [1, 2, 3, 4, 5].map((index) => resolveHeaderSlot(
      row,
      index,
      socialPost,
      preview,
      componentBaseConfigs,
    )),
  };
}

function resolveHeaderSlot(
  row: 1 | 2,
  index: number,
  socialPost: Record<string, unknown>,
  preview: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
): SocialPostHeaderSlot {
  const prefix = `row${row}Slot${index}`;
  const kind = requiredString(
    socialPost,
    `${prefix}Kind`,
    `module.core.socialPost.${prefix}Kind`,
  );
  if (kind !== "none" && kind !== "avatar" && kind !== "icon" && kind !== "label") {
    throw new Error(`Unsupported Social Post slot kind '${kind}'`);
  }

  const avatarSlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    `${prefix}AvatarSlot`,
    "avatar",
  );
  const iconSlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    `${prefix}IconSlot`,
    "button",
  );
  const labelSlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    `${prefix}LabelSlot`,
    "label",
  );
  if (kind === "none") return { index, kind, inputs: {} };
  if (kind === "icon") {
    return {
      index,
      kind,
      componentType: "button",
      componentSlot: iconSlot,
      inputs: {
        state: "normal",
        sampleText: "",
        iconSizeToken: "theme.iconSizes.m",
        showBadge: false,
      },
    };
  }
  const label = stringValue(preview[`${prefix}Label`]);
  const sublabel = stringValue(preview[`${prefix}Sublabel`]);
  if (kind === "label") {
    return {
      index,
      kind,
      componentType: "label",
      componentSlot: labelSlot,
      inputs: literalLabelInputs(label, sublabel),
    };
  }

  const actor = structuredClone(requiredRecord(
    preview,
    `${prefix}Actor`,
    `module.core.socialPost.${prefix}Actor`,
  ));
  if (label.trim()) actor.displayName = label;
  return {
    index,
    kind: kind as SocialPostHeaderSlotKind,
    componentType: "avatar",
    componentSlot: avatarSlot,
    inputs: {
      actorId: requiredString(
        preview,
        `${prefix}ActorId`,
        `module.core.socialPost.${prefix}ActorId`,
      ),
      actor,
      sampleText: label,
      sampleSubtext: sublabel,
      showBadge: false,
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
): SocialPostComponentSlot {
  const path = `module.core.socialPost.${key}`;
  const componentSlot = requiredComponentVariantSlot(owner, key, path);
  requireComponentVariantType(componentBaseConfigs, componentSlot, componentType, path);
  return componentSlot;
}
