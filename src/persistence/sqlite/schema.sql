PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS productions (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  slug TEXT,
  default_fps INTEGER NOT NULL DEFAULT 30 CHECK (default_fps > 0),
  created_at TEXT,
  updated_at TEXT,
  settings_json TEXT,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS media_assets (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  asset_type TEXT NOT NULL,
  uri TEXT NOT NULL,
  mime_type TEXT NOT NULL,
  checksum TEXT,
  dimensions_json TEXT,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS production_fonts (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  family TEXT NOT NULL,
  files_json TEXT NOT NULL,
  source_path TEXT,
  metadata_json TEXT,
  UNIQUE (production_id, family)
);

CREATE TABLE IF NOT EXISTS palette_colors (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  token TEXT NOT NULL,
  value_hex TEXT NOT NULL CHECK (value_hex GLOB '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'),
  metadata_json TEXT,
  UNIQUE (production_id, token)
);

CREATE TABLE IF NOT EXISTS episodes (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  slug TEXT,
  sort_order INTEGER CHECK (sort_order >= 0),
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS icon_themes (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  family TEXT NOT NULL,
  asset_root TEXT NOT NULL,
  mapping_json TEXT NOT NULL,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS status_bars (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  family TEXT NOT NULL,
  config_json TEXT NOT NULL,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS navigation_bars (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  family TEXT NOT NULL,
  config_json TEXT NOT NULL,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS component_classes (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  component_type TEXT NOT NULL,
  name TEXT NOT NULL,
  tokens_json TEXT NOT NULL,
  metadata_json TEXT,
  UNIQUE (production_id, component_type, name)
);

CREATE TABLE IF NOT EXISTS themes (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  family TEXT NOT NULL,
  icon_theme_id TEXT REFERENCES icon_themes(id) ON DELETE SET NULL,
  status_bar_id TEXT REFERENCES status_bars(id) ON DELETE SET NULL,
  navigation_bar_id TEXT REFERENCES navigation_bars(id) ON DELETE SET NULL,
  version TEXT NOT NULL,
  tokens_json TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS module_theme_configs (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  theme_id TEXT NOT NULL REFERENCES themes(id) ON DELETE CASCADE,
  app_id TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
  module_id TEXT NOT NULL,
  module_schema_version INTEGER NOT NULL CHECK (module_schema_version > 0),
  name TEXT NOT NULL,
  tokens_json TEXT NOT NULL,
  metadata_json TEXT,
  UNIQUE (theme_id, app_id, module_id, module_schema_version, name)
);

CREATE TABLE IF NOT EXISTS devices (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  manufacturer TEXT NOT NULL,
  model TEXT NOT NULL,
  os_family TEXT NOT NULL,
  metrics_json TEXT NOT NULL,
  frame_asset_id TEXT REFERENCES media_assets(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS device_states (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  device_id TEXT NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  state_json TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS actors (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  display_name TEXT NOT NULL,
  short_name TEXT,
  avatar_asset_id TEXT REFERENCES media_assets(id) ON DELETE SET NULL,
  default_device_id TEXT REFERENCES devices(id) ON DELETE SET NULL,
  default_theme_id TEXT REFERENCES themes(id) ON DELETE SET NULL,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS apps (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  bundle_key TEXT NOT NULL,
  app_type TEXT NOT NULL,
  icon_asset_id TEXT REFERENCES media_assets(id) ON DELETE SET NULL,
  config_json TEXT,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS animation_presets (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  animation_type TEXT NOT NULL,
  version TEXT NOT NULL,
  parameters_json TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS render_presets (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  width INTEGER NOT NULL CHECK (width > 0),
  height INTEGER NOT NULL CHECK (height > 0),
  fps INTEGER NOT NULL CHECK (fps > 0),
  format TEXT NOT NULL,
  codec_json TEXT,
  color_json TEXT,
  quality_json TEXT,
  export_json TEXT
);

CREATE TABLE IF NOT EXISTS shots (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  episode_id TEXT REFERENCES episodes(id) ON DELETE SET NULL,
  owner_actor_id TEXT REFERENCES actors(id) ON DELETE SET NULL,
  name TEXT NOT NULL,
  slug TEXT,
  version INTEGER NOT NULL DEFAULT 1 CHECK (version >= 0),
  sort_order INTEGER CHECK (sort_order >= 0),
  duration_frames INTEGER NOT NULL CHECK (duration_frames > 0),
  fps INTEGER NOT NULL CHECK (fps > 0),
  render_preset_id TEXT REFERENCES render_presets(id) ON DELETE SET NULL,
  canvas_json TEXT,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS conversations (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  app_id TEXT REFERENCES apps(id) ON DELETE SET NULL,
  owner_actor_id TEXT REFERENCES actors(id) ON DELETE SET NULL,
  target_actor_id TEXT REFERENCES actors(id) ON DELETE SET NULL,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS conversation_participants (
  id TEXT PRIMARY KEY,
  conversation_id TEXT NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
  actor_id TEXT NOT NULL REFERENCES actors(id) ON DELETE CASCADE,
  role TEXT NOT NULL,
  sort_order INTEGER NOT NULL CHECK (sort_order >= 0),
  metadata_json TEXT,
  UNIQUE (conversation_id, actor_id)
);

CREATE TABLE IF NOT EXISTS messages (
  id TEXT PRIMARY KEY,
  conversation_id TEXT NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
  sort_order INTEGER NOT NULL CHECK (sort_order >= 0),
  sender_actor_id TEXT NOT NULL REFERENCES actors(id) ON DELETE RESTRICT,
  message_type TEXT NOT NULL,
  text TEXT,
  start_frame INTEGER NOT NULL CHECK (start_frame >= 0),
  enter_duration_frames INTEGER NOT NULL CHECK (enter_duration_frames >= 0),
  write_on_enabled INTEGER NOT NULL CHECK (write_on_enabled IN (0, 1)),
  write_on_start_frame INTEGER CHECK (write_on_start_frame >= 0),
  write_on_duration_frames INTEGER CHECK (write_on_duration_frames >= 0),
  exit_frame INTEGER CHECK (exit_frame >= 0),
  media_asset_id TEXT REFERENCES media_assets(id) ON DELETE SET NULL,
  style_override_json TEXT NOT NULL,
  animation_override_json TEXT NOT NULL,
  layout_override_json TEXT NOT NULL,
  metadata_json TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS notifications (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  app_id TEXT NOT NULL REFERENCES apps(id) ON DELETE RESTRICT,
  owner_actor_id TEXT NOT NULL REFERENCES actors(id) ON DELETE RESTRICT,
  sender_actor_id TEXT REFERENCES actors(id) ON DELETE SET NULL,
  notification_type TEXT NOT NULL,
  title TEXT NOT NULL,
  body TEXT NOT NULL,
  sort_order INTEGER NOT NULL CHECK (sort_order >= 0),
  payload_json TEXT NOT NULL,
  style_override_json TEXT,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS calls (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  app_id TEXT REFERENCES apps(id) ON DELETE SET NULL,
  owner_actor_id TEXT NOT NULL REFERENCES actors(id) ON DELETE RESTRICT,
  target_actor_id TEXT NOT NULL REFERENCES actors(id) ON DELETE RESTRICT,
  call_type TEXT NOT NULL,
  initial_state TEXT NOT NULL,
  payload_json TEXT,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS data_sources (
  id TEXT PRIMARY KEY,
  production_id TEXT NOT NULL REFERENCES productions(id) ON DELETE CASCADE,
  name TEXT NOT NULL,
  data_type TEXT NOT NULL,
  app_id TEXT REFERENCES apps(id) ON DELETE SET NULL,
  version TEXT NOT NULL,
  data_json TEXT NOT NULL,
  config_json TEXT,
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS screen_instances (
  id TEXT PRIMARY KEY,
  shot_id TEXT NOT NULL REFERENCES shots(id) ON DELETE CASCADE,
  app_id TEXT NOT NULL REFERENCES apps(id) ON DELETE RESTRICT,
  screen_type TEXT NOT NULL,
  module_id TEXT,
  module_schema_version INTEGER CHECK (module_schema_version > 0),
  owner_actor_id TEXT NOT NULL REFERENCES actors(id) ON DELETE RESTRICT,
  device_id TEXT REFERENCES devices(id) ON DELETE SET NULL,
  device_state_id TEXT REFERENCES device_states(id) ON DELETE SET NULL,
  device_state_json TEXT,
  theme_id TEXT REFERENCES themes(id) ON DELETE SET NULL,
  theme_mode TEXT CHECK (theme_mode IS NULL OR theme_mode IN ('light', 'dark')),
  duration_frames INTEGER NOT NULL DEFAULT 1 CHECK (duration_frames > 0),
  start_frame INTEGER NOT NULL CHECK (start_frame >= 0),
  end_frame INTEGER NOT NULL CHECK (end_frame > start_frame),
  layer_order INTEGER NOT NULL,
  data_ref_json TEXT,
  module_data_json TEXT,
  module_config_json TEXT,
  module_tokens_override_json TEXT,
  transform_json TEXT NOT NULL,
  props_json TEXT NOT NULL,
  transition_in_json TEXT,
  transition_out_json TEXT
);

CREATE TABLE IF NOT EXISTS module_instances (
  id TEXT PRIMARY KEY,
  screen_instance_id TEXT NOT NULL REFERENCES screen_instances(id) ON DELETE CASCADE,
  module_id TEXT NOT NULL,
  module_schema_version INTEGER NOT NULL CHECK (module_schema_version > 0),
  sort_order INTEGER CHECK (sort_order >= 0),
  content_json TEXT NOT NULL,
  behavior_json TEXT NOT NULL,
  animation_json TEXT NOT NULL DEFAULT '{"schemaVersion":1,"tracks":[]}',
  metadata_json TEXT
);

CREATE TABLE IF NOT EXISTS screen_events (
  id TEXT PRIMARY KEY,
  screen_instance_id TEXT NOT NULL REFERENCES screen_instances(id) ON DELETE CASCADE,
  event_type TEXT NOT NULL,
  start_frame INTEGER NOT NULL CHECK (start_frame >= 0),
  duration_frames INTEGER NOT NULL CHECK (duration_frames >= 0),
  target_id TEXT,
  animation_preset_id TEXT REFERENCES animation_presets(id) ON DELETE SET NULL,
  payload_json TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_shots_production ON shots(production_id, sort_order);
CREATE INDEX IF NOT EXISTS idx_episodes_production ON episodes(production_id, sort_order);
CREATE INDEX IF NOT EXISTS idx_icon_themes_production ON icon_themes(production_id, name);
CREATE INDEX IF NOT EXISTS idx_production_fonts_lookup ON production_fonts(production_id, family);
CREATE INDEX IF NOT EXISTS idx_palette_colors_lookup ON palette_colors(production_id, token);
CREATE INDEX IF NOT EXISTS idx_status_bars_production ON status_bars(production_id, name);
CREATE INDEX IF NOT EXISTS idx_navigation_bars_production ON navigation_bars(production_id, name);
CREATE INDEX IF NOT EXISTS idx_component_classes_lookup ON component_classes(production_id, component_type, name);
CREATE INDEX IF NOT EXISTS idx_module_theme_configs_lookup ON module_theme_configs(theme_id, app_id, module_id, module_schema_version);
CREATE INDEX IF NOT EXISTS idx_screen_instances_shot ON screen_instances(shot_id, layer_order);
CREATE INDEX IF NOT EXISTS idx_module_instances_screen ON module_instances(screen_instance_id, sort_order, id);
CREATE INDEX IF NOT EXISTS idx_screen_events_instance ON screen_events(screen_instance_id, start_frame);
CREATE INDEX IF NOT EXISTS idx_conversation_participants_conversation ON conversation_participants(conversation_id, sort_order);
CREATE INDEX IF NOT EXISTS idx_messages_conversation ON messages(conversation_id, sort_order);

PRAGMA user_version = 13;
