import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { HexColorPicker, RgbaStringColorPicker } from "react-colorful";

interface ColorValueEditorProps {
  value: string;
  alpha?: boolean;
  label?: string;
  onChange: (nextValue: string) => void;
}

function isHexColor(value: string): boolean {
  return /^#[0-9a-fA-F]{6}$/.test(value);
}

function hexToRgba(hex: string, alpha = 1) {
  const normalized = hex.replace("#", "");
  const red = Number.parseInt(normalized.slice(0, 2), 16);
  const green = Number.parseInt(normalized.slice(2, 4), 16);
  const blue = Number.parseInt(normalized.slice(4, 6), 16);
  return `rgba(${red}, ${green}, ${blue}, ${clampAlpha(alpha)})`;
}

function rgbaToHexAndAlpha(value: string): { hex: string; alpha: number } | null {
  const match = value
    .trim()
    .match(
      /^rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(0|1|0?\.\d+)\s*\)$/i,
    );
  if (!match) return null;
  const [, red, green, blue, alpha] = match;
  const channels = [red, green, blue].map((channel) =>
    Math.max(0, Math.min(255, Number(channel))),
  );
  return {
    hex: `#${channels
      .map((channel) => channel.toString(16).padStart(2, "0"))
      .join("")}`,
    alpha: clampAlpha(Number(alpha)),
  };
}

function clampAlpha(value: number) {
  if (!Number.isFinite(value)) return 1;
  return Math.max(0, Math.min(1, value));
}

function normalizeColor(value: string, alpha: boolean) {
  if (alpha) {
    if (value === "transparent") return "rgba(255, 255, 255, 0)";
    if (isHexColor(value)) return hexToRgba(value, 1);
    return rgbaToHexAndAlpha(value) ? value : "rgba(255, 255, 255, 0)";
  }
  if (isHexColor(value)) return value.toLowerCase();
  return rgbaToHexAndAlpha(value)?.hex ?? "#000000";
}

function displaySwatch(value: string, alpha: boolean) {
  return alpha ? normalizeColor(value, true) : normalizeColor(value, false);
}

function alphaValue(value: string) {
  return rgbaToHexAndAlpha(normalizeColor(value, true))?.alpha ?? 1;
}

function withAlpha(value: string, alpha: number) {
  const parsed = rgbaToHexAndAlpha(normalizeColor(value, true));
  return hexToRgba(parsed?.hex ?? "#ffffff", alpha);
}

export function ColorValueEditor({
  value,
  alpha = false,
  label = "Color",
  onChange,
}: ColorValueEditorProps) {
  const [open, setOpen] = useState(false);
  const [popoverRect, setPopoverRect] = useState({ top: 0, left: 0 });
  const rootRef = useRef<HTMLDivElement>(null);
  const popoverRef = useRef<HTMLDivElement>(null);
  const normalized = normalizeColor(value, alpha);

  useEffect(() => {
    function onPointerDown(event: PointerEvent) {
      const target = event.target as Node;
      if (
        !rootRef.current?.contains(target) &&
        !popoverRef.current?.contains(target)
      ) {
        setOpen(false);
      }
    }
    if (open) {
      window.addEventListener("pointerdown", onPointerDown);
    }
    return () => window.removeEventListener("pointerdown", onPointerDown);
  }, [open]);

  function toggleOpen() {
    const rect = rootRef.current?.getBoundingClientRect();
    if (rect) {
      setPopoverRect({
        top: rect.bottom + 8,
        left: Math.min(rect.left, window.innerWidth - 220),
      });
    }
    setOpen((current) => !current);
  }

  return (
    <div className="color-value-editor" ref={rootRef}>
      <button
        type="button"
        className="color-swatch-button"
        aria-label={`${label} picker`}
        onClick={toggleOpen}
      >
        <span style={{ background: displaySwatch(value, alpha) }} />
      </button>
      {alpha ? (
        <input
          aria-label={`${label} alpha`}
          className="color-alpha-input"
          type="number"
          min={0}
          max={1}
          step={0.01}
          value={String(alphaValue(value))}
          onChange={(event) => onChange(withAlpha(value, Number(event.target.value)))}
        />
      ) : null}
      {open
        ? createPortal(
            <div
              className="color-picker-popover"
              ref={popoverRef}
              style={{ top: popoverRect.top, left: popoverRect.left }}
            >
              {alpha ? (
                <RgbaStringColorPicker color={normalized} onChange={onChange} />
              ) : (
                <HexColorPicker color={normalized} onChange={onChange} />
              )}
            </div>,
            document.body,
          )
        : null}
    </div>
  );
}
