using System;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

// Published only after the serialized write and its authoritative read complete.
internal sealed record RuntimeInputCommittedDocument(string OwnerId, string RuntimeJson)
{
    public JsonObject Document() => JsonNode.Parse(RuntimeJson)?.AsObject()
        ?? throw new InvalidOperationException("A confirmed Runtime document must be an object.");
}
