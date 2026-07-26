using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

public sealed partial class SpikeDatabase
{
    private readonly object _variantEditingSessionGate = new();
    private readonly HashSet<string> _sessionUnlockedDefaultVariants = new(StringComparer.Ordinal);

    private bool IsVariantLockedForEditing(
        string ownerId,
        string variantId,
        bool persistedLocked)
    {
        if (!variantId.Equals(VariantEnvelopeContract.DefaultId, StringComparison.Ordinal))
        {
            return persistedLocked;
        }

        var reference = VariantReferenceId.Format(ownerId, variantId);
        lock (_variantEditingSessionGate)
        {
            return !_sessionUnlockedDefaultVariants.Contains(reference);
        }
    }

    private bool ToggleDefaultVariantSessionLock(string ownerId, string variantId)
    {
        if (!variantId.Equals(VariantEnvelopeContract.DefaultId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Variant '{variantId}' is not the stable Default Variant.");
        }

        var reference = VariantReferenceId.Format(ownerId, variantId);
        lock (_variantEditingSessionGate)
        {
            if (_sessionUnlockedDefaultVariants.Remove(reference))
            {
                return true;
            }

            _sessionUnlockedDefaultVariants.Add(reference);
            return false;
        }
    }
}
