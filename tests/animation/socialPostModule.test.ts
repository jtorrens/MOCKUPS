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
    runtime.actorId = "actor_alex";
    runtime.actor = avatarRuntime.actor;
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

test("Social Post owns semantic Test Values without exposing a child Runtime contract", () => {
  const source = fixture();
  const contract = resolveSocialPostModule(source);
  const runtime = JSON.parse(source.runtimeContractJson) as {
    inputs: Array<{ id: string }>;
    collections: Array<{ id: string }>;
  };
  const config = JSON.parse(source.configJson) as {
    socialPost: Record<string, unknown>;
  };
  assert.equal(contract.bubbleSlot.variantReference,
    "component_project_foqn_s2_bubble::variant::default");
  assert.equal(Object.hasOwn(config.socialPost, "runtimeContract"), false);
  assert.equal(Object.hasOwn(config.socialPost, "forwarding"), false);
  assert.ok(runtime.inputs.some(({ id }) => id === "caption"));
  assert.ok(runtime.inputs.some(({ id }) => id === "actor"));
  assert.deepEqual(runtime.collections, []);
  assert.equal(contract.bubbleInputs.sampleText,
    (JSON.parse(source.designPreviewJson) as Record<string, unknown>).sampleText);
  assert.equal(contract.bubbleInputs.mediaType, "none");
  assert.equal(contract.mediaInputs.mediaType, "image");
});

test("Social Post renders one four-zone Component Stack", () => {
  const node = socialPostModuleToRenderable(fixture());
  const stack = requiredNode(node, "componentStack");
  const header = requiredNode(node, "header");
  const media = requiredNode(node, "media");
  const message = requiredNode(node, "message");
  const actions = requiredNode(node, "actions");
  assert.equal(node.id, "module.core.socialPost");
  assert.deepEqual(node.box, { x: 0, y: 0, width: 360, height: 720 });
  assert.ok(stack.box);
  assert.ok(header.box);
  assert.ok(media.box);
  assert.ok(message.box);
  assert.ok(actions.box);
  assert.equal(header.box.y, stack.box.y);
  assert.equal(media.box.y - (header.box.y + header.box.height), 4);
  assert.equal(message.box.y - (media.box.y + media.box.height), 4);
  assert.equal(actions.box.y - (message.box.y + message.box.height), 4);
  assert.equal(actions.box.y + actions.box.height, stack.box.y + stack.box.height);
  assert.ok(findNode(header, "collectionStack"));
  assert.ok(findNode(media, "media"));
  assert.ok(findNode(message, "component.bubble"));
  assert.ok(findNode(actions, "component.iconBar"));
});

test("Social Post independently hides system chrome and reveals the composer", () => {
  const source = fixture();
  const config = JSON.parse(source.configJson) as {
    socialPost: {
      showStatusBar: boolean;
      showNavigationBar: boolean;
      showTextInputBar: boolean;
      showKeyboard: boolean;
    };
  };
  config.socialPost.showStatusBar = false;
  config.socialPost.showNavigationBar = false;
  config.socialPost.showTextInputBar = true;
  config.socialPost.showKeyboard = true;
  source.configJson = JSON.stringify(config);
  const runtime = JSON.parse(source.designPreviewJson) as Record<string, unknown>;
  runtime.textInputVisible = true;
  runtime.keyboardVisible = true;
  source.designPreviewJson = JSON.stringify(runtime);
  const node = socialPostModuleToRenderable(source);
  assert.equal(findNode(node, "status_bar"), undefined);
  assert.equal(findNode(node, "navigation_bar"), undefined);
  assert.ok(findNode(node, "component.textInputBar"));
  assert.ok(findNode(node, "component.keyboard"));
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

test("Social Post rejects a wrong Bubble boundary and a missing owned Actor", () => {
  const wrongBoundary = fixture();
  const wrongConfig = JSON.parse(wrongBoundary.configJson) as {
    socialPost: { bubbleSlot: { variantReference: string } };
  };
  wrongConfig.socialPost.bubbleSlot.variantReference =
    "component_project_foqn_s2_media::variant::default";
  wrongBoundary.configJson = JSON.stringify(wrongConfig);
  assert.throws(
    () => resolveSocialPostModule(wrongBoundary),
    /bubbleSlot.*must resolve to Component 'bubble'/,
  );

  const missingActor = fixture();
  const runtime = JSON.parse(missingActor.designPreviewJson) as Record<string, unknown>;
  delete runtime.actor;
  missingActor.designPreviewJson = JSON.stringify(runtime);
  assert.throws(
    () => resolveSocialPostModule(missingActor),
    /module\.core\.socialPost\.actor/,
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

function requiredNode(root: RenderableNode, id: string): RenderableNode {
  const node = findNode(root, id);
  assert.ok(node, `Missing renderable node '${id}'.`);
  return node;
}
