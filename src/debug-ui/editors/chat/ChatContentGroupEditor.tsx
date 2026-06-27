import type { AppRecord } from "../../api/client.js";
import {
  cloneJson,
  defaultJsonValue,
  isJsonObject,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "../../components/json-editor/jsonEditorUtils.js";
import type { JsonUiHints } from "../../components/json-editor/uiHints.js";
import { friendlyGroupLabel } from "../../components/json-editor/labels.js";
import { ChatContentArrayEditor } from "./ChatContentArrayEditor.js";
import { ChatHeaderFieldsEditor } from "./ChatHeaderFieldsEditor.js";
import { ChatMessageFieldsEditor } from "./ChatMessageFieldsEditor.js";
import { ChatNestedValueEditor } from "./ChatNestedValueEditor.js";
import {
  actorOptions,
  defaultMessageItem,
  mediaNumberFieldsForMessage,
  messageWithDirection,
  messageWithMediaPath,
  messageWithMediaType,
  messageWithTextRevealMode,
} from "./chatContentModel.js";

interface ChatContentGroupEditorProps {
  actors: AppRecord[];
  groupKey: string;
  groupValue: JsonValue;
  hints: JsonUiHints;
  openItems: Record<string, boolean>;
  recordId?: unknown;
  canBrowseMedia: boolean;
  mediaRoot: string;
  productionId: string;
  normalizeMediaPath: (filePath: string) => string;
  onBrowseMedia: () => Promise<string | undefined>;
  onGroupValueChange: (value: JsonValue) => void;
  onToggleItem: (groupKey: string, openKey: string, isOpen: boolean) => void;
}

export function ChatContentGroupEditor({
  actors,
  groupKey,
  groupValue,
  hints,
  openItems,
  recordId,
  canBrowseMedia,
  mediaRoot,
  productionId,
  normalizeMediaPath,
  onBrowseMedia,
  onGroupValueChange,
  onToggleItem,
}: ChatContentGroupEditorProps) {
  function updateAtPath(path: JsonPath, nextValue: JsonValue) {
    onGroupValueChange(setAtPath(groupValue, path, nextValue));
  }

  function actorDisplayName(actorId: unknown) {
    const actor = actors.find((item) => item.id === actorId);
    return String(actor?.display_name ?? "");
  }

  function messageDirection(message: Record<string, JsonValue>) {
    if (message.direction === "system" || message.type === "system") {
      return "system";
    }
    if (message.direction === "outgoing") return "sent";
    return "received";
  }

  function updateObjectPath(
    basePath: JsonPath,
    leafPath: JsonPath,
    nextValue: JsonValue,
  ) {
    updateAtPath([...basePath, ...leafPath], nextValue);
  }

  function renderHeaderFields(header: Record<string, JsonValue>) {
    const inheritedTitle = actorDisplayName(header.actorId);
    const headerActorOptions = actorOptions(actors);
    return (
      <ChatHeaderFieldsEditor
        header={header}
        inheritedTitle={inheritedTitle}
        actorOptions={headerActorOptions}
        onChange={(key, value) => {
          if (key !== "actorId") {
            updateAtPath([key], value);
            return;
          }
          const nextTitle = actorDisplayName(value);
          onGroupValueChange(
            setAtPath(
              setAtPath(groupValue, ["actorId"], value),
              ["title"],
              nextTitle,
            ),
          );
        }}
      />
    );
  }

  function renderMessageFields(
    message: Record<string, JsonValue>,
    index: number,
  ) {
    const direction = messageDirection(message);
    const media = isJsonObject(message.media) ? message.media : {};
    const status = isJsonObject(message.status) ? message.status : {};
    const textReveal = isJsonObject(message.textReveal) ? message.textReveal : {};
    const mediaType = String(media.type ?? (message.mediaAssetId ? "image" : "none"));
    const messageActorOptions = actorOptions(actors);
    const currentActorId = String(message.actorId ?? "");
    const actorOptionsWithCurrentActor =
      currentActorId &&
      !messageActorOptions.some((option) => option.value === currentActorId)
        ? [
            {
              value: currentActorId,
              label: actorDisplayName(currentActorId) || currentActorId,
            },
            ...messageActorOptions,
          ]
        : messageActorOptions;
    const delayAfterPreviousFrames = Number(
      message.delayAfterPreviousFrames ?? message.startFrame ?? 0,
    );
    const writeOnDurationFrames = Number(textReveal.durationFrames ?? 30);

    function updateMessage(nextMessage: JsonValue) {
      updateAtPath([index], nextMessage);
    }

    function setMessagePath(path: JsonPath, nextValue: JsonValue) {
      updateObjectPath([index], path, nextValue);
    }

    function setDirection(nextDirection: string) {
      updateMessage(
        messageWithDirection(
          message,
          nextDirection,
          messageActorOptions[0]?.value ?? "",
          messageActorOptions.find((option) => option.value !== currentActorId)?.value ??
            messageActorOptions[0]?.value ??
            "",
          currentActorId,
        ),
      );
    }

    function setMediaType(nextType: string) {
      updateMessage(messageWithMediaType(message, nextType));
    }

    function setConversationMediaPath(nextPath: string) {
      updateMessage(messageWithMediaPath(message, mediaType, nextPath));
    }

    return (
      <ChatMessageFieldsEditor
        direction={direction}
        actorId={currentActorId}
        actorOptions={actorOptionsWithCurrentActor}
        delayAfterPreviousFrames={delayAfterPreviousFrames}
        writeOnDurationFrames={writeOnDurationFrames}
        showBubbleBackground={message.showBubbleBackground !== false}
        textScale={Number(message.textScale ?? 1)}
        text={String(message.text ?? "")}
        statusText={String(status.text ?? "")}
        deliveryStatus={String(status.deliveryStatus ?? "none")}
        textRevealMode={String(textReveal.mode ?? "simple_write_on")}
        mediaType={mediaType}
        mediaFilePath={String(media.filePath ?? "")}
        mediaDurationSeconds={Number(media.durationSeconds ?? 8)}
        mediaPlayMode={String(media.playMode ?? "once")}
        mediaPlayStartFrame={Number(media.playStartFrame ?? 0)}
        mediaRoot={mediaRoot}
        productionId={productionId}
        canBrowseMedia={canBrowseMedia}
        mediaNumberFields={mediaNumberFieldsForMessage(message)}
        onDirectionChange={setDirection}
        onActorChange={(nextActorId) =>
          setMessagePath(["actorId"], nextActorId)
        }
        onDelayAfterPreviousFramesChange={(nextFrame) =>
          setMessagePath(["delayAfterPreviousFrames"], Math.max(0, nextFrame))
        }
        onWriteOnDurationFramesChange={(nextFrameCount) =>
          setMessagePath(["textReveal", "durationFrames"], nextFrameCount)
        }
        onShowBubbleBackgroundChange={(showBubbleBackground) =>
          setMessagePath(["showBubbleBackground"], showBubbleBackground)
        }
        onTextScaleChange={(textScale) =>
          setMessagePath(["textScale"], textScale)
        }
        onTextChange={(nextText) => setMessagePath(["text"], nextText)}
        onStatusTextChange={(nextText) => setMessagePath(["status", "text"], nextText)}
        onDeliveryStatusChange={(deliveryStatus) =>
          setMessagePath(["status", "deliveryStatus"], deliveryStatus)
        }
        onTextRevealModeChange={(mode) =>
          updateMessage(messageWithTextRevealMode(message, mode))
        }
        onMediaTypeChange={setMediaType}
        onMediaFilePathChange={(nextPath) =>
          setConversationMediaPath(normalizeMediaPath(nextPath))
        }
        onMediaDurationSecondsChange={(durationSeconds) =>
          setMessagePath(["media", "durationSeconds"], Math.max(0.1, durationSeconds))
        }
        onMediaPlayModeChange={(playMode) =>
          setMessagePath(["media", "playMode"], playMode)
        }
        onMediaPlayStartFrameChange={(playStartFrame) =>
          setMessagePath(["media", "playStartFrame"], Math.max(0, playStartFrame))
        }
        onBrowseMedia={() => {
          void (async () => {
            const filePath = await onBrowseMedia();
            if (filePath) {
              setConversationMediaPath(filePath);
            }
          })();
        }}
        onMediaNumberFieldChange={(path, nextValue) =>
          setMessagePath(path, nextValue)
        }
      />
    );
  }

  function renderNestedValue(path: JsonPath, label: string, value: JsonValue) {
    return (
      <ChatNestedValueEditor
        key={path.join(".") || label}
        rootValue={groupValue}
        groupKey={groupKey}
        path={path}
        label={label}
        value={value}
        hints={hints}
        onPathChange={updateAtPath}
        onRootChange={onGroupValueChange}
      />
    );
  }

  function renderObjectFields(value: Record<string, JsonValue>, basePath: JsonPath) {
    return Object.entries(value).map(([key, entryValue]) =>
      renderNestedValue([...basePath, key], key, entryValue),
    );
  }

  function addArrayItem() {
    const nextIndex = Array.isArray(groupValue) ? groupValue.length : 0;
    const firstActorId = String(actors[0]?.id ?? "");
    const nextItem =
      groupKey === "messages"
        ? defaultMessageItem(nextIndex, firstActorId)
        : defaultJsonValue("object");
    onGroupValueChange(
      Array.isArray(groupValue) ? [...groupValue, nextItem] : [nextItem],
    );
  }

  function duplicateArrayItem(index: number) {
    if (!Array.isArray(groupValue)) return;
    onGroupValueChange([
      ...groupValue.slice(0, index + 1),
      cloneJson(groupValue[index]),
      ...groupValue.slice(index + 1),
    ]);
  }

  function deleteArrayItem(index: number) {
    if (!Array.isArray(groupValue)) return;
    onGroupValueChange(
      groupValue.filter((_, candidateIndex) => candidateIndex !== index),
    );
  }

  function moveArrayItem(index: number, direction: -1 | 1) {
    if (!Array.isArray(groupValue)) return;
    const targetIndex = index + direction;
    if (targetIndex < 0 || targetIndex >= groupValue.length) return;
    const nextValue = [...groupValue];
    const current = nextValue[index];
    nextValue[index] = nextValue[targetIndex];
    nextValue[targetIndex] = current;
    onGroupValueChange(nextValue);
  }

  if (Array.isArray(groupValue)) {
    return (
      <ChatContentArrayEditor
        groupKey={groupKey}
        recordId={recordId}
        value={groupValue}
        openItems={openItems}
        onToggleItem={onToggleItem}
        onMoveItem={moveArrayItem}
        onDuplicateItem={duplicateArrayItem}
        onDeleteItem={deleteArrayItem}
        onAddItem={addArrayItem}
        renderItemContent={(entryValue, index, isOpen) =>
          isOpen ? (
            <>
              {groupKey === "messages" && isJsonObject(entryValue) ? (
                renderMessageFields(entryValue, index)
              ) : (
                <div className="record-editor-content-fields">
                  {isJsonObject(entryValue)
                    ? renderObjectFields(entryValue, [index])
                    : renderNestedValue([index], `[${index}]`, entryValue)}
                </div>
              )}
            </>
          ) : null
        }
      />
    );
  }

  if (isJsonObject(groupValue)) {
    if (groupKey === "header") {
      return (
        <div className="record-editor-content-object-editor">
          {renderHeaderFields(groupValue)}
        </div>
      );
    }
    return (
      <div className="record-editor-content-object-editor">
        {renderObjectFields(groupValue, [])}
      </div>
    );
  }

  return (
    <div className="record-editor-content-object-editor">
      {renderNestedValue([], friendlyGroupLabel(groupKey), groupValue)}
    </div>
  );
}
