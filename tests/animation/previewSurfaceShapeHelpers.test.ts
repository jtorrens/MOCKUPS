import assert from "node:assert/strict";
import test from "node:test";

import {
  surfaceTailBox,
  type SurfaceTailShape,
} from "../../src/desktop-preview/previewSurfaceShapeHelpers.js";

const body = {
  x: 10,
  y: 20,
  width: 100,
  height: 60,
};

function tail(
  side: "left" | "right",
  vertical: "top" | "bottom",
): SurfaceTailShape {
  return {
    side,
    vertical,
    style: "rounded_wedge",
    width: 24,
    height: 16,
    cornerRadius: 12,
    outerCornerRadius: 0,
  };
}

test("generic Surface tails cover the complete body corner without a seam", () => {
  assert.deepEqual(
    surfaceTailBox(body, tail("left", "top")),
    { x: -1, y: 20, width: 24, height: 16 },
  );
  assert.deepEqual(
    surfaceTailBox(body, tail("right", "top")),
    { x: 97, y: 20, width: 24, height: 16 },
  );
  assert.deepEqual(
    surfaceTailBox(body, tail("left", "bottom")),
    { x: -1, y: 64, width: 24, height: 16 },
  );
  assert.deepEqual(
    surfaceTailBox(body, tail("right", "bottom")),
    { x: 97, y: 64, width: 24, height: 16 },
  );
});
