import readline from "node:readline";
import { createHash } from "node:crypto";
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

const knownAssets = new Map<string, string>();
const dataUriPattern = /(?<uri>data:(?:image|video)\/.*?)(?=&quot;|&#39;|[\s"'<>]|$)/gis;

function compactPreviewAssets(html: string) {
  const assets: Array<{ key: string; uri: string }> = [];
  const compactHtml = html.replace(dataUriPattern, (match, _uri, _offset, _input, groups) => {
    const uri = String(groups?.uri ?? match);
    let key = knownAssets.get(uri);
    if (!key) {
      key = createHash("sha256").update(uri).digest("hex");
      knownAssets.set(uri, key);
      assets.push({ key, uri });
    }
    return `mockups-asset:${key}`;
  });
  return { html: compactHtml, assets };
}

input.on("line", (line) => {
  if (!line.trim()) return;

  let id = "";
  try {
    const request = JSON.parse(line) as PreviewRenderRequest;
    id = request.id;
    const rendered = renderDesignPreviewMarkup(request.payload);
    const compact = compactPreviewAssets(rendered);
    process.stdout.write(`${JSON.stringify({ id, ok: true, html: compact.html, assets: compact.assets })}\n`);
  } catch (error) {
    const message = error instanceof Error
      ? `${error.name}: ${error.message}\n${error.stack ?? ""}`
      : String(error);
    process.stdout.write(`${JSON.stringify({ id, ok: false, error: message })}\n`);
  }
});
