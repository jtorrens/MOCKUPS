import "./EditorSystem.css";
import "./EditorContent.css";

interface EditorHeaderProps {
  title: string;
  eyebrow: string;
  summary?: string;
}

export function EditorHeader({ title, eyebrow, summary }: EditorHeaderProps) {
  return (
    <div className="record-editor-heading">
      <div>
        <span className="record-editor-eyebrow">{eyebrow}</span>
        <h2>{title}</h2>
        {summary ? <p className="record-editor-summary">{summary}</p> : null}
      </div>
    </div>
  );
}
