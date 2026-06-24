import { createServer } from "node:http";
import { execFile } from "node:child_process";
import { createReadStream, statSync } from "node:fs";
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { promisify } from "node:util";
import { createDatabase } from "../persistence/sqlite/createDatabase.js";
import { developmentDatabasePath } from "../persistence/sqlite/paths.js";
import type { RenderableNode } from "../visual/renderable/types.js";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import {
  listDebugOptions,
  loadAppState,
  loadDebugPayload,
  saveDebugPayload,
  createAppRecord,
  deleteAppRecord,
  duplicateAppRecord,
  updateAppRecord,
  type AppCreateRequest,
  type AppRecordActionRequest,
  type AppUpdateRequest,
  type DebugSaveRequest,
  type DebugSelection,
} from "./debugService.js";

const PORT = 4174;
const database = createDatabase(developmentDatabasePath);
const execFileAsync = promisify(execFile);
const renderOutputDir = path.resolve("out");
const currentFramePng = path.join(renderOutputDir, "current-frame.png");
const currentFrameProps = path.join(renderOutputDir, "current-frame-props.json");

function absoluteServerUrl(request: import("node:http").IncomingMessage) {
  return `http://${request.headers.host ?? `127.0.0.1:${PORT}`}`;
}

function mediaContentType(filePath: string) {
  const extension = path.extname(filePath).toLowerCase();
  if (extension === ".jpg" || extension === ".jpeg") return "image/jpeg";
  if (extension === ".webp") return "image/webp";
  if (extension === ".gif") return "image/gif";
  if (extension === ".svg") return "image/svg+xml";
  if (extension === ".avif") return "image/avif";
  return "image/png";
}

function productionMediaRoot(productionId: string) {
  const row = database
    .prepare("SELECT settings_json FROM productions WHERE id = ?")
    .get(productionId) as { settings_json: string } | undefined;
  if (!row) return "";
  try {
    const settings = JSON.parse(row.settings_json) as unknown;
    if (
      settings &&
      typeof settings === "object" &&
      !Array.isArray(settings) &&
      typeof (settings as Record<string, unknown>).mediaRoot === "string"
    ) {
      return (settings as Record<string, string>).mediaRoot;
    }
  } catch {
    return "";
  }
  return "";
}

function resolveProductionMediaPath(productionId: string, requestedPath: string) {
  const mediaRoot = productionMediaRoot(productionId);
  if (!mediaRoot && path.isAbsolute(requestedPath)) {
    return path.resolve(requestedPath);
  }
  if (!mediaRoot) {
    throw new Error("Production media root is not set");
  }
  const rootPath = path.resolve(mediaRoot);
  const resolvedPath = path.isAbsolute(requestedPath)
    ? path.resolve(requestedPath)
    : path.resolve(rootPath, requestedPath);
  const relativePath = path.relative(rootPath, resolvedPath);
  if (
    relativePath.startsWith("..") ||
    path.isAbsolute(relativePath)
  ) {
    throw new Error("Media path is outside production media root");
  }
  return resolvedPath;
}

function sendJson(
  response: import("node:http").ServerResponse,
  status: number,
  value: unknown,
) {
  response.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Cache-Control": "no-store",
  });
  response.end(JSON.stringify(value));
}

