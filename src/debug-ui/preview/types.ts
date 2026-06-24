import type { DebugOptions, DebugPayload, DebugSelection } from "../api/client.js";

export interface RightPreviewShellProps {
  options: DebugOptions;
  selection: DebugSelection;
  payload: DebugPayload | null;
  busy: boolean;
  onSelectionChange: (selection: DebugSelection) => void;
  error?: string;
}
