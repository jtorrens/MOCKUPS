import type { FieldDescriptor } from "./types.js";

export function descriptorKey(storagePath: FieldDescriptor["storagePath"]) {
  return storagePath
    .map((segment) => (typeof segment === "number" ? "[]" : segment))
    .join(".");
}

export function descriptorsToHints(descriptors: FieldDescriptor[]) {
  return Object.fromEntries(
    descriptors.map((descriptor) => [
      descriptorKey(descriptor.storagePath),
      {
        canonicalPath: descriptor.canonicalPath,
        storagePath: descriptor.storagePath,
        label: descriptor.label,
        section: descriptor.section,
        area: descriptor.area,
        group: descriptor.group,
        role: descriptor.role,
        property: descriptor.property,
        widget: descriptor.widget,
        options: descriptor.options,
        min: descriptor.min,
        max: descriptor.max,
        step: descriptor.step,
        summaryKeys: descriptor.summaryKeys,
      },
    ]),
  );
}
