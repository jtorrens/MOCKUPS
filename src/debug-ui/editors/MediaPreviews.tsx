import { useEffect, useState } from "react";

interface MediaPreviewNativeBridge {
  mediaDataUrl?: (filePath: string, rootPath: string) => Promise<string>;
}

function mockupsNative() {
  return (window as Window & { mockupsNative?: MediaPreviewNativeBridge }).mockupsNative;
}

function normalizeFilesystemPath(value: string) {
  return value.replace(/\\/g, "/").replace(/\/+$/g, "");
}

function mediaPreviewUrl(filePath: string, rootPath: string) {
  const trimmedPath = filePath.trim();
  if (!trimmedPath) return "";
  if (/^(data:|file:|https?:)/i.test(trimmedPath)) return trimmedPath;
  const isAbsolutePath = trimmedPath.startsWith("/");
  const resolvedPath =
    rootPath && !isAbsolutePath
      ? `${normalizeFilesystemPath(rootPath)}/${trimmedPath}`
      : trimmedPath;
  if (resolvedPath.startsWith("/")) {
    return `file://${encodeURI(resolvedPath)}`;
  }
  return resolvedPath;
}

export function cssUrl(value: string) {
  return `url("${value.replace(/"/g, '\\"')}")`;
}

export function useMediaPreviewUrl({
  enabled = true,
  filePath,
  mediaRoot,
}: {
  enabled?: boolean;
  filePath: string;
  mediaRoot: string;
}) {
  const [previewUrl, setPreviewUrl] = useState("");

  useEffect(() => {
    let cancelled = false;
    setPreviewUrl("");
    if (!enabled || !filePath.trim()) return () => undefined;
    const fallbackUrl = mediaPreviewUrl(filePath, mediaRoot);
    const loader = mockupsNative()?.mediaDataUrl;
    if (!loader) {
      setPreviewUrl(fallbackUrl);
      return () => undefined;
    }
    void loader(filePath, mediaRoot)
      .then((nextUrl) => {
        if (!cancelled) setPreviewUrl(nextUrl || fallbackUrl);
      })
      .catch(() => {
        if (!cancelled) setPreviewUrl(fallbackUrl);
      });
    return () => {
      cancelled = true;
    };
  }, [enabled, filePath, mediaRoot]);

  return previewUrl;
}

export function ActorAvatarPreview({
  filePath,
  mediaRoot,
  scale,
  offsetX,
  offsetY,
  useInitials,
  backgroundColor,
  textColor,
  initials,
}: {
  filePath: string;
  mediaRoot: string;
  scale: number;
  offsetX: number;
  offsetY: number;
  useInitials: boolean;
  backgroundColor: string;
  textColor: string;
  initials: string;
}) {
  const previewUrl = useMediaPreviewUrl({
    enabled: !useInitials,
    filePath,
    mediaRoot,
  });

  const shouldShowInitials = useInitials || !previewUrl;
  return (
    <div
      className="actor-avatar-preview"
      style={
        shouldShowInitials
          ? {
              backgroundColor,
              color: textColor,
            }
          : {
              backgroundImage: cssUrl(previewUrl),
              backgroundSize: `${Math.max(0.01, scale) * 100}%`,
              backgroundPosition: `calc(50% + ${offsetX}px) calc(50% + ${offsetY}px)`,
            }
      }
    >
      {shouldShowInitials ? initials : null}
    </div>
  );
}

export function MediaCoverPreview({
  filePath,
  mediaRoot,
  fallbackLabel,
}: {
  filePath: string;
  mediaRoot: string;
  fallbackLabel: string;
}) {
  const previewUrl = useMediaPreviewUrl({ filePath, mediaRoot });

  return (
    <div
      className="media-cover-preview"
      style={
        previewUrl
          ? {
              backgroundImage: cssUrl(previewUrl),
            }
          : undefined
      }
    >
      {!previewUrl ? fallbackLabel : null}
    </div>
  );
}
