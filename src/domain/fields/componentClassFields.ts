import {
  defineFields,
  type JsonFieldBinding,
} from "../value-system/index.js";

export const COMPONENT_CLASS_FIELDS = defineFields({
  name: {
    id: "componentClass.name",
    kind: "text",
    ui: {
      label: "Name",
    },
  },
  componentType: {
    id: "componentClass.componentType",
    kind: "enum",
    ui: {
      label: "Component type",
      options: [
        "avatar",
        "audio",
        "buttonIcon",
        "cursor",
        "iconRow",
        "keyboard",
        "label",
        "navigation_bar",
        "status_bar",
        "surface",
        "textBox",
        "textInputBar",
        "video",
      ],
    },
  },
});

export const COMPONENT_CLASS_COLUMN_BINDINGS: readonly JsonFieldBinding[] = [
  {
    outputPath: ["name"],
    field: COMPONENT_CLASS_FIELDS.name,
  },
  {
    outputPath: ["component_type"],
    field: COMPONENT_CLASS_FIELDS.componentType,
  },
];
