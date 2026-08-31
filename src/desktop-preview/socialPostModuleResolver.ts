import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { requireComponentVariantType } from "./componentPreviewDefaults.js";
import type {
  SocialPostComponentSlot,
  SocialPostModuleContract,
} from "./socialPostModuleContract.js";
import {
  parseObject,
  requiredBoolean,
  requiredComponentVariantSlot,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";
import { requiredObjectArray } from "./previewJsonHelpers.js";

export function resolveSocialPostModule(
  payload: DesignPreviewPayload,
): SocialPostModuleContract {
  const config = parseObject(payload.configJson);
  const socialPost = requiredRecord(config, "socialPost", "module.core.socialPost");
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const stackSlot = requiredTypedSlot(socialPost, componentBaseConfigs, "stackSlot", "componentStack");
  const headerStackSlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    "headerStackSlot",
    "collectionStack",
  );
  const headerPrimarySlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    "headerPrimarySlot",
    "listItem",
  );
  const headerSecondaryIconRowSlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    "headerSecondaryIconRowSlot",
    "iconRow",
  );
  const mediaSlot = requiredTypedSlot(socialPost, componentBaseConfigs, "mediaSlot", "media");
  const bubbleSlot = requiredTypedSlot(socialPost, componentBaseConfigs, "bubbleSlot", "bubble");
  const footerIconBarSlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    "footerIconBarSlot",
    "iconBar",
  );
  const textInputBarSlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    "textInputBarSlot",
    "textInputBar",
  );
  const keyboardSlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    "keyboardSlot",
    "keyboard",
  );
  const actor = requiredRecord(preview, "actor", "module.core.socialPost.actor");
  const actorName = requiredString(actor, "displayName", "module.core.socialPost.actor.displayName");
  const inputText = requiredString(preview, "inputText", "module.core.socialPost.inputText");

  return {
    id: "module.core.socialPost",
    useAppWallpaper: requiredBoolean(
      socialPost,
      "useAppWallpaper",
      "module.core.socialPost.useAppWallpaper",
    ),
    screenGutter: requiredString(socialPost, "screenGutter", "module.core.socialPost.screenGutter"),
    zoneGap: requiredString(socialPost, "zoneGap", "module.core.socialPost.zoneGap"),
    showHeader: requiredBoolean(socialPost, "showHeader", "module.core.socialPost.showHeader"),
    showStatusBar: requiredBoolean(socialPost, "showStatusBar", "module.core.socialPost.showStatusBar"),
    showNavigationBar: requiredBoolean(
      socialPost,
      "showNavigationBar",
      "module.core.socialPost.showNavigationBar",
    ),
    showTextInputBar: requiredBoolean(
      socialPost,
      "showTextInputBar",
      "module.core.socialPost.showTextInputBar",
    ) && requiredBoolean(preview, "textInputVisible", "module.core.socialPost.textInputVisible"),
    showKeyboard: requiredBoolean(socialPost, "showKeyboard", "module.core.socialPost.showKeyboard")
      && requiredBoolean(preview, "keyboardVisible", "module.core.socialPost.keyboardVisible"),
    stackSlot,
    headerStackSlot,
    headerPrimarySlot,
    headerSecondaryIconRowSlot,
    mediaSlot,
    bubbleSlot,
    footerIconBarSlot,
    textInputBarSlot,
    keyboardSlot,
    headerStackInputs: resolvedHeaderInputs(
      headerPrimarySlot,
      requiredRecord(socialPost, "headerPrimaryInputs", "module.core.socialPost.headerPrimaryInputs"),
      headerSecondaryIconRowSlot,
      requiredRecord(
        socialPost,
        "headerSecondaryIconRowInputs",
        "module.core.socialPost.headerSecondaryIconRowInputs",
      ),
      preview,
      actor,
      actorName,
    ),
    mediaInputs: {
      ...preview,
      motionElapsedMs: 0,
    },
    bubbleInputs: {
      ...preview,
      state: "incoming",
      actorName,
      maxWidth: 100,
      writeOnDurationFrames: 0,
      writeOnTrigger: false,
      writeOnFrame: 0,
      statusText: "",
      statusState: "none",
      mediaType: "none",
      mediaSource: "",
      isPlaying: false,
      currentTimeSeconds: 0,
      durationSeconds: 0,
      isFullScreen: false,
      fullScreenTransition: false,
      controlsElapsedMs: 0,
      motionElapsedMs: 0,
    },
    footerIconBarInputs: {
      state: "idle",
      size: "360|56",
    },
    textInputBarInputs: {
      availableWidth: 360,
      forwarded_component_textInputBar_textBox_inputs_sampleText: inputText,
    },
    keyboardInputs: {
      text: inputText,
      currentCharacter: inputText.length,
      trigger: false,
    },
  };
}

