const itemSurfaces = new Map();
let lastContext = null;

const text = navigator.language?.toLowerCase().startsWith('de')
    ? {
        continueWatching: 'Serie aus Weiterschauen entfernen',
        nextUp: 'Serie aus Als Nächstes entfernen',
        title: 'Serie entfernen?',
        message: (name, list) => `„${name}“ wird aus „${list}“ entfernt. Dein Wiedergabestand bleibt unverändert.`,
        cancel: 'Abbrechen',
        remove: 'Entfernen',
        removed: 'Serie wurde entfernt.',
        undo: 'Rückgängig',
        failed: 'Die Serie konnte nicht entfernt werden.',
        continueLabel: 'Weiterschauen',
        nextLabel: 'Als Nächstes'
    }
    : {
        continueWatching: 'Remove series from Continue Watching',
        nextUp: 'Remove series from Next Up',
        title: 'Remove series?',
        message: (name, list) => `“${name}” will be removed from “${list}”. Your playback progress will not change.`,
        cancel: 'Cancel',
        remove: 'Remove',
        removed: 'Series removed.',
        undo: 'Undo',
        failed: 'The series could not be removed.',
        continueLabel: 'Continue Watching',
        nextLabel: 'Next Up'
    };

export function normalizeId(value) {
    const match = String(value || '').match(/[0-9a-f]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);
    return match ? match[0].replaceAll('-', '').toLowerCase() : null;
}

export function classifyRequest(value) {
    let pathname;
    try {
        pathname = new URL(typeof value === 'string' ? value : value?.url || '', document.baseURI).pathname.toLowerCase();
    } catch {
        return null;
    }
    if (/\/(?:users\/[^/]+\/)?items\/resume$/.test(pathname) || /\/useritems\/resume$/.test(pathname)) {
        return 'continue-watching';
    }
    if (/\/shows\/nextup$/.test(pathname) || /\/nextup$/.test(pathname)) {
        return 'next-up';
    }
    return null;
}

export function getItemId(element) {
    if (!element) return null;
    const card = element.closest?.('.card, [data-id], [data-itemid]') || element;
    const direct = normalizeId(card.dataset?.id || card.dataset?.itemid || card.getAttribute?.('data-id'));
    if (direct) return direct;
    const link = card.querySelector?.('a[href*="id="], a[href*="/Items/"]');
    return normalizeId(link?.href);
}

function rememberItems(payload, surface) {
    const items = payload?.Items || payload?.items || [];
    for (const item of items) {
        const itemId = normalizeId(item.Id || item.id);
        const seriesId = normalizeId(item.SeriesId || item.seriesId);
        if (!itemId || !seriesId) continue;
        const current = itemSurfaces.get(itemId) || new Map();
        current.set(surface, {
            seriesId,
            seriesName: item.SeriesName || item.seriesName || item.Name || item.name || 'Series'
        });
        itemSurfaces.set(itemId, current);
    }
}

function patchFetch() {
    if (window.__removeSeriesFetchPatched) return;
    window.__removeSeriesFetchPatched = true;
    const originalFetch = window.fetch.bind(window);
    window.fetch = async function patchedFetch(input, init) {
        const response = await originalFetch(input, init);
        const surface = classifyRequest(input);
        if (surface && response.ok) {
            response.clone().json().then(payload => rememberItems(payload, surface)).catch(() => {});
        }
        return response;
    };
}

function detectSurface(card, itemId) {
    const section = card?.closest?.('.homeSection, section, .verticalSection');
    const heading = section?.querySelector?.('.sectionTitle, h2, h3')?.textContent?.trim().toLowerCase() || '';
    if (/weiterschauen|continue watching/.test(heading)) return 'continue-watching';
    if (/als nächstes|next up/.test(heading)) return 'next-up';
    const known = itemSurfaces.get(itemId);
    return known?.size === 1 ? known.keys().next().value : null;
}

