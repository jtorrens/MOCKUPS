import { useState, type ReactNode } from "react";
import { AppModalDialog } from "../../components/AppModalDialog.js";
import {
  isJsonObject,
  type JsonValue,
} from "../../components/json-editor/jsonEditorUtils.js";
import { InspectorFieldRow } from "../../components/inspector/InspectorFieldRow.js";
import type { FieldDefinition } from "../../../domain/value-system/index.js";
import { DictionaryFieldControl } from "../DictionaryFieldControl.js";
import { EditorSubsectionAccordion } from "../EditorSubsectionAccordion.js";
import type { EditorFieldDescriptor } from "../fields/EditorFieldDescriptor.js";
import { toDictionaryFieldControlProps } from "../fields/EditorFieldDescriptor.js";

export type AnimationInterpolation = "hold" | "linear" | "ease";
export type AnimationValueType = "text" | "select";

export interface AnimatableField {
  key: string;
  label: string;
  valueType: AnimationValueType;
  value: JsonValue;
  interpolationOptions: AnimationInterpolation[];
  field?: FieldDefinition;
  selectOptions?: Array<{ value: string; label: string }>;
}

interface AnimationKeyframe {
  frame: number;
  interpolation: AnimationInterpolation;
  value: JsonValue;
}

interface AnimationFieldEditorRenderApi {
  animationCard: ReactNode;
  fieldLabel: (trackKey: string) => ReactNode;
  hasAnimationTracks: boolean;
}

interface AnimationFieldEditorProps {
  animation: Record<string, JsonValue>;
  fields: AnimatableField[];
  timelineDurationFrames: number;
  children: (api: AnimationFieldEditorRenderApi) => ReactNode;
  onAnimationChange: (animation: Record<string, JsonValue>) => void;
  onAnimationFrameChange?: (frame: number) => void;
}

export function animationHasTracks(animation: unknown) {
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

function animationValueField(field: AnimatableField): FieldDefinition {
  return (
    field.field ?? {
      id: `chat.animation.${field.key}`,
      kind: field.valueType === "select" ? "enum" : "text",
      defaultValue: field.valueType === "select" ? String(field.value) : "",
      ui: {
        label: field.label,
        options:
          field.valueType === "select"
            ? field.selectOptions?.map((option) => option.value)
            : undefined,
      },
    }
  );
}

const ANIMATION_INTERPOLATION_FIELD: FieldDefinition = {
  id: "chat.animation.keyframe.interpolation",
  kind: "enum",
  defaultValue: "hold",
  ui: {
    label: "Interpolation",
    options: ["hold", "linear", "ease"],
  },
};

function animationDescriptor({
  field,
  value,
  onChange,
  options,
}: {
  field: FieldDefinition;
  value: unknown;
  onChange: (nextValue: unknown) => void;
  options?: Array<{ value: string; label: string }>;
}): EditorFieldDescriptor {
  return {
    kind: "field",
    field,
    localValue: value,
    displayValue: value,
    resolvedValue: value,
    state: "default",
    readonly: false,
    canInherit: false,
    canRestore: false,
    source: { kind: "module-instance-content", path: field.id.split(".") },
    actions: { write: onChange },
    selectOptions:
      options || field.ui?.options
        ? {
            options: (options ?? field.ui?.options?.map((option) => ({
              value: option,
              label: option,
            })) ?? []),
          }
        : undefined,
  };
}

export function AnimationFieldEditor({
  animation,
  fields,
  timelineDurationFrames,
  children,
  onAnimationChange,
  onAnimationFrameChange,
}: AnimationFieldEditorProps) {
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

  function valueAtFrame(field: AnimatableField) {
    const keyframes = keyframesFor(field.key);
    const exact = keyframes.find((keyframe) => keyframe.frame === animationFrame);
    if (exact) return exact.value;
    const previous = keyframes
      .filter((keyframe) => keyframe.frame <= animationFrame)
      .at(-1);
    return previous?.value ?? field.value;
  }

  function setTrackKeyframes(
    field: AnimatableField,
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

  function enableAnimationTrack(field: AnimatableField) {
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
    field: AnimatableField,
    value: JsonValue,
    interpolation?: AnimationInterpolation,
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

  function toggleKeyframeAtCurrentFrame(field: AnimatableField) {
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
      <span className="animation-field-label">
        <button
          type="button"
          className={`animation-field-toggle record-editor-content-action ui-icon-button ${
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

  function renderAnimationValueControl(field: AnimatableField) {
    const currentValue = valueAtFrame(field);
    const descriptor = animationDescriptor({
      field: animationValueField(field),
      value:
        field.valueType === "select"
          ? typeof currentValue === "string"
            ? currentValue
            : String(field.value)
          : typeof currentValue === "string"
            ? currentValue
            : String(currentValue),
      options: field.selectOptions,
      onChange: (nextValue) => upsertKeyframeValue(field, nextValue as JsonValue),
    });
    return (
      <DictionaryFieldControl {...toDictionaryFieldControlProps(descriptor)} />
    );
  }

  function renderInterpolationControl(
    field: AnimatableField,
    currentKeyframe: AnimationKeyframe | undefined,
  ) {
    const descriptor = animationDescriptor({
      field: ANIMATION_INTERPOLATION_FIELD,
      value: currentKeyframe?.interpolation ?? field.interpolationOptions[0],
      options: field.interpolationOptions.map((option) => ({
        value: option,
        label: option,
      })),
      onChange: (nextValue) =>
        upsertKeyframeValue(
          field,
          valueAtFrame(field),
          String(nextValue) as AnimationInterpolation,
        ),
    });
    return <DictionaryFieldControl {...toDictionaryFieldControlProps(descriptor)} />;
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
        <div className="animation-field-timeline">
          <div className="animation-field-controls">
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
              className="json-value-control animation-field-current-frame"
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
            className="json-value-control animation-field-slider"
            type="range"
            min={0}
            max={timelineEndFrame}
            step={1}
            value={animationFrame}
            onChange={(event) =>
              setClampedAnimationFrame(Number(event.currentTarget.value))
            }
          />
          <div className="animation-field-keyframe-strip" aria-hidden="true">
            {visibleKeyframeFrames.map((frame) => (
              <span
                key={frame}
                className={`animation-field-keyframe-mark ${
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
              className="record-editor-content-field-row dictionary-field animation-field-row"
              state={currentKeyframe ? "override" : "default"}
              label={
                <span className="animation-field-row-label">
                  <button
                    type="button"
                    className="animation-field-row-step"
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
                    className={`animation-field-row-key ${
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
                    className="animation-field-row-step"
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
                <div className="animation-field-row-controls">
                  {renderAnimationValueControl(field)}
                  {renderInterpolationControl(field, currentKeyframe)}
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