function resolvedHeaderInputs(
  primarySlot: SocialPostComponentSlot,
  authoredPrimaryInputs: Record<string, unknown>,
  secondarySlot: SocialPostComponentSlot,
  authoredSecondaryInputs: Record<string, unknown>,
  preview: Record<string, unknown>,
  actor: Record<string, unknown>,
  actorName: string,
) {
  const inputs = structuredClone(authoredPrimaryInputs);
  const avatar = requiredObjectArray(inputs, "avatarContent", "module.core.socialPost.header avatar")
    .find((item) => requiredString(item, "id", "module.core.socialPost.header avatar id")
      === "set_a_avatar");
  if (!avatar) throw new Error("module.core.socialPost header requires 'set_a_avatar'");
  const avatarInputs = requiredRecord(avatar, "runtimeInputs", "module.core.socialPost.header avatar inputs");
  avatarInputs.actorId = requiredString(preview, "actorId", "module.core.socialPost.actorId");
  avatarInputs.actor = actor;
  const label = requiredObjectArray(inputs, "labelContent", "module.core.socialPost.header label")
    .find((item) => requiredString(item, "id", "module.core.socialPost.header label id")
      === "set_a_label");
  if (label) {
    const labelInputs = requiredRecord(label, "runtimeInputs", "module.core.socialPost.header label inputs");
    labelInputs.sampleText = actorName;
  }
  const noMotion = {
    transition: "none",
    direction: "bottom",
    bounds: "parent",
    fade: false,
    translate: false,
    scale: false,
  };
  return {
    distributionMode: "flow",
    sizingMode: "content",
    startGapToken: "theme.spacing.none",
    endGapToken: "theme.spacing.s",
    stackDirection: "down",
    stackOffsetToken: "theme.spacing.none",
    itemSizingMode: "intrinsic",
    scaleRatio: 1,
    opacityRatio: 1,
    items: [
      {
        id: "social_header_primary",
        name: "Profile",
        variantReference: primarySlot.variantReference,
        overrides: primarySlot.overrides,
        inputs,
        present: true,
        presenceMotion: noMotion,
        alignment: "center",
        gapBeforeMode: "fixed",
        gapBeforeToken: "theme.spacing.none",
        gapBeforeWeight: 1,
      },
      {
        id: "social_header_info",
        name: "Tags and information",
        variantReference: secondarySlot.variantReference,
        overrides: secondarySlot.overrides,
        inputs: structuredClone(authoredSecondaryInputs),
        present: true,
        presenceMotion: noMotion,
        alignment: "start",
        gapBeforeMode: "fixed",
        gapBeforeToken: "theme.spacing.xs",
        gapBeforeWeight: 1,
      },
    ],
  };
}

function requiredTypedSlot(
  owner: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  key: string,
  componentType: string,
): SocialPostComponentSlot {
  const path = `module.core.socialPost.${key}`;
  const slot = requiredComponentVariantSlot(owner, key, path);
  requireComponentVariantType(componentBaseConfigs, slot, componentType, path);
  return slot;
}
