/* 个人资料页面交互脚本 — 所有注释使用中文，遵循项目约定 */

// #region DOM 元素引用
const saveBtn = document.getElementById('saveBtn');
const cancelBtn = document.getElementById('cancelBtn');
const avatarFile = document.getElementById('avatarFile');
const avatarImg = document.getElementById('avatarImg');
const avatarInitials = document.getElementById('avatarInitials');
const avatarContainer = document.getElementById('avatarContainer');
// #endregion

// #region 状态变量
let originalProfile = { name: '', handle: '', email: '', role: '', bio: '', signature: '', avatarSrc: '' };
let targetUid = null;
let currentUserUid = null;
let isViewingOtherProfile = false;

const pathParts = window.location.pathname.split('/').filter(p => p);
if (pathParts.length >= 2 && pathParts[0] === 'profile' && pathParts[1]) {
    targetUid = pathParts[1];
}
// #endregion

// #region 标签页切换
(function initTabs() {
    const tabs = document.querySelectorAll('.profile-tab');
    const panels = document.querySelectorAll('.tab-panel');

    /* 记录管理员选项卡是否已加载过数据 */
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

            // 首次切换到管理员选项卡时加载数据
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

/* 侧边栏"编辑个人资料"按钮跳转到编辑标签页 */
document.getElementById('editProfileBtn').addEventListener('click', () => {
    const editTab = document.querySelector('[data-tab="edit"]');
    if (editTab) editTab.click();
});
// #endregion

// #region 工具函数
function formatUnix(ts) {
    if (!ts || ts <= 0) return '-';
    try { return new Date(ts * 1000).toLocaleString(); } catch (e) { return '-'; }
}

function mapPermissionToRole(n) {
    switch (Number(n)) {
        case 0: return '控制台';
        case 1: return 'Root';
        case 2: return '管理员';
        default: return '普通用户';
    }
}

/** 邮箱脱敏：user@domain.com → use****@domain.com */
function maskEmail(email) {
    if (!email || !email.includes('@')) return '—';
    const [local, domain] = email.split('@');
    const visible = Math.min(3, local.length);
    return local.slice(0, visible) + '****@' + domain;
}

/** 检查 Token 和重定向 */
function checkToken() {
    const token = localStorage.getItem('kax_login_token');
    if (!token) { location.href = '/login'; return null; }
    return token;
}

/** 设置错误消息样式 */
function showErrorMsg(el, text, isDanger = true) {
    if (!el) return;
    el.style.display = 'block';
    el.style.background = isDanger ? 'rgba(239,68,68,0.1)' : 'rgba(34,197,94,0.1)';
    el.style.borderColor = isDanger ? 'rgba(239,68,68,0.3)' : 'rgba(34,197,94,0.3)';
    el.style.color = isDanger ? 'var(--profile-danger)' : 'var(--profile-success)';
    el.textContent = text;
}

/** 设置元素显示状态 */
function setElementDisplay(el, show) {
    if (el) el.style.display = show ? 'block' : 'none';
}

/** 批量设置元素显示状态 */
function setElementsDisplay(displayMap) {
    Object.entries(displayMap).forEach(([id, show]) => {
        setElementDisplay(document.getElementById(id), show);
    });
}

/** 设置按钮状态 */
async function withButtonLoading(btn, loadingText, fn) {
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

/** HTML 转义 */
function escapeHtml(str) {
    if (!str) return '';
    return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}
// #endregion

// #region 错误页面显示
function showErrorPage(message = '资料不存在或已被删除。请检查 UID 是否正确。') {
    const errorContainer = document.getElementById('errorContainer');
    const mainContent = document.getElementById('mainContent');
    const errorMsg = errorContainer.querySelector('.error-message');
    if (errorMsg) errorMsg.textContent = message;
    errorContainer.classList.add('show');
    mainContent.style.display = 'none';
}
// #endregion

// #region 加载用户资料
async function loadProfileFromServer() {
    const token = localStorage.getItem('kax_login_token');
    if (!token) { location.href = '/login'; return; }

    try {
        const endpoint = targetUid ? `/api/user/profile/${targetUid}` : '/api/user/profile';

        const cachedAvatar = localStorage.getItem('kax_avatar_versions');
        const hasCachedVersions = cachedAvatar && cachedAvatar !== '{}';
        if (!hasCachedVersions) {
            avatarImg.style.display = 'none';
            avatarInitials.style.display = 'block';
        }

        let selfUid = null;
        if (targetUid) {
            try {
                const selfResp = await fetch('/api/user/profile', { headers: { 'Authorization': 'Bearer ' + token } });
                if (selfResp.status === 200) {
                    const selfData = await selfResp.json();
                    selfUid = (typeof selfData.id !== 'undefined') ? selfData.id : null;
                }
            } catch (e) { /* 容错 */ }
        }

        const resp = await fetch(endpoint, { headers: { 'Authorization': 'Bearer ' + token } });
        if (resp.status === 200) {
            const data = await resp.json();
            const user = data.user || '';
            const displayName = data.displayName || user;
            const email = data.email || '';
            const bio = data.bio || '';
            const signature = data.signature || '';
            const registeredAt = data.registeredAt || 0;
            const lastLoginAt = data.lastLoginAt || 0;
            const roleText = mapPermissionToRole(data.permissionGroup);
            const uid = (typeof data.id !== 'undefined') ? data.id : null;
            const isBanned = !!data.isBanned;
            const banReason = data.banReason || '';
            const banExpiresAt = data.banExpiresAt || 0;

            // 使用 AvatarCache 加载头像
            const serverAvatar = data.avatarUrl || '';
            if (serverAvatar && window.AvatarCache) {
                try {
                    const resolvedUrl = await AvatarCache.getAvatar(serverAvatar);
                    avatarImg.src = resolvedUrl;
                    avatarImg.style.display = 'block';
                    avatarInitials.style.display = 'none';
                } catch (e) {
                    avatarImg.src = serverAvatar;
                    avatarImg.style.display = 'block';
                    avatarInitials.style.display = 'none';
                }
            } else if (serverAvatar) {
                avatarImg.src = serverAvatar;
                avatarImg.style.display = 'block';
                avatarInitials.style.display = 'none';
            } else {
                avatarImg.style.display = 'none';
                avatarInitials.style.display = 'block';
                avatarInitials.textContent = (displayName || '?').charAt(0).toUpperCase();
            }

            // 侧边栏
            document.getElementById('displayName').textContent = displayName;
            document.getElementById('displayHandle').textContent = '@' + user;
            document.getElementById('displayBio').textContent = bio;
            document.getElementById('role').textContent = roleText;
            const emailEl = document.getElementById('currentEmailDisplay');
            if (emailEl) emailEl.textContent = maskEmail(email);
            const uidEl = document.getElementById('uid');
            if (uidEl) uidEl.textContent = uid ? String(uid) : '-';
            document.getElementById('joined').textContent = formatUnix(registeredAt);
            document.getElementById('lastLogin').textContent = formatUnix(lastLoginAt);

            // 封禁状态
            const banRow = document.getElementById('metaBan');
            const banEl = document.getElementById('banStatus');
            if (isBanned) {
                banRow.style.display = 'flex';
                banEl.textContent = `已封禁（到期: ${formatUnix(banExpiresAt)}${banReason ? ' 原因: ' + banReason : ''}）`;
                banEl.style.color = 'var(--profile-danger)';
            } else {
                banRow.style.display = 'none';
            }

            // 统计数据
            document.getElementById('statResourceCount').textContent = (data.assetCount ?? data.resourceCount ?? 0).toString();
            document.getElementById('statGold').textContent = (data.gold || 0).toLocaleString();
            const goldEl = document.getElementById('gold');
            if (goldEl) goldEl.textContent = (data.gold || 0).toLocaleString();

            // 概览面板
            document.getElementById('overviewBio').textContent = bio || '这个人很懒，什么都没有留下。';
            document.getElementById('overviewSignature').textContent = signature || '—';
            document.getElementById('overviewRole').textContent = roleText;
            document.getElementById('overviewUid').textContent = uid ? String(uid) : '—';
            document.getElementById('overviewGold').textContent = (data.gold || 0).toLocaleString();
            document.getElementById('overviewResources').textContent = (data.assetCount ?? data.resourceCount ?? 0).toString();
            document.getElementById('overviewJoined').textContent = formatUnix(registeredAt);
            document.getElementById('overviewLastLogin').textContent = formatUnix(lastLoginAt);

            // 编辑面板
            document.getElementById('inputName').value = displayName;
            document.getElementById('inputHandle').value = user;
            document.getElementById('inputRole').value = roleText;
            document.getElementById('inputBio').value = bio;
            document.getElementById('inputSignature').value = signature;

            originalProfile = { name: displayName, handle: user, email, role: roleText, bio, signature, avatarSrc: serverAvatar || (avatarImg.src || '') };

            if (uid) currentUserUid = targetUid ? selfUid : uid;

            const resolvedSelfUid = targetUid ? selfUid : uid;
            isViewingOtherProfile = !!(targetUid && resolvedSelfUid && targetUid !== String(resolvedSelfUid));

            // 管理员选项卡权限控制：仅自己的资料页且是管理员时显示
            if (!isViewingOtherProfile && isAdminPermission(data.permissionGroup)) {
                _isAdminUser = true;
                showAdminTabs();
            } else {
                _isAdminUser = false;
                hideAdminTabs();
            }

            updateEditableState();
        } else if (resp.status === 401) {
            localStorage.removeItem('kax_login_token');
            location.href = '/login';
        } else if (resp.status === 403) {
            alert('账号被封禁，无法访问资料页。');
            location.href = '/login';
        } else if (resp.status === 404) {
            showErrorPage('抱歉，你访问的用户资料不存在或已被删除。请检查 UID 是否正确。');
        } else {
            console.warn('读取用户资料失败：', resp.status);
            showErrorPage('加载资料失败，请稍后重试。');
        }
    } catch (err) {
        console.error('加载用户资料时发生错误：', err);
        showErrorPage('加载资料时发生错误，请稍后重试。');
    }
}
// #endregion

// #region 权限控制——查看他人资料时隐藏编辑功能
function updateEditableState() {
    const tabEdit = document.getElementById('tabEdit');
    const tabSecurity = document.getElementById('tabSecurity');
    const tabAssets = document.getElementById('tabAssets');
    const sidebarActions = document.getElementById('sidebarActions');
    const avatarOverlay = avatarContainer.querySelector('.avatar-overlay');

    if (isViewingOtherProfile) {
        if (tabEdit) tabEdit.style.display = 'none';
        if (tabSecurity) tabSecurity.style.display = 'none';
        if (tabAssets) tabAssets.style.display = 'none';
        if (sidebarActions) sidebarActions.style.display = 'none';
        if (avatarContainer) {
            avatarContainer.style.cursor = 'default';
            avatarContainer.style.pointerEvents = 'none';
        }
        if (avatarOverlay) avatarOverlay.style.display = 'none';
    } else {
        if (tabEdit) tabEdit.style.display = '';
        if (tabSecurity) tabSecurity.style.display = '';
        if (tabAssets) tabAssets.style.display = '';
        if (sidebarActions) sidebarActions.style.display = '';
        if (avatarContainer) {
            avatarContainer.style.cursor = 'pointer';
            avatarContainer.style.pointerEvents = 'auto';
        }
        if (avatarOverlay) avatarOverlay.style.display = 'flex';
    }
}
// #endregion

// #region 保存资料
document.getElementById('profileForm').addEventListener('submit', async (ev) => {
    ev.preventDefault();
    if (isViewingOtherProfile) { alert('无法编辑他人资料'); return; }

    const token = localStorage.getItem('kax_login_token');
    if (!token) { location.href = '/login'; return; }

    const displayName = document.getElementById('inputName').value.trim();
    const bio = document.getElementById('inputBio').value || '';
    const signature = document.getElementById('inputSignature').value || '';

    saveBtn.disabled = true;
    try {
        const resp = await fetch('/api/user/profile', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
            body: JSON.stringify({ displayName, bio, signature, targetUid: currentUserUid })
        });

        const result = await resp.json().catch(() => ({}));
        if (resp.status === 200) {
            document.getElementById('displayName').textContent = displayName || originalProfile.name;
            document.getElementById('displayBio').textContent = bio;
            document.getElementById('overviewBio').textContent = bio || '这个人很懒，什么都没有留下。';
            document.getElementById('overviewSignature').textContent = signature || '—';
            originalProfile = { ...originalProfile, name: displayName || originalProfile.name, bio, signature };
            const leftEmail = document.getElementById('currentEmailDisplay');
            if (leftEmail) leftEmail.textContent = maskEmail(originalProfile.email);
            alert(result.message || '资料已保存');
        } else if (resp.status === 401) {
            localStorage.removeItem('kax_login_token');
            location.href = '/login';
        } else {
            alert(result.message || ('保存失败：' + resp.status));
        }
    } catch (err) {
        console.error(err);
        alert('无法连接到服务器');
    } finally {
        saveBtn.disabled = false;
    }
});
// #endregion

// #region 取消编辑
cancelBtn.addEventListener('click', () => {
    document.getElementById('inputName').value = originalProfile.name;
    document.getElementById('inputHandle').value = originalProfile.handle;
    document.getElementById('inputRole').value = originalProfile.role;
    document.getElementById('inputBio').value = originalProfile.bio;
    document.getElementById('inputSignature').value = originalProfile.signature;
    if (originalProfile.avatarSrc && !originalProfile.avatarSrc.endsWith('/default-avatar.jpg')) {
        avatarImg.src = originalProfile.avatarSrc;
        avatarImg.style.display = 'block';
        avatarInitials.style.display = 'none';
    } else {
        avatarImg.style.display = 'none';
        avatarInitials.style.display = 'block';
    }
});
// #endregion

// #region 头像上传交互（含裁剪）

// ---- 裁剪弹窗状态 ----
const _cropModal    = document.getElementById('avatarCropModal');
const _cropImg      = document.getElementById('cropSourceImg');
const _cropOverlay  = document.getElementById('cropOverlay');
const _cropViewport = document.getElementById('cropViewport');
const _cropZoom     = document.getElementById('cropZoomRange');
const _cropConfirm  = document.getElementById('cropConfirmBtn');
const _cropCancel   = document.getElementById('cropCancelBtn');

let _cropNatW = 0, _cropNatH = 0;   // 原图自然尺寸
let _cropVpW  = 0, _cropVpH  = 0;   // viewport 像素尺寸
let _cropMinScale = 0.1;             // 最小缩放（使图像填满圆圈）
let _cropMaxScale = 4;
let _cropScale = 1;
let _cropOffX  = 0, _cropOffY = 0;  // 图像左上角在 viewport 中的偏移（px）
let _cropDragging = false;
let _cropDragStartX = 0, _cropDragStartY = 0;
let _cropDragOffX0 = 0, _cropDragOffY0 = 0;
let _cropFile = null;

/** 把 scale 限制在 [min, max] */
function _cropClampScale(s) {
    return Math.max(_cropMinScale, Math.min(_cropMaxScale, s));
}

/** 把偏移限制在「图像始终完全覆盖圆形裁剪框」范围内 */
function _cropClampOffset(ox, oy, scale) {
    const imgW = _cropNatW * scale;
    const imgH = _cropNatH * scale;
    // 圆形裁剪框 = viewport 正中，直径 = min(vW, vH)
    const circleR = Math.min(_cropVpW, _cropVpH) / 2;
    const cx = _cropVpW / 2, cy = _cropVpH / 2;
    // 图像必须覆盖圆：left <= cx - circleR，right >= cx + circleR
    const minOx = cx + circleR - imgW;
    const maxOx = cx - circleR;
    const minOy = cy + circleR - imgH;
    const maxOy = cy - circleR;
    return {
        x: Math.max(minOx, Math.min(maxOx, ox)),
        y: Math.max(minOy, Math.min(maxOy, oy))
    };
}

/** 将当前 offset 重置为居中 */
function _cropCenter() {
    _cropOffX = (_cropVpW - _cropNatW * _cropScale) / 2;
    _cropOffY = (_cropVpH - _cropNatH * _cropScale) / 2;
    const clamped = _cropClampOffset(_cropOffX, _cropOffY, _cropScale);
    _cropOffX = clamped.x;
    _cropOffY = clamped.y;
}

/** 更新图片 transform */
function _cropApplyTransform() {
    _cropImg.style.transform = `translate(${_cropOffX}px, ${_cropOffY}px) scale(${_cropScale})`;
    _cropDrawOverlay();
}

/** 绘制遮罩层：四角半透明 + 圆形透明孔 + 白色圆框 */
function _cropDrawOverlay() {
    const canvas = _cropOverlay;
    const vW = _cropVpW, vH = _cropVpH;
    if (!vW || !vH) return;
    canvas.width  = vW;
    canvas.height = vH;
    const ctx = canvas.getContext('2d');
    ctx.clearRect(0, 0, vW, vH);
    const cx = vW / 2, cy = vH / 2;
    const r  = Math.min(vW, vH) / 2 - 4; // 4px 内边距
    // 暗角
    ctx.fillStyle = 'rgba(0,0,0,0.55)';
    ctx.fillRect(0, 0, vW, vH);
    // 抠出圆形
    ctx.globalCompositeOperation = 'destination-out';
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.fill();
    // 白边
    ctx.globalCompositeOperation = 'source-over';
    ctx.strokeStyle = 'rgba(255,255,255,0.85)';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.stroke();
    // 十字参考线
    ctx.strokeStyle = 'rgba(255,255,255,0.25)';
    ctx.lineWidth = 1;
    ctx.setLineDash([4, 4]);
    ctx.beginPath();
    ctx.moveTo(cx - r, cy); ctx.lineTo(cx + r, cy);
    ctx.moveTo(cx, cy - r); ctx.lineTo(cx, cy + r);
    ctx.stroke();
    ctx.setLineDash([]);
}

/** 计算最小 scale（图像能填满圆圈）*/
function _cropCalcMinScale() {
    const r = Math.min(_cropVpW, _cropVpH) / 2 - 4;
    const diameter = r * 2;
    return Math.max(diameter / _cropNatW, diameter / _cropNatH);
}

/** 打开裁剪弹窗 */
function _cropOpen(file) {
    _cropFile = file;
    const reader = new FileReader();
    reader.onload = (e) => {
        _cropImg.onload = () => {
            _cropNatW = _cropImg.naturalWidth;
            _cropNatH = _cropImg.naturalHeight;
            // 等 viewport 渲染完毕再取尺寸
            requestAnimationFrame(() => {
                const rect = _cropViewport.getBoundingClientRect();
                _cropVpW = rect.width || _cropViewport.offsetWidth;
                _cropVpH = rect.height || _cropViewport.offsetHeight;
                _cropMinScale = _cropCalcMinScale();
                _cropScale = _cropClampScale(_cropMinScale * 1.05);
                _cropZoom.value = 50;
                _cropCenter();
                _cropApplyTransform();
            });
        };
        _cropImg.src = e.target.result;
        _cropModal.classList.add('active');
    };
    reader.readAsDataURL(file);
}

/** 关闭裁剪弹窗 */
function _cropClose() {
    _cropModal.classList.remove('active');
    avatarFile.value = '';
}

// ---- 缩放滑竿 ----
_cropZoom.addEventListener('input', () => {
    const t     = _cropZoom.value / 100; // 0~1
    const newS  = _cropMinScale + (_cropMaxScale - _cropMinScale) * t;
    // 以圆心为缩放中心
    const cx = _cropVpW / 2, cy = _cropVpH / 2;
    _cropOffX = cx - (cx - _cropOffX) * (newS / _cropScale);
    _cropOffY = cy - (cy - _cropOffY) * (newS / _cropScale);
    _cropScale = newS;
    const clamped = _cropClampOffset(_cropOffX, _cropOffY, _cropScale);
    _cropOffX = clamped.x;
    _cropOffY = clamped.y;
    _cropApplyTransform();
});

// ---- 滚轮缩放 ----
_cropViewport.addEventListener('wheel', (e) => {
    e.preventDefault();
    const delta = e.deltaY > 0 ? -0.08 : 0.08;
    const newS  = _cropClampScale(_cropScale + delta * _cropScale);
    const rect  = _cropViewport.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;
    _cropOffX = mx - (mx - _cropOffX) * (newS / _cropScale);
    _cropOffY = my - (my - _cropOffY) * (newS / _cropScale);
    _cropScale = newS;
    const clamped = _cropClampOffset(_cropOffX, _cropOffY, _cropScale);
    _cropOffX = clamped.x;
    _cropOffY = clamped.y;
    // 将 scale 同步回滑竿
    _cropZoom.value = ((_cropScale - _cropMinScale) / (_cropMaxScale - _cropMinScale)) * 100;
    _cropApplyTransform();
}, { passive: false });

// ---- 鼠标拖动 ----
_cropViewport.addEventListener('mousedown', (e) => {
    e.preventDefault();
    _cropDragging   = true;
    _cropViewport.style.cursor = 'grabbing';
    _cropDragStartX = e.clientX;
    _cropDragStartY = e.clientY;
    _cropDragOffX0  = _cropOffX;
    _cropDragOffY0  = _cropOffY;
});
window.addEventListener('mousemove', (e) => {
    if (!_cropDragging) return;
    const dx = e.clientX - _cropDragStartX;
    const dy = e.clientY - _cropDragStartY;
    const clamped = _cropClampOffset(_cropDragOffX0 + dx, _cropDragOffY0 + dy, _cropScale);
    _cropOffX = clamped.x;
    _cropOffY = clamped.y;
    _cropApplyTransform();
});
window.addEventListener('mouseup', () => {
    if (_cropDragging) {
        _cropDragging = false;
        _cropViewport.style.cursor = 'grab';
    }
});

// ---- 触控拖动 ----
let _touchPrevX = 0, _touchPrevY = 0, _touchStartDist = 0, _touchStartScale = 1;
_cropViewport.addEventListener('touchstart', (e) => {
    if (e.touches.length === 1) {
        _touchPrevX = e.touches[0].clientX;
        _touchPrevY = e.touches[0].clientY;
    } else if (e.touches.length === 2) {
        _touchStartDist  = Math.hypot(e.touches[1].clientX - e.touches[0].clientX, e.touches[1].clientY - e.touches[0].clientY);
        _touchStartScale = _cropScale;
    }
    e.preventDefault();
}, { passive: false });
_cropViewport.addEventListener('touchmove', (e) => {
    e.preventDefault();
    if (e.touches.length === 1) {
        const dx = e.touches[0].clientX - _touchPrevX;
        const dy = e.touches[0].clientY - _touchPrevY;
        _touchPrevX = e.touches[0].clientX;
        _touchPrevY = e.touches[0].clientY;
        const clamped = _cropClampOffset(_cropOffX + dx, _cropOffY + dy, _cropScale);
        _cropOffX = clamped.x;
        _cropOffY = clamped.y;
        _cropApplyTransform();
    } else if (e.touches.length === 2) {
        const dist  = Math.hypot(e.touches[1].clientX - e.touches[0].clientX, e.touches[1].clientY - e.touches[0].clientY);
        const newS  = _cropClampScale(_touchStartScale * (dist / _touchStartDist));
        const cx = (_cropVpW) / 2, cy = (_cropVpH) / 2;
        _cropOffX = cx - (cx - _cropOffX) * (newS / _cropScale);
        _cropOffY = cy - (cy - _cropOffY) * (newS / _cropScale);
        _cropScale = newS;
        const clamped = _cropClampOffset(_cropOffX, _cropOffY, _cropScale);
        _cropOffX = clamped.x;
        _cropOffY = clamped.y;
        _cropZoom.value = ((_cropScale - _cropMinScale) / (_cropMaxScale - _cropMinScale)) * 100;
        _cropApplyTransform();
    }
}, { passive: false });

// ---- 取消 ----
_cropCancel.addEventListener('click', _cropClose);
_cropModal.addEventListener('click', (e) => {
    if (e.target === _cropModal) _cropClose();
});

// ---- 确认并上传 ----
_cropConfirm.addEventListener('click', async () => {
    // 将圆形区域绘制到离屏 canvas，输出正方形 PNG
    const r       = Math.min(_cropVpW, _cropVpH) / 2 - 4;
    const diameter = Math.round(r * 2);
    const cx = _cropVpW / 2, cy = _cropVpH / 2;
    const offscreen = document.createElement('canvas');
    offscreen.width = offscreen.height = diameter;
    const ctx = offscreen.getContext('2d');

    // 裁剪为圆形（圆形 mask，实际后端存储正方形即可，上传的是方形裁剪结果）
    ctx.drawImage(
        _cropImg,
        (cx - r - _cropOffX) / _cropScale,   // srcX
        (cy - r - _cropOffY) / _cropScale,   // srcY
        diameter / _cropScale,                // srcW
        diameter / _cropScale,                // srcH
        0, 0, diameter, diameter             // dst
    );

    offscreen.toBlob(async (blob) => {
        if (!blob) { alert('裁剪失败，请重试'); return; }

        const token = localStorage.getItem('kax_login_token');
        if (!token) { location.href = '/login'; return; }

        _cropConfirm.disabled = true;
        _cropConfirm.textContent = '上传中…';
        try {
            const fd = new FormData();
            fd.append('avatar', blob, 'avatar.png');
            const resp = await fetch('/api/user/avatar', {
                method: 'POST',
                headers: { 'Authorization': 'Bearer ' + token },
                body: fd
            });
            const json = await resp.json().catch(() => ({}));
            if (resp.ok && json.url) {
                // 立即更新页面头像
                if (window.AvatarCache) {
                    try {
                        await AvatarCache.updateCache(json.url, blob);
                        const resolved = await AvatarCache.getAvatar(json.url);
                        avatarImg.src = resolved;
                    } catch (e) { avatarImg.src = json.url; }
                } else {
                    avatarImg.src = json.url;
                }
                avatarImg.style.display = 'block';
                avatarInitials.style.display = 'none';
                originalProfile.avatarSrc = json.url;
                _cropClose();
            } else if (resp.status === 401) {
                localStorage.removeItem('kax_login_token');
                location.href = '/login';
            } else {
                alert(json.message || '头像上传失败');
            }
        } catch (err) {
            console.error(err);
            alert('无法连接到服务器，请稍后重试');
        } finally {
            _cropConfirm.disabled = false;
            _cropConfirm.textContent = '确认并上传';
        }
    }, 'image/png');
});

// ---- 触发文件选择 ----
avatarContainer.addEventListener('click', () => {
    if (!isViewingOtherProfile) avatarFile.click();
});
avatarContainer.addEventListener('keydown', (e) => {
    if (!isViewingOtherProfile && (e.key === 'Enter' || e.key === ' ')) {
        e.preventDefault();
        avatarFile.click();
    }
});
avatarFile.addEventListener('change', (ev) => {
    const file = ev.target.files[0];
    if (!file) return;
    _cropOpen(file);
});
// #endregion

// #region 修改密码
document.getElementById('changePwBtn').addEventListener('click', async () => {
    const pwOldEl = document.getElementById('pwOld');
    const pw1El = document.getElementById('pw1');
    const pw2El = document.getElementById('pw2');
    if (!pwOldEl || !pw1El || !pw2El) { alert('修改密码表单未加载完毕，请刷新页面后重试。'); return; }

    const oldPw = pwOldEl.value || '';
    const newPw = pw1El.value || '';
    const confirmPw = pw2El.value || '';

    if (!oldPw) { alert('请输入当前密码'); return; }
    if (newPw.length < 8) { alert('新密码长度至少 8 位'); return; }
    if (newPw !== confirmPw) { alert('两次新密码不匹配'); return; }

    const token = checkToken();
    if (!token) return;

    const btn = document.getElementById('changePwBtn');
    await withButtonLoading(btn, '更新中...', async () => {
        try {
            const resp = await fetch('/api/user/password', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                body: JSON.stringify({ oldPassword: oldPw, newPassword: newPw, confirmPassword: confirmPw })
            });
            const result = await resp.json().catch(() => ({}));
            if (resp.status === 200) {
                alert(result.message || '密码已更新');
                pwOldEl.value = '';
                pw1El.value = '';
                pw2El.value = '';
            } else if (resp.status === 401) {
                localStorage.removeItem('kax_login_token');
                location.href = '/login';
            } else {
                alert(result.message || ('修改失败：' + resp.status));
            }
        } catch (err) {
            console.error(err);
            alert('无法连接到服务器');
        }
    });
});
// #endregion

// #region 加载激活资产
async function loadActiveAssets() {
    const token = checkToken();
    if (!token) return;

    const assetsLoading = document.getElementById('assetsLoading');
    const assetsEmpty = document.getElementById('assetsEmpty');
    const assetsList = document.getElementById('assetsList');
    const assetsCount = document.getElementById('assetsCount');

    try {
        const resp = await fetch('/api/user/assets/active', { headers: { 'Authorization': 'Bearer ' + token } });
        if (resp.status === 200) {
            const result = await resp.json().catch(() => ({}));
            const assets = result.data || [];
            setElementDisplay(assetsLoading, false);

            if (assets.length === 0) {
                setElementsDisplay({ 'assetsEmpty': true });
                assetsCount.textContent = '0 个';
            } else {
                setElementsDisplay({ 'assetsEmpty': false });
                assetsCount.textContent = `${assets.length} 个`;

                const assetNameCache = {};
                async function fetchAssetName(id) {
                    if (assetNameCache[id]) return assetNameCache[id];
                    try {
                        const r = await fetch(`/api/asset/name/${id}`);
                        if (r.status === 200) {
                            const j = await r.json().catch(() => ({}));
                            assetNameCache[id] = j.name || `资源 #${id}`;
                            return assetNameCache[id];
                        }
                    } catch (e) { /* ignore */ }
                    assetNameCache[id] = `资源 #${id}`;
                    return assetNameCache[id];
                }

                assetsList.innerHTML = '';
                let countActive = 0, countExpired = 0, countForever = 0;
                for (const asset of assets) {
                    const activatedTime = new Date(asset.activatedAt).toLocaleDateString();
                    let expiresText = '';
                    let remainingText = '';

                    if (asset.expiresAt === 0) {
                        expiresText = '永久有效';
                        remainingText = '无限期';
                    } else {
                        const expiresTime = new Date(asset.expiresAt);
                        expiresText = expiresTime.toLocaleDateString();
                        if (asset.remainingSeconds < 0) {
                            remainingText = '已过期';
                        } else if (asset.remainingSeconds === 0) {
                            remainingText = '即将过期';
                        } else {
                            const days = Math.floor(asset.remainingSeconds / 86400);
                            const hours = Math.floor((asset.remainingSeconds % 86400) / 3600);
                            remainingText = days > 0 ? `${days} 天 ${hours} 小时` : hours > 0 ? `${hours} 小时` : `${asset.remainingSeconds} 秒`;
                        }
                    }

                    const name = await fetchAssetName(asset.assetId);
                    const isExpired = asset.remainingSeconds < 0;
                    const isForever = asset.expiresAt === 0;
                    const statusClass = isExpired ? 'expired' : isForever ? 'forever' : 'active';
                    const statusLabel = isExpired ? '已过期' : isForever ? '永久' : '有效';

                    if (isExpired) countExpired++;
                    else if (isForever) countForever++;
                    else countActive++;

                    assetsList.insertAdjacentHTML('beforeend', `
                        <div class="asset-card">
                            <div class="asset-card-top">
                                <div class="asset-name">${name}</div>
                                <span class="asset-status asset-status--${statusClass}">${statusLabel}</span>
                            </div>
                            <div class="asset-meta">
                                <div class="asset-meta-item">
                                    <span class="asset-meta-label">剩余：</span>
                                    <span class="asset-meta-value ${isExpired ? 'text-danger' : ''}">${remainingText}</span>
                                </div>
                                <div class="asset-meta-item">
                                    <span class="asset-meta-label">激活于：</span>
                                    <span class="asset-meta-value">${activatedTime}</span>
                                </div>
                                <div class="asset-meta-item">
                                    <span class="asset-meta-label">到期：</span>
                                    <span class="asset-meta-value">${expiresText}</span>
                                </div>
                            </div>
                            <div class="asset-actions" data-asset-id="${asset.assetId}" data-asset-name="${name}">
                                <button class="asset-action-btn" data-action="changePlan">
                                    <span class="material-icons">swap_horiz</span>更变
                                </button>
                                <button class="asset-action-btn danger" data-action="unsubscribe">
                                    <span class="material-icons">cancel</span>退订
                                </button>
                            </div>
                        </div>
                    `);
                }

                // 更新概览统计
                const elTotal   = document.getElementById('assetsSummaryTotal');
                const elActive  = document.getElementById('assetsSummaryActive');
                const elExpired = document.getElementById('assetsSummaryExpired');
                const elForever = document.getElementById('assetsSummaryForever');
                if (elTotal)   elTotal.textContent   = assets.length;
                if (elActive)  elActive.textContent  = countActive;
                if (elExpired) elExpired.textContent = countExpired;
                if (elForever) elForever.textContent = countForever;
            }
        } else if (resp.status === 401) {
            localStorage.removeItem('kax_login_token');
            location.href = '/login';
        } else {
            setElementDisplay(assetsLoading, false);
            setElementDisplay(assetsEmpty, true);
            assetsEmpty.textContent = '无法加载资产列表';
        }
    } catch (err) {
        console.error('加载激活资产时发生错误：', err);
        setElementDisplay(assetsLoading, false);
        setElementDisplay(assetsEmpty, true);
        assetsEmpty.textContent = '加载失败，请重试';
    }
}
// #endregion

// #region CDK 激活
const cdkInput = document.getElementById('cdkInput');
const activateCdkBtn = document.getElementById('activateCdkBtn');
const cdkMessage = document.getElementById('cdkMessage');
const cdkResult = document.getElementById('cdkResult');
const cdkResultDetails = document.getElementById('cdkResultDetails');

activateCdkBtn.addEventListener('click', async () => {
    const cdkCode = cdkInput.value || cdkInput.textContent.trim();
    if (!cdkCode) {
        showErrorMsg(cdkMessage, '错误：CDK为空，请输入有效的 CDK 代码', true);
        activateCdkBtn.textContent = '激活失败';
        setTimeout(() => { activateCdkBtn.textContent = '激活'; }, 2000);
        return;
    }

    const token = checkToken();
    if (!token) return;

    await withButtonLoading(activateCdkBtn, '激活中...', async () => {
        setElementDisplay(cdkMessage, false);
        setElementDisplay(cdkResult, false);

        try {
            const resp = await fetch('/api/cdk/activate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                body: JSON.stringify({ code: cdkCode })
            });
            const result = await resp.json().catch(() => ({}));

            if (resp.status === 200) {
                setElementDisplay(cdkResult, true);
                const details = [];
                if (result.assetId > 0) details.push(`获得资源 #${result.assetId}`);
                if (result.goldValue > 0) details.push(`+${result.goldValue} 金币`);
                if (result.description) details.push(result.description);
                cdkResultDetails.textContent = details.length > 0 ? details.join(' • ') : '资源已添加至您的库中';
                cdkInput.value = '';
                activateCdkBtn.textContent = '激活成功';
                setTimeout(() => { activateCdkBtn.textContent = '激活'; }, 2000);
                try { await loadProfileFromServer(); await loadActiveAssets(); } catch (e) { /* 忽略 */ }
            } else if (resp.status === 401) {
                localStorage.removeItem('kax_login_token');
                location.href = '/login';
            } else {
                const code = result.code;
                let errorMsg = result.message || ('激活失败：' + resp.status);
                if (code === 1) errorMsg = '错误：CDK为空';
                else if (code === 2) errorMsg = '错误：CDK错误或不存在';
                else if (code === 3) errorMsg = '错误：CDK已被使用';
                
                showErrorMsg(cdkMessage, errorMsg, true);
                activateCdkBtn.textContent = '激活失败';
                setTimeout(() => { activateCdkBtn.textContent = '激活'; }, 2000);
            }
        } catch (err) {
            console.error('CDK激活请求失败：', err);
            showErrorMsg(cdkMessage, '错误：无法连接到服务器', true);
            activateCdkBtn.textContent = '激活失败';
            setTimeout(() => { activateCdkBtn.textContent = '激活'; }, 2000);
        }
    });
});

cdkInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') activateCdkBtn.click();
});
// #endregion

// #region 页面初始化
async function initializePage() {
    const token = localStorage.getItem('kax_login_token');
    if (!token) { location.href = '/login'; return; }

    try {
        const currentResp = await fetch('/api/user/profile', { headers: { 'Authorization': 'Bearer ' + token } });
        if (currentResp.status === 200) {
            const currentData = await currentResp.json();
            currentUserUid = (typeof currentData.id !== 'undefined') ? currentData.id : null;
            if (targetUid && currentUserUid && targetUid !== String(currentUserUid)) {
                isViewingOtherProfile = true;
            }
        }
    } catch (err) {
        console.error('获取当前用户信息失败：', err);
    }

    await loadProfileFromServer();
    await loadActiveAssets();

    if (!avatarImg.src || avatarImg.src.endsWith('/default-avatar.jpg')) {
        avatarImg.style.display = 'none';
        avatarInitials.style.display = 'block';
    }

    try {
        const emailEl = document.getElementById('email');
        if (emailEl && (!emailEl.title || emailEl.title.trim() === '')) {
            emailEl.title = emailEl.textContent.trim();
        }
    } catch (error) {
        console.warn('Failed to set email title:', error);
    }
}

initializePage();
// #endregion

// #region 弹出卡片——更变计划 / 取消订阅
let currentAssetId = null;
let currentAssetName = null;
let selectedPlanId = null;
let availablePlans = [];

