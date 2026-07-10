import readline from "node:readline";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { renderDesignPreviewMarkup } from "./renderDesignPreviewMarkup.js";

interface PreviewRenderRequest {
  id: string;
  payload: DesignPreviewPayload;
}

const input = readline.createInterface({
  input: process.stdin,
  crlfDelay: Number.POSITIVE_INFINITY,
});

input.on("line", (line) => {
  if (!line.trim()) return;

  let id = "";
  try {
    const request = JSON.parse(line) as PreviewRenderRequest;
    id = request.id;
    const html = renderDesignPreviewMarkup(request.payload);
    process.stdout.write(`${JSON.stringify({ id, ok: true, html })}\n`);
  } catch (error) {
    const message = error instanceof Error
      ? `${error.name}: ${error.message}\n${error.stack ?? ""}`
      : String(error);
    process.stdout.write(`${JSON.stringify({ id, ok: false, error: message })}\n`);
  }
});
