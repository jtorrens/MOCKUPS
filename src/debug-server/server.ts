import { createServer } from "node:http";
import { execFile } from "node:child_process";
import { createHash } from "node:crypto";
import { createReadStream, statSync } from "node:fs";
import { copyFile, mkdir, writeFile } from "node:fs/promises";
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
const mediaFrameDir = path.join(renderOutputDir, "media-frames");

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
  if (extension === ".mp4" || extension === ".m4v") return "video/mp4";
  if (extension === ".mov") return "video/quicktime";
  if (extension === ".webm") return "video/webm";
  return "image/png";
}

function mediaFrameFilePath({
  fps,
  frame,
  sourcePath,
}: {
  fps: number;
  frame: number;
  sourcePath: string;
}) {
  const hash = createHash("sha1")
    .update(`${path.resolve(sourcePath)}:${frame}:${fps}`)
    .digest("hex");
  return path.join(mediaFrameDir, `${hash}.png`);
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

function safeFilePart(value: unknown, fallback: string) {
  const source = typeof value === "string" && value.trim() ? value : fallback;
  return source
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9._-]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .slice(0, 80) || fallback;
}

function safeRelativePathPart(value: unknown, fallback: string) {
  return safeFilePart(value, fallback).replace(/^\.+/, "") || fallback;
}

function titleCaseFilePart(value: string) {
  return value
    .replace(/[_-]+/g, " ")
    .trim()
    .replace(/\s+/g, " ")
    .replace(/\b\w/g, (match) => match.toUpperCase());
}

function inferFontNameParts(sourcePath: string) {
  const parsed = path.parse(sourcePath);
  const name = parsed.name.trim();
  const segments = name.split(/[-_]+/).filter(Boolean);
  if (segments.length <= 1) {
    return {
      family: titleCaseFilePart(name) || "New Font",
      style: "Regular",
    };
  }
  return {
    family: titleCaseFilePart(segments.slice(0, -1).join(" ")),
    style: titleCaseFilePart(segments[segments.length - 1] ?? "Regular"),
  };
}

function frameFileName(selection: DebugSelection) {
  const row = database
    .prepare(
      `SELECT
        p.slug AS productionSlug,
        p.name AS productionName,
        e.slug AS episodeSlug,
        e.name AS episodeName,
        s.slug AS shotSlug,
        s.name AS shotName,
        s.version AS shotVersion
      FROM shots s
      JOIN productions p ON p.id = s.production_id
      LEFT JOIN episodes e ON e.id = s.episode_id
      WHERE s.id = ?`,
    )
    .get(selection.shotId) as
    | {
        productionSlug?: string | null;
        productionName?: string | null;
        episodeSlug?: string | null;
        episodeName?: string | null;
        shotSlug?: string | null;
        shotName?: string | null;
        shotVersion?: number | null;
      }
    | undefined;
  const production = safeFilePart(
    row?.productionSlug ?? row?.productionName,
    selection.productionId,
  );
  const episode = safeFilePart(row?.episodeSlug ?? row?.episodeName, "episode");
  const shot = safeFilePart(row?.shotSlug ?? row?.shotName, selection.shotId);
  const version = String(row?.shotVersion ?? 1).padStart(2, "0");
  const frame = String(selection.frame).padStart(6, "0");
  return `${production}_${episode}_${shot}_v${version}_f${frame}.png`;
}

function renderFrameOutputPath(selection: DebugSelection) {
  const mediaRoot = productionMediaRoot(selection.productionId);
  const root = mediaRoot
    ? path.resolve(mediaRoot)
    : path.join(renderOutputDir, "renders", selection.productionId);
  const directory = path.join(root, "renders", "frames");
  const filePath = path.join(directory, frameFileName(selection));
  return {
    directory,
    filePath,
    relativeFilePath: path.relative(root, filePath),
  };
}

