import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

const GROUPS = {
  transform: { id: "transform", label: "Transform" },
  transition: { id: "transition", label: "Transition" },
  deviceState: { id: "deviceState", label: "Device state" },
} as const;

export const SCREEN_INSTANCE_FIELDS = defineFields({
  id: {
    id: "screenInstance.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  shotId: {
    id: "screenInstance.shotId",
    kind: "recordReference",
    ui: {
      label: "Shot",
      tableId: "shots",
      labelColumn: "name",
    },
  },
  appId: {
    id: "screenInstance.appId",
    kind: "recordReference",
    ui: {
      label: "App",
      tableId: "apps",
      labelColumn: "name",
    },
  },
  screenType: {
    id: "screenInstance.screenType",
    kind: "text",
    ui: {
      label: "Screen type",
    },
  },
  moduleId: {
    id: "screenInstance.moduleId",
    kind: "text",
    ui: {
      label: "Module ID",
    },
  },
  moduleSchemaVersion: {
    id: "screenInstance.moduleSchemaVersion",
    kind: "integer",
    ui: {
      label: "Module schema version",
      min: 1,
      step: 1,
    },
  },
  deviceStateId: {
    id: "screenInstance.deviceStateId",
    kind: "recordReference",
    ui: {
      label: "Device state",
      tableId: "device_states",
      labelColumn: "name",
      allowEmpty: true,
    },
  },
  deviceState: {
    id: "screenInstance.deviceState",
    kind: "jsonObject",
    ui: {
      label: "Device state",
    },
  },
  themeMode: {
    id: "screenInstance.themeMode",
    kind: "enum",
    ui: {
      label: "Theme mode",
      options: ["light", "dark"],
    },
  },
  durationFrames: {
    id: "screenInstance.durationFrames",
    kind: "integer",
    defaultValue: 1,
    ui: {
      label: "Duration frames",
      min: 1,
      step: 1,
    },
  },
  transform: {
    id: "screenInstance.transform",
    kind: "jsonObject",
    ui: {
      label: "Screen transform",
    },
  },
  transformX: {
    id: "screenInstance.transform.x",
    kind: "decimal",
    defaultValue: 0,
    ui: {
      label: "X position",
      group: GROUPS.transform,
      step: "any",
    },
  },
  transformY: {
    id: "screenInstance.transform.y",
    kind: "decimal",
    defaultValue: 0,
    ui: {
      label: "Y position",
      group: GROUPS.transform,
      step: "any",
    },
  },
  transformScale: {
    id: "screenInstance.transform.scale",
    kind: "decimal",
    defaultValue: 1,
    ui: {
      label: "Scale",
      group: GROUPS.transform,
      min: 0,
      step: 0.01,
    },
  },
  transformRotationDegrees: {
    id: "screenInstance.transform.rotationDegrees",
    kind: "decimal",
    defaultValue: 0,
    ui: {
      label: "Rotation",
      group: GROUPS.transform,
      step: "any",
    },
  },
  transition: {
    id: "screenInstance.transition",
    kind: "jsonObject",
    ui: {
      label: "Transition",
    },
  },
  transitionType: {
    id: "screenInstance.transition.type",
    kind: "enum",
    defaultValue: "none",
    ui: {
      label: "Transition",
      group: GROUPS.transition,
      options: ["none", "overlay", "dissolve"],
    },
  },
  transitionDurationFrames: {
    id: "screenInstance.transition.durationFrames",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Duration frames",
      description: "Starts before this screen",
      group: GROUPS.transition,
      min: 0,
      step: 1,
    },
  },
  orientation: {
    id: "screenInstance.deviceState.orientation",
    kind: "enum",
    defaultValue: "portrait",
    ui: {
      label: "Orientation",
      group: GROUPS.deviceState,
      options: ["portrait", "landscape"],
    },
  },
  time: {
    id: "screenInstance.deviceState.time",
    kind: "text",
    ui: {
      label: "Time",
      group: GROUPS.deviceState,
    },
  },
  batteryLevel: {
    id: "screenInstance.deviceState.batteryLevel",
    kind: "alpha",
    defaultValue: 1,
    ui: {
      label: "Battery level",
      group: GROUPS.deviceState,
      step: 0.01,
    },
  },
  batteryCharging: {
    id: "screenInstance.deviceState.batteryCharging",
    kind: "boolean",
    defaultValue: false,
    ui: {
      label: "Battery charging",
      group: GROUPS.deviceState,
    },
  },
  signalBars: {
    id: "screenInstance.deviceState.signalBars",
    kind: "integer",
    defaultValue: 4,
    ui: {
      label: "Signal bars",
      group: GROUPS.deviceState,
      min: 0,
      max: 4,
      step: 1,
    },
  },
  networkLabel: {
    id: "screenInstance.deviceState.networkLabel",
    kind: "text",
    ui: {
      label: "Network label",
      group: GROUPS.deviceState,
      allowEmpty: true,
    },
  },
  wifiEnabled: {
    id: "screenInstance.deviceState.wifiEnabled",
    kind: "boolean",
    defaultValue: true,
    ui: {
      label: "Wi-Fi enabled",
      group: GROUPS.deviceState,
    },
  },
  wifiIconState: {
    id: "screenInstance.deviceState.wifiIconState",
    kind: "text",
    ui: {
      label: "Wi-Fi icon state",
      group: GROUPS.deviceState,
      allowEmpty: true,
    },
  },
  locked: {
    id: "screenInstance.deviceState.locked",
    kind: "boolean",
    defaultValue: false,
    ui: {
      label: "Locked",
      group: GROUPS.deviceState,
    },
  },
});

