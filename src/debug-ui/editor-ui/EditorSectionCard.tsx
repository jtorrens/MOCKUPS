import type { ReactNode } from "react";

interface EditorSectionCardProps {
  children: ReactNode;
}

export function EditorSectionCard({ children }: EditorSectionCardProps) {
  return <div className="editor-section-card">{children}</div>;
}

export function EditorSubsectionCard({ children }: EditorSectionCardProps) {
  return <div className="editor-subsection-card">{children}</div>;
}
