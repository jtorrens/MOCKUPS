import type { ResolvedMessageBubbleProps } from "../../../domain/schemas/index.js";
import {
  layoutMessageBubble,
  type MessageBubbleLayout,
} from "../../layout/index.js";
import type { RenderableNode } from "../../renderable/types.js";
import type { VisualModule } from "../types.js";
import { AvatarModule } from "./AvatarModule.js";

function animationOpacity(
  animation: Record<string, unknown>,
): number | undefined {
  const enter = animation.enter;
  if (typeof enter !== "object" || enter === null || !("opacity" in enter)) {
    return undefined;
  }
  return typeof enter.opacity === "number" ? enter.opacity : undefined;
}

export function renderMessageBubbleWithLayout(
  input: ResolvedMessageBubbleProps,
  layout: MessageBubbleLayout,
): RenderableNode {
    const children: RenderableNode[] = [];
    if (input.actor.avatarUri && layout.avatarBox) {
      children.push(
        {
          ...AvatarModule.render({
            id: `${input.id}:avatar`,
            uri: input.actor.avatarUri,
            size: input.style.avatarSize,
            label: input.actor.displayName,
            frame: input.frame,
          }),
          box: layout.avatarBox,
        },
      );
    }
    children.push({
      id: `${input.id}:text`,
      type: "text",
      role: "message_text",
      frame: input.frame,
      box: layout.textBox,
      text: input.visibleText,
      style: {
        color: input.style.textColor,
        fontFamily: input.style.fontFamily,
        fontSize: input.style.fontSize,
        lineHeight: input.style.lineHeight,
        fontWeight: input.style.fontWeight,
      },
    });

    return {
      id: input.id,
      type: "message_bubble",
      role: input.direction,
      frame: input.frame,
      box: layout.bubbleBox,
      transform: {
        opacity: animationOpacity(input.animation),
      },
      style: {
        backgroundColor: input.style.backgroundColor,
        textColor: input.style.textColor,
        fontFamily: input.style.fontFamily,
        fontSize: input.style.fontSize,
        lineHeight: input.style.lineHeight,
        fontWeight: input.style.fontWeight,
        borderRadius: input.style.borderRadius,
        paddingX: input.style.paddingX,
        paddingY: input.style.paddingY,
        showTail: input.layout.showTail,
        tailStyle: input.style.tailStyle,
        tailWidth: input.style.tailWidth,
        tailHeight: input.style.tailHeight,
        shadow: input.style.shadow,
      },
      text: input.visibleText,
      children,
      metadata: {
        actorId: input.actor.id,
        fullText: input.text,
        timing: input.timing,
        animation: input.animation,
        layoutIntent: input.layout,
        avatarGap: input.layout.avatarGap,
        tailGeometry: {
          style: input.style.tailStyle,
          width: input.style.tailWidth,
          height: input.style.tailHeight,
          path: "not_computed_renderer_agnostic_stub",
        },
        measurement: layout.measurement,
        maxBubbleWidth: layout.maxBubbleWidth,
        alignment: layout.alignment,
        tokenSources: {
          style: "theme.tokens_json.chatBubbles/typography/avatars",
          layout: "theme.tokens_json.chatBubbles/avatars",
        },
      },
    };
}

export const MessageBubbleModule: VisualModule<ResolvedMessageBubbleProps> = {
  type: "message_bubble",
  version: 1,
  render(input): RenderableNode {
    const avatarReserve =
      input.direction === "incoming" && input.actor.avatarUri
        ? input.style.avatarSize + input.layout.avatarGap
        : 0;
    const layout = layoutMessageBubble({
      props: input,
      messageArea: {
        x: 0,
        y: 0,
        width: input.layout.maxWidth + avatarReserve,
        height: Number.MAX_SAFE_INTEGER,
      },
      y: 0,
    });
    return renderMessageBubbleWithLayout(input, layout);
  },
};
