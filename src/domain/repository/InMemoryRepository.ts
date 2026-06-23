import type {
  Actor,
  App,
  Conversation,
  Device,
  DeviceState,
  Episode,
  MediaAsset,
  ModuleInstance,
  ModuleThemeConfig,
  Notification,
  Production,
  Shot,
  Theme,
} from "../schemas/index.js";
import type { DomainRepository, RepositoryDataset } from "./types.js";

function indexById<T extends { id: string }>(records: T[]): Map<string, T> {
  return new Map(records.map((record) => [record.id, record]));
}

export class InMemoryRepository implements DomainRepository {
  readonly #dataset: RepositoryDataset;
  readonly #productions: Map<string, Production>;
  readonly #episodes: Map<string, Episode>;
  readonly #shots: Map<string, Shot>;
  readonly #themes: Map<string, Theme>;
  readonly #moduleThemeConfigs: Map<string, ModuleThemeConfig>;
  readonly #moduleInstances: Map<string, ModuleInstance>;
  readonly #devices: Map<string, Device>;
  readonly #deviceStates: Map<string, DeviceState>;
  readonly #actors: Map<string, Actor>;
  readonly #apps: Map<string, App>;
  readonly #mediaAssets: Map<string, MediaAsset>;
  readonly #conversations: Map<string, Conversation>;
  readonly #notifications: Map<string, Notification>;

  constructor(dataset: RepositoryDataset) {
    this.#dataset = dataset;
    this.#productions = indexById(dataset.productions);
    this.#episodes = indexById(dataset.episodes);
    this.#shots = indexById(dataset.shots);
    this.#themes = indexById(dataset.themes);
    this.#moduleThemeConfigs = indexById(dataset.moduleThemeConfigs);
    this.#moduleInstances = indexById(dataset.moduleInstances);
    this.#devices = indexById(dataset.devices);
    this.#deviceStates = indexById(dataset.deviceStates);
    this.#actors = indexById(dataset.actors);
    this.#apps = indexById(dataset.apps);
    this.#mediaAssets = indexById(dataset.mediaAssets);
    this.#conversations = indexById(dataset.conversations);
    this.#notifications = indexById(dataset.notifications);
  }

  getProduction(id: string) {
    return this.#productions.get(id);
  }

  getShot(id: string) {
    return this.#shots.get(id);
  }

  getScreenInstancesForShot(shotId: string) {
    return this.#dataset.screenInstances
      .filter((instance) => instance.shot_id === shotId)
      .sort((a, b) => a.layer_order - b.layer_order);
  }

  getModuleInstancesForScreenInstance(screenInstanceId: string) {
    return this.#dataset.moduleInstances
      .filter((instance) => instance.screen_instance_id === screenInstanceId)
      .sort((a, b) => (a.sort_order ?? 0) - (b.sort_order ?? 0));
  }

  getPrimaryModuleInstanceForScreenInstance(screenInstanceId: string) {
    return this.getModuleInstancesForScreenInstance(screenInstanceId)[0];
  }

  getScreenEventsForInstance(screenInstanceId: string) {
    return this.#dataset.screenEvents
      .filter((event) => event.screen_instance_id === screenInstanceId)
      .sort((a, b) => a.start_frame - b.start_frame);
  }

  getTheme(id: string) {
    return this.#themes.get(id);
  }

  getModuleThemeConfig(
    themeId: string,
    appId: string,
    moduleId: string,
    moduleSchemaVersion: number,
  ) {
    return [...this.#moduleThemeConfigs.values()].find(
      (config) =>
        config.theme_id === themeId &&
        config.app_id === appId &&
        config.module_id === moduleId &&
        config.module_schema_version === moduleSchemaVersion,
    );
  }

  getDevice(id: string) {
    return this.#devices.get(id);
  }

  getDeviceState(id: string) {
    return this.#deviceStates.get(id);
  }

  getActor(id: string) {
    return this.#actors.get(id);
  }

  getMediaAsset(id: string) {
    return this.#mediaAssets.get(id);
  }

  getConversation(id: string) {
    return this.#conversations.get(id);
  }

  getConversationParticipants(conversationId: string) {
    return this.#dataset.conversationParticipants
      .filter((participant) => participant.conversation_id === conversationId)
      .sort((a, b) => a.sort_order - b.sort_order);
  }

  getMessagesForConversation(conversationId: string) {
    return this.#dataset.messages
      .filter((message) => message.conversation_id === conversationId)
      .sort((a, b) => a.sort_order - b.sort_order);
  }

  getNotification(id: string) {
    return this.#notifications.get(id);
  }

  getApp(id: string) {
    return this.#apps.get(id);
  }
}
