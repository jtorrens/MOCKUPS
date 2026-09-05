import assert from "node:assert/strict";
import test from "node:test";

import Database from "better-sqlite3";

import { parityDatabasePath } from "../../src/development-scaffolding/parityDatabasePath.js";
import { socialPostModuleToRenderable } from "../../src/desktop-preview/socialPostModuleRenderable.js";
import { resolveSocialPostModule } from "../../src/desktop-preview/socialPostModuleResolver.js";
import type { RenderableNode } from "../../src/visual/renderable/types.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

type ModuleRow = {
  app_id: string;
  design_preview_json: string;
  metadata_json: string;
};

function fixture() {
  const bubble = committedComponentFixture("bubble", "default");
  const avatar = committedComponentFixture("avatar", "default");
  const database = new Database(parityDatabasePath(), { readonly: true, fileMustExist: true });
  try {
    const module = database.prepare(`
      SELECT app_id, design_preview_json, metadata_json
      FROM modules
      WHERE record_class_id = 'module.core.socialPost'
    `).get() as ModuleRow | undefined;
    assert.ok(module);
    assert.equal(module.app_id, "app_project_foqn_s2_social_media");
    const metadata = JSON.parse(module.metadata_json) as {
      variants: Array<{ id: string; config: Record<string, unknown> }>;
    };
    const defaultVariant = metadata.variants.find((variant) => variant.id === "default");
    assert.ok(defaultVariant);
    const runtime = JSON.parse(module.design_preview_json) as Record<string, unknown>;
    const avatarRuntime = JSON.parse(avatar.designPreviewJson) as Record<string, unknown>;
    const runtimeRows = runtime.socialPostRows as Array<Record<string, unknown>>;
    const footerRuntimeRows = runtime.socialPostFooterRows as Array<Record<string, unknown>>;
    for (const runtimeRow of [...runtimeRows, ...footerRuntimeRows]) {
      const slots = runtimeRow.slotInputs as Array<Record<string, unknown>>;
      for (const slot of slots) slot.actor = avatarRuntime.actor;
    }
    return {
      ...bubble,
      kind: "module" as const,
      componentType: "module.core.socialPost",
      appConfigJson: JSON.stringify({
        wallpaper: {
          opacity: 1,
          kind: "solid",
          images: {
            light: { filePath: "" },
            dark: { filePath: "" },
          },
        },
        modes: {
          light: { wallpaper: { color: "palette_project_foqn_s2_gray_100" } },
          dark: { wallpaper: { color: "palette_project_foqn_s2_gray_000" } },
        },
      }),
      configJson: JSON.stringify(defaultVariant.config),
      designPreviewJson: JSON.stringify(runtime),
      runtimeContractJson: JSON.stringify(runtime),
      themeStatusBarVariantReference:
        "component_project_foqn_s2_status_bar::variant::default",
      themeNavigationBarVariantReference:
        "component_project_foqn_s2_navigation_bar::variant::default",
    };
  } finally {
    database.close();
  }
}

