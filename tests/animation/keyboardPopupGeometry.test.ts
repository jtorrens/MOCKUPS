import assert from "node:assert/strict";
import test from "node:test";

import {
  KeyboardPopupWidthRatio,
  keyboardPopupGeometry,
} from "../../src/desktop-preview/keyboardComponentRenderable.js";

const keyboardBox = { x: 10, y: 100, width: 300, height: 180 };

function close(actual: number, expected: number) {
  assert.ok(
    Math.abs(actual - expected) < 0.0001,
    `Expected ${actual} to be close to ${expected}`,
  );
}

test("Keyboard popup forms one wider silhouette through the pressed key", () => {
  const keyBox = { x: 140, y: 200, width: 40, height: 44 };
  const geometry = keyboardPopupGeometry(keyboardBox, keyBox, 6);

  close(geometry.shapeBox.width, keyBox.width * KeyboardPopupWidthRatio);
  close(
    geometry.shapeBox.x + geometry.shapeBox.width / 2,
    keyBox.x + keyBox.width / 2,
  );
  close(
    geometry.shapeBox.y + geometry.shapeBox.height,
    keyBox.y + keyBox.height,
  );
  close(
    geometry.shapeBox.x + geometry.connectorCenterX,
    keyBox.x + keyBox.width / 2,
  );
  assert.equal(geometry.labelBox.width, geometry.shapeBox.width);
  assert.ok(geometry.labelBox.y + geometry.labelBox.height < keyBox.y);
  assert.match(geometry.pathData, /^M/u);
  assert.match(geometry.pathData, /Z$/u);
});

test("Keyboard popup stays inside both frame edges and keeps pointing to its key", () => {
  const leftKey = { x: keyboardBox.x, y: 200, width: 40, height: 44 };
  const rightKey = {
    x: keyboardBox.x + keyboardBox.width - 40,
    y: 200,
    width: 40,
    height: 44,
  };
  const left = keyboardPopupGeometry(keyboardBox, leftKey, 6);
  const right = keyboardPopupGeometry(keyboardBox, rightKey, 6);

  close(left.shapeBox.x, keyboardBox.x);
  close(
    right.shapeBox.x + right.shapeBox.width,
    keyboardBox.x + keyboardBox.width,
  );
  close(
    left.shapeBox.x + left.connectorCenterX,
    leftKey.x + leftKey.width / 2,
  );
  close(
    right.shapeBox.x + right.connectorCenterX,
    rightKey.x + rightKey.width / 2,
  );
  assert.ok(left.connectorCenterX < left.shapeBox.width / 2);
  assert.ok(right.connectorCenterX > right.shapeBox.width / 2);
});

test("Keyboard popup caps its head at an exceptionally narrow frame", () => {
  const narrowKeyboard = { x: 0, y: 0, width: 30, height: 100 };
  const fullWidthKey = { x: 0, y: 40, width: 30, height: 30 };
  const geometry = keyboardPopupGeometry(narrowKeyboard, fullWidthKey, 4);

  close(geometry.shapeBox.x, narrowKeyboard.x);
  close(geometry.shapeBox.width, narrowKeyboard.width);
  close(
    geometry.shapeBox.x + geometry.connectorCenterX,
    fullWidthKey.x + fullWidthKey.width / 2,
  );
});