function openChangePlanModal(assetId, assetName) {
    currentAssetId = assetId;
    currentAssetName = assetName;
    selectedPlanId = null;
    document.getElementById('planModalAssetName').textContent = assetName;
    loadAvailablePlans();
    document.getElementById('changePlanModal').classList.add('show');
}

function closePlanModal() {
    document.getElementById('changePlanModal').classList.remove('show');
    currentAssetId = null;
    currentAssetName = null;
    selectedPlanId = null;
    document.getElementById('planModalConfirm').style.display = 'none';
    document.getElementById('planModalMessage').style.display = 'none';
}

function openUnsubscribeModal(assetId, assetName) {
    currentAssetId = assetId;
    currentAssetName = assetName;
    document.getElementById('unsubscribeModalAssetName').textContent = assetName;
    document.getElementById('unsubscribeModal').classList.add('show');
}

function closeUnsubscribeModal() {
    document.getElementById('unsubscribeModal').classList.remove('show');
    currentAssetId = null;
    currentAssetName = null;
}

async function loadAvailablePlans() {
    const planList = document.getElementById('planList');
    planList.innerHTML = '<div style="color: var(--profile-muted); text-align: center; padding: 20px;">加载套餐中...</div>';

    try {
        const token = localStorage.getItem('kax_login_token');
        if (!token) { location.href = '/login'; return; }

        const resp = await fetch(`/api/asset/${currentAssetId}/plans`, { headers: { 'Authorization': 'Bearer ' + token } });
        if (resp.status === 200) {
            const result = await resp.json().catch(() => ({}));
            const plans = result.data || [];
            availablePlans = plans;
            if (plans.length === 0) {
                planList.innerHTML = '<div style="color: var(--profile-muted); text-align: center; padding: 20px;">暂无可用套餐</div>';
            } else {
                planList.innerHTML = plans.map(plan => {
                    const hasDiscount = plan.discountRate > 0;
                    const subtitle = hasDiscount
                        ? `原价 ${(plan.originalPrice || plan.price || 0).toFixed(2)} · 折扣 ${Math.round((1 - plan.discountRate) * 10)}折`
                        : '无折扣';
                    return `
                    <div class="plan-item" data-plan-id="${plan.id}" onclick="selectPlan('${plan.id}', this)">
                        <div class="plan-name">
                            <div style="font-weight: 600; color: var(--profile-text);">${plan.durationLabel}</div>
                            ${subtitle ? `<div style="font-size: 0.85rem; color: var(--profile-muted); margin-top: 2px;">${subtitle}</div>` : ''}
                        </div>
                        <div class="plan-price">💰 ${(plan.price || 0).toFixed(2)}</div>
                    </div>
                `}).join('');
            }
        } else if (resp.status === 401) {
            localStorage.removeItem('kax_login_token');
            location.href = '/login';
        } else {
            planList.innerHTML = '<div style="color: var(--profile-danger); text-align: center; padding: 20px;">加载套餐失败</div>';
        }
    } catch (err) {
        console.error('加载套餐失败：', err);
        planList.innerHTML = '<div style="color: var(--profile-danger); text-align: center; padding: 20px;">网络错误</div>';
    }
}

