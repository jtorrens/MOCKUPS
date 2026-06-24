import type { ReactNode } from "react";

interface EditorSectionsProps {
  children: ReactNode;
}

export function EditorSections({ children }: EditorSectionsProps) {
  return <div className="editor-sections">{children}</div>;
}
