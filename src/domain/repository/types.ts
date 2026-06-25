import type {
  Actor,
  AnimationPreset,
  App,
  Conversation,
  ConversationParticipant,
  Device,
  DeviceState,
  Episode,
  IconTheme,
  MediaAsset,
  Message,
  ModuleInstance,
  ModuleThemeConfig,
  NavigationBar,
  Notification,
  Production,
  ScreenEvent,
  ScreenInstance,
  Shot,
  StatusBar,
  Theme,
} from "../schemas/index.js";

export interface RepositoryDataset {
  productions: Production[];
  episodes: Episode[];
  shots: Shot[];
  screenInstances: ScreenInstance[];
  moduleInstances: ModuleInstance[];
  screenEvents: ScreenEvent[];
  iconThemes: IconTheme[];
  statusBars: StatusBar[];
  navigationBars: NavigationBar[];
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
  getModuleInstancesForScreenInstance(screenInstanceId: string): ModuleInstance[];
  getPrimaryModuleInstanceForScreenInstance(
    screenInstanceId: string,
  ): ModuleInstance | undefined;
  getScreenEventsForInstance(screenInstanceId: string): ScreenEvent[];
  getIconTheme(id: string): IconTheme | undefined;
  getStatusBar(id: string): StatusBar | undefined;
  getNavigationBar(id: string): NavigationBar | undefined;
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
