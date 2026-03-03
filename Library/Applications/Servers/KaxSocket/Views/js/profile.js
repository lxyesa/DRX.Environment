/* 个人资料页面入口编排脚本（Task 6）
 * 职责：
 * 1) 统一初始化顺序（shared -> user/admin/orders）；
 * 2) 保留最小全局桥接函数，兼容现有调用点与 inline onclick；
 * 3) 不承载业务实现细节（细节已下沉至 profile.user/admin/orders）。
 */

// #region 路由状态
let targetUid = null;
const pathParts = window.location.pathname.split('/').filter(p => p);
if (pathParts.length >= 2 && pathParts[0] === 'profile' && pathParts[1]) {
    targetUid = pathParts[1];
}
// #endregion

// #region 标签页切换
(function initTabs() {
    const tabs = document.querySelectorAll('.profile-tab');
    const panels = document.querySelectorAll('.tab-panel');
    const adminTabLoaded = { 'admin-assets': false, 'admin-cdk': false };

    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            const target = tab.dataset.tab;
            tabs.forEach(t => {
                t.classList.remove('active');
                t.setAttribute('aria-selected', 'false');
            });
            panels.forEach(p => p.classList.remove('active'));

            tab.classList.add('active');
            tab.setAttribute('aria-selected', 'true');
            const panel = document.getElementById('panel-' + target);
            if (panel) panel.classList.add('active');

            if (target === 'admin-assets' && !adminTabLoaded['admin-assets']) {
                adminTabLoaded['admin-assets'] = true;
                loadAdminAssets(1);
            } else if (target === 'admin-cdk' && !adminTabLoaded['admin-cdk']) {
                adminTabLoaded['admin-cdk'] = true;
                loadAdminCdks(1);
            }
        });
    });
})();

document.getElementById('editProfileBtn')?.addEventListener('click', () => {
    const editTab = document.querySelector('[data-tab="edit"]');
    if (editTab) editTab.click();
});
// #endregion

// #region 公共工具与权限
const profileShared = window.ProfileShared || {};
let _isAdminUser = false;

function formatUnix(ts) {
    if (!ts || ts <= 0) return '-';
    try { return new Date(ts * 1000).toLocaleString(); } catch (e) { return '-'; }
}

function mapPermissionToRole(n) {
    switch (Number(n)) {
        case 0: return '系统';
        case 2: return '控制台';
        case 3: return '管理员';
        default: return '普通用户';
    }
}

function maskEmail(email) {
    if (!email || !email.includes('@')) return '—';
    const [local, domain] = email.split('@');
    const visible = Math.min(3, local.length);
    return local.slice(0, visible) + '****@' + domain;
}

function isAdminPermission(permissionGroup) {
    const group = Number(permissionGroup);
    return group === 0 || group === 2 || group === 3;
}

function showAdminTabs() {
    document.querySelectorAll('.admin-only-tab').forEach(el => el.style.display = '');
}

function hideAdminTabs() {
    document.querySelectorAll('.admin-only-tab').forEach(el => el.style.display = 'none');
}

function setAdminUser(flag) {
    _isAdminUser = !!flag;
}

function checkToken() {
    if (typeof profileShared.checkToken === 'function') {
        return profileShared.checkToken();
    }
    const token = localStorage.getItem('kax_login_token');
    if (!token) { location.href = '/login'; return null; }
    return token;
}

function showErrorMsg(el, text, isDanger = true) {
    if (typeof profileShared.showErrorMsg === 'function') {
        profileShared.showErrorMsg(el, text, isDanger);
        return;
    }
    if (!el) return;
    el.style.display = 'block';
    el.style.background = isDanger ? 'rgba(239,68,68,0.1)' : 'rgba(34,197,94,0.1)';
    el.style.borderColor = isDanger ? 'rgba(239,68,68,0.3)' : 'rgba(34,197,94,0.3)';
    el.style.color = isDanger ? 'var(--profile-danger)' : 'var(--profile-success)';
    el.textContent = text;
}

function setElementDisplay(el, show) {
    if (typeof profileShared.setElementDisplay === 'function') {
        profileShared.setElementDisplay(el, show);
        return;
    }
    if (el) el.style.display = show ? 'block' : 'none';
}

function setElementsDisplay(displayMap) {
    if (typeof profileShared.setElementsDisplay === 'function') {
        profileShared.setElementsDisplay(displayMap);
        return;
    }
    Object.entries(displayMap).forEach(([id, show]) => {
        const el = document.getElementById(id);
        if (el) el.style.display = show ? 'block' : 'none';
    });
}

