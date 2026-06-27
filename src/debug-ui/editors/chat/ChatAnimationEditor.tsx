import { useState, type ReactNode } from "react";
import { AppModalDialog } from "../../components/AppModalDialog.js";
import {
  isJsonObject,
  type JsonValue,
} from "../../components/json-editor/jsonEditorUtils.js";
import { InspectorFieldRow } from "../../components/inspector/InspectorFieldRow.js";
import { DeferredTextInput } from "../../editor-ui/DeferredTextInput.js";
import { EditorSubsectionAccordion } from "../../editor-ui/EditorSubsectionAccordion.js";

export type ChatAnimationInterpolation = "hold" | "linear" | "ease";
export type ChatAnimationValueType = "text" | "select";

export interface ChatAnimatableField {
  key: string;
  label: string;
  valueType: ChatAnimationValueType;
  value: JsonValue;
  interpolationOptions: ChatAnimationInterpolation[];
  selectOptions?: Array<{ value: string; label: string }>;
}

interface AnimationKeyframe {
  frame: number;
  interpolation: ChatAnimationInterpolation;
  value: JsonValue;
}

interface ChatAnimationEditorRenderApi {
  animationCard: ReactNode;
  fieldLabel: (trackKey: string) => ReactNode;
  hasAnimationTracks: boolean;
}

interface ChatAnimationEditorProps {
  animation: Record<string, JsonValue>;
  fields: ChatAnimatableField[];
  timelineDurationFrames: number;
  children: (api: ChatAnimationEditorRenderApi) => ReactNode;
  onAnimationChange: (animation: Record<string, JsonValue>) => void;
  onAnimationFrameChange?: (frame: number) => void;
}

export function chatAnimationHasTracks(animation: unknown) {
  if (typeof animation !== "object" || animation === null || Array.isArray(animation)) {
    return false;
  }
  const animationRoot = animation as Record<string, JsonValue>;
  const tracks = isJsonObject(animationRoot.tracks) ? animationRoot.tracks : {};
  return Object.keys(tracks).length > 0;
}

function normalizedKeyframe(value: Record<string, JsonValue>): AnimationKeyframe {
  const interpolation = String(value.interpolation ?? "hold");
  return {
    frame: Number(value.frame ?? 0),
    interpolation:
      interpolation === "linear" || interpolation === "ease" ? interpolation : "hold",
    value: (value.value ?? "") as JsonValue,
  };
}

