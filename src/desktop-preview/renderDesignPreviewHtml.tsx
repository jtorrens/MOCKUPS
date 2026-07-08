import { readFile } from "node:fs/promises";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { renderDesignPreviewMarkup } from "./renderDesignPreviewMarkup.js";

async function main() {
  const inputPath = process.argv[2];
  if (!inputPath) {
    throw new Error("Missing design preview payload path.");
  }

  const payload = JSON.parse(
    await readFile(inputPath, "utf8"),
  ) as DesignPreviewPayload;
  process.stdout.write(renderDesignPreviewMarkup(payload));
}

main().catch((error: unknown) => {
  console.error(error);
  process.exitCode = 1;
});
