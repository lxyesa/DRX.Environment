(function () {
    const TOKEN_KEY = 'kax_login_token';

    const qs = new URLSearchParams(window.location.search);
    const clientId = (qs.get('client_id') || qs.get('clientId') || '').trim();
    const appName = (qs.get('app_name') || qs.get('application_name') || clientId || '第三方应用').trim();
    const appDesc = (qs.get('app_desc') || qs.get('application_description') || '').trim();
    const redirectUri = (qs.get('redirect_uri') || '').trim();
    const state = (qs.get('state') || '').trim();
    const scope = (qs.get('scope') || 'profile').trim();

    const leadEl = document.getElementById('oauthLead');
    const appNameEl = document.getElementById('appName');
    const clientIdEl = document.getElementById('clientId');
    const scopeEl = document.getElementById('scopeText');
    const redirectEl = document.getElementById('redirectUri');
    const stateEl = document.getElementById('stateText');
    const descRowEl = document.getElementById('descRow');
    const descEl = document.getElementById('appDesc');

    const approveBtn = document.getElementById('approveBtn');
    const rejectBtn = document.getElementById('rejectBtn');
    const goLoginBtn = document.getElementById('goLoginBtn');
    const errorEl = document.getElementById('error');

    function showError(msg) {
        errorEl.style.display = 'block';
        errorEl.textContent = msg || '请求参数错误';
    }

    function clearError() {
        errorEl.style.display = 'none';
        errorEl.textContent = '';
    }

    function isValidHttpUrl(url) {
        try {
            const u = new URL(url);
            return u.protocol === 'http:' || u.protocol === 'https:';
        } catch {
            return false;
        }
    }

    function appendQuery(url, key, value) {
        const sep = url.indexOf('?') >= 0 ? '&' : '?';
        return `${url}${sep}${encodeURIComponent(key)}=${encodeURIComponent(value)}`;
    }

    function getCurrentAuthUrl() {
        return `${window.location.pathname}${window.location.search}`;
    }

    function toLogin() {
        const target = encodeURIComponent(getCurrentAuthUrl());
        window.location.href = `/login?redirect=${target}`;
    }

    function setLoading(on) {
        approveBtn.disabled = on;
        rejectBtn.disabled = on;
        goLoginBtn.disabled = on;
        approveBtn.textContent = on ? '授权中...' : '同意并继续';
    }

    appNameEl.textContent = appName || '--';
    clientIdEl.textContent = clientId || '--';
    scopeEl.textContent = scope || 'profile';
    redirectEl.textContent = redirectUri || '--';
    stateEl.textContent = state || '(无)';

    if (appDesc) {
        descRowEl.style.display = '';
        descEl.textContent = appDesc;
    }

    if (!clientId || !redirectUri || !isValidHttpUrl(redirectUri)) {
        leadEl.textContent = '授权请求不完整，无法继续。';
        showError('缺少必要参数：client_id / redirect_uri，或 redirect_uri 非法');
        setLoading(true);
        goLoginBtn.disabled = false;
        rejectBtn.disabled = false;
        return;
    }

    leadEl.textContent = `${appName} 正在请求使用你的 KaxHub 账号登录。`;

    goLoginBtn.addEventListener('click', function () {
        toLogin();
    });

    rejectBtn.addEventListener('click', function () {
        if (isValidHttpUrl(redirectUri)) {
            let denyUrl = appendQuery(redirectUri, 'error', 'access_denied');
            if (state) denyUrl = appendQuery(denyUrl, 'state', state);
            window.location.href = denyUrl;
            return;
        }
        window.location.href = '/';
    });

    approveBtn.addEventListener('click', async function () {
        clearError();
        const token = localStorage.getItem(TOKEN_KEY);
        if (!token) {
            showError('你尚未登录 KaxHub，请先登录。');
            toLogin();
            return;
        }

        setLoading(true);
        try {
            await window.ensureMinLoadDelay(500);

            const resp = await fetch('/api/oauth/authorize/confirm', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer ' + token
                },
                body: JSON.stringify({
                    clientId: clientId,
                    applicationName: appName,
                    applicationDescription: appDesc,
                    redirectUri: redirectUri,
                    state: state,
                    scope: scope,
                    approve: true
                })
            });

            const text = await resp.text();
            let data = {};
            try { data = JSON.parse(text); } catch { }

            if (resp.status === 200) {
                if (data.redirectUrl) {
                    window.location.href = data.redirectUrl;
                    return;
                }
                showError('授权成功，但缺少跳转地址。');
                return;
            }

            if (resp.status === 401) {
                try { localStorage.removeItem(TOKEN_KEY); } catch (_) { }
                showError('登录状态已失效，请重新登录。');
                toLogin();
                return;
            }

            showError(data.message || data.error_description || text || '授权失败，请稍后重试。');
        } catch (err) {
            console.error('[oauth_authorize] 授权请求失败:', err);
            showError('网络错误，无法提交授权请求。');
        } finally {
            setLoading(false);
        }
    });
})();

window.initGlobalFooter && window.initGlobalFooter();
