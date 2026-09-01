import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { naturalWriteOnFrame, resolveBehaviorTimingFrames } from "./behaviorTiming.js";
import { requireComponentVariantType } from "./componentPreviewDefaults.js";
import type {
  SocialPostComponentSlot,
  SocialPostRow,
  SocialPostRowSlot,
  SocialPostRowSlotKind,
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
import { optionalObject, requiredObjectArray } from "./previewJsonHelpers.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import { rootScreenFrame } from "./previewFrameContext.js";
import { RuntimeOwnerTimeline } from "./runtimeOwnerTimeline.js";
import {
  simpleWriteOnFrameVisibleCount,
  textGraphemes,
} from "./previewTextRevealHelpers.js";

export function resolveSocialPostModule(
  payload: DesignPreviewPayload,
): SocialPostModuleContract {
  const config = parseObject(payload.configJson);
  const socialPost = requiredRecord(config, "socialPost", "module.core.socialPost");
  const resolvedFrame = resolveSocialPostModuleFrame(payload);
  const preview = resolvedFrame.preview;
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const themeTokens = parseObject(payload.themeTokensJson);
  const rows = requiredRows(socialPost, "rows");
  const runtimeRows = requiredRuntimeRows(preview, "socialPostRows");
  const footerRows = requiredRows(socialPost, "footerRows");
  const footerRuntimeRows = requiredRuntimeRows(preview, "socialPostFooterRows");
  const message = resolveMessage(
    socialPost,
    preview,
    componentBaseConfigs,
    themeTokens,
    resolvedFrame.messageTextAnimated,
  );

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
      resolveRow("header", 1, rows[0], runtimeRows[0], componentBaseConfigs),
      resolveRow("header", 2, rows[1], runtimeRows[1], componentBaseConfigs),
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
    mediaScale: Math.max(0.01, requiredNumber(
      preview,
      "mediaScale",
      "module.core.socialPost.mediaScale",
    )),
    mediaOffset: requiredString(
      preview,
      "mediaOffset",
      "module.core.socialPost.mediaOffset",
    ),
    showMediaSeparator: requiredBoolean(
      socialPost,
      "showMediaSeparator",
      "module.core.socialPost.showMediaSeparator",
    ),
    message,
    footerHeight: Math.max(0, requiredNumber(
      socialPost,
      "footerHeight",
      "module.core.socialPost.footerHeight",
    )),
    footerSurfaceSlot: requiredTypedSlot(
      socialPost,
      componentBaseConfigs,
      "footerSurfaceSlot",
      "surface",
    ),
    footerRowGapToken: requiredString(
      socialPost,
      "footerRowGapToken",
      "module.core.socialPost.footerRowGapToken",
    ),
    footerRows: [
      resolveRow("footer", 1, footerRows[0], footerRuntimeRows[0], componentBaseConfigs),
      resolveRow("footer", 2, footerRows[1], footerRuntimeRows[1], componentBaseConfigs),
    ],
  };
}

function resolveSocialPostModuleFrame(payload: DesignPreviewPayload) {
  const preview = parseObject(payload.designPreviewJson);
  const instance = parseObject(payload.instanceJson);
  const animation = optionalObject(instance, "animation", "Preview instance envelope");
  const screenFrame = rootScreenFrame(payload);
  const themeTokens = parseObject(payload.themeTokensJson);
  const timeline = new RuntimeOwnerTimeline(
    preview,
    preview,
    animation,
    themeTokens,
    0,
    payload.frameRate,
  );
  const resolvedText = resolveParameterAnimation(
    animation,
    "messageText",
    "",
    timeline.temporalLocalFrame("messageText", "", screenFrame),
    preview.messageText,
  );
  preview.messageText = requiredAnimatedString(
    resolvedText.value,
    "module.core.socialPost.messageText",
  );
  for (const collectionKey of ["socialPostRows", "socialPostFooterRows"] as const) {
    preview[collectionKey] = requiredObjectArray(
      preview,
      collectionKey,
      "module.core.socialPost Runtime",
    ).map((value, index) => {
      const row = { ...value };
      const targetId = requiredString(
        row,
        "id",
        `module.core.socialPost.${collectionKey}[${index}].id`,
      );
      for (let slot = 1; slot <= 5; slot += 1) {
        resolveRowText("label", `slot${slot}Label`, slot);
        resolveRowText("sublabel", `slot${slot}Sublabel`, slot);
      }
      return row;

      function resolveRowText(
        part: "label" | "sublabel",
        jsonKey: string,
        slot: number,
      ) {
        const fieldId = `slot${slot}.${part}`;
        const resolved = resolveParameterAnimation(
          animation,
          fieldId,
          targetId,
          timeline.temporalLocalFrame(fieldId, targetId, screenFrame),
          stringValue(row[jsonKey]),
        );
        row[jsonKey] = requiredAnimatedString(
          resolved.value,
          `module.core.socialPost.${collectionKey}.${targetId}.${fieldId}`,
        );
      }
    });
  }
  return {
    preview,
    messageTextAnimated: resolvedText.animated,
  };
}

