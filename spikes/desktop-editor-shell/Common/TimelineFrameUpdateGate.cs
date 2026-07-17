using System;

namespace Mockups.DesktopEditorShell.Common;

internal sealed class TimelineFrameUpdateGate
{
    private int _depth;

    public bool IsActive => _depth > 0;

    public void Run(Action update)
    {
        _depth++;
        try
        {
            update();
        }
        finally
        {
            _depth--;
        }
    }
}
