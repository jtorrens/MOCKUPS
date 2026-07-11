# System Bars

Status: active system components. Status Bar and Navigation Bar have separate
contracts/resolvers/renderables but follow the same preview boundary.

Source of truth: `src/desktop-preview/statusBarComponentContract.ts`,
`statusBarComponentResolver.ts`, `statusBarComponentRenderable.ts`,
`navigationBarComponentContract.ts`, `navigationBarComponentResolver.ts` and
`navigationBarComponentRenderable.ts`.

## Status Bar

### Purpose and ownership

Status Bar renders device status chrome from ordered left/right item zones. It
owns item filtering, zone sorting and the bar's internal layout. Conversation
owns final screen placement and selected variant/configuration.

### Runtime inputs and configuration

Runtime item values may be text, booleans/numbers, icons/tokens, order, zone
and charging state. Variant/configuration defines foreground/background theme
tokens, background alpha, height, item size, side padding and gap.

### Layout, states and motion

Items with zone `off` are hidden; blank text items are hidden. Visible items
sort by order within left/right zones. Background alpha is part of the bar
contract, allowing header content to show through. There is no independent
motion/time source.

### Parent vs local resolution

Parent selects the variant and supplies relevant values. Status Bar resolves
item visibility/order and generic bar atoms.

## Navigation Bar

### Purpose and ownership

Navigation Bar renders device navigation chrome in button or gesture-bar mode.
It owns item filtering, left/center/right zones and its internal geometry.

### Runtime inputs and configuration

Runtime item values supply zone/order/value/token/charging metadata.
Variant/configuration supplies type, foreground/background theme tokens,
background alpha, bar height, item size, side padding, stroke/corner/filled
button style and gesture geometry.

### Layout, states and motion

Items with zone `off` are omitted and remaining items sort by order in each
zone. Gesture and button forms resolve from the selected type. There is no
independent motion/time source.

### Parent vs local resolution

Conversation selects/configures Navigation Bar and places it above Keyboard.
Navigation Bar resolves local item geometry and final generic atoms.

## Shared limitations

- System bars are normal components in the active registry; the `system`
  category is organizational only, not a separate preview path.
- Their background/foreground must come from their own component tokens and
  alpha fields, not from extra theme-only special cases.
