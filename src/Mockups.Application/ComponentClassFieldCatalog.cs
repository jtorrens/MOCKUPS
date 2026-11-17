using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record ComponentClassFieldDescriptor(
    string Id,
    string Label,
    ValueKind ValueKind,
    string[] JsonPath,
    string DefaultValue,
    bool IsEditable = true,
    IReadOnlyList<FieldOption>? Options = null,
    PairFieldLabels? PairLabels = null,
    NumberDefinition? Number = null,
    IReadOnlyList<ComponentInputBindingDefinition>? ComponentInputBindings = null,
    RuntimeInputCollectionDefinition? StructuredCollection = null,
    string ComponentVariantType = "",
    string RuntimeInputComponentVariantFieldId = "",
    string Unit = "",
    string HelpText = "",
    string ValuePattern = "",
    string ValuePatternMessage = "",
    string ThemeTokenFamily = "");

public static partial class ComponentClassFieldCatalog
{
    private const string CalculatedTextFormatHelp =
        "Time: MM:SS or HH:MM:SS · Number: ###0 optional digits, 0000 zero-padded.";
    private const string CalculatedTextFormatPattern =
        "^(?:(?:M|MM):SS|(?:H|HH):MM(?::SS)?|#*0+)$";
    private const string CalculatedTextFormatPatternMessage =
        "must use M:SS, MM:SS, H:MM, HH:MM, H:MM:SS, HH:MM:SS or a #*0+ numeric mask.";

    static ComponentClassFieldCatalog()
    {
        AddGeneratedFields(Fields);
        ValidateOptionContracts();
    }

    static partial void AddGeneratedFields(
        Dictionary<string, ComponentClassFieldDescriptor> fields);

    private static readonly FieldOption[] ThemeColorOptions = ThemeTokenSelectionContract.Options("colors");

    private static readonly FieldOption[] RadiusTokenOptions = ThemeTokenSelectionContract.Options("radii");

    public static readonly FieldOption[] TypographySizeOptions = ThemeTokenSelectionContract.Options("typography.sizes");

    public static readonly FieldOption[] IconSizeTokenOptions = ThemeTokenSelectionContract.Options("iconSizes");

    private static readonly FieldOption[] PasswordAnchorOptions =
    [
        new("container", "Container"),
        new("input", "Input component"),
    ];

    private static readonly FieldOption[] PasswordModeOptions =
    [
        new("pin", "PIN"),
        new("fingerprint", "Fingerprint"),
        new("faceRecognition", "Face recognition"),
        new("drawPassword", "Draw password"),
    ];

    private static readonly FieldOption[] ButtonContentModeOptions =
    [
        new("icon", "Icon"),
        new("text", "Text"),
        new("iconText", "Icon + text"),
    ];
    private static readonly FieldOption[] IconRowSizeSourceOptions = [new("shared", "Shared"), new("perButton", "Per button")];
    private static readonly FieldOption[] IconBarSizeSourceOptions = [new("shared", "Shared"), new("perRow", "Per row")];

    public static readonly FieldOption[] SpacingTokenOptions = ThemeTokenSelectionContract.Options("spacing");

    private static readonly FieldOption[] KeyboardHeightTokenOptions =
    [
        new("theme.keyboard.height", "keyboard.height"),
    ];

    private static readonly FieldOption[] KeyboardKeyGapTokenOptions =
    [
        new("theme.keyboard.keyGap", "keyboard.keyGap"),
    ];

    private static readonly FieldOption[] KeyboardRowGapTokenOptions =
    [
        new("theme.keyboard.rowGap", "keyboard.rowGap"),
    ];

    private static readonly FieldOption[] TextStyleOptions =
    [
        new("normal", "Normal"),
        new("italic", "Italic"),
    ];

    private static readonly FieldOption[] TextAlignOptions =
    [
        new("left", "Left"),
        new("center", "Center"),
        new("right", "Right"),
    ];

