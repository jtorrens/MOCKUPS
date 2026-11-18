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

function fixture(variantId = "default") {
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
    const defaultVariant = metadata.variants.find((variant) => variant.id === variantId);
    assert.ok(defaultVariant);
    const runtime = JSON.parse(module.design_preview_json) as Record<string, unknown>;
    // Explicit prepared fixture for the Default Icon Row boundary; Reel uses
    // its authored action identities and Runtime values from the Module fixture.
    if (variantId === "default") {
      const buttonValues = JSON.parse(committedComponentFixture("iconRow").designPreviewJson).buttonInputs;
      const actionContract = (runtime.collections as Array<{ id: string; fields: Array<{ jsonKey: string }> }>).find(item => item.id === "socialPostActions")!;
      const declaredKeys = new Set(["id", ...actionContract.fields.map(field => field.jsonKey)]);
      runtime.socialPostActions = buttonValues.map((item: Record<string, unknown>) =>
        Object.fromEntries(Object.entries(item).filter(([key]) => declaredKeys.has(key))));
    }
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

test("Social Post owns structure-projected Runtime row sections", () => {
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
    "mediaCutout",
    "galleryMediaCutout",
    "mediaSource",
  ]);
  assert.deepEqual(runtime.collections.map(({ id }) => id), [
    "socialPostRows",
    "socialPostFooterRows",
    "socialPostActions",
    "floatingComponents",
  ]);
  assert.deepEqual(contract.rows.map(({ id }) => id), ["item_690ab7d988ef466c8d6b20406ef2e024", "item_d2a1bd7aeb5f49c49501136b62bc4389"]);
  assert.equal(contract.rows[0].content.slots[0].kind, "avatar");
  const headerAvatar = contract.rows[0].content.slots[0].content;
  assert.ok(headerAvatar && "actor" in headerAvatar);
  assert.equal(headerAvatar.actor.id, "system_preview_actor_primary");
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
  assert.deepEqual(contract.footerRows.map(({ id }) => id), ["item_9df0890d96604176aca0bb0a6e7889e2", "item_1729c03bec80498bb124f253df1c1709"]);
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

test("Social Post Media source is independent of Gallery and preserves explicit absence", () => {
  const source = fixture();
  source.projectMediaFiles = ["media/a.jpg", "media/b.jpg"];
  const runtime = JSON.parse(source.designPreviewJson);
  runtime.mediaSource = "media/independent.jpg";
  runtime.galleryDirectory = "media";
  runtime.gallerySelectedIndex = 1;
  source.designPreviewJson = JSON.stringify(runtime);
  const first = resolveSocialPostModule(source);
  assert.equal(first.mediaSource, "media/independent.jpg");
  const before = requiredNode(socialPostModuleToRenderable(source), "module.core.socialPost.media.page.0");
  runtime.gallerySelectedIndex = 0;
  runtime.galleryDirectory = "another";
  source.designPreviewJson = JSON.stringify(runtime);
  assert.deepEqual(requiredNode(socialPostModuleToRenderable(source), "module.core.socialPost.media.page.0"), before);
  runtime.mediaSource = "";
  source.designPreviewJson = JSON.stringify(runtime);
  assert.equal(resolveSocialPostModule(source).mediaSource, "");
  delete runtime.mediaSource;
  source.designPreviewJson = JSON.stringify(runtime);
  assert.throws(() => resolveSocialPostModule(source), /mediaSource/);
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
  runtime.mediaSource = "media/b.mov";
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
  const activeMedia = requiredNode(renderable, "module.core.socialPost.media.page.0");
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
    "component.contentRow.slot.item_5247adbf5f9f4bf59935d1be38fd72db",
  ));
});

