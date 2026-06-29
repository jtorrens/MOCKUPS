import { useEffect, useState } from "react";

export interface IconThemePreviewBridge {
  readonly mediaDataUrl?: (filePath: string, rootPath: string) => Promise<string>;
}

export interface IconThemeLikeRecord {
  readonly id?: unknown;
  readonly name?: unknown;
  readonly asset_root?: unknown;
  readonly mapping_json?: unknown;
}

export interface IconThemeTokenEntry {
  readonly token: string;
  readonly file: string;
  readonly category?: string;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function iconThemeName(record: IconThemeLikeRecord | undefined) {
  return typeof record?.name === "string" && record.name.trim()
    ? record.name
    : typeof record?.id === "string"
      ? record.id
      : "Icon theme";
}

export function iconThemeAssetRoot(record: IconThemeLikeRecord | undefined) {
  return typeof record?.asset_root === "string" ? record.asset_root : "";
}

export function iconThemeTokenEntries(
  record: IconThemeLikeRecord | undefined,
): IconThemeTokenEntry[] {
  const mapping = isRecord(record?.mapping_json) ? record.mapping_json : {};
  const tokens = isRecord(mapping.tokens) ? mapping.tokens : {};
  return Object.entries(tokens)
    .map(([token, value]) => {
      const row = isRecord(value) ? value : {};
      return {
        token,
        file: typeof row.file === "string" ? row.file : "",
        category: typeof row.category === "string" ? row.category : undefined,
      };
    })
    .filter((entry) => entry.file)
    .sort((left, right) =>
      left.token.localeCompare(right.token, undefined, {
        numeric: true,
        sensitivity: "base",
      }),
    );
}

function iconPath(assetRoot: string, file: string) {
  const trimmedFile = file.trim();
  if (!trimmedFile) return "";
  if (/^(data:|file:|https?:|\/)/i.test(trimmedFile)) return trimmedFile;
  return `${assetRoot.replace(/\/+$/g, "")}/${trimmedFile.replace(/^\/+/g, "")}`;
}

export function IconGlyphPreview({
  assetRoot,
  file,
  mediaRoot,
  nativeBridge,
  label = "—",
}: {
  assetRoot: string;
  file: string;
  mediaRoot?: string;
  nativeBridge?: IconThemePreviewBridge;
  label?: string;
}) {
  const [url, setUrl] = useState("");
  const path = iconPath(assetRoot, file);

  useEffect(() => {
    let cancelled = false;
    setUrl("");
    if (!path) return () => undefined;
    const loader = nativeBridge?.mediaDataUrl;
    if (!loader || !mediaRoot) {
      setUrl(path.startsWith("/") ? `file://${encodeURI(path)}` : path);
      return () => undefined;
    }
    void loader(path, mediaRoot)
      .then((nextUrl) => {
        if (!cancelled) setUrl(nextUrl || "");
      })
      .catch(() => {
        if (!cancelled) setUrl("");
      });
    return () => {
      cancelled = true;
    };
  }, [mediaRoot, nativeBridge, path]);

  return (
    <span className="icon-theme-preview" aria-hidden="true">
      {url ? (
        <span
          className="icon-theme-preview-mask"
          style={{
            WebkitMaskImage: `url("${url}")`,
            maskImage: `url("${url}")`,
          }}
        />
      ) : (
        label
      )}
    </span>
  );
}
