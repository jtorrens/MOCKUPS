import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { chromium } from 'playwright';

// Real generated WebView document, real decoded images and actual painted pixels.
// The only controlled dependency is the completion of the HTTP image request.
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 500, height: 900 } });
  page.setDefaultTimeout(10000);
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.exposeFunction('invokeCSharpAction', () => {});
  await page.setContent(await readFile(process.argv[2], 'utf8'));
  const viewport = page.locator('#previewViewport');
  const painted = () => viewport.screenshot({ animations: 'disabled' });
  const baseline = await painted();
  let releaseImage;
  let imageRequested;
  const requested = new Promise(resolve => { imageRequested = resolve; });
  const release = new Promise(resolve => { releaseImage = resolve; });
  await page.route('http://preview.test/slow.svg', async route => {
    imageRequested();
    await release;
    await route.fulfill({ contentType: 'image/svg+xml', body:
      '<svg xmlns="http://www.w3.org/2000/svg" width="360" height="800"><rect width="360" height="800" fill="lime"/></svg>' });
  });
  const slow = await page.evaluate(() => window.mockupsSetPreviewBody(
    '<div data-renderable-id="owner" style="width:100%;height:100%;background:lime">A2<img src="http://preview.test/slow.svg"></div>'));
  await requested;
  assert.ok((await painted()).equals(baseline), 'same-owner frame must retain its painted pixels while its new image loads');

  await page.evaluate(() => window.mockupsDiscardPreviewBody());
  assert.equal(await page.locator('#previewScale [data-renderable-id="owner"]').count(), 0);
  const empty = await painted();
  assert.ok(!empty.equals(baseline), 'owner departure must remove the previous painted frame');
  const next = await page.evaluate(() => window.mockupsSetPreviewBody(
    '<div data-renderable-id="owner-b" style="width:100%;height:100%;background:blue">B</div>'));
  await page.waitForFunction(id => window.mockupsPreviewPatchStatus(id) === 'commit', next);
  const blue = await painted();
  releaseImage();
  await page.waitForFunction(id => window.mockupsPreviewPatchStatus(id) === 'stale', slow);
  assert.ok((await painted()).equals(blue), 'a delayed frame from A must never overwrite B');

  // Re-entry creates a new presentation, and transparent frames clear normally.
  await page.evaluate(() => window.mockupsDiscardPreviewBody());
  const reentry = await page.evaluate(() => window.mockupsSetPreviewBody(
    '<div data-renderable-id="owner" style="width:100%;height:100%;background:red">A</div>'));
  await page.waitForFunction(id => window.mockupsPreviewPatchStatus(id) === 'commit', reentry);
  assert.ok((await painted()).equals(baseline));
  const readyMorph = await page.evaluate(() => window.mockupsSetPreviewBody(
    '<div data-renderable-id="owner" style="width:100%;height:100%;background:lime">A2<img src="http://preview.test/slow.svg"></div>'));
  await page.waitForFunction(id => window.mockupsPreviewPatchStatus(id) === 'commit', readyMorph);
  assert.ok(!(await painted()).equals(baseline), 'a ready same-owner image must publish its changed pixels');
  assert.equal(await page.locator('#previewScale [data-renderable-id="owner"]').count(), 1);
  assert.equal(await page.locator('#previewScale [data-renderable-id="owner"] img').evaluate(image =>
    image.complete && image.naturalWidth === 360), true);
  const transparent = await page.evaluate(() => window.mockupsSetPreviewBody(''));
  await page.waitForFunction(id => window.mockupsPreviewPatchStatus(id) === 'commit', transparent);
  assert.ok((await painted()).equals(empty));
  assert.deepEqual(errors, [], 'the real Preview script must not throw');
  console.log('PASS BROWSER: delayed frame holds pixels; owner clear; stale completion; re-entry; ready morph; transparent frame');
} finally { await browser.close(); }