    private static readonly FieldOption[] SubtextVerticalPositionOptions =
    [
        new("top", "Top"),
        new("bottom", "Bottom"),
    ];

    public static readonly FieldOption[] BubbleStatusStateOptions =
    [
        new("none", "None"),
        new("sent", "Sent"),
        new("delivered", "Delivered"),
        new("read", "Read"),
    ];

    private static readonly FieldOption[] MediaTextOverlayModeOptions =
    [
        new("free", "Free text"),
        new("countUp", "Count up"),
        new("countDown", "Count down"),
    ];

    private static readonly FieldOption[] MediaTypeOptions =
    [
        new("none", "None"),
        new("image", "Image"),
        new("video", "Video"),
        new("audio", "Audio"),
    ];

    private static readonly FieldOption[] BubbleMediaPositionOptions =
    [
        new("top", "Top"),
        new("bottom", "Bottom"),
        new("left", "Left"),
        new("right", "Right"),
    ];

    private static readonly FieldOption[] DimensionModeOptions =
    [
        new("fixed", "Fixed size"),
        new("content", "Content + padding"),
    ];

    private static readonly FieldOption[] TextBoxDimensionModeOptions =
    [
        new("fixed", "Fixed size"),
        new("content", "Content + padding"),
        new("growVertical", "Grow vertical"),
    ];

    private static readonly FieldOption[] TextBoxOverflowOptions =
    [
        new("clip", "Clip"),
        new("scroll", "Scroll"),
    ];

    private static readonly FieldOption[] IconRowOrientationOptions =
    [
        new("horizontal", "Horizontal"),
        new("vertical", "Vertical"),
    ];

    private static readonly FieldOption[] IconRowItemSizingModeOptions =
    [
        new("content", "Content"),
        new("fillParent", "Fill parent"),
    ];

    private static readonly FieldOption[] SurfaceTailStyleOptions =
    [
        new("rounded_wedge", "Rounded wedge"),
        new("curved_hook", "Curved hook"),
        new("simple_triangle", "Simple triangle"),
        new("cut_corner", "Cut corner"),
    ];

    private static readonly FieldOption[] SurfaceTailSideOptions =
    [
        new("left", "Left"),
        new("right", "Right"),
    ];

    private static readonly FieldOption[] SurfaceTailVerticalOptions =
    [
        new("top", "Top"),
        new("bottom", "Bottom"),
    ];

    private static readonly ComponentInputBindingDefinition[] TextBoxParentInputBindings =
    [
        new(
            "sampleText",
            "Text",
            "sampleText",
            ValueKind.StringMultiline,
            ComponentInputBindingSource.Runtime,
            "Message"),
        new(
            "fixedSize",
            "Size",
            "size",
            ValueKind.IntegerPair,
            ComponentInputBindingSource.Calculated,
            "220|44",
            PairLabels: new PairFieldLabels("W", "H"),
            Number: new NumberDefinition(1, 9999, 1, 0)),
        new(
            "contentMaxWidth",
            "Max width",
            "maxWidth",
            ValueKind.Integer,
            ComponentInputBindingSource.Calculated,
            "220",
            Number: new NumberDefinition(1, 9999, 1, 0)),
        new(
            "growSize",
            "Size",
            "size",
            ValueKind.IntegerPair,
            ComponentInputBindingSource.Calculated,
            "220|44",
            PairLabels: new PairFieldLabels("W", "Min H"),
            Number: new NumberDefinition(1, 9999, 1, 0)),
    ];

