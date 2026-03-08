/**
 * Module: shared/utils.js
 * Responsibility: Shared, domain-agnostic utility functions for KaxSocket frontend pages.
 *   Centralizes formatDate, escapeHtml, formatDownloadCount and formatCurrency so that
 *   individual page scripts (shop.js, shop_detail.js, etc.) do not duplicate these helpers.
 * Dependencies: none — this module is a pure utility library with no external dependencies.
 *   Must be loaded before any page script that uses window.ShopUtils.
 */

(function (global) {
    'use strict';

    /**
     * Converts a Unix timestamp (seconds or milliseconds) or date string to a zh-CN locale date string.
     * Returns a fallback dash string when the input is absent or invalid.
     *
     * @param {number|string|null|undefined} ts - Unix timestamp (s or ms) or parseable date string.
     * @param {string} [fallback='--'] - Value returned when ts is empty or invalid.
     * @returns {string} Formatted date string (e.g. "2024/01/15") or fallback.
     */
    function formatDate(ts, fallback) {
        fallback = fallback !== undefined ? fallback : '--';
        if (!ts || ts === '--') return fallback;
        var ms = (typeof ts === 'number' || /^\d+$/.test(String(ts)))
            ? (Number(ts) > 9999999999 ? Number(ts) : Number(ts) * 1000)
            : NaN;
        if (isNaN(ms)) {
            // Try as a date string directly.
            ms = Date.parse(String(ts));
        }
        var date = new Date(ms);
        if (isNaN(date.getTime())) return fallback;
        return date.toLocaleDateString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit' });
    }

    /**
     * Escapes HTML special characters in a value to prevent XSS when rendering
     * user-supplied content into innerHTML.
     *
     * @param {*} value - Any value; will be coerced to string.
     * @returns {string} HTML-escaped string.
     */
    function escapeHtml(value) {
        var div = document.createElement('div');
        div.textContent = String(value != null ? value : '');
        return div.innerHTML;
    }

    /**
     * Formats a numeric download/purchase/view count for compact display.
     * Values >= 1000 are shown with a "K" suffix (one decimal place).
     *
     * @param {number|string|null|undefined} v - Numeric count value.
     * @param {string} [fallback='--'] - Value returned when v is absent or non-numeric.
     * @returns {string} Formatted count string (e.g. "1.2K" or "987").
     */
    function formatDownloadCount(v, fallback) {
        fallback = fallback !== undefined ? fallback : '--';
        if (v === null || v === undefined || v === '--') return fallback;
        var n = Number(v);
        if (isNaN(n)) return fallback;
        return n >= 1000 ? (n / 1000).toFixed(1) + 'K' : String(n);
    }

    /**
     * Formats a numeric price (in yuan) as a display string with a coin emoji prefix.
     * Returns a fallback dash string when the value is absent or non-numeric.
     *
     * @param {number|string|null|undefined} v - Price in yuan.
     * @param {string} [fallback='--'] - Value returned when v is absent or non-numeric.
     * @returns {string} Formatted price string (e.g. "💰9.90") or fallback.
     */
    function formatCurrency(v, fallback) {
        fallback = fallback !== undefined ? fallback : '--';
        if (v === null || v === undefined) return fallback;
        var n = Number(v);
        if (isNaN(n)) return fallback;
        return '💰' + n.toFixed(2);
    }

    // ----------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------

    /** @namespace ShopUtils */
    global.ShopUtils = {
        formatDate: formatDate,
        escapeHtml: escapeHtml,
        formatDownloadCount: formatDownloadCount,
        formatCurrency: formatCurrency
    };

}(window));
