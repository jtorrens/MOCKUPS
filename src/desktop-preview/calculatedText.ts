export type CalculatedTextMode = "literal" | "countUp" | "countDown";

export function resolveCalculatedText(
  value: string,
  mode: CalculatedTextMode,
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
  const match = /^(\d+):([0-5]\d)$/.exec(value);
  if (!match) {
    throw new Error(`Calculated text value "${value}" must use M:SS or MM:SS format`);
  }
  const minuteWidth = match[1].length;
  const initialSeconds = Number(match[1]) * 60 + Number(match[2]);
  const elapsedSeconds = Math.floor(Math.max(0, localFrame) / frameRate);
  const seconds = mode === "countUp"
    ? initialSeconds + elapsedSeconds
    : Math.max(0, initialSeconds - elapsedSeconds);
  const minutes = Math.floor(seconds / 60).toString().padStart(minuteWidth, "0");
  return `${minutes}:${(seconds % 60).toString().padStart(2, "0")}`;
}
