import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import path from "node:path";
import test from "node:test";

import { committedComponentFixture } from "../animation/committedComponentFixture.js";

const repositoryRoot = process.cwd();
const packageJson = JSON.parse(
  readFileSync(path.join(repositoryRoot, "package.json"), "utf8"),
) as { scripts: Record<string, string> };
const desktopProject = readFileSync(
  path.join(
    repositoryRoot,
    "src",
    "Mockups.Desktop",
    "Mockups.DesktopEditorShell.csproj",
  ),
  "utf8",
);

test("the Desktop project owns Preview generation for every .NET entrypoint", () => {
  assert.match(
    packageJson.scripts.desktop ?? "",
    /^dotnet run /u,
  );
  assert.equal(
    packageJson.scripts["desktop:build"] ?? "",
    "npm run desktop:compile",
  );
  assert.match(
    packageJson.scripts["desktop:compile"] ?? "",
    /^dotnet build /u,
  );
  assert.match(
    packageJson.scripts["test:focus:desktop"] ?? "",
    /^dotnet run /u,
  );
  assert.match(desktopProject, /Name="PrepareDesktopPreview"/u);
  assert.match(desktopProject, /Name="BuildDesktopPreviewArtifacts"/u);
  assert.match(desktopProject, /BeforeTargets="PrepareForBuild"/u);
  assert.match(desktopProject, /--artifacts-only/u);
  assert.match(desktopProject, /--manifest-only/u);
  assert.match(desktopProject, /Inputs="@\(DesktopPreviewBuildInput\)"/u);
  assert.match(desktopProject, /Outputs="[^"]+renderDesignPreviewHtml\.cjs/u);
  assert.match(desktopProject, /dist\/desktop-preview/u);
  assert.match(desktopProject, /manifest\.json/u);
});

test("the generated Preview bundle is manifested and routes an integrated scaffold Component", () => {
  const build = spawnSync(
    process.execPath,
    [path.join(repositoryRoot, "scripts", "buildDesktopPreview.mjs")],
    { cwd: repositoryRoot, encoding: "utf8" },
  );
  assert.equal(build.status, 0, build.stderr || build.stdout);

  const bundleDirectory = path.join(
    repositoryRoot,
    "dist",
    "desktop-preview",
  );
  const manifest = JSON.parse(
    readFileSync(path.join(bundleDirectory, "manifest.json"), "utf8"),
  ) as {
    schemaVersion: number;
    commit: string;
    builtAt: string;
    bundleHash: string;
    artifacts: Record<string, string>;
  };
  assert.equal(manifest.schemaVersion, 1);
  assert.match(manifest.commit, /^[0-9a-f]{40}(?:[0-9a-f]{24})?$/u);
  assert.equal(Number.isNaN(Date.parse(manifest.builtAt)), false);
  for (const requiredArtifact of [
    "renderDesignPreviewHtml.cjs",
    "renderDesignPreviewHtmlServer.cjs",
    "renderPreviewRasterServer.cjs",
  ]) {
    assert.equal(typeof manifest.artifacts[requiredArtifact], "string");
  }
  const bundleSource = Object.entries(manifest.artifacts)
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([name, expectedHash]) => {
      const contents = readFileSync(path.join(bundleDirectory, name));
      const actualHash = createHash("sha256").update(contents).digest("hex");
      assert.equal(actualHash, expectedHash);
      return `${name}:${actualHash}\n`;
    })
    .join("");
  assert.equal(
    createHash("sha256").update(bundleSource).digest("hex"),
    manifest.bundleHash,
  );

  const serverPath = path.join(
    bundleDirectory,
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