function selectPlan(planId, element) {
    document.querySelectorAll('.plan-item').forEach(el => el.classList.remove('selected'));
    element.classList.add('selected');
    selectedPlanId = planId;
}

document.getElementById('confirmChangePlanBtn').addEventListener('click', () => {
    if (!selectedPlanId) { alert('请先选择要更变的套餐'); return; }
    const plan = availablePlans.find(p => p.id === selectedPlanId);
    const cost = plan ? (plan.price || 0) : 0;
    document.getElementById('planModalConfirmCost').textContent = `💰 ${cost.toFixed(2)}`;
    setElementDisplay(document.getElementById('planModalConfirm'), true);
});

document.getElementById('planModalConfirmNo').addEventListener('click', () => {
    setElementDisplay(document.getElementById('planModalConfirm'), false);
});

document.getElementById('planModalConfirmYes').addEventListener('click', async () => {
    setElementDisplay(document.getElementById('planModalConfirm'), false);
    const token = checkToken();
    if (!token) return;

    const btn = document.getElementById('confirmChangePlanBtn');
    await withButtonLoading(btn, '处理中...', async () => {
        try {
            const resp = await fetch(`/api/asset/${currentAssetId}/changePlan`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                body: JSON.stringify({ planId: selectedPlanId })
            });
            const result = await resp.json().catch(() => ({}));
            const msgEl = document.getElementById('planModalMessage');
            if (resp.status === 200) {
                showErrorMsg(msgEl, `成功更变套餐！需支付 💰 ${(result.cost || 0).toFixed(2)}`, false);
                setTimeout(() => { closePlanModal(); loadActiveAssets(); }, 1500);
            } else if (resp.status === 401) {
                localStorage.removeItem('kax_login_token');
                location.href = '/login';
            } else {
                showErrorMsg(msgEl, result.message || ('更变失败：' + resp.status), true);
            }
        } catch (err) {
            console.error('更变套餐请求失败：', err);
            alert('无法连接到服务器');
        }
    });
});

document.getElementById('confirmUnsubscribeBtn').addEventListener('click', async () => {
    const token = checkToken();
    if (!token) return;

    const btn = document.getElementById('confirmUnsubscribeBtn');
    await withButtonLoading(btn, '取消中...', async () => {
        try {
            const resp = await fetch(`/api/asset/${currentAssetId}/unsubscribe`, {
                method: 'POST',
                headers: { 'Authorization': 'Bearer ' + token }
            });
            const result = await resp.json().catch(() => ({}));
            if (resp.status === 200) {
                alert(result.message || '订阅已取消');
                closeUnsubscribeModal();
                await loadActiveAssets();
            } else if (resp.status === 401) {
                localStorage.removeItem('kax_login_token');
                location.href = '/login';
            } else {
                alert(result.message || ('取消失败：' + resp.status));
            }
        } catch (err) {
            console.error('取消订阅请求失败：', err);
            alert('无法连接到服务器');
        }
    });
});

/* 事件委托：资产操作按钮 */
document.addEventListener('click', (e) => {
    const btn = e.target.closest('.asset-action-btn');
    if (!btn) return;
    const action = btn.dataset.action;
    const container = btn.closest('.asset-actions');
    if (!container) return;
    const assetId = container.dataset.assetId;
    const assetName = container.dataset.assetName;
    if (action === 'changePlan') openChangePlanModal(assetId, assetName);
    else if (action === 'unsubscribe') openUnsubscribeModal(assetId, assetName);
});

/* 点击背景关闭弹窗 */
document.querySelectorAll('.modal-overlay').forEach(overlay => {
    overlay.addEventListener('click', (e) => {
        if (e.target === overlay) {
            overlay.classList.remove('show');
            if (overlay.id === 'changePlanModal') closePlanModal();
        }
    });
});
// #endregion

// #region 管理员权限检查与选项卡控制
let _isAdminUser = false;

/** 权限组编号 <= 2 即为管理员（Console=0, Root=1, Admin=2） */
function isAdminPermission(permissionGroup) {
    return typeof permissionGroup !== 'undefined' && Number(permissionGroup) <= 2;
}

function showAdminTabs() {
    document.querySelectorAll('.admin-only-tab').forEach(el => el.style.display = '');
}

function hideAdminTabs() {
    document.querySelectorAll('.admin-only-tab').forEach(el => el.style.display = 'none');
}
// #endregion

// #region 管理员——资产管理

let adminAssetPage = 1;
let adminAssetTotalPages = 1;
const adminAssetPageSize = 20;

/** 更新分页按钮状态 */
function updatePaginationButtons(page, totalPages, prevBtnId, nextBtnId) {
    const prevBtn = document.getElementById(prevBtnId);
    const nextBtn = document.getElementById(nextBtnId);
    if (prevBtn) prevBtn.disabled = page <= 1;
    if (nextBtn) nextBtn.disabled = page >= totalPages;
}

