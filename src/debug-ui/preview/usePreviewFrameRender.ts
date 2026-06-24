import { useCallback, useState } from "react";
import {
  renderPreviewFrame,
  type DebugSelection,
  type RenderFrameResult,
} from "../api/client.js";

export function usePreviewFrameRender(
  selection: DebugSelection,
  includeFrame: boolean,
) {
  const [renderBusy, setRenderBusy] = useState(false);
  const [renderResult, setRenderResult] = useState<RenderFrameResult | null>(null);
  const [renderError, setRenderError] = useState("");

  const renderFramePng = useCallback(() => {
    setRenderBusy(true);
    setRenderError("");
    void renderPreviewFrame({
      ...selection,
      includeFrame,
    })
      .then((result) => {
        const separator = result.url.includes("?") ? "&" : "?";
        setRenderResult({
          ...result,
          url: `${result.url}${separator}t=${Date.now()}`,
        });
      })
      .catch((error: Error) => {
        setRenderResult(null);
        setRenderError(error.message);
      })
      .finally(() => setRenderBusy(false));
  }, [includeFrame, selection]);

  return {
    renderBusy,
    renderError,
    renderFramePng,
    renderResult,
  };
}
