import { resolveCollectionStackComponent } from "./collectionStackComponentResolver.js";
import { componentPresetConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
import { asRecord, parseObject, requiredString } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { NotificationsDesignContract } from "./notificationsComponentContract.js";

export function resolveNotificationsComponent(payload: DesignPreviewPayload): NotificationsDesignContract {
  const config = parseObject(payload.configJson);
  const bases = parseObject(payload.componentBaseConfigsJson);
  const slot = asRecord(asRecord(config.notifications).collectionStackSlot);
  const stackConfig = mergeComponentDefaults(
    componentPresetConfig(bases, "collectionStack", requiredString(slot, "presetId", "component.notifications.collectionStackSlot.presetId")),
    asRecord(slot.overrides),
  );
  return {
    id: "component.notifications",
    stack: resolveCollectionStackComponent({
      ...payload,
      componentType: "collectionStack",
      configJson: JSON.stringify(stackConfig),
    }),
  };
}