test("Social Post owns two fixed structure-projected Runtime row sections", () => {
  const source = fixture();
  const contract = resolveSocialPostModule(source);
  const runtime = JSON.parse(source.runtimeContractJson) as {
    inputs: Array<{ id: string }>;
    collections: Array<{ id: string }>;
  };
  const config = JSON.parse(source.configJson) as {
    socialPost: Record<string, unknown>;
  };
  assert.equal(Object.hasOwn(config.socialPost, "runtimeContract"), false);
  assert.equal(Object.hasOwn(config.socialPost, "forwarding"), false);
  assert.deepEqual(runtime.inputs.map(({ id }) => id), [
    "mediaScale",
    "mediaOffset",
    "isPlaying",
    "currentTimeSeconds",
    "durationSeconds",
    "isFullScreen",
    "fullScreenTransition",
    "motionElapsedMs",
    "showGallery",
    "galleryDirectory",
    "gallerySelectedIndex",
    "galleryScrollRow",
    "messageText",
    "messageWriteOnTiming",
    "messageTextInputVisible",
    "messageKeyboardVisible",
    "messageBubbleRevealMode",
    "messageWriteOnTrigger",
    "messageWriteOnFrame",
  ]);
  assert.deepEqual(runtime.collections.map(({ id }) => id), [
    "socialPostRows",
    "socialPostFooterRows",
  ]);
  assert.deepEqual(contract.rows.map(({ id }) => id), ["row1", "row2"]);
  assert.equal(contract.rows[0].content.slots[0].kind, "avatar");
  const headerAvatar = contract.rows[0].content.slots[0].content;
  assert.ok(headerAvatar && "actor" in headerAvatar);
  assert.equal(headerAvatar.actor.id, "actor_alex");
  assert.equal(headerAvatar.actor.displayName, "Alex Q");
  assert.equal(contract.rows[1].content.slots[0].kind, "label");
  const headerLabel = contract.rows[1].content.slots[0].content;
  assert.ok(headerLabel && "text" in headerLabel);
  assert.equal(headerLabel.text, "#FOQN");
  const headerConfigRows = config.socialPost.rows as Array<{
    rowSlot: { overrides: { contentRow: { slots: Array<Record<string, unknown>> } } };
  }>;
  for (const [rowIndex, row] of contract.rows.entries()) {
    for (const [slotIndex, slot] of row.content.slots.entries()) {
      const slotConfig = headerConfigRows[rowIndex]?.rowSlot.overrides.contentRow.slots[slotIndex];
      assert.equal(slot.kind, slotConfig?.kind);
      if (slot.kind !== "icon") continue;
      assert.ok(slot.content && "iconSizeToken" in slot.content);
      assert.equal(slot.content.iconSizeToken, slotConfig?.iconSizeToken);
      assert.equal(slot.content.contentMode, "icon");
    }
  }
  assert.equal(contract.mediaScale, 1);
  assert.equal(contract.mediaHeightMode, "fixed");
  assert.equal(contract.mediaOffset, "0|0");
  assert.equal(contract.mediaIsPlaying, false);
  assert.equal(contract.mediaCurrentTimeSeconds, 0);
  assert.equal(contract.mediaDurationSeconds, 12);
  assert.equal(contract.mediaIsFullScreen, false);
  assert.equal(contract.mediaFullScreenTransition, false);
  assert.equal(contract.mediaMotionElapsedMs, 0);
  assert.equal(contract.showGallerySeparator, true);
  assert.deepEqual(contract.footerRows.map(({ id }) => id), ["footerRow1", "footerRow2"]);
  const footerConfigRows = config.socialPost.footerRows as Array<{
    rowSlot: { overrides: { contentRow: { slots: Array<Record<string, unknown>> } } };
  }>;
  for (const [rowIndex, row] of contract.footerRows.entries()) {
    for (const [slotIndex, slot] of row.content.slots.entries()) {
      const slotConfig = footerConfigRows[rowIndex]?.rowSlot.overrides.contentRow?.slots?.[slotIndex];
      if (!slotConfig) continue;
      assert.equal(slot.kind, slotConfig?.kind);
      if (slot.kind === "icon") {
        assert.ok(slot.content && "iconSizeToken" in slot.content);
        assert.equal(slot.content.iconSizeToken, slotConfig?.iconSizeToken);
        assert.equal(slot.content.contentMode, "icon");
      }
    }
  }
});

test("Social Post pages its main Media from the Gallery directory and selected index", () => {
  const source = fixture();
  source.projectMediaFiles = ["media/a.jpg", "media/b.jpg", "media/c.jpg"];
  const runtime = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  const config = JSON.parse(source.configJson) as {
    socialPost: {
      mediaSlot: { overrides: Record<string, unknown> };
    };
  };
  config.socialPost.mediaSlot.overrides = {
    media: {
      surfaceSlot: {
        overrides: { style: { shadowEnabled: true } },
      },
    },
  };
  source.configJson = JSON.stringify(config);
  runtime.galleryDirectory = "media";
  runtime.gallerySelectedIndex = 1.25;
  source.designPreviewJson = JSON.stringify(runtime);
  source.runtimeContractJson = JSON.stringify(runtime);

  const contract = resolveSocialPostModule(source);
  assert.deepEqual(contract.mediaSources, ["media/a.jpg", "media/b.jpg", "media/c.jpg"]);
  assert.equal(contract.gallerySelectedIndex, 1.25);

  const renderable = socialPostModuleToRenderable(source);
  const viewport = requiredNode(renderable, "module.core.socialPost.media.viewport");
  const outgoing = requiredNode(renderable, "module.core.socialPost.media.page.1");
  const incoming = requiredNode(renderable, "module.core.socialPost.media.page.2");
  const pageShadow = findNodeBy(outgoing, (node) => node.style?.shadow !== undefined);
  assert.ok(pageShadow, "Expected the Media page override to enable its Surface shadow.");
  assert.equal(viewport.style?.overflow, "hidden");
  assert.ok(viewport.box);
  assert.ok(outgoing.box);
  assert.ok(incoming.box);
  assert.equal(incoming.box.x - outgoing.box.x, outgoing.box.width);
  const mediaOrigin = outgoing.box.x + outgoing.box.width * 0.25;
  assert.ok(
    viewport.box.x < mediaOrigin,
    JSON.stringify({ viewport: viewport.box, outgoing: outgoing.box, mediaOrigin }),
  );
  assert.ok(viewport.box.x + viewport.box.width > mediaOrigin + outgoing.box.width);
  assert.ok(
    viewport.box.y < outgoing.box.y,
    JSON.stringify({ viewport: viewport.box, outgoing: outgoing.box, pageShadow: pageShadow.box }),
  );
  assert.ok(
    viewport.box.y + viewport.box.height > outgoing.box.y + outgoing.box.height,
    JSON.stringify({ viewport: viewport.box, outgoing: outgoing.box }),
  );
});

