export type KeyboardMode = "lowercase" | "shift" | "numeric" | "symbols" | "emoji";

export interface KeyboardKeyDefinition {
  id: string;
  label: string;
  kind: "character" | "special" | "space" | "emoji";
  weight: number;
}

export interface KeyboardModeDefinition {
  id: KeyboardMode;
  label: string;
  rowsText: string;
}

export interface KeyboardLayoutDefinition {
  id: string;
  name: string;
  defaultLanguage: string;
  defaultMode: KeyboardMode;
  modes: Record<KeyboardMode, KeyboardModeDefinition>;
  keys: Record<string, Partial<KeyboardKeyDefinition>>;
  localizedLabels: Record<string, Record<string, string>>;
}

export const STANDARD_IOS_KEYBOARD_LAYOUT: KeyboardLayoutDefinition = {
  id: "standard_ios_spanish_keyboard",
  name: "Standard iOS Spanish Keyboard",
  defaultLanguage: "es",
  defaultMode: "lowercase",
  modes: {
    lowercase: {
      id: "lowercase",
      label: "Minúsculas",
      rowsText: [
        "qwertyuiop",
        "asdfghjklñ",
        "[shift] z x c v b n m [backspace]",
        "[123] [emoji] [space] [return]",
      ].join("\n"),
    },
    shift: {
      id: "shift",
      label: "Mayúsculas",
      rowsText: [
        "QWERTYUIOP",
        "ASDFGHJKLÑ",
        "[shift] Z X C V B N M [backspace]",
        "[123] [emoji] [space] [return]",
      ].join("\n"),
    },
    numeric: {
      id: "numeric",
      label: "Números",
      rowsText: [
        "1234567890",
        "- / : ; ( ) € & @ \"",
        "[symbols] . , ¿ ? ¡ ! [backspace]",
        "[abc] [emoji] [space] [return]",
      ].join("\n"),
    },
    symbols: {
      id: "symbols",
      label: "Símbolos",
      rowsText: [
        "[ ] { } # % ^ * + =",
        "_ \\\\ | ~ < > € £ ¥ ·",
        "[numeric] . , ¿ ? ¡ ! [backspace]",
        "[abc] [emoji] [space] [return]",
      ].join("\n"),
    },
    emoji: {
      id: "emoji",
      label: "Emoji",
      rowsText: [
        "😀 😃 😄 😁 😆 😅 😂 🙂",
        "😍 😘 😎 😭 😡 👍 👎 🙏",
        "❤️ 🔥 ✨ 🎉 🤔 👀 💬 💯",
        "[abc] [space] [return]",
      ].join("\n"),
    },
  },
  keys: {
    shift: { label: "⇧", kind: "special", weight: 1.35 },
    backspace: { label: "⌫", kind: "special", weight: 1.35 },
    "123": { label: "123", kind: "special", weight: 1.5 },
    abc: { label: "ABC", kind: "special", weight: 1.5 },
    numeric: { label: "123", kind: "special", weight: 1.5 },
    symbols: { label: "#+=", kind: "special", weight: 1.5 },
    emoji: { label: "☺", kind: "emoji", weight: 1.2 },
    space: { label: "", kind: "space", weight: 5 },
    return: { label: "intro", kind: "special", weight: 2.2 },
  },
  localizedLabels: {
    space: {
      es: "",
      en: "",
    },
    return: {
      es: "intro",
      en: "return",
    },
  },
};

function rowTokens(rowText: string) {
  const trimmed = rowText.trim();
  if (!trimmed) return [];
  if (/\s/.test(trimmed) || /\[[^\]]+\]/.test(trimmed)) {
    return trimmed.match(/\[[^\]]+\]|\S+/gu)?.map((token) => token.trim()) ?? [];
  }
  return Array.from(trimmed);
}

export function parseKeyboardRows(
  layout: KeyboardLayoutDefinition,
  mode: KeyboardMode,
  language = layout.defaultLanguage,
): KeyboardKeyDefinition[][] {
  const modeDefinition = layout.modes[mode] ?? layout.modes[layout.defaultMode];
  return modeDefinition.rowsText
    .split("\n")
    .map((row) =>
      rowTokens(row).map((rawToken) => {
        const id = rawToken.startsWith("[") && rawToken.endsWith("]")
          ? rawToken.slice(1, -1)
          : rawToken;
        const override = layout.keys[id] ?? {};
        const localizedLabel = layout.localizedLabels[id]?.[language];
        return {
          id,
          label: localizedLabel ?? override.label ?? id,
          kind: override.kind ?? "character",
          weight: override.weight ?? 1,
        };
      }),
    )
    .filter((row) => row.length > 0);
}