async function loadAdminAssets(page = 1) {
    const token = checkToken();
    if (!token) return;

    const q = (document.getElementById('adminAssetSearch')?.value || '').trim();
    const includeDeleted = document.getElementById('adminAssetIncludeDeleted')?.checked ? 'true' : 'false';
    const loadingEl = document.getElementById('adminAssetLoading');
    const emptyEl = document.getElementById('adminAssetEmpty');
    const listEl = document.getElementById('adminAssetList');
    const pagerEl = document.getElementById('adminAssetPager');
    const pageInfoEl = document.getElementById('adminAssetPageInfo');

    setElementsDisplay({ 'adminAssetLoading': true, 'adminAssetEmpty': false });
    listEl.innerHTML = '';
    setElementDisplay(pagerEl, false);

    try {
        const params = new URLSearchParams({ page, pageSize: adminAssetPageSize, includeDeleted });
        if (q) params.append('q', q);
        const resp = await fetch('/api/asset/admin/list?' + params, { headers: { 'Authorization': 'Bearer ' + token } });
        if (resp.status === 401) { localStorage.removeItem('kax_login_token'); location.href = '/login'; return; }
        if (!resp.ok) { 
            setElementsDisplay({ 'adminAssetLoading': false, 'adminAssetEmpty': true });
            emptyEl.querySelector('span:last-child').textContent = '加载失败'; 
            return; 
        }

        const result = await resp.json().catch(() => ({}));
        const items = result.data || [];
        const total = result.total || 0;
        adminAssetPage = page;
        adminAssetTotalPages = Math.max(1, Math.ceil(total / adminAssetPageSize));

        setElementDisplay(loadingEl, false);
        if (items.length === 0) {
            setElementDisplay(emptyEl, true);
            emptyEl.querySelector('span:last-child').textContent = '暂无资产';
            return;
        }

        setElementDisplay(emptyEl, false);
        listEl.innerHTML = items.map(a => `
            <div class="admin-list-item ${a.isDeleted ? 'admin-list-item--deleted' : ''}">
                <div class="admin-list-item-info">
                    <div class="admin-list-item-name">${escapeHtml(a.name)} <span class="admin-list-item-badge">${escapeHtml(a.version)}</span>${a.isDeleted ? '<span class="admin-list-item-badge danger">已删除</span>' : ''}</div>
                    <div class="admin-list-item-meta">作者: ${escapeHtml(a.author)} · ID: ${a.id}</div>
                </div>
                <div class="admin-list-item-actions">
                    <button class="asset-action-btn" onclick="openAssetEditModal(${a.id})">
                        <span class="material-icons">edit</span>编辑
                    </button>
                    ${a.isDeleted
                ? `<button class="asset-action-btn" onclick="restoreAdminAsset(${a.id})"><span class="material-icons">restore</span>恢复</button>`
                : `<button class="asset-action-btn danger" onclick="openAssetDeleteModal(${a.id}, '${escapeHtml(a.name)}')"><span class="material-icons">delete</span>删除</button>`
            }
                </div>
            </div>
        `).join('');

        if (adminAssetTotalPages > 1) {
            setElementDisplay(pagerEl, true);
            pageInfoEl.textContent = `第 ${adminAssetPage} / ${adminAssetTotalPages} 页，共 ${total} 条`;
            updatePaginationButtons(adminAssetPage, adminAssetTotalPages, 'adminAssetPrevBtn', 'adminAssetNextBtn');
        }
    } catch (err) {
        console.error('加载资产列表失败:', err);
        setElementsDisplay({ 'adminAssetLoading': false, 'adminAssetEmpty': true });
        emptyEl.querySelector('span:last-child').textContent = '网络错误';
    }
}

document.getElementById('adminAssetSearchBtn')?.addEventListener('click', () => loadAdminAssets(1));
document.getElementById('adminAssetSearch')?.addEventListener('keypress', e => { if (e.key === 'Enter') loadAdminAssets(1); });
document.getElementById('adminAssetIncludeDeleted')?.addEventListener('change', () => loadAdminAssets(1));
document.getElementById('adminAssetPrevBtn')?.addEventListener('click', () => { if (adminAssetPage > 1) loadAdminAssets(adminAssetPage - 1); });
document.getElementById('adminAssetNextBtn')?.addEventListener('click', () => { if (adminAssetPage < adminAssetTotalPages) loadAdminAssets(adminAssetPage + 1); });

// 新建资产按钮
document.getElementById('adminCreateAssetBtn')?.addEventListener('click', () => openAssetEditModal(null));

/* 暂存当前编辑的价格方案列表（用于新建/编辑资产） */
let assetPricePlans = [];

/**
 * 自动计算最终价格 = 原价 × (1 - 折扣率)
 */
function calculateFinalPrice(originalPrice, discountRate) {
    const price = Math.max(0, (originalPrice || 0) * (1 - (discountRate || 0)));
    return Math.round(price * 100) / 100; // 保留两位小数
}

function renderAssetPricePlans() {
    const container = document.getElementById('assetPriceList');
    if (!container) return;
    if (assetPricePlans.length === 0) {
        container.innerHTML = '<div style="color:var(--muted-strong);font-size:0.85rem;padding:8px 0;">暂无价格方案，点击「添加」创建</div>';
        return;
    }
    const unitMap = { once: '一次性', hour: '小时', day: '天', month: '月', year: '年' };
    container.innerHTML = assetPricePlans.map((p, i) => {
        const calculatedPrice = calculateFinalPrice(p.originalPrice, p.discountRate);
        return `
        <div class="admin-price-row" data-idx="${i}">
            <div class="admin-price-cols">
                <div class="admin-price-field">
                    <label class="admin-price-label">最终价格 <span style="color:var(--muted);font-weight:400;">(自动)</span></label>
                    <input class="admin-price-input" type="number" min="0" placeholder="0" value="${calculatedPrice}" readonly data-field="price" data-idx="${i}" title="最终价格根据原价和折扣自动计算" style="cursor:not-allowed;opacity:0.7;">
                </div>
                <div class="admin-price-field">
                    <label class="admin-price-label">原价</label>
                    <input class="admin-price-input" type="number" min="0" placeholder="0" value="${p.originalPrice ?? 0}" data-field="originalPrice" data-idx="${i}">
                </div>
                <div class="admin-price-field">
                    <label class="admin-price-label">折扣率 (0-1)</label>
                    <input class="admin-price-input" type="number" min="0" max="1" step="0.01" placeholder="0" value="${p.discountRate ?? 0}" data-field="discountRate" data-idx="${i}">
                </div>
                <div class="admin-price-field">
                    <label class="admin-price-label">时长</label>
                    <input class="admin-price-input" type="number" min="1" placeholder="1" value="${p.duration ?? 1}" data-field="duration" data-idx="${i}">
                </div>
                <div class="admin-price-field">
                    <label class="admin-price-label">单位</label>
                    <select class="admin-select admin-price-select" data-field="unit" data-idx="${i}" title="时间单位">
                        ${Object.entries(unitMap).map(([k, v]) => `<option value="${k}"${p.unit === k ? ' selected' : ''}>${v}</option>`).join('')}
                    </select>
                </div>
                <div class="admin-price-field">
                    <label class="admin-price-label">库存 (-1=无限)</label>
                    <input class="admin-price-input" type="number" min="-1" placeholder="-1" value="${p.stock ?? -1}" data-field="stock" data-idx="${i}">
                </div>
            </div>
            <button class="asset-action-btn danger admin-price-remove" data-idx="${i}" title="移除此方案" type="button">
                <span class="material-icons">close</span>
            </button>
        </div>
    `;
    }).join('');

    container.querySelectorAll('.admin-price-input, .admin-price-select').forEach(el => {
        el.addEventListener('change', () => {
            const idx = parseInt(el.dataset.idx);
            const field = el.dataset.field;

            // 跳过只读的最终价格字段
            if (field === 'price') return;

            let val = el.value;
            if (['originalPrice', 'duration', 'stock'].includes(field)) val = parseInt(val) || 0;
            if (field === 'discountRate') val = parseFloat(val) || 0;
            assetPricePlans[idx][field] = val;

            // 如果修改了原价或折扣率，重新计算最终价格并保存到数组
            if (['originalPrice', 'discountRate'].includes(field)) {
                const plan = assetPricePlans[idx];
                plan.price = calculateFinalPrice(plan.originalPrice, plan.discountRate);
                renderAssetPricePlans();
            }
        });
    });
    container.querySelectorAll('.admin-price-remove').forEach(btn => {
        btn.addEventListener('click', () => {
            const idx = parseInt(btn.dataset.idx);
            assetPricePlans.splice(idx, 1);
            renderAssetPricePlans();
        });
    });
}

document.getElementById('assetAddPriceBtn')?.addEventListener('click', () => {
    assetPricePlans.push({ price: 0, originalPrice: 0, discountRate: 0, duration: 1, unit: 'month', stock: -1 });
    renderAssetPricePlans();
});

async function openAssetEditModal(assetId) {
    const modal = document.getElementById('assetEditModal');
    const titleEl = document.getElementById('assetEditModalTitle');
    const msgEl = document.getElementById('assetEditMsg');
    const idInput = document.getElementById('assetEditId');

    setElementDisplay(msgEl, false);
    assetPricePlans = [];

    if (!assetId) {
        titleEl.textContent = '新建资产';
        idInput.value = '';
        ['assetEditName', 'assetEditVersion', 'assetEditAuthor', 'assetEditCategory', 'assetEditDesc', 'assetEditDownloadUrl', 'assetEditLicense', 'assetEditCompatibility', 'assetEditFileSize'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.value = '';
        });
        renderAssetPricePlans();
        modal.classList.add('show');
        return;
    }

    titleEl.textContent = '编辑资产';
    idInput.value = String(assetId);

    const token = checkToken();
    if (!token) return;

    try {
        const resp = await fetch('/api/asset/admin/inspect', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
            body: JSON.stringify({ id: assetId })
        });
        if (!resp.ok) { alert('无法加载资产信息'); return; }
        const result = await resp.json().catch(() => ({}));
        const d = result.data || {};

        const setVal = (id, v) => { const el = document.getElementById(id); if (el) el.value = v ?? ''; };
        setVal('assetEditName', d.name);
        setVal('assetEditVersion', d.version);
        setVal('assetEditAuthor', d.author || (d.authorId ? `ID: ${d.authorId}` : ''));
        setVal('assetEditCategory', d.category);
        setVal('assetEditDesc', d.description);
        setVal('assetEditDownloadUrl', d.downloadUrl || d.specs?.downloadUrl);
        setVal('assetEditLicense', d.license || d.specs?.license);
        setVal('assetEditCompatibility', d.compatibility || d.specs?.compatibility);
        setVal('assetEditFileSize', d.fileSize || d.specs?.fileSize || 0);

        assetPricePlans = (d.prices || []).map(p => {
            const originalPrice = p.originalPrice ?? 0;
            const discountRate = p.discountRate ?? 0;
            return {
                id: p.id,
                price: calculateFinalPrice(originalPrice, discountRate),
                originalPrice: originalPrice,
                discountRate: discountRate,
                duration: p.duration ?? 1,
                unit: p.unit ?? 'month',
                stock: p.stock ?? -1
            };
        });
        renderAssetPricePlans();
        modal.classList.add('show');
    } catch (err) {
        console.error('加载资产详情失败:', err);
        alert('加载失败');
    }
}

document.getElementById('assetEditCancelBtn')?.addEventListener('click', () => {
    document.getElementById('assetEditModal').classList.remove('show');
});

document.getElementById('assetEditSaveBtn')?.addEventListener('click', async () => {
    const token = checkToken();
    if (!token) return;

    const id = document.getElementById('assetEditId').value;
    const name = document.getElementById('assetEditName')?.value?.trim() || '';
    const version = document.getElementById('assetEditVersion')?.value?.trim() || '';
    const author = document.getElementById('assetEditAuthor')?.value?.trim() || '';
    const category = document.getElementById('assetEditCategory')?.value?.trim() || '';
    const description = document.getElementById('assetEditDesc')?.value?.trim() || '';
    const downloadUrl = document.getElementById('assetEditDownloadUrl')?.value?.trim() || '';
    const license = document.getElementById('assetEditLicense')?.value?.trim() || '';
    const compatibility = document.getElementById('assetEditCompatibility')?.value?.trim() || '';
    const fileSize = parseInt(document.getElementById('assetEditFileSize')?.value || '0') || 0;
    const msgEl = document.getElementById('assetEditMsg');

    const showMsg = (text, ok) => {
        showErrorMsg(msgEl, text, !ok);
    };

    if (!name) { showMsg('请填写资产名称', false); return; }
    if (!version) { showMsg('请填写版本', false); return; }

    const saveBtn = document.getElementById('assetEditSaveBtn');
    await withButtonLoading(saveBtn, '保存中…', async () => {
        try {
            const isEdit = !!id;
            const url = isEdit ? '/api/asset/admin/update' : '/api/asset/admin/create';
            const payload = { name, version, category, description, downloadUrl, license, compatibility, fileSize, prices: assetPricePlans };
            if (isEdit) payload.id = parseInt(id);

            const resp = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                body: JSON.stringify(payload)
            });
            const result = await resp.json().catch(() => ({}));
            if (resp.ok) {
                showMsg(result.message || (isEdit ? '已更新' : '已创建'), true);
                
                // 在保存按钮旁显示成功提示
                const successLabel = document.createElement('span');
                successLabel.textContent = isEdit ? '✓ 资源已更新' : '✓ 资源已创建';
                successLabel.style.cssText = 'color:var(--profile-success);font-size:0.9rem;margin-left:8px;display:inline-flex;align-items:center;gap:4px;';
                const footer = saveBtn.closest('.modal-footer');
                if (footer) {
                    const existing = footer.querySelector('[data-success-label]');
                    if (existing) existing.remove();
                    successLabel.setAttribute('data-success-label', '1');
                    footer.appendChild(successLabel);
                    setTimeout(() => successLabel.remove(), 2000);
                }
                
                setTimeout(() => { document.getElementById('assetEditModal').classList.remove('show'); loadAdminAssets(adminAssetPage); }, 1000);
            } else {
                showMsg(result.message || ('保存失败: ' + resp.status), false);
            }
        } catch (err) {
            showMsg('网络错误', false);
        }
    });
});

