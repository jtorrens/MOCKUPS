import { calculatePreviewFit } from "./previewSizing.js";

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) {
    throw new Error(message);
  }
}

function assertEqual(actual: unknown, expected: unknown, message: string) {
  assert(
    actual === expected,
    `${message}. Expected ${String(expected)}, received ${String(actual)}`,
  );
}

function assertClose(actual: number, expected: number, message: string) {
  const delta = Math.abs(actual - expected);
  assert(delta < 0.000001, `${message}. Expected ${expected}, received ${actual}`);
}

const deviceWidth = 1290;
const deviceHeight = 2796;

{
  const fit = calculatePreviewFit({
    availableWidth: deviceWidth,
    availableHeight: deviceHeight,
    renderWidth: deviceWidth,
    renderHeight: deviceHeight,
  });
  assertEqual(fit.scale, 1, "Exact device fit should render at 1x");
  assertEqual(fit.width, deviceWidth, "Exact device fit width");
  assertEqual(fit.height, deviceHeight, "Exact device fit height");
}

{
  const fit = calculatePreviewFit({
    availableWidth: deviceWidth * 2,
    availableHeight: deviceHeight * 2,
    renderWidth: deviceWidth,
    renderHeight: deviceHeight,
  });
  assertEqual(fit.scale, 1, "Preview should clamp display zoom to maxScale=1");
  assertEqual(fit.width, deviceWidth, "Clamped preview width");
  assertEqual(fit.height, deviceHeight, "Clamped preview height");
}

{
  const availableWidth = 322;
  const availableHeight = 698;
  const expectedScale = Math.min(
    availableWidth / deviceWidth,
    availableHeight / deviceHeight,
    1,
  );
  const fit = calculatePreviewFit({
    availableWidth,
    availableHeight,
    renderWidth: deviceWidth,
    renderHeight: deviceHeight,
  });
  assertClose(fit.scale, expectedScale, "Portrait preview scale");
  assertEqual(
    fit.width,
    Math.max(1, Math.round(deviceWidth * expectedScale)),
    "Portrait preview width",
  );
  assertEqual(
    fit.height,
    Math.max(1, Math.round(deviceHeight * expectedScale)),
    "Portrait preview height",
  );
}

{
  const fit = calculatePreviewFit({
    availableWidth: 0,
    availableHeight: -10,
    renderWidth: 0,
    renderHeight: Number.NaN,
  });
  assert(Number.isFinite(fit.scale), "Fallback scale should be finite");
  assert(fit.scale > 0, "Fallback scale should be positive");
  assert(fit.width > 0, "Fallback width should be positive");
  assert(fit.height > 0, "Fallback height should be positive");
}

{
  const fitWithoutShell = calculatePreviewFit({
    availableWidth: 360,
    availableHeight: 780,
    renderWidth: deviceWidth,
    renderHeight: deviceHeight,
  });
  const fitWithSameRenderable = calculatePreviewFit({
    availableWidth: 360,
    availableHeight: 780,
    renderWidth: deviceWidth,
    renderHeight: deviceHeight,
  });
  assertEqual(
    fitWithSameRenderable.width,
    fitWithoutShell.width,
    "External phone frame overlay must not change renderable fit width",
  );
  assertEqual(
    fitWithSameRenderable.height,
    fitWithoutShell.height,
    "External phone frame overlay must not change renderable fit height",
  );
}

console.log("Preview sizing validation passed.");
