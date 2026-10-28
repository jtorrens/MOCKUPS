using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Styling;
using Avalonia.LogicalTree;
using System;
using System.Linq;

namespace Mockups.DesktopEditorShell.Common;

// Integrates Suki's chrome with Avalonia's TopLevel template contract. The
// platform initializes popup, light-dismiss and selector layers on this part.
public sealed class EditorSukiWindowTemplate : IControlTemplate
{
    private const string LayerPart = "PART_VisualLayerManager";
    public required ControlTheme ChromeTheme { get; set; }

    public TemplateResult<Control>? Build(TemplatedControl control)
    {
        var template = ChromeTheme.Setters.OfType<Setter>()
            .Single(setter => setter.Property == TemplatedControl.TemplateProperty).Value as IControlTemplate
            ?? throw new InvalidOperationException("The Suki chrome theme must declare its control template.");
        var chrome = template.Build(control)
            ?? throw new InvalidOperationException("The Suki chrome template must create its root.");
        if (chrome.NameScope.Find(LayerPart) is not null)
            throw new InvalidOperationException("The Suki chrome already declares the window layer owner.");
        var layers = new VisualLayerManager { Name = LayerPart, Child = chrome.Result };
        var scope = new NameScope();
        scope.Register(LayerPart, layers);
        foreach (var part in chrome.Result.GetLogicalDescendants().Prepend(chrome.Result).OfType<Control>())
            if (part.Name is { } name && ReferenceEquals(chrome.NameScope.Find(name), part))
                scope.Register(name, part);
        scope.Complete();
        return new TemplateResult<Control>(layers, scope);
    }
}