test("Social Post renders its two header rows against one Surface", () => {
  const source = fixture();
  const node = socialPostModuleToRenderable(source);
  const config = JSON.parse(source.configJson) as {
    socialPost: { headerHeight: number };
  };
  const header = requiredNode(node, "module.core.socialPost.header");
  const row1 = requiredNode(node, "module.core.socialPost.header.item_690ab7d988ef466c8d6b20406ef2e024");
  const row2 = requiredNode(node, "module.core.socialPost.header.item_d2a1bd7aeb5f49c49501136b62bc4389");
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

test("Social Post resolves row fields by the declared nested ids, without positional aliases", () => {
  const source = fixture();
  const runtime = JSON.parse(source.designPreviewJson) as {
    socialPostRows: Array<{ id: string; slotInputs: Array<{ id: string; label: string }> }>;
  };
  const row = runtime.socialPostRows[1]!;
  const slot = row.slotInputs[2]!;
  slot.label = "payload";
  source.designPreviewJson = JSON.stringify(runtime);
  source.runtimeContractJson = source.designPreviewJson;
  source.instanceJson = JSON.stringify({ animation: { schemaVersion: 2, tracks: [{
    fieldId: `slotInputs.${slot.id}.label`, targetId: row.id,
    keyframes: [{ frame: 0, value: "before", interpolation: "hold" },
      { frame: 5, value: "after", interpolation: "hold" }],
  }] } });
  for (const [frame, expected] of [[4, "before"], [5, "after"], [10, "after"]] as const) {
    source.localFrame = frame;
    const content = resolveSocialPostModule(source).rows[1].content.slots[2].content;
    assert.ok(content && "text" in content);
    assert.equal(content.text, expected);
  }
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
  const row1 = requiredNode(node, "module.core.socialPost.header.item_690ab7d988ef466c8d6b20406ef2e024");
  assert.equal(findNode(header, "module.core.socialPost.header.item_d2a1bd7aeb5f49c49501136b62bc4389"), undefined);
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
  const row1 = requiredNode(node, "module.core.socialPost.footer.item_9df0890d96604176aca0bb0a6e7889e2");
  const navigation = requiredNode(node, "navigation_bar");
  assert.ok(message.box);
  assert.ok(footer.box);
  assert.ok(row1.box);
  assert.ok(navigation.box);
  assert.equal(message.box.y + message.box.height, footer.box.y);
  assert.equal(footer.box.y + footer.box.height, navigation.box.y);
  assert.equal(findNode(footer, "module.core.socialPost.footer.item_1729c03bec80498bb124f253df1c1709"), undefined);
  assert.ok(findNode(footer, "component.contentRow.slot.item_3a5f8a29b07b43f6a589286bd1463ce7"));
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


test("Reel fills the Screen below optional floating sections and vertical actions", () => {
  const source = fixture("reel");
  const contract = resolveSocialPostModule(source);
  assert.equal(contract.actions.orientation, "vertical");
  assert.equal(contract.actions.items.length, 3);
  for (const item of contract.actions.items) {
    assert.equal(item.button.labelPosition, "bottom");
    assert.equal(item.button.contentMode, "iconText");
    assert.ok(item.button.appearance.label?.text);
  }
  const node = socialPostModuleToRenderable(source);
  const media = requiredNode(node, "module.core.socialPost.media.page.0");
  assert.deepEqual(media.box, node.box);
  const children = node.children!;
  assert.ok(children.findIndex(child => child.id === "module.core.socialPost.media") < children.findIndex(child => child.id === "module.core.socialPost.header"));
  assert.ok(requiredNode(node, "module.core.socialPost.actions").box);
  assert.equal(findNode(node, "module.core.socialPost.message"), undefined);
  assert.equal(findNode(node, "module.core.socialPost.gallery"), undefined);
  const config = JSON.parse(source.configJson);
  config.socialPost.showHeader = false;
  config.socialPost.showFooter = false;
  config.socialPost.showActions = false;
  const hidden = socialPostModuleToRenderable({ ...source, configJson: JSON.stringify(config) });
  for (const section of ["header", "footer", "actions"]) assert.equal(findNode(hidden, `module.core.socialPost.${section}`), undefined);
  assert.deepEqual(requiredNode(hidden, "module.core.socialPost.media.page.0").box, media.box);
});

test("Social Post message, input and keyboard have independent presence and floating flow", () => {
  const source = fixture();
  const config = JSON.parse(source.configJson);
  config.socialPost.mediaHeightMode = "fill";
  const runtime = JSON.parse(source.designPreviewJson);
  for (const showMessage of [false, true]) {
    for (const textInputVisible of [false, true]) {
      for (const keyboardVisible of [false, true]) {
        config.socialPost.showMessage = showMessage;
        runtime.messageTextInputVisible = textInputVisible;
        runtime.messageKeyboardVisible = keyboardVisible;
        runtime.messageBubbleRevealMode = "duringWriteOn";
        const payload = { ...source, configJson: JSON.stringify(config), designPreviewJson: JSON.stringify(runtime) };
        const node = socialPostModuleToRenderable(payload);
        assert.equal(!!findNode(node, "module.core.socialPost.message"), showMessage);
        assert.equal(!!findNode(node, "component.textInputBar"), textInputVisible);
        assert.equal(!!findNode(node, "component.keyboard"), keyboardVisible);
      }
    }
  }
  config.socialPost.messageLayout.mode = "float";
  config.socialPost.textInputLayout.mode = "float";
  config.socialPost.keyboardLayout.mode = "float";
  const floating = socialPostModuleToRenderable({ ...source, configJson: JSON.stringify(config), designPreviewJson: JSON.stringify(runtime) });
  config.socialPost.showMessage = false;
  runtime.messageTextInputVisible = false;
  runtime.messageKeyboardVisible = false;
  const absent = socialPostModuleToRenderable({ ...source, configJson: JSON.stringify(config), designPreviewJson: JSON.stringify(runtime) });
  assert.deepEqual(requiredNode(floating, "module.core.socialPost.media").box, requiredNode(absent, "module.core.socialPost.media").box);
});

test("Social Post rejects incomplete or unknown shared section layouts", () => {
  const source = fixture();
  for (const value of [{ mode: "float" }, { mode: "unknown", padding: "theme.spacing.none|theme.spacing.none", placement: {} }]) {
    const config = JSON.parse(source.configJson);
    config.socialPost.headerLayout = value;
    assert.throws(() => resolveSocialPostModule({ ...source, configJson: JSON.stringify(config) }), /requires exactly|must be stack or float/);
  }
});

test("Reel action and footer labels consume independent Screen Runtime values", () => {
  const source = fixture("reel");
  const configBefore = source.configJson;
  for (const label of ["Screen A", "Screen B", ""]) {
    const runtime = JSON.parse(source.designPreviewJson);
    runtime.socialPostActions[0].sampleText = label;
    const footer = runtime.socialPostFooterRows[0].slotInputs.find((slot: { kind: string }) => slot.kind === "icon");
    footer.label = label;
    const payload = { ...source, designPreviewJson: JSON.stringify(runtime), runtimeContractJson: JSON.stringify(runtime) };
    const contract = resolveSocialPostModule(payload);
    assert.equal(contract.actions.items[0].button.appearance.label?.text ?? "", label);
    const button = contract.footerRows[0].content.slots.find(slot => slot.id === footer.id)!.content;
    assert.ok(button && "contentMode" in button);
    assert.equal(button.contentMode, "iconText");
    assert.equal(button.appearance.label?.text ?? "", label);
    const node = socialPostModuleToRenderable(payload);
    assert.ok(node.box.height > 0);
    assert.equal(payload.configJson, configBefore);
  }
  const runtime = JSON.parse(source.designPreviewJson);
  runtime.socialPostActions[0].id = "item_00000000000000000000000000000000";
  assert.throws(() => resolveSocialPostModule({ ...source, designPreviewJson: JSON.stringify(runtime), runtimeContractJson: JSON.stringify(runtime) }), /missing|unknown|match/i);
});

 test("SocialPost resolves arbitrary row membership and rejects unprepared rows", () => {
  const source = fixture();
  const config = JSON.parse(source.configJson);
  const runtime = JSON.parse(source.designPreviewJson);
  const original = config.socialPost.rows[0];
  const originalRuntime = runtime.socialPostRows[0];
  for (const count of [0, 1, 4]) {
    config.socialPost.rows = Array.from({length: count}, (_, i) => ({...structuredClone(original), id: `item_${i.toString(16).padStart(32, "0")}`}));
    runtime.socialPostRows = config.socialPost.rows.map((row: {id: string}) => ({...structuredClone(originalRuntime), id: row.id}));
    source.configJson = JSON.stringify(config);
    source.designPreviewJson = JSON.stringify(runtime);
    assert.equal(resolveSocialPostModule(source).rows.length, count);
    assert.ok(socialPostModuleToRenderable(source));
  }
  runtime.socialPostRows.pop();
  source.designPreviewJson = JSON.stringify(runtime);
  assert.throws(() => resolveSocialPostModule(source), /match the prepared structure/);
 });

 test("Social Post video accumulates Play intervals from Current time and holds at the end", () => {
  for (const variant of ["default", "reel"]) {
    const source = fixture(variant);
    const runtime = JSON.parse(source.designPreviewJson);
    Object.assign(runtime, {isPlaying: true, currentTimeSeconds: 0.2, durationSeconds: 1});
    source.designPreviewJson = JSON.stringify(runtime);
    source.runtimeContractJson = JSON.stringify(runtime);
    source.instanceJson = JSON.stringify({animation:{schemaVersion:2,tracks:[]}});
    const at = (frame: number) => { source.localFrame = frame; return resolveSocialPostModule(source); };
    assert.equal(at(0).mediaCurrentTimeSeconds, 0.2);
    assert.equal(at(10).mediaCurrentTimeSeconds, 0.6000000000000001);
    assert.equal(at(100).mediaCurrentTimeSeconds, 1);
    assert.equal(at(100).mediaIsPlaying, false);
    source.instanceJson = JSON.stringify({animation:{schemaVersion:2,tracks:[{
      fieldId:"isPlaying",keyframes:[{frame:0,value:true},{frame:5,value:false},{frame:10,value:true}]
    }]}});
    assert.equal(at(8).mediaCurrentTimeSeconds, 0.4);
    assert.equal(at(8).mediaIsPlaying, false);
    assert.equal(at(10).mediaCurrentTimeSeconds, 0.4);
    assert.equal(at(15).mediaCurrentTimeSeconds, 0.6000000000000001);
    assert.equal(at(100).mediaCurrentTimeSeconds, 1);
    assert.equal(at(2).mediaCurrentTimeSeconds, 0.28);
  }
 });

test("floating presence uses the same bounded interval for Bubble and Label", async () => {
  const { resolveRuntimePresenceInterval } = await import("../../src/desktop-preview/runtimeCollectionPresence.js");
  for (const componentType of ["bubble", "label"]) {
    const payload = committedComponentFixture(componentType);
    const motion = { transition: "slide", direction: "bottom", bounds: "screen", fade: true, translate: true, scale: false } as const;
    assert.equal(resolveRuntimePresenceInterval(payload, 9, 10, 100, true, 150, motion).visible, false);
    assert.equal(resolveRuntimePresenceInterval(payload, 10, 10, 100, true, 150, motion).presenceMotionKind, "enter");
    assert.equal(resolveRuntimePresenceInterval(payload, 99, 10, 100, true, 150, motion).presenceMotionKind, "exit");
    assert.equal(resolveRuntimePresenceInterval(payload, 100, 10, 100, true, 150, motion).visible, false);
    assert.notEqual(resolveRuntimePresenceInterval(payload, 149, 10, 150, false, 150, motion).presenceMotionKind, "exit");
  }
});
