import { loadExampleRepository } from "../domain/repository/fixtureLoader.js";
import { resolveShot } from "../domain/resolvers/index.js";
import { ResolvedChatScreenPropsSchema } from "../domain/schemas/index.js";
import { ChatScreenModule } from "../visual/modules/screens/ChatScreenModule.js";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import type { RenderableNode } from "../visual/renderable/types.js";

export const CHAT_PREVIEW_WIDTH = 1290;
export const CHAT_PREVIEW_HEIGHT = 2796;
export const CHAT_PREVIEW_FPS = 25;
export const CHAT_PREVIEW_DURATION_FRAMES = 100;

const SOURCE_SHOT_FPS = 30;
const SOURCE_SHOT_START_FRAME = 0;
const PRODUCTION_ID = "production_demo";
const SHOT_ID = "shot_chat";
const repository = loadExampleRepository();

export function remotionFrameToShotFrame(remotionFrame: number): number {
  return (
    SOURCE_SHOT_START_FRAME +
    Math.floor((remotionFrame * SOURCE_SHOT_FPS) / CHAT_PREVIEW_FPS)
  );
}

export function buildRenderableForFrame(remotionFrame: number): RenderableNode {
  const shotFrame = remotionFrameToShotFrame(remotionFrame);
  const resolvedShot = resolveShot({
    repository,
    productionId: PRODUCTION_ID,
    shotId: SHOT_ID,
    shotFrame,
  });
  const chatInstance = resolvedShot.active_screen_instances.find(
    (screen) => screen.screen_type === "chat",
  );
  if (!chatInstance?.resolved_props) {
    throw new Error(`Chat screen is not active at shot frame ${shotFrame}`);
  }

  const resolvedProps = ResolvedChatScreenPropsSchema.parse(
    chatInstance.resolved_props,
  );
  return RenderableNodeSchema.parse(ChatScreenModule.render(resolvedProps));
}
