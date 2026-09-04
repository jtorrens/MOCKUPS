import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import {
  numberToken,
  previewPayloadInBox,
  previewScreenBox,
  selectedColor,
} from "./componentRenderableCommon.js";
import { parseObject } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  renderableVisualBounds,
  renderScale,
  translateRenderableNode,
} from "./previewGeometryHelpers.js";
import { projectMediaType } from "./projectMediaDirectorySources.js";
import type {
  SocialPostComponentSlot,
  SocialPostRow,
  SocialPostModuleContract,
} from "./socialPostModuleContract.js";
import { resolveSocialPostModule } from "./socialPostModuleResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";
import { resolvedTextInputBarRuntimeConfig } from "./textInputBarRuntimeConfig.js";

interface MeasuredSlot {
  index: number;
  node: RenderableNode;
  width: number;
  height: number;
}

interface RenderedRow {
  node: RenderableNode;
  height: number;
}

interface SocialPostComposerLayout {
  top: number;
  keyboard?: RenderableNode;
  textInput?: RenderableNode;
}

export function socialPostModuleToRenderable(
  payload: DesignPreviewPayload,
): RenderableNode {
  const contract = resolveSocialPostModule(payload);
  const screen = previewScreenBox(payload);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const statusReference = payload.themeStatusBarVariantReference?.trim() ?? "";
  const navigationReference = payload.themeNavigationBarVariantReference?.trim() ?? "";
  const status = contract.showStatusBar && statusReference
    ? componentNode(payload, componentBaseConfigs, "status_bar", {
        variantReference: statusReference,
        overrides: {},
      }, {})
    : undefined;
  const navigation = contract.showNavigationBar && navigationReference
    ? componentNode(payload, componentBaseConfigs, "navigation_bar", {
        variantReference: navigationReference,
        overrides: {},
      }, {})
    : undefined;
  const contentY = screen.y + (status?.box?.height ?? 0);
  const header = contract.showHeader
      ? rowsSectionNode(payload, componentBaseConfigs, {
        ownerId: "module.core.socialPost",
        section: "header",
        rows: contract.rows,
        rowGapToken: contract.rowGapToken,
        height: contract.headerHeight,
        surfaceSlot: contract.headerSurfaceSlot,
        edge: "top",
        contentEdge: contentY,
      })
    : undefined;
  const bodyTop = header?.box ? header.box.y + header.box.height : contentY;
  const contentBottom = screen.y + screen.height - (navigation?.box?.height ?? 0);
  const footer = rowsSectionNode(payload, componentBaseConfigs, {
    ownerId: "module.core.socialPost",
    section: "footer",
    rows: contract.footerRows,
    rowGapToken: contract.footerRowGapToken,
    height: contract.footerHeight,
    surfaceSlot: contract.footerSurfaceSlot,
    edge: "bottom",
    contentEdge: contentBottom,
  });
  const composer = composerLayoutNode(
    payload,
    componentBaseConfigs,
    contract,
    contentBottom,
  );
  const bodyBottom = contract.message.keyboardVisible || contract.message.textInputVisible
    ? composer.top
    : footer.box?.y ?? contentBottom;
  const scale = renderScale(payload);
  const bodyHeight = Math.max(0, bodyBottom - bodyTop);
  const minimumMessageHeight = Math.min(
    bodyHeight,
    contract.message.show ? contract.messageMinHeight * scale : 0,
  );
  const sectionCapacity = Math.max(0, bodyHeight - minimumMessageHeight);
  const mediaChrome = contract.showMedia
    ? mediaSectionNode(
        payload,
        componentBaseConfigs,
        contract,
        bodyTop,
        1,
      )
    : undefined;
  const mediaChromeHeight = Math.max(0, (mediaChrome?.box?.height ?? 0) - scale);
  const provisionalGallery = contract.showGallery
    ? gallerySectionNode(
        payload,
        componentBaseConfigs,
        contract,
        bodyTop,
        bodyTop + Math.max(1, sectionCapacity),
      )
    : undefined;
  const galleryRequiredHeight = provisionalGallery?.box?.height ?? 0;
  const mediaHeightLimit = contract.showGallery
    ? contract.galleryMode === "carousel"
      ? Math.max(0, sectionCapacity - galleryRequiredHeight)
      : Math.max(0, sectionCapacity - 1)
    : sectionCapacity;
  const availableMediaContentHeight = Math.max(
    1,
    (mediaHeightLimit - mediaChromeHeight) / scale,
  );
  const media = contract.showMedia
    ? mediaSectionNode(
        payload,
        componentBaseConfigs,
        contract,
        bodyTop,
        contract.mediaHeightMode === "fill"
          ? availableMediaContentHeight
          : Math.min(contract.mediaHeight, availableMediaContentHeight),
      )
    : undefined;
  const galleryTop = media?.box ? media.box.y + media.box.height : bodyTop;
  const finalGalleryHeight = contract.galleryMode === "carousel"
    ? galleryRequiredHeight
    : Math.max(1, sectionCapacity - (media?.box?.height ?? 0));
  const gallery = contract.showGallery
    ? gallerySectionNode(
        payload,
        componentBaseConfigs,
        contract,
        galleryTop,
        galleryTop + Math.max(1, finalGalleryHeight),
      )
    : undefined;
  const messageTop = gallery?.box
    ? gallery.box.y + gallery.box.height
    : galleryTop;
  const message = contract.message.show
    ? messageSectionNode(
        payload,
        componentBaseConfigs,
        contract,
        messageTop,
        bodyBottom,
      )
    : undefined;
  const backgroundNode = contract.useAppWallpaper
    ? wallpaperRenderable(payload, screen) ?? background(payload)
    : background(payload);
  const children: RenderableNode[] = [backgroundNode];
  if (header) children.push(header);
  if (media) children.push(media);
  if (gallery) children.push(gallery);
  if (message) children.push(message);
  children.push(footer);
  if (composer.textInput) children.push(withZIndex(composer.textInput, 40));
  if (composer.keyboard) children.push(withZIndex(composer.keyboard, 50));
  if (status) children.push(withZIndex(status, 20));
  if (navigation) children.push(withZIndex(navigation, 30));
  return {
    id: contract.id,
    type: "group",
    frame: 0,
    box: screen,
    style: { overflow: "hidden" },
    children,
  };
}

function messageSectionNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  contract: SocialPostModuleContract,
  y: number,
  bottom: number,
): RenderableNode {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const message = contract.message;
  const [horizontalPadding, verticalPadding] = spacingPair(payload, message.padding);
  const separatorHeight = message.showSeparator ? Math.max(1, scale) : 0;
  const separatorY = Math.max(y, bottom - separatorHeight);
  const innerWidth = Math.max(1, screen.width - horizontalPadding * 2);
  const innerWidthDesign = innerWidth / scale;
  const contentBottom = Math.max(y, separatorY - verticalPadding);
  const bubbleY = y + verticalPadding;
  const bubbleHeight = Math.max(1, contentBottom - bubbleY);
  const bubbleBox = {
    x: screen.x + horizontalPadding,
    y: bubbleY,
    width: innerWidth,
    height: bubbleHeight,
  };
  const bubble = message.bubbleVisible
    ? componentNode(
        previewPayloadInBox(payload, bubbleBox),
        componentBaseConfigs,
        "bubble",
        message.bubbleSlot,
        {
          ...message.bubbleInputs,
          sampleText: message.text,
          maxWidth: 100,
          size: `${innerWidthDesign}|${bubbleHeight / scale}`,
          writeOnTrigger: message.bubbleWriteOnTrigger,
          writeOnFrame: message.writeOnFrame,
          writeOnDurationFrames: message.writeOnDurationFrames,
        },
      )
    : undefined;
  const children: RenderableNode[] = [];
  if (bubble) children.push(bubble);
  if (message.showSeparator) {
    children.push({
      id: "module.core.socialPost.message.separator",
      type: "surface",
      frame: 0,
      box: {
        x: screen.x,
        y: separatorY,
        width: screen.width,
        height: separatorHeight,
      },
      style: { background: selectedColor(payload, "theme.colors.divider") },
    });
  }
  return {
    id: "module.core.socialPost.message",
    type: "group",
    frame: 0,
    box: {
      x: screen.x,
      y,
      width: screen.width,
      height: Math.max(0, bottom - y),
    },
    style: { overflow: "hidden" },
    children,
  };
}

function composerLayoutNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  contract: SocialPostModuleContract,
  bottom: number,
): SocialPostComposerLayout {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const message = contract.message;
  const [horizontalPadding] = spacingPair(payload, message.padding);
  const innerWidth = Math.max(1, screen.width - horizontalPadding * 2);
  const innerWidthDesign = innerWidth / scale;
  const keyboard = message.keyboardVisible
    ? componentNode(
        payload,
        componentBaseConfigs,
        "keyboard",
        message.keyboardSlot,
        {
          text: message.visibleText,
          currentCharacter: message.currentCharacter,
          trigger: message.currentCharacter > 0,
          motionElapsedMs: message.writeOnFrame / Math.max(1, payload.frameRate) * 1000,
        },
      )
    : undefined;
  const keyboardNode = keyboard?.box
    ? translateRenderableNode(keyboard, {
        x: screen.x - keyboard.box.x,
        y: bottom - keyboard.box.height - keyboard.box.y,
      })
    : keyboard;
  const keyboardTop = keyboardNode?.box?.y ?? bottom;
  const textInputConfig = message.textInputVisible
    ? resolvedTextInputBarRuntimeConfig(
        payload,
        componentBaseConfigs,
        message.textInputBarSlot,
        message.visibleText,
        innerWidthDesign,
        "module.core.socialPost.messageTextInputBarSlot",
      )
    : undefined;
  const textInput = message.textInputVisible
    ? componentNode(
        previewPayloadInBox(payload, {
          x: screen.x + horizontalPadding,
          y: screen.y,
          width: innerWidth,
          height: Math.max(1, keyboardTop - screen.y),
        }),
        componentBaseConfigs,
        "textInputBar",
        message.textInputBarSlot,
        { availableWidth: innerWidthDesign },
        textInputConfig,
      )
    : undefined;
  const textInputNode = textInput?.box
    ? translateRenderableNode(textInput, {
        x: screen.x + horizontalPadding - textInput.box.x,
        y: keyboardTop - textInput.box.height - textInput.box.y,
      })
    : textInput;
  return {
    top: textInputNode?.box?.y ?? keyboardTop,
    keyboard: keyboardNode,
    textInput: textInputNode,
  };
}

function gallerySectionNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  contract: SocialPostModuleContract,
  y: number,
  bottom: number,
): RenderableNode {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const separatorHeight = contract.showGallerySeparator ? Math.max(1, scale) : 0;
  const box = {
    x: screen.x,
    y,
    width: screen.width,
    height: Math.max(1, bottom - y - separatorHeight),
  };
  const gallery = componentNode(
    previewPayloadInBox(payload, box),
    componentBaseConfigs,
    "gallery",
    contract.gallerySlot,
    {
      mediaDirectory: contract.galleryDirectory,
      viewportSize: `${box.width / scale}|${box.height / scale}`,
      selectedIndex: contract.gallerySelectedIndex,
      scrollRow: contract.galleryScrollRow,
    },
  );
  const galleryBox = gallery.box ?? box;
  const children: RenderableNode[] = [gallery];
  if (contract.showGallerySeparator) {
    children.push({
      id: "module.core.socialPost.gallery.separator",
      type: "surface",
      frame: 0,
      box: {
        x: screen.x,
        y: galleryBox.y + galleryBox.height,
        width: screen.width,
        height: separatorHeight,
      },
      style: { background: selectedColor(payload, "theme.colors.divider") },
    });
  }
  return {
    id: "module.core.socialPost.gallery",
    type: "group",
    frame: 0,
    box: {
      x: galleryBox.x,
      y: galleryBox.y,
      width: galleryBox.width,
      height: galleryBox.height + separatorHeight,
    },
    style: { overflow: "visible" },
    children,
  };
}

function mediaSectionNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  contract: SocialPostModuleContract,
  y: number,
  mediaHeightDesign: number,
): RenderableNode {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const [horizontalPadding, verticalPadding] = spacingPair(payload, contract.mediaPadding);
  const mediaHeight = mediaHeightDesign * scale;
  const mediaBox = {
    x: screen.x + horizontalPadding,
    y: y + verticalPadding,
    width: Math.max(1, screen.width - horizontalPadding * 2),
    height: mediaHeight,
  };
  const pages = mediaPagerPages(contract.mediaSources, contract.gallerySelectedIndex);
  const activeIndex = contract.mediaSources.length > 0
    ? Math.min(
        Math.round(contract.gallerySelectedIndex),
        contract.mediaSources.length - 1,
      )
    : 0;
  const mediaPages = pages.map(({ index, sourceUri }) => {
    const isActive = index === activeIndex;
    const isVideo = projectMediaType(sourceUri) === "video";
    const pageBox = {
      ...mediaBox,
      x: mediaBox.x + (index - contract.gallerySelectedIndex) * mediaBox.width,
    };
    const media = componentNode(
      payload,
      componentBaseConfigs,
      "media",
      contract.mediaSlot,
      {
        ...contract.mediaInputs,
        mediaSource: sourceUri,
        mediaType: projectMediaType(sourceUri),
        mediaScale: contract.mediaScale,
        mediaOffset: contract.mediaOffset,
        isPlaying: isActive && isVideo ? contract.mediaIsPlaying : false,
        currentTimeSeconds: isActive && isVideo ? contract.mediaCurrentTimeSeconds : 0,
        durationSeconds: isActive && isVideo ? contract.mediaDurationSeconds : 0,
        isFullScreen: isActive ? contract.mediaIsFullScreen : false,
        fullScreenTransition: isActive ? contract.mediaFullScreenTransition : false,
        controlsElapsedMs: isActive && isVideo
          ? contract.mediaCurrentTimeSeconds * 1000
          : 0,
        motionElapsedMs: isActive ? contract.mediaMotionElapsedMs : 0,
        viewportSize: `${mediaBox.width / scale}|${mediaHeightDesign}`,
      },
      undefined,
      pageBox,
    );
    return {
      id: `module.core.socialPost.media.page.${index}`,
      type: "group" as const,
      frame: 0,
      box: pageBox,
      style: { overflow: "visible" as const },
      children: [media],
    };
  });
  const visualOverflow = mediaPages.reduce(
    (maximum, page) => {
      const pageBox = page.box!;
      const visual = renderableVisualBounds(page);
      return {
        left: Math.max(maximum.left, pageBox.x - visual.x),
        top: Math.max(maximum.top, pageBox.y - visual.y),
        right: Math.max(
          maximum.right,
          visual.x + visual.width - pageBox.x - pageBox.width,
        ),
        bottom: Math.max(
          maximum.bottom,
          visual.y + visual.height - pageBox.y - pageBox.height,
        ),
      };
    },
    { left: 0, top: 0, right: 0, bottom: 0 },
  );
  const mediaViewportBox = {
    x: mediaBox.x - visualOverflow.left,
    y: mediaBox.y - visualOverflow.top,
    width: mediaBox.width + visualOverflow.left + visualOverflow.right,
    height: mediaBox.height + visualOverflow.top + visualOverflow.bottom,
  };
  const mediaViewport: RenderableNode = {
    id: "module.core.socialPost.media.viewport",
    type: "group",
    frame: 0,
    box: mediaViewportBox,
    style: { overflow: "hidden" },
    children: mediaPages,
  };
  const separatorHeight = contract.showMediaSeparator ? Math.max(1, scale) : 0;
  const separatorY = mediaBox.y + mediaBox.height + verticalPadding;
  const children: RenderableNode[] = [mediaViewport];
  if (contract.showMediaSeparator) {
    children.push({
      id: "module.core.socialPost.media.separator",
      type: "surface",
      frame: 0,
      box: {
        x: screen.x,
        y: separatorY,
        width: screen.width,
        height: separatorHeight,
      },
      style: { background: selectedColor(payload, "theme.colors.divider") },
    });
  }
  return {
    id: "module.core.socialPost.media",
    type: "group",
    frame: 0,
    box: {
      x: screen.x,
      y,
      width: screen.width,
      height: verticalPadding + mediaBox.height + verticalPadding + separatorHeight,
    },
    style: { overflow: "visible" },
    children,
  };
}

