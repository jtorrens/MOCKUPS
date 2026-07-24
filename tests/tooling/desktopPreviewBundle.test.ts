import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";

import { committedComponentFixture } from "../animation/committedComponentFixture.js";

const repositoryRoot = process.cwd();
const packageJson = JSON.parse(
  readFileSync(path.join(repositoryRoot, "package.json"), "utf8"),
) as { scripts: Record<string, string> };

test("desktop development commands rebuild Preview before invoking .NET", () => {
  assert.match(
    packageJson.scripts.desktop ?? "",
    /^npm run desktop-preview:build && dotnet run /u,
  );
  assert.match(
    packageJson.scripts["desktop:build"] ?? "",
    /^npm run desktop-preview:build && dotnet build /u,
  );
});

test("the generated Preview server routes an integrated scaffold Component", () => {
  const build = spawnSync(
    process.execPath,
    [path.join(repositoryRoot, "scripts", "buildDesktopPreview.mjs")],
    { cwd: repositoryRoot, encoding: "utf8" },
  );
  assert.equal(build.status, 0, build.stderr || build.stdout);

  const serverPath = path.join(
    repositoryRoot,
    "dist",
    "desktop-preview",
    "renderDesignPreviewHtmlServer.cjs",
  );
  const request = {
    id: "integrated-scaffold-route",
    payload: committedComponentFixture("incomingCallNotification"),
  };
  const render = spawnSync(
    process.execPath,
    [serverPath],
    {
      cwd: repositoryRoot,
      encoding: "utf8",
      input: `${JSON.stringify(request)}\n`,
      maxBuffer: 8 * 1024 * 1024,
    },
  );
  assert.equal(render.status, 0, render.stderr);
  const response = JSON.parse(render.stdout.trim()) as {
    id: string;
    ok: boolean;
    error?: string;
  };
  assert.equal(response.id, request.id);
  assert.equal(response.ok, true, response.error);
});
