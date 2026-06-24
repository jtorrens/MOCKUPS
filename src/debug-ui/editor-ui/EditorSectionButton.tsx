import type { ReactNode } from "react";

function sectionMeta(label: string) {
  const normalized = label.toLowerCase();
  const meta: Record<string, { icon: string; subtitle: string }> = {
    general: {
      icon: "⌘",
      subtitle: "Identity, timing and structural settings",
    },
    generales: {
      icon: "⌘",
      subtitle: "Identity, timing and structural settings",
    },
    tokens: {
      icon: "◫",
      subtitle: "Semantic tokens inherited by lower levels",
    },
    colors: {
      icon: "◐",
      subtitle: "Mode-aware colors, surfaces and semantic roles",
    },
    notes: {
      icon: "✎",
      subtitle: "Documentation and internal annotations",
    },
    content: {
      icon: "☰",
      subtitle: "Shot-specific screen data and copy",
    },
    "module content": {
      icon: "☰",
      subtitle: "Shot-specific data for this module instance",
    },
    behavior: {
      icon: "⚙",
      subtitle: "Runtime behavior for this screen",
    },
    "device state": {
      icon: "▥",
      subtitle: "Screen-specific time, battery, network and lock state",
    },
    transform: {
      icon: "⌖",
      subtitle: "Screen placement inside the shot canvas",
    },
    transition: {
      icon: "⇄",
      subtitle: "How this screen overlaps into the next one",
    },
    overrides: {
      icon: "↺",
      subtitle: "Local style overrides against inherited tokens",
    },
    design: {
      icon: "◇",
      subtitle: "Spacing, typography, layout and component tokens",
    },
    settings: {
      icon: "▣",
      subtitle: "Module binding, metadata and technical settings",
    },
    wallpaper: {
      icon: "▧",
      subtitle: "Background and wallpaper behavior",
    },
    fonts: {
      icon: "T",
      subtitle: "Family, sizes and text weights",
    },
    typography: {
      icon: "T",
      subtitle: "Text styles and hierarchy",
    },
    spacing: {
      icon: "↔",
      subtitle: "Gaps, gutters and layout spacing",
    },
    radii: {
      icon: "◜",
      subtitle: "Corner radius tokens",
    },
    shadows: {
      icon: "◒",
      subtitle: "Shadows and elevation",
    },
    layout: {
      icon: "▦",
      subtitle: "Layout metrics",
    },
    header: {
      icon: "▤",
      subtitle: "Top bar and navigation",
    },
    messages: {
      icon: "☰",
      subtitle: "Message list spacing and behavior",
    },
    participants: {
      icon: "♙",
      subtitle: "Linked users, display names and participant roles",
    },
    chatbubbles: {
      icon: "☵",
      subtitle: "Incoming and outgoing bubble tokens",
    },
    avatars: {
      icon: "◉",
      subtitle: "Avatar sizing and gaps",
    },
    cursor: {
      icon: "⌁",
      subtitle: "Write-on cursor tokens",
    },
    statusbar: {
      icon: "▥",
      subtitle: "Device status bar appearance",
    },
    notifications: {
      icon: "◌",
      subtitle: "Notification card styling",
    },
  };
  return meta[normalized] ?? { icon: "•", subtitle: "" };
}

interface EditorSectionButtonProps {
  active: boolean;
  warning?: boolean;
  children: ReactNode;
  onClick: () => void;
}

export function EditorSectionButton({
  active,
  warning,
  children,
  onClick,
}: EditorSectionButtonProps) {
  const label = typeof children === "string" ? children : "";
  const meta = sectionMeta(label);
  return (
    <button
      type="button"
      className={`${active ? "active" : ""} ${warning ? "has-warning" : ""}`}
      onClick={onClick}
    >
      <span className="tab-icon ui-glyph" aria-hidden="true">
        {meta.icon}
      </span>
      <span className="tab-copy">
        <span>{children}</span>
        {meta.subtitle ? <small>{meta.subtitle}</small> : null}
      </span>
    </button>
  );
}