function mediaPagerPages(sources: string[], selectedIndex: number) {
  if (sources.length === 0) return [{ index: 0, sourceUri: "" }];
  const lowerIndex = Math.floor(selectedIndex);
  const upperIndex = Math.ceil(selectedIndex);
  return upperIndex === lowerIndex
    ? [{ index: lowerIndex, sourceUri: sources[lowerIndex]! }]
    : [
        { index: lowerIndex, sourceUri: sources[lowerIndex]! },
        { index: upperIndex, sourceUri: sources[upperIndex]! },
      ];
}

export function rowsSectionNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  options: {
    ownerId: string;
    section: "header" | "footer";
    rows: [SocialPostRow, SocialPostRow];
    rowGapToken: string;
    height: number;
    surfaceSlot: SocialPostComponentSlot;
    edge: "top" | "bottom";
    contentEdge: number;
  },
): RenderableNode {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const first = renderRow(payload, componentBaseConfigs, options.ownerId, options.section, options.rows[0], 0);
  const gap = numberToken(payload, options.rowGapToken) * scale;
  const second = renderRow(payload, componentBaseConfigs, options.ownerId, options.section, options.rows[1], 0);
  const rowsHeight = first.height + gap + second.height;
  const sectionHeight = Math.max(options.height * scale, rowsHeight);
  const sectionY = options.edge === "top"
    ? options.contentEdge
    : options.contentEdge - sectionHeight;
  const rowsY = sectionY + sectionHeight - rowsHeight;
  const firstNode = translateRenderableNode(first.node, { x: 0, y: rowsY });
  const secondNode = translateRenderableNode(second.node, {
    x: 0,
    y: rowsY + first.height + gap,
  });
  const surfaceBox = {
    x: screen.x,
    y: options.edge === "top" ? screen.y : sectionY,
    width: screen.width,
    height: options.edge === "top"
      ? Math.max(0, sectionY + sectionHeight - screen.y)
      : Math.max(0, screen.y + screen.height - sectionY),
  };
  const surface = resolveSurfaceComponentAtSize(
    embeddedComponentConfig(
      componentBaseConfigs,
      options.surfaceSlot,
      "surface",
      `${options.ownerId}.${options.section}SurfaceSlot`,
    ),
    { width: surfaceBox.width / scale, height: surfaceBox.height / scale },
    `${options.ownerId}.${options.section}.surface`,
  );
  return {
    id: `${options.ownerId}.${options.section}`,
    type: "group",
    frame: 0,
    box: {
      x: screen.x,
      y: sectionY,
      width: screen.width,
      height: sectionHeight,
    },
    style: { overflow: "visible" },
    children: [
      surfaceComponentToRenderableAt(payload, surface, surfaceBox),
      firstNode,
      secondNode,
    ],
  };
}

function renderRow(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  ownerId: string,
  section: "header" | "footer",
  row: SocialPostRow,
  y: number,
): RenderedRow {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const measured = row.slots.flatMap((slot) => {
    if (!slot.componentType || !slot.componentSlot || !rowSlotHasContent(slot)) return [];
    const node = componentNode(
      payload,
      componentBaseConfigs,
      slot.componentType,
      slot.componentSlot,
      slot.inputs,
    );
    if (!node.box) return [];
    return [{
      index: slot.index,
      node,
      width: node.box.width,
      height: node.box.height,
    } satisfies MeasuredSlot];
  });
  const contentHeight = measured.reduce((height, item) => Math.max(height, item.height), 0);
  const [horizontalPadding, verticalPadding] = spacingPair(payload, row.padding);
  const rowHeight = contentHeight + verticalPadding * 2;
  const contentY = y + verticalPadding;
  const leftEdge = screen.x + horizontalPadding;
  const rightEdge = screen.x + screen.width - horizontalPadding;
  const left = measured.find((item) => item.index === 1);
  const right = measured.find((item) => item.index === 5);
  const middle = measured.filter((item) => item.index >= 2 && item.index <= 4);
  const children: RenderableNode[] = [];

  if (left) children.push(placeMeasuredSlot(
    ownerId,
    section,
    row.id,
    left,
    leftEdge,
    rowY(row, contentY, contentHeight, left.height),
  ));
  if (right) {
    children.push(placeMeasuredSlot(
      ownerId,
      section,
      row.id,
      right,
      rightEdge - right.width,
      rowY(row, contentY, contentHeight, right.height),
    ));
  }

  const middleLeft = leftEdge + (left?.width ?? 0);
  const middleRight = rightEdge - (right?.width ?? 0);
  const middleWidth = middle.reduce((width, item) => width + item.width, 0);
  const freeWidth = Math.max(0, middleRight - middleLeft - middleWidth);
  const middleGap = middle.length > 0 ? freeWidth / (middle.length + 1) : 0;
  let middleX = middleLeft + middleGap;
  for (const item of middle) {
    children.push(placeMeasuredSlot(
      ownerId,
      section,
      row.id,
      item,
      middleX,
      rowY(row, contentY, contentHeight, item.height),
    ));
    middleX += item.width + middleGap;
  }

  const separatorHeight = row.showSeparator ? Math.max(1, scale) : 0;
  if (row.showSeparator) {
    children.push({
      id: `${ownerId}.${section}.${row.id}.separator`,
      type: "surface",
      frame: 0,
      box: {
        x: screen.x,
        y: y + rowHeight,
        width: screen.width,
        height: separatorHeight,
      },
      style: { background: selectedColor(payload, "theme.colors.divider") },
    });
  }
  const height = rowHeight + separatorHeight;
  return {
    height,
    node: {
      id: `${ownerId}.${section}.${row.id}`,
      type: "group",
      frame: 0,
      box: { x: screen.x, y, width: screen.width, height },
      style: { overflow: "visible" },
      children,
    },
  };
}

