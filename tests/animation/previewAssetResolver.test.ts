import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import type { DesignPreviewPayload } from "../../src/desktop-preview/designPreviewPayload.js";
import {
  iconUriForToken,
  mediaFrameUriForPath,
} from "../../src/desktop-preview/previewAssetResolver.js";

const payload = {
  projectMediaRoot: "/project",
} as DesignPreviewPayload;

test("Icon resolution preserves explicit absence without fabricating a mapping", () => {
  assert.equal(iconUriForToken(payload, "send"), "");
  assert.equal(iconUriForToken({
    ...payload,
    iconMappingJson: JSON.stringify({ tokens: {} }),
  }, "send"), "");
});

test("Present Icon Theme mappings require exact token documents", () => {
  assert.throws(
    () => iconUriForToken({ ...payload, iconMappingJson: "{}" }, "send"),
    /Missing object value icon mapping\.tokens/,
  );
  assert.throws(
    () => iconUriForToken({
      ...payload,
      iconMappingJson: JSON.stringify({ tokens: [] }),
    }, "send"),
    /Missing object value icon mapping\.tokens/,
  );
  assert.throws(
    () => iconUriForToken({
      ...payload,
      iconMappingJson: JSON.stringify({ tokens: { send: [] } }),
    }, "send"),
    /Missing object value icon mapping\.tokens\.send/,
  );
  assert.throws(
    () => iconUriForToken({
      ...payload,
      iconMappingJson: JSON.stringify({ tokens: { send: {} } }),
    }, "send"),
    /Missing string value icon mapping\.tokens\.send\.file/,
  );
});

test("Icon files remain explicit safe SVG filenames under an exact asset root", () => {
  for (const file of ["send.png", "nested/send.svg", "nested\\send.svg"]) {
    assert.throws(
      () => iconUriForToken({
        ...payload,
        iconAssetRoot: "icon-themes/example",
        iconMappingJson: JSON.stringify({ tokens: { send: { file } } }),
      }, "send"),
      /Invalid local SVG file/,
    );
  }
  assert.throws(
    () => iconUriForToken({
      ...payload,
      iconMappingJson: JSON.stringify({ tokens: { send: { file: "send.svg" } } }),
    }, "send"),
    /Missing Icon Theme asset root for token send/,
  );
  assert.equal(iconUriForToken({
    ...payload,
    iconAssetRoot: "icon-themes/missing",
    iconMappingJson: JSON.stringify({ tokens: { send: { file: "send.svg" } } }),
  }, "send"), "");
});