test("Social Post resolves animated Media scale and offset", () => {
  const source = fixture();
  source.localFrame = 5;
  source.instanceJson = JSON.stringify({
    animation: {
      schemaVersion: 2,
      tracks: [
        {
          id: "media-scale",
          fieldId: "mediaScale",
          keyframes: [
            { id: "scale-0", frame: 0, value: 1, interpolation: "hold", enabled: true },
            { id: "scale-10", frame: 10, value: 2, interpolation: "linear", enabled: true },
          ],
        },
        {
          id: "media-offset",
          fieldId: "mediaOffset",
          keyframes: [
            { id: "offset-0", frame: 0, value: "0|0", interpolation: "hold", enabled: true },
            { id: "offset-10", frame: 10, value: "20|-10", interpolation: "linear", enabled: true },
          ],
        },
      ],
    },
  });
  const contract = resolveSocialPostModule(source);
  assert.equal(contract.mediaScale, 1.5);
  assert.equal(contract.mediaOffset, "10|-5");
});

test("Social Post resolves selected video playback and full-screen animation", () => {
  const source = fixture();
  source.localFrame = 12;
  source.projectMediaFiles = ["media/a.jpg", "media/b.mov"];
  const runtime = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  runtime.galleryDirectory = "media";
  runtime.gallerySelectedIndex = 1;
  runtime.durationSeconds = 2;
  source.designPreviewJson = JSON.stringify(runtime);
  source.runtimeContractJson = JSON.stringify(runtime);
  source.instanceJson = JSON.stringify({
    animation: {
      schemaVersion: 2,
      tracks: [
        {
          id: "media-playback",
          fieldId: "isPlaying",
          keyframes: [
            { id: "play-0", frame: 0, value: false, interpolation: "hold", enabled: true },
            { id: "play-6", frame: 6, value: true, interpolation: "hold", enabled: true },
          ],
        },
        {
          id: "media-full-screen",
          fieldId: "isFullScreen",
          keyframes: [
            { id: "full-0", frame: 0, value: false, interpolation: "hold", enabled: true },
            { id: "full-9", frame: 9, value: true, interpolation: "hold", enabled: true },
          ],
        },
      ],
    },
  });

  const contract = resolveSocialPostModule(source);
  assert.equal(contract.mediaIsPlaying, true);
  assert.equal(contract.mediaCurrentTimeSeconds, 0.24);
  assert.equal(contract.mediaDurationSeconds, 2);
  assert.equal(contract.mediaIsFullScreen, true);
  assert.equal(contract.mediaFullScreenTransition, true);
  assert.equal(contract.mediaMotionElapsedMs, 120);

  const renderable = socialPostModuleToRenderable(source);
  const activeMedia = requiredNode(renderable, "module.core.socialPost.media.page.1");
  assert.ok(findNodeBy(activeMedia, (node) => node.style?.rootOverlay === true));
});

test("Social Post hides its Message without reserving its minimum height", () => {
  const source = fixture();
  const config = JSON.parse(source.configJson) as {
    socialPost: { showMessage: boolean };
  };
  config.socialPost.showMessage = false;
  source.configJson = JSON.stringify(config);

  const node = socialPostModuleToRenderable(source);
  assert.equal(findNode(node, "module.core.socialPost.message"), undefined);
  assert.equal(resolveSocialPostModule(source).message.show, false);
});

test("Social Post shows the Gallery separator only when its Variant enables it", () => {
  const source = fixture();
  const runtime = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  runtime.showGallery = true;
  source.designPreviewJson = JSON.stringify(runtime);
  source.runtimeContractJson = JSON.stringify(runtime);

  const visible = socialPostModuleToRenderable(source);
  assert.ok(findNode(visible, "module.core.socialPost.gallery.separator"));

  const config = JSON.parse(source.configJson) as {
    socialPost: { showGallerySeparator: boolean };
  };
  config.socialPost.showGallerySeparator = false;
  source.configJson = JSON.stringify(config);
  const hidden = socialPostModuleToRenderable(source);
  assert.equal(findNode(hidden, "module.core.socialPost.gallery.separator"), undefined);
});

