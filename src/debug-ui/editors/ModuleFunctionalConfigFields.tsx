import {
  isJsonObject,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import { KeyboardBehaviorFields } from "./module-behavior/KeyboardBehaviorFields.js";
import { ModuleBehaviorCard } from "./module-behavior/ModuleBehaviorCard.js";
import { TextInputBarBehaviorFields } from "./module-behavior/TextInputBarBehaviorFields.js";
import { useSessionStoredState } from "../editor-ui/useSessionStoredState.js";
import { parsedObject } from "./recordJsonUtils.js";

function stringifyJson(value: unknown): string {
  return JSON.stringify(value ?? {}, null, 2);
}

interface ModuleFunctionalConfigFieldsProps {
  rawValue: string;
  sessionKey: string;
  onRawChange: (nextRaw: string) => void;
}

export function ModuleFunctionalConfigFields({
  rawValue,
  sessionKey,
  onRawChange,
}: ModuleFunctionalConfigFieldsProps) {
  const [openCards, setOpenCards] = useSessionStoredState<
    Record<string, boolean>
  >(`${sessionKey}:openCards`, {
    textInputBar: true,
    keyboard: false,
  });
  const root = parsedObject(rawValue);
  const keyboardRoot = isJsonObject(root.keyboard as JsonValue)
    ? (root.keyboard as Record<string, JsonValue>)
    : {};
  const textInputBarRoot = isJsonObject(root.textInputBar as JsonValue)
    ? (root.textInputBar as Record<string, JsonValue>)
    : {};

  function updateConfigValue(path: JsonPath, nextValue: JsonValue) {
    onRawChange(stringifyJson(setAtPath(root as JsonValue, path, nextValue)));
  }

  function toggleCard(card: string) {
    setOpenCards((current) => {
      const nextOpen = !(current[card] ?? false);
      return {
        textInputBar: false,
        keyboard: false,
        [card]: nextOpen,
      };
    });
  }

  return (
    <>
      <ModuleBehaviorCard
        title="Text input bar"
        summary="Composer placeholder, cursor and state-specific side icons"
        icon="⌨"
        open={openCards.textInputBar ?? false}
        onToggle={() => toggleCard("textInputBar")}
      >
        <TextInputBarBehaviorFields
          textInputBarRoot={textInputBarRoot}
          showTextInputBar
          showVisibilityToggle={false}
          updateBehaviorValue={updateConfigValue}
        />
      </ModuleBehaviorCard>
      <ModuleBehaviorCard
        title="Keyboard"
        summary="Language and bottom utility icons"
        icon="⌨"
        open={openCards.keyboard ?? false}
        onToggle={() => toggleCard("keyboard")}
      >
        <KeyboardBehaviorFields
          keyboardRoot={keyboardRoot}
          showKeyboard
          showModeField={false}
          showVisibilityToggle={false}
          updateBehaviorValue={updateConfigValue}
        />
      </ModuleBehaviorCard>
    </>
  );
}