function rowSlotHasContent(slot: SocialPostRow["slots"][number]) {
  if (slot.kind === "none") return false;
  if (slot.kind !== "label") return true;
  const label = typeof slot.inputs.sampleText === "string"
    ? slot.inputs.sampleText.trim()
    : "";
  const sublabel = typeof slot.inputs.sampleSubtext === "string"
    ? slot.inputs.sampleSubtext.trim()
    : "";
  return label.length > 0 || sublabel.length > 0;
}

function rowY(
  row: SocialPostRow,
  y: number,
  rowHeight: number,
  itemHeight: number,
) {
  if (row.verticalAlignment === "bottom") return y + rowHeight - itemHeight;
  if (row.verticalAlignment === "center") return y + (rowHeight - itemHeight) * 0.5;
  return y;
}

function placeMeasuredSlot(
  ownerId: string,
  section: "header" | "footer",
  rowId: SocialPostRow["id"],
  item: MeasuredSlot,
  x: number,
  y: number,
): RenderableNode {
  const translated = translateRenderableNode(item.node, {
    x: x - (item.node.box?.x ?? 0),
    y: y - (item.node.box?.y ?? 0),
  });
  return {
    id: `${ownerId}.${section}.${rowId}.slot.${item.index}.${translated.id}`,
    type: "group",
    frame: 0,
    box: translated.box,
    style: { overflow: "visible" },
    children: [translated],
  };
}

function componentNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: Record<string, unknown>,
  componentType: string,
  slot: SocialPostComponentSlot,
  inputs: Record<string, unknown>,
  resolvedConfig?: Record<string, unknown>,
  assignedBox?: RenderableBox,
) {
  return componentClassToRenderable({
    ...payload,
    componentType,
    configJson: JSON.stringify(resolvedConfig ?? embeddedComponentConfig(
      componentBaseConfigs,
      slot,
      componentType,
      `module.core.socialPost.${componentType}`,
    )),
    designPreviewJson: JSON.stringify(inputs),
  }, assignedBox);
}

function spacingPair(payload: DesignPreviewPayload, value: string) {
  const [leftToken = "theme.spacing.none", rightToken = leftToken] = value.split("|");
  const scale = renderScale(payload);
  return [
    numberToken(payload, leftToken) * scale,
    numberToken(payload, rightToken) * scale,
  ] as const;
}

function background(payload: DesignPreviewPayload): RenderableNode {
  return {
    id: "module.core.socialPost.background",
    type: "surface",
    frame: 0,
    box: previewScreenBox(payload),
    style: { background: selectedColor(payload, "theme.colors.background") },
    metadata: { paintRole: "moduleBackground" },
  };
}

function withZIndex(node: RenderableNode, zIndex: number): RenderableNode {
  return { ...node, style: { ...node.style, zIndex } };
}