test("Social Post resolves animated text and distributes non-empty Runtime row slots", () => {
  const source = fixture();
  const runtime = JSON.parse(source.designPreviewJson) as {
    socialPostRows: Array<{ slotInputs: Array<{ id: string; label: string }> }>;
  };
  runtime.socialPostRows[1]!.slotInputs[2]!.label = "#Tag";
  source.designPreviewJson = JSON.stringify(runtime);
  source.runtimeContractJson = JSON.stringify(runtime);
  source.instanceJson = JSON.stringify({
    animation: {
      schemaVersion: 2,
      tracks: [
        {
          id: "message-text",
          fieldId: "messageText",
          keyframes: [
            { id: "message-0", frame: 0, value: "", interpolation: "hold", enabled: true },
            { id: "message-10", frame: 10, value: "Hello", interpolation: "writeOn", enabled: true },
          ],
        },
      ],
    },
  });

  source.localFrame = 0;
  const emptyFrame = resolveSocialPostModule(source);
  assert.equal(emptyFrame.message.text, "");
  const emptyAnimatedLabel = emptyFrame.rows[1].content.slots[2].content;
  assert.ok(emptyAnimatedLabel && "text" in emptyAnimatedLabel);
  assert.equal(emptyAnimatedLabel.text, "#Tag");

  source.localFrame = 5;
  const animatedFrame = resolveSocialPostModule(source);
  assert.equal(animatedFrame.message.text, "He");
  assert.equal(animatedFrame.message.visibleText, "He");
  const animatedLabel = animatedFrame.rows[1].content.slots[2].content;
  assert.ok(animatedLabel && "text" in animatedLabel);
  assert.equal(animatedLabel.text, "#Tag");
  assert.ok(findNode(
    socialPostModuleToRenderable(source),
    "component.contentRow.slot.row2_slot3",
  ));
});

test("Social Post renders its two header rows against one Surface", () => {
  const source = fixture();
  const node = socialPostModuleToRenderable(source);
  const config = JSON.parse(source.configJson) as {
    socialPost: { headerHeight: number };
  };
  const header = requiredNode(node, "module.core.socialPost.header");
  const row1 = requiredNode(node, "module.core.socialPost.header.row1");
  const row2 = requiredNode(node, "module.core.socialPost.header.row2");
  const separator = requiredNode(row2, "component.contentRow.separator");
  assert.equal(node.id, "module.core.socialPost");
  assert.deepEqual(node.box, { x: 0, y: 0, width: 360, height: 720 });
  assert.ok(header.box);
  assert.ok(row1.box);
  assert.ok(row2.box);
  assert.ok(separator.box);
  assert.equal(header.box.height, config.socialPost.headerHeight);
  assert.equal(row2.box.y, row1.box.y + row1.box.height);
  assert.equal(separator.box.y + separator.box.height, row2.box.y + row2.box.height);
  assert.ok(findNode(header, "component.contentRow.slots[0].avatar"));
  assert.ok(findNode(header, "component.contentRow.slots[1].label"));
  assert.equal(header.children?.[0]?.type, "surface");
});

test("Social Post excludes invisible rows from section measurement and gaps", () => {
  const source = fixture();
  const config = JSON.parse(source.configJson) as {
    socialPost: { rows: Array<{ visible: boolean }> };
  };
  config.socialPost.rows[1]!.visible = false;
  const node = socialPostModuleToRenderable({
    ...source,
    configJson: JSON.stringify(config),
  });
  const header = requiredNode(node, "module.core.socialPost.header");
  const row1 = requiredNode(node, "module.core.socialPost.header.row1");
  assert.equal(findNode(header, "module.core.socialPost.header.row2"), undefined);
  assert.ok(header.box && row1.box);
  assert.equal(
    row1.box.y + row1.box.height * 0.5,
    header.box.y + header.box.height * 0.5,
  );
});

