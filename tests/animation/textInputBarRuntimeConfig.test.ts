import assert from "node:assert/strict";
import test from "node:test";
import { resolveTextInputBarComponent } from "../../src/desktop-preview/textInputBarComponentResolver.js";
import { resolvedTextInputBarRuntimeConfig } from "../../src/desktop-preview/textInputBarRuntimeConfig.js";
import { committedComponentFixture } from "./committedComponentFixture.js";

test("a parent Runtime value changes an Icon Bar glyph without changing its Variant topology", () => {
  const fixture = committedComponentFixture("textInputBar", "default_copy");
  const forwardedIconKey = "conversationTextBarActiveRightIcon";
  const slot = {
    variantReference: "component_project_foqn_s2_textInputBar::variant::default_copy",
    overrides: {
      textInput: {
        iconBarSlot: {
          overrides: {
            iconBar: {
              activeRightIconRowInputs: {
                buttonInputs: [{
                  id: "button_001",
                  state: "normal",
                  sampleText: "",
                  iconToken: "chat_send",
                  iconSizeToken: "theme.iconSizes.m",
                  textSizeToken: "theme.typography.sizes.s",
                  showBadge: false,
                  badgeContentMode: "text",
                  badgeIconToken: "system_check",
                  badgeText: "1",
                  badgeSize: 20,
                  badgeBackgroundPaletteColor: "palette_project_foqn_s2_blue",
                  badgeContentPaletteColor: "palette_project_foqn_s2_gray_100",
                  iconColorToken: "theme.colors.icon",
                  textColorToken: "theme.colors.textPrimary",
                  $forwardedInputs: {
                    iconToken: {
                      id: "forwarded.component.iconBar.activeRight.button_001.iconToken",
                      jsonKey: forwardedIconKey,
                    },
                  },
                }],
              },
            },
          },
        },
      },
    },
  };
  const parentPayload = {
    ...fixture,
    designPreviewJson: JSON.stringify({
      ...JSON.parse(fixture.designPreviewJson),
      [forwardedIconKey]: "media_camera",
    }),
  };
  const config = resolvedTextInputBarRuntimeConfig(
    parentPayload,
    JSON.parse(fixture.componentBaseConfigsJson),
    slot,
    "Message",
    360,
    "module.core.chat.textInputBarSlot",
  );
  const resolved = resolveTextInputBarComponent({
    ...fixture,
    configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify({ availableWidth: 360 }),
  });

  assert.deepEqual(
    resolved.iconBar.rows.right.items.map((item) => item.id),
    ["button_001"],
  );
  assert.equal(resolved.iconBar.rows.right.items[0]!.button.iconToken, "media_camera");
});
