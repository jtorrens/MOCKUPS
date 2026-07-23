import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { parseObject, requiredNumber, requiredPlacement, requiredRecord, requiredString, requiredTypographyStyle } from "./componentResolverCommon.js";
import type { BadgeDesignContract } from "./badgeComponentContract.js";

export function resolveBadgeComponent(payload: DesignPreviewPayload): BadgeDesignContract {
  return resolveBadgeComponentFromRecords(
    parseObject(payload.configJson),
    parseObject(payload.designPreviewJson),
    "component.badge",
  );
}

export function resolveBadgeComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  id: string,
): BadgeDesignContract {
  const badge = requiredRecord(config, "badge", `${id}.badge`);
  const contentMode = requiredString(inputs, "contentMode", `${id}.input.contentMode`);
  if (contentMode !== "icon" && contentMode !== "text") {
    throw new Error(`Unsupported Badge content mode ${contentMode}`);
  }
  return {
    id,
    contentMode,
    iconToken: contentMode === "icon"
      ? requiredString(inputs, "iconToken", `${id}.input.iconToken`)
      : typeof inputs.iconToken === "string" ? inputs.iconToken : "",
    text: contentMode === "text"
      ? requiredString(inputs, "text", `${id}.input.text`)
      : typeof inputs.text === "string" ? inputs.text : "",
    size: requiredNumber(inputs, "size", `${id}.input.size`),
    textTypography: requiredTypographyStyle(badge, "textTypography", `${id}.textTypography`),
    backgroundPaletteColor: requiredString(inputs, "backgroundPaletteColor", `${id}.input.backgroundPaletteColor`),
    contentPaletteColor: requiredString(inputs, "contentPaletteColor", `${id}.input.contentPaletteColor`),
    paddingToken: requiredString(badge, "paddingToken", `${id}.paddingToken`),
    placement: requiredPlacement(badge, "placement", `${id}.placement`),
  };
}
