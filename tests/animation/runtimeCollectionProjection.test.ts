import assert from "node:assert/strict";
import test from "node:test";

import { projectRuntimeCollectionItemInputs } from "../../src/desktop-preview/runtimeCollectionProjection.js";

test("embedded collection projection preserves every Runtime item property", () => {
  const runtimeItem = {
    id: "item_001",
    stringValue: "current",
    booleanValue: true,
    numberValue: 24,
    nullableValue: null,
    objectValue: { mode: "exact" },
  };

  assert.deepEqual(
    projectRuntimeCollectionItemInputs(runtimeItem, { contextualValue: "owner" }),
    {
      ...runtimeItem,
      contextualValue: "owner",
    },
  );
});

test("collection-owner context overrides only the keys it explicitly owns", () => {
  assert.deepEqual(
    projectRuntimeCollectionItemInputs(
      { id: "item_001", inheritedValue: "runtime", untouchedValue: "kept" },
      { inheritedValue: "owner" },
    ),
    { id: "item_001", inheritedValue: "owner", untouchedValue: "kept" },
  );
});
