import type { RenderFrameResult } from "../api/client.js";

interface PreviewOutputStackProps {
  error?: string;
  renderError: string;
  renderResult: RenderFrameResult | null;
  warnings?: string[];
}

export function PreviewOutputStack({
  error,
  renderError,
  renderResult,
  warnings = [],
}: PreviewOutputStackProps) {
  return (
    <>
      {error ? (
        <div className="preview-message-card error" role="alert">
          <strong>Preview error</strong>
          {error}
        </div>
      ) : null}
      {renderError ? (
        <div className="preview-message-card error" role="alert">
          <strong>PNG render error</strong>
          {renderError}
        </div>
      ) : null}
      {renderResult ? (
        <div className="preview-output-card">
          <div>
            <strong>PNG rendered</strong>
            <span>
              {renderResult.relativeFilePath ?? renderResult.filePath}
            </span>
          </div>
          <p>
            {renderResult.outputWidth}×{renderResult.outputHeight} · scale{" "}
            {renderResult.outputScale} ·{" "}
            {renderResult.includeFrame ? "with frame" : "no frame"}
          </p>
          <a href={renderResult.url} target="_blank" rel="noreferrer">
            Open PNG
          </a>
        </div>
      ) : null}
      {warnings.length ? (
        <div className="preview-message-card warning" data-testid="warnings">
          <strong>Preview warnings</strong>
          {warnings.map((warning) => (
            <div key={warning}>{warning}</div>
          ))}
        </div>
      ) : null}
    </>
  );
}
