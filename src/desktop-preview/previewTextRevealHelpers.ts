export interface SimpleWriteOnPlan {
  enabled: boolean;
  timeSeconds: number;
  durationSeconds: number;
}

export function simpleWriteOnText(text: string, plan: SimpleWriteOnPlan) {
  if (!plan.enabled) return text;
  if (plan.durationSeconds <= 0) return text;

  return textGraphemes(text)
    .slice(0, simpleWriteOnVisibleCount(text, plan))
    .join("");
}

export function simpleWriteOnVisibleCount(text: string, plan: SimpleWriteOnPlan) {
  const characters = textGraphemes(text);
  const progress = Math.max(
    0,
    Math.min(1, plan.timeSeconds / Math.max(0.0001, plan.durationSeconds)),
  );
  return Math.floor(characters.length * progress);
}

export function textGraphemes(value: string) {
  const segmenterConstructor = (Intl as unknown as {
    Segmenter?: new (
      locale?: string,
      options?: { granularity?: "grapheme" },
    ) => { segment(input: string): Iterable<{ segment: string }> };
  }).Segmenter;

  if (segmenterConstructor) {
    return Array.from(
      new segmenterConstructor(undefined, { granularity: "grapheme" }).segment(value),
      (entry) => entry.segment,
    );
  }

  return Array.from(value);
}
