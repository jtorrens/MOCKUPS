import type { ReactNode } from "react";
import { friendlyGroupLabel } from "../components/json-editor/labels.js";
import { EditorSectionButton } from "./EditorSectionButton.js";
import { EditorSubsectionCard } from "./EditorSectionCard.js";

interface EditorSubsectionAccordionProps {
  group: string;
  label?: string;
  activeGroup: string;
  warning?: boolean;
  animationState?: "unsupported" | "inactive" | "active";
  onToggle: (group: string) => void;
  children: ReactNode;
}

export function EditorSubsectionAccordion({
  group,
  label,
  activeGroup,
  warning,
  animationState = "unsupported",
  onToggle,
  children,
}: EditorSubsectionAccordionProps) {
  const active = activeGroup === group;
  return (
    <EditorSubsectionCard>
      <EditorSectionButton
        active={active}
        warning={warning}
        animationState={animationState}
        onClick={() => onToggle(active ? "" : group)}
      >
        {label ?? friendlyGroupLabel(group)}
      </EditorSectionButton>
      {active ? <div className="editor-subsection-body">{children}</div> : null}
    </EditorSubsectionCard>
  );
}