async function withButtonLoading(btn, loadingText, fn) {
    if (typeof profileShared.withButtonLoading === 'function') {
        return profileShared.withButtonLoading(btn, loadingText, fn);
    }
    const originalText = btn.textContent;
    btn.disabled = true;
    btn.textContent = loadingText;
    try {
        return await fn();
    } finally {
        btn.disabled = false;
        btn.textContent = originalText;
    }
}

function escapeHtml(str) {
    if (typeof profileShared.escapeHtml === 'function') {
        return profileShared.escapeHtml(str);
    }
    if (!str) return '';
    const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
    return String(str).replace(/[&<>"']/g, m => map[m]);
}

function updatePaginationButtons(page, totalPages, prevBtnId, nextBtnId) {
    const prevBtn = document.getElementById(prevBtnId);
    const nextBtn = document.getElementById(nextBtnId);
    if (prevBtn) prevBtn.disabled = page <= 1;
    if (nextBtn) nextBtn.disabled = page >= totalPages;
}
// #endregion

// #region 模块初始化
const profileUser = window.ProfileUser || null;
const profileAdmin = window.ProfileAdmin || null;
const profileOrders = window.ProfileOrders || null;

if (profileUser && typeof profileUser.init === 'function') {
    profileUser.init({
        targetUid,
        formatUnix,
        mapPermissionToRole,
        maskEmail,
        isAdminPermission,
        showAdminTabs,
        hideAdminTabs,
        setAdminUser,
        setElementDisplay,
        setElementsDisplay,
        showErrorMsg,
        escapeHtml
    });
}

if (profileAdmin && typeof profileAdmin.init === 'function') {
    profileAdmin.init({
        checkToken,
        setElementDisplay,
        setElementsDisplay,
        withButtonLoading,
        escapeHtml,
        showErrorMsg,
        updatePaginationButtons,
        formatUnix
    });
}

if (profileOrders && typeof profileOrders.init === 'function') {
    profileOrders.init({
        checkToken,
        setElementDisplay,
        escapeHtml
    });
}
// #endregion

// #region 最小桥接（兼容历史调用点）
async function loadProfileFromServer() {
    if (profileUser && typeof profileUser.loadProfileFromServer === 'function') {
        return profileUser.loadProfileFromServer();
    }
}

function updateEditableState() {
    if (profileUser && typeof profileUser.updateEditableState === 'function') {
        return profileUser.updateEditableState();
    }
}

async function loadActiveAssets() {
    if (profileUser && typeof profileUser.loadActiveAssets === 'function') {
        return profileUser.loadActiveAssets();
    }
}

function openChangePlanModal(assetId, assetName) {
    if (profileUser && typeof profileUser.openChangePlanModal === 'function') {
        return profileUser.openChangePlanModal(assetId, assetName);
    }
}

function closePlanModal() {
    if (profileUser && typeof profileUser.closePlanModal === 'function') {
        return profileUser.closePlanModal();
    }
}

function openUnsubscribeModal(assetId, assetName) {
    if (profileUser && typeof profileUser.openUnsubscribeModal === 'function') {
        return profileUser.openUnsubscribeModal(assetId, assetName);
    }
}

function closeUnsubscribeModal() {
    if (profileUser && typeof profileUser.closeUnsubscribeModal === 'function') {
        return profileUser.closeUnsubscribeModal();
    }
}

function selectPlan(planId, element) {
    if (profileUser && typeof profileUser.selectPlan === 'function') {
        return profileUser.selectPlan(planId, element);
    }
}

async function loadAdminAssets(page = 1) {
    if (profileAdmin && typeof profileAdmin.loadAdminAssets === 'function') {
        return profileAdmin.loadAdminAssets(page);
    }
}

async function loadAdminCdks(page = 1) {
    if (profileAdmin && typeof profileAdmin.loadAdminCdks === 'function') {
        return profileAdmin.loadAdminCdks(page);
    }
}

async function loadUserOrders(page = 1) {
    if (profileOrders && typeof profileOrders.loadUserOrders === 'function') {
        return profileOrders.loadUserOrders(page);
    }
}

function searchOrders() {
    if (profileOrders && typeof profileOrders.searchOrders === 'function') {
        return profileOrders.searchOrders();
    }
}

function showOrderDetail(order) {
    if (profileOrders && typeof profileOrders.showOrderDetail === 'function') {
        return profileOrders.showOrderDetail(order);
    }
}
// #endregion

// #region 页面启动
async function initializePage() {
    if (profileUser && typeof profileUser.initializePage === 'function') {
        return profileUser.initializePage();
    }
}

initializePage();
// #endregion
