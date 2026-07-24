import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { requireComponentVariantType } from "./componentPreviewDefaults.js";
import type {
  ChatListComponentSlot,
  ChatListModuleContract,
} from "./chatListModuleContract.js";
import {
  parseObject,
  requiredBoolean,
  requiredComponentVariantSlot,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";
import { requiredObjectArray } from "./previewJsonHelpers.js";

const exactInputIds = ["itemWidth", "itemHeight"] as const;
const exactCollectionIds = ["items"] as const;

export function resolveChatListModule(
  payload: DesignPreviewPayload,
): ChatListModuleContract {
  const config = parseObject(payload.configJson);
  const chatList = requiredRecord(config, "chatList", "module.core.chatList");
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const stackSlot = requiredTypedSlot(
    chatList,
    componentBaseConfigs,
    "stackSlot",
    "componentStack",
  );
  const topIconBarSlot = requiredTypedSlot(
    chatList,
    componentBaseConfigs,
    "topIconBarSlot",
    "iconBar",
  );
  const bottomIconBarSlot = requiredTypedSlot(
    chatList,
    componentBaseConfigs,
    "bottomIconBarSlot",
    "iconBar",
  );
  const listSlot = requiredTypedSlot(
    chatList,
    componentBaseConfigs,
    "listSlot",
    "list",
  );
  const statusBarSlot = requiredTypedSlot(
    chatList,
    componentBaseConfigs,
    "statusBarSlot",
    "status_bar",
  );
  const navigationBarSlot = requiredTypedSlot(
    chatList,
    componentBaseConfigs,
    "navigationBarSlot",
    "navigation_bar",
  );

  const runtimeDeclaration = requiredRecord(
    chatList,
    "runtimeContract",
    "module.core.chatList.runtimeContract",
  );
  requireExactValue(
    requiredString(runtimeDeclaration, "mode", "module.core.chatList.runtimeContract.mode"),
    "exact",
    "module.core.chatList.runtimeContract.mode",
  );
  requireExactValue(
    requiredString(
      runtimeDeclaration,
      "componentType",
      "module.core.chatList.runtimeContract.componentType",
    ),
    "list",
    "module.core.chatList.runtimeContract.componentType",
  );
  const runtimeVariantReference = requiredString(
    runtimeDeclaration,
    "variantReference",
    "module.core.chatList.runtimeContract.variantReference",
  );
  requireExactValue(
    runtimeVariantReference,
    listSlot.variantReference,
    "module.core.chatList.runtimeContract.variantReference",
  );
  requireExactStringArray(
    runtimeDeclaration,
    "inputIds",
    exactInputIds,
    "module.core.chatList.runtimeContract.inputIds",
  );
  requireExactStringArray(
    runtimeDeclaration,
    "collectionIds",
    exactCollectionIds,
    "module.core.chatList.runtimeContract.collectionIds",
  );

  const listInputs = parseObject(payload.designPreviewJson);
  const runtimeContract = parseObject(payload.runtimeContractJson);
  requireExactDeclarationIds(runtimeContract, "inputs", exactInputIds);
  requireExactDeclarationIds(runtimeContract, "collections", exactCollectionIds);
  requireExactDeclarationIds(listInputs, "inputs", exactInputIds);
  requireExactDeclarationIds(listInputs, "collections", exactCollectionIds);

  return {
    id: "module.core.chatList",
    wallpaperEnabled: requiredBoolean(
      chatList,
      "wallpaperEnabled",
      "module.core.chatList.wallpaperEnabled",
    ),
    stackSlot,
    topIconBarSlot,
    bottomIconBarSlot,
    listSlot,
    statusBarSlot,
    navigationBarSlot,
    topIconBarInputs: requiredRecord(
      chatList,
      "topIconBarInputs",
      "module.core.chatList.topIconBarInputs",
    ),
    bottomIconBarInputs: requiredRecord(
      chatList,
      "bottomIconBarInputs",
      "module.core.chatList.bottomIconBarInputs",
    ),
    listInputs,
  };
}

function requiredTypedSlot(
  owner: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  key: string,
  componentType: string,
): ChatListComponentSlot {
  const path = `module.core.chatList.${key}`;
  const slot = requiredComponentVariantSlot(owner, key, path);
  requireComponentVariantType(componentBaseConfigs, slot, componentType, path);
  return slot;
}

function requireExactDeclarationIds(
  owner: Record<string, unknown>,
  key: string,
  expected: readonly string[],
) {
  const ids = requiredObjectArray(owner, key, "module.core.chatList Runtime contract")
    .map((entry, index) => requiredString(
      entry,
      "id",
      `module.core.chatList Runtime contract.${key}[${index}].id`,
    ));
  requireExactIds(ids, expected, `module.core.chatList Runtime contract.${key}`);
}

function requireExactStringArray(
  owner: Record<string, unknown>,
  key: string,
  expected: readonly string[],
  path: string,
) {
  const raw = owner[key];
  if (!Array.isArray(raw) || !raw.every((entry) => typeof entry === "string")) {
    throw new Error(`${path} must be a string array`);
  }
  requireExactIds(raw, expected, path);
}

function requireExactIds(
  actual: readonly string[],
  expected: readonly string[],
  path: string,
) {
  if (
    actual.length !== expected.length
    || actual.some((value, index) => value !== expected[index])
  ) {
    throw new Error(`${path} must be exactly ${expected.join(", ")}`);
  }
}

function requireExactValue(actual: string, expected: string, path: string) {
  if (actual !== expected) {
    throw new Error(`${path} must be '${expected}'`);
  }
}
