import { mkdir } from "node:fs/promises";
import path from "node:path";
import readline from "node:readline";
import { chromium, type Browser, type Page } from "playwright";

interface RasterRequest {
  id: string;
  html: string;
  width: number;
  height: number;
  outputPath: string;
  format: "webp" | "png";
  quality?: number;
  captureScale?: number;
  assets?: Array<{ key: string; uri: string }>;
}

let browser: Browser | undefined;
let page: Page | undefined;
let loadedViewport = "";
const previewAssets = new Map<string, string>();

async function registerBrowserAssets(activePage: Page, assets: Array<{ key: string; uri: string }>) {
  if (assets.length === 0) return;
  await activePage.evaluate((incoming) => {
    const state = globalThis as typeof globalThis & { __mockupsRasterAssets?: Record<string, string> };
    const registry = state.__mockupsRasterAssets ??= {};
    for (const asset of incoming) {
      if (registry[asset.key]) continue;
      const comma = asset.uri.indexOf(",");
      const header = asset.uri.slice(0, comma);
      const encoded = asset.uri.slice(comma + 1);
      const mime = /^data:([^;,]+)/.exec(header)?.[1] ?? "application/octet-stream";
      const bytes = header.includes(";base64")
        ? Uint8Array.from(atob(encoded), (character) => character.charCodeAt(0))
        : new TextEncoder().encode(decodeURIComponent(encoded));
      registry[asset.key] = URL.createObjectURL(new Blob([bytes], { type: mime }));
    }
  }, assets);
}

async function hydrateBrowserAssets(activePage: Page) {
  await activePage.evaluate(() => {
    const state = globalThis as typeof globalThis & { __mockupsRasterAssets?: Record<string, string> };
    const registry = state.__mockupsRasterAssets ?? {};
    const resolve = (value: string) => value.replace(
      /mockups-asset:([a-f0-9]{64})/g,
      (token, key) => registry[key] ?? token,
    );
    for (const element of document.querySelectorAll<HTMLElement>("[src], [poster], [style]")) {
      for (const attribute of ["src", "poster", "style"]) {
        const value = element.getAttribute(attribute);
        if (value?.includes("mockups-asset:")) element.setAttribute(attribute, resolve(value));
      }
    }
    for (const style of document.querySelectorAll<HTMLStyleElement>("style")) {
      if (style.textContent?.includes("mockups-asset:")) style.textContent = resolve(style.textContent);
    }
  });
}

async function ensurePage(width: number, height: number) {
  browser ??= await chromium.launch({ headless: true });
  if (!page) page = await browser.newPage({ viewport: { width, height }, deviceScaleFactor: 1 });
  await page.setViewportSize({ width, height });
  return page;
}