export const SCREEN_INSTANCE_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: SCREEN_INSTANCE_FIELDS.id },
  { outputPath: ["shot_id"], field: SCREEN_INSTANCE_FIELDS.shotId },
  { outputPath: ["app_id"], field: SCREEN_INSTANCE_FIELDS.appId },
  { outputPath: ["screen_type"], field: SCREEN_INSTANCE_FIELDS.screenType },
  { outputPath: ["module_id"], field: SCREEN_INSTANCE_FIELDS.moduleId },
  {
    outputPath: ["module_schema_version"],
    field: SCREEN_INSTANCE_FIELDS.moduleSchemaVersion,
  },
  { outputPath: ["device_state_id"], field: SCREEN_INSTANCE_FIELDS.deviceStateId },
  { outputPath: ["device_state_json"], field: SCREEN_INSTANCE_FIELDS.deviceState },
  { outputPath: ["theme_mode"], field: SCREEN_INSTANCE_FIELDS.themeMode },
  { outputPath: ["duration_frames"], field: SCREEN_INSTANCE_FIELDS.durationFrames },
  { outputPath: ["transform_json"], field: SCREEN_INSTANCE_FIELDS.transform },
  { outputPath: ["transition_in_json"], field: SCREEN_INSTANCE_FIELDS.transition },
] satisfies readonly JsonFieldBinding[];

export const SCREEN_INSTANCE_TRANSFORM_BINDINGS = [
  { outputPath: ["x"], field: SCREEN_INSTANCE_FIELDS.transformX },
  { outputPath: ["y"], field: SCREEN_INSTANCE_FIELDS.transformY },
  { outputPath: ["scale"], field: SCREEN_INSTANCE_FIELDS.transformScale },
  {
    outputPath: ["rotation_degrees"],
    field: SCREEN_INSTANCE_FIELDS.transformRotationDegrees,
  },
] satisfies readonly JsonFieldBinding[];

export const SCREEN_INSTANCE_TRANSITION_BINDINGS = [
  { outputPath: ["type"], field: SCREEN_INSTANCE_FIELDS.transitionType },
  {
    outputPath: ["durationFrames"],
    field: SCREEN_INSTANCE_FIELDS.transitionDurationFrames,
  },
] satisfies readonly JsonFieldBinding[];

export const SCREEN_INSTANCE_DEVICE_STATE_BINDINGS = [
  { outputPath: ["orientation"], field: SCREEN_INSTANCE_FIELDS.orientation },
  { outputPath: ["time"], field: SCREEN_INSTANCE_FIELDS.time },
  { outputPath: ["batteryLevel"], field: SCREEN_INSTANCE_FIELDS.batteryLevel },
  {
    outputPath: ["batteryCharging"],
    field: SCREEN_INSTANCE_FIELDS.batteryCharging,
  },
  { outputPath: ["signalBars"], field: SCREEN_INSTANCE_FIELDS.signalBars },
  { outputPath: ["networkLabel"], field: SCREEN_INSTANCE_FIELDS.networkLabel },
  { outputPath: ["wifiEnabled"], field: SCREEN_INSTANCE_FIELDS.wifiEnabled },
  { outputPath: ["wifiIconState"], field: SCREEN_INSTANCE_FIELDS.wifiIconState },
  { outputPath: ["locked"], field: SCREEN_INSTANCE_FIELDS.locked },
] satisfies readonly JsonFieldBinding[];
