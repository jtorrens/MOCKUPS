using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal interface IEditorShellMessageSink
{
    void Clear();
    void Info(string area, string message);
    void Warning(string area, string message);
    void Error(string area, Exception exception);
    void Error(string area, string message);
}

internal sealed class EditorShellMessageSink : IEditorShellMessageSink
{
    private readonly TextBox _target;

    public EditorShellMessageSink(TextBox target)
    {
        _target = target;
        Clear();
    }

    public void Clear()
    {
        Set("No messages", "#99FFFFFF");
    }

    public void Info(string area, string message)
    {
        Set($"Info · {area} · {message}", "#4ADE80");
    }

    public void Warning(string area, string message)
    {
        Set($"Warning · {area} · {message}", "#FACC15");
    }

    public void Error(string area, Exception exception)
    {
        Error(area, $"{exception.GetType().Name}: {exception.Message}");
    }

    public void Error(string area, string message)
    {
        Set($"Error · {area} · {message}", "#F87171");
    }

    private void Set(string message, string color)
    {
        _target.Text = message;
        _target.Foreground = new SolidColorBrush(Color.Parse(color));
    }
}
