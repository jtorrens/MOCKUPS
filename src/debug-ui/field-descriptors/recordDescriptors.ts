import type { FieldDescriptor } from "./types.js";

export const appRecordDescriptors: FieldDescriptor[] = [
  {
    canonicalPath: "app.general.id",
    storagePath: ["id"],
    label: "ID",
    section: "General",
    area: "general",
    property: "id",
  },
  {
    canonicalPath: "app.general.name",
    storagePath: ["name"],
    label: "Name",
    section: "General",
    area: "general",
    property: "name",
  },
];

export const moduleThemeConfigRecordDescriptors: FieldDescriptor[] = [
  {
    canonicalPath: "module.general.id",
    storagePath: ["id"],
    label: "ID",
    section: "Settings",
    area: "settings",
    property: "id",
  },
  {
    canonicalPath: "module.general.moduleId",
    storagePath: ["module_id"],
    label: "Module ID",
    section: "Settings",
    area: "settings",
    property: "moduleId",
  },
  {
    canonicalPath: "module.general.schemaVersion",
    storagePath: ["module_schema_version"],
    label: "Schema version",
    section: "Settings",
    area: "settings",
    property: "schemaVersion",
    widget: "number",
  },
  {
    canonicalPath: "module.general.name",
    storagePath: ["name"],
    label: "Name",
    section: "Settings",
    area: "settings",
    property: "name",
  },
];

export const moduleInstanceRecordDescriptors: FieldDescriptor[] = [
  {
    canonicalPath: "moduleInstance.general.id",
    storagePath: ["id"],
    label: "ID",
    section: "General",
    area: "general",
    property: "id",
  },
  {
    canonicalPath: "moduleInstance.general.screenInstanceId",
    storagePath: ["screen_instance_id"],
    label: "Screen instance",
    section: "General",
    area: "general",
    property: "screenInstanceId",
  },
  {
    canonicalPath: "moduleInstance.general.moduleId",
    storagePath: ["module_id"],
    label: "Module ID",
    section: "General",
    area: "general",
    property: "moduleId",
  },
];

export const screenInstanceRecordDescriptors: FieldDescriptor[] = [
  {
    canonicalPath: "screen.general.id",
    storagePath: ["id"],
    label: "ID",
    section: "General",
    area: "general",
    property: "id",
  },
  {
    canonicalPath: "screen.general.appId",
    storagePath: ["app_id"],
    label: "App",
    section: "General",
    area: "general",
    property: "appId",
  },
  {
    canonicalPath: "screen.general.moduleId",
    storagePath: ["module_id"],
    label: "Module ID",
    section: "General",
    area: "general",
    property: "moduleId",
  },
  {
    canonicalPath: "screen.timeline.startFrame",
    storagePath: ["start_frame"],
    label: "Start frame",
    section: "Timeline",
    area: "timeline",
    property: "startFrame",
    widget: "number",
  },
  {
    canonicalPath: "screen.timeline.endFrame",
    storagePath: ["end_frame"],
    label: "End frame",
    section: "Timeline",
    area: "timeline",
    property: "endFrame",
    widget: "number",
  },
];
