import type { DebugOptions, DebugPayload, DebugSelection } from "../api/client.js";

export interface RightPreviewShellProps {
  options: DebugOptions;
  selection: DebugSelection;
  payload: DebugPayload | null;
  onSelectionChange: (selection: DebugSelection) => void;
  error?: string;
}
