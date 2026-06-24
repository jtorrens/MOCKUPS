import { useEffect, useMemo, useState } from "react";

export const FALLBACK_FONT_FAMILIES = [
  "SF Pro Text",
  "SF Pro Display",
  "Helvetica Neue",
  "Arial",
  "Avenir Next",
  "Inter",
  "Roboto",
  "System UI",
  "Georgia",
  "Times New Roman",
  "Menlo",
  "SF Mono",
];

export const FALLBACK_FONT_STYLES = [
  "Regular",
  "Medium",
  "Semibold",
  "Bold",
];

type LocalFont = {
  family: string;
  style?: string;
  fullName?: string;
  postscriptName?: string;
};

type QueryLocalFonts = () => Promise<LocalFont[]>;

type FontCatalogState = {
  status: "idle" | "loading" | "ready" | "failed";
  fonts: LocalFont[];
};

declare global {
  interface Window {
    queryLocalFonts?: QueryLocalFonts;
    mockupsNative?: {
      listFonts?: () => Promise<LocalFont[]>;
    };
  }
}

function styleFromFont(font: LocalFont): string | undefined {
  if (font.style?.trim()) return font.style.trim();
  const fullName = font.fullName ?? font.postscriptName ?? "";
  const family = font.family ?? "";
  const stripped = fullName
    .replace(family, "")
    .replace(/^[\s-]+/, "")
    .trim();
  return stripped || undefined;
}

let catalogState: FontCatalogState = {
  status: "idle",
  fonts: [],
};
let loadingPromise: Promise<LocalFont[]> | null = null;
const subscribers = new Set<() => void>();

function notifySubscribers() {
  for (const subscriber of subscribers) {
    subscriber();
  }
}

async function requestLocalFonts() {
  const [browserFonts, nativeFonts] = await Promise.all([
    window.queryLocalFonts?.().catch(() => []) ?? Promise.resolve([]),
    window.mockupsNative?.listFonts?.().catch(() => []) ?? Promise.resolve([]),
  ]);
  return [...browserFonts, ...nativeFonts];
}

function loadSharedFontCatalog() {
  if (catalogState.status === "ready") {
    return Promise.resolve(catalogState.fonts);
  }
  if (loadingPromise) return loadingPromise;

  catalogState = { ...catalogState, status: "loading" };
  notifySubscribers();

  loadingPromise = requestLocalFonts()
    .then((fonts) => {
      catalogState = {
        status: "ready",
        fonts,
      };
      notifySubscribers();
      return fonts;
    })
    .catch(() => {
      catalogState = {
        status: "failed",
        fonts: [],
      };
      notifySubscribers();
      return [];
    })
    .finally(() => {
      loadingPromise = null;
    });

  return loadingPromise;
}

export function resetSystemFontCatalogCacheForTests() {
  catalogState = { status: "idle", fonts: [] };
  loadingPromise = null;
  notifySubscribers();
}

export function useSystemFontCatalog() {
  const [state, setState] = useState(catalogState);

  useEffect(() => {
    const subscriber = () => setState(catalogState);
    subscribers.add(subscriber);
    void loadSharedFontCatalog();
    return () => {
      subscribers.delete(subscriber);
    };
  }, []);

  return useMemo(() => {
    const localFonts = state.fonts;
    const families = Array.from(
      new Set([
        ...localFonts.map((font) => font.family).filter(Boolean),
        ...FALLBACK_FONT_FAMILIES,
      ]),
    ).sort((a, b) => a.localeCompare(b));

    const stylesByFamily = new Map<string, string[]>();
    for (const font of localFonts) {
      const style = styleFromFont(font);
      if (!font.family || !style) continue;
      const styles = stylesByFamily.get(font.family) ?? [];
      styles.push(style);
      stylesByFamily.set(font.family, Array.from(new Set(styles)).sort());
    }

    return { families, stylesByFamily, status: state.status };
  }, [state]);
}

export function fontStylesForFamily(
  stylesByFamily: Map<string, string[]>,
  family: string | undefined,
) {
  if (!family) return FALLBACK_FONT_STYLES;
  const styles = stylesByFamily.get(family);
  return styles?.length ? styles : FALLBACK_FONT_STYLES;
}
