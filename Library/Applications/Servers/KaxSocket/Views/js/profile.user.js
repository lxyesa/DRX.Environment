/* 用户资料域模块（Task 2）
 * 目标：零行为改动地承接 profile.js 中用户资料相关流程。
 * 暴露：window.ProfileUser
 */
(function registerProfileUser(global) {
    const profileShared = global.ProfileShared || {};

    const state = {
        originalProfile: { name: '', handle: '', email: '', role: '', bio: '', signature: '', avatarSrc: '' },
        currentUserUid: null,
        isViewingOtherProfile: false,
        targetUid: null,
        initialized: false,
        deps: {
            formatUnix: (ts) => ts,
            mapPermissionToRole: () => '普通用户',
            maskEmail: (v) => v,
            isAdminPermission: () => false,
            showAdminTabs: () => { },
            hideAdminTabs: () => { },
            setAdminUser: () => { },
            setElementDisplay: (_el, _show) => { },
            setElementsDisplay: (_displayMap) => { },
            showErrorMsg: (_el, _text, _isDanger) => { },
            escapeHtml: (v) => String(v ?? '')
        },
        currentAssetId: null,
        currentAssetName: null,
        selectedPlanId: null,
        availablePlans: []
    };

    const avatarCropState = {
        fileName: 'avatar.png',
        fileType: 'image/png',
        imgNaturalW: 0,
        imgNaturalH: 0,
        scale: 1,
        minScale: 1,
        maxScale: 1,
        offsetX: 0,
        offsetY: 0,
        cropRadius: 0,
        dragging: false,
        dragStartX: 0,
        dragStartY: 0,
        dragOriginX: 0,
        dragOriginY: 0
    };

    // #region DOM 映射
    const saveBtn = document.getElementById('saveBtn');
    const cancelBtn = document.getElementById('cancelBtn');
    const avatarFile = document.getElementById('avatarFile');
    const avatarImg = document.getElementById('avatarImg');
    const avatarInitials = document.getElementById('avatarInitials');
    const avatarContainer = document.getElementById('avatarContainer');
    const emailEditInput = document.getElementById('emailEditInput');
    const oldEmailCodeInput = document.getElementById('oldEmailCodeInput');
    const updateEmailBtn = document.getElementById('updateEmailBtn');
    const sendOldEmailCodeBtn = document.getElementById('sendOldEmailCodeBtn');
    const emailVerifyMessage = document.getElementById('emailVerifyMessage');
    const avatarCropModal = document.getElementById('avatarCropModal');
    const cropViewport = document.getElementById('cropViewport');
    const cropSourceImg = document.getElementById('cropSourceImg');
    const cropOverlay = document.getElementById('cropOverlay');
    const cropZoomRange = document.getElementById('cropZoomRange');
    const cropCancelBtn = document.getElementById('cropCancelBtn');
    const cropConfirmBtn = document.getElementById('cropConfirmBtn');
    // #endregion

    function checkToken() {
        if (typeof profileShared.checkToken === 'function') return profileShared.checkToken();
        const token = localStorage.getItem('kax_login_token');
        if (!token) { location.href = '/login'; return null; }
        return token;
    }

    function setElementDisplay(el, show) {
        if (typeof state.deps.setElementDisplay === 'function') {
            state.deps.setElementDisplay(el, show);
            return;
        }
        if (el) el.style.display = show ? 'block' : 'none';
    }

    function setElementsDisplay(displayMap) {
        if (typeof state.deps.setElementsDisplay === 'function') {
            state.deps.setElementsDisplay(displayMap);
            return;
        }
        Object.entries(displayMap).forEach(([id, show]) => {
            const el = document.getElementById(id);
            if (el) el.style.display = show ? 'block' : 'none';
        });
    }

    function showErrorMsg(el, text, isDanger = true) {
        if (typeof state.deps.showErrorMsg === 'function') {
            state.deps.showErrorMsg(el, text, isDanger);
            return;
        }
        if (!el) return;
        el.style.display = 'block';
        el.style.background = isDanger ? 'rgba(239,68,68,0.1)' : 'rgba(34,197,94,0.1)';
        el.style.borderColor = isDanger ? 'rgba(239,68,68,0.3)' : 'rgba(34,197,94,0.3)';
        el.style.color = isDanger ? 'var(--profile-danger)' : 'var(--profile-success)';
        el.textContent = text;
    }

    function escapeHtml(str) {
        if (typeof state.deps.escapeHtml === 'function') {
            return state.deps.escapeHtml(str);
        }
        if (!str) return '';
        const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
        return String(str).replace(/[&<>"']/g, m => map[m]);
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

    function showErrorPage(message = '资料不存在或已被删除。请检查 UID 是否正确。') {
        const errorContainer = document.getElementById('errorContainer');
        const mainContent = document.getElementById('mainContent');
        const errorMsg = errorContainer.querySelector('.error-message');
        if (errorMsg) errorMsg.textContent = message;
        errorContainer.classList.add('show');
        mainContent.style.display = 'none';
    }

    /**
     * 业务意图：根据“是否访问他人资料”切换编辑区可见性，保证只读语义。
     * 异常边界：若某些 DOM 未渲染（灰度或模板差异），仅跳过该节点，不抛错。
     * DOM/API 映射：tabEdit/tabSecurity/tabAssets/sidebarActions/avatarContainer 仅做显示与交互禁用切换，不触发额外 API。
     */
    function updateEditableState() {
        const tabEdit = document.getElementById('tabEdit');
        const tabSecurity = document.getElementById('tabSecurity');
        const tabAssets = document.getElementById('tabAssets');
        const sidebarActions = document.getElementById('sidebarActions');
        const avatarOverlay = avatarContainer?.querySelector('.avatar-overlay');

        if (state.isViewingOtherProfile) {
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

    /**
     * 业务意图：从 /api/user/profile（本人）或 /api/user/profile/{uid}（他人）拉取资料并回填页面。
     * 异常边界：401/403/404 分支保持原行为（跳转/提示）；网络异常回退错误页，避免空白页面。
     * DOM/API 映射：
     * - API: GET /api/user/profile, GET /api/user/profile/{uid}
     * - DOM: displayName/displayHandle/displayBio/currentEmailDisplay/overview* 等展示位
     */
    async function loadProfileFromServer() {
        const token = localStorage.getItem('kax_login_token');
        if (!token) { location.href = '/login'; return; }

        try {
            const endpoint = state.targetUid ? `/api/user/profile/${state.targetUid}` : '/api/user/profile';

            const cachedAvatar = localStorage.getItem('kax_avatar_versions');
            const hasCachedVersions = cachedAvatar && cachedAvatar !== '{}';
            if (!hasCachedVersions) {
                avatarImg.style.display = 'none';
                avatarInitials.style.display = 'block';
            }

            let selfUid = null;
            if (state.targetUid) {
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
                const roleText = state.deps.mapPermissionToRole(data.permissionGroup);
                const uid = (typeof data.id !== 'undefined') ? data.id : null;
                const isBanned = !!data.isBanned;
                const banReason = data.banReason || '';
                const banExpiresAt = data.banExpiresAt || 0;

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

                document.getElementById('displayName').textContent = displayName;
                document.getElementById('displayHandle').textContent = '@' + user;
                document.getElementById('displayBio').textContent = bio;
                document.getElementById('role').textContent = roleText;
                const emailEl = document.getElementById('currentEmailDisplay');
                if (emailEl) emailEl.textContent = state.deps.maskEmail(email);
                const uidEl = document.getElementById('uid');
                if (uidEl) uidEl.textContent = uid ? String(uid) : '-';
                document.getElementById('joined').textContent = state.deps.formatUnix(registeredAt);
                document.getElementById('lastLogin').textContent = state.deps.formatUnix(lastLoginAt);

                const banRow = document.getElementById('metaBan');
                const banEl = document.getElementById('banStatus');
                if (isBanned) {
                    banRow.style.display = 'flex';
                    banEl.textContent = `已封禁（到期: ${state.deps.formatUnix(banExpiresAt)}${banReason ? ' 原因: ' + banReason : ''}）`;
                    banEl.style.color = 'var(--profile-danger)';
                } else {
                    banRow.style.display = 'none';
                }

                document.getElementById('statResourceCount').textContent = (data.assetCount ?? data.resourceCount ?? 0).toString();
                document.getElementById('statGold').textContent = (data.gold || 0).toLocaleString();
                const goldEl = document.getElementById('gold');
                if (goldEl) goldEl.textContent = (data.gold || 0).toLocaleString();

                document.getElementById('overviewBio').textContent = bio || '这个人很懒，什么都没有留下。';
                document.getElementById('overviewSignature').textContent = signature || '—';
                document.getElementById('overviewRole').textContent = roleText;
                document.getElementById('overviewUid').textContent = uid ? String(uid) : '—';
                document.getElementById('overviewGold').textContent = (data.gold || 0).toLocaleString();
                document.getElementById('overviewResources').textContent = (data.assetCount ?? data.resourceCount ?? 0).toString();
                document.getElementById('overviewJoined').textContent = state.deps.formatUnix(registeredAt);
                document.getElementById('overviewLastLogin').textContent = state.deps.formatUnix(lastLoginAt);

                document.getElementById('inputName').value = displayName;
                document.getElementById('inputHandle').value = user;
                document.getElementById('inputRole').value = roleText;
                document.getElementById('inputBio').value = bio;
                document.getElementById('inputSignature').value = signature;

                state.originalProfile = { name: displayName, handle: user, email, role: roleText, bio, signature, avatarSrc: serverAvatar || (avatarImg.src || '') };

                if (uid) state.currentUserUid = state.targetUid ? selfUid : uid;

                const resolvedSelfUid = state.targetUid ? selfUid : uid;
                state.isViewingOtherProfile = !!(state.targetUid && resolvedSelfUid && state.targetUid !== String(resolvedSelfUid));

                if (!state.isViewingOtherProfile && state.deps.isAdminPermission(data.permissionGroup)) {
                    state.deps.setAdminUser(true);
                    state.deps.showAdminTabs();
                } else {
                    state.deps.setAdminUser(false);
                    state.deps.hideAdminTabs();
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

    function avatarCropClampPosition() {
        const viewSize = cropViewport.clientWidth || 0;
        const { cropRadius, scale, imgNaturalW, imgNaturalH } = avatarCropState;
        if (!viewSize || !cropRadius || !scale || !imgNaturalW || !imgNaturalH) return;

        const center = viewSize / 2;
        const leftLimit = center - cropRadius;
        const rightLimit = center + cropRadius;
        const topLimit = center - cropRadius;
        const bottomLimit = center + cropRadius;

        const displayW = imgNaturalW * scale;
        const displayH = imgNaturalH * scale;

        const minOffsetX = rightLimit - displayW;
        const maxOffsetX = leftLimit;
        const minOffsetY = bottomLimit - displayH;
        const maxOffsetY = topLimit;

        avatarCropState.offsetX = Math.min(maxOffsetX, Math.max(minOffsetX, avatarCropState.offsetX));
        avatarCropState.offsetY = Math.min(maxOffsetY, Math.max(minOffsetY, avatarCropState.offsetY));
    }

    function avatarCropApplyTransform() {
        cropSourceImg.style.transform = `translate(${avatarCropState.offsetX}px, ${avatarCropState.offsetY}px) scale(${avatarCropState.scale})`;
    }

    function avatarCropDrawOverlay() {
        const w = cropViewport.clientWidth || 0;
        const h = cropViewport.clientHeight || w;
        if (!w || !h) return;
        cropOverlay.width = w;
        cropOverlay.height = h;

        const ctx = cropOverlay.getContext('2d');
        if (!ctx) return;

        const radius = Math.floor(Math.min(w, h) * 0.38);
        avatarCropState.cropRadius = radius;
        const cx = w / 2;
        const cy = h / 2;

        ctx.clearRect(0, 0, w, h);
        ctx.fillStyle = 'rgba(0,0,0,0.52)';
        ctx.fillRect(0, 0, w, h);

        ctx.save();
        ctx.globalCompositeOperation = 'destination-out';
        ctx.beginPath();
        ctx.arc(cx, cy, radius, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();

        ctx.beginPath();
        ctx.arc(cx, cy, radius, 0, Math.PI * 2);
        ctx.lineWidth = 2;
        ctx.strokeStyle = 'rgba(255,255,255,0.85)';
        ctx.stroke();
    }

    function avatarCropFitAndCenter() {
        const viewSize = cropViewport.clientWidth || 0;
        if (!viewSize || !avatarCropState.imgNaturalW || !avatarCropState.imgNaturalH) return;

        const needSize = avatarCropState.cropRadius * 2;
        const minScaleToCoverCircle = Math.max(needSize / avatarCropState.imgNaturalW, needSize / avatarCropState.imgNaturalH);
        const minScaleToFillViewport = Math.max(viewSize / avatarCropState.imgNaturalW, viewSize / avatarCropState.imgNaturalH);

        avatarCropState.minScale = Math.max(minScaleToCoverCircle, minScaleToFillViewport * 0.6);
        avatarCropState.maxScale = Math.max(avatarCropState.minScale * 4, avatarCropState.minScale + 0.1);
        avatarCropState.scale = avatarCropState.minScale;

        const displayW = avatarCropState.imgNaturalW * avatarCropState.scale;
        const displayH = avatarCropState.imgNaturalH * avatarCropState.scale;
        avatarCropState.offsetX = (viewSize - displayW) / 2;
        avatarCropState.offsetY = (viewSize - displayH) / 2;

        avatarCropClampPosition();
        avatarCropApplyTransform();
        cropZoomRange.value = '0';
    }

    function avatarCropApplyScaleBySlider(value) {
        const t = Math.max(0, Math.min(100, Number(value) || 0)) / 100;
        const oldScale = avatarCropState.scale;
        const newScale = avatarCropState.minScale + (avatarCropState.maxScale - avatarCropState.minScale) * t;
        if (!oldScale || !newScale || oldScale === newScale) return;

        const viewSize = cropViewport.clientWidth || 0;
        const anchorX = viewSize / 2;
        const anchorY = viewSize / 2;
        const ratio = newScale / oldScale;

        avatarCropState.offsetX = anchorX - (anchorX - avatarCropState.offsetX) * ratio;
        avatarCropState.offsetY = anchorY - (anchorY - avatarCropState.offsetY) * ratio;
        avatarCropState.scale = newScale;

        avatarCropClampPosition();
        avatarCropApplyTransform();
    }

    /**
     * 业务意图：提供头像裁剪预览与最终 512x512 导出，保持原上传体验。
     * 异常边界：文件类型非法、Canvas/Blob 失败时给出提示并回退，不写入错误文件。
     * DOM/API 映射：avatarFile/avatarCropModal/cropViewport/cropConfirmBtn；最终仍走原有 /api/user/avatar 上传链路（由保存资料提交触发）。
     */
    function openAvatarCropModal(dataUrl, fileName, fileType) {
        avatarCropState.fileName = fileName || 'avatar.png';
        avatarCropState.fileType = fileType || 'image/png';

        cropSourceImg.onload = () => {
            avatarCropState.imgNaturalW = cropSourceImg.naturalWidth;
            avatarCropState.imgNaturalH = cropSourceImg.naturalHeight;
            avatarCropDrawOverlay();
            avatarCropFitAndCenter();
        };
        cropSourceImg.src = dataUrl;
        avatarCropModal.classList.add('show');
    }

    function closeAvatarCropModal(resetFile = false) {
        avatarCropModal.classList.remove('show');
        avatarCropState.dragging = false;
        if (resetFile) avatarFile.value = '';
    }

    function avatarCropStopDragging(e) {
        if (!avatarCropState.dragging) return;
        avatarCropState.dragging = false;
        cropViewport.style.cursor = 'grab';
        if (typeof e?.pointerId !== 'undefined') {
            try { cropViewport.releasePointerCapture(e.pointerId); } catch (err) { /* 容错 */ }
        }
    }

    function bindAvatarEvents() {
        cropZoomRange?.addEventListener('input', () => {
            avatarCropApplyScaleBySlider(cropZoomRange.value);
        });

        cropViewport?.addEventListener('pointerdown', (e) => {
            if (!avatarCropModal.classList.contains('show')) return;
            avatarCropState.dragging = true;
            avatarCropState.dragStartX = e.clientX;
            avatarCropState.dragStartY = e.clientY;
            avatarCropState.dragOriginX = avatarCropState.offsetX;
            avatarCropState.dragOriginY = avatarCropState.offsetY;
            cropViewport.style.cursor = 'grabbing';
            cropViewport.setPointerCapture(e.pointerId);
        });

        cropViewport?.addEventListener('pointermove', (e) => {
            if (!avatarCropState.dragging) return;
            const dx = e.clientX - avatarCropState.dragStartX;
            const dy = e.clientY - avatarCropState.dragStartY;
            avatarCropState.offsetX = avatarCropState.dragOriginX + dx;
            avatarCropState.offsetY = avatarCropState.dragOriginY + dy;
            avatarCropClampPosition();
            avatarCropApplyTransform();
        });

        cropViewport?.addEventListener('pointerup', avatarCropStopDragging);
        cropViewport?.addEventListener('pointercancel', avatarCropStopDragging);
        cropViewport?.addEventListener('wheel', (e) => {
            if (!avatarCropModal.classList.contains('show')) return;
            e.preventDefault();
            const delta = e.deltaY > 0 ? -3 : 3;
            cropZoomRange.value = String(Math.max(0, Math.min(100, Number(cropZoomRange.value) + delta)));
            avatarCropApplyScaleBySlider(cropZoomRange.value);
        }, { passive: false });

        cropCancelBtn?.addEventListener('click', () => closeAvatarCropModal(true));

        cropConfirmBtn?.addEventListener('click', async () => {
            if (!cropSourceImg.src || !avatarCropState.scale) return;

            const viewSize = cropViewport.clientWidth || 0;
            const center = viewSize / 2;
            const cropSize = avatarCropState.cropRadius * 2;
            const cropX = center - avatarCropState.cropRadius;
            const cropY = center - avatarCropState.cropRadius;

            const sx = (cropX - avatarCropState.offsetX) / avatarCropState.scale;
            const sy = (cropY - avatarCropState.offsetY) / avatarCropState.scale;
            const sSize = cropSize / avatarCropState.scale;

            const outSize = 512;
            const canvas = document.createElement('canvas');
            canvas.width = outSize;
            canvas.height = outSize;
            const ctx = canvas.getContext('2d');
            if (!ctx) return;

            ctx.drawImage(cropSourceImg, sx, sy, sSize, sSize, 0, 0, outSize, outSize);

            const blob = await new Promise(resolve => canvas.toBlob(resolve, avatarCropState.fileType, 0.92));
            if (!blob) { alert('头像裁剪失败，请重试'); return; }

            const croppedFile = new File([blob], avatarCropState.fileName, { type: avatarCropState.fileType });
            const dt = new DataTransfer();
            dt.items.add(croppedFile);
            avatarFile.files = dt.files;

            avatarImg.src = URL.createObjectURL(croppedFile);
            avatarImg.style.display = 'block';
            avatarInitials.style.display = 'none';

            closeAvatarCropModal(false);
        });

        avatarContainer?.addEventListener('click', () => {
            if (!state.isViewingOtherProfile) avatarFile.click();
        });
        avatarContainer?.addEventListener('keydown', (e) => {
            if (!state.isViewingOtherProfile && (e.key === 'Enter' || e.key === ' ')) {
                e.preventDefault();
                avatarFile.click();
            }
        });
        avatarFile?.addEventListener('change', (ev) => {
            const file = ev.target.files[0];
            if (!file) return;

            if (!file.type || !file.type.startsWith('image/')) {
                alert('请选择图片文件');
                avatarFile.value = '';
                return;
            }

            const reader = new FileReader();
            reader.onload = function (e) {
                openAvatarCropModal(e.target.result, file.name, file.type);
            };
            reader.readAsDataURL(file);
        });
    }

    function bindProfileFormEvents() {
        document.getElementById('profileForm')?.addEventListener('submit', async (ev) => {
            ev.preventDefault();
            if (state.isViewingOtherProfile) { alert('无法编辑他人资料'); return; }

            const token = localStorage.getItem('kax_login_token');
            if (!token) { location.href = '/login'; return; }

            const displayName = document.getElementById('inputName').value.trim();
            const bio = document.getElementById('inputBio').value || '';
            const signature = document.getElementById('inputSignature').value || '';

            saveBtn.disabled = true;
            try {
                const avatarFileEl = document.getElementById('avatarFile');
                if (avatarFileEl && avatarFileEl.files && avatarFileEl.files.length > 0) {
                    const file = avatarFileEl.files[0];
                    const fd = new FormData();
                    fd.append('avatar', file, file.name);
                    const upResp = await fetch('/api/user/avatar', { method: 'POST', headers: { 'Authorization': 'Bearer ' + token }, body: fd });
                    const upJson = await upResp.json().catch(() => ({}));
                    if (upResp.status === 200 || upResp.status === 201) {
                        if (upJson.url) {
                            avatarImg.style.display = 'block';
                            avatarInitials.style.display = 'none';
                            state.originalProfile.avatarSrc = upJson.url;
                            if (window.AvatarCache) {
                                try {
                                    await AvatarCache.updateCache(upJson.url, file);
                                    var resolvedUrl = await AvatarCache.getAvatar(upJson.url);
                                    avatarImg.src = resolvedUrl;
                                } catch (e) { avatarImg.src = upJson.url; }
                            } else {
                                avatarImg.src = upJson.url;
                            }
                        }
                    } else if (upResp.status === 401) {
                        localStorage.removeItem('kax_login_token'); location.href = '/login'; return;
                    } else {
                        alert(upJson.message || '头像上传失败');
                        saveBtn.disabled = false; return;
                    }
                }

                const resp = await fetch('/api/user/profile', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                    body: JSON.stringify({ displayName, bio, signature, targetUid: state.currentUserUid })
                });

                const result = await resp.json().catch(() => ({}));
                if (resp.status === 200) {
                    document.getElementById('displayName').textContent = displayName || state.originalProfile.name;
                    document.getElementById('displayBio').textContent = bio;
                    document.getElementById('overviewBio').textContent = bio || '这个人很懒，什么都没有留下。';
                    document.getElementById('overviewSignature').textContent = signature || '—';
                    state.originalProfile = { ...state.originalProfile, name: displayName || state.originalProfile.name, bio, signature };
                    const leftEmail = document.getElementById('currentEmailDisplay');
                    if (leftEmail) leftEmail.textContent = state.deps.maskEmail(state.originalProfile.email);
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

        cancelBtn?.addEventListener('click', () => {
            document.getElementById('inputName').value = state.originalProfile.name;
            document.getElementById('inputHandle').value = state.originalProfile.handle;
            document.getElementById('inputRole').value = state.originalProfile.role;
            document.getElementById('inputBio').value = state.originalProfile.bio;
            document.getElementById('inputSignature').value = state.originalProfile.signature;
            if (state.originalProfile.avatarSrc && !state.originalProfile.avatarSrc.endsWith('/default-avatar.jpg')) {
                avatarImg.src = state.originalProfile.avatarSrc;
                avatarImg.style.display = 'block';
                avatarInitials.style.display = 'none';
            } else {
                avatarImg.style.display = 'none';
                avatarInitials.style.display = 'block';
            }
        });
    }

    /**
     * 业务意图：处理账号敏感字段更新（密码/邮箱），并维持原有成功/失败反馈语义。
     * 异常边界：本地校验失败直接拦截；401 清 token 并跳转；网络失败统一提示。
     * DOM/API 映射：
     * - API: POST /api/user/password, POST /api/user/profile/update-field
     * - DOM: pwOld/pw1/pw2/changePwBtn/emailEditInput/updateEmailBtn/currentEmailDisplay
     */
    function bindSecurityEvents() {
        function startButtonCooldown(btn, seconds = 30) {
            if (!btn) return;
            const original = btn.dataset.originalText || btn.textContent;
            btn.dataset.originalText = original;
            let remain = Number(seconds) || 30;
            btn.disabled = true;
            btn.textContent = `${remain}s 后可重发`;
            const timer = setInterval(() => {
                remain -= 1;
                if (remain <= 0) {
                    clearInterval(timer);
                    btn.disabled = false;
                    btn.textContent = original;
                    return;
                }
                btn.textContent = `${remain}s 后可重发`;
            }, 1000);
        }

        async function sendOldEmailCode() {
            if (state.isViewingOtherProfile) { alert('无法修改他人邮箱'); return; }
            const token = checkToken();
            if (!token) return;
            if (!state.currentUserUid) { alert('用户信息尚未就绪，请稍后重试'); return; }

            const btn = sendOldEmailCodeBtn;
            await withButtonLoading(btn, '发送中...', async () => {
                try {
                    const payload = { targetUid: state.currentUserUid, channel: 'old' };

                    const resp = await fetch('/api/user/email-change/send-code', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                        body: JSON.stringify(payload)
                    });
                    const result = await resp.json().catch(() => ({}));

                    if (resp.status === 200) {
                        showErrorMsg(emailVerifyMessage, '旧邮箱验证码已发送', false);
                        const cooldown = Number(result?.data?.cooldownSeconds || 30);
                        startButtonCooldown(btn, cooldown);
                    } else if (resp.status === 401) {
                        localStorage.removeItem('kax_login_token');
                        location.href = '/login';
                    } else {
                        showErrorMsg(emailVerifyMessage, result.message || `验证码发送失败：${resp.status}`, true);
                    }
                } catch (err) {
                    console.error('发送邮箱验证码失败：', err);
                    showErrorMsg(emailVerifyMessage, '无法连接到服务器', true);
                }
            });
        }

        document.getElementById('changePwBtn')?.addEventListener('click', async () => {
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

        updateEmailBtn?.addEventListener('click', async () => {
            if (state.isViewingOtherProfile) { alert('无法修改他人邮箱'); return; }
            const token = checkToken();
            if (!token) return;
            if (!state.currentUserUid) { alert('用户信息尚未就绪，请稍后重试'); return; }

            const newEmail = (emailEditInput?.value || '').trim();
            if (!newEmail) { alert('请输入新邮箱'); return; }
            const oldEmailCode = (oldEmailCodeInput?.value || '').trim();
            if (!oldEmailCode) { alert('请先输入旧邮箱验证码'); return; }

            await withButtonLoading(updateEmailBtn, '验证并更新中...', async () => {
                try {
                    const resp = await fetch('/api/user/email-change/confirm', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                        body: JSON.stringify({ targetUid: state.currentUserUid, newEmail, oldEmailCode })
                    });
                    const result = await resp.json().catch(() => ({}));

                    if (resp.status === 200) {
                        state.originalProfile.email = newEmail;
                        const emailEl = document.getElementById('currentEmailDisplay');
                        if (emailEl) emailEl.textContent = state.deps.maskEmail(newEmail);
                        showErrorMsg(emailVerifyMessage, result.message || '邮箱已更新', false);
                        if (result?.data?.requireRelogin) {
                            localStorage.removeItem('kax_login_token');
                            setTimeout(() => { location.href = '/login'; }, 800);
                        }
                    } else if (resp.status === 401) {
                        localStorage.removeItem('kax_login_token');
                        location.href = '/login';
                    } else {
                        showErrorMsg(emailVerifyMessage, result.message || ('邮箱更新失败：' + resp.status), true);
                    }
                } catch (err) {
                    console.error('更新邮箱失败：', err);
                    showErrorMsg(emailVerifyMessage, '无法连接到服务器', true);
                }
            });
        });

        sendOldEmailCodeBtn?.addEventListener('click', sendOldEmailCode);
    }

    /**
     * 业务意图：加载当前用户已激活资产，并保持更变/退订入口与统计展示一致。
     * 异常边界：401 清 token 跳转登录；网络失败显示空态错误文案。
     * DOM/API 映射：
     * - API: GET /api/user/assets/active, GET /api/asset/name/{id}
     * - DOM: assetsLoading/assetsEmpty/assetsList/assetsSummary*
     */
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

                    const elTotal = document.getElementById('assetsSummaryTotal');
                    const elActive = document.getElementById('assetsSummaryActive');
                    const elExpired = document.getElementById('assetsSummaryExpired');
                    const elForever = document.getElementById('assetsSummaryForever');
                    if (elTotal) elTotal.textContent = assets.length;
                    if (elActive) elActive.textContent = countActive;
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

    function openChangePlanModal(assetId, assetName) {
        state.currentAssetId = assetId;
        state.currentAssetName = assetName;
        state.selectedPlanId = null;
        document.getElementById('planModalAssetName').textContent = assetName;
        loadAvailablePlans();
        document.getElementById('changePlanModal').classList.add('show');
    }

    function closePlanModal() {
        document.getElementById('changePlanModal').classList.remove('show');
        state.currentAssetId = null;
        state.currentAssetName = null;
        state.selectedPlanId = null;
        document.getElementById('planModalConfirm').style.display = 'none';
        document.getElementById('planModalMessage').style.display = 'none';
    }

    function openUnsubscribeModal(assetId, assetName) {
        state.currentAssetId = assetId;
        state.currentAssetName = assetName;
        document.getElementById('unsubscribeModalAssetName').textContent = assetName;
        document.getElementById('unsubscribeModal').classList.add('show');
    }

    function closeUnsubscribeModal() {
        document.getElementById('unsubscribeModal').classList.remove('show');
        state.currentAssetId = null;
        state.currentAssetName = null;
    }

    async function loadAvailablePlans() {
        const planList = document.getElementById('planList');
        planList.innerHTML = '<div style="color: var(--profile-muted); text-align: center; padding: 20px;">加载套餐中...</div>';

        try {
            const token = localStorage.getItem('kax_login_token');
            if (!token) { location.href = '/login'; return; }

            const resp = await fetch(`/api/asset/${state.currentAssetId}/plans`, { headers: { 'Authorization': 'Bearer ' + token } });
            if (resp.status === 200) {
                const result = await resp.json().catch(() => ({}));
                const plans = result.data || [];
                state.availablePlans = plans;
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
                    `;
                    }).join('');
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
        state.selectedPlanId = planId;
    }

    function bindAssetsEvents() {
        const cdkInput = document.getElementById('cdkInput');
        const activateCdkBtn = document.getElementById('activateCdkBtn');
        const cdkMessage = document.getElementById('cdkMessage');
        const cdkResult = document.getElementById('cdkResult');
        const cdkResultDetails = document.getElementById('cdkResultDetails');

        activateCdkBtn?.addEventListener('click', async () => {
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

        cdkInput?.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') activateCdkBtn?.click();
        });

        document.getElementById('confirmChangePlanBtn')?.addEventListener('click', () => {
            if (!state.selectedPlanId) { alert('请先选择要更变的套餐'); return; }
            const plan = state.availablePlans.find(p => p.id === state.selectedPlanId);
            const cost = plan ? (plan.price || 0) : 0;
            document.getElementById('planModalConfirmCost').textContent = `💰 ${cost.toFixed(2)}`;
            setElementDisplay(document.getElementById('planModalConfirm'), true);
        });

        document.getElementById('planModalConfirmNo')?.addEventListener('click', () => {
            setElementDisplay(document.getElementById('planModalConfirm'), false);
        });

        document.getElementById('planModalConfirmYes')?.addEventListener('click', async () => {
            setElementDisplay(document.getElementById('planModalConfirm'), false);
            const token = checkToken();
            if (!token) return;

            const btn = document.getElementById('confirmChangePlanBtn');
            await withButtonLoading(btn, '处理中...', async () => {
                try {
                    const resp = await fetch(`/api/asset/${state.currentAssetId}/changePlan`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                        body: JSON.stringify({ planId: state.selectedPlanId })
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

        document.getElementById('confirmUnsubscribeBtn')?.addEventListener('click', async () => {
            const token = checkToken();
            if (!token) return;

            const btn = document.getElementById('confirmUnsubscribeBtn');
            await withButtonLoading(btn, '取消中...', async () => {
                try {
                    const resp = await fetch(`/api/asset/${state.currentAssetId}/unsubscribe`, {
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

        document.querySelectorAll('.modal-overlay').forEach(overlay => {
            overlay.addEventListener('click', (e) => {
                if (e.target === overlay) {
                    overlay.classList.remove('show');
                    if (overlay.id === 'changePlanModal') closePlanModal();
                    if (overlay.id === 'avatarCropModal') closeAvatarCropModal(true);
                }
            });
        });
    }

    async function initializePage() {
        const token = localStorage.getItem('kax_login_token');
        if (!token) { location.href = '/login'; return; }

        try {
            const currentResp = await fetch('/api/user/profile', { headers: { 'Authorization': 'Bearer ' + token } });
            if (currentResp.status === 200) {
                const currentData = await currentResp.json();
                state.currentUserUid = (typeof currentData.id !== 'undefined') ? currentData.id : null;
                if (state.targetUid && state.currentUserUid && state.targetUid !== String(state.currentUserUid)) {
                    state.isViewingOtherProfile = true;
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

    function init(options = {}) {
        if (state.initialized) return;
        state.targetUid = options.targetUid || null;
        state.deps = {
            ...state.deps,
            ...options
        };

        bindProfileFormEvents();
        bindAvatarEvents();
        bindSecurityEvents();
        bindAssetsEvents();

        // 兼容既有 inline onclick
        global.openChangePlanModal = openChangePlanModal;
        global.closePlanModal = closePlanModal;
        global.openUnsubscribeModal = openUnsubscribeModal;
        global.closeUnsubscribeModal = closeUnsubscribeModal;
        global.selectPlan = selectPlan;

        state.initialized = true;
    }

    global.ProfileUser = {
        init,
        initializePage,
        loadProfileFromServer,
        updateEditableState,
        loadActiveAssets,
        openChangePlanModal,
        closePlanModal,
        openUnsubscribeModal,
        closeUnsubscribeModal,
        selectPlan,
        closeAvatarCropModal,
        isViewingOtherProfile: () => state.isViewingOtherProfile,
        getCurrentUserUid: () => state.currentUserUid,
        getOriginalProfile: () => ({ ...state.originalProfile })
    };
})(window);
