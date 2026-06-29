import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

const GROUPS = {
  layout: { id: "layout", label: "Layout" },
} as const;

export const NAVIGATION_BAR_FIELDS = defineFields({
  id: {
    id: "navigationBar.id",
    kind: "text",
    ui: {
      label: "ID",
    },
  },
  productionId: {
    id: "navigationBar.productionId",
    kind: "recordReference",
    ui: {
      label: "Production",
      tableId: "productions",
      labelColumn: "name",
    },
  },
  name: {
    id: "navigationBar.name",
    kind: "text",
    ui: {
      label: "Name",
    },
  },
  family: {
    id: "navigationBar.family",
    kind: "text",
    defaultValue: "android",
    ui: {
      label: "Family",
    },
  },
  config: {
    id: "navigationBar.config",
    kind: "jsonObject",
    ui: {
      label: "Navigation bar config",
    },
  },
  metadata: {
    id: "navigationBar.metadata",
    kind: "jsonObject",
    ui: {
      label: "Navigation bar notes",
    },
  },
  layoutHeight: {
    id: "navigationBar.layout.height",
    kind: "integer",
    defaultValue: 34,
    ui: {
      label: "Height",
      group: GROUPS.layout,
      min: 0,
      step: 1,
    },
  },
  layoutItemSize: {
    id: "navigationBar.layout.itemSize",
    kind: "integer",
    defaultValue: 18,
    ui: {
      label: "Item size",
      group: GROUPS.layout,
      min: 0,
      step: 1,
    },
  },
  layoutSidePadding: {
    id: "navigationBar.layout.sidePadding",
    kind: "integer",
    defaultValue: 40,
    ui: {
      label: "Side padding",
      group: GROUPS.layout,
      min: 0,
      step: 1,
    },
  },
  layoutStrokeWidth: {
    id: "navigationBar.layout.strokeWidth",
    kind: "decimal",
    defaultValue: 2,
    ui: {
      label: "Stroke width",
      group: GROUPS.layout,
      min: 0,
      step: 0.1,
    },
  },
  layoutCornerRadius: {
    id: "navigationBar.layout.cornerRadius",
    kind: "integer",
    defaultValue: 3,
    ui: {
      label: "Corner radius",
      group: GROUPS.layout,
      min: 0,
      step: 1,
    },
  },
  layoutFilled: {
    id: "navigationBar.layout.filled",
    kind: "boolean",
    defaultValue: false,
    ui: {
      label: "Filled",
      group: GROUPS.layout,
    },
  },
  itemZone: {
    id: "navigationBar.item.zone",
    kind: "enum",
    defaultValue: "off",
    ui: {
      label: "Zone",
      options: ["off", "left", "center", "right"],
    },
  },
  itemOrder: {
    id: "navigationBar.item.order",
    kind: "integer",
    defaultValue: 0,
    ui: {
      label: "Order",
      step: 1,
    },
  },
});

export const NAVIGATION_BAR_COLUMN_BINDINGS = [
  { outputPath: ["id"], field: NAVIGATION_BAR_FIELDS.id },
  { outputPath: ["production_id"], field: NAVIGATION_BAR_FIELDS.productionId },
  { outputPath: ["name"], field: NAVIGATION_BAR_FIELDS.name },
  { outputPath: ["family"], field: NAVIGATION_BAR_FIELDS.family },
  { outputPath: ["config_json"], field: NAVIGATION_BAR_FIELDS.config },
  { outputPath: ["metadata_json"], field: NAVIGATION_BAR_FIELDS.metadata },
] satisfies readonly JsonFieldBinding[];

export const NAVIGATION_BAR_CONFIG_BINDINGS: readonly JsonFieldBinding[] = [
  { outputPath: ["layout", "height"], field: NAVIGATION_BAR_FIELDS.layoutHeight },
  {
    outputPath: ["layout", "itemSize"],
    field: NAVIGATION_BAR_FIELDS.layoutItemSize,
  },
  {
    outputPath: ["layout", "sidePadding"],
    field: NAVIGATION_BAR_FIELDS.layoutSidePadding,
  },
  {
    outputPath: ["layout", "strokeWidth"],
    field: NAVIGATION_BAR_FIELDS.layoutStrokeWidth,
  },
  {
    outputPath: ["layout", "cornerRadius"],
    field: NAVIGATION_BAR_FIELDS.layoutCornerRadius,
  },
  { outputPath: ["layout", "filled"], field: NAVIGATION_BAR_FIELDS.layoutFilled },
];
