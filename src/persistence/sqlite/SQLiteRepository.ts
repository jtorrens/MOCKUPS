import type { DomainRepository } from "../../domain/repository/types.js";
import {
  ActorSchema,
  AppSchema,
  ConversationParticipantSchema,
  ConversationSchema,
  DeviceSchema,
  DeviceStateSchema,
  MediaAssetSchema,
  MessageSchema,
  ModuleInstanceSchema,
  ModuleThemeConfigSchema,
  NotificationSchema,
  ProductionSchema,
  ScreenEventSchema,
  ScreenInstanceSchema,
  ShotSchema,
  ThemeSchema,
  type Actor,
  type App,
  type Conversation,
  type ConversationParticipant,
  type Device,
  type DeviceState,
  type MediaAsset,
  type Message,
  type ModuleInstance,
  type ModuleThemeConfig,
  type Notification,
  type Production,
  type ScreenEvent,
  type ScreenInstance,
  type Shot,
  type Theme,
} from "../../domain/schemas/index.js";
import type { SQLiteDatabase } from "./createDatabase.js";
import {
  readNullableJson,
  readOptionalJson,
  readRequiredJson,
} from "./json.js";

type Row = Record<string, unknown>;

interface JsonColumnConfig {
  required?: string[];
  nullable?: string[];
  optional?: string[];
  optionalScalars?: string[];
  booleanFields?: string[];
}

interface Parser<T> {
  parse(value: unknown): T;
}

function decodeRow(row: Row, config: JsonColumnConfig = {}): Row {
  const decoded = { ...row };
  for (const field of config.required ?? []) {
    decoded[field] = readRequiredJson(row, field);
  }
  for (const field of config.nullable ?? []) {
    decoded[field] = readNullableJson(row, field);
  }
  for (const field of config.optional ?? []) {
    const value = readOptionalJson(row, field);
    if (value === undefined) {
      delete decoded[field];
    } else {
      decoded[field] = value;
    }
  }
  for (const field of config.optionalScalars ?? []) {
    if (decoded[field] === null) {
      delete decoded[field];
    }
  }
  for (const field of config.booleanFields ?? []) {
    decoded[field] = Boolean(decoded[field]);
  }
  return decoded;
}

export class SQLiteRepository implements DomainRepository {
  constructor(private readonly database: SQLiteDatabase) {}

  private getOne<T>(
    sql: string,
    id: string,
    parser: Parser<T>,
    config?: JsonColumnConfig,
  ): T | undefined {
    const row = this.database.prepare(sql).get(id) as Row | undefined;
    return row ? parser.parse(decodeRow(row, config)) : undefined;
  }

  private getMany<T>(
    sql: string,
    id: string,
    parser: Parser<T>,
    config?: JsonColumnConfig,
  ): T[] {
    const rows = this.database.prepare(sql).all(id) as Row[];
    return rows.map((row) => parser.parse(decodeRow(row, config)));
  }

  getProduction(id: string): Production | undefined {
    return this.getOne(
      "SELECT * FROM productions WHERE id = ?",
      id,
      ProductionSchema,
      {
        optional: ["settings_json", "metadata_json"],
        optionalScalars: ["slug", "created_at", "updated_at"],
      },
    );
  }

  getShot(id: string): Shot | undefined {
    return this.getOne("SELECT * FROM shots WHERE id = ?", id, ShotSchema, {
      optional: ["canvas_json", "metadata_json"],
      optionalScalars: ["episode_id", "owner_actor_id", "sort_order"],
    });
  }

  getScreenInstancesForShot(shotId: string): ScreenInstance[] {
    return this.getMany(
      "SELECT * FROM screen_instances WHERE shot_id = ? ORDER BY layer_order, id",
      shotId,
      ScreenInstanceSchema,
      {
        required: ["transform_json", "props_json"],
        optional: [
          "module_data_json",
          "module_config_json",
          "module_tokens_override_json",
        ],
        optionalScalars: [
          "module_id",
          "module_schema_version",
          "theme_mode",
        ],
        nullable: [
          "data_ref_json",
          "transition_in_json",
          "transition_out_json",
        ],
      },
    );
  }