let assetDeleteTargetId = null;

function openAssetDeleteModal(id, name) {
    assetDeleteTargetId = id;
    document.getElementById('assetDeleteName').textContent = name;
    document.getElementById('assetDeleteModal').classList.add('show');
}

document.getElementById('assetDeleteCancelBtn')?.addEventListener('click', () => {
    document.getElementById('assetDeleteModal').classList.remove('show');
    assetDeleteTargetId = null;
});

document.getElementById('assetDeleteConfirmBtn')?.addEventListener('click', async () => {
    if (!assetDeleteTargetId) return;
    const token = checkToken();
    if (!token) return;
    const btn = document.getElementById('assetDeleteConfirmBtn');
    
    await withButtonLoading(btn, '删除中...', async () => {
        try {
            const resp = await fetch('/api/asset/admin/delete', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                body: JSON.stringify({ id: assetDeleteTargetId })
            });
            const result = await resp.json().catch(() => ({}));
            if (resp.ok) {
                document.getElementById('assetDeleteModal').classList.remove('show');
                assetDeleteTargetId = null;
                loadAdminAssets(adminAssetPage);
            } else {
                alert(result.message || '删除失败');
            }
        } catch (err) {
            alert('网络错误');
        }
    });
});

async function restoreAdminAsset(id) {
    if (!confirm('确认恢复此资产？')) return;
    const token = localStorage.getItem('kax_login_token');
    try {
        const resp = await fetch('/api/asset/admin/restore', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
            body: JSON.stringify({ id })
        });
        const result = await resp.json().catch(() => ({}));
        if (resp.ok) { loadAdminAssets(adminAssetPage); }
        else { alert(result.message || '恢复失败'); }
    } catch (err) { alert('网络错误'); }
}
// #endregion

// #region 管理员——CDK 管理

let cdkAdminPage = 1;
let cdkAdminTotalPages = 1;
const cdkAdminPageSize = 50;
let cdkAdminLastKeyword = '';
const cdkSelectedCodes = new Set(); // 批量选择状态

async function loadAdminCdks(page = 1) {
    const token = checkToken();
    if (!token) return;

    const keyword = (document.getElementById('cdkAdminSearch')?.value || '').trim();
    const searchIn = document.getElementById('cdkAdminSearchIn')?.value || 'all';
    const loadingEl = document.getElementById('cdkAdminLoading');
    const emptyEl = document.getElementById('cdkAdminEmpty');
    const listEl = document.getElementById('cdkAdminList');
    const pagerEl = document.getElementById('cdkAdminPager');
    const pageInfoEl = document.getElementById('cdkAdminPageInfo');

    setElementsDisplay({ 'cdkAdminLoading': true, 'cdkAdminEmpty': false });
    listEl.innerHTML = '';
    setElementDisplay(pagerEl, false);
    cdkAdminLastKeyword = keyword;

    try {
        const isSearch = !!keyword;
        const params = new URLSearchParams({ page, pageSize: cdkAdminPageSize });
        if (isSearch) { params.append('keyword', keyword); params.append('searchIn', searchIn); }
        const url = isSearch ? '/api/cdk/admin/search?' + params : '/api/cdk/admin/list?' + params;
        const resp = await fetch(url, { headers: { 'Authorization': 'Bearer ' + token } });
        if (resp.status === 401) { localStorage.removeItem('kax_login_token'); location.href = '/login'; return; }
        if (!resp.ok) { 
            setElementsDisplay({ 'cdkAdminLoading': false, 'cdkAdminEmpty': true });
            emptyEl.querySelector('span:last-child').textContent = '加载失败'; 
            return; 
        }

        const result = await resp.json().catch(() => ({}));
        const items = result.data || [];
        const total = result.total || 0;
        cdkAdminPage = page;
        cdkAdminTotalPages = Math.max(1, Math.ceil(total / cdkAdminPageSize));

        setElementDisplay(loadingEl, false);
        if (items.length === 0) {
            setElementDisplay(emptyEl, true);
            emptyEl.querySelector('span:last-child').textContent = '暂无 CDK';
            setElementDisplay(document.getElementById('cdkBatchBar'), false);
            return;
        }

        setElementDisplay(emptyEl, false);
        // 显示批量操作工具栏
        setElementDisplay(document.getElementById('cdkBatchBar'), true);
        // 重置全选状态
        const selectAllCb = document.getElementById('cdkSelectAll');
        if (selectAllCb) selectAllCb.checked = false;

        listEl.innerHTML = items.map(c => {
            const isChecked = cdkSelectedCodes.has(c.code);
            return `
            <div class="admin-list-item${isChecked ? ' cdk-selected' : ''}" data-code="${escapeHtml(c.code)}">
                <label class="cdk-select-all-label" style="flex-shrink:0;margin-right:4px;" title="选择此 CDK">
                    <input type="checkbox" class="cdk-checkbox cdk-item-cb" data-code="${escapeHtml(c.code)}"${isChecked ? ' checked' : ''}>
                </label>
                <div class="admin-list-item-info">
                    <div class="admin-list-item-name" style="font-family:monospace;">${escapeHtml(c.code)}
                        ${c.isUsed ? '<span class="admin-list-item-badge">已使用</span>' : '<span class="admin-list-item-badge success">未使用</span>'}
                        ${c.goldValue > 0 ? `<span class="admin-list-item-badge gold">💰 ${c.goldValue}</span>` : ''}
                    </div>
                    <div class="admin-list-item-meta">
                        有效期: ${formatCdkExpire(c.expiresInSeconds)}
                        ${c.description ? ` · ${escapeHtml(c.description)}` : ''}
                        · 创建于 ${formatUnix(c.createdAt)}
                        ${c.createdBy ? ` by ${escapeHtml(c.createdBy)}` : ''}
                        ${c.isUsed ? ` · 使用者: ${escapeHtml(c.usedBy || '—')} @ ${formatUnix(c.usedAt)}` : ''}
                    </div>
                </div>
                <div class="admin-list-item-actions">
                    <button class="asset-action-btn" onclick="copyCdkCode('${escapeHtml(c.code)}')" title="复制代码">
                        <span class="material-icons">content_copy</span>复制
                    </button>
                    <button class="asset-action-btn danger" onclick="openCdkDeleteModal('${escapeHtml(c.code)}')">
                        <span class="material-icons">delete</span>删除
                    </button>
                </div>
            </div>`;
        }).join('');

        // 绑定复选框交互
        listEl.querySelectorAll('.cdk-item-cb').forEach(cb => {
            cb.addEventListener('change', () => {
                const code = cb.dataset.code;
                const row = cb.closest('.admin-list-item');
                if (cb.checked) {
                    cdkSelectedCodes.add(code);
                    row?.classList.add('cdk-selected');
                } else {
                    cdkSelectedCodes.delete(code);
                    row?.classList.remove('cdk-selected');
                }
                updateCdkBatchBar();
            });
        });

        if (cdkAdminTotalPages > 1) {
            setElementDisplay(pagerEl, true);
            pageInfoEl.textContent = `第 ${cdkAdminPage} / ${cdkAdminTotalPages} 页，共 ${total} 条`;
            updatePaginationButtons(cdkAdminPage, cdkAdminTotalPages, 'cdkAdminPrevBtn', 'cdkAdminNextBtn');
        }
        updateCdkBatchBar();
    } catch (err) {
        console.error('加载 CDK 列表失败:', err);
        setElementsDisplay({ 'cdkAdminLoading': false, 'cdkAdminEmpty': true });
        emptyEl.querySelector('span:last-child').textContent = '网络错误';
    }
}

/** 更新批量操作工具栏状态 */
function updateCdkBatchBar() {
    const count = cdkSelectedCodes.size;
    const countEl = document.getElementById('cdkBatchCount');
    if (countEl) countEl.textContent = count > 0 ? `已选 ${count} 项` : '已选 0 项';
    const batchDeleteBtn = document.getElementById('cdkBatchDeleteBtn');
    const batchCopyBtn = document.getElementById('cdkBatchCopyBtn');
    if (batchDeleteBtn) batchDeleteBtn.disabled = count === 0;
    if (batchCopyBtn) batchCopyBtn.disabled = count === 0;
}

/** 复制单个 CDK 代码到剪贴板 */
async function copyCdkCode(code) {
    try {
        await navigator.clipboard.writeText(code);
        // 简短视觉反馈：找到对应按钮，短暂改变文字
        const btns = document.querySelectorAll(`[data-code="${CSS.escape(code)}"]`);
        const row = document.querySelector(`.admin-list-item[data-code="${CSS.escape(code)}"]`);
        const copyBtn = row?.querySelector('.asset-action-btn:not(.danger)');
        if (copyBtn) {
            const orig = copyBtn.innerHTML;
            copyBtn.innerHTML = '<span class="material-icons">check</span>已复制';
            setTimeout(() => { copyBtn.innerHTML = orig; }, 1500);
        }
    } catch (e) {
        // 降级：使用 execCommand
        const ta = document.createElement('textarea');
        ta.value = code;
        ta.style.position = 'fixed';
        ta.style.opacity = '0';
        document.body.appendChild(ta);
        ta.select();
        document.execCommand('copy');
        document.body.removeChild(ta);
    }
}

/** 格式化 CDK 过期时间 */
function formatCdkExpire(s) {
    if (!s || s === 0) return '永久';
    if (s < 3600) return `${s}秒`;
    if (s < 86400) return `${(s / 3600).toFixed(1)}小时`;
    return `${Math.floor(s / 86400)}天`;
}

// ── 全选 / 取消全选 ──
document.getElementById('cdkSelectAll')?.addEventListener('change', function () {
    const checked = this.checked;
    document.querySelectorAll('#cdkAdminList .cdk-item-cb').forEach(cb => {
        cb.checked = checked;
        const code = cb.dataset.code;
        const row = cb.closest('.admin-list-item');
        if (checked) {
            cdkSelectedCodes.add(code);
            row?.classList.add('cdk-selected');
        } else {
            cdkSelectedCodes.delete(code);
            row?.classList.remove('cdk-selected');
        }
    });
    updateCdkBatchBar();
});

// ── 批量复制 ──
document.getElementById('cdkBatchCopyBtn')?.addEventListener('click', async () => {
    if (cdkSelectedCodes.size === 0) return;
    const text = [...cdkSelectedCodes].join('\n');
    try {
        await navigator.clipboard.writeText(text);
        const btn = document.getElementById('cdkBatchCopyBtn');
        if (btn) {
            const orig = btn.innerHTML;
            btn.innerHTML = '<span class="material-icons">check</span>已复制';
            setTimeout(() => { btn.innerHTML = orig; }, 1500);
        }
    } catch (e) {
        const ta = document.createElement('textarea');
        ta.value = text;
        ta.style.position = 'fixed'; ta.style.opacity = '0';
        document.body.appendChild(ta); ta.select(); document.execCommand('copy');
        document.body.removeChild(ta);
    }
});

