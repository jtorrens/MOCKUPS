import {
  APP_CONFIG_BINDINGS,
  APP_METADATA_BINDINGS,
} from "../../domain/fields/appFields.js";
import { TokenOverrideEditor } from "../components/json-editor/TokenOverrideEditor.js";
import { jsonUiHintsFromFieldBindings } from "../components/json-editor/fieldDefinitionHints.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";

interface AppNativeBridge {
  pickFile?: () => Promise<string[]>;
}

interface AppIconFieldsProps {
  metadataRoot: Record<string, unknown>;
  mediaRoot: string;
  nativeBridge: AppNativeBridge | undefined;
  onMetadataRootChange: (nextRoot: JsonValue) => void;
}

const appIconHints = jsonUiHintsFromFieldBindings(
  APP_METADATA_BINDINGS.filter((binding) => binding.outputPath[0] === "icon"),
);

const appWallpaperHints = jsonUiHintsFromFieldBindings(APP_CONFIG_BINDINGS);

export function AppIconFields({
  metadataRoot,
  mediaRoot,
  nativeBridge,
  onMetadataRootChange,
}: AppIconFieldsProps) {
  return (
    <TokenOverrideEditor
      rootValue={metadataRoot as JsonValue}
      inheritedRoot={{}}
      hints={appIconHints}
      inlineSingleGroup
      mediaRoot={mediaRoot}
      nativeBridge={nativeBridge}
      onRootChange={onMetadataRootChange}
    />
  );
}

interface AppWallpaperEditorProps {
  appTokenRoot: Record<string, unknown>;
  mediaRoot: string;
  nativeBridge: AppNativeBridge | undefined;
  paletteCatalog?: PaletteColorCatalog;
  onTokenRootChange: (nextValue: JsonValue) => void;
}

export function AppWallpaperEditor({
  appTokenRoot,
  mediaRoot,
  nativeBridge,
  paletteCatalog,
  onTokenRootChange,
}: AppWallpaperEditorProps) {
  return (
    <div className="record-editor-field-stack record-editor-single-column wallpaper-editor">
      <TokenOverrideEditor
        rootValue={appTokenRoot as JsonValue}
        inheritedRoot={{}}
        hints={appWallpaperHints}
        inlineSingleGroup
        mediaRoot={mediaRoot}
        nativeBridge={nativeBridge}
        paletteCatalog={paletteCatalog}
        onRootChange={onTokenRootChange}
      />
    </div>
  );
}