  getModuleInstancesForScreenInstance(screenInstanceId: string): ModuleInstance[] {
    return this.getMany(
      "SELECT * FROM module_instances WHERE screen_instance_id = ? ORDER BY sort_order, id",
      screenInstanceId,
      ModuleInstanceSchema,
      {
        required: ["content_json", "behavior_json"],
        optional: ["metadata_json"],
        optionalScalars: ["sort_order"],
      },
    );
  }

  getPrimaryModuleInstanceForScreenInstance(
    screenInstanceId: string,
  ): ModuleInstance | undefined {
    return this.getModuleInstancesForScreenInstance(screenInstanceId)[0];
  }

  getScreenEventsForInstance(screenInstanceId: string): ScreenEvent[] {
    return this.getMany(
      "SELECT * FROM screen_events WHERE screen_instance_id = ? ORDER BY start_frame, id",
      screenInstanceId,
      ScreenEventSchema,
      { required: ["payload_json"] },
    );
  }

  getTheme(id: string): Theme | undefined {
    return this.getOne("SELECT * FROM themes WHERE id = ?", id, ThemeSchema, {
      required: ["tokens_json"],
    });
  }

  getModuleThemeConfig(
    themeId: string,
    appId: string,
    moduleId: string,
    moduleSchemaVersion: number,
  ): ModuleThemeConfig | undefined {
    const row = this.database
      .prepare(
        `SELECT * FROM module_theme_configs
         WHERE theme_id = ? AND app_id = ? AND module_id = ? AND module_schema_version = ?
         ORDER BY name, id
         LIMIT 1`,
      )
      .get(themeId, appId, moduleId, moduleSchemaVersion) as Row | undefined;
    return row
      ? ModuleThemeConfigSchema.parse(
          decodeRow(row, {
            required: ["tokens_json"],
            optional: ["metadata_json"],
          }),
        )
      : undefined;
  }

  getDevice(id: string): Device | undefined {
    return this.getOne("SELECT * FROM devices WHERE id = ?", id, DeviceSchema, {
      required: ["metrics_json"],
    });
  }

  getDeviceState(id: string): DeviceState | undefined {
    return this.getOne(
      "SELECT * FROM device_states WHERE id = ?",
      id,
      DeviceStateSchema,
      { required: ["state_json"] },
    );
  }

  getActor(id: string): Actor | undefined {
    return this.getOne("SELECT * FROM actors WHERE id = ?", id, ActorSchema, {
      optional: ["metadata_json"],
    });
  }

  getMediaAsset(id: string): MediaAsset | undefined {
    return this.getOne(
      "SELECT * FROM media_assets WHERE id = ?",
      id,
      MediaAssetSchema,
      { optional: ["dimensions_json", "metadata_json"] },
    );
  }

  getConversation(id: string): Conversation | undefined {
    return this.getOne(
      "SELECT * FROM conversations WHERE id = ?",
      id,
      ConversationSchema,
      { optional: ["metadata_json"] },
    );
  }

  getConversationParticipants(
    conversationId: string,
  ): ConversationParticipant[] {
    return this.getMany(
      "SELECT * FROM conversation_participants WHERE conversation_id = ? ORDER BY sort_order, id",
      conversationId,
      ConversationParticipantSchema,
      { optional: ["metadata_json"] },
    );
  }

  getMessagesForConversation(conversationId: string): Message[] {
    return this.getMany(
      "SELECT * FROM messages WHERE conversation_id = ? ORDER BY sort_order, id",
      conversationId,
      MessageSchema,
      {
        required: [
          "style_override_json",
          "animation_override_json",
          "layout_override_json",
          "metadata_json",
        ],
        booleanFields: ["write_on_enabled"],
      },
    );
  }

  getNotification(id: string): Notification | undefined {
    return this.getOne(
      "SELECT * FROM notifications WHERE id = ?",
      id,
      NotificationSchema,
      {
        required: ["payload_json"],
        optional: ["style_override_json", "metadata_json"],
      },
    );
  }

  getApp(id: string): App | undefined {
    return this.getOne("SELECT * FROM apps WHERE id = ?", id, AppSchema, {
      optional: ["config_json", "metadata_json"],
    });
  }
}
