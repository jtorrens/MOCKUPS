import { z } from "zod";

const IconThemeManifestIconSchema = z.object({
  semanticName: z.string().min(1),
  materialName: z.string().min(1).optional(),
  file: z.string().min(1),
  source: z.string().min(1).optional(),
});

export const IconThemeManifestSchema = z.object({
  name: z.string().min(1),
  source: z.string().min(1).optional(),
  style: z.string().min(1).optional(),
  weight: z.number().optional(),
  generatedAt: z.string().optional(),
  count: z.number().optional(),
  icons: z.array(IconThemeManifestIconSchema),
});

export type IconThemeManifest = z.infer<typeof IconThemeManifestSchema>;

function categoryForSemanticName(semanticName: string): string {
  const [category] = semanticName.split("_");
  return category || "misc";
}

export function buildIconThemeMapping(manifest: IconThemeManifest) {
  const tokens: Record<
    string,
    {
      category: string;
      file: string;
      materialName?: string;
      source?: string;
    }
  > = {};
  const categories: Record<string, string[]> = {};

  for (const icon of manifest.icons) {
    const category = categoryForSemanticName(icon.semanticName);
    tokens[icon.semanticName] = {
      category,
      file: icon.file,
      ...(icon.materialName ? { materialName: icon.materialName } : {}),
      ...(icon.source ? { source: icon.source } : {}),
    };
    categories[category] = [...(categories[category] ?? []), icon.semanticName];
  }

  return {
    schemaVersion: 1,
    source: {
      name: manifest.name,
      ...(manifest.source ? { package: manifest.source } : {}),
      ...(manifest.style ? { style: manifest.style } : {}),
      ...(manifest.weight ? { weight: manifest.weight } : {}),
      ...(manifest.generatedAt ? { generatedAt: manifest.generatedAt } : {}),
    },
    tokens,
    categories,
  };
}
