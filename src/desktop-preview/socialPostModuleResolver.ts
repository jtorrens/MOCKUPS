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
  requiredNumber,
  requiredPossiblyEmptyString,
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
  const rows = requiredRows(socialPost);
  const runtimeRows = requiredRuntimeRows(preview);

  return {
    id: "module.core.socialPost",
    useAppWallpaper: requiredBoolean(
      socialPost,
      "useAppWallpaper",
      "module.core.socialPost.useAppWallpaper",
    ),
    showHeader: requiredBoolean(socialPost, "showHeader", "module.core.socialPost.showHeader"),
    headerHeight: Math.max(0, requiredNumber(
      socialPost,
      "headerHeight",
      "module.core.socialPost.headerHeight",
    )),
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
      resolveRow(1, rows[0], runtimeRows[0], componentBaseConfigs),
      resolveRow(2, rows[1], runtimeRows[1], componentBaseConfigs),
    ],
    mediaSlot: requiredTypedSlot(
      socialPost,
      componentBaseConfigs,
      "mediaSlot",
      "media",
    ),
    mediaPadding: requiredString(
      socialPost,
      "mediaPadding",
      "module.core.socialPost.mediaPadding",
    ),
    mediaInputs: structuredClone(requiredRecord(
      socialPost,
      "mediaInputs",
      "module.core.socialPost.mediaInputs",
    )),
    mediaSource: requiredPossiblyEmptyString(
      preview,
      "mediaSource",
      "module.core.socialPost.mediaSource",
    ),
    mediaHeight: Math.max(1, requiredNumber(
      preview,
      "mediaHeight",
      "module.core.socialPost.mediaHeight",
    )),
    showMediaSeparator: requiredBoolean(
      socialPost,
      "showMediaSeparator",
      "module.core.socialPost.showMediaSeparator",
    ),
  };
}

function requiredRuntimeRows(
  preview: Record<string, unknown>,
): [Record<string, unknown>, Record<string, unknown>] {
  const value = preview.socialPostRows;
  if (!Array.isArray(value) || value.length !== 2) {
    throw new Error("module.core.socialPost Runtime collection 'socialPostRows' must contain exactly row1 and row2");
  }
  const rows = value.map((item, index) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) {
      throw new Error(`module.core.socialPost socialPostRows[${index}] must be an object`);
    }
    const row = item as Record<string, unknown>;
    const expectedId = `row${index + 1}`;
    if (requiredString(row, "id", `module.core.socialPost.socialPostRows[${index}].id`) !== expectedId) {
      throw new Error(`module.core.socialPost socialPostRows[${index}] must have id '${expectedId}'`);
    }
    return row;
  });
  return [rows[0]!, rows[1]!];
}

function requiredRows(
  socialPost: Record<string, unknown>,
): [Record<string, unknown>, Record<string, unknown>] {
  const value = socialPost.rows;
  if (!Array.isArray(value) || value.length !== 2) {
    throw new Error("module.core.socialPost.rows must contain exactly row1 and row2");
  }
  const rows = value.map((item, index) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) {
      throw new Error(`module.core.socialPost.rows[${index}] must be an object`);
    }
    const row = item as Record<string, unknown>;
    const expectedId = `row${index + 1}`;
    if (requiredString(row, "id", `module.core.socialPost.${expectedId}.id`) !== expectedId) {
      throw new Error(`module.core.socialPost.rows[${index}] must have id '${expectedId}'`);
    }
    return row;
  });
  return [rows[0]!, rows[1]!];
}

function resolveRow(
  row: 1 | 2,
  rowConfig: Record<string, unknown>,
  runtimeRow: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
): SocialPostHeaderRow {
  const alignment = requiredString(
    rowConfig,
    "verticalAlignment",
    `module.core.socialPost.row${row}.verticalAlignment`,
  );
  if (alignment !== "top" && alignment !== "center" && alignment !== "bottom") {
    throw new Error(`Unsupported Social Post row alignment '${alignment}'`);
  }
  return {
    id: `row${row}`,
    padding: requiredString(
      rowConfig,
      "padding",
      `module.core.socialPost.row${row}.padding`,
    ),
    verticalAlignment: alignment as SocialPostVerticalAlignment,
    showSeparator: requiredBoolean(
      rowConfig,
      "showSeparator",
      `module.core.socialPost.row${row}.showSeparator`,
    ),
    slots: [1, 2, 3, 4, 5].map((index) => resolveHeaderSlot(
      row,
      index,
      rowConfig,
      runtimeRow,
      componentBaseConfigs,
    )),
  };
}

function resolveHeaderSlot(
  row: 1 | 2,
  index: number,
  rowConfig: Record<string, unknown>,
  runtimeRow: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
): SocialPostHeaderSlot {
  const configPrefix = `slot${index}`;
  const runtimePrefix = `slot${index}`;
  const kind = requiredString(
    rowConfig,
    `${configPrefix}Kind`,
    `module.core.socialPost.row${row}.${configPrefix}Kind`,
  );
  if (kind !== "none" && kind !== "avatar" && kind !== "icon" && kind !== "label") {
    throw new Error(`Unsupported Social Post slot kind '${kind}'`);
  }

  const avatarSlot = requiredTypedSlot(
    rowConfig,
    componentBaseConfigs,
    `${configPrefix}AvatarSlot`,
    "avatar",
  );
  const iconSlot = requiredTypedSlot(
    rowConfig,
    componentBaseConfigs,
    `${configPrefix}IconSlot`,
    "button",
  );
  const labelSlot = requiredTypedSlot(
    rowConfig,
    componentBaseConfigs,
    `${configPrefix}LabelSlot`,
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

  const actor = structuredClone(requiredRecord(
    runtimeRow,
    `${runtimePrefix}Actor`,
    `module.core.socialPost.row${row}.${runtimePrefix}Actor`,
  ));
  if (label.trim()) actor.displayName = label;
  return {
    index,
    kind: kind as SocialPostHeaderSlotKind,
    componentType: "avatar",
    componentSlot: avatarSlot,
    inputs: {
      actorId: requiredString(
        runtimeRow,
        `${runtimePrefix}ActorId`,
        `module.core.socialPost.row${row}.${runtimePrefix}ActorId`,
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
