import { readFileSync } from "node:fs";
import type { DesignPreviewPayload } from "../src/desktop-preview/designPreviewPayload.js";
import { renderDesignPreviewRenderable } from "../src/desktop-preview/renderDesignPreviewMarkup.js";
import { renderableToSvg } from "../src/desktop-preview/RenderableSvgAdapter.js";

const payloadPath = process.argv[2];
if (!payloadPath) {
  throw new Error("Missing design preview payload path.");
}

const targetArgument = process.argv.slice(3).find((argument) => argument.startsWith("--target="));
const target = targetArgument?.slice("--target=".length);
if (target !== undefined && target !== "web" && target !== "affinity") {
  throw new Error("SVG target must be 'web' or 'affinity'.");
}

const payload = JSON.parse(readFileSync(payloadPath, "utf8")) as DesignPreviewPayload;
process.stdout.write(renderableToSvg(renderDesignPreviewRenderable(payload), { target }));
