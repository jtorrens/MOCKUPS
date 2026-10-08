import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
type Definition = { provider?: string; stroke?: number; style?: string; weight?: number };
type Prepared = { setId: string; svg: string; provider: string; sourceName: string; copied: boolean };
const { prepareToken } = require('../../scripts/icon-themes/sync-icon-theme-token.cjs') as {
  prepareToken: (request: unknown, load: (provider: string, name: string, def: Definition) => string | null) => { sets: Prepared[] };
};

const svg = (label: string) => `<svg viewBox="0 0 24 24"><path id="${label}" d="M0 0h24v24z"/></svg>`;
const sets = [
  { id: 'lucide_a', name: 'Lucide A', iconSet: { provider: 'lucide', stroke: 1 } },
  { id: 'lucide_b', name: 'Lucide B', iconSet: { provider: 'lucide', stroke: 2 } },
  { id: 'material_a', name: 'Material A', iconSet: { provider: 'material', style: 'rounded', weight: 400 } },
  { id: 'material_b', name: 'Material B', iconSet: { provider: 'material', style: 'outlined', weight: 300 } },
];
const request = (sources: Record<string, string>) => ({ token: 'phone_call', selectedSources: sources, sets });
const load = (provider: string, name: string, def: Definition) => svg(`${provider}-${name}-${def.stroke ?? def.weight}`);

test('both providers preserve native per-collection SVGs and stable ids', () => {
  const result = prepareToken(request({ lucide: 'phone', material: 'call' }), load);
  assert.deepEqual(result.sets.map(s => s.setId), sets.map(s => s.id));
  assert.ok(result.sets.every(s => !s.copied));
  assert.equal(result.sets[0].svg, svg('lucide-phone-1'));
  assert.equal(result.sets[3].svg, svg('material-call-300'));
});
for (const provider of ['lucide', 'material']) {
  test(`${provider} alone explicitly copies its valid SVG into other collections`, () => {
    const result = prepareToken(request({ [provider]: 'phone' }), load);
    const canonical = load(provider, 'phone', provider === 'lucide' ? { stroke: 2 } : { weight: 400 });
    for (const item of result.sets) {
      assert.equal(item.provider, provider);
      assert.equal(item.sourceName, 'phone');
      if (sets.find(s => s.id === item.setId)!.iconSet.provider !== provider) {
        assert.equal(item.svg, canonical);
        assert.equal(item.copied, true);
      }
    }
  });
}
test('a selected but missing provider is copied, not treated as mandatory', () => {
  const result = prepareToken(request({ lucide: 'phone', material: 'absent' }),
    (provider, ...args) => provider === 'material' ? null : load(provider, ...args));
  assert.equal(result.sets.filter(s => s.copied).length, 2);
});
test('missing collection style copies the selected canonical SVG', () => {
  const result = prepareToken(request({ material: 'phone' }),
    (provider, name, def) => def.weight === 300 ? null : load(provider, name, def));
  assert.equal(result.sets[3].svg, result.sets[2].svg);
  assert.equal(result.sets[3].copied, true);
});
test('no available source, malformed SVG or a read error fails all preparation', () => {
  assert.throws(() => prepareToken(request({}), load), /at least one/);
  assert.throws(() => prepareToken(request({ lucide: 'gone' }), () => null), /at least one/);
  assert.throws(() => prepareToken(request({ lucide: 'bad' }), () => 'invalid'), /not an SVG/);
  assert.throws(() => prepareToken(request({ lucide: 'bad', material: 'good' }), () => { throw Error('read failed'); }), /read failed/);
});
test('duplicate ids and invalid provider definitions are rejected', () => {
  assert.throws(() => prepareToken({ ...request({ lucide: 'phone' }), sets: [sets[0], sets[0]] }, load), /unique stable ids/);
  assert.throws(() => prepareToken({ ...request({ lucide: 'phone' }), sets: [{ ...sets[0], iconSet: { provider: 'unknown' } }] }, load), /Unsupported/);
});
