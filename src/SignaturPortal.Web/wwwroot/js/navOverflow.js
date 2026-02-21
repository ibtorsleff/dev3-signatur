// navOverflow.js
// ResizeObserver-based Row 1 navigation overflow detection.
//
// Measures the left menu <ul> width plus the natural widths of every individual
// portal item (li[data-portal-index]).  Blazor uses these to decide:
//   • whether to show portal items individually or collapse them into the
//     "Portals" dropdown button (portals drop off first, all at once)
//   • which left items (if any) to overflow into the "More" dropdown

let _observer = null;
let _dotnetRef = null;

/**
 * Initialise: attach ResizeObserver to the left menu <ul> and perform an
 * initial measurement of left items and portal items.
 * @param {import('@microsoft/dotnet-js-interop').DotNetObjectReference} dotnetHelper
 * @param {string} leftUlId - id of the left menu <ul>
 */
export function init(dotnetHelper, leftUlId) {
    _dotnetRef = dotnetHelper;

    const leftUl = document.getElementById(leftUlId);
    if (!leftUl) return;

    // The left <ul> has flex:1 so it resizes whenever the viewport or the right
    // side changes.  Watching it gives us a single, stable signal.
    _observer = new ResizeObserver(() => {
        const el = document.getElementById(leftUlId);
        if (el && _dotnetRef) {
            _dotnetRef.invokeMethodAsync('OnContainerResized', el.clientWidth);
        }
    });
    _observer.observe(leftUl);

    // Wait for the next animation frame so that the browser has completed
    // layout before we measure element widths.
    requestAnimationFrame(() => reportMeasurements(leftUlId));
}

/**
 * Re-measure all items. Call after Blazor has re-rendered with overflow and
 * portal state both reset (all items visible).
 * @param {string} leftUlId
 */
export function remeasure(leftUlId) {
    requestAnimationFrame(() => reportMeasurements(leftUlId));
}

function reportMeasurements(leftUlId) {
    const leftUl = document.getElementById(leftUlId);
    if (!leftUl || !_dotnetRef) return;

    // Left nav items — regular nav items only (not the More button).
    const leftItems = Array.from(leftUl.querySelectorAll('li[data-nav-index]'));
    const leftWidths = leftItems.map(li => li.getBoundingClientRect().width);

    // Portal items live in the right <ul>; query them from the whole document.
    // They are only present in the DOM when portals are expanded, which is
    // guaranteed when this is called (Blazor resets _portalsCollapsed = false
    // before requesting a remeasure).
    const portalItems = Array.from(document.querySelectorAll('li[data-portal-index]'));
    const portalWidths = portalItems.map(li => li.getBoundingClientRect().width);

    _dotnetRef.invokeMethodAsync('OnNavMeasured', leftUl.clientWidth, leftWidths, portalWidths);
}

/**
 * Disconnect the observer and release the DotNet object reference.
 */
export function dispose() {
    if (_observer) {
        _observer.disconnect();
        _observer = null;
    }
    if (_dotnetRef) {
        _dotnetRef.dispose();
        _dotnetRef = null;
    }
}
