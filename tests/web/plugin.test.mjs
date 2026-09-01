import test from 'node:test';
import assert from 'node:assert/strict';

globalThis.document = { baseURI: 'https://example.test/jellyfin/web/' };

const { normalizeId, classifyRequest, getItemId } = await import('../../src/Jellyfin.Plugin.RemoveSeries/Web/plugin.mjs');

test('normalizeId accepts dashed and compact Jellyfin ids', () => {
    assert.equal(normalizeId('44cb2f44-5d12-46eb-9234-93e78ea35d2e'), '44cb2f445d1246eb923493e78ea35d2e');
    assert.equal(normalizeId('/Items/44cb2f445d1246eb923493e78ea35d2e/Images/Primary'), '44cb2f445d1246eb923493e78ea35d2e');
    assert.equal(normalizeId('not-an-id'), null);
});

test('classifyRequest detects current, legacy and base-path home endpoints', () => {
    assert.equal(classifyRequest('/jellyfin/Items/Resume'), 'continue-watching');
    assert.equal(classifyRequest('/jellyfin/Users/abc/Items/Resume'), 'continue-watching');
    assert.equal(classifyRequest('/jellyfin/UserItems/Resume'), 'continue-watching');
    assert.equal(classifyRequest('/jellyfin/Shows/NextUp'), 'next-up');
    assert.equal(classifyRequest('/jellyfin/Items/Latest'), null);
});

test('getItemId uses card data attributes', () => {
    const element = {
        closest: () => ({
            dataset: { id: '44cb2f44-5d12-46eb-9234-93e78ea35d2e' },
            getAttribute: () => null,
            querySelector: () => null
        })
    };
    assert.equal(getItemId(element), '44cb2f445d1246eb923493e78ea35d2e');
});
