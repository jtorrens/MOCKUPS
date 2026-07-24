import assert from "node:assert/strict";
import path from "node:path";
import test from "node:test";

import Database from "better-sqlite3";

import { chatListModuleToRenderable } from "../../src/desktop-preview/chatListModuleRenderable.js";
import { resolveChatListModule } from "../../src/desktop-preview/chatListModuleResolver.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

type ModuleRow = {
  app_id: string;
  design_preview_json: string;
  metadata_json: string;
};

function fixture() {
  const list = committedComponentFixture("list", "chats");
  const database = new Database(
    path.join(process.cwd(), "data", "desktop-editor-spike.sqlite"),
    { readonly: true, fileMustExist: true },
  );
  try {
    const module = database.prepare(`
      SELECT app_id, design_preview_json, metadata_json
      FROM modules
      WHERE record_class_id = 'module.core.chatList'
    `).get() as ModuleRow | undefined;
    assert.ok(module);
    assert.equal(module.app_id, "app_core_chat");
    const metadata = JSON.parse(module.metadata_json) as {
      variants: Array<{ id: string; config: Record<string, unknown> }>;
    };
    const defaultVariant = metadata.variants.find((variant) => variant.id === "default");
    assert.ok(defaultVariant);
    const persistedRuntime = JSON.parse(module.design_preview_json) as {
      animationTimeline: Record<string, unknown>;
    };
    const runtime = JSON.parse(list.designPreviewJson) as Record<string, unknown>;
    runtime.componentType = "module.core.chatList";
    runtime.animationTimeline = persistedRuntime.animationTimeline;
    return {
      ...list,
      kind: "module" as const,
      componentType: "module.core.chatList",
      configJson: JSON.stringify(defaultVariant.config),
      designPreviewJson: JSON.stringify(runtime),
      runtimeContractJson: JSON.stringify(runtime),
    };
  } finally {
    database.close();
  }
}

test("Chat List belongs to the Chat App and exposes the exact List Runtime contract", () => {
  const source = fixture();
  const contract = resolveChatListModule(source);
  const runtime = JSON.parse(source.runtimeContractJson) as {
    inputs: Array<{ id: string }>;
    collections: Array<{ id: string }>;
  };

  assert.equal(contract.listSlot.variantReference,
    "component_project_foqn_s2_list::variant::chats");
  assert.deepEqual(runtime.inputs.map(({ id }) => id), ["itemWidth", "itemHeight"]);
  assert.deepEqual(runtime.collections.map(({ id }) => id), ["items"]);
  assert.deepEqual(contract.listInputs, JSON.parse(source.designPreviewJson));
});

test("Chat List composes the exact List boundary at the configured Screen placement", () => {
  const source = fixture();
  const node = chatListModuleToRenderable(source);
  const list = node.children?.find((child) => child.id === "component.list");

  assert.equal(node.id, "module.core.chatList");
  assert.deepEqual(node.box, { x: 0, y: 0, width: 360, height: 720 });
  assert.ok(list?.box);
  assert.equal(list.box.x, 0);
  assert.equal(list.box.y, 0);
  assert.equal(list.box.width, 360);
  assert.ok((list.children?.length ?? 0) > 0);
});

test("Chat List rejects a different Component type and a reduced Runtime contract", () => {
  const wrongSlot = fixture();
  const wrongSlotConfig = JSON.parse(wrongSlot.configJson) as {
    chatList: { listSlot: { variantReference: string } };
  };
  wrongSlotConfig.chatList.listSlot.variantReference =
    "component_project_foqn_s2_list_item::variant::chats";
  wrongSlot.configJson = JSON.stringify(wrongSlotConfig);
  assert.throws(
    () => resolveChatListModule(wrongSlot),
    /listSlot.*must resolve to Component 'list'/,
  );

  const reduced = fixture();
  const reducedRuntime = JSON.parse(reduced.runtimeContractJson) as {
    inputs: Array<{ id: string }>;
  };
  reducedRuntime.inputs = reducedRuntime.inputs.slice(0, 1);
  reduced.runtimeContractJson = JSON.stringify(reducedRuntime);
  assert.throws(
    () => resolveChatListModule(reduced),
    /Runtime contract.inputs must be exactly itemWidth, itemHeight/,
  );
});
