import type { DebugOptions, DebugPayload, DebugSelection } from "../api/client.js";
import type { PreviewFit } from "./previewSizing.js";

export function choosePreviewScreenForShot(
  options: DebugOptions,
  shotId: string | undefined,
) {
  return (
    options.screenInstances.find(
      (candidate) =>
        candidate.shotId === shotId && candidate.moduleId === "core.chat",
    ) ?? options.screenInstances.find((candidate) => candidate.shotId === shotId)
  );
}

export function previewFrameForScreen(
  screen: NonNullable<ReturnType<typeof choosePreviewScreenForShot>>,
  fallbackFrame = 210,
) {
  return Math.max(
    screen.startFrame,
    Math.min(fallbackFrame, screen.endFrame - 1),
  );
}

interface BuildPreviewOptionsViewModelInput {
  options: DebugOptions;
  payload: DebugPayload | null;
  previewFit: PreviewFit | null;
  selection: DebugSelection;
  showFrame: boolean;
}

export function buildPreviewOptionsViewModel({
  options,
  payload,
  previewFit,
  selection,
  showFrame,
}: BuildPreviewOptionsViewModelInput) {
  const episodes = options.episodes.filter(
    (episode) => episode.productionId === selection.productionId,
  );
  const selectedShot = options.shots.find((shot) => shot.id === selection.shotId);
  const selectedEpisodeId =
    selectedShot?.episodeId ?? episodes[0]?.id ?? "";
  const shots = options.shots.filter((shot) =>
    selectedEpisodeId
      ? shot.episodeId === selectedEpisodeId
      : shot.productionId === selection.productionId,
  );
  const screenInstances = options.screenInstances.filter(
    (instance) => instance.shotId === selection.shotId,
  );
  const selectedInstance = options.screenInstances.find(
    (instance) => instance.id === selection.screenInstanceId,
  );
  const previewTitle =
    selectedInstance?.moduleId?.replace(/^core\./, "") ??
    selectedInstance?.screenType ??
    "Preview";
  const previewContext = payload?.previewContext;
  const maxFrame = Math.max(0, (selectedShot?.durationFrames ?? 1) - 1);
  const renderSizeText = payload?.renderable
    ? `${payload.renderable.box?.width ?? 1290}×${payload.renderable.box?.height ?? 2796}`
    : "No render";
  const zoomText = previewFit ? `${previewFit.scale.toFixed(3)}×` : "—";
  const previewSummary = [
    previewTitle,
    previewContext?.deviceName,
    previewContext
      ? `${previewContext.themeName} · ${previewContext.themeMode}`
      : undefined,
    `zoom ${zoomText}`,
    showFrame ? "frame on" : "frame off",
  ]
    .filter(Boolean)
    .join(" · ");

  return {
    episodes,
    maxFrame,
    previewContext,
    previewSummary,
    previewTitle,
    renderSizeText,
    screenInstances,
    selectedEpisodeId,
    selectedInstance,
    shots,
    zoomText,
  };
}