function assertReadableRenderOutputPath(filePath: string) {
  const resolved = path.resolve(filePath);
  const outputRelative = path.relative(renderOutputDir, resolved);
  if (!outputRelative.startsWith("..") && !path.isAbsolute(outputRelative)) {
    return resolved;
  }
  const productions = database
    .prepare("SELECT settings_json FROM productions")
    .all() as { settings_json: string }[];
  for (const production of productions) {
    try {
      const settings = JSON.parse(production.settings_json) as unknown;
      const mediaRoot =
        settings &&
        typeof settings === "object" &&
        !Array.isArray(settings) &&
        typeof (settings as Record<string, unknown>).mediaRoot === "string"
          ? (settings as Record<string, string>).mediaRoot
          : "";
      if (!mediaRoot) continue;
      const rootRelative = path.relative(path.resolve(mediaRoot), resolved);
      if (!rootRelative.startsWith("..") && !path.isAbsolute(rootRelative)) {
        return resolved;
      }
    } catch {
      // Ignore malformed settings while checking readable render outputs.
    }
  }
  throw new Error("Render output path is outside allowed output roots");
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

async function importProductionFont({
  productionId,
  recordId,
  sourcePath,
}: {
  productionId: string;
  recordId: string;
  sourcePath: string;
}) {
  if (!productionId || !recordId || !sourcePath) {
    throw new Error("Missing productionId, recordId or sourcePath");
  }
  const mediaRoot = productionMediaRoot(productionId);
  if (!mediaRoot) {
    throw new Error("Production media root is not set");
  }
  const extension = path.extname(sourcePath).toLowerCase();
  if (![".ttf", ".otf", ".woff", ".woff2"].includes(extension)) {
    throw new Error("Choose a .ttf, .otf, .woff or .woff2 font file");
  }
  const existing = database
    .prepare("SELECT id FROM production_fonts WHERE id = ? AND production_id = ?")
    .get(recordId, productionId);
  if (!existing) {
    throw new Error(`Production font ${recordId} not found`);
  }
  const inferred = inferFontNameParts(sourcePath);
  const conflict = database
    .prepare(
      `SELECT id FROM production_fonts
       WHERE production_id = ? AND family = ? AND style = ? AND id <> ?`,
    )
    .get(productionId, inferred.family, inferred.style, recordId);
  if (conflict) {
    throw new Error(`Font ${inferred.family} ${inferred.style} already exists`);
  }
  const rootPath = path.resolve(mediaRoot);
  const sourceResolved = path.resolve(sourcePath);
  const fileName = path.basename(sourcePath);
  const relativeFilePath = path.posix.join(
    "fonts",
    safeRelativePathPart(inferred.family, "font"),
    fileName,
  );
  const destination = path.resolve(rootPath, relativeFilePath);
  const relativeDestination = path.relative(rootPath, destination);
  if (relativeDestination.startsWith("..") || path.isAbsolute(relativeDestination)) {
    throw new Error("Font destination is outside production media root");
  }
  await mkdir(path.dirname(destination), { recursive: true });
  if (sourceResolved !== destination) {
    await copyFile(sourceResolved, destination);
  }
  const metadataJson = JSON.stringify({
    importedAt: new Date().toISOString(),
    sourceFileName: fileName,
  });
  database
    .prepare(
      `UPDATE production_fonts
       SET family = ?,
           style = ?,
           file_path = ?,
           source_path = ?,
           metadata_json = ?
       WHERE id = ?`,
    )
    .run(
      inferred.family,
      inferred.style,
      relativeFilePath,
      sourceResolved,
      metadataJson,
      recordId,
    );
  const state = loadAppState(database);
  const record = state.records.production_fonts?.find(
    (candidate) => candidate.id === recordId,
  );
  if (!record) {
    throw new Error(`Imported production font ${recordId} was not found`);
  }
  return {
    tableId: "production_fonts",
    record,
    state,
  };
}

async function ensureMediaFrame({
  filePath,
  fps,
  frame,
}: {
  filePath: string;
  fps: number;
  frame: number;
}) {
  const safeFrame = Math.max(0, Math.floor(frame));
  const safeFps = Number.isFinite(fps) && fps > 0 ? fps : 30;
  const outputPath = mediaFrameFilePath({
    fps: safeFps,
    frame: safeFrame,
    sourcePath: filePath,
  });
  try {
    const sourceStats = statSync(filePath);
    const outputStats = statSync(outputPath);
    if (outputStats.mtimeMs >= sourceStats.mtimeMs) {
      return outputPath;
    }
  } catch {
    // Missing or stale thumbnail; generate below.
  }
  await mkdir(mediaFrameDir, { recursive: true });
  const seconds = (safeFrame / safeFps).toFixed(6);
  await execFileAsync(
    "ffmpeg",
    [
      "-hide_banner",
      "-loglevel",
      "error",
      "-y",
      "-ss",
      seconds,
      "-i",
      filePath,
      "-frames:v",
      "1",
      outputPath,
    ],
    { maxBuffer: 1024 * 1024 * 8 },
  );
  return outputPath;
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
  const maskUrl = extractCssUrl(node.style?.maskImage);
  const webkitMaskUrl = extractCssUrl(node.style?.WebkitMaskImage);
  const absolutize = (url: string) =>
    url.startsWith("/api/") ? new URL(url, origin).toString() : url;
  const nextStyle = node.style
    ? {
        ...node.style,
        ...(backgroundUrl
          ? { backgroundImage: cssUrl(absolutize(backgroundUrl)) }
          : {}),
        ...(maskUrl ? { maskImage: cssUrl(absolutize(maskUrl)) } : {}),
        ...(webkitMaskUrl
          ? { WebkitMaskImage: cssUrl(absolutize(webkitMaskUrl)) }
          : {}),
      }
    : node.style;
  const assetUri =
    node.asset?.type === "image" && typeof node.asset.uri === "string"
      ? absolutize(node.asset.uri)
      : undefined;
  const metadataUri =
    typeof node.metadata?.uri === "string"
      ? absolutize(node.metadata.uri)
      : undefined;
  return {
    ...node,
    ...(nextStyle ? { style: nextStyle } : {}),
    ...(assetUri ? { asset: { type: "image", uri: assetUri } } : {}),
    ...(metadataUri
      ? { metadata: { ...node.metadata, uri: metadataUri } }
      : {}),
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
  const output = renderFrameOutputPath(selection);
  await mkdir(output.directory, { recursive: true });
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
      output.filePath,
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
    url: `/api/render-output/frame.png?path=${encodeURIComponent(output.filePath)}`,
    filePath: output.filePath,
    includeFrame: selection.includeFrame === true,
    outputHeight,
    outputScale,
    outputWidth,
    relativeFilePath: output.relativeFilePath,
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
    if (request.method === "POST" && url.pathname === "/api/app/production-font/import") {
      const body = (await readJson(request)) as {
        productionId?: string;
        recordId?: string;
        sourcePath?: string;
      };
      sendJson(
        response,
        200,
        await importProductionFont({
          productionId: body.productionId ?? "",
          recordId: body.recordId ?? "",
          sourcePath: body.sourcePath ?? "",
        }),
      );
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
    if (request.method === "GET" && url.pathname === "/api/render-output/frame.png") {
      const filePath = assertReadableRenderOutputPath(
        url.searchParams.get("path") ?? "",
      );
      const stats = statSync(filePath);
      const filename = path.basename(filePath).replace(/"/g, "");
      response.writeHead(200, {
        "Content-Type": "image/png",
        "Content-Length": stats.size,
        "Content-Disposition": `inline; filename="${filename}"`,
        "Cache-Control": "no-store",
      });
      createReadStream(filePath).pipe(response);
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
    if (request.method === "GET" && url.pathname === "/api/media-frame") {
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
      const framePath = await ensureMediaFrame({
        filePath,
        fps: Number(url.searchParams.get("fps") ?? 30),
        frame: Number(url.searchParams.get("frame") ?? 0),
      });
      const frameStats = statSync(framePath);
      response.writeHead(200, {
        "Content-Type": "image/png",
        "Content-Length": frameStats.size,
        "Cache-Control": "no-store",
      });
      createReadStream(framePath).pipe(response);
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
