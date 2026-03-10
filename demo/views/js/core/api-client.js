/**
 * Module: api-client.js
 * Responsibility: Unified HTTP request client for all KaxSocket frontend pages.
 *   Centralizes token injection, request timeout, 401 auto-redirect, and JSON
 *   parsing so that page scripts do not duplicate fetch boilerplate.
 * Dependencies: core/auth-state.js (window.AuthState.getToken / clearToken).
 *   Must be loaded after auth-state.js in the page's script list.
 */

(function (global) {
    'use strict';

    /** Default request timeout in milliseconds. */
    var DEFAULT_TIMEOUT_MS = 15000;

    /**
     * Internal error class used to carry HTTP status and server-side code/message.
     * @param {string} message - Human-readable description.
     * @param {number} httpStatus - HTTP response status code (0 for network errors).
     * @param {number|string|null} apiCode - Backend business error code from envelope.
     * @param {string|null} apiMessage - Backend business error message from envelope.
     */
    function ApiError(message, httpStatus, apiCode, apiMessage) {
        this.name = 'ApiError';
        this.message = message;
        this.httpStatus = httpStatus || 0;
        this.apiCode = apiCode != null ? apiCode : null;
        this.apiMessage = apiMessage || null;
        // Capture stack if available (V8 / modern browsers).
        if (Error.captureStackTrace) Error.captureStackTrace(this, ApiError);
    }
    ApiError.prototype = Object.create(Error.prototype);
    ApiError.prototype.constructor = ApiError;

    // ----------------------------------------------------------------
    // Token helpers — delegate to AuthState when available, else raw LS.
    // ----------------------------------------------------------------

    /**
     * Returns the active auth token.
     * Prefers window.AuthState.getToken() if auth-state.js is loaded.
     * @returns {string|null}
     */
    function _resolveToken() {
        if (global.AuthState && typeof global.AuthState.getToken === 'function') {
            return global.AuthState.getToken();
        }
        return localStorage.getItem('kax_web_token') ||
               localStorage.getItem('kax_login_token') ||
               null;
    }

    /**
     * Clears auth tokens and redirects to /login.
     * Delegates to window.AuthState.clearToken() when available.
     */
    function _handleUnauthorized() {
        if (global.AuthState && typeof global.AuthState.clearToken === 'function') {
            global.AuthState.clearToken();
        } else {
            localStorage.removeItem('kax_web_token');
            localStorage.removeItem('kax_login_token');
        }
        location.href = '/login';
    }

    // ----------------------------------------------------------------
    // Core request
    // ----------------------------------------------------------------

    /**
     * Low-level fetch wrapper. Injects Authorization header, enforces timeout,
     * and handles 401 by clearing tokens and redirecting to /login.
     *
     * @param {string} url - Request URL (relative or absolute).
     * @param {RequestInit & { timeoutMs?: number }} [options] - Fetch options, plus
     *   optional timeoutMs override. Do NOT set Authorization manually — this
     *   function injects it automatically.
     * @returns {Promise<Response>} Resolved Response on success (non-401 status codes
     *   pass through; callers decide how to handle non-ok statuses).
     * @throws {ApiError} On network failure or request abort (timeout).
     */
    async function request(url, options) {
        options = options || {};
        var timeoutMs = typeof options.timeoutMs === 'number' ? options.timeoutMs : DEFAULT_TIMEOUT_MS;

        // Build headers merging caller's headers with the auth token.
        var headers = Object.assign({}, options.headers || {});
        var token = _resolveToken();
        if (token) {
            headers['Authorization'] = 'Bearer ' + token;
        }

        var controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
        var timeoutId = null;
        if (controller) {
            timeoutId = setTimeout(function () { controller.abort(); }, timeoutMs);
        }

        var fetchOptions = Object.assign({}, options, {
            headers: headers,
            signal: controller ? controller.signal : undefined
        });
        // Remove our custom key before passing to fetch.
        delete fetchOptions.timeoutMs;

        try {
            var response = await fetch(url, fetchOptions);
            if (response.status === 401) {
                _handleUnauthorized();
                // Return the response anyway so the caller can inspect; navigation
                // is already triggered but the promise chain completes cleanly.
                return response;
            }
            return response;
        } catch (err) {
            if (err && err.name === 'AbortError') {
                throw new ApiError('请求超时，请稍后重试。', 0, 'TIMEOUT', null);
            }
            throw new ApiError('网络连接异常，请检查网络后重试。', 0, 'NETWORK_ERROR', null);
        } finally {
            if (timeoutId !== null) clearTimeout(timeoutId);
        }
    }

    /**
     * Fetches a URL, parses the JSON response, and throws ApiError for non-ok statuses.
     * For responses that carry a KaxSocket API Envelope ({ code, message, data, traceId }),
     * a non-zero envelope code is surfaced as apiCode/apiMessage on the thrown ApiError.
     *
     * @param {string} url - Request URL.
     * @param {RequestInit & { timeoutMs?: number }} [options] - Fetch options.
     * @returns {Promise<any>} The parsed response body (full envelope or raw JSON).
     * @throws {ApiError} On HTTP error, JSON parse failure, or network error.
     */
    async function requestJson(url, options) {
        var response = await request(url, options);

        var body;
        try {
            body = await response.json();
        } catch (_) {
            if (!response.ok) {
                throw new ApiError('服务返回数据格式错误。', response.status, 'PARSE_ERROR', null);
            }
            throw new ApiError('服务返回数据格式错误。', response.status, 'PARSE_ERROR', null);
        }

        if (!response.ok) {
            // Prefer envelope message if available.
            var envMsg = (body && body.message) || null;
            var envCode = (body && body.code != null) ? body.code : response.status;
            throw new ApiError(envMsg || ('HTTP ' + response.status), response.status, envCode, envMsg);
        }

        return body;
    }

    /**
     * Convenience wrapper for POST requests with JSON body.
     *
     * @param {string} url - Request URL.
     * @param {any} payload - Object to serialize as the request body.
     * @param {RequestInit & { timeoutMs?: number }} [options] - Additional fetch options.
     * @returns {Promise<any>} Parsed JSON response body.
     * @throws {ApiError} On any request or response error.
     */
    async function requestJsonPost(url, payload, options) {
        var mergedOptions = Object.assign({}, options || {}, {
            method: 'POST',
            headers: Object.assign({ 'Content-Type': 'application/json' }, (options && options.headers) || {}),
            body: JSON.stringify(payload != null ? payload : {})
        });
        return requestJson(url, mergedOptions);
    }

    // ----------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------

    /** @namespace ApiClient */
    global.ApiClient = {
        /** @see request */
        request: request,
        /** @see requestJson */
        requestJson: requestJson,
        /** @see requestJsonPost */
        requestJsonPost: requestJsonPost,
        /** Exposed so pages can reference the error class for instanceof checks. */
        ApiError: ApiError
    };

}(window));