function requiredAnimatedString(value: unknown, owner: string) {
  if (typeof value === "string") return value;
  throw new Error(`${owner} animation must resolve a string`);
}

function resolveMessage(
  socialPost: Record<string, unknown>,
  preview: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  themeTokens: Record<string, unknown>,
  messageTextAnimated: boolean,
) {
  const text = requiredPossiblyEmptyString(
    preview,
    "messageText",
    "module.core.socialPost.messageText",
  );
  const inputDefinitions = requiredObjectArray(
    preview,
    "inputs",
    "module.core.socialPost Runtime contract",
  );
  const timingDefinition = inputDefinitions.find((definition) =>
    definition.id === "messageWriteOnTiming"
  );
  if (!timingDefinition) {
    throw new Error("Missing module.core.socialPost messageWriteOnTiming Runtime contract");
  }
  const durationFrames = resolveBehaviorTimingFrames(
    preview,
    timingDefinition,
    inputDefinitions,
    themeTokens,
  );
  const authoredTrigger = requiredBoolean(
    preview,
    "messageWriteOnTrigger",
    "module.core.socialPost.messageWriteOnTrigger",
  );
  const trigger = !messageTextAnimated && authoredTrigger;
  const actionFrame = Math.max(0, Math.floor(requiredNumber(
    preview,
    "messageWriteOnFrame",
    "module.core.socialPost.messageWriteOnFrame",
  )));
  const timingValue = requiredRecord(
    preview,
    "messageWriteOnTiming",
    "module.core.socialPost.messageWriteOnTiming",
  );
  const writeOnFrame = trigger
    ? naturalWriteOnFrame(
        text,
        timingValue,
        actionFrame,
        durationFrames,
        "module.core.socialPost.message",
      )
    : durationFrames;
  const graphemes = textGraphemes(text);
  const currentCharacter = trigger
    ? simpleWriteOnFrameVisibleCount(text, {
        enabled: true,
        frame: writeOnFrame,
        durationFrames,
      })
    : graphemes.length;
  const complete = !trigger || actionFrame >= durationFrames;
  const textInputVisible = requiredBoolean(
    preview,
    "messageTextInputVisible",
    "module.core.socialPost.messageTextInputVisible",
  );
  const keyboardVisible = requiredBoolean(
    preview,
    "messageKeyboardVisible",
    "module.core.socialPost.messageKeyboardVisible",
  );
  const revealMode = requiredString(
    preview,
    "messageBubbleRevealMode",
    "module.core.socialPost.messageBubbleRevealMode",
  );
  if (revealMode !== "duringWriteOn" && revealMode !== "afterWriteOn") {
    throw new Error(`Unsupported Social Post Bubble reveal mode '${revealMode}'`);
  }
  const writesInBubble = !textInputVisible;
  return {
    bubbleSlot: requiredTypedSlot(
      socialPost,
      componentBaseConfigs,
      "messageBubbleSlot",
      "bubble",
    ),
    textInputBarSlot: requiredTypedSlot(
      socialPost,
      componentBaseConfigs,
      "messageTextInputBarSlot",
      "textInputBar",
    ),
    keyboardSlot: requiredTypedSlot(
      socialPost,
      componentBaseConfigs,
      "messageKeyboardSlot",
      "keyboard",
    ),
    padding: requiredString(
      socialPost,
      "messagePadding",
      "module.core.socialPost.messagePadding",
    ),
    bubbleInputs: structuredClone(requiredRecord(
      socialPost,
      "messageBubbleInputs",
      "module.core.socialPost.messageBubbleInputs",
    )),
    text,
    visibleText: graphemes.slice(0, currentCharacter).join(""),
    writeOnDurationFrames: durationFrames,
    writeOnFrame,
    writeOnTrigger: trigger,
    currentCharacter: complete ? 0 : currentCharacter,
    textInputVisible,
    keyboardVisible,
    bubbleVisible: writesInBubble || revealMode === "duringWriteOn" || complete,
    bubbleWriteOnTrigger: trigger && !complete
      && (writesInBubble || revealMode === "duringWriteOn"),
    showSeparator: requiredBoolean(
      socialPost,
      "showMessageSeparator",
      "module.core.socialPost.showMessageSeparator",
    ),
  };
}

