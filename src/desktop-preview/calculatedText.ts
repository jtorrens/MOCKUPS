export type CalculatedTextMode = "literal" | "countUp" | "countDown";

export function resolveCalculatedText(
  value: string,
  mode: CalculatedTextMode,
  format: string,
  localFrame: number,
  frameRate: number,
) {
  if (mode === "literal") return value;
  if (mode !== "countUp" && mode !== "countDown") {
    throw new Error(`Unsupported calculated text mode ${mode}`);
  }
  if (!Number.isFinite(frameRate) || frameRate <= 0) {
    throw new Error(`Calculated text requires a positive frame rate`);
  }
  const calculatedFormat = parseCalculatedTextFormat(format);
  const initialValue = calculatedFormat.kind === "number"
    ? parseNumericValue(value, format)
    : parseClockValue(value, calculatedFormat);
  const elapsedSeconds = Math.floor(Math.max(0, localFrame) / frameRate);
  const resolvedValue = mode === "countUp"
    ? initialValue + elapsedSeconds
    : Math.max(0, initialValue - elapsedSeconds);
  return calculatedFormat.kind === "number"
    ? formatNumber(resolvedValue, calculatedFormat.minimumDigits)
    : formatClock(resolvedValue, calculatedFormat);
}

type CalculatedTextFormat =
  | { kind: "number"; minimumDigits: number }
  | {
      kind: "clock";
      largestUnit: "hours" | "minutes";
      largestWidth: number;
      showsSeconds: boolean;
    };

function parseCalculatedTextFormat(format: string): CalculatedTextFormat {
  const number = /^(#*)(0+)$/.exec(format);
  if (number) {
    return {
      kind: "number",
      minimumDigits: number[2]!.length,
    };
  }
  if (format === "M:SS" || format === "MM:SS") {
    return {
      kind: "clock",
      largestUnit: "minutes",
      largestWidth: format.startsWith("MM") ? 2 : 1,
      showsSeconds: true,
    };
  }
  if (
    format === "H:MM"
    || format === "HH:MM"
    || format === "H:MM:SS"
    || format === "HH:MM:SS"
  ) {
    return {
      kind: "clock",
      largestUnit: "hours",
      largestWidth: format.startsWith("HH") ? 2 : 1,
      showsSeconds: format.endsWith(":SS"),
    };
  }
  throw new Error(
    `Calculated text format "${format}" must use a supported clock mask or #*0+ numeric mask`,
  );
}

function parseNumericValue(value: string, format: string) {
  if (!/^\d+$/.test(value)) {
    throw new Error(
      `Calculated text value "${value}" must be a non-negative integer for format "${format}"`,
    );
  }
  return Number(value);
}

function parseClockValue(
  value: string,
  format: Extract<CalculatedTextFormat, { kind: "clock" }>,
) {
  const largest = format.largestWidth === 2 ? "(\\d{2,})" : "(\\d+)";
  const match = new RegExp(
    format.largestUnit === "minutes"
      ? `^${largest}:([0-5]\\d)$`
      : format.showsSeconds
        ? `^${largest}:([0-5]\\d):([0-5]\\d)$`
        : `^${largest}:([0-5]\\d)$`,
  ).exec(value);
  if (!match) {
    throw new Error(`Calculated text value "${value}" does not match its clock format`);
  }
  const largestValue = Number(match[1]);
  if (format.largestUnit === "minutes") {
    return largestValue * 60 + Number(match[2]);
  }
  return largestValue * 3600
    + Number(match[2]) * 60
    + (format.showsSeconds ? Number(match[3]) : 0);
}

function formatNumber(value: number, minimumDigits: number) {
  return Math.floor(value).toString().padStart(minimumDigits, "0");
}

function formatClock(
  value: number,
  format: Extract<CalculatedTextFormat, { kind: "clock" }>,
) {
  const totalSeconds = Math.floor(value);
  const largestValue = format.largestUnit === "hours"
    ? Math.floor(totalSeconds / 3600)
    : Math.floor(totalSeconds / 60);
  const middle = format.largestUnit === "hours"
    ? Math.floor(totalSeconds / 60) % 60
    : totalSeconds % 60;
  const largest = largestValue.toString().padStart(format.largestWidth, "0");
  const result = `${largest}:${middle.toString().padStart(2, "0")}`;
  return format.largestUnit === "hours" && format.showsSeconds
    ? `${result}:${(totalSeconds % 60).toString().padStart(2, "0")}`
    : result;
}
