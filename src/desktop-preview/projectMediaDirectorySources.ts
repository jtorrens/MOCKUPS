import path from "node:path";

export function projectMediaDirectorySources(
  files: string[],
  directoryValue: string,
  mediaRoot: string,
) {
  const directory = normalizeDirectory(directoryValue, mediaRoot);
  const prefix = directory ? `${directory}/` : "";
  return files
    .map((file) => file.replace(/\\/g, "/").replace(/^\/+/, ""))
    .filter((file) => file.startsWith(prefix) && !file.slice(prefix.length).includes("/"))
    .sort((left, right) => left.localeCompare(
      right,
      undefined,
      { numeric: true, sensitivity: "base" },
    ));
}

export function projectMediaType(sourceUri: string): "image" | "video" {
  return /\.(mp4|mov|m4v|webm)$/i.test(sourceUri) ? "video" : "image";
}

function normalizeDirectory(value: string, mediaRoot: string) {
  const trimmed = value.trim();
  const relative = path.isAbsolute(trimmed) && mediaRoot
    ? path.relative(mediaRoot, trimmed)
    : trimmed;
  return relative.replace(/\\/g, "/").replace(/^\/+|\/+$/g, "");
}