function requiredRuntimeRows(
  preview: Record<string, unknown>,
  key: "socialPostRows" | "socialPostFooterRows",
): [Record<string, unknown>, Record<string, unknown>] {
  const idPrefix = key === "socialPostFooterRows" ? "footerRow" : "row";
  const value = preview[key];
  if (!Array.isArray(value) || value.length !== 2) {
    throw new Error(
      `module.core.socialPost Runtime collection '${key}' must contain exactly ${idPrefix}1 and ${idPrefix}2`,
    );
  }
  const rows = value.map((item, index) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) {
      throw new Error(`module.core.socialPost ${key}[${index}] must be an object`);
    }
    const row = item as Record<string, unknown>;
    const expectedId = `${idPrefix}${index + 1}`;
    if (requiredString(row, "id", `module.core.socialPost.${key}[${index}].id`) !== expectedId) {
      throw new Error(`module.core.socialPost ${key}[${index}] must have id '${expectedId}'`);
    }
    return row;
  });
  return [rows[0]!, rows[1]!];
}

function requiredRows(
  socialPost: Record<string, unknown>,
  key: "rows" | "footerRows",
): [Record<string, unknown>, Record<string, unknown>] {
  const idPrefix = key === "footerRows" ? "footerRow" : "row";
  const value = socialPost[key];
  if (!Array.isArray(value) || value.length !== 2) {
    throw new Error(
      `module.core.socialPost.${key} must contain exactly ${idPrefix}1 and ${idPrefix}2`,
    );
  }
  const rows = value.map((item, index) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) {
      throw new Error(`module.core.socialPost.${key}[${index}] must be an object`);
    }
    const row = item as Record<string, unknown>;
    const expectedId = `${idPrefix}${index + 1}`;
    if (requiredString(row, "id", `module.core.socialPost.${key}.${expectedId}.id`) !== expectedId) {
      throw new Error(`module.core.socialPost.${key}[${index}] must have id '${expectedId}'`);
    }
    return row;
  });
  return [rows[0]!, rows[1]!];
}

function resolveRow(
  section: "header" | "footer",
  row: 1 | 2,
  rowConfig: Record<string, unknown>,
  runtimeRow: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
): SocialPostRow {
  const owner = `module.core.socialPost.${section}.row${row}`;
  const alignment = requiredString(
    rowConfig,
    "verticalAlignment",
    `${owner}.verticalAlignment`,
  );
  if (alignment !== "top" && alignment !== "center" && alignment !== "bottom") {
    throw new Error(`Unsupported Social Post row alignment '${alignment}'`);
  }
  return {
    id: `row${row}`,
    padding: requiredString(
      rowConfig,
      "padding",
      `${owner}.padding`,
    ),
    verticalAlignment: alignment as SocialPostVerticalAlignment,
    showSeparator: requiredBoolean(
      rowConfig,
      "showSeparator",
      `${owner}.showSeparator`,
    ),
    slots: [1, 2, 3, 4, 5].map((index) => resolveRowSlot(
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
  section: "header" | "footer",
  row: 1 | 2,
  index: number,
  rowConfig: Record<string, unknown>,
  runtimeRow: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
): SocialPostRowSlot {
  const owner = `module.core.socialPost.${section}.row${row}`;
  const configPrefix = `slot${index}`;
  const runtimePrefix = `slot${index}`;
  const kind = requiredString(
    rowConfig,
    `${configPrefix}Kind`,
    `${owner}.${configPrefix}Kind`,
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
    `${owner}.${runtimePrefix}Actor`,
  ));
  if (label.trim()) actor.displayName = label;
  return {
    index,
    kind: kind as SocialPostRowSlotKind,
    componentType: "avatar",
    componentSlot: avatarSlot,
    inputs: {
      actorId: requiredString(
        runtimeRow,
        `${runtimePrefix}ActorId`,
        `${owner}.${runtimePrefix}ActorId`,
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
