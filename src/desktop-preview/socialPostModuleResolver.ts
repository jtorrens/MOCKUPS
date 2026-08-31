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
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const stackSlot = requiredTypedSlot(socialPost, componentBaseConfigs, "stackSlot", "componentStack");
  const headerStackSlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    "headerStackSlot",
    "collectionStack",
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
  const statusBarSlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    "statusBarSlot",
    "status_bar",
  );
  const navigationBarSlot = requiredTypedSlot(
    socialPost,
    componentBaseConfigs,
    "navigationBarSlot",
    "navigation_bar",
  );
  const runtimeDeclaration = requiredRecord(
    socialPost,
    "runtimeContract",
    "module.core.socialPost.runtimeContract",
  );
  requireExactValue(
    requiredString(runtimeDeclaration, "mode", "module.core.socialPost.runtimeContract.mode"),
    "exact",
    "module.core.socialPost.runtimeContract.mode",
  );
  requireExactValue(
    requiredString(
      runtimeDeclaration,
      "componentType",
      "module.core.socialPost.runtimeContract.componentType",
    ),
    "bubble",
    "module.core.socialPost.runtimeContract.componentType",
  );
  requireExactValue(
    requiredString(
      runtimeDeclaration,
      "variantReference",
      "module.core.socialPost.runtimeContract.variantReference",
    ),
    bubbleSlot.variantReference,
    "module.core.socialPost.runtimeContract.variantReference",
  );
  const bubbleInputs = parseObject(payload.designPreviewJson);
  const runtimeContract = parseObject(payload.runtimeContractJson);
  requireExactDeclarationIds(runtimeDeclaration, runtimeContract, bubbleInputs, "inputs");
  requireExactDeclarationIds(runtimeDeclaration, runtimeContract, bubbleInputs, "collections");

  return {
    id: "module.core.socialPost",
    wallpaperEnabled: requiredBoolean(socialPost, "wallpaperEnabled", "module.core.socialPost.wallpaperEnabled"),
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
    ),
    showKeyboard: requiredBoolean(socialPost, "showKeyboard", "module.core.socialPost.showKeyboard"),
    stackSlot,
    headerStackSlot,
    mediaSlot,
    bubbleSlot,
    footerIconBarSlot,
    textInputBarSlot,
    keyboardSlot,
    statusBarSlot,
    navigationBarSlot,
    headerStackInputs: forwardHeaderActor(
      requiredRecord(socialPost, "headerStackInputs", "module.core.socialPost.headerStackInputs"),
      requiredRecord(socialPost, "forwarding", "module.core.socialPost.forwarding"),
      bubbleInputs,
    ),
    mediaInputs: {
      ...requiredRecord(socialPost, "mediaInputs", "module.core.socialPost.mediaInputs"),
      motionElapsedMs: 0,
    },
    bubbleInputs,
    footerIconBarInputs: requiredRecord(
      socialPost,
      "footerIconBarInputs",
      "module.core.socialPost.footerIconBarInputs",
    ),
    textInputBarInputs: requiredRecord(
      socialPost,
      "textInputBarInputs",
      "module.core.socialPost.textInputBarInputs",
    ),
    keyboardInputs: requiredRecord(socialPost, "keyboardInputs", "module.core.socialPost.keyboardInputs"),
  };
}