function captureContext(event) {
    const card = event.target?.closest?.('.card');
    if (!card) return;
    if (event.type === 'click' && !event.target.closest('.btnCardOptions, [data-action="menu"], [aria-label*="More"], [aria-label*="Mehr"]')) return;
    if (event.type === 'pointerdown' && event.pointerType !== 'touch') return;
    const itemId = getItemId(card);
    const surface = itemId && detectSurface(card, itemId);
    const item = surface && itemSurfaces.get(itemId)?.get(surface);
    if (itemId && surface && item) {
        lastContext = { itemId, surface, card, ...item, capturedAt: Date.now() };
    }
}

function createActionButton(context) {
    const button = document.createElement('button');
    button.type = 'button';
    button.setAttribute('is', 'emby-button');
    button.className = 'listItem listItem-button actionSheetMenuItem emby-button removeSeriesAction';
    button.innerHTML = `<span class="listItemIcon material-icons" aria-hidden="true">playlist_remove</span><div class="listItemBody"><div class="listItemBodyText">${context.surface === 'continue-watching' ? text.continueWatching : text.nextUp}</div></div>`;
    button.addEventListener('click', () => removeSeries(context));
    return button;
}

function injectAction(sheet) {
    if (sheet.querySelector('.removeSeriesAction') || !lastContext || Date.now() - lastContext.capturedAt > 5000) return;
    const container = sheet.querySelector('.actionSheetScroller, .scrollSlider') || sheet;
    container.appendChild(createActionButton(lastContext));
}

function scanActionSheets(root = document) {
    root.querySelectorAll?.('.actionSheet:not(.hide), .actionSheetContent').forEach(node => injectAction(node.closest('.actionSheet') || node));
}

