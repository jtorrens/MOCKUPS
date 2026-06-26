import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { RgbaStringColorPicker } from "react-colorful";
import type { PaletteColorCatalog, PaletteColorOption } from "./paletteColors.js";

interface ColorValueEditorProps {
  value: string;
  alpha?: boolean;
  label?: string;
  paletteCatalog?: PaletteColorCatalog;
  onChange: (nextValue: string) => void;
}

function isHexColor(value: string): boolean {
  return /^#[0-9a-fA-F]{6}$/.test(value.trim());
}

function normalizeHex(value: string) {
  return value.trim().toUpperCase();
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

function hexToCompactRgbString(hex: string): string {
  return `rgb(${Number.parseInt(hex.slice(1, 3), 16)},${Number.parseInt(
    hex.slice(3, 5),
    16,
  )},${Number.parseInt(hex.slice(5, 7), 16)})`;
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
      .join("")}`.toUpperCase(),
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

function colorTokenOption(
  value: string,
  paletteCatalog: PaletteColorCatalog | undefined,
): PaletteColorOption | undefined {
  if (!paletteCatalog) return undefined;
  const trimmed = value.trim();
  const byToken = paletteCatalog.byToken.get(trimmed);
  if (byToken) return byToken;
  const rgba = rgbaToHexAndAlpha(trimmed);
  if (rgba) return paletteCatalog.byHex.get(normalizeHex(rgba.hex));
  if (isHexColor(trimmed)) return paletteCatalog.byHex.get(normalizeHex(trimmed));
  const rgbHex = rgbToHex(trimmed);
  return rgbHex ? paletteCatalog.byHex.get(normalizeHex(rgbHex)) : undefined;
}

function displayColor(
  value: string,
  alpha: boolean,
  paletteCatalog?: PaletteColorCatalog,
) {
  const paletteColor = colorTokenOption(value, paletteCatalog);
  if (paletteColor) {
    return alpha
      ? hexToRgba(paletteColor.valueHex, alphaValue(value, paletteCatalog))
      : paletteColor.valueHex;
  }
  return displaySwatch(value, alpha);
}

function displaySwatch(value: string, alpha: boolean) {
  return alpha ? normalizeColor(value, true) : normalizeColor(value, false);
}

function alphaValue(value: string, paletteCatalog?: PaletteColorCatalog) {
  if (colorTokenOption(value, paletteCatalog) && !rgbaToHexAndAlpha(value)) {
    return 1;
  }
  return rgbaToHexAndAlpha(normalizeColor(value, true))?.alpha ?? 1;
}

function withAlpha(value: string, alpha: number, paletteCatalog?: PaletteColorCatalog) {
  const paletteColor = colorTokenOption(value, paletteCatalog);
  if (paletteColor) return hexToRgba(paletteColor.valueHex, alpha);
  const parsed = rgbaToHexAndAlpha(normalizeColor(value, true));
  return hexToRgba(parsed?.hex ?? "#ffffff", alpha);
}

function paletteValueForApply(
  option: PaletteColorOption,
  alpha: boolean,
  nextAlpha: number,
) {
  return alpha ? hexToRgba(option.valueHex, nextAlpha) : option.token;
}

export function ColorValueEditor({
  value,
  alpha = false,
  label = "Color",
  paletteCatalog,
  onChange,
}: ColorValueEditorProps) {
  const [open, setOpen] = useState(false);
  const [draftValue, setDraftValue] = useState(value);
  const [pendingToken, setPendingToken] = useState("");
  const [pendingAlpha, setPendingAlpha] = useState(alphaValue(value, paletteCatalog));
  const [popoverRect, setPopoverRect] = useState({ top: 0, left: 0 });
  const rootRef = useRef<HTMLDivElement>(null);
  const popoverRef = useRef<HTMLDivElement>(null);
  const normalized = normalizeColor(value, alpha);
  const paletteColors = paletteCatalog?.colors ?? [];
  const currentPaletteOption = colorTokenOption(value, paletteCatalog);

  useEffect(() => {
    setDraftValue(value);
    setPendingToken(currentPaletteOption?.token ?? "");
    setPendingAlpha(alphaValue(value, paletteCatalog));
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
        left: Math.min(rect.left, window.innerWidth - 190),
      });
    }
    setPendingToken(currentPaletteOption?.token ?? paletteColors[0]?.token ?? "");
    setPendingAlpha(alphaValue(value, paletteCatalog));
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

  function applyPaletteSelection() {
    const option = paletteCatalog?.byToken.get(pendingToken);
    if (!option) return;
    onChange(paletteValueForApply(option, alpha, pendingAlpha));
    setOpen(false);
  }

  const pendingPaletteOption = paletteCatalog?.byToken.get(pendingToken);
  const pendingHex = pendingPaletteOption?.valueHex ?? "";
  const pendingRgb = pendingHex ? hexToCompactRgbString(pendingHex) : "";
  const pendingRgba = pendingHex ? hexToRgba(pendingHex, pendingAlpha) : "";

  return (
    <div className="color-value-editor" ref={rootRef}>
      {paletteColors.length ? (
        <>
          <button
            type="button"
            className="color-swatch-button"
            aria-label={`${label} picker`}
            onClick={toggleOpen}
          >
            <span style={{ background: displayColor(value, alpha, paletteCatalog) }} />
          </button>
          {alpha ? (
            <input
              aria-label={`${label} alpha`}
              className="color-alpha-input"
              type="number"
              min={0}
              max={1}
              step={0.01}
              value={String(alphaValue(value, paletteCatalog))}
              onChange={(event) =>
                onChange(withAlpha(value, Number(event.target.value), paletteCatalog))
              }
            />
          ) : (
            <span className="color-token-readout" title={currentPaletteOption?.token ?? value}>
              {currentPaletteOption?.token ?? value}
            </span>
          )}
        </>
      ) : alpha ? (
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
            onChange={(event) =>
              onChange(withAlpha(value, Number(event.target.value)))
            }
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
              {paletteColors.length ? (
                <div className="palette-picker">
                  <div className="palette-picker-grid" role="listbox" aria-label={label}>
                    {paletteColors.map((color) => (
                      <button
                        key={color.id}
                        type="button"
                        className={`palette-picker-swatch ${
                          pendingToken === color.token ? "is-selected" : ""
                        }`}
                        title={`${color.token} · ${color.valueHex}`}
                        aria-label={color.token}
                        aria-selected={pendingToken === color.token}
                        style={{ backgroundColor: color.valueHex }}
                        onClick={() => setPendingToken(color.token)}
                      />
                    ))}
                  </div>
                  {alpha ? (
                    <label className="palette-picker-alpha">
                      <span>Alpha</span>
                      <input
                        type="range"
                        min={0}
                        max={1}
                        step={0.01}
                        value={pendingAlpha}
                        onChange={(event) =>
                          setPendingAlpha(clampAlpha(Number(event.target.value)))
                        }
                      />
                      <input
                        type="number"
                        min={0}
                        max={1}
                        step={0.01}
                        value={String(pendingAlpha)}
                        onChange={(event) =>
                          setPendingAlpha(clampAlpha(Number(event.target.value)))
                        }
                      />
                    </label>
                  ) : null}
                  <div className="palette-picker-footer">
                    <div className="palette-picker-selection">
                      <span
                        className="palette-picker-selected-swatch"
                        style={{
                          backgroundColor:
                            paletteCatalog?.byToken.get(pendingToken)?.valueHex ??
                            "transparent",
                        }}
                        aria-hidden="true"
                      />
                      <span className="palette-picker-selected-token">
                        <strong>{pendingToken || "No color"}</strong>
                        {pendingHex ? (
                          <small>
                            {pendingHex} · {alpha ? pendingRgba : pendingRgb}
                          </small>
                        ) : null}
                      </span>
                    </div>
                    <div className="palette-picker-actions">
                      <button
                        type="button"
                        className="palette-picker-action"
                        onClick={applyPaletteSelection}
                      >
                        Apply
                      </button>
                      <button
                        type="button"
                        className="palette-picker-action"
                        onClick={() => setOpen(false)}
                      >
                        Cancel
                      </button>
                    </div>
                  </div>
                </div>
              ) : (
                <RgbaStringColorPicker color={normalized} onChange={changePickerColor} />
              )}
            </div>,
            document.body,
          )
        : null}
    </div>
  );
}