function forwardHeaderActor(
  authoredHeaderInputs: Record<string, unknown>,
  forwarding: Record<string, unknown>,
  runtimeValues: Record<string, unknown>,
) {
  const declaration = requiredRecord(
    forwarding,
    "headerActor",
    "module.core.socialPost.forwarding.headerActor",
  );
  const sourceInputId = requiredExactString(
    declaration,
    "sourceInputId",
    "actorId",
    "module.core.socialPost.forwarding.headerActor.sourceInputId",
  );
  const sourceResolvedJsonKey = requiredExactString(
    declaration,
    "sourceResolvedJsonKey",
    "actor",
    "module.core.socialPost.forwarding.headerActor.sourceResolvedJsonKey",
  );
  const targetItemId = requiredExactString(
    declaration,
    "targetItemId",
    "social_header_primary",
    "module.core.socialPost.forwarding.headerActor.targetItemId",
  );
  const targetContentSetId = requiredExactString(
    declaration,
    "targetContentSetId",
    "set_a",
    "module.core.socialPost.forwarding.headerActor.targetContentSetId",
  );
  const targetContentId = requiredExactString(
    declaration,
    "targetContentId",
    "set_a_avatar",
    "module.core.socialPost.forwarding.headerActor.targetContentId",
  );
  const targetInputJsonKey = requiredExactString(
    declaration,
    "targetInputJsonKey",
    "actorId",
    "module.core.socialPost.forwarding.headerActor.targetInputJsonKey",
  );
  const targetResolvedJsonKey = requiredExactString(
    declaration,
    "targetResolvedJsonKey",
    "actor",
    "module.core.socialPost.forwarding.headerActor.targetResolvedJsonKey",
  );
  const inputDefinitions = requiredObjectArray(
    runtimeValues,
    "inputs",
    "module.core.socialPost Runtime values",
  );
  if (!inputDefinitions.some((definition) => requiredString(
    definition,
    "id",
    "module.core.socialPost Runtime input id",
  ) === sourceInputId)) {
    throw new Error(`module.core.socialPost forwarding source '${sourceInputId}' is undeclared`);
  }
  const actorId = requiredString(
    runtimeValues,
    sourceInputId,
    `module.core.socialPost Runtime value '${sourceInputId}'`,
  );
  const actor = requiredRecord(
    runtimeValues,
    sourceResolvedJsonKey,
    `module.core.socialPost Runtime value '${sourceResolvedJsonKey}'`,
  );
  const resolved = structuredClone(authoredHeaderInputs);
  const item = requiredObjectArray(resolved, "items", "module.core.socialPost.headerStackInputs")
    .find((candidate) => requiredString(
      candidate,
      "id",
      "module.core.socialPost.headerStackInputs item id",
    ) === targetItemId);
  if (!item) throw new Error(`module.core.socialPost forwarding target item '${targetItemId}' is missing`);
  const inputs = requiredRecord(item, "inputs", `module.core.socialPost header item '${targetItemId}'`);
  const avatarContent = requiredObjectArray(
    inputs,
    "avatarContent",
    `module.core.socialPost header item '${targetItemId}' inputs`,
  ).find((candidate) => requiredString(
    candidate,
    "id",
    "module.core.socialPost header avatar content id",
  ) === targetContentId && requiredString(
    candidate,
    "contentSetId",
    "module.core.socialPost header avatar content set id",
  ) === targetContentSetId);
  if (!avatarContent) {
    throw new Error(`module.core.socialPost forwarding target '${targetContentId}' is missing`);
  }
  const targetInputs = requiredRecord(
    avatarContent,
    "runtimeInputs",
    `module.core.socialPost header avatar '${targetContentId}'`,
  );
  targetInputs[targetInputJsonKey] = actorId;
  targetInputs[targetResolvedJsonKey] = actor;
  return resolved;
}

function requiredExactString(
  owner: Record<string, unknown>,
  key: string,
  expected: string,
  path: string,
) {
  const value = requiredString(owner, key, path);
  requireExactValue(value, expected, path);
  return value;
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

function requireExactDeclarationIds(
  declaration: Record<string, unknown>,
  runtimeContract: Record<string, unknown>,
  runtimeValues: Record<string, unknown>,
  key: "inputs" | "collections",
) {
  const declarationKey = key === "inputs" ? "inputIds" : "collectionIds";
  const path = `module.core.socialPost.runtimeContract.${declarationKey}`;
  const declared = declaration[declarationKey];
  if (!Array.isArray(declared) || !declared.every((value) => typeof value === "string")) {
    throw new Error(`${path} must be a string array`);
  }
  const contractIds = requiredObjectArray(runtimeContract, key, "module.core.socialPost Runtime contract")
    .map((entry, index) => requiredString(
      entry,
      "id",
      `module.core.socialPost Runtime contract.${key}[${index}].id`,
    ));
  const valueIds = requiredObjectArray(runtimeValues, key, "module.core.socialPost Runtime values")
    .map((entry, index) => requiredString(
      entry,
      "id",
      `module.core.socialPost Runtime values.${key}[${index}].id`,
    ));
  requireExactIds(declared, contractIds, path);
  requireExactIds(valueIds, contractIds, `module.core.socialPost Runtime values.${key}`);
}

function requireExactIds(actual: readonly string[], expected: readonly string[], path: string) {
  if (actual.length !== expected.length || actual.some((value, index) => value !== expected[index])) {
    throw new Error(`${path} must be exactly ${expected.join(", ")}`);
  }
}

function requireExactValue(actual: string, expected: string, path: string) {
  if (actual !== expected) throw new Error(`${path} must be '${expected}'`);
}
