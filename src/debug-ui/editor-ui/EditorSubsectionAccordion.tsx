import type { ReactNode } from "react";
import { friendlyGroupLabel } from "../components/json-editor/labels.js";
import { EditorSectionButton } from "./EditorSectionButton.js";
import { EditorSubsectionCard } from "./EditorSectionCard.js";

interface EditorSubsectionAccordionProps {
  group: string;
  activeGroup: string;
  warning?: boolean;
  onToggle: (group: string) => void;
  children: ReactNode;
}

export function EditorSubsectionAccordion({
  group,
  activeGroup,
  warning,
  onToggle,
  children,
}: EditorSubsectionAccordionProps) {
  const active = activeGroup === group;
  return (
    <EditorSubsectionCard>
      <EditorSectionButton
        active={active}
        warning={warning}
        onClick={() => onToggle(active ? "" : group)}
      >
        {friendlyGroupLabel(group)}
      </EditorSectionButton>
      {active ? <div className="editor-subsection-body">{children}</div> : null}
    </EditorSubsectionCard>
  );
}
