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
    runtimeRows[0]!.slot1Actor = avatarRuntime.actor;
    const footerRuntimeRows = runtime.socialPostFooterRows as Array<Record<string, unknown>>;
    footerRuntimeRows[0]!.slot1Actor = avatarRuntime.actor;
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
    "mediaSource",
    "mediaHeight",
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
  assert.equal(contract.rows[0].slots[0].kind, "avatar");
  assert.equal(contract.rows[0].slots[0].inputs.actorId, "actor_alex");
  assert.equal(contract.rows[0].slots[0].inputs.sampleText, "Alex Q");
  assert.equal(contract.rows[1].slots[0].kind, "label");
  assert.equal(contract.rows[1].slots[0].inputs.sampleText, "#FOQN");
  assert.deepEqual(contract.footerRows.map(({ id }) => id), ["row1", "row2"]);
  assert.equal(contract.footerRows[0].slots[0].kind, "avatar");
  assert.equal(contract.footerRows[1].slots[0].kind, "label");
});

test("Social Post renders its two header rows against one Surface", () => {
  const node = socialPostModuleToRenderable(fixture());
  const header = requiredNode(node, "module.core.socialPost.header");
  const row1 = requiredNode(node, "module.core.socialPost.header.row1");
  const row2 = requiredNode(node, "module.core.socialPost.header.row2");
  const separator = requiredNode(node, "module.core.socialPost.header.row2.separator");
  assert.equal(node.id, "module.core.socialPost");
  assert.deepEqual(node.box, { x: 0, y: 0, width: 360, height: 720 });
  assert.ok(header.box);
  assert.ok(row1.box);
  assert.ok(row2.box);
  assert.ok(separator.box);
  assert.equal(header.box.height, 130);
  assert.equal(row2.box.y, row1.box.y + row1.box.height + 4);
  assert.equal(separator.box.y + separator.box.height, row2.box.y + row2.box.height);
  assert.ok(findNode(header, "component.avatar"));
  assert.ok(findNode(header, "component.label"));
  assert.equal(header.children?.[0]?.type, "surface");
});

test("Social Post renders the same two-row contract as a footer above navigation", () => {
  const node = socialPostModuleToRenderable(fixture());
  const message = requiredNode(node, "module.core.socialPost.message");
  const footer = requiredNode(node, "module.core.socialPost.footer");
  const row1 = requiredNode(node, "module.core.socialPost.footer.row1");
  const row2 = requiredNode(node, "module.core.socialPost.footer.row2");
  const navigation = requiredNode(node, "navigation_bar");
  assert.ok(message.box);
  assert.ok(footer.box);
  assert.ok(row1.box);
  assert.ok(row2.box);
  assert.ok(navigation.box);
  assert.equal(message.box.y + message.box.height, footer.box.y);
  assert.equal(footer.box.y + footer.box.height, navigation.box.y);
  assert.equal(row2.box.y, row1.box.y + row1.box.height + 4);
  assert.ok(findNode(footer, "component.avatar"));
  assert.ok(findNode(footer, "component.label"));
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
  delete runtimeRows[0]!.slot1Actor;
  missingActor.designPreviewJson = JSON.stringify(runtime);
  assert.throws(
    () => resolveSocialPostModule(missingActor),
    /module\.core\.socialPost\.header\.row1\.slot1Actor/,
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
