import { createServer } from "node:http";
import { createDatabase } from "../persistence/sqlite/createDatabase.js";
import { developmentDatabasePath } from "../persistence/sqlite/paths.js";
import {
  listDebugOptions,
  loadAppState,
  loadDebugPayload,
  saveDebugPayload,
  createAppRecord,
  updateAppRecord,
  type AppCreateRequest,
  type AppUpdateRequest,
  type DebugSaveRequest,
  type DebugSelection,
} from "./debugService.js";

const PORT = 4174;
const database = createDatabase(developmentDatabasePath);

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
