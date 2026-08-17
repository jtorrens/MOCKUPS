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
    "component.textInputBar.barSurfaceSlot",
  );
  const textBoxSlot = requiredRecord(
    textInput,
    "textBoxSlot",
    "component.textInputBar.textBoxSlot",
  );
  const iconBarSlot = requiredRecord(
    textInput,
    "iconBarSlot",
    "component.textInputBar.iconBarSlot",
  );
  const textBoxInputs = requiredRecord(
    textInput,
    "textBoxInputs",
    "component.textInputBar.textBoxInputs",
  );
  const sampleText = requiredPossiblyEmptyString(
    textBoxInputs,
    "sampleText",
    "component.textInputBar.textBoxInputs.sampleText",
  );
  const availableWidth = Math.max(
    1,
    requiredNumber(preview, "availableWidth", "component.textInputBar.input.availableWidth"),
  );
  const isTyping = sampleText.trim().length > 0;
  const height = requiredNumber(textInput, "height", "component.textInputBar.height");
  const embeddedBarSurfaceConfig = mergeComponentDefaults(
    componentVariantConfig(
      componentBaseConfigs,
      "surface",
      requiredString(
        barSurfaceSlot,
        "variantReference",
        "component.textInputBar.barSurfaceSlot.variantReference",
      ),
    ),
    requiredRecord(
      barSurfaceSlot,
      "overrides",
      "component.textInputBar.barSurfaceSlot.overrides",
    ),
  );
  const embeddedTextBoxConfig = mergeComponentDefaults(
    componentVariantConfig(
      componentBaseConfigs,
      "textBox",
      requiredString(
        textBoxSlot,
        "variantReference",
        "component.textInputBar.textBoxSlot.variantReference",
      ),
    ),
    requiredRecord(
      textBoxSlot,
      "overrides",
      "component.textInputBar.textBoxSlot.overrides",
    ),
  );
  const embeddedIconBarConfig = mergeComponentDefaults(
    componentVariantConfig(
      componentBaseConfigs,
      "iconBar",
      requiredString(iconBarSlot, "variantReference", "component.textInputBar.iconBarSlot.variantReference"),
    ),
    requiredRecord(
      iconBarSlot,
      "overrides",
      "component.textInputBar.iconBarSlot.overrides",
    ),
  );

  const resolvedTextBox = resolveTextBoxComponentFromRecords(
    embeddedTextBoxConfig,
    {
      sampleText,
      size: `${availableWidth}|${height}`,
      maxWidth: availableWidth,
    },
    componentBaseConfigs,
    "component.textInputBar.textBox",
    payload,
  );

  return {
    id: "component.textInputBar",
    availableWidth,
    height,
    barPadding: toSpacingPair(requiredStringPair(
      textInput,
      "barPadding",
      "component.textInputBar.barPadding",
    )),
    barSurface: resolveSurfaceComponentAtSize(
      embeddedBarSurfaceConfig,
      { width: availableWidth, height },
      "component.textInputBar.barSurface",
    ),
    iconGapToken: requiredString(textInput, "iconGap", "component.textInputBar.iconGap"),
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
