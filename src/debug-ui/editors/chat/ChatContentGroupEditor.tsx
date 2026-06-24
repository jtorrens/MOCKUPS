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
import { ChatParticipantFieldsEditor } from "./ChatParticipantFieldsEditor.js";
import {
  defaultMessageItem,
  defaultParticipantItem,
  firstReceivedParticipant,
  mediaNumberFieldsForMessage,
  messageDirectionFromSenderRole,
  messageWithDirection,
  messageWithMediaPath,
  messageWithMediaType,
  messageWithTextRevealMode,
  ownerParticipant,
  participantById,
  participantDisplayName,
  participantOptions,
  participantsFromContentRoot,
} from "./chatContentModel.js";

interface ChatContentGroupEditorProps {
  actors: AppRecord[];
  groupKey: string;
  groupValue: JsonValue;
  hints: JsonUiHints;
  openItems: Record<string, boolean>;
  recordId?: unknown;
  root: Record<string, unknown>;
  actorTitleForRecord: (actor: AppRecord) => string;
  canBrowseMedia: boolean;
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
  root,
  actorTitleForRecord,
  canBrowseMedia,
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

  function participantsArray() {
    return participantsFromContentRoot(root);
  }

  function participantLabel(participant: Record<string, JsonValue> | undefined) {
    return participantDisplayName(participant, actorDisplayName);
  }

  function messageDirection(message: Record<string, JsonValue>) {
    const sender = participantById(participantsArray(), message.senderParticipantId);
    return messageDirectionFromSenderRole(message, sender?.role);
  }

  function updateObjectPath(
    basePath: JsonPath,
    leafPath: JsonPath,
    nextValue: JsonValue,
  ) {
    updateAtPath([...basePath, ...leafPath], nextValue);
  }

  function renderParticipantFields(
    participant: Record<string, JsonValue>,
    index: number,
  ) {
    const actorId = String(participant.actorId ?? "");
    const inheritedDisplayName = actorDisplayName(actorId);
    const displayName = String(
      participant.displayName ?? inheritedDisplayName ?? "",
    );
    return (
      <ChatParticipantFieldsEditor
        participant={participant}
        actorOptions={actors}
        actorId={actorId}
        displayName={displayName}
        inheritedDisplayName={inheritedDisplayName}
        actorTitleForRecord={actorTitleForRecord}
        onActorChange={(nextActorId) => {
          const nextDisplayName = actorDisplayName(nextActorId);
          onGroupValueChange(
            setAtPath(
              setAtPath(groupValue, [index, "actorId"], nextActorId),
              [index, "displayName"],
              nextDisplayName,
            ),
          );
        }}
        onDisplayNameChange={(nextValue) =>
          updateAtPath([index, "displayName"], nextValue)
        }
        onRoleChange={(nextRole) => updateAtPath([index, "role"], nextRole)}
      />
    );
  }

  function renderHeaderFields(header: Record<string, JsonValue>) {
    const avatarParticipant = participantById(
      participantsArray(),
      header.avatarParticipantId,
    );
    const inheritedTitle = participantLabel(avatarParticipant);
    return (
      <ChatHeaderFieldsEditor
        header={header}
        inheritedTitle={inheritedTitle}
        onChange={(key, value) => updateAtPath([key], value)}
      />
    );
  }

  function renderMessageFields(
    message: Record<string, JsonValue>,
    index: number,
  ) {
    const direction = messageDirection(message);
    const media = isJsonObject(message.media) ? message.media : {};
    const textReveal = isJsonObject(message.textReveal) ? message.textReveal : {};
    const mediaType = String(media.type ?? (message.mediaAssetId ? "image" : "none"));
    const receivedOptions = participantOptions(
      participantsArray().filter((participant) => participant.role !== "owner"),
      participantLabel,
    );
    const senderId = String(message.senderParticipantId ?? "");

    function updateMessage(nextMessage: JsonValue) {
      updateAtPath([index], nextMessage);
    }

    function setMessagePath(path: JsonPath, nextValue: JsonValue) {
      updateObjectPath([index], path, nextValue);
    }

    function setDirection(nextDirection: string) {
      const participants = participantsArray();
      const owner = ownerParticipant(participants);
      const received = firstReceivedParticipant(participants);
      updateMessage(
        messageWithDirection(
          message,
          nextDirection,
          String(owner?.id ?? ""),
          String(received?.id ?? ""),
          senderId,
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
        senderId={senderId}
        receivedOptions={receivedOptions}
        showBubbleBackground={message.showBubbleBackground !== false}
        textScale={Number(message.textScale ?? 1)}
        text={String(message.text ?? "")}
        textRevealMode={String(textReveal.mode ?? "simple_write_on")}
        mediaType={mediaType}
        mediaFilePath={String(media.filePath ?? "")}
        canBrowseMedia={canBrowseMedia}
        mediaNumberFields={mediaNumberFieldsForMessage(message)}
        onDirectionChange={setDirection}
        onSenderChange={(nextSenderId) =>
          setMessagePath(["senderParticipantId"], nextSenderId)
        }
        onShowBubbleBackgroundChange={(showBubbleBackground) =>
          setMessagePath(["showBubbleBackground"], showBubbleBackground)
        }
        onTextScaleChange={(textScale) =>
          setMessagePath(["textScale"], textScale)
        }
        onTextChange={(nextText) => setMessagePath(["text"], nextText)}
        onTextRevealModeChange={(mode) =>
          updateMessage(messageWithTextRevealMode(message, mode))
        }
        onMediaTypeChange={setMediaType}
        onMediaFilePathChange={(nextPath) =>
          setConversationMediaPath(normalizeMediaPath(nextPath))
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
    const participants = participantsArray();
    const nextItem =
      groupKey === "messages"
        ? defaultMessageItem(
            nextIndex,
            String(
              (
                firstReceivedParticipant(participants) ??
                ownerParticipant(participants)
              )?.id ?? "",
            ),
          )
        : groupKey === "participants"
          ? defaultParticipantItem(nextIndex)
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
              {groupKey === "participants" && isJsonObject(entryValue) ? (
                renderParticipantFields(entryValue, index)
              ) : groupKey === "messages" && isJsonObject(entryValue) ? (
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