    private static readonly ComponentInputBindingDefinition[] AvatarBadgeParentInputBindings =
    [
        new("showBadge", "Show badge", "showBadge", ValueKind.Boolean, ComponentInputBindingSource.Variant, "false", UiGroupId: "badge", UiGroupLabel: "Badge", UiOrder: 10),
        new("badgeContentMode", "Content", "badgeContentMode", ValueKind.OptionToken, ComponentInputBindingSource.Variant, "icon", Options: [new("icon", "Icon"), new("text", "Text")], UiGroupId: "badge", UiGroupLabel: "Badge", UiOrder: 20),
        new("badgeIconToken", "Icon", "badgeIconToken", ValueKind.IconToken, ComponentInputBindingSource.Variant, "system_check", UiGroupId: "badge", UiGroupLabel: "Badge", UiOrder: 30),
        new("badgeText", "Text", "badgeText", ValueKind.StringSingleLine, ComponentInputBindingSource.Variant, "1", UiGroupId: "badge", UiGroupLabel: "Badge", UiOrder: 40),
        new("badgeSize", "Size", "badgeSize", ValueKind.Integer, ComponentInputBindingSource.Variant, "20", Number: new NumberDefinition(1, 512, 1, 0), UiGroupId: "badge", UiGroupLabel: "Badge", UiOrder: 50),
        new("badgeBackgroundPaletteColor", "Background", "badgeBackgroundPaletteColor", ValueKind.PaletteColorToken, ComponentInputBindingSource.Variant, "blue", UiGroupId: "badge", UiGroupLabel: "Badge", UiOrder: 60),
        new("badgeContentPaletteColor", "Icon / text color", "badgeContentPaletteColor", ValueKind.PaletteColorToken, ComponentInputBindingSource.Variant, "gray_100", UiGroupId: "badge", UiGroupLabel: "Badge", UiOrder: 70),
    ];

    private static readonly ComponentInputBindingDefinition[] NotificationsBaseInputBindings =
    [
        new(
            "maxWidth",
            "Max width %",
            "maxWidth",
            ValueKind.Integer,
            ComponentInputBindingSource.Variant,
            "90",
            Number: new NumberDefinition(1, 100, 1, 0)),
    ];

    private static readonly FieldOption[] PressedEffectOptions =
    [
        new("popup", "Popup"),
        new("scale", "Scale in place"),
        new("none", "None"),
    ];

    private static readonly RuntimeInputCollectionDefinition KeypadKeysCollection = new(
        "keys",
        "Keys",
        "keys",
        "Key",
        [
            new ComponentInputDefinition(
                "kind", "Kind", "kind", ComponentInputKind.Option, ValueKind.OptionToken, "text",
                [new("text", "Text"), new("icon", "Icon"), new("spacer", "Spacer")]),
            new ComponentInputDefinition(
                "value", "Value", "value", ComponentInputKind.Text, ValueKind.StringSingleLine, "",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["text", "icon"]),
            new ComponentInputDefinition(
                "text", "Text", "text", ComponentInputKind.Text, ValueKind.StringSingleLine, "",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["text"]),
            new ComponentInputDefinition(
                "subtext", "Subtext", "subtext", ComponentInputKind.Text, ValueKind.StringSingleLine, "",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["text"]),
            new ComponentInputDefinition(
                "iconToken", "Icon", "iconToken", ComponentInputKind.Icon, ValueKind.IconToken, "app_clock",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["icon"]),
            new ComponentInputDefinition(
                "disabled", "Disabled", "disabled", ComponentInputKind.Boolean, ValueKind.Boolean, "false",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["text", "icon"]),
        ],
        ItemPresentation: new RuntimeInputCollectionItemPresentation(
            "",
            "",
            ["text", "subtext", "value"],
            72,
            "iconToken",
            "keypad",
            new Dictionary<string, string>()));

