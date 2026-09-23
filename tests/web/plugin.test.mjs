import test from 'node:test';
import assert from 'node:assert/strict';

globalThis.document = { baseURI: 'https://example.test/jellyfin/web/' };

const { normalizeId, classifyRequest, getItemId, actionModesForSurface, authHeaders, captureContext, scanActionSheets } = await import('../../src/Jellyfin.Plugin.RemoveSeries/Web/plugin.mjs');

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

test('Continue Watching offers episode and series actions while Next Up stays series-only', () => {
    assert.deepEqual(actionModesForSurface('continue-watching'), ['episode', 'series']);
    assert.deepEqual(actionModesForSurface('next-up'), ['series']);
    assert.deepEqual(actionModesForSurface(null), []);
});

test('Jellyfin 12 authorization uses the MediaBrowser scheme', () => {
    globalThis.window = { ApiClient: { accessToken: () => 'test-token' } };
    assert.deepEqual(authHeaders(), {
        'Content-Type': 'application/json',
        Authorization: 'MediaBrowser Token="test-token"'
    });
    delete globalThis.window;
});

test('right-click adds removal actions even without captured API responses', () => {
    const itemId = '44cb2f445d1246eb923493e78ea35d2e';
    const section = { querySelector: () => ({ textContent: 'Weiterschauen' }) };
    const card = {
        dataset: { id: itemId, type: 'Episode' },
        closest: selector => selector.includes('verticalSection') ? section : card
    };
    captureContext({ type: 'contextmenu', target: { closest: () => card } });

    const buttons = [];
    const container = { appendChild: button => buttons.push(button) };
    const sheet = {
        matches: () => true,
        closest: () => sheet,
        querySelector: selector => selector.includes('actionSheetScroller') ? container : null,
        querySelectorAll: () => []
    };
    document.createElement = () => ({ setAttribute() {}, addEventListener() {} });
    scanActionSheets(sheet);
    delete document.createElement;

    assert.equal(buttons.length, 2);
    assert.match(buttons[0].className, /removeSeriesAction-episode/);
    assert.match(buttons[1].className, /removeSeriesAction-series/);

    const nextUpSection = { querySelector: () => ({ textContent: 'Als Nächstes' }) };
    card.closest = selector => selector.includes('verticalSection') ? nextUpSection : card;
    captureContext({ type: 'contextmenu', target: { closest: () => card } });
    buttons.length = 0;
    document.createElement = () => ({ setAttribute() {}, addEventListener() {} });
    scanActionSheets(sheet);
    delete document.createElement;
    assert.equal(buttons.length, 1);
    assert.match(buttons[0].className, /removeSeriesAction-series/);
});
