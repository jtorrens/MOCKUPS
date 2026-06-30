import deviceExample from "../../../../docs/examples/device_iphone_generic.json" with {
  type: "json",
};
import productionExample from "../../../../docs/examples/production_minimal.json" with {
  type: "json",
};
import resolvedChatExample from "../../../../docs/examples/resolved_props_chat_screen.json" with {
  type: "json",
};
import shotExample from "../../../../docs/examples/shot_chat.json" with {
  type: "json",
};
import themeExample from "../../../../docs/examples/theme_ios_light.json" with {
  type: "json",
};
import {
  ActorSchema,
  AnimationPresetSchema,
  AppSchema,
  ComponentClassSchema,
  DeviceSchema,
  DeviceStateSchema,
  EpisodeSchema,
  ModuleInstanceSchema,
  ModuleThemeConfigSchema,
  NavigationBarSchema,
  PaletteColorSchema,
  ProductionFontSchema,
  ProductionSchema,
  ResolvedChatScreenPropsSchema,
  ScreenEventSchema,
  ScreenInstanceSchema,
  ShotSchema,
  StatusBarSchema,
  ThemeSchema,
} from "../../schemas/index.js";
import type { RepositoryDataset } from "../types.js";

const SEED_PALETTE_COLORS = [
  ["debug_red", "#FF00FF"],
  ["gray_000", "#000000"],
  ["gray_010", "#1A1A1A"],
  ["gray_020", "#333333"],
  ["gray_030", "#4D4D4D"],
  ["gray_040", "#666666"],
  ["gray_050", "#808080"],
  ["gray_060", "#999999"],
  ["gray_070", "#B3B3B3"],
  ["gray_080", "#CCCCCC"],
  ["gray_090", "#E6E6E6"],
  ["gray_100", "#FFFFFF"],
  ["red", "#FA0000"],
  ["blue", "#007AFF"],
  ["blue_bright", "#0A84FF"],
  ["pastel_coral", "#FF8A80"],
  ["pastel_orange", "#FFB74D"],
  ["pastel_yellow", "#FFF176"],
  ["pastel_mint", "#66D9A3"],
  ["pastel_sky", "#64B5F6"],
  ["pastel_lavender", "#B39DDB"],
  ["purple", "#6750A4"],
  ["purple_tint", "#D0BCFF"],
] as const;

