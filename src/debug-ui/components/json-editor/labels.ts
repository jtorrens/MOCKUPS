export function friendlyGroupLabel(group: string): string {
  return group
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .trim()
    .replace(/\s+/g, " ")
    .replace(/^./, (character) => character.toUpperCase());
}

export function singularFriendlyGroupLabel(group: string): string {
  const friendly = friendlyGroupLabel(group);
  if (/ies$/i.test(friendly)) return friendly.replace(/ies$/i, "y");
  if (/s$/i.test(friendly) && !/ss$/i.test(friendly)) {
    return friendly.slice(0, -1);
  }
  return friendly;
}

export function compactLabelForGroup(
  label: string,
  group: string | undefined,
): string {
  if (!group) return label;
  const prefixes = [
    friendlyGroupLabel(group),
    singularFriendlyGroupLabel(group),
  ].filter(Boolean);
  let compact = label;
  for (const prefix of prefixes) {
    compact = compact.replace(new RegExp(`^${prefix}\\s+`, "i"), "");
  }
  return compact.replace(/^./, (character) => character.toUpperCase());
}

export function friendlyPathLeafLabel(path: Array<string | number>): string {
  const last = path[path.length - 1];
  if (typeof last === "number") return `[${last}]`;
  return friendlyGroupLabel(String(last ?? "value"));
}

export function groupFromPathSegment(segment: unknown): string | undefined {
  return typeof segment === "string" ? segment : undefined;
}
