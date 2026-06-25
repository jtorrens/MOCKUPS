import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { RgbaStringColorPicker } from "react-colorful";

interface ColorValueEditorProps {
  value: string;
  alpha?: boolean;
  label?: string;
  onChange: (nextValue: string) => void;
}

function isHexColor(value: string): boolean {
  return /^#[0-9a-fA-F]{6}$/.test(value.trim());
}

function rgbToHex(value: string): string | null {
  const match = value
    .trim()
    .match(/^rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)$/i);
  if (!match) return null;
  const [, red, green, blue] = match;
  const channels = [red, green, blue].map((channel) =>
    Math.max(0, Math.min(255, Number(channel))),
  );
  return `#${channels
    .map((channel) => channel.toString(16).padStart(2, "0"))
    .join("")}`;
}

function hexToRgbString(hex: string): string {
  return `rgb(${Number.parseInt(hex.slice(1, 3), 16)}, ${Number.parseInt(
    hex.slice(3, 5),
    16,
  )}, ${Number.parseInt(hex.slice(5, 7), 16)})`;
}

function normalizePlainColorInput(value: string): string | null {
  const trimmed = value.trim();
  if (isHexColor(trimmed)) return trimmed.toLowerCase();
  const rgbHex = rgbToHex(trimmed);
  return rgbHex ? hexToRgbString(rgbHex) : null;
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
  const rgbHex = rgbToHex(value);
  if (rgbHex) return rgbHex;
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
  const [draftValue, setDraftValue] = useState(value);
  const [popoverRect, setPopoverRect] = useState({ top: 0, left: 0 });
  const rootRef = useRef<HTMLDivElement>(null);
  const popoverRef = useRef<HTMLDivElement>(null);
  const normalized = normalizeColor(value, alpha);

  useEffect(() => {
    setDraftValue(value);
  }, [value]);

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

  function changePickerColor(nextValue: string) {
    setDraftValue(nextValue);
    onChange(nextValue);
  }

  function changePlainColorText(nextValue: string) {
    setDraftValue(nextValue);
    const normalizedInput = normalizePlainColorInput(nextValue);
    if (normalizedInput) {
      onChange(normalizedInput);
    }
  }

  function resetInvalidPlainColorText() {
    if (!normalizePlainColorInput(draftValue)) {
      setDraftValue(value);
    }
  }

  return (
    <div className="color-value-editor" ref={rootRef}>
      {alpha ? (
        <>
          <button
            type="button"
            className="color-swatch-button"
            aria-label={`${label} picker`}
            onClick={toggleOpen}
          >
            <span style={{ background: displaySwatch(value, alpha) }} />
          </button>
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
        </>
      ) : (
        <>
          <input
            aria-label={`${label} system picker`}
            className="color-native-input"
            type="color"
            value={normalized}
            onChange={(event) => changePickerColor(event.target.value)}
          />
          <input
            aria-label={`${label} color value`}
            className="color-text-input"
            type="text"
            spellCheck={false}
            placeholder="#000000 or rgb(0, 0, 0)"
            value={draftValue}
            onBlur={resetInvalidPlainColorText}
            onChange={(event) => changePlainColorText(event.target.value)}
          />
        </>
      )}
      {open
        ? createPortal(
            <div
              className="color-picker-popover"
              ref={popoverRef}
              style={{ top: popoverRect.top, left: popoverRect.left }}
            >
              <RgbaStringColorPicker color={normalized} onChange={changePickerColor} />
            </div>,
            document.body,
          )
        : null}
    </div>
  );
}