test("Social Post renders its non-empty footer rows above navigation", () => {
  const source = fixture();
  const node = socialPostModuleToRenderable(source);
  const message = requiredNode(node, "module.core.socialPost.message");
  const footer = requiredNode(node, "module.core.socialPost.footer");
  const row1 = requiredNode(node, "module.core.socialPost.footer.footerRow1");
  const navigation = requiredNode(node, "navigation_bar");
  assert.ok(message.box);
  assert.ok(footer.box);
  assert.ok(row1.box);
  assert.ok(navigation.box);
  assert.equal(message.box.y + message.box.height, footer.box.y);
  assert.equal(footer.box.y + footer.box.height, navigation.box.y);
  assert.equal(findNode(footer, "module.core.socialPost.footer.footerRow2"), undefined);
  assert.ok(findNode(footer, "component.contentRow.slot.footerRow1_slot1"));
  assert.equal(footer.children?.[0]?.type, "surface");
  assert.equal(
    footer.children?.[0]?.box?.y! + footer.children?.[0]?.box?.height!,
    720,
  );
});

test("Social Post independently hides system chrome and its header", () => {
  const source = fixture();
  const config = JSON.parse(source.configJson) as {
    socialPost: {
      showStatusBar: boolean;
      showNavigationBar: boolean;
      showHeader: boolean;
    };
  };
  config.socialPost.showStatusBar = false;
  config.socialPost.showNavigationBar = false;
  config.socialPost.showHeader = false;
  source.configJson = JSON.stringify(config);
  const node = socialPostModuleToRenderable(source);
  assert.equal(findNode(node, "status_bar"), undefined);
  assert.equal(findNode(node, "navigation_bar"), undefined);
  assert.equal(findNode(node, "module.core.socialPost.header"), undefined);
});

test("Social Post selects App wallpaper or Theme background from one toggle", () => {
  const source = fixture();
  const config = JSON.parse(source.configJson) as {
    socialPost: { useAppWallpaper: boolean };
  };
  config.socialPost.useAppWallpaper = false;
  source.configJson = JSON.stringify(config);
  assert.equal(
    socialPostModuleToRenderable(source).children?.[0]?.id,
    "module.core.socialPost.background",
  );
  config.socialPost.useAppWallpaper = true;
  source.configJson = JSON.stringify(config);
  source.appConfigJson = JSON.stringify({
    wallpaper: {
      opacity: 1,
      kind: "solid",
      images: {
        light: { filePath: "" },
        dark: { filePath: "" },
      },
    },
    modes: {
      light: { wallpaper: { color: "palette_project_foqn_s2_gray_100" } },
      dark: { wallpaper: { color: "palette_project_foqn_s2_gray_000" } },
    },
  });
  assert.match(
    socialPostModuleToRenderable(source).children?.[0]?.id ?? "",
    /^wallpaper\./,
  );
});

test("Social Post rejects a wrong Surface boundary and a missing row Actor", () => {
  const wrongBoundary = fixture();
  const wrongConfig = JSON.parse(wrongBoundary.configJson) as {
    socialPost: { headerSurfaceSlot: { variantReference: string } };
  };
  wrongConfig.socialPost.headerSurfaceSlot.variantReference =
    "component_project_foqn_s2_media::variant::default";
  wrongBoundary.configJson = JSON.stringify(wrongConfig);
  assert.throws(
    () => resolveSocialPostModule(wrongBoundary),
    /headerSurfaceSlot.*must resolve to Component 'surface'/,
  );

  const missingActor = fixture();
  const runtime = JSON.parse(missingActor.designPreviewJson) as Record<string, unknown>;
  const runtimeRows = runtime.socialPostRows as Array<Record<string, unknown>>;
  const firstRowSlots = runtimeRows[0]!.slotInputs as Array<Record<string, unknown>>;
  delete firstRowSlots[0]!.actor;
  missingActor.designPreviewJson = JSON.stringify(runtime);
  assert.throws(
    () => resolveSocialPostModule(missingActor),
    /component\.contentRow\.slots\[0\]\.runtime\.actor/,
  );
});

function findNode(root: RenderableNode, id: string): RenderableNode | undefined {
  if (root.id === id) return root;
  for (const child of root.children ?? []) {
    const found = findNode(child, id);
    if (found) return found;
  }
  return undefined;
}

function findNodeWithPrefix(root: RenderableNode, prefix: string): RenderableNode | undefined {
  if (root.id.startsWith(prefix)) return root;
  for (const child of root.children ?? []) {
    const found = findNodeWithPrefix(child, prefix);
    if (found) return found;
  }
  return undefined;
}

function findNodeBy(
  root: RenderableNode,
  predicate: (node: RenderableNode) => boolean,
): RenderableNode | undefined {
  if (predicate(root)) return root;
  for (const child of root.children ?? []) {
    const found = findNodeBy(child, predicate);
    if (found) return found;
  }
  return undefined;
}

function requiredNode(root: RenderableNode, id: string): RenderableNode {
  const node = findNode(root, id);
  assert.ok(node, `Missing renderable node '${id}'.`);
  return node;
}
