import { access } from "node:fs/promises";
import { spawn } from "node:child_process";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

export type MacDesktopLaunchMode = "development" | "packaged";

export type MacDesktopLaunchSpec = {
  command: "caffeinate";
  args: string[];
  artifactPath?: string;
};

const displayWakeSeconds = 10;
const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

export function macDesktopLaunchSpec(
  mode: MacDesktopLaunchMode,
  root = repositoryRoot,
): MacDesktopLaunchSpec {
  if (mode === "development") {
    return {
      command: "caffeinate",
      args: [
        "-du",
        "-t",
        String(displayWakeSeconds),
        "dotnet",
        "run",
        "--project",
        resolve(root, "spikes", "desktop-editor-shell", "Mockups.DesktopEditorShell.csproj"),
      ],
    };
  }

  const appPath = resolve(root, "out", "desktop", "MOCKUPS Editor.app");
  return {
    command: "caffeinate",
    args: [
      "-du",
      "-t",
      String(displayWakeSeconds),
      "open",
      "-W",
      appPath,
    ],
    artifactPath: appPath,
  };
}

export async function launchDesktopMac(mode: MacDesktopLaunchMode) {
  if (process.platform !== "darwin") {
    throw new Error("The display-aware desktop launcher is available only on macOS.");
  }

  const spec = macDesktopLaunchSpec(mode);
  if (spec.artifactPath) {
    await access(spec.artifactPath);
  }

  return await new Promise<number>((accept, reject) => {
    const child = spawn(spec.command, spec.args, {
      cwd: repositoryRoot,
      stdio: "inherit",
    });
    child.once("error", reject);
    child.once("exit", (code, signal) => {
      if (signal) {
        reject(new Error(`macOS desktop launcher ended from signal ${signal}.`));
        return;
      }
      accept(code ?? 1);
    });
  });
}

function requestedMode(args: string[]): MacDesktopLaunchMode {
  if (args.length !== 1) {
    throw new Error("Usage: launchDesktopMac.ts --development|--packaged");
  }
  if (args[0] === "--development") return "development";
  if (args[0] === "--packaged") return "packaged";
  throw new Error(`Unknown macOS desktop launch mode '${args[0]}'.`);
}

const executedPath = process.argv[1]
  ? pathToFileURL(resolve(process.argv[1])).href
  : "";
if (executedPath === import.meta.url) {
  launchDesktopMac(requestedMode(process.argv.slice(2)))
    .then((code) => {
      process.exitCode = code;
    })
    .catch((error: unknown) => {
      console.error(error instanceof Error ? error.message : String(error));
      process.exitCode = 1;
    });
}