export function ChatAnimationEditor({
  animation,
  fields,
  timelineDurationFrames,
  children,
  onAnimationChange,
  onAnimationFrameChange,
}: ChatAnimationEditorProps) {
  const [activeAnimationGroup, setActiveAnimationGroup] = useState("");
  const [animationFrame, setAnimationFrame] = useState(0);
  const [pendingDeleteTrack, setPendingDeleteTrack] = useState<string | null>(null);
  const [pendingDeleteKeyframe, setPendingDeleteKeyframe] = useState<{
    trackKey: string;
    frame: number;
  } | null>(null);

  const animationTracks = isJsonObject(animation.tracks) ? animation.tracks : {};
  const hasAnimationTracks = Object.keys(animationTracks).length > 0;

  function fieldFor(trackKey: string) {
    return fields.find((field) => field.key === trackKey);
  }

  function trackFor(trackKey: string) {
    const track = animationTracks[trackKey];
    return isJsonObject(track) ? track : undefined;
  }

  function keyframesFor(trackKey: string): AnimationKeyframe[] {
    const track = trackFor(trackKey);
    const keyframes = Array.isArray(track?.keyframes) ? track.keyframes : [];
    return keyframes
      .filter(isJsonObject)
      .map(normalizedKeyframe)
      .filter((keyframe) => Number.isFinite(keyframe.frame))
      .sort((a, b) => a.frame - b.frame);
  }

  function allKeyframeFrames() {
    return Array.from(
      new Set(
        fields.flatMap((field) =>
          keyframesFor(field.key).map((keyframe) => keyframe.frame),
        ),
      ),
    ).sort((a, b) => a - b);
  }

  const timelineEndFrame = Math.max(
    0,
    Math.round(Number.isFinite(timelineDurationFrames) ? timelineDurationFrames : 1) -
      1,
  );
  const visibleKeyframeFrames = allKeyframeFrames().filter(
    (frame) => frame >= 0 && frame <= timelineEndFrame,
  );

  function setClampedAnimationFrame(nextFrame: number) {
    if (!Number.isFinite(nextFrame)) return;
    const clampedFrame = Math.max(
      0,
      Math.min(timelineEndFrame, Math.round(nextFrame)),
    );
    setAnimationFrame(clampedFrame);
    onAnimationFrameChange?.(clampedFrame);
  }

  function previousKeyframeFrame() {
    return allKeyframeFrames()
      .filter((frame) => frame < animationFrame)
      .at(-1);
  }

  function nextKeyframeFrame() {
    return allKeyframeFrames().find((frame) => frame > animationFrame);
  }

  function previousFieldKeyframeFrame(trackKey: string) {
    return keyframesFor(trackKey)
      .map((keyframe) => keyframe.frame)
      .filter((frame) => frame < animationFrame)
      .at(-1);
  }

  function nextFieldKeyframeFrame(trackKey: string) {
    return keyframesFor(trackKey)
      .map((keyframe) => keyframe.frame)
      .find((frame) => frame > animationFrame);
  }

  function isTrackEnabled(trackKey: string) {
    return Boolean(trackFor(trackKey));
  }

  function keyframeAtCurrentFrame(trackKey: string) {
    return keyframesFor(trackKey).find(
      (keyframe) => keyframe.frame === animationFrame,
    );
  }

  function valueAtFrame(field: ChatAnimatableField) {
    const keyframes = keyframesFor(field.key);
    const exact = keyframes.find((keyframe) => keyframe.frame === animationFrame);
    if (exact) return exact.value;
    const previous = keyframes
      .filter((keyframe) => keyframe.frame <= animationFrame)
      .at(-1);
    return previous?.value ?? field.value;
  }

  function setTrackKeyframes(
    field: ChatAnimatableField,
    keyframes: AnimationKeyframe[],
  ) {
    const nextTracks = { ...animationTracks };
    if (!keyframes.length) {
      delete nextTracks[field.key];
    } else {
      nextTracks[field.key] = {
        valueType: field.valueType,
        keyframes: keyframes
          .slice()
          .sort((a, b) => a.frame - b.frame)
          .map((keyframe) => ({
            frame: Math.max(0, Math.round(keyframe.frame)),
            value: keyframe.value,
            interpolation:
              field.interpolationOptions.includes(keyframe.interpolation)
                ? keyframe.interpolation
                : field.interpolationOptions[0],
          })),
      };
    }
    onAnimationChange({ tracks: nextTracks });
  }

  function enableAnimationTrack(field: ChatAnimatableField) {
    setTrackKeyframes(field, [
      {
        frame: 0,
        value: field.value,
        interpolation: field.interpolationOptions[0],
      },
    ]);
    setActiveAnimationGroup("animation");
  }

  function deleteAnimationTrack(trackKey: string) {
    const field = fieldFor(trackKey);
    if (!field) return;
    setTrackKeyframes(field, []);
  }

  function upsertKeyframeValue(
    field: ChatAnimatableField,
    value: JsonValue,
    interpolation?: ChatAnimationInterpolation,
  ) {
    const keyframes = keyframesFor(field.key);
    const existing = keyframes.find(
      (keyframe) => keyframe.frame === animationFrame,
    );
    const nextInterpolation =
      interpolation ?? existing?.interpolation ?? field.interpolationOptions[0];
    const nextKeyframes = existing
      ? keyframes.map((keyframe) =>
          keyframe.frame === animationFrame
            ? { ...keyframe, value, interpolation: nextInterpolation }
            : keyframe,
        )
      : [
          ...keyframes,
          {
            frame: animationFrame,
            value,
            interpolation: nextInterpolation,
          },
        ];
    setTrackKeyframes(field, nextKeyframes);
  }

  function deleteKeyframe(trackKey: string, frame: number) {
    const field = fieldFor(trackKey);
    if (!field) return;
    setTrackKeyframes(
      field,
      keyframesFor(trackKey).filter((keyframe) => keyframe.frame !== frame),
    );
  }

  function toggleKeyframeAtCurrentFrame(field: ChatAnimatableField) {
    const currentKeyframe = keyframeAtCurrentFrame(field.key);
    if (currentKeyframe) {
      setPendingDeleteKeyframe({
        trackKey: field.key,
        frame: animationFrame,
      });
      return;
    }
    upsertKeyframeValue(field, valueAtFrame(field));
  }

  function fieldLabel(trackKey: string) {
    const field = fieldFor(trackKey);
    if (!field) return <span>{trackKey}</span>;
    const active = isTrackEnabled(field.key);
    return (
      <span className="message-animation-field-label">
        <button
          type="button"
          className={`message-animation-toggle record-editor-content-action ui-icon-button ${
            active ? "is-active" : ""
          }`}
          title={active ? "Remove animation" : "Animate field"}
          aria-label={
            active ? `Remove ${field.label} animation` : `Animate ${field.label}`
          }
          onClick={() => {
            if (active) {
              setPendingDeleteTrack(field.key);
              return;
            }
            enableAnimationTrack(field);
          }}
        >
          {active ? "◆" : "◇"}
        </button>
        <span>{field.label}</span>
      </span>
    );
  }

  function renderAnimationValueControl(field: ChatAnimatableField) {
    const currentValue = valueAtFrame(field);
    if (field.valueType === "select") {
      return (
        <select
          className="json-value-control"
          value={typeof currentValue === "string" ? currentValue : String(field.value)}
          onChange={(event) => upsertKeyframeValue(field, event.currentTarget.value)}
        >
          {(field.selectOptions ?? []).map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      );
    }
    return (
      <DeferredTextInput
        value={typeof currentValue === "string" ? currentValue : String(currentValue)}
        onCommit={(nextValue) => upsertKeyframeValue(field, nextValue)}
      />
    );
  }

  function renderAnimationRows() {
    const activeFields = fields.filter((field) => isTrackEnabled(field.key));
    if (!activeFields.length) {
      return (
        <div className="record-editor-content-fields">
          <small className="modal-help">
            Use the diamond control beside a field to enable animation.
          </small>
        </div>
      );
    }

    return (
      <div className="record-editor-content-fields">
        <div className="message-animation-timeline">
          <div className="message-animation-controls">
            <button
              type="button"
              className="record-editor-content-action ui-icon-button"
              aria-label="Go to first frame"
              onClick={() => setClampedAnimationFrame(0)}
            >
              |‹
            </button>
            <button
              type="button"
              className="record-editor-content-action ui-icon-button"
              aria-label="Go to previous frame"
              disabled={animationFrame <= 0}
              onClick={() => setClampedAnimationFrame(animationFrame - 1)}
            >
              ‹
            </button>
            <button
              type="button"
              className="record-editor-content-action ui-icon-button"
              aria-label="Go to previous keyframe"
              disabled={previousKeyframeFrame() === undefined}
              onClick={() => {
                const frame = previousKeyframeFrame();
                if (frame !== undefined) setClampedAnimationFrame(frame);
              }}
            >
              ◆‹
            </button>
            <input
              className="json-value-control message-animation-current-frame"
              type="number"
              min={0}
              max={timelineEndFrame}
              step={1}
              value={animationFrame}
              onChange={(event) =>
                setClampedAnimationFrame(Number(event.currentTarget.value))
              }
            />
            <button
              type="button"
              className="record-editor-content-action ui-icon-button"
              aria-label="Go to next keyframe"
              disabled={nextKeyframeFrame() === undefined}
              onClick={() => {
                const frame = nextKeyframeFrame();
                if (frame !== undefined) setClampedAnimationFrame(frame);
              }}
            >
              ›◆
            </button>
            <button
              type="button"
              className="record-editor-content-action ui-icon-button"
              aria-label="Go to next frame"
              disabled={animationFrame >= timelineEndFrame}
              onClick={() => setClampedAnimationFrame(animationFrame + 1)}
            >
              ›
            </button>
            <button
              type="button"
              className="record-editor-content-action ui-icon-button"
              aria-label="Go to last frame"
              onClick={() => setClampedAnimationFrame(timelineEndFrame)}
            >
              ›|
            </button>
          </div>
          <input
            className="json-value-control message-animation-slider"
            type="range"
            min={0}
            max={timelineEndFrame}
            step={1}
            value={animationFrame}
            onChange={(event) =>
              setClampedAnimationFrame(Number(event.currentTarget.value))
            }
          />
          <div className="message-animation-keyframe-strip" aria-hidden="true">
            {visibleKeyframeFrames.map((frame) => (
              <span
                key={frame}
                className={`message-animation-keyframe-mark ${
                  frame === animationFrame ? "is-current" : ""
                }`}
                style={{
                  left: `${
                    timelineEndFrame > 0 ? (frame / timelineEndFrame) * 100 : 0
                  }%`,
                }}
              />
            ))}
          </div>
        </div>
        {activeFields.map((field) => {
          const currentKeyframe = keyframeAtCurrentFrame(field.key);
          return (
            <InspectorFieldRow
              key={field.key}
              className="record-editor-content-field-row message-animation-row"
              state={currentKeyframe ? "override" : "default"}
              label={
                <span className="message-animation-row-label">
                  <button
                    type="button"
                    className="message-animation-row-step"
                    title="Previous property keyframe"
                    aria-label={`Previous ${field.label} keyframe`}
                    disabled={previousFieldKeyframeFrame(field.key) === undefined}
                    onClick={() => {
                      const frame = previousFieldKeyframeFrame(field.key);
                      if (frame !== undefined) setClampedAnimationFrame(frame);
                    }}
                  >
                    ‹
                  </button>
                  <button
                    type="button"
                    className={`message-animation-row-key ${
                      currentKeyframe ? "is-active" : ""
                    }`}
                    title={currentKeyframe ? "Delete keyframe" : "Add keyframe"}
                    aria-label={
                      currentKeyframe
                        ? `Delete ${field.label} keyframe`
                        : `Add ${field.label} keyframe`
                    }
                    onClick={() => toggleKeyframeAtCurrentFrame(field)}
                  >
                    {currentKeyframe ? "◆" : "◇"}
                  </button>
                  <button
                    type="button"
                    className="message-animation-row-step"
                    title="Next property keyframe"
                    aria-label={`Next ${field.label} keyframe`}
                    disabled={nextFieldKeyframeFrame(field.key) === undefined}
                    onClick={() => {
                      const frame = nextFieldKeyframeFrame(field.key);
                      if (frame !== undefined) setClampedAnimationFrame(frame);
                    }}
                  >
                    ›
                  </button>
                  <span>{field.label}</span>
                </span>
              }
              meta={
                <span>
                  {keyframesFor(field.key).length} keyframe
                  {keyframesFor(field.key).length === 1 ? "" : "s"}
                </span>
              }
              control={
                <div className="message-animation-row-controls">
                  {renderAnimationValueControl(field)}
                  <select
                    className="json-value-control"
                    value={
                      currentKeyframe?.interpolation ??
                      field.interpolationOptions[0]
                    }
                    onChange={(event) =>
                      upsertKeyframeValue(
                        field,
                        valueAtFrame(field),
                        event.currentTarget.value as ChatAnimationInterpolation,
                      )
                    }
                  >
                    {field.interpolationOptions.map((option) => (
                      <option key={option} value={option}>
                        {option}
                      </option>
                    ))}
                  </select>
                </div>
              }
            />
          );
        })}
      </div>
    );
  }

  return (
    <>
      {pendingDeleteTrack ? (
        <AppModalDialog
          eyebrow="Animation"
          title="Delete animation track?"
          message="This removes all keyframes for this field."
          confirmLabel="Delete"
          destructive
          onCancel={() => setPendingDeleteTrack(null)}
          onConfirm={() => {
            const trackKey = pendingDeleteTrack;
            setPendingDeleteTrack(null);
            deleteAnimationTrack(trackKey);
          }}
        />
      ) : null}
      {pendingDeleteKeyframe ? (
        <AppModalDialog
          eyebrow="Animation"
          title={`Delete keyframe at ${pendingDeleteKeyframe.frame}f?`}
          message="This removes only this keyframe. The animation track remains if it has other keyframes."
          confirmLabel="Delete"
          destructive
          onCancel={() => setPendingDeleteKeyframe(null)}
          onConfirm={() => {
            const keyframe = pendingDeleteKeyframe;
            setPendingDeleteKeyframe(null);
            deleteKeyframe(keyframe.trackKey, keyframe.frame);
          }}
        />
      ) : null}
      {children({
        animationCard: (
          <EditorSubsectionAccordion
            group="animation"
            activeGroup={activeAnimationGroup}
            animationState={hasAnimationTracks ? "active" : "inactive"}
            onToggle={setActiveAnimationGroup}
          >
            {renderAnimationRows()}
          </EditorSubsectionAccordion>
        ),
        fieldLabel,
        hasAnimationTracks,
      })}
    </>
  );
}