// ── 批量删除弹窗 ──
document.getElementById('cdkBatchDeleteBtn')?.addEventListener('click', () => {
    if (cdkSelectedCodes.size === 0) return;
    document.getElementById('cdkBatchDeleteCount').textContent = cdkSelectedCodes.size;
    document.getElementById('cdkBatchDeleteModal').classList.add('show');
});

document.getElementById('cdkBatchDeleteCancelBtn')?.addEventListener('click', () => {
    document.getElementById('cdkBatchDeleteModal').classList.remove('show');
});

document.getElementById('cdkBatchDeleteConfirmBtn')?.addEventListener('click', async () => {
    if (cdkSelectedCodes.size === 0) return;
    const token = checkToken();
    if (!token) return;
    const btn = document.getElementById('cdkBatchDeleteConfirmBtn');
    await withButtonLoading(btn, '删除中…', async () => {
        try {
            const resp = await fetch('/api/cdk/admin/delete', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                body: JSON.stringify({ codes: [...cdkSelectedCodes] })
            });
            const result = await resp.json().catch(() => ({}));
            if (resp.ok) {
                document.getElementById('cdkBatchDeleteModal').classList.remove('show');
                cdkSelectedCodes.clear();
                loadAdminCdks(cdkAdminPage);
            } else {
                alert(result.message || '批量删除失败');
            }
        } catch (err) {
            alert('网络错误');
        }
    });
});

// ── 规则删除弹窗 ──
document.getElementById('cdkRuleDeleteBtn')?.addEventListener('click', () => {
    document.getElementById('cdkRulePreviewMsg').style.display = 'none';
    document.getElementById('cdkRuleDeleteModal').classList.add('show');
});

document.getElementById('cdkRuleDeleteCancelBtn')?.addEventListener('click', () => {
    document.getElementById('cdkRuleDeleteModal').classList.remove('show');
});

/** 获取规则删除对应的 CDK codes（返回 string[] 或 null 表示失败） */
async function fetchCdksByRule(scope, token) {
    const keyword = scope === 'search' ? (document.getElementById('cdkAdminSearch')?.value || '').trim() : '';
    const searchIn = scope === 'search' ? (document.getElementById('cdkAdminSearchIn')?.value || 'all') : 'all';

    let allCodes = [];
    let page = 1;
    const pageSize = 200;
    while (true) {
        const params = new URLSearchParams({ page, pageSize });
        let url;
        if (scope === 'search' && keyword) {
            params.append('keyword', keyword); params.append('searchIn', searchIn);
            url = '/api/cdk/admin/search?' + params;
        } else {
            url = '/api/cdk/admin/list?' + params;
        }
        const resp = await fetch(url, { headers: { 'Authorization': 'Bearer ' + token } });
        if (!resp.ok) return null;
        const result = await resp.json().catch(() => ({}));
        const items = result.data || [];
        if (items.length === 0) break;

        for (const c of items) {
            if (scope === 'used' && !c.isUsed) continue;
            if (scope === 'unused' && c.isUsed) continue;
            allCodes.push(c.code);
        }

        const total = result.total || 0;
        if (allCodes.length >= total || items.length < pageSize) break;
        page++;
    }
    return allCodes;
}

// 规则删除：预览数量
document.getElementById('cdkRulePreviewCountBtn')?.addEventListener('click', async () => {
    const token = checkToken();
    if (!token) return;
    const scope = document.getElementById('cdkRuleScope')?.value || 'used';
    const msgEl = document.getElementById('cdkRulePreviewMsg');
    msgEl.style.display = '';
    msgEl.className = 'admin-msg';
    msgEl.textContent = '查询中…';
    try {
        const codes = await fetchCdksByRule(scope, token);
        if (codes === null) {
            msgEl.className = 'admin-msg admin-msg--err';
            msgEl.textContent = '查询失败';
        } else {
            msgEl.className = 'admin-msg admin-msg--ok';
            msgEl.textContent = `符合条件的 CDK 共 ${codes.length} 个`;
        }
    } catch (e) {
        msgEl.className = 'admin-msg admin-msg--err';
        msgEl.textContent = '网络错误';
    }
});

// 规则删除：确认执行
document.getElementById('cdkRuleDeleteConfirmBtn')?.addEventListener('click', async () => {
    const token = checkToken();
    if (!token) return;
    const scope = document.getElementById('cdkRuleScope')?.value || 'used';
    const scopeLabels = { used: '所有已使用', unused: '所有未使用', search: '当前搜索结果', all: '全部' };
    if (!confirm(`确认删除"${scopeLabels[scope] || scope}"的 CDK？此操作不可撤销。`)) return;

    const btn = document.getElementById('cdkRuleDeleteConfirmBtn');
    const msgEl = document.getElementById('cdkRulePreviewMsg');
    msgEl.style.display = '';
    msgEl.className = 'admin-msg';
    msgEl.textContent = '正在查询符合条件的 CDK…';

    await withButtonLoading(btn, '删除中…', async () => {
        try {
            const codes = await fetchCdksByRule(scope, token);
            if (codes === null) {
                msgEl.className = 'admin-msg admin-msg--err';
                msgEl.textContent = '查询失败，请重试';
                return;
            }
            if (codes.length === 0) {
                msgEl.className = 'admin-msg admin-msg--ok';
                msgEl.textContent = '没有符合条件的 CDK，无需删除';
                return;
            }
            msgEl.textContent = `正在删除 ${codes.length} 个 CDK…`;

            // 分批删除（每批 50 个防止超时）
            const batchSize = 50;
            let removed = 0;
            for (let i = 0; i < codes.length; i += batchSize) {
                const batch = codes.slice(i, i + batchSize);
                const resp = await fetch('/api/cdk/admin/delete', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                    body: JSON.stringify({ codes: batch })
                });
                const result = await resp.json().catch(() => ({}));
                if (resp.ok) removed += (result.removed || 0);
            }
            msgEl.className = 'admin-msg admin-msg--ok';
            msgEl.textContent = `删除完成，共删除 ${removed} 个 CDK`;
            setTimeout(() => {
                document.getElementById('cdkRuleDeleteModal').classList.remove('show');
                cdkSelectedCodes.clear();
                loadAdminCdks(1);
            }, 1200);
        } catch (err) {
            msgEl.className = 'admin-msg admin-msg--err';
            msgEl.textContent = '网络错误：' + err.message;
        }
    });
});
document.getElementById('cdkAdminSearchBtn')?.addEventListener('click', () => loadAdminCdks(1));
document.getElementById('cdkAdminSearch')?.addEventListener('keypress', e => { if (e.key === 'Enter') loadAdminCdks(1); });
document.getElementById('cdkAdminPrevBtn')?.addEventListener('click', () => { if (cdkAdminPage > 1) loadAdminCdks(cdkAdminPage - 1); });
document.getElementById('cdkAdminNextBtn')?.addEventListener('click', () => { if (cdkAdminPage < cdkAdminTotalPages) loadAdminCdks(cdkAdminPage + 1); });

// CDK 预览
document.getElementById('cdkAdminPreviewBtn')?.addEventListener('click', async () => {
    const token = checkToken();
    if (!token) return;
    const count = Math.min(1000, Math.max(1, parseInt(document.getElementById('cdkAdminCount')?.value || '1') || 1));
    const length = Math.min(256, Math.max(4, parseInt(document.getElementById('cdkAdminLength')?.value || '16') || 16));
    const prefix = document.getElementById('cdkAdminPrefix')?.value?.trim() || '';
    const previewEl = document.getElementById('cdkAdminPreview');
    const msgEl = document.getElementById('cdkAdminGenMsg');

    try {
        const resp = await fetch('/api/cdk/admin/generate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
            body: JSON.stringify({ count: Math.min(count, 20), length, prefix })
        });
        const result = await resp.json().catch(() => ({}));
        if (resp.ok && result.codes) {
            const preview = result.codes.slice(0, 20).join('\n') + (count > 20 ? `\n…（共 ${count} 个）` : '');
            setElementDisplay(previewEl, true);
            previewEl.textContent = preview;
            setElementDisplay(msgEl, false);
        } else {
            setElementDisplay(msgEl, true);
            msgEl.className = 'admin-msg admin-msg--err';
            msgEl.textContent = result.message || '预览失败';
        }
    } catch (err) { alert('网络错误'); }
});

// CDK 生成并保存
document.getElementById('cdkAdminSaveBtn')?.addEventListener('click', async () => {
    const token = checkToken();
    if (!token) return;
    const count = Math.min(1000, Math.max(1, parseInt(document.getElementById('cdkAdminCount')?.value || '1') || 1));
    const length = Math.min(256, Math.max(4, parseInt(document.getElementById('cdkAdminLength')?.value || '16') || 16));
    const goldValue = Math.max(0, parseInt(document.getElementById('cdkAdminGold')?.value || '0') || 0);
    const expiresInSeconds = Math.max(0, parseInt(document.getElementById('cdkAdminExpires')?.value || '0') || 0);
    const prefix = document.getElementById('cdkAdminPrefix')?.value?.trim() || '';
    const description = document.getElementById('cdkAdminDesc')?.value?.trim() || '';
    const msgEl = document.getElementById('cdkAdminGenMsg');
    const saveBtn = document.getElementById('cdkAdminSaveBtn');

    await withButtonLoading(saveBtn, '生成中…', async () => {
        setElementDisplay(msgEl, false);
        try {
            const resp = await fetch('/api/cdk/admin/save', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                body: JSON.stringify({ count, length, prefix, goldValue, expiresInSeconds, description })
            });
            const result = await resp.json().catch(() => ({}));
            setElementDisplay(msgEl, true);
            if (resp.ok) {
                msgEl.className = 'admin-msg admin-msg--ok';
                msgEl.textContent = result.message || `已成功生成 ${result.count || count} 个 CDK`;
                setElementDisplay(document.getElementById('cdkAdminPreview'), false);
                loadAdminCdks(1);
            } else {
                msgEl.className = 'admin-msg admin-msg--err';
                msgEl.textContent = result.message || ('生成失败: ' + resp.status);
            }
        } catch (err) {
            setElementDisplay(msgEl, true);
            msgEl.className = 'admin-msg admin-msg--err';
            msgEl.textContent = '网络错误';
        }
    });
});

let cdkDeleteTargetCode = null;

function openCdkDeleteModal(code) {
    cdkDeleteTargetCode = code;
    document.getElementById('cdkDeleteCode').textContent = code;
    document.getElementById('cdkDeleteModal').classList.add('show');
}

document.getElementById('cdkDeleteCancelBtn')?.addEventListener('click', () => {
    document.getElementById('cdkDeleteModal').classList.remove('show');
    cdkDeleteTargetCode = null;
});

document.getElementById('cdkDeleteConfirmBtn')?.addEventListener('click', async () => {
    if (!cdkDeleteTargetCode) return;
    const token = checkToken();
    if (!token) return;
    const btn = document.getElementById('cdkDeleteConfirmBtn');
    
    await withButtonLoading(btn, '删除中...', async () => {
        try {
            const resp = await fetch('/api/cdk/admin/delete', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                body: JSON.stringify({ code: cdkDeleteTargetCode })
            });
            const result = await resp.json().catch(() => ({}));
            if (resp.ok) {
                document.getElementById('cdkDeleteModal').classList.remove('show');
                cdkDeleteTargetCode = null;
                loadAdminCdks(cdkAdminPage);
            } else {
                alert(result.message || '删除失败');
            }
        } catch (err) {
            alert('网络错误');
        }
    });
});
// #endregion

// #region 订单 Tab 处理逻辑
let ordersPage = 1;
const ordersPageSize = 10;

