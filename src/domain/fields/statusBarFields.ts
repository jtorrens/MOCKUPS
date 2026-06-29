import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

const GROUPS = {
  layout: { id: "layout", label: "Layout" },
} as const;

export const STATUS_BAR_FIELDS = defineFields({
  id: {
    id: "statusBar.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  productionId: {
    id: "statusBar.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  name: {
    id: "statusBar.name",
    kind: "text",
    ui: {
      label: "Name",
    },
  },
  family: {
    id: "statusBar.family",
    kind: "text",
    defaultValue: "ios",
    ui: {
      label: "Family",
    },
  },
  layoutHeight: {
    id: "statusBar.layout.height",
    kind: "integer",
    defaultValue: 54,
    ui: {
      label: "Height",
      group: GROUPS.layout,
      min: 0,
      step: 1,
    },
  },
  layoutItemSize: {
    id: "statusBar.layout.itemSize",
    kind: "integer",
    defaultValue: 18,
    ui: {
      label: "Item size",
      group: GROUPS.layout,
      min: 0,
      step: 1,
    },
  },
  layoutGap: {
    id: "statusBar.layout.gap",
    kind: "integer",
    defaultValue: 6,
    ui: {
      label: "Gap",
      group: GROUPS.layout,
      min: 0,
      step: 1,
    },
  },
  layoutSidePadding: {
    id: "statusBar.layout.sidePadding",
    kind: "integer",
    defaultValue: 24,
    ui: {
      label: "Side padding",
      group: GROUPS.layout,
      min: 0,
      step: 1,
    },
  },
  itemTextValue: {
    id: "statusBar.item.value.text",
    kind: "text",
    ui: {
      label: "Value",
    },
  },
  itemIconToken: {
    id: "statusBar.item.iconToken",
    kind: "iconToken",
    ui: {
      label: "Token",
      allowEmpty: true,
    },
  },
  itemBatteryValue: {
    id: "statusBar.item.value.battery",
    kind: "integer",
    defaultValue: 85,
    ui: {
      label: "Battery",
      min: 0,
      max: 100,
      step: 1,
    },
  },
  itemSignalValue: {
    id: "statusBar.item.value.signal",
    kind: "integer",
    defaultValue: 4,
    ui: {
      label: "Signal",
      min: 0,
      max: 4,
      step: 1,
    },
  },
  itemCharging: {
    id: "statusBar.item.charging",
    kind: "boolean",
    defaultValue: false,
    ui: {
      label: "Charging",
    },
  },
  itemZone: {
    id: "statusBar.item.zone",
    kind: "enum",
    defaultValue: "off",
    ui: {
      label: "Zone",
      options: ["off", "left", "right"],
    },
  },
  itemOrder: {
    id: "statusBar.item.order",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Order",
      step: 1,
    },
  },
});

export const STATUS_BAR_COLUMN_BINDINGS: readonly JsonFieldBinding[] = [
  { outputPath: ["id"], field: STATUS_BAR_FIELDS.id },
  { outputPath: ["production_id"], field: STATUS_BAR_FIELDS.productionId },
  { outputPath: ["name"], field: STATUS_BAR_FIELDS.name },
  { outputPath: ["family"], field: STATUS_BAR_FIELDS.family },
];

export const STATUS_BAR_CONFIG_BINDINGS: readonly JsonFieldBinding[] = [
  { outputPath: ["layout", "height"], field: STATUS_BAR_FIELDS.layoutHeight },
  { outputPath: ["layout", "itemSize"], field: STATUS_BAR_FIELDS.layoutItemSize },
  { outputPath: ["layout", "gap"], field: STATUS_BAR_FIELDS.layoutGap },
  {
    outputPath: ["layout", "sidePadding"],
    field: STATUS_BAR_FIELDS.layoutSidePadding,
  },
];
