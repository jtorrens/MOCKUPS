# Label

Status: functional atom on the component resolver -> renderable route.

Source of truth: `src/desktop-preview/labelComponentContract.ts`,
`labelComponentResolver.ts`, `labelComponentRenderable.ts` and
`calculatedText.ts`.

## Runtime inputs

Label exposes two independent text lanes. Text and Subtext each declare:

- a string value;
- a content source: `literal`, `countUp` or `countDown`;
- a decimal size multiplier with default `1`.

The multiplier is applied after the Variant typography size token resolves. It
scales font size, line height and measurement together, so content-sized labels
and final rendering use the same geometry. The supported authored range is
`0.1` through `20`.

At a parent boundary these remain ordinary child runtime inputs. The parent
binds them as Variant values unless its designer explicitly activates Forward.

## Calculated text

`literal` returns the authored string unchanged. `countUp` and `countDown`
interpret the string as both initial value and format:

- `4:23` uses `M:SS`;
- `04:23` uses `MM:SS` and retains a minimum two-digit minute field.

Seconds must contain exactly two digits in the `00` through `59` range. An
invalid value is a resolver error; there is no implicit conversion or literal
fallback. Count down clamps at `0:00`.

When a designer changes a Content source from `literal` to either counter mode,
the generic component-input transition contract checks the related text value.
If that value is Forward and does not already match `M:SS` or `MM:SS`, the edit
explicitly replaces both its Variant value and current parent runtime test value
with `00:00`. This is an editor transaction, not a resolver fallback. Valid
counter values are preserved, and malformed values arriving through any other
route remain resolver errors.

The resolver derives elapsed whole seconds from the explicit owner-local frame
and project frame rate in the preview payload. Frame zero returns the authored
initial value. No timer, CSS animation or renderer clock participates. The
renderable receives only the resolved text for the requested frame.

The source discriminator is intentionally extensible so future calculated text
kinds can be added without changing the literal fields or preview boundary.

## Variant data

The concrete Label Variant owns content/fixed sizing, tokenized padding, text
and subtext colors, typography, alignment, text gap, subtext vertical position
(`Top`/`Bottom`), subtext horizontal alignment (`Left`/`Center`/`Right`) and the
embedded Surface Variant. It also owns one `Text shadow` switch. When enabled,
the Theme default shadow is resolved once and applied to both Text and Subtext
glyphs; it never affects the Surface or layout measurement. The field follows
the normal embedded Variant/Override route and is not Runtime by default.
Horizontal alignment uses the measured bounds of the
primary text, not the complete Label frame. The tokenized text gap is the exact
vertical separation. Runtime multipliers do not extend the Theme
typography token vocabulary.

The retired generic subtext placement is migrated explicitly. Its X alignment
and offsets are discarded, horizontal alignment becomes `Center`, and `alignY`
values below `0.5` become `Top`; values at or above `0.5` become `Bottom`.
There is no compatibility fallback for the retired field.

`reserveSubtextSpace` is an explicit Variant boolean. When enabled, an empty
subtext still participates in Label measurement using the configured subtext
line height, relative alignment and gap, while no empty text node is painted. This keeps
primary text aligned with sibling labels that contain subtext without relying
on whitespace content. Keypad enables it in its state-owned Label slots.
