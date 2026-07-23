import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentVariantConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import type { TextInputBarDesignContract } from "./textInputBarComponentContract.js";
import {
  parseObject,
  requiredNumber,
  requiredPossiblyEmptyString,
  requiredRecord,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import { resolveIconBarComponentFromRecords } from "./iconBarComponentResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import { resolveTextBoxComponentFromRecords } from "./textBoxComponentResolver.js";

export function resolveTextInputBarComponent(
  payload: DesignPreviewPayload,
): TextInputBarDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const textInput = requiredRecord(config, "textInput", "component.textInput");
  const barSurfaceSlot = requiredRecord(
    textInput,
    "barSurfaceSlot",
    "component.textInput.barSurfaceSlot",
  );
  const textBoxSlot = requiredRecord(
    textInput,
    "textBoxSlot",
    "component.textInput.textBoxSlot",
  );
  const iconBarSlot = requiredRecord(
    textInput,
    "iconBarSlot",
    "component.textInput.iconBarSlot",
  );
  const textBoxInputs = requiredRecord(
    textInput,
    "textBoxInputs",
    "component.textInput.textBoxInputs",
  );
  const sampleText = requiredPossiblyEmptyString(
    textBoxInputs,
    "sampleText",
    "component.textInput.textBox.sampleText",
  );
  const availableWidth = Math.max(
    1,
    requiredNumber(preview, "availableWidth", "component.textInputBar.input.availableWidth"),
  );
  const isTyping = sampleText.trim().length > 0;
  const height = requiredNumber(textInput, "height", "component.textInput.height");
  const embeddedBarSurfaceConfig = mergeComponentDefaults(
    componentVariantConfig(
      componentBaseConfigs,
      "surface",
      requiredString(
        barSurfaceSlot,
        "variantReference",
        "component.textInput.barSurfaceSlot.variantReference",
      ),
    ),
    requiredRecord(
      barSurfaceSlot,
      "overrides",
      "component.textInput.barSurfaceSlot.overrides",
    ),
  );
  const embeddedTextBoxConfig = mergeComponentDefaults(
    componentVariantConfig(
      componentBaseConfigs,
      "textBox",
      requiredString(
        textBoxSlot,
        "variantReference",
        "component.textInput.textBoxSlot.variantReference",
      ),
    ),
    requiredRecord(
      textBoxSlot,
      "overrides",
      "component.textInput.textBoxSlot.overrides",
    ),
  );
  const embeddedIconBarConfig = mergeComponentDefaults(
    componentVariantConfig(
      componentBaseConfigs,
      "iconBar",
      requiredString(iconBarSlot, "variantReference", "component.textInput.iconBarSlot.variantReference"),
    ),
    requiredRecord(
      iconBarSlot,
      "overrides",
      "component.textInput.iconBarSlot.overrides",
    ),
  );

  const resolvedTextBox = resolveTextBoxComponentFromRecords(
    embeddedTextBoxConfig,
    {
      ...textBoxInputs,
      sampleText,
      size: `${availableWidth}|${height}`,
      maxWidth: availableWidth,
    },
    componentBaseConfigs,
    "component.textInputBar.textBox",
  );

  return {
    id: "component.textInputBar",
    availableWidth,
    height,
    barPadding: toSpacingPair(requiredStringPair(
      textInput,
      "barPadding",
      "component.textInput.barPadding",
    )),
    barSurface: resolveSurfaceComponentAtSize(
      embeddedBarSurfaceConfig,
      { width: availableWidth, height },
      "component.textInputBar.barSurface",
    ),
    iconGapToken: requiredString(textInput, "iconGap", "component.textInput.iconGap"),
    iconBar: resolveIconBarComponentFromRecords(
      embeddedIconBarConfig,
      {
        state: isTyping ? "active" : "idle",
        size: `${availableWidth}|${height}`,
      },
      componentBaseConfigs,
      "component.textInputBar.iconBar",
    ),
    textBox: {
      ...resolvedTextBox,
      typography: {
        ...resolvedTextBox.typography,
        fontFamilyId: "theme.system",
      },
    },
  };
}

function toSpacingPair(pair: { first: string; second: string }) {
  return { xToken: pair.first, yToken: pair.second };
}