    private static readonly RuntimeInputCollectionDefinition StatusBarItemsCollection = new(
        "statusBarItems",
        "Items",
        "items",
        "Status item",
        [
            new ComponentInputDefinition(
                "label", "Label", "label", ComponentInputKind.Text, ValueKind.StringReadOnly, "",
                ShowInEditor: false),
            new ComponentInputDefinition(
                "kind", "Kind", "kind", ComponentInputKind.Text, ValueKind.StringReadOnly, "",
                ShowInEditor: false),
            new ComponentInputDefinition(
                "textValue", "Value", "value", ComponentInputKind.Text, ValueKind.StringSingleLine, "",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["text"]),
            new ComponentInputDefinition(
                "signalValue", "Signal", "value", ComponentInputKind.Number, ValueKind.Integer, "4",
                Minimum: 0, Maximum: 4,
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["generatedSignal"]),
            new ComponentInputDefinition(
                "batteryValue", "Battery %", "value", ComponentInputKind.Number, ValueKind.Integer, "85",
                Minimum: 0, Maximum: 100,
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["generatedBattery"]),
            new ComponentInputDefinition(
                "token", "Icon token", "token", ComponentInputKind.Icon, ValueKind.IconToken, "",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["iconToken"]),
            new ComponentInputDefinition(
                "charging", "Charging", "charging", ComponentInputKind.Boolean, ValueKind.Boolean, "false",
                EnabledWhenItemJsonKey: "kind", EnabledWhenItemValues: ["generatedBattery"]),
            new ComponentInputDefinition(
                "zone", "Zone", "zone", ComponentInputKind.Option, ValueKind.OptionToken, "off",
                [new("off", "Off"), new("left", "Left"), new("right", "Right")]),
            new ComponentInputDefinition(
                "order", "Order", "order", ComponentInputKind.Number, ValueKind.Integer, "0",
                Minimum: 0, Maximum: 10000),
        ],
        ItemPresentation: new RuntimeInputCollectionItemPresentation(
            "label",
            "",
            ["kind"],
            72,
            "",
            "status",
            new Dictionary<string, string>()),
        CanEditStructure: false);

    private static readonly RuntimeInputCollectionDefinition NavigationBarItemsCollection = new(
        "navigationBarItems",
        "Button Items",
        "items",
        "Navigation item",
        [
            new ComponentInputDefinition(
                "label", "Label", "label", ComponentInputKind.Text, ValueKind.StringReadOnly, "",
                ShowInEditor: false),
            new ComponentInputDefinition(
                "kind", "Kind", "kind", ComponentInputKind.Text, ValueKind.StringReadOnly, "",
                ShowInEditor: false),
            new ComponentInputDefinition(
                "zone", "Zone", "zone", ComponentInputKind.Option, ValueKind.OptionToken, "off",
                [new("off", "Off"), new("left", "Left"), new("center", "Center"), new("right", "Right")]),
            new ComponentInputDefinition(
                "order", "Order", "order", ComponentInputKind.Number, ValueKind.Integer, "0",
                Minimum: 0, Maximum: 10000),
        ],
        ItemPresentation: new RuntimeInputCollectionItemPresentation(
            "label",
            "",
            ["kind"],
            72,
            "",
            "navigation",
            new Dictionary<string, string>()),
        CanEditStructure: false);

    private static readonly FieldOption[] KeyboardLanguageOptions =
    [
        new("es", "Spanish"),
        new("en", "English"),
    ];

    private static readonly FieldOption[] KeyboardIconRowPlacementOptions =
    [
        new("top", "Top"),
        new("bottom", "Bottom"),
    ];

    private static readonly FieldOption[] NavigationBarTypeOptions =
    [
        new("buttons", "Buttons"),
        new("gestureBar", "Gesture Bar"),
    ];

