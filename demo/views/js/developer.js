/* ================================================================
 *  developer.js - Entry / Public API
 *  Load order in HTML:
 *    1. developer.utils.js
 *    2. developer.renderer.js
 *    3. developer.api.js
 *    4. developer.actions.js
 *    5. developer.js  (this file)
 * ================================================================ */
'use strict';

const DevApp = {
    init,
    editAsset,
    submitReview,
    publishAsset,
    openReviewModal,
    openSystemAssetModal,
    switchTab
};

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => DevApp.init());
} else {
    DevApp.init();
}
