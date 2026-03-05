/* ================================================================
 *  developer.api.js — 后端接口调用层
 *  所有 fetch 请求集中于此，依赖 developer.utils.js 中的常量
 * ================================================================ */
'use strict';

function getToken() {
    return localStorage.getItem(TOKEN_KEY);
}

function authHeaders(json = true) {
    const h = {};
    const t = getToken();
    if (t) h['Authorization'] = 'Bearer ' + t;
    if (json) h['Content-Type'] = 'application/json';
    return h;
}

function requireLogin() {
    if (!getToken()) {
        alert('请先登录');
        location.href = '/login';
        return false;
    }
    return true;
}

async function verifyUser() {
    const token = getToken();
    if (!token) { location.href = '/login'; return false; }
    try {
        const resp = await fetch('/api/user/verify/account', {
            method: 'POST',
            headers: authHeaders()
        });
        if (!resp.ok) { location.href = '/login'; return false; }
        const body = await resp.json();
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
    const resp = await fetch('/api/developer/assets?' + params, { headers: authHeaders(false) });
    if (!resp.ok) throw new Error('获取资产列表失败');
    return resp.json();
}

async function apiGetMyAssetDetail(id) {
    const resp = await fetch(`/api/developer/asset/${id}`, { headers: authHeaders(false) });
    if (!resp.ok) throw new Error('获取资产详情失败');
    return resp.json();
}

async function apiCreateAsset(payload) {
    const resp = await fetch('/api/developer/asset/create', {
        method: 'POST', headers: authHeaders(), body: JSON.stringify(payload)
    });
    return resp.json();
}

async function apiUpdateAsset(payload) {
    const resp = await fetch('/api/developer/asset/update', {
        method: 'POST', headers: authHeaders(), body: JSON.stringify(payload)
    });
    return resp.json();
}

async function apiSubmitReview(id, payload = null) {
    const body = payload ? { id, ...payload } : { id };
    const resp = await fetch('/api/developer/asset/submit', {
        method: 'POST', headers: authHeaders(), body: JSON.stringify(body)
    });
    return resp.json();
}

async function apiPublishAsset(id) {
    const resp = await fetch('/api/developer/asset/publish', {
        method: 'POST', headers: authHeaders(), body: JSON.stringify({ id })
    });
    return resp.json();
}

async function apiGetReviewList(page, status) {
    const params = new URLSearchParams({ page, pageSize: PAGE_SIZE });
    if (status != null) params.set('status', status);
    const resp = await fetch('/api/review/pending?' + params, { headers: authHeaders(false) });
    if (!resp.ok) throw new Error('获取审核列表失败');
    return resp.json();
}

async function apiGetReviewAssetDetail(id) {
    const resp = await fetch(`/api/review/asset/${id}`, { headers: authHeaders(false) });
    if (!resp.ok) throw new Error('获取审核详情失败');
    return resp.json();
}

async function apiApprove(id) {
    const resp = await fetch('/api/review/approve', {
        method: 'POST', headers: authHeaders(), body: JSON.stringify({ id })
    });
    return resp.json();
}

async function apiReject(id, reason) {
    const resp = await fetch('/api/review/reject', {
        method: 'POST', headers: authHeaders(), body: JSON.stringify({ id, reason })
    });
    return resp.json();
}

async function apiGetSystemAssetList(page, status, q, authorId) {
    const params = new URLSearchParams({ page, pageSize: PAGE_SIZE });
    if (status !== '' && status != null) params.set('status', status);
    if (q) params.set('q', q);
    if (authorId) params.set('authorId', authorId);
    const resp = await fetch('/api/asset/system/list?' + params, { headers: authHeaders(false) });
    if (!resp.ok) throw new Error('获取系统资产列表失败');
    return resp.json();
}

async function apiGetSystemAssetDetail(id) {
    const resp = await fetch(`/api/asset/system/${id}`, { headers: authHeaders(false) });
    if (!resp.ok) throw new Error('获取系统资产详情失败');
    return resp.json();
}

async function apiSystemUpdateField(id, field, value) {
    const resp = await fetch('/api/asset/system/update-field', {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ id, field, value })
    });
    return resp.json();
}

async function apiSystemReturn(id, reason) {
    const resp = await fetch('/api/asset/system/return', {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ assetId: id, reason })
    });
    return resp.json();
}

async function apiSystemOffShelf(id, reason) {
    const resp = await fetch('/api/asset/system/off-shelf', {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ assetId: id, reason })
    });
    return resp.json();
}

async function apiSystemForceReview(id, reason) {
    const resp = await fetch('/api/asset/system/review/force', {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ assetId: id, reason, force: true })
    });
    return resp.json();
}

async function apiSystemHardDelete(id, reason) {
    const resp = await fetch('/api/asset/system/hard-delete', {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ assetId: id, reason, confirm: true })
    });
    return resp.json();
}