test("HEIF assets publish oriented image geometry for cover crop", () => {
  const directory = mkdtempSync(path.join(os.tmpdir(), "mockups-heif-size-"));
  const file = path.join(directory, "photo.heic");
  try {
    writeFileSync(file, Buffer.concat([
      heifSpatialExtent(320, 240),
      heifSpatialExtent(4032, 3024),
      heifSpatialExtent(768, 576),
      heifRotation(3),
    ]));
    const frame = mediaFrameUriForPath({ projectMediaRoot: directory } as DesignPreviewPayload, file, 0);
    assert.equal(frame.width, 3024);
    assert.equal(frame.height, 4032);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

function heifSpatialExtent(width: number, height: number) {
  const box = Buffer.alloc(20);
  box.writeUInt32BE(20, 0);
  box.write("ispe", 4, "ascii");
  box.writeUInt32BE(width, 12);
  box.writeUInt32BE(height, 16);
  return box;
}

function heifRotation(quarterTurns: number) {
  const box = Buffer.alloc(9);
  box.writeUInt32BE(9, 0);
  box.write("irot", 4, "ascii");
  box.writeUInt8(quarterTurns & 0x03, 8);
  return box;
}

function withVideo(args: string[], verify: (file: string) => void) {
  const directory = mkdtempSync(path.join(os.tmpdir(), "mockups-video-timeline-"));
  const file = path.join(directory, "source.mov");
  try {
    execFileSync("ffmpeg", ["-v", "error", ...args, "-an", file], { timeout: 10000 });
    verify(file);
  } finally { rmSync(directory, { recursive: true, force: true }); }
}

function exactDecodedFrame(file: string, index: number) {
  // Independent oracle: decode from the beginning and select by display ordinal,
  // without using the resolver's timestamp selection or input seek.
  const jpeg = execFileSync("ffmpeg", ["-v", "error", "-i", file,
    "-map", "0:v:0", "-vf", `select=eq(n\\,${index})`, "-fps_mode", "vfr",
    "-frames:v", "1", "-an", "-q:v", "4", "-c:v", "mjpeg", "-f", "image2pipe", "pipe:1"]);
  assert.ok(jpeg.length > 0);
  return `data:image/jpeg;base64,${jpeg.toString("base64")}`;
}

function resolvedVideo(file: string, time: number) {
  const frame = mediaFrameUriForPath(payload, file, time);
  assert.equal(frame.error, undefined, `Video at ${time}s`);
  assert.ok(frame.uri);
  return frame.uri;
}

test("short ProRes holds its actual last frame on a cold out-of-range request", () => {
  withVideo(["-f", "lavfi", "-i", "testsrc2=size=64x48:rate=25", "-frames:v", "67",
    "-c:v", "prores_ks", "-profile:v", "3", "-pix_fmt", "yuv422p10le"], file => {
    const last = exactDecodedFrame(file, 66);
    for (const time of [20, 2.679, 2.68, 2.64]) assert.equal(resolvedVideo(file, time), last);
    const first = exactDecodedFrame(file, 0);
    assert.notEqual(first, last);
    assert.equal(resolvedVideo(file, 0), first);
    assert.equal(resolvedVideo(file, -1), first);
    assert.equal(resolvedVideo(file, 2.639), exactDecodedFrame(file, 65));
    assert.equal(resolvedVideo(file, 30), last);
  });
});

test("fractional-rate interframe video selects actual frames without millisecond rounding", () => {
  withVideo(["-f", "lavfi", "-i", "testsrc2=size=64x48:rate=30000/1001", "-frames:v", "17",
    "-c:v", "mpeg4", "-bf", "2"], file => {
    for (const index of [16, 1, 7, 0]) {
      const time = index * 1001 / 30000;
      assert.equal(resolvedVideo(file, time), exactDecodedFrame(file, index));
      assert.equal(resolvedVideo(file, time + 0.001), exactDecodedFrame(file, index));
    }
    assert.equal(resolvedVideo(file, 100), exactDecodedFrame(file, 16));
  });
});

test("variable-rate video uses display intervals and its own nonzero timestamp origin", () => {
  withVideo(["-f", "lavfi", "-i", "testsrc2=size=64x48:rate=25", "-frames:v", "4",
    "-vf", "select=eq(n\\,0)+eq(n\\,1)+eq(n\\,4)+eq(n\\,8)",
    "-fps_mode", "vfr", "-c:v", "prores_ks", "-output_ts_offset", "5"], file => {
    for (const [time, index] of [[20, 3], [0, 0], [0.039, 0], [0.04, 1],
      [0.159, 1], [0.16, 2], [0.319, 2], [0.32, 3]]) {
      assert.equal(resolvedVideo(file, time!), exactDecodedFrame(file, index!));
    }
  });
});

test("single-frame video holds that frame and does not replace genuine black content", () => {
  withVideo(["-f", "lavfi", "-i", "color=c=black:size=64x48:rate=25", "-frames:v", "1",
    "-c:v", "prores_ks"], file => {
    const black = exactDecodedFrame(file, 0);
    for (const time of [12, 0.039, 0]) assert.equal(resolvedVideo(file, time), black);
  });
});

test("an authored black tail is not replaced with an earlier nonblack frame", () => {
  withVideo(["-f", "lavfi", "-i", "testsrc2=size=64x48:rate=25", "-frames:v", "5",
    "-vf", "drawbox=x=0:y=0:w=iw:h=ih:color=black:t=fill:enable=gte(n\\,3)",
    "-c:v", "prores_ks"], file => {
    const first = resolvedVideo(file, 0);
    const last = resolvedVideo(file, 2);
    assert.notEqual(last, first);
    assert.equal(last, exactDecodedFrame(file, 4));
  });
});

test("replacing a video invalidates both its frame timeline and extracted images", () => {
  withVideo(["-f", "lavfi", "-i", "testsrc2=size=64x48:rate=25", "-frames:v", "5",
    "-c:v", "prores_ks"], file => {
    const oldLast = resolvedVideo(file, 2);
    execFileSync("ffmpeg", ["-v", "error", "-f", "lavfi", "-i",
      "color=c=red:size=64x48:rate=25", "-frames:v", "2", "-c:v", "prores_ks", "-an", "-y", file],
    { timeout: 10000 });
    const currentLast = resolvedVideo(file, 2);
    assert.notEqual(currentLast, oldLast);
    assert.equal(currentLast, exactDecodedFrame(file, 1));
  });
});

test("video probe failure is nonblocking and is never cached as a guessed duration", () => {
  withVideo(["-f", "lavfi", "-i", "testsrc2=size=64x48:rate=25", "-frames:v", "2",
    "-c:v", "prores_ks"], file => {
    const previous = process.env.MOCKUPS_FFPROBE;
    try {
      process.env.MOCKUPS_FFPROBE = process.execPath;
      assert.deepEqual(mediaFrameUriForPath(payload, file, 5), { uri: "", error: "Error al leer media" });
    } finally {
      if (previous === undefined) delete process.env.MOCKUPS_FFPROBE;
      else process.env.MOCKUPS_FFPROBE = previous;
    }
    assert.equal(resolvedVideo(file, 5), exactDecodedFrame(file, 1));
  });
});
