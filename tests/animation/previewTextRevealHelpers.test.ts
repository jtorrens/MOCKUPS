import assert from "node:assert/strict";
import test from "node:test";
import {
  simpleWriteOnFrameText,
  simpleWriteOnFrameVisibleCount,
} from "../../src/desktop-preview/previewTextRevealHelpers.js";

test("write-on keeps frame zero empty and reveals the first grapheme at frame one", () => {
  const plan = { enabled: true, durationFrames: 100 };
  assert.equal(simpleWriteOnFrameText("AB", { ...plan, frame: 0 }), "");
  assert.equal(simpleWriteOnFrameText("AB", { ...plan, frame: 1 }), "A");
  assert.equal(simpleWriteOnFrameText("AB", { ...plan, frame: 99 }), "A");
  assert.equal(simpleWriteOnFrameText("AB", { ...plan, frame: 100 }), "AB");
});

test("write-on distributes complete graphemes through the track end", () => {
  const text = "A👨‍👩‍👧‍👦éZ";
  const counts = Array.from({ length: 5 }, (_, frame) =>
    simpleWriteOnFrameVisibleCount(text, {
      enabled: true,
      frame,
      durationFrames: 4,
    }));
  assert.deepEqual(counts, [0, 1, 2, 3, 4]);
  assert.equal(simpleWriteOnFrameText("🙂", {
    enabled: true,
    frame: 1,
    durationFrames: 100,
  }), "🙂");
});
