import type { JsonRecord } from "./previewJsonHelpers.js";
import type {
  ComponentMotionContract,
  ComponentMotionFrameContract,
} from "./previewComponentContracts.js";

export type ConversationIncomingRevealMode = "writeOn" | "typingIndicator";
export type ConversationTypingIndicatorAnimation = "none" | "pulsating" | "wave";

export interface ConversationTimingContract {
  bubbleRevealMode: "duringWriteOn" | "afterWriteOn";
  incomingRevealMode: ConversationIncomingRevealMode;
  showTextInput: boolean;
  keyboardVisible: boolean;
  typingIndicatorText: string;
  typingIndicatorSizeToken: string;
  typingIndicatorAnimation: ConversationTypingIndicatorAnimation;
}

export interface ConversationComposerContract {
  text: string;
  currentCharacter: number;
  textInputVisible: boolean;
  keyboardVisible: boolean;
}

export interface ConversationMessageContract {
  id: string;
  actor: JsonRecord;
  actorIdentityVisible: boolean;
  state: string;
  text: string;
  statusState: string;
  statusText: string;
  writeOnDurationFrames: number;
  writeOnTrigger: boolean;
  writeOnFrame: number;
  keepCursorAfterWrite: boolean;
  statusVisible: boolean;
  visibleAtFrame: number;
  mediaType: "none" | "image" | "video" | "audio";
  mediaSource: string;
  viewportSize: string;
  mediaScale: number;
  mediaOffset: string;
  isPlaying: boolean;
  playbackTimeSeconds: number;
  durationSeconds: number;
  playbackMode: "once" | "loop";
  isFullScreen: boolean;
  fullScreenTransition: boolean;
  fullScreenMotionElapsedMs: number;
  fullframeOrientation: string;
  controlsElapsedMs: number;
  isTypingIndicator: boolean;
  presenceMotion: ComponentMotionContract;
  presenceMotionKind?: "enter" | "exit";
  presenceMotionFrame?: ComponentMotionFrameContract;
}

export interface ConversationModuleContract {
  id: "conversation";
  preview: JsonRecord;
  conversationType: "individual" | "group";
  frame: number;
  motionElapsedMs: number;
  timing: ConversationTimingContract;
  composer: ConversationComposerContract;
  messages: JsonRecord[];
  visibleMessages: ConversationMessageContract[];
  viewportMotionProgress: number;
  messageReflow?: {
    progress: number;
    fromMessages: ConversationMessageContract[];
  };
  textInputConfig?: JsonRecord;
}
