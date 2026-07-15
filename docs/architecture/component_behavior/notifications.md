# Notification and Notifications

Status: structural components built on the generic component composition route.

## Notification

Notification is one reusable item. Its Variant owns only composition:

- concrete Avatar Variant plus local overrides;
- concrete Label Variant plus local overrides;
- Avatar position (`start` or `end`);
- tokenized gap between Avatar and Label.

Runtime Inputs own the notification data: Actor, text and subtext. The Actor is
resolved through the ordinary record-reference path and passed explicitly to
Avatar. Label receives final literal text/subtext values. Notification owns the
horizontal arrangement and emits only generic child renderables.

## Notifications

Notifications owns one embedded Collection Stack Variant. Its public Runtime
Inputs reproduce that child's runtime contract: Stacked/Flow distribution,
tokenized boundaries and offsets, plus the ordered `items` collection. The
collection's Component selector is restricted to Notification Variants.

Each item stores a stable id, full Notification Variant reference, local
overrides and Notification runtime inputs. Notifications does not inspect Actor,
text or Avatar fields inside an item; it passes the complete values to the
embedded Collection Stack, which invokes the ordinary component registry.

The default contract uses Stacked + Fit content. Later notification lifecycle,
expanded/collapsed behavior and enter/exit timing must be resolved by the
generic item-presence contract before preview. Neither component owns a timer.