(function initOrdersTab() {
    const tabOrders = document.querySelector('[data-tab="orders"]');
    if (tabOrders) {
        tabOrders.addEventListener('click', () => {
            // 首次切换到订单标签页时加载数据
            if (!window.ordersTabLoaded) {
                window.ordersTabLoaded = true;
                loadUserOrders(1);
            }
        });
    }

    // 搜索按钮
    document.getElementById('orderSearchBtn')?.addEventListener('click', searchOrders);
    document.getElementById('orderSearch')?.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') searchOrders();
    });

    // 状态筛选
    document.getElementById('orderStatusFilter')?.addEventListener('change', () => {
        ordersPage = 1;
        loadUserOrders(1);
    });

    // 分页按钮
    document.getElementById('ordersPrevBtn')?.addEventListener('click', () => {
        if (ordersPage > 1) loadUserOrders(ordersPage - 1);
    });
    document.getElementById('ordersNextBtn')?.addEventListener('click', () => {
        loadUserOrders(ordersPage + 1);
    });
})();

/**
 * 加载用户订单列表
 */
async function loadUserOrders(page = 1) {
    const token = checkToken();
    if (!token) return;

    const ordersLoading = document.getElementById('ordersLoading');
    const ordersEmpty = document.getElementById('ordersEmpty');
    const ordersList = document.getElementById('ordersList');

    setElementDisplay(ordersLoading, true);
    setElementDisplay(ordersEmpty, false);
    ordersList.innerHTML = '';

    try {
        const resp = await fetch(`/api/user/orders?page=${page}&pageSize=${ordersPageSize}`, {
            headers: { 'Authorization': 'Bearer ' + token }
        });

        if (!resp.ok) {
            if (resp.status === 401) {
                localStorage.removeItem('kax_login_token');
                location.href = '/login';
            }
            throw new Error(`HTTP ${resp.status}`);
        }

        const result = await resp.json();
        setElementDisplay(ordersLoading, false);

        if (result.code !== 0 || !result.data) {
            setElementDisplay(ordersEmpty, true);
            return;
        }

        const orders = result.data;
        const total = result.total;
        const totalPages = Math.ceil(total / ordersPageSize);

        if (orders.length === 0) {
            setElementDisplay(ordersEmpty, true);
            return;
        }

        // 应用状态筛选
        const statusFilter = document.getElementById('orderStatusFilter')?.value || 'all';
        let filteredOrders = orders;
        if (statusFilter !== 'all') {
            filteredOrders = orders.filter(order => {
                // 基于订单类型和金币变化判断状态
                if (statusFilter === 'pending') {
                    return order.orderType === 'purchase'; // 假设金币购买为待支付
                } else if (statusFilter === 'paid') {
                    return order.orderType === 'cdk'; // CDK 兑换视为已支付
                }
                return true;
            });
        }

        if (filteredOrders.length === 0) {
            setElementDisplay(ordersEmpty, true);
            return;
        }

        // 渲染订单列表
        filteredOrders.forEach(order => {
            const orderCard = createOrderCard(order);
            ordersList.appendChild(orderCard);
        });

        // 更新计数
        document.getElementById('ordersCount').textContent = `共 ${total} 条订单`;

        // 更新分页
        ordersPage = page;
        const pagerEl = document.getElementById('ordersPager');
        if (totalPages > 1) {
            setElementDisplay(pagerEl, true);
            document.getElementById('ordersPageInfo').textContent = `第 ${page} / ${totalPages} 页`;
            document.getElementById('ordersPrevBtn').disabled = page === 1;
            document.getElementById('ordersNextBtn').disabled = page === totalPages;
        } else {
            setElementDisplay(pagerEl, false);
        }

    } catch (err) {
        console.error('加载订单失败:', err);
        setElementDisplay(ordersLoading, false);
        setElementDisplay(ordersEmpty, true);
        document.getElementById('ordersEmpty').querySelector('span:last-child').textContent = '加载失败，请重试';
    }
}

/**
 * 搜索订单
 */
function searchOrders() {
    const keyword = document.getElementById('orderSearch')?.value?.trim() || '';
    if (!keyword) {
        loadUserOrders(1);
        return;
    }

    const token = checkToken();
    if (!token) return;

    const ordersLoading = document.getElementById('ordersLoading');
    const ordersEmpty = document.getElementById('ordersEmpty');
    const ordersList = document.getElementById('ordersList');

    setElementDisplay(ordersLoading, true);
    setElementDisplay(ordersEmpty, false);
    ordersList.innerHTML = '';

    (async () => {
        try {
            const resp = await fetch(`/api/user/orders?page=1&pageSize=999`, {
                headers: { 'Authorization': 'Bearer ' + token }
            });

            if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
            const result = await resp.json();
            setElementDisplay(ordersLoading, false);

            if (result.code !== 0 || !result.data) {
                setElementDisplay(ordersEmpty, true);
                return;
            }

            // 客户端搜索
            const filtered = result.data.filter(order =>
                order.assetName?.toLowerCase().includes(keyword.toLowerCase()) ||
                order.cdkCode?.toLowerCase().includes(keyword.toLowerCase()) ||
                order.description?.toLowerCase().includes(keyword.toLowerCase()) ||
                order.id?.toLowerCase().includes(keyword.toLowerCase())
            );

            if (filtered.length === 0) {
                setElementDisplay(ordersEmpty, true);
                return;
            }

            filtered.slice(0, ordersPageSize).forEach(order => {
                const orderCard = createOrderCard(order);
                ordersList.appendChild(orderCard);
            });

            document.getElementById('ordersCount').textContent = `搜索结果: ${filtered.length} 条`;
            setElementDisplay(document.getElementById('ordersPager'), false);

        } catch (err) {
            console.error('搜索失败:', err);
            setElementDisplay(ordersLoading, false);
            setElementDisplay(ordersEmpty, true);
        }
    })();
}

/**
 * 创建订单卡片元素
 */
function createOrderCard(order) {
    // ── 订单类型元数据 ──────────────────────────────────────────────
    const ORDER_TYPES = {
        'purchase':            { label: '购买资产', color: '#3b82f6', bg: 'rgba(59,130,246,0.15)'  },
        'cdk':                 { label: 'CDK兑换',  color: '#10b981', bg: 'rgba(16,185,129,0.15)'  },
        'cancel_subscription': { label: '取消订阅', color: '#f59e0b', bg: 'rgba(245,158,11,0.15)'  },
        'change_plan':         { label: '更变计划', color: '#8b5cf6', bg: 'rgba(139,92,246,0.15)'  },
        'gold_adjust':         { label: '金币调整', color: '#ec4899', bg: 'rgba(236,72,153,0.15)'  },
    };
    const typeInfo = ORDER_TYPES[order.orderType] ?? { label: order.orderType ?? '未知', color: '#6b7280', bg: 'rgba(107,114,128,0.15)' };

    // ── 金币变化 ────────────────────────────────────────────────────
    let goldClass = 'neutral', goldText = '—';
    if (order.goldChange > 0)      { goldClass = 'positive'; goldText = '+' + order.goldChange + ' 💰'; }
    else if (order.goldChange < 0) { goldClass = 'negative'; goldText =       order.goldChange + ' 💰'; }

    // ── 时间格式化 ───────────────────────────────────────────────────
    const d = new Date(order.createdAt);
    const dateStr = `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
    const timeStr = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

    // ── 资产名称 ────────────────────────────────────────────────────
    const assetName = order.assetName || (order.orderType === 'cdk' ? 'CDK 兑换' : order.orderType === 'gold_adjust' ? '金币调整' : '资产操作');

    // ── DOM 构建 ────────────────────────────────────────────────────
    const card = document.createElement('div');
    card.className = 'order-card';
    card.innerHTML = `
        <div class="order-card-stripe" style="background:${typeInfo.color};"></div>
        <div class="order-card-inner">
            <span class="order-type-badge"
                  style="color:${typeInfo.color};background:${typeInfo.bg};">${typeInfo.label}</span>
            <div class="order-card-body">
                <div class="order-asset-name">${escapeHtml(assetName)}</div>
                <div class="order-meta-row">
                    <span class="order-id-chip">${(order.id ?? '').substring(0, 10)}…</span>
                    <span class="order-date">${dateStr} ${timeStr}</span>
                </div>
            </div>
            <div class="order-card-right">
                <span class="order-gold-change ${goldClass}">${goldText}</span>
                <span class="order-chevron material-icons">chevron_right</span>
            </div>
        </div>`;

    card.addEventListener('click', () => showOrderDetail(order));
    return card;
}

/**
 * 显示订单详情弹窗
 */
function showOrderDetail(order) {
    const ORDER_TYPES = {
        'purchase':            { label: '购买资产', color: '#3b82f6' },
        'cdk':                 { label: 'CDK兑换',  color: '#10b981' },
        'cancel_subscription': { label: '取消订阅', color: '#f59e0b' },
        'change_plan':         { label: '更变计划', color: '#8b5cf6' },
        'gold_adjust':         { label: '金币调整', color: '#ec4899' },
    };
    const typeInfo = ORDER_TYPES[order.orderType] ?? { label: order.orderType ?? '未知', color: '#6b7280' };

    let goldClass = '', goldText = '—';
    if (order.goldChange > 0)      { goldClass = 'gold-pos'; goldText = '+' + order.goldChange + ' 金币'; }
    else if (order.goldChange < 0) { goldClass = 'gold-neg'; goldText =       order.goldChange + ' 金币'; }
    else                            { goldClass = 'muted';   goldText = '无变化'; }

    const REASON_MAP = {
        'purchase': '购买资产', 'cdk_redeem': 'CDK 兑换', 'plan_change': '更变套餐',
        'plan_extension': '续期套餐', 'admin': '管理员操作', 'refund': '退款',
        'bonus': '奖励发放',
    };
    const reasonLabel = REASON_MAP[order.goldChangeReason] ?? order.goldChangeReason ?? '—';

    const renderField = (label, value, extra = '') =>
        `<div class="order-detail-field${extra}">
            <div class="order-detail-label">${label}</div>
            <div class="order-detail-value ${value.cls ?? ''}">${value.html ?? escapeHtml(value.text ?? value)}</div>
        </div>`;

    const modal = document.createElement('div');
    modal.className = 'modal-overlay show order-detail-modal';
    modal.id = 'orderDetailModal';
    modal.innerHTML = `
        <div class="modal-card">
            <div class="order-detail-header-strip">
                <div class="order-detail-type-dot" style="background:${typeInfo.color};"></div>
                <div class="order-detail-title">${typeInfo.label}</div>
                <span class="order-detail-gold-badge ${goldClass}">${goldText}</span>
            </div>
            <div class="order-detail-grid">
                ${renderField('订单 ID',    { text: order.id ?? '-',       cls: 'mono' }, ' span-full')}
                ${renderField('资产名称',   { text: order.assetName ?? '-'               })}
                ${renderField('创建时间',   { text: new Date(order.createdAt).toLocaleString() })}
                ${renderField('金币加减方式', { text: reasonLabel                         })}
                ${renderField('计划变更',   { text: order.planTransition || '—'          })}
                ${order.cdkCode ? renderField('CDK 代码', { text: order.cdkCode, cls: 'mono' }) : ''}
                ${renderField('备注',       { text: order.description || '—'            }, ' span-full')}
            </div>
            <div class="modal-footer" style="margin-top:20px;">
                <button class="btn" id="orderDetailClose">关闭</button>
            </div>
        </div>`;

    document.body.appendChild(modal);

    const close = () => modal.remove();
    modal.querySelector('#orderDetailClose').addEventListener('click', close);
    modal.addEventListener('click', e => { if (e.target === modal) close(); });
}

/**
 * HTML 转义函数
 */
function escapeHtml(text) {
    if (!text) return '';
    const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
    return text.replace(/[&<>"']/g, m => map[m]);
}

// #endregion

// #region 工具函数——HTML 转义已移到工具函数区域
// #endregion
