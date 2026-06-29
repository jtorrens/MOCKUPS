import type {
  FieldDefinition,
  JsonFieldBinding,
} from "../value-system/index.js";
import { ANIMATION_PRESET_COLUMN_BINDINGS } from "./animationPresetFields.js";
import { ACTOR_COLUMN_BINDINGS } from "./actorFields.js";
import { DEVICE_COLUMN_BINDINGS } from "./deviceFields.js";
import { NAVIGATION_BAR_COLUMN_BINDINGS } from "./navigationBarFields.js";
import { PALETTE_COLOR_COLUMN_BINDINGS } from "./paletteColorFields.js";
import { PRODUCTION_COLUMN_BINDINGS } from "./productionFields.js";
import { PRODUCTION_FONT_COLUMN_BINDINGS } from "./productionFontFields.js";
import { RENDER_PRESET_COLUMN_BINDINGS } from "./renderPresetFields.js";
import { STATUS_BAR_COLUMN_BINDINGS } from "./statusBarFields.js";
import { THEME_COLUMN_BINDINGS } from "./themeFields.js";

const RECORD_COLUMN_BINDINGS: Record<string, readonly JsonFieldBinding[]> = {
  animation_presets: ANIMATION_PRESET_COLUMN_BINDINGS,
  actors: ACTOR_COLUMN_BINDINGS,
  devices: DEVICE_COLUMN_BINDINGS,
  navigation_bars: NAVIGATION_BAR_COLUMN_BINDINGS,
  palette_colors: PALETTE_COLOR_COLUMN_BINDINGS,
  productions: PRODUCTION_COLUMN_BINDINGS,
  production_fonts: PRODUCTION_FONT_COLUMN_BINDINGS,
  render_presets: RENDER_PRESET_COLUMN_BINDINGS,
  status_bars: STATUS_BAR_COLUMN_BINDINGS,
  themes: THEME_COLUMN_BINDINGS,
};

export function fieldDefinitionForRecordColumn(
  tableId: string,
  column: string,
): FieldDefinition | undefined {
  return RECORD_COLUMN_BINDINGS[tableId]?.find(
    (binding) =>
      binding.outputPath.length === 1 && binding.outputPath[0] === column,
  )?.field;
}
