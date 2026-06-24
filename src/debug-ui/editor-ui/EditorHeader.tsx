interface EditorHeaderProps {
  title: string;
  eyebrow: string;
}

export function EditorHeader({ title, eyebrow }: EditorHeaderProps) {
  return (
    <div className="record-editor-heading">
      <div>
        <span className="record-editor-eyebrow">{eyebrow}</span>
        <h2>{title}</h2>
      </div>
    </div>
  );
}
