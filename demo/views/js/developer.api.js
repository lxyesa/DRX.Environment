/* ================================================================
 *  developer.api.js — 后端接口调用层
 *  所有请求通过 ApiClient 统一发出（token 注入、超时、401 处理由
 *  core/api-client.js 负责），不再重复 fetch 样板代码。
 *  依赖加载顺序：core/auth-state.js → core/api-client.js → 本文件
 * ================================================================ */
'use strict';

/**
 * 检查登录态；未登录则跳转 /login 并返回 false。
 * 优先由 ApiClient 的 401 拦截器处理，此函数用于主动前置校验。
 * @returns {boolean}
 */
function requireLogin() {
    if (window.AuthState && typeof window.AuthState.getToken === 'function') {
        if (!window.AuthState.getToken()) {
            location.href = '/login';
            return false;
        }
        return true;
    }
    const token = localStorage.getItem('kax_web_token') || localStorage.getItem('kax_login_token');
    if (!token) { location.href = '/login'; return false; }
    return true;
}

/**
 * 调用 /api/user/verify/account 验证登录态并填充 state 中的用户信息。
 * 通过 ApiClient.requestJsonPost 统一处理 token 注入与 401 跳转。
 * @returns {Promise<boolean>} 验证成功返回 true，失败跳转 /login 并返回 false。
 */
async function verifyUser() {
    if (!requireLogin()) return false;
    try {
        const body = await ApiClient.requestJsonPost('/api/user/verify/account', {});
        state.userName = body.user || '';
        state.permissionGroup = body.permissionGroup ?? 999;
        state.isAdmin = body.isAdmin === true;
        state.isSystem = body.isSystem === true || state.permissionGroup === 0;

        if (state.isAdmin) {
            const tab = document.getElementById('reviewTab');
            if (tab) tab.style.display = '';
        }
        if (state.isSystem) {
            const systemTab = document.getElementById('assetManagementTab');
            if (systemTab) systemTab.style.display = '';
        }
        return true;
    } catch (e) {
        console.error('验证用户失败', e);
        location.href = '/login';
        return false;
    }
}

async function apiGetMyAssets(page, status) {
    const params = new URLSearchParams({ page, pageSize: PAGE_SIZE });
    if (status !== '' && status != null) params.set('status', status);
    return ApiClient.requestJson('/api/developer/assets?' + params);
}

async function apiGetMyAssetDetail(id) {
    return ApiClient.requestJson(`/api/developer/asset/${id}`);
}

async function apiCreateAsset(payload) {
    return ApiClient.requestJsonPost('/api/developer/asset/create', payload);
}

async function apiUpdateAsset(payload) {
    return ApiClient.requestJsonPost('/api/developer/asset/update', payload);
}

async function apiSubmitReview(id, payload = null) {
    const body = payload ? { id, ...payload } : { id };
    return ApiClient.requestJsonPost('/api/developer/asset/submit', body);
}

async function apiPublishAsset(id) {
    return ApiClient.requestJsonPost('/api/developer/asset/publish', { id });
}

async function apiGetReviewList(page, status) {
    const params = new URLSearchParams({ page, pageSize: PAGE_SIZE });
    if (status != null) params.set('status', status);
    return ApiClient.requestJson('/api/review/pending?' + params);
}

async function apiGetReviewAssetDetail(id) {
    return ApiClient.requestJson(`/api/review/asset/${id}`);
}

async function apiApprove(id) {
    return ApiClient.requestJsonPost('/api/review/approve', { id });
}

async function apiReject(id, reason) {
    return ApiClient.requestJsonPost('/api/review/reject', { id, reason });
}

async function apiGetSystemAssetList(page, status, q, authorId) {
    const params = new URLSearchParams({ page, pageSize: PAGE_SIZE });
    if (status !== '' && status != null) params.set('status', status);
    if (q) params.set('q', q);
    if (authorId) params.set('authorId', authorId);
    return ApiClient.requestJson('/api/asset/system/list?' + params);
}

async function apiGetSystemAssetDetail(id) {
    return ApiClient.requestJson(`/api/asset/system/${id}`);
}

async function apiSystemUpdateField(id, field, value) {
    return ApiClient.requestJsonPost('/api/asset/system/update-field', { id, field, value });
}

async function apiSystemReturn(id, reason) {
    return ApiClient.requestJsonPost('/api/asset/system/return', { assetId: id, reason });
}

async function apiSystemOffShelf(id, reason) {
    return ApiClient.requestJsonPost('/api/asset/system/off-shelf', { assetId: id, reason });
}

async function apiSystemForceReview(id, reason) {
    return ApiClient.requestJsonPost('/api/asset/system/review/force', { assetId: id, reason, force: true });
}

async function apiSystemHardDelete(id, reason) {
    return ApiClient.requestJsonPost('/api/asset/system/hard-delete', { assetId: id, reason, confirm: true });
}
