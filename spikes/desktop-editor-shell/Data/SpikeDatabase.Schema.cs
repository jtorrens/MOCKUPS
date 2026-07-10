namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    internal const string SchemaSql =
        """
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS projects (
          id TEXT PRIMARY KEY,
          name TEXT NOT NULL,
          slug TEXT NOT NULL DEFAULT '',
          default_fps INTEGER NOT NULL DEFAULT 25,
          notes TEXT NOT NULL DEFAULT '',
          media_root TEXT NOT NULL DEFAULT '',
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS episodes (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          slug TEXT NOT NULL DEFAULT '',
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS shots (
          id TEXT PRIMARY KEY,
          episode_id TEXT NOT NULL REFERENCES episodes(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          slug TEXT NOT NULL DEFAULT '',
          version INTEGER NOT NULL DEFAULT 1,
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          fps_override INTEGER,
          duration_frames INTEGER NOT NULL DEFAULT 240,
          owner_actor_id TEXT NOT NULL DEFAULT '',
          render_preset_id TEXT NOT NULL DEFAULT '',
          canvas_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS apps (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          record_class_id TEXT NOT NULL,
          name TEXT NOT NULL,
          bundle_key TEXT NOT NULL DEFAULT '',
          app_type TEXT NOT NULL DEFAULT 'chat',
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          config_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS modules (
          id TEXT PRIMARY KEY,
          app_id TEXT NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
          record_class_id TEXT NOT NULL,
          name TEXT NOT NULL,
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          config_json TEXT NOT NULL DEFAULT '{}',
          design_preview_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS module_instances (
          id TEXT PRIMARY KEY,
          shot_id TEXT NOT NULL REFERENCES shots(id) ON DELETE CASCADE,
          app_id TEXT NOT NULL REFERENCES apps(id) ON DELETE RESTRICT,
          module_id TEXT NOT NULL REFERENCES modules(id) ON DELETE RESTRICT,
          name TEXT NOT NULL,
          notes TEXT NOT NULL DEFAULT '',
          sort_order INTEGER NOT NULL DEFAULT 0,
          duration_frames INTEGER NOT NULL DEFAULT 240,
          transition_json TEXT NOT NULL DEFAULT '{"type":"cut"}',
          content_json TEXT NOT NULL DEFAULT '{}',
          behavior_json TEXT NOT NULL DEFAULT '{}',
          animation_json TEXT NOT NULL DEFAULT '{"schemaVersion":1,"tracks":[]}',
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE INDEX IF NOT EXISTS idx_module_instances_shot
          ON module_instances(shot_id, sort_order, id);

        CREATE TABLE IF NOT EXISTS palette_colors (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          token TEXT NOT NULL,
          value_hex TEXT NOT NULL,
          metadata_json TEXT NOT NULL DEFAULT '{}',
          is_neutral INTEGER NOT NULL DEFAULT 0,
          UNIQUE(project_id, token)
        );

        CREATE TABLE IF NOT EXISTS devices (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          manufacturer TEXT NOT NULL DEFAULT '',
          model TEXT NOT NULL DEFAULT '',
          os_family TEXT NOT NULL DEFAULT '',
          metrics_json TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS actors (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          display_name TEXT NOT NULL,
          short_name TEXT NOT NULL DEFAULT '',
          default_device_id TEXT NOT NULL DEFAULT '',
          default_theme_id TEXT NOT NULL DEFAULT '',
          metadata_json TEXT NOT NULL DEFAULT '{}'
        );

        CREATE TABLE IF NOT EXISTS production_fonts (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          family_name TEXT NOT NULL,
          category TEXT NOT NULL DEFAULT 'text',
          source_directory TEXT NOT NULL DEFAULT '',
          files_json TEXT NOT NULL DEFAULT '[]',
          metadata_json TEXT NOT NULL DEFAULT '{}',
          UNIQUE(project_id, family_name)
        );

        CREATE TABLE IF NOT EXISTS icon_themes (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          asset_root TEXT NOT NULL DEFAULT '',
          mapping_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}',
          UNIQUE(project_id, name)
        );

        CREATE TABLE IF NOT EXISTS render_presets (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          width INTEGER NOT NULL DEFAULT 1080,
          height INTEGER NOT NULL DEFAULT 1920,
          fps INTEGER NOT NULL DEFAULT 25,
          format TEXT NOT NULL DEFAULT 'mov',
          codec_json TEXT NOT NULL DEFAULT '{}',
          color_json TEXT NOT NULL DEFAULT '{}',
          quality_json TEXT NOT NULL DEFAULT '{}',
          export_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}',
          UNIQUE(project_id, name)
        );

        CREATE TABLE IF NOT EXISTS component_classes (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          component_type TEXT NOT NULL,
          record_class_id TEXT NOT NULL,
          name TEXT NOT NULL,
          notes TEXT NOT NULL DEFAULT '',
          config_json TEXT NOT NULL DEFAULT '{}',
          design_preview_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}',
          UNIQUE(project_id, component_type, name)
        );

        CREATE TABLE IF NOT EXISTS themes (
          id TEXT PRIMARY KEY,
          project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
          name TEXT NOT NULL,
          family TEXT NOT NULL DEFAULT 'ios',
          icon_theme_id TEXT NOT NULL DEFAULT '',
          status_bar_id TEXT NOT NULL DEFAULT '',
          navigation_bar_id TEXT NOT NULL DEFAULT '',
          tokens_json TEXT NOT NULL DEFAULT '{}',
          metadata_json TEXT NOT NULL DEFAULT '{}',
          UNIQUE(project_id, name)
        );

        CREATE TABLE IF NOT EXISTS editor_layouts (
          record_class_id TEXT PRIMARY KEY,
          layout_json TEXT NOT NULL
        );

        PRAGMA user_version = 1;
        """;

}
