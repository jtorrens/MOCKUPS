import deviceExample from "../../../../docs/examples/device_iphone_generic.json" with {
  type: "json",
};
import productionExample from "../../../../docs/examples/production_minimal.json" with {
  type: "json",
};
import resolvedChatExample from "../../../../docs/examples/resolved_props_chat_screen.json" with {
  type: "json",
};
import shotExample from "../../../../docs/examples/shot_lock_to_chat.json" with {
  type: "json",
};
import themeExample from "../../../../docs/examples/theme_ios_light.json" with {
  type: "json",
};
import {
  ActorSchema,
  AnimationPresetSchema,
  AppSchema,
  DeviceSchema,
  DeviceStateSchema,
  EpisodeSchema,
  MediaAssetSchema,
  ModuleInstanceSchema,
  ModuleThemeConfigSchema,
  NavigationBarSchema,
  NotificationSchema,
  ProductionSchema,
  ResolvedChatScreenPropsSchema,
  ScreenEventSchema,
  ScreenInstanceSchema,
  ShotSchema,
  StatusBarSchema,
  ThemeSchema,
} from "../../schemas/index.js";
import type { RepositoryDataset } from "../types.js";

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
        foreground: "#000000",
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
      device_state_json:
        instance.device_state_id === "device_state_locked_morning"
          ? { ...resolvedChat.deviceState, locked: true }
          : resolvedChat.deviceState,
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
  const app = AppSchema.parse({
    id: "app_messages",
    production_id: production.id,
    name: "Messages",
    bundle_key: "messages",
    app_type: "chat",
    config_json: {
      tokens_json: {
        wallpaper: {
          kind: "solid",
          opacity: 1,
        },
        modes: {
          light: {
            wallpaper: {
              color: "#FFFFFF",
            },
          },
          dark: {
            wallpaper: {
              color: "#000000",
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
      separatorWidth: 1,
      elementGap: 8,
      sidePadding: 8,
      iconSize: 24,
      leftIconTokens: "nav_chevron_left",
      rightIconTokens: "media_camera, phone_call",
      avatarSize: 56,
      avatarCornerRadius: 12,
      avatarBorderWidth: 0,
      avatarShadow: false,
      subtitleBottomPadding: 10,
    },
    messages: {
      spacing: 6,
      groupSpacing: 12,
    },
    typography: {
      message: {
        fontSize: 17,
        lineHeight: 21.25,
        fontWeight: "Regular",
      },
      headerTitle: {
        fontSize: 17,
        lineHeight: 22,
        fontWeight: "Semibold",
      },
      headerSubtitle: {
        fontSize: 13,
        lineHeight: 16,
        fontWeight: "Regular",
      },
    },
    chatBubbles: {
      paddingX: 14,
      paddingY: 9,
      maxWidthRatio: 0.6667,
      avatarSize: 32,
      avatarGap: 8,
      tail: {
        style: "rounded_wedge",
        width: 8,
        height: 12,
      },
      shadow: {
        offsetX: 0,
        offsetY: 0,
        blur: 0,
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
          typing: [{ token: "chat_send", order: 10, color: "#007AFF" }],
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
      bubble: 18,
    },
    modes: {
      light: {
        header: {
          background: "#FFFFFF",
          separatorColor: "#D1D1D6",
          avatarBorderColor: "#FFFFFF",
        },
        chatBubbles: {
          outgoingBackground: "#0B84FF",
          outgoingText: "#FFFFFF",
          incomingBackground: "#E9E9EB",
          incomingText: "#000000",
          shadow: {
            color: "rgba(0,0,0,0)",
          },
        },
      },
      dark: {
        header: {
          background: "#1C1C1E",
          separatorColor: "#38383A",
          avatarBorderColor: "#1C1C1E",
        },
        chatBubbles: {
          outgoingBackground: "#0A84FF",
          outgoingText: "#FFFFFF",
          incomingBackground: "#2C2C2E",
          incomingText: "#FFFFFF",
          shadow: {
            color: "rgba(0,0,0,0)",
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
      avatar_asset_id: ownerAvatar.assetId,
      metadata_json: {
        modes: {
          light: {
            color: "#64748b",
            avatarTextColor: "#ffffff",
          },
          dark: {
            color: "#475569",
            avatarTextColor: "#ffffff",
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
      avatar_asset_id: otherAvatar.assetId,
      default_device_id: device.id,
      default_theme_id: theme.id,
      metadata_json: {
        modes: {
          light: {
            color: "#3b82f6",
            avatarTextColor: "#ffffff",
          },
          dark: {
            color: "#60a5fa",
            avatarTextColor: "#0f172a",
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

  const mediaAssets = MediaAssetSchema.array().parse([
    {
      id: ownerAvatar.assetId,
      production_id: production.id,
      name: `${resolvedChat.ownerActor.displayName} avatar`,
      asset_type: "image",
      uri: ownerAvatar.uri,
      mime_type: "image/png",
      metadata_json: { scope: "production_reusable", usage: "actor_avatar" },
    },
    {
      id: otherAvatar.assetId,
      production_id: production.id,
      name: `${otherMessage.sender.displayName} avatar`,
      asset_type: "image",
      uri: otherAvatar.uri,
      mime_type: "image/png",
      metadata_json: { scope: "production_reusable", usage: "actor_avatar" },
    },
    {
      id: device.frame_asset_id,
      production_id: production.id,
      name: "Generic iPhone frame",
      asset_type: "image",
      uri: "assets/iphone-generic-frame.png",
      mime_type: "image/png",
      metadata_json: { scope: "production_reusable", usage: "device_frame" },
    },
  ]);

  const deviceStates = DeviceStateSchema.array().parse([
    {
      id: "device_state_locked_morning",
      production_id: production.id,
      device_id: device.id,
      name: "Locked morning",
      state_json: { ...resolvedChat.deviceState, locked: true },
    },
    {
      id: "device_state_unlocked_morning",
      production_id: production.id,
      device_id: device.id,
      name: "Unlocked morning",
      state_json: resolvedChat.deviceState,
    },
  ]);

  const animationPresets = AnimationPresetSchema.array().parse([
    {
      id: "animation_notification_slide",
      production_id: production.id,
      name: "Notification slide",
      animation_type: "slide",
      version: "1.0.0",
      parameters_json: { direction: "down", easing: "ease_out" },
    },
    {
      id: "animation_unlock_default",
      production_id: production.id,
      name: "Default unlock",
      animation_type: "unlock",
      version: "1.0.0",
      parameters_json: { easing: "ease_in_out" },
    },
  ]);

  const notification = NotificationSchema.parse({
    id: shotExample.references.notification_id,
    production_id: production.id,
    app_id: app.id,
    owner_actor_id: resolvedChat.ownerActor.id,
    sender_actor_id: otherMessage.sender.id,
    notification_type: "message",
    title: otherMessage.sender.displayName,
    body: otherMessage.text,
    sort_order: 0,
    payload_json: { destination_screen_instance_id: "screen_instance_chat" },
    style_override_json: {},
    metadata_json: {},
  });

  return {
    productions: [production],
    episodes: [episode],
    shots: [shot],
    screenInstances,
    moduleInstances,
    screenEvents,
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
    mediaAssets,
    conversations: [],
    conversationParticipants: [],
    messages: [],
    notifications: [notification],
  };
}
