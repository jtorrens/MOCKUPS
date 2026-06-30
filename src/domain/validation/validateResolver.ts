import shotExample from "../../../docs/examples/shot_chat.json" with {
  type: "json",
};
import { loadExampleRepository } from "../repository/fixtureLoader.js";
import {
  resolveShot,
} from "../resolvers/index.js";
import {
  ChatModuleDataSchema,
  ResolvedChatScreenPropsSchema,
} from "../schemas/index.js";

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) {
    throw new Error(message);
  }
}

const repository = loadExampleRepository();
const chatScreenInstance = repository
  .getScreenInstancesForShot(shotExample.shot.id)
  .find((instance) => instance.screen_type === "chat");
assert(chatScreenInstance, "Fixture must contain a Chat screen instance");
const chatModuleInstance = repository.getPrimaryModuleInstanceForScreenInstance(
  chatScreenInstance.id,
);
assert(chatModuleInstance, "Fixture must contain a Chat module instance");
assert(
  chatModuleInstance.module_id === "core.chat" &&
    chatModuleInstance.module_schema_version === 1,
  "Chat must use core.chat module schema version 1",
);
const canonicalChatData = ChatModuleDataSchema.parse(
  chatModuleInstance.content_json,
);
assert(
  chatScreenInstance.data_ref_json === null,
  "Canonical Chat must not use data_ref_json",
);
assert(
  repository.getConversation("conversation_alex_sam") === undefined &&
    repository.getMessagesForConversation("conversation_alex_sam").length ===
      0,
  "Canonical Chat fixture must resolve without legacy conversation/message records",
);
const commonInput = {
  repository,
  productionId: shotExample.production_id,
  shotId: shotExample.shot.id,
};

const writeOnFrame = resolveShot({ ...commonInput, shotFrame: 60 });
const chatInstance = writeOnFrame.active_screen_instances.find(
  (screen) => screen.screen_type === "chat",
);
assert(chatInstance?.resolved_props, "Frame 60 must resolve chat props");
const chatProps = ResolvedChatScreenPropsSchema.parse(
  chatInstance.resolved_props,
);
assert(chatProps.frame === 60, "Shot frame 60 must become chat local frame 60");
assert(
  chatProps.messages[1]?.visibleText === "Two minu",
  "Frame 60 must resolve deterministic partial write-on text",
);
assert(
  chatProps.messages[0]?.direction === "incoming" &&
    chatProps.messages[1]?.direction === "outgoing",
  "message.direction must determine incoming/outgoing/system alignment",
);
assert(
  chatProps.messages[0]?.text === "Are you nearby?" &&
    chatProps.messages[0]?.media?.uri === "assets/conversations/sam-nearby.jpg",
  "Chat messages may resolve text and conversation-specific media paths together",
);
assert(
  chatProps.messages[1]?.sender.id ===
    canonicalChatData.messages[1]?.actorId,
  "Resolved message must retain its actorId",
);

const completedFrame = resolveShot({ ...commonInput, shotFrame: 90 });
const completedChat = completedFrame.active_screen_instances.find(
  (screen) => screen.screen_type === "chat",
);
assert(completedChat?.resolved_props, "Frame 90 must resolve chat props");
const completedProps = ResolvedChatScreenPropsSchema.parse(
  completedChat.resolved_props,
);
assert(
  completedProps.messages[1]?.visibleText === "Two minutes away.",
  "Completed write-on must expose the full message",
);

console.log("✓ chat props validated during write-on at shot frame 60");
console.log("✓ Chat resolved without legacy conversation/message records");
console.log("✓ message.direction determined incoming/outgoing/system alignment");
console.log("✓ text and media can coexist on one Chat message");
console.log("✓ completed write-on validated at shot frame 90");
console.log("In-memory repository and resolver validation succeeded.");
