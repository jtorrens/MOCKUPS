import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { naturalWriteOnFrame, resolveBehaviorTimingFrames } from "./behaviorTiming.js";
import {
  embeddedComponentConfig,
  requireComponentVariantType,
} from "./componentPreviewDefaults.js";
import type {
  SocialPostComponentSlot,
  SocialPostModuleContract,
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
import { requiredRows, requiredRuntimeRows, resolveRow } from "./moduleRowSectionResolver.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import { rootScreenFrame } from "./previewFrameContext.js";
import { RuntimeOwnerTimeline } from "./runtimeOwnerTimeline.js";
import { projectMediaDirectorySources } from "./projectMediaDirectorySources.js";
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
  const rows = requiredRows(socialPost, "rows", "row", "module.core.socialPost");
  const runtimeRows = requiredRuntimeRows(preview, "socialPostRows", "row", "module.core.socialPost");
  const footerRows = requiredRows(socialPost, "footerRows", "footerRow", "module.core.socialPost");
  const footerRuntimeRows = requiredRuntimeRows(preview, "socialPostFooterRows", "footerRow", "module.core.socialPost");
  const message = resolveMessage(
    socialPost,
    preview,
    componentBaseConfigs,
    themeTokens,
    resolvedFrame.messageTextAnimated,
  );
  const showGallery = requiredBoolean(
    preview,
    "showGallery",
    "module.core.socialPost.showGallery",
  );
  const galleryDirectory = requiredPossiblyEmptyString(
    preview,
    "galleryDirectory",
    "module.core.socialPost.galleryDirectory",
  );
  const gallerySelectedIndex = Math.max(0, requiredNumber(
    preview,
    "gallerySelectedIndex",
    "module.core.socialPost.gallerySelectedIndex",
  ));
  const gallerySources = projectMediaDirectorySources(
    payload.projectMediaFiles ?? [],
    galleryDirectory,
    payload.projectMediaRoot ?? "",
  );
  const effectiveGallerySelectedIndex = gallerySources.length > 0
    ? Math.min(gallerySelectedIndex, gallerySources.length - 1)
    : 0;
  const gallerySlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    "gallerySlot",
    "gallery",
  );
  const galleryConfig = requiredRecord(
    embeddedComponentConfig(
      componentBaseConfigs,
      gallerySlot,
      "gallery",
      "module.core.socialPost.gallerySlot",
    ),
    "gallery",
    "module.core.socialPost.gallerySlot.gallery",
  );
  const galleryModeValue = requiredString(
    galleryConfig,
    "mode",
    "module.core.socialPost.gallerySlot.gallery.mode",
  );
  if (galleryModeValue !== "carousel" && galleryModeValue !== "gallery") {
    throw new Error(`Unsupported Gallery mode '${galleryModeValue}'`);
  }

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
      resolveRow("module.core.socialPost", "header", 1, rows[0], runtimeRows[0], componentBaseConfigs),
      resolveRow("module.core.socialPost", "header", 2, rows[1], runtimeRows[1], componentBaseConfigs),
    ],
    showMedia: requiredBoolean(
      socialPost,
      "showMedia",
      "module.core.socialPost.showMedia",
    ),
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
    mediaSources: gallerySources,
    mediaHeightMode: requiredMediaHeightMode(socialPost),
    mediaHeight: Math.max(1, requiredNumber(
      socialPost,
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
    mediaIsPlaying: requiredBoolean(
      preview,
      "isPlaying",
      "module.core.socialPost.isPlaying",
    ),
    mediaCurrentTimeSeconds: Math.max(0, requiredNumber(
      preview,
      "currentTimeSeconds",
      "module.core.socialPost.currentTimeSeconds",
    )),
    mediaDurationSeconds: Math.max(0, requiredNumber(
      preview,
      "durationSeconds",
      "module.core.socialPost.durationSeconds",
    )),
    mediaIsFullScreen: requiredBoolean(
      preview,
      "isFullScreen",
      "module.core.socialPost.isFullScreen",
    ),
    mediaFullScreenTransition: requiredBoolean(
      preview,
      "fullScreenTransition",
      "module.core.socialPost.fullScreenTransition",
    ),
    mediaMotionElapsedMs: Math.max(0, requiredNumber(
      preview,
      "motionElapsedMs",
      "module.core.socialPost.motionElapsedMs",
    )),
    showMediaSeparator: requiredBoolean(
      socialPost,
      "showMediaSeparator",
      "module.core.socialPost.showMediaSeparator",
    ),
    showGallery,
    gallerySlot,
    showGallerySeparator: requiredBoolean(
      socialPost,
      "showGallerySeparator",
      "module.core.socialPost.showGallerySeparator",
    ),
    galleryMode: galleryModeValue,
    galleryDirectory,
    gallerySelectedIndex: effectiveGallerySelectedIndex,
    galleryScrollRow: Math.max(0, requiredNumber(
      preview,
      "galleryScrollRow",
      "module.core.socialPost.galleryScrollRow",
    )),
    messageMinHeight: Math.max(1, requiredNumber(
      socialPost,
      "messageMinHeight",
      "module.core.socialPost.messageMinHeight",
    )),
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
      resolveRow("module.core.socialPost", "footer", 1, footerRows[0], footerRuntimeRows[0], componentBaseConfigs),
      resolveRow("module.core.socialPost", "footer", 2, footerRows[1], footerRuntimeRows[1], componentBaseConfigs),
    ],
  };
}

