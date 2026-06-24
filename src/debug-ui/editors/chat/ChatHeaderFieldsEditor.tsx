import { DeferredTextInput } from "../../editor-ui/DeferredTextInput.js";
import {
  InspectorFieldRow,
  InspectorRestoreButton,
} from "../../components/inspector/InspectorFieldRow.js";
import type { JsonValue } from "../../components/json-editor/jsonEditorUtils.js";

interface ChatHeaderFieldsEditorProps {
  header: Record<string, JsonValue>;
  inheritedTitle: string;
  onChange: (key: "title" | "subtitle", value: string) => void;
}

export function ChatHeaderFieldsEditor({
  header,
  inheritedTitle,
  onChange,
}: ChatHeaderFieldsEditorProps) {
  const title = String(header.title ?? inheritedTitle ?? "");
  const hasTitleOverride = Boolean(inheritedTitle) && title !== inheritedTitle;

  return (
    <div className="record-editor-content-fields">
      <InspectorFieldRow
        className={`record-editor-content-field-row ${
          hasTitleOverride ? "json-override" : ""
        }`}
        state={hasTitleOverride ? "override" : "default"}
        label={<span>Title</span>}
        meta={inheritedTitle ? <code>{`User: ${inheritedTitle}`}</code> : null}
        control={
          <DeferredTextInput
            value={title}
            onCommit={(nextValue) => onChange("title", nextValue)}
          />
        }
        restore={
          hasTitleOverride ? (
            <InspectorRestoreButton
              label="Restore user title"
              onClick={() => onChange("title", inheritedTitle)}
            />
          ) : null
        }
      />
      <InspectorFieldRow
        className="record-editor-content-field-row"
        label={<span>Subtitle</span>}
        control={
          <DeferredTextInput
            value={String(header.subtitle ?? "")}
            onCommit={(nextValue) => onChange("subtitle", nextValue)}
          />
        }
      />
    </div>
  );
}
