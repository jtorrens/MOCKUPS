import type {
  Actor,
  AnimationPreset,
  App,
  Conversation,
  ConversationParticipant,
  Device,
  DeviceState,
  Episode,
  MediaAsset,
  Message,
  ModuleThemeConfig,
  Notification,
  Production,
  ScreenEvent,
  ScreenInstance,
  Shot,
  Theme,
} from "../schemas/index.js";

export interface RepositoryDataset {
  productions: Production[];
  episodes: Episode[];
  shots: Shot[];
  screenInstances: ScreenInstance[];
  screenEvents: ScreenEvent[];
  themes: Theme[];
  moduleThemeConfigs: ModuleThemeConfig[];
  devices: Device[];
  deviceStates: DeviceState[];
  actors: Actor[];
  animationPresets: AnimationPreset[];
  apps: App[];
  mediaAssets: MediaAsset[];
  conversations: Conversation[];
  conversationParticipants: ConversationParticipant[];
  messages: Message[];
  notifications: Notification[];
}

export interface DomainRepository {
  getProduction(id: string): Production | undefined;
  getShot(id: string): Shot | undefined;
  getScreenInstancesForShot(shotId: string): ScreenInstance[];
  getScreenEventsForInstance(screenInstanceId: string): ScreenEvent[];
  getTheme(id: string): Theme | undefined;
  getModuleThemeConfig(
    themeId: string,
    appId: string,
    moduleId: string,
    moduleSchemaVersion: number,
  ): ModuleThemeConfig | undefined;
  getDevice(id: string): Device | undefined;
  getDeviceState(id: string): DeviceState | undefined;
  getActor(id: string): Actor | undefined;
  getMediaAsset(id: string): MediaAsset | undefined;
  getConversation(id: string): Conversation | undefined;
  getConversationParticipants(conversationId: string): ConversationParticipant[];
  getMessagesForConversation(conversationId: string): Message[];
  getNotification(id: string): Notification | undefined;
  getApp(id: string): App | undefined;
}
