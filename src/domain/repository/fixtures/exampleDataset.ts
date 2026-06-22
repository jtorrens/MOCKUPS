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
  ModuleThemeConfigSchema,
  NotificationSchema,
  ProductionSchema,
  ResolvedChatScreenPropsSchema,
  ScreenEventSchema,
  ScreenInstanceSchema,
  ScreenTemplateSchema,
  ShotSchema,
  ThemeSchema,
} from "../../schemas/index.js";
import type { RepositoryDataset } from "../types.js";

export function createExampleDataset(): RepositoryDataset {
  const production = ProductionSchema.parse(productionExample.production);
  const episode = EpisodeSchema.parse({
    id: "episode_demo_001",
    production_id: production.id,
    name: "Episode 1",
    sort_order: 0,
    metadata_json: {
      source: "initial_fixture",
      note: "Default episode container for demo shots.",
    },
  });
  const shot = ShotSchema.parse(shotExample.shot);
  const screenInstances = ScreenInstanceSchema.array().parse(
    shotExample.screen_instances,
  );
  const screenEvents = ScreenEventSchema.array().parse(
    shotExample.screen_events,
  );
  const theme = ThemeSchema.parse(themeExample);
  const chatModuleThemeConfigTokens = {
    layout: {
      screenGutter: 24,
    },
    header: {
      height: 96,
      background: "#FFFFFF",
      separatorColor: "#D1D1D6",
      separatorWidth: 1,
    },
    messages: {
      spacing: 6,
      groupSpacing: 12,
    },
    typography: {
      message: {
        fontFamily: "SF Pro Text",
        fontSize: 17,
        lineHeight: 21.25,
        fontWeight: 400,
      },
      headerTitle: {
        fontFamily: "SF Pro Text",
        fontSize: 17,
        lineHeight: 22,
        fontWeight: 600,
      },
      headerSubtitle: {
        fontFamily: "SF Pro Text",
        fontSize: 13,
        lineHeight: 16,
        fontWeight: 400,
      },
    },
    chatBubbles: {
      outgoingBackground: "#0B84FF",
      outgoingText: "#FFFFFF",
      incomingBackground: "#E9E9EB",
      incomingText: "#000000",
      paddingX: 14,
      paddingY: 9,
      maxWidthRatio: 0.6667,
      tail: {
        style: "rounded_wedge",
        width: 8,
        height: 12,
      },
      shadow: {
        color: "rgba(0,0,0,0)",
        offsetX: 0,
        offsetY: 0,
        blur: 0,
      },
    },
    avatars: {
      defaultSize: 32,
      headerSize: 56,
      gap: 8,
    },
    radii: {
      bubble: 18,
    },
    cursor: {
      style: "bar",
      width: 2,
      color: "#007AFF",
      blinkFrames: 15,
    },
  };
  const chatModuleThemeConfig = ModuleThemeConfigSchema.parse({
    id: "module_theme_config_ios_light_core_chat",
    production_id: production.id,
    theme_id: theme.id,
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
  const resolvedChat = ResolvedChatScreenPropsSchema.parse(resolvedChatExample);
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
    },
    {
      id: otherMessage.sender.id,
      production_id: production.id,
      display_name: otherMessage.sender.displayName,
      short_name: otherMessage.sender.displayName,
      avatar_asset_id: otherAvatar.assetId,
      default_device_id: device.id,
      default_theme_id: theme.id,
      metadata_json: {},
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

  const screenTemplates = ScreenTemplateSchema.array().parse([
    {
      id: "template_lock_default",
      production_id: production.id,
      name: "Default lock screen",
      screen_type: "lock_screen",
      module_key: "LockScreen",
      version: "1.0.0",
      default_props_json: {},
      config_json: {},
    },
    {
      id: "template_chat_default",
      production_id: production.id,
      name: "Default chat screen",
      screen_type: "chat",
      module_key: "ChatScreen",
      version: "1.0.0",
      default_props_json: {
        show_header: true,
        show_status_bar: true,
        message_grouping: "bySender",
      },
      config_json: {
        module_id: "core.chat",
        module_schema_version: 1,
        module_config_json: {
          showHeader: true,
          showStatusBar: true,
          showKeyboard: false,
          initialScroll: "keep_latest_visible",
          messageGrouping: "bySender",
          debugShowBounds: false,
        },
        module_tokens_override_json: {},
      },
    },
  ]);

  const app = AppSchema.parse({
    id: "app_messages",
    production_id: production.id,
    name: "Messages",
    bundle_key: "messages",
    app_type: "chat",
    config_json: {},
    metadata_json: {},
  });

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
    screenTemplates,
    screenInstances,
    screenEvents,
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