function apiUrl(path) {
    const client = window.ApiClient || window.apiClient;
    if (client?.getUrl) return client.getUrl(path.replace(/^\//, ''));
    return new URL(`../${path.replace(/^\//, '')}`, document.baseURI).href;
}

function authHeaders() {
    const client = window.ApiClient || window.apiClient;
    const token = typeof client?.accessToken === 'function' ? client.accessToken() : client?.accessToken;
    return {
        'Content-Type': 'application/json',
        ...(token ? { 'X-Emby-Token': token } : {})
    };
}

async function removeSeries(context) {
    const listName = context.surface === 'continue-watching' ? text.continueLabel : text.nextLabel;
    if (!await confirmDialog(text.message(context.seriesName, listName))) return;
    try {
        const response = await fetch(apiUrl('/RemoveSeries/Exclusions'), {
            method: 'POST',
            headers: authHeaders(),
            credentials: 'same-origin',
            body: JSON.stringify({ itemId: context.itemId, surface: context.surface })
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const result = await response.json();
        const seriesId = normalizeId(result.seriesId || result.SeriesId) || context.seriesId;
        const hiddenCards = hideSeriesCards(seriesId, context.surface);
        document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', keyCode: 27, bubbles: true }));
        showToast(text.removed, text.undo, () => undoRemoval(seriesId, context.surface, hiddenCards));
    } catch (error) {
        console.error('[RemoveSeries]', error);
        showToast(text.failed);
    }
}

function hideSeriesCards(seriesId, surface) {
    const hidden = [];
    document.querySelectorAll('.card').forEach(card => {
        const itemId = getItemId(card);
        const info = itemSurfaces.get(itemId)?.get(surface);
        if (info?.seriesId === seriesId && detectSurface(card, itemId) === surface) {
            hidden.push({ card, display: card.style.display });
            card.style.display = 'none';
        }
    });
    return hidden;
}

async function undoRemoval(seriesId, surface, hiddenCards) {
    try {
        const response = await fetch(apiUrl(`/RemoveSeries/Exclusions/${surface}/${seriesId}`), {
            method: 'DELETE',
            headers: authHeaders(),
            credentials: 'same-origin'
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        hiddenCards.forEach(({ card, display }) => { if (card.isConnected) card.style.display = display; });
    } catch (error) {
        console.error('[RemoveSeries]', error);
        showToast(text.failed);
    }
}

function confirmDialog(message) {
    return new Promise(resolve => {
        const overlay = document.createElement('div');
        overlay.className = 'removeSeriesOverlay';
        overlay.innerHTML = `<div class="removeSeriesDialog" role="dialog" aria-modal="true" aria-labelledby="removeSeriesTitle"><h2 id="removeSeriesTitle">${text.title}</h2><p></p><div><button class="removeSeriesCancel">${text.cancel}</button><button class="removeSeriesConfirm">${text.remove}</button></div></div>`;
        overlay.querySelector('p').textContent = message;
        const finish = result => { overlay.remove(); resolve(result); };
        overlay.querySelector('.removeSeriesCancel').addEventListener('click', () => finish(false));
        overlay.querySelector('.removeSeriesConfirm').addEventListener('click', () => finish(true));
        overlay.addEventListener('click', event => { if (event.target === overlay) finish(false); });
        document.body.appendChild(overlay);
        overlay.querySelector('.removeSeriesConfirm').focus();
    });
}

function showToast(message, actionLabel, action) {
    document.querySelector('.removeSeriesToast')?.remove();
    const toast = document.createElement('div');
    toast.className = 'removeSeriesToast';
    toast.textContent = message;
    if (actionLabel && action) {
        const button = document.createElement('button');
        button.textContent = actionLabel;
        button.addEventListener('click', () => { toast.remove(); action(); });
        toast.appendChild(button);
    }
    document.body.appendChild(toast);
    setTimeout(() => toast.remove(), 8000);
}

function addStyles() {
    const style = document.createElement('style');
    style.textContent = `.removeSeriesOverlay{position:fixed;inset:0;z-index:100000;background:#000a;display:grid;place-items:center;padding:1rem}.removeSeriesDialog{width:min(28rem,100%);background:#202020;color:#fff;border-radius:.5rem;padding:1.25rem;box-shadow:0 1rem 3rem #0008}.removeSeriesDialog h2{margin:0 0 .75rem}.removeSeriesDialog p{line-height:1.45}.removeSeriesDialog>div{display:flex;justify-content:flex-end;gap:.5rem;margin-top:1rem}.removeSeriesDialog button,.removeSeriesToast button{border:0;border-radius:.25rem;padding:.65rem 1rem;font-weight:600;cursor:pointer}.removeSeriesConfirm{background:#00a4dc;color:#fff}.removeSeriesCancel{background:#444;color:#fff}.removeSeriesToast{position:fixed;z-index:100001;left:50%;bottom:2rem;transform:translateX(-50%);display:flex;align-items:center;gap:1rem;background:#111;color:#fff;border-radius:.35rem;padding:.8rem 1rem;box-shadow:0 .5rem 2rem #0008}.removeSeriesToast button{background:transparent;color:#00a4dc;padding:.25rem}`;
    document.head.appendChild(style);
}

if (typeof window !== 'undefined' && typeof document !== 'undefined') {
    (window.__removeSeriesEarly || []).forEach(([payload, surface]) => rememberItems(payload, surface));
    window.__removeSeriesCaptureHandler = rememberItems;
    patchFetch();
    addStyles();
    document.addEventListener('contextmenu', captureContext, true);
    document.addEventListener('click', captureContext, true);
    document.addEventListener('pointerdown', captureContext, true);
    const start = () => {
        scanActionSheets();
        new MutationObserver(mutations => mutations.forEach(mutation => mutation.addedNodes.forEach(node => {
            if (node.nodeType === Node.ELEMENT_NODE) scanActionSheets(node);
        }))).observe(document.body, { childList: true, subtree: true });
    };
    if (document.body) start(); else document.addEventListener('DOMContentLoaded', start, { once: true });
    console.info('[RemoveSeries] web integration loaded');
}