export function createExampleDataset(): RepositoryDataset {
  const production = ProductionSchema.parse(productionExample.production);
  const episode = EpisodeSchema.parse({
    id: "episode_demo_001",
    production_id: production.id,
    name: "Episode 1",
    slug: "episode-1",
    sort_order: 0,
    metadata_json: {
      note: "Default episode container for demo shots.",
    },
  });
  const resolvedChatSource = resolvedChatExample as Record<string, unknown> & {
    theme: Record<string, unknown>;
  };
  const resolvedChat = ResolvedChatScreenPropsSchema.parse({
    ...resolvedChatSource,
    theme: {
      ...resolvedChatSource.theme,
      navigationBar: resolvedChatSource.theme.navigationBar ?? {
        background: "transparent",
        foreground: "gray_000",
      },
      radii: resolvedChatSource.theme.radii ?? {
        control: 8,
        card: 12,
        panel: 16,
        surface: 18,
        pill: 999,
        avatar: 12,
        full: 9999,
      },
    },
    navigationBar: resolvedChatSource.navigationBar ?? {
      layout: {
        height: 0,
        itemSize: 0,
        sidePadding: 0,
        strokeWidth: 0,
        cornerRadius: 0,
        filled: false,
      },
      items: [],
    },
  });
  const shot = ShotSchema.parse(shotExample.shot);
  const screenInstances = ScreenInstanceSchema.array().parse(
    shotExample.screen_instances.map((instance) => ({
      ...instance,
      duration_frames:
        instance.duration_frames ??
        Math.max(1, Number(instance.end_frame) - Number(instance.start_frame)),
      device_state_json: resolvedChat.deviceState,
      module_data_json: null,
      module_config_json: null,
      module_tokens_override_json: null,
    })),
  );
  const moduleInstances = ModuleInstanceSchema.array().parse(
    shotExample.module_instances,
  );
  const screenEvents = ScreenEventSchema.array().parse(
    shotExample.screen_events,
  );
  const statusBar = StatusBarSchema.parse({
    id: "status_bar_ios_default",
    production_id: production.id,
    name: "iOS Default Status Bar",
    family: "ios",
    config_json: {
      schemaVersion: 2,
      layout: {
        height: 54,
        itemSize: 18,
        gap: 6,
        sidePadding: 24,
      },
      items: [
        {
          id: "time",
          label: "Time",
          kind: "text",
          value: "9:41",
          zone: "left",
          order: 10,
        },
        {
          id: "carrier",
          label: "Carrier",
          kind: "text",
          value: "",
          zone: "off",
          order: 20,
        },
        {
          id: "signal",
          label: "Signal",
          kind: "generatedSignal",
          value: 4,
          zone: "right",
          order: 10,
        },
        {
          id: "wifi",
          label: "Wi‑Fi",
          kind: "iconToken",
          token: "status_wifi",
          zone: "right",
          order: 20,
        },
        {
          id: "soundOff",
          label: "Sound Off",
          kind: "iconToken",
          token: "media_volume_off",
          zone: "off",
          order: 30,
        },
        {
          id: "bluetooth",
          label: "Bluetooth",
          kind: "iconToken",
          token: "status_bluetooth",
          zone: "off",
          order: 40,
        },
        {
          id: "battery",
          label: "Battery",
          kind: "generatedBattery",
          value: 85,
          charging: false,
          zone: "right",
          order: 50,
        },
      ],
    },
    metadata_json: {
      note: "Reusable status bar composition. Icon shapes resolve through the selected theme icon theme.",
    },
  });
  const navigationBar = NavigationBarSchema.parse({
    id: "navigation_bar_ios_default",
    production_id: production.id,
    name: "iOS Default Navigation Bar",
    family: "ios",
    config_json: {
      schemaVersion: 1,
      layout: {
        height: 34,
        itemSize: 18,
        sidePadding: 40,
        strokeWidth: 2,
        cornerRadius: 3,
        filled: false,
      },
      items: [
        {
          id: "back",
          label: "Back",
          kind: "generatedBack",
          zone: "left",
          order: 10,
        },
        {
          id: "home",
          label: "Home",
          kind: "generatedHome",
          zone: "center",
          order: 10,
        },
        {
          id: "recents",
          label: "Recents",
          kind: "generatedRecents",
          zone: "right",
          order: 10,
        },
      ],
    },
    metadata_json: {
      note: "Reusable navigation bar composition. Shapes are generated by the renderer; theme tokens provide color.",
    },
  });
  const theme = ThemeSchema.parse({
    ...themeExample,
    status_bar_id: statusBar.id,
    navigation_bar_id: navigationBar.id,
  });
  const paletteColors = PaletteColorSchema.array().parse(
    SEED_PALETTE_COLORS.map(([token, valueHex]) => ({
      id: `palette_${production.id}_${token}`,
      production_id: production.id,
      token,
      value_hex: valueHex,
      is_neutral: (() => {
        const normalized = valueHex.replace("#", "");
        return (
          normalized.slice(0, 2) === normalized.slice(2, 4) &&
          normalized.slice(2, 4) === normalized.slice(4, 6)
        );
      })()
        ? 1
        : 0,
      metadata_json: {
        source: token === "debug_red" ? "debug_sentinel" : "base_seed_palette",
        protected: token === "debug_red",
        hiddenFromPickers: token === "debug_red",
        note:
          token === "debug_red"
            ? "Protected sentinel color for unresolved theme/component color decisions."
            : "Primitive production color seeded from the base design palette.",
      },
    })),
  );
  const app = AppSchema.parse({
    id: "app_messages",
    production_id: production.id,
    name: "Messages",
    bundle_key: "messages",
    app_type: "chat",
    config_json: {
      tokens_json: {
        neutralTint: {
          hueDeg: 0,
          saturation: 0,
        },
        wallpaper: {
          kind: "solid",
          opacity: 1,
        },
        modes: {
          light: {
            wallpaper: {
              color: "gray_100",
            },
          },
          dark: {
            wallpaper: {
              color: "gray_000",
            },
          },
        },
      },
    },
    metadata_json: {
      note: "App-level reusable defaults inherited by Messages screens.",
      icon: {
        baseSize: 640,
        filePath: "",
        scale: 1,
        offsetX: 0,
        offsetY: 0,
      },
    },
  });
  const chatModuleThemeConfigTokens = {
    layout: {
      screenGutter: 24,
    },
    header: {
      height: 96,
      background: "colors.background",
      separatorWidth: 1,
      separatorColor: "borders.primary",
      elementGap: 8,
      sidePadding: 8,
      iconSize: 24,
      leftIconTokens: "nav_chevron_left",
      rightIconTokens: "media_camera, phone_call",
      avatarSize: 56,
      subtitleBottomPadding: 10,
    },
    messages: {
      spacing: 6,
      groupSpacing: 12,
    },
    chatBubbles: {
      paddingX: 14,
      paddingY: 9,
      maxWidthRatio: 0.6667,
      avatarSize: 32,
      avatarGap: 8,
      shadowEnabled: false,
      surfaceReliefEnabled: true,
      messageLabelUseActorColor: true,
      messageLabelOffsetX: 0,
      messageLabelOffsetY: 0,
      tail: {
        style: "rounded_wedge",
        verticalPosition: "bottom",
        width: 8,
        height: 12,
        scale: 1,
      },
      status: {
        showText: true,
        showTicks: true,
        size: 11,
        gap: 3,
        offsetX: -8,
        offsetY: -5,
        tickSingleIconToken: "message_check",
        tickDoubleIconToken: "message_done_all",
        textColor: "gray_040",
        sentColor: "gray_040",
        deliveredColor: "gray_040",
        readColor: "blue",
        failedColor: "red",
      },
    },
    textInputBar: {
      placeholder: "Mensaje",
      cursorVisible: true,
      iconSets: {
        left: {
          idle: [
            { token: "chat_emoji", order: 10 },
            { token: "chat_attach", order: 20 },
          ],
          typing: [{ token: "chat_emoji", order: 10 }],
        },
        right: {
          idle: [
            { token: "media_camera", order: 10 },
            { token: "media_mic", order: 20 },
          ],
          typing: [{ token: "chat_send", order: 10, color: "icons.accent" }],
        },
      },
    },
    keyboard: {
      language: "es",
      bottomItems: [
        {
          id: "app_language",
          label: "app_language",
          kind: "iconToken",
          token: "app_language",
          zone: "left",
          order: 10,
        },
        {
          id: "media_mic",
          label: "media_mic",
          kind: "iconToken",
          token: "media_mic",
          zone: "right",
          order: 10,
        },
      ],
    },
    radii: {
      control: 8,
      card: 12,
      panel: 16,
      surface: 18,
      pill: 999,
      avatar: 12,
      full: 9999,
    },
    modes: {
      light: {
        chatBubbles: {
          outgoingBackground: "blue",
          outgoingText: "gray_100",
          incomingBackground: "gray_100",
          incomingText: "gray_000",
          status: {
            textColor: "gray_040",
            sentColor: "gray_040",
            deliveredColor: "gray_040",
            readColor: "blue",
            failedColor: "red",
          },
        },
      },
      dark: {
        chatBubbles: {
          outgoingBackground: "blue_bright",
          outgoingText: "gray_100",
          incomingBackground: "gray_020",
          incomingText: "gray_100",
          status: {
            textColor: "gray_060",
            sentColor: "gray_060",
            deliveredColor: "gray_060",
            readColor: "blue_bright",
            failedColor: "red",
          },
        },
      },
    },
  };
  const chatModuleThemeConfig = ModuleThemeConfigSchema.parse({
    id: "module_theme_config_ios_light_core_chat",
    production_id: production.id,
    theme_id: theme.id,
    app_id: app.id,
    module_id: "core.chat",
    module_schema_version: 1,
    name: "iOS Light Chat defaults",
    tokens_json: chatModuleThemeConfigTokens,
    metadata_json: {
      source: "0016_module_theme_configs",
      note: "Chat-specific defaults moved out of global theme tokens.",
      default_tokens_json: chatModuleThemeConfigTokens,
    },
  });
  const device = DeviceSchema.parse(deviceExample);
  const otherMessage = resolvedChat.messages.find(
    (message) => message.sender.id !== resolvedChat.ownerActor.id,
  );

  if (!otherMessage) {
    throw new Error("Example chat requires a participant other than its owner");
  }

  const ownerAvatar = resolvedChat.ownerActor.avatar;
  const otherAvatar = resolvedChat.header.avatar;
  if (!ownerAvatar || !otherAvatar) {
    throw new Error("Example chat actors require resolved avatar assets");
  }

  const actors = ActorSchema.array().parse([
    {
      ...productionExample.actors[0],
      metadata_json: {
        modes: {
          light: {
            color: "blue",
            avatarTextColor: "gray_100",
          },
          dark: {
            color: "blue_bright",
            avatarTextColor: "gray_100",
          },
        },
        avatar: {
          baseSize: 640,
          filePath: ownerAvatar.uri,
          useInitials: false,
          scale: 1,
          offsetX: 0,
          offsetY: 0,
        },
      },
    },
    {
      id: otherMessage.sender.id,
      production_id: production.id,
      display_name: otherMessage.sender.displayName,
      short_name: otherMessage.sender.displayName,
      default_device_id: device.id,
      default_theme_id: theme.id,
      metadata_json: {
        modes: {
          light: {
            color: "gray_040",
            avatarTextColor: "gray_100",
          },
          dark: {
            color: "gray_020",
            avatarTextColor: "gray_100",
          },
        },
        avatar: {
          baseSize: 640,
          filePath: otherAvatar.uri,
          useInitials: false,
          scale: 1,
          offsetX: 0,
          offsetY: 0,
        },
      },
    },
  ]);

  const productionFonts = ProductionFontSchema.array().parse([
    {
      id: "font_oswald_regular",
      production_id: production.id,
      family: "Oswald",
      category: "normal",
      files_json: {
        files: [
          {
            style: "Regular",
            filePath: "fonts/Oswald/Oswald-Regular.ttf",
          },
        ],
      },
      metadata_json: {
        note: "Demo production font family placeholder. Add the font family files in a real production root.",
      },
    },
  ]);

  const deviceStates = DeviceStateSchema.array().parse([
    {
      id: "device_state_chat_morning",
      production_id: production.id,
      device_id: device.id,
      name: "Chat morning",
      state_json: resolvedChat.deviceState,
    },
  ]);

  const animationPresets = AnimationPresetSchema.array().parse([
  ]);
  const avatarComponentClass = ComponentClassSchema.parse({
    id: `${production.id}:avatar_default`,
    production_id: production.id,
    component_type: "avatar",
    name: "Default avatar",
    tokens_json: {
      schemaVersion: 1,
      componentType: "avatar",
      cornerRadius: 12,
      borderWidth: 0,
      borderColorToken: "borders.primary",
      shadowEnabled: false,
      shadowToken: "system",
      surfaceReliefEnabled: true,
    },
    metadata_json: { source: "seed" },
  });
  const buttonIconComponentClass = ComponentClassSchema.parse({
    id: `${production.id}:button_icon_default`,
    production_id: production.id,
    component_type: "button_icon",
    name: "Default icon button",
    tokens_json: {
      schemaVersion: 1,
      componentType: "button_icon",
      cornerRadius: 0,
      iconPadding: 2,
      borderWidth: 0,
      borderColorToken: "borders.primary",
      shadowEnabled: false,
      shadowToken: "system",
      surfaceReliefEnabled: false,
      labelEnabled: false,
      labelPosition: "bottom",
      labelPadding: 2,
      labelSize: 10,
      labelColorToken: "icons.primary",
    },
    metadata_json: { source: "seed" },
  });
  const labelComponentClass = ComponentClassSchema.parse({
    id: `${production.id}:label_default`,
    production_id: production.id,
    component_type: "label",
    name: "Default label",
    tokens_json: {
      schemaVersion: 1,
      componentType: "label",
      sizingMode: "content",
      width: 120,
      height: 28,
      paddingX: 8,
      paddingY: 4,
      cornerRadius: 10,
      borderWidth: 0,
      borderColorToken: "borders.primary",
      backgroundVisible: true,
      backgroundColorToken: "background",
      textColorToken: "textPrimary",
      fontSize: 12,
      shadowEnabled: false,
      shadowToken: "system",
      surfaceReliefEnabled: false,
    },
    metadata_json: { source: "seed" },
  });
  const audioMessageComponentClass = ComponentClassSchema.parse({
    id: `${production.id}:audio_message_default`,
    production_id: production.id,
    component_type: "audio_message",
    name: "Default audio message",
    tokens_json: {
      schemaVersion: 1,
      componentType: "audio_message",
      width: 260,
      height: 58,
      avatarSize: 38,
      avatarPosition: "left",
      avatarGap: 8,
      playCircleSize: 32,
      playCircleColorToken: "icons.accent",
      playIconColorToken: "icons.secondary",
      microphoneBadgeSize: 16,
      microphoneBadgeIconToken: "media_mic",
      waveformBarCount: 28,
      waveformGap: 2,
      waveformMinHeight: 4,
      waveformMaxHeight: 22,
      progressKnobSize: 9,
      waveformColorToken: "icons.primary",
      waveformPlayedColorToken: "icons.accent",
      textSize: 11,
      textColorToken: "icons.secondary",
    },
    metadata_json: { source: "seed" },
  });
  const videoMessageComponentClass = ComponentClassSchema.parse({
    id: `${production.id}:video_message_default`,
    production_id: production.id,
    component_type: "video_message",
    name: "Default video message",
    tokens_json: {
      schemaVersion: 1,
      componentType: "video_message",
      playOverlayEnabled: true,
      playCircleSize: 44,
      playCircleColorToken: "icons.accent",
      playCircleAlpha: 0.55,
      playIconColorToken: "icons.secondary",
      statusVisible: true,
      statusIconToken: "media_video",
      statusColorToken: "icons.secondary",
      statusSize: 12,
      statusPaddingX: 8,
      statusPaddingY: 6,
      statusGap: 4,
    },
    metadata_json: { source: "seed" },
  });
  const textInputBarComponentClass = ComponentClassSchema.parse({
    id: `${production.id}:text_input_bar_default`,
    production_id: production.id,
    component_type: "text_input_bar",
    name: "Default text input bar",
    tokens_json: {
      schemaVersion: 1,
      componentType: "text_input_bar",
      placeholder: "Mensaje",
      cursorVisible: true,
      idleTextColor: "icons.secondary",
      cursorWidth: 2,
      cursorBlinkFrames: 15,
      cursorColor: "theme.cursor.color",
      fieldRadius: 20,
      fieldShadowEnabled: true,
      iconSets: {
        left: {
          idle: [
            { token: "chat_emoji", order: 10 },
            { token: "chat_attach", order: 20 },
          ],
          typing: [{ token: "chat_emoji", order: 10 }],
        },
        right: {
          idle: [
            { token: "media_camera", order: 10 },
            { token: "media_mic", order: 20 },
          ],
          typing: [{ token: "chat_send", order: 10, color: "icons.accent" }],
        },
      },
    },
    metadata_json: { source: "seed" },
  });
  const keyboardComponentClass = ComponentClassSchema.parse({
    id: `${production.id}:keyboard_default`,
    production_id: production.id,
    component_type: "keyboard",
    name: "Default keyboard",
    tokens_json: {
      schemaVersion: 1,
      componentType: "keyboard",
      language: "es",
      fontFamily: "Oswald",
      fontWeight: 400,
      fontStyle: "normal",
      pushDurationFrames: 8,
      messageGapToTextInput: 10,
      pressedEffect: "popover",
      keyRadius: 7,
      keyPadding: 6,
      keyShadowEnabled: true,
      surfaceReliefEnabled: true,
      bottomItems: [
        {
          id: "app_language",
          label: "app_language",
          kind: "iconToken",
          token: "app_language",
          zone: "left",
          order: 10,
        },
        {
          id: "media_mic",
          label: "media_mic",
          kind: "iconToken",
          token: "media_mic",
          zone: "right",
          order: 10,
        },
      ],
    },
    metadata_json: { source: "seed" },
  });

  return {
    productions: [production],
    episodes: [episode],
    shots: [shot],
    screenInstances,
    moduleInstances,
    screenEvents,
    componentClasses: [
      avatarComponentClass,
      audioMessageComponentClass,
      buttonIconComponentClass,
      labelComponentClass,
      videoMessageComponentClass,
      textInputBarComponentClass,
      keyboardComponentClass,
    ],
    iconThemes: [],
    statusBars: [statusBar],
    navigationBars: [navigationBar],
    themes: [theme],
    moduleThemeConfigs: [chatModuleThemeConfig],
    devices: [device],
    deviceStates,
    actors,
    animationPresets,
    apps: [app],
    paletteColors,
    productionFonts,
    conversations: [],
    conversationParticipants: [],
    messages: [],
    notifications: [],
  };
}
