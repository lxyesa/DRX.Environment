/**
 * Module: error-presenter.js
 * Responsibility: Unified error-code-to-user-message mapping and notification
 *   presenter for all KaxSocket frontend pages.
 *   Replaces per-page ERROR_CODE_MAP / showErrorMessage duplicates.
 * Dependencies: window.showMsgBox (optional, provided by global.js); falls back
 *   to window.alert when not available.
 */

(function (global) {
    'use strict';

    // ----------------------------------------------------------------
    // Error code map — single source of truth for all domains.
    // HTTP status codes and business codes (1000+) share the same table.
    // ----------------------------------------------------------------

    /**
     * Maps HTTP status codes and business error codes to structured descriptions.
     * Each entry: { title: string, message: string, type: 'success'|'info'|'warn'|'error', action?: string }
     * The optional `action` field:
     *   - 'login' — triggers redirect to /login after dismissal.
     */
    var ERROR_CODE_MAP = {
        // Success
        0:    { title: '成功',     message: '操作成功',                          type: 'success' },
        // HTTP status codes
        400:  { title: '参数错误', message: '请求参数有误，请检查后重试。',        type: 'error' },
        401:  { title: '未登录',   message: '请先登录以继续操作。',                type: 'warn', action: 'login' },
        403:  { title: '无权限',   message: '您没有执行此操作的权限。',            type: 'error' },
        404:  { title: '未找到',   message: '请求的资源不存在。',                  type: 'error' },
        405:  { title: '方法不允许', message: '请求方式不支持，请联系开发者。',    type: 'error' },
        409:  { title: '操作冲突', message: '资源已存在或状态冲突，请稍后重试。', type: 'warn' },
        422:  { title: '参数无效', message: '提交的数据无法处理，请检查内容后重试。', type: 'error' },
        429:  { title: '请求频繁', message: '操作太频繁，请稍后再试。',            type: 'warn' },
        500:  { title: '服务异常', message: '服务器出现问题，请稍后重试。',        type: 'error' },
        502:  { title: '服务异常', message: '服务暂时不可用，请稍后重试。',        type: 'error' },
        503:  { title: '服务繁忙', message: '系统繁忙，请稍后重试。',              type: 'error' },
        // Business codes (1000+)
        1001: { title: '余额不足', message: '您的账户余额不足，请先充值。',        type: 'warn' },
        1002: { title: '已购买',   message: '您已拥有该资产，无需重复购买。',      type: 'info' },
        1003: { title: '库存不足', message: '该商品已售罄，请选择其他方案。',      type: 'warn' },
        1004: { title: '已下架',   message: '该商品已下架，暂时无法购买。',        type: 'warn' },
        // Sentinel keys for network / parse failures (string keys used by ApiClient).
        NETWORK_ERROR: { title: '网络错误', message: '网络连接异常，请检查网络后重试。', type: 'error' },
        TIMEOUT:       { title: '请求超时', message: '服务响应超时，请稍后重试。',       type: 'error' },
        PARSE_ERROR:   { title: '数据异常', message: '服务返回数据格式错误，请稍后重试。', type: 'error' },
        UNKNOWN:       { title: '未知错误', message: '发生未知错误，请稍后重试。',       type: 'error' }
    };

    // ----------------------------------------------------------------
    // Core functions
    // ----------------------------------------------------------------

    /**
     * Maps an HTTP status code or business error code to a structured error object.
     * Falls back to the UNKNOWN entry when the code is not in the table.
     *
     * @param {number|string|null} code - HTTP status code, business code, or sentinel key.
     * @param {string|null} [serverMessage] - Raw message from the server (used as the
     *   displayed message when provided, overriding the static table entry).
     * @returns {{ title: string, message: string, type: string, action?: string }}
     */
    function resolveError(code, serverMessage) {
        var entry = (code != null && ERROR_CODE_MAP[code]) ? ERROR_CODE_MAP[code] : ERROR_CODE_MAP['UNKNOWN'];
        return {
            title:   entry.title,
            message: serverMessage || entry.message,
            type:    entry.type,
            action:  entry.action || null
        };
    }

    /**
     * Displays a user-facing error notification.
     * Uses window.showMsgBox when available (global.js component); otherwise falls
     * back to window.alert so the function is safe on any page.
     *
     * @param {number|string|null} code - HTTP status or business error code.
     * @param {string|null} [serverMessage] - Optional server message to override default text.
     */
    function notifyError(code, serverMessage) {
        var info = resolveError(code, serverMessage);
        _present(info);
    }

    /**
     * Displays a user-facing success notification using showMsgBox or alert.
     *
     * @param {string} [message] - Optional custom success message.
     * @param {string} [title] - Optional custom title (defaults to '成功').
     */
    function notifySuccess(message, title) {
        _present({
            title:   title   || '成功',
            message: message || '操作成功',
            type:    'success',
            action:  null
        });
    }

    /**
     * Internal: routes a structured error/success info object to the appropriate
     * notification mechanism.
     * @param {{ title: string, message: string, type: string, action: string|null }} info
     */
    function _present(info) {
        if (typeof global.showMsgBox === 'function') {
            global.showMsgBox({
                title:   info.title,
                message: info.message,
                type:    info.type,
                onConfirm: info.action === 'login'
                    ? function () { location.href = '/login'; }
                    : null
            });
        } else {
            // Graceful degradation when showMsgBox is not on the page.
            alert(info.title + ': ' + info.message);
            if (info.action === 'login') location.href = '/login';
        }
    }

    // ----------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------

    /** @namespace ErrorPresenter */
    global.ErrorPresenter = {
        /** @see resolveError */
        resolveError:   resolveError,
        /** @see notifyError */
        notifyError:    notifyError,
        /** @see notifySuccess */
        notifySuccess:  notifySuccess,
        /**
         * Direct access to the error code map for advanced consumers that need
         * to look up entries without displaying them (e.g. unit tests, custom renderers).
         */
        ERROR_CODE_MAP: ERROR_CODE_MAP
    };

}(window));
