import type { ResolvedChatScreenProps } from "../../../domain/schemas/index.js";
import { readNumber, readString } from "../../renderable/helpers.js";
import type { VisualModule } from "../types.js";

export interface StatusBarModuleInput {
  frame: number;
  viewport: ResolvedChatScreenProps["viewport"];
  statusBarHeight: number;
  state: ResolvedChatScreenProps["deviceState"];
  tokens: ResolvedChatScreenProps["theme"]["statusBar"];
}

export const StatusBarModule: VisualModule<StatusBarModuleInput> = {
  type: "status_bar",
  version: 1,
  render(input) {
    const foreground = readString(input.tokens, "foreground", "#000000");
    const background = readString(input.tokens, "background", "transparent");
    const iconScale = readNumber(input.tokens, "iconScale", 1);
    return {
      id: "status_bar",
      type: "status_bar",
      role: "device_status",
      frame: input.frame,
      box: {
        x: input.viewport.x,
        y: input.viewport.y,
        width: input.viewport.width,
        height: input.statusBarHeight,
      },
      style: { foreground, background },
      children: [
        {
          id: "status_bar:time",
          type: "text",
          role: "time",
          frame: input.frame,
          text: input.state.time,
          style: { color: foreground },
        },
        {
          id: "status_bar:connectivity",
          type: "status_indicators",
          role: "connectivity_and_battery",
          frame: input.frame,
          style: { color: foreground, iconScale },
          metadata: {
            signalBars: input.state.signalBars,
            networkLabel: input.state.networkLabel,
            batteryLevel: input.state.batteryLevel,
            batteryCharging: input.state.batteryCharging,
            wifiEnabled: input.state.wifiEnabled,
            wifiIconState: input.state.wifiIconState,
          },
        },
      ],
      metadata: {
        layout: "approximate_status_bar",
        tokenSource: "theme.tokens_json.statusBar",
        stateSource: "device_states.state_json",
        iconGlyphs: "renderer_specific_not_selected",
      },
    };
  },
};