async function rasterize(request: RasterRequest) {
  for (const asset of request.assets ?? []) previewAssets.set(asset.key, asset.uri);
  const html = request.html;
  const activePage = await ensurePage(request.width, request.height);
  const renderStartedAt = performance.now();
  const patchStartedAt = performance.now();
  const viewportKey = `${request.width}x${request.height}`;
  const resetsDocument = loadedViewport !== viewportKey;
  if (resetsDocument) {
    await activePage.setContent(html, { waitUntil: "domcontentloaded", timeout: 15_000 });
    loadedViewport = viewportKey;
  } else {
    const bodyMatch = /<body(?:\s[^>]*)?>([\s\S]*?)<\/body>/i.exec(html);
    if (!bodyMatch) throw new Error("Raster document body is unavailable for incremental replacement");
    await activePage.evaluate((nextBodyHtml) => {
      document.body.style.visibility = "hidden";
      const fragment = document.createElement("template");
      fragment.innerHTML = nextBodyHtml;
      const selector = '[data-renderable-id="design_preview.surface"]';
      const nextRoot = fragment.content.querySelector(selector);
      const currentRoot = document.querySelector(selector);
      if (!nextRoot || !currentRoot) throw new Error("Raster renderable root is unavailable for incremental replacement");
      currentRoot.replaceWith(document.importNode(nextRoot, true));
    }, bodyMatch[1]);
  }
  const patchMs = performance.now() - patchStartedAt;
  const assetsStartedAt = performance.now();
  const browserAssets = resetsDocument
    ? [...previewAssets].map(([key, uri]) => ({ key, uri }))
    : request.assets ?? [];
  await registerBrowserAssets(activePage, browserAssets);
  await hydrateBrowserAssets(activePage);
  const assetsMs = performance.now() - assetsStartedAt;
  const readyStartedAt = performance.now();
  await activePage.evaluate(async () => {
    const timeout = (label: string) => new Promise<never>((_, reject) => {
      window.setTimeout(() => reject(new Error(`${label} timed out`)), 10_000);
    });
    if (document.fonts?.ready) await Promise.race([document.fonts.ready, timeout("Fonts")]);
    const imageWait = Promise.all([...document.images].map((image) => {
      const source = image.currentSrc || image.getAttribute("src") || "";
      if (!source) return Promise.resolve();
      if (image.complete) {
        return image.naturalWidth > 0
          ? Promise.resolve()
          : Promise.reject(new Error(`Image failed before capture: ${source.slice(0, 160)}`));
      }
      return new Promise<void>((resolve, reject) => {
        image.addEventListener("load", () => resolve(), { once: true });
        image.addEventListener("error", () => reject(new Error(`Image failed: ${image.currentSrc || image.src}`)), { once: true });
      });
    }));
    await Promise.race([imageWait, timeout("Images")]);
    document.body.style.visibility = "visible";
  });
  const readyMs = performance.now() - readyStartedAt;
  const root = activePage.locator('[data-renderable-id="design_preview.surface"]').first();
  await root.waitFor({ state: "visible" });
  const box = await root.boundingBox();
  if (!box) throw new Error("Renderable root has no capture bounds");
  const renderMs = performance.now() - renderStartedAt;
  await mkdir(path.dirname(request.outputPath), { recursive: true });
  const captureStartedAt = performance.now();
  if (request.format === "png") {
    await activePage.screenshot({
      path: request.outputPath,
      type: "png",
      clip: box,
      scale: "css",
      animations: "disabled",
    });
  } else {
    const session = await activePage.context().newCDPSession(activePage);
    try {
      const result = await session.send("Page.captureScreenshot", {
        format: "webp",
        quality: Math.max(0, Math.min(100, request.quality ?? 95)),
        fromSurface: true,
        captureBeyondViewport: false,
        optimizeForSpeed: true,
        clip: {
          x: box.x,
          y: box.y,
          width: box.width,
          height: box.height,
          scale: Math.max(0.01, request.captureScale ?? 1),
        },
      });
      await BunWriteCompat(request.outputPath, Buffer.from(result.data, "base64"));
    } finally {
      await session.detach();
    }
  }
  return { renderMs, patchMs, assetsMs, readyMs, captureMs: performance.now() - captureStartedAt, width: box.width, height: box.height };
}

async function BunWriteCompat(outputPath: string, data: Buffer) {
  const { writeFile } = await import("node:fs/promises");
  await writeFile(outputPath, data);
}

const input = readline.createInterface({ input: process.stdin, crlfDelay: Number.POSITIVE_INFINITY });
input.on("line", async (line) => {
  if (!line.trim()) return;
  let id = "";
  try {
    const request = JSON.parse(line) as RasterRequest;
    id = request.id;
    const metrics = await rasterize(request);
    process.stdout.write(`${JSON.stringify({ id, ok: true, ...metrics })}\n`);
  } catch (error) {
    const message = error instanceof Error ? `${error.name}: ${error.message}\n${error.stack ?? ""}` : String(error);
    process.stdout.write(`${JSON.stringify({ id, ok: false, error: message })}\n`);
  }
});

async function shutdown() {
  await browser?.close();
  process.exit(0);
}
process.once("SIGTERM", shutdown);
process.once("SIGINT", shutdown);