function extractCssUrl(value: unknown) {
  if (typeof value !== "string") return "";
  const trimmed = value.trim();
  const match = /^url\((['"]?)(.*?)\1\)$/i.exec(trimmed);
  return match?.[2] ?? "";
}

function cssUrl(value: string) {
  return `url("${value.replace(/"/g, '\\"')}")`;
}

function absolutizeRenderableUrls(
  node: RenderableNode,
  origin: string,
): RenderableNode {
  const backgroundUrl = extractCssUrl(node.style?.backgroundImage);
  const nextStyle =
    backgroundUrl && node.style && backgroundUrl.startsWith("/api/")
      ? {
          ...node.style,
          backgroundImage: cssUrl(new URL(backgroundUrl, origin).toString()),
        }
      : node.style;
  return {
    ...node,
    ...(nextStyle ? { style: nextStyle } : {}),
    ...(node.children
      ? {
          children: node.children.map((child) =>
            absolutizeRenderableUrls(child, origin),
          ),
        }
      : {}),
  };
}

function readOutputScale(selection: DebugSelection) {
  const row = database
    .prepare("SELECT transform_json FROM screen_instances WHERE id = ?")
    .get(selection.screenInstanceId) as { transform_json?: string } | undefined;
  if (!row?.transform_json) return 1;
  try {
    const transform = JSON.parse(row.transform_json) as unknown;
    if (
      transform &&
      typeof transform === "object" &&
      !Array.isArray(transform) &&
      typeof (transform as Record<string, unknown>).scale === "number" &&
      Number.isFinite((transform as Record<string, number>).scale) &&
      (transform as Record<string, number>).scale > 0
    ) {
      return (transform as Record<string, number>).scale;
    }
  } catch {
    return 1;
  }
  return 1;
}

async function renderCurrentFramePng(
  request: import("node:http").IncomingMessage,
  selection: DebugSelection & { includeFrame?: boolean },
) {
  const payload = loadDebugPayload(database, selection);
  if (!payload.renderable) {
    throw new Error("Selected preview has no renderable output");
  }
  await mkdir(renderOutputDir, { recursive: true });
  const renderable = absolutizeRenderableUrls(
    RenderableNodeSchema.parse(payload.renderable),
    absoluteServerUrl(request),
  );
  const outputScale = readOutputScale(selection);
  const outputWidth = Math.round((renderable.box?.width ?? 1) * outputScale);
  const outputHeight = Math.round((renderable.box?.height ?? 1) * outputScale);
  await writeFile(
    currentFrameProps,
    JSON.stringify(
      {
        includeFrame: selection.includeFrame === true,
        renderable,
      },
      null,
      2,
    ),
    "utf8",
  );
  await execFileAsync(
    path.resolve("node_modules/.bin/remotion"),
    [
      "still",
      "src/remotion/index.ts",
      "ChatScreenPreview",
      currentFramePng,
      "--frame=0",
      "--overwrite",
      `--scale=${outputScale}`,
      `--props=${currentFrameProps}`,
    ],
    {
      cwd: process.cwd(),
      maxBuffer: 1024 * 1024 * 8,
    },
  );
  return {
    url: "/api/render-output/current-frame.png",
    filePath: currentFramePng,
    outputHeight,
    outputScale,
    outputWidth,
    selection,
  };
}

async function readJson(request: import("node:http").IncomingMessage) {
  const chunks: Buffer[] = [];
  for await (const chunk of request) {
    chunks.push(Buffer.from(chunk));
  }
  return JSON.parse(Buffer.concat(chunks).toString("utf8"));
}

const server = createServer(async (request, response) => {
  try {
    const url = new URL(
      request.url ?? "/",
      `http://${request.headers.host ?? `127.0.0.1:${PORT}`}`,
    );
    if (request.method === "GET" && url.pathname === "/api/options") {
      sendJson(response, 200, listDebugOptions(database));
      return;
    }
    if (request.method === "GET" && url.pathname === "/api/app") {
      sendJson(response, 200, loadAppState(database));
      return;
    }
    if (request.method === "PATCH" && url.pathname === "/api/app/record") {
      const body = (await readJson(request)) as AppUpdateRequest;
      sendJson(response, 200, updateAppRecord(database, body));
      return;
    }
    if (request.method === "POST" && url.pathname === "/api/app/record") {
      const body = (await readJson(request)) as AppCreateRequest;
      sendJson(response, 200, createAppRecord(database, body));
      return;
    }
    if (request.method === "POST" && url.pathname === "/api/app/record/duplicate") {
      const body = (await readJson(request)) as AppRecordActionRequest;
      sendJson(response, 200, duplicateAppRecord(database, body));
      return;
    }
    if (request.method === "DELETE" && url.pathname === "/api/app/record") {
      const body: AppRecordActionRequest = {
        tableId: url.searchParams.get("tableId") as AppRecordActionRequest["tableId"],
        recordId: url.searchParams.get("recordId") ?? "",
      };
      sendJson(response, 200, deleteAppRecord(database, body));
      return;
    }
    if (request.method === "GET" && url.pathname === "/api/app/preview") {
      const selection: DebugSelection = {
        productionId: url.searchParams.get("productionId") ?? "",
        shotId: url.searchParams.get("shotId") ?? "",
        screenInstanceId: url.searchParams.get("screenInstanceId") ?? "",
        frame: Number(url.searchParams.get("frame")),
      };
      sendJson(response, 200, loadDebugPayload(database, selection));
      return;
    }
    if (request.method === "POST" && url.pathname === "/api/app/render-frame") {
      const selection = (await readJson(request)) as DebugSelection & {
        includeFrame?: boolean;
      };
      sendJson(response, 200, await renderCurrentFramePng(request, selection));
      return;
    }
    if (request.method === "GET" && url.pathname === "/api/render-output/current-frame.png") {
      const stats = statSync(currentFramePng);
      response.writeHead(200, {
        "Content-Type": "image/png",
        "Content-Length": stats.size,
        "Cache-Control": "no-store",
      });
      createReadStream(currentFramePng).pipe(response);
      return;
    }
    if (request.method === "GET" && url.pathname === "/api/media") {
      const productionId = url.searchParams.get("productionId") ?? "";
      const requestedPath = url.searchParams.get("path") ?? "";
      if (!productionId || !requestedPath) {
        sendJson(response, 400, { error: "Missing productionId or path" });
        return;
      }
      const filePath = resolveProductionMediaPath(productionId, requestedPath);
      const stats = statSync(filePath);
      if (!stats.isFile()) {
        sendJson(response, 404, { error: "Media file not found" });
        return;
      }
      response.writeHead(200, {
        "Content-Type": mediaContentType(filePath),
        "Content-Length": stats.size,
        "Cache-Control": "no-store",
      });
      createReadStream(filePath).pipe(response);
      return;
    }
    if (request.method === "GET" && url.pathname === "/api/debug") {
      const selection: DebugSelection = {
        productionId: url.searchParams.get("productionId") ?? "",
        shotId: url.searchParams.get("shotId") ?? "",
        screenInstanceId: url.searchParams.get("screenInstanceId") ?? "",
        frame: Number(url.searchParams.get("frame")),
      };
      sendJson(response, 200, loadDebugPayload(database, selection));
      return;
    }
    if (request.method === "PUT" && url.pathname === "/api/debug") {
      const body = (await readJson(request)) as DebugSaveRequest;
      sendJson(response, 200, saveDebugPayload(database, body));
      return;
    }
    if (request.method === "GET" && url.pathname === "/api/health") {
      sendJson(response, 200, { ok: true, database: developmentDatabasePath });
      return;
    }
    sendJson(response, 404, { error: "Not found" });
  } catch (error) {
    sendJson(response, 400, {
      error: error instanceof Error ? error.message : String(error),
    });
  }
});

server.listen(PORT, "127.0.0.1", () => {
  console.log(`MOCKUPS debug API: http://127.0.0.1:${PORT}`);
});

function close() {
  server.close(() => {
    database.close();
    process.exit(0);
  });
}

process.on("SIGINT", close);
process.on("SIGTERM", close);