    private static readonly Dictionary<string, ComponentClassFieldDescriptor> Fields = new(StringComparer.Ordinal)
    {
        ["component.type"] = new("component.type", "Component Type", ValueKind.StringReadOnly, [], "", false),
        ["component.style.shadowEnabled"] = new("component.style.shadowEnabled", "Shadow", ValueKind.Boolean, ["style", "shadowEnabled"], "false"),
        ["component.style.reliefEnabled"] = new("component.style.reliefEnabled", "Relief", ValueKind.Boolean, ["style", "reliefEnabled"], "false"),
        ["component.style.borderWidth"] = new("component.style.borderWidth", "Border width", ValueKind.Integer, ["style", "borderWidth"], "0"),
        ["component.style.borderColorToken"] = new("component.style.borderColorToken", "Border color", ValueKind.ThemeToken, ["style", "borderColorToken"], "theme.borders.primary", ThemeTokenFamily: "colors"),
        ["component.style.cornerRadiusToken"] = new("component.style.cornerRadiusToken", "Corner radius", ValueKind.ThemeToken, ["style", "cornerRadiusToken"], "theme.radii.xl", ThemeTokenFamily: "radii"),
        ["component.style.reliefAngle"] = new("component.style.reliefAngle", "Relief angle", ValueKind.Integer, ["style", "reliefAngle"], "-45"),
        ["component.style.reliefExtent"] = new(
            "component.style.reliefExtent",
            "Relief extent",
            ValueKind.Decimal,
            ["style", "reliefExtent"],
            "1",
            Number: new NumberDefinition(0, 12, 0.05m, 2)),
        ["component.style.reliefSpread"] = new(
            "component.style.reliefSpread",
            "Relief spread",
            ValueKind.Decimal,
            ["style", "reliefSpread"],
            "0",
            Number: new NumberDefinition(0, 12, 0.05m, 2)),
        ["component.style.reliefTopIntensity"] = new(
            "component.style.reliefTopIntensity",
            "Relief top",
            ValueKind.Decimal,
            ["style", "reliefTopIntensity"],
            "0.12",
            Number: new NumberDefinition(-1, 1, 0.05m, 2)),
        ["component.style.reliefBottomIntensity"] = new(
            "component.style.reliefBottomIntensity",
            "Relief bottom",
            ValueKind.Decimal,
            ["style", "reliefBottomIntensity"],
            "-0.1",
            Number: new NumberDefinition(-1, 1, 0.05m, 2)),

    };

    public static ComponentClassFieldDescriptor Get(string fieldId)
    {
        if (Fields.TryGetValue(fieldId, out var field))
        {
            return field;
        }

        throw new InvalidOperationException($"Unknown component class field '{fieldId}'.");
    }

    public static IReadOnlyList<ComponentClassFieldDescriptor> All() => Fields.Values.ToList();

    public static void ValidateCurrentValue(
        ComponentClassFieldDescriptor descriptor,
        JsonNode value,
        string owner)
    {
        RuntimeInputValueKindContract.ValidateValue(
            descriptor.ValueKind,
            value,
            owner);
        if (descriptor.ValueKind == ValueKind.OptionToken)
        {
            FieldOptionContract.ValidateValue(
                FieldOptionContract.RequireOptions(
                    descriptor.Options,
                    owner),
                value.GetValue<string>(),
                owner);
        }
        ScalarValuePatternContract.Validate(
            descriptor.ValuePattern,
            descriptor.ValuePatternMessage,
            value,
            owner);
    }

    public static void ValidateConfigOptionValues(
        string recordClassId,
        JsonObject config,
        string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordClassId);
        ArgumentNullException.ThrowIfNull(config);
        foreach (var descriptor in Fields.Values.Where((field) =>
                     field.ValueKind == ValueKind.OptionToken
                     && field.Id.StartsWith(
                         $"{recordClassId}.",
                         StringComparison.Ordinal)))
        {
            var value = JsonPath.Get(config, descriptor.JsonPath);
            if (value is null)
            {
                continue;
            }
            ValidateCurrentValue(
                descriptor,
                value,
                $"{owner} field '{descriptor.Id}'");
        }
    }

    private static void ValidateOptionContracts()
    {
        foreach (var field in Fields.Values)
            ThemeTokenSelectionContract.Validate(field.ValueKind, field.ThemeTokenFamily, field.Options, field.Id);

        foreach (var field in Fields.Values.Where((field) =>
                     field.ValueKind == ValueKind.OptionToken))
        {
            _ = FieldOptionContract.RequireOptions(
                field.Options,
                $"Component field '{field.Id}'");
        }
    }

    public static bool IsRuntimeOverrideField(string fieldId) =>
        Fields.TryGetValue(fieldId, out var field)
        && field.IsEditable
        && field.JsonPath.Length > 0;
}