function requiredMediaHeightMode(socialPost: Record<string, unknown>): "fixed" | "fill" {
  const value = requiredString(
    socialPost,
    "mediaHeightMode",
    "module.core.socialPost.mediaHeightMode",
  );
  if (value === "fixed" || value === "fill") return value;
  throw new Error(`Unsupported Social Post Media height mode '${value}'`);
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
  for (const fieldId of [
    "showGallery",
    "gallerySelectedIndex",
    "galleryScrollRow",
    "mediaScale",
    "mediaOffset",
    "messageKeyboardVisible",
  ] as const) {
    const resolved = resolveParameterAnimation(
      animation,
      fieldId,
      "",
      timeline.temporalLocalFrame(fieldId, "", screenFrame),
      preview[fieldId],
      fieldId === "mediaOffset" ? "integerPair" : undefined,
    );
    preview[fieldId] = resolved.value;
  }
  const playing = resolveParameterAnimation(
    animation,
    "isPlaying",
    "",
    timeline.temporalLocalFrame("isPlaying", "", screenFrame),
    preview.isPlaying,
  );
  preview.isPlaying = playing.value;
  if (playing.animated
      && playing.value === true
      && playing.sourceKeyframeFrame !== undefined) {
    const elapsedSeconds = Math.max(
      0,
      timeline.temporalLocalFrame("isPlaying", "", screenFrame)
        - playing.sourceKeyframeFrame,
    ) / Math.max(1, payload.frameRate);
    const durationSeconds = Math.max(
      0,
      requiredNumber(preview, "durationSeconds", "module.core.socialPost.durationSeconds"),
    );
    preview.currentTimeSeconds = Math.min(elapsedSeconds, durationSeconds);
    preview.isPlaying = durationSeconds > 0 && elapsedSeconds < durationSeconds;
  }
  const fullScreen = resolveParameterAnimation(
    animation,
    "isFullScreen",
    "",
    timeline.temporalLocalFrame("isFullScreen", "", screenFrame),
    preview.isFullScreen,
  );
  preview.isFullScreen = fullScreen.value;
  const fullScreenChanged = fullScreen.sourceKeyframeFrame !== undefined
    && typeof fullScreen.previousValue === "boolean"
    && typeof fullScreen.value === "boolean"
    && fullScreen.previousValue !== fullScreen.value;
  if (fullScreenChanged) {
    preview.fullScreenTransition = true;
    preview.motionElapsedMs = Math.max(
      0,
      timeline.temporalLocalFrame("isFullScreen", "", screenFrame)
        - fullScreen.sourceKeyframeFrame!,
    ) / Math.max(1, payload.frameRate) * 1000;
  }
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
    show: requiredBoolean(
      socialPost,
      "showMessage",
      "module.core.socialPost.showMessage",
    ),
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

function requiredTypedSlot(
  owner: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  key: string,
  componentType: string,
  ownerId = "module.core.socialPost",
): SocialPostComponentSlot {
  const path = `${ownerId}.${key}`;
  const componentSlot = requiredComponentVariantSlot(owner, key, path);
  requireComponentVariantType(componentBaseConfigs, componentSlot, componentType, path);
  return componentSlot;
}
