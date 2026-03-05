/* ================================================================
 *  developer.actions.js — 用户交互操作层
 *  包含：switchTab、editAsset、submitForm、submitReview、
 *        publishAsset、审核操作（approve / reject）、
 *        系统操作（return / off-shelf / force-review / hard-delete）
 *        bindEvents、init
 *  依赖：developer.utils.js、developer.renderer.js、developer.api.js
 * ================================================================ */
'use strict';

function devMsgBox({ title = '提示', message = '', type = 'info', confirmText = '确定', cancelText = '取消', showCancel = false } = {}) {
    if (typeof window.showMsgBox === 'function' && !showCancel) {
        return new Promise(resolve => {
            window.showMsgBox({
                title,
                message,
                type,
                onConfirm: () => resolve(true)
            });
        });
    }

    return new Promise(resolve => {
        const overlay = document.createElement('div');
        overlay.style.cssText = 'position:fixed;inset:0;background:rgba(0,0,0,.48);display:flex;align-items:center;justify-content:center;z-index:9999;padding:16px;';

        const card = document.createElement('div');
        card.style.cssText = 'width:min(420px,100%);background:#121212;border:1px solid rgba(255,255,255,.1);border-radius:8px;padding:16px;color:#fff;box-shadow:0 14px 40px rgba(0,0,0,.4);';

        const titleEl = document.createElement('h3');
        titleEl.textContent = title;
        titleEl.style.cssText = 'margin:0 0 8px;font-size:16px;font-weight:700;';

        const msgEl = document.createElement('p');
        msgEl.textContent = message;
        msgEl.style.cssText = 'margin:0 0 14px;font-size:13px;line-height:1.6;color:rgba(255,255,255,.78);';

        const actions = document.createElement('div');
        actions.style.cssText = 'display:flex;justify-content:flex-end;gap:8px;';

        const cancelBtn = document.createElement('button');
        cancelBtn.type = 'button';
        cancelBtn.textContent = cancelText;
        cancelBtn.style.cssText = 'padding:8px 12px;border-radius:6px;border:1px solid rgba(255,255,255,.18);background:transparent;color:#fff;cursor:pointer;';

        const confirmBtn = document.createElement('button');
        confirmBtn.type = 'button';
        confirmBtn.textContent = confirmText;
        confirmBtn.style.cssText = 'padding:8px 12px;border-radius:6px;border:none;background:#fff;color:#0f0f0f;font-weight:700;cursor:pointer;';

        const close = (result) => {
            try { overlay.remove(); } catch (_) { }
            resolve(result);
        };

        confirmBtn.addEventListener('click', () => close(true));
        cancelBtn.addEventListener('click', () => close(false));
        overlay.addEventListener('click', (e) => {
            if (e.target === overlay && showCancel) close(false);
        });

        actions.appendChild(confirmBtn);
        if (showCancel) actions.insertBefore(cancelBtn, confirmBtn);
        card.appendChild(titleEl);
        card.appendChild(msgEl);
        card.appendChild(actions);
        overlay.appendChild(card);
        document.body.appendChild(overlay);
    });
}

/* ----------------------------------------------------------------
   Tab 切换
   ---------------------------------------------------------------- */
function switchTab(tabName) {
    document.querySelectorAll('.dev-tab').forEach(t => {
        const active = t.dataset.tab === tabName;
        t.classList.toggle('active', active);
        t.setAttribute('aria-selected', active ? 'true' : 'false');
    });
    document.querySelectorAll('.dev-tab-content').forEach(c => {
        c.classList.toggle('active', c.id === 'tab-' + tabName);
    });

    if (tabName === 'review-panel' && state.isAdmin) {
        loadReviewList();
    }
    if (tabName === 'asset-management' && state.isSystem) {
        loadSystemAssetList();
    }
    if (tabName === 'create-asset' && !state.editingId) {
        resetForm();
    }
}

/* ----------------------------------------------------------------
   我的资源操作
   ---------------------------------------------------------------- */
async function editAsset(id) {
    try {
        const resp = await apiGetMyAssetDetail(id);
        if (resp.code !== 0) { alert(resp.message); return; }
        fillForm(resp.data);
        switchTab('create-asset');
    } catch (e) {
        alert('加载资产详情失败');
    }
}

async function submitForm() {
    if (!requireLogin()) return;

    const data = collectFormData();
    if (!data.name) { alert('请填写资源名称'); return; }
    if (!data.version) { alert('请填写版本号'); return; }

    try {
        if (state.editingId) {
            const shouldSubmitReview = await devMsgBox({
                title: '确认发起重审',
                message: '发起后将先保存当前修改，并使资源下架后重新进入审核状态。',
                type: 'warn',
                confirmText: '发起重审',
                cancelText: '稍后再说',
                showCancel: true
            });

            if (!shouldSubmitReview) {
                return;
            }

            const updateResp = await apiUpdateAsset({ id: state.editingId, ...data });
            if (updateResp.code !== 0) {
                await devMsgBox({ title: '保存失败', message: updateResp.message || '保存失败', type: 'error' });
                return;
            }

            if (shouldSubmitReview) {
                const submitResp = await apiSubmitReview(state.editingId);
                if (submitResp.code === 0) {
                    await devMsgBox({
                        title: '已提交重审',
                        message: '资源已下架并重新进入审核状态，请等待审核结果。',
                        type: 'success'
                    });
                } else if (submitResp.cooldownRemaining) {
                    await devMsgBox({
                        title: '提交冷却中',
                        message: `资源已保存，但提交冷却中，请在 ${formatCooldown(submitResp.cooldownRemaining)} 后手动重新提交。`,
                        type: 'warn'
                    });
                } else {
                    await devMsgBox({
                        title: '提交审核失败',
                        message: `资源已保存，但提交审核失败：${submitResp.message || '未知错误'}`,
                        type: 'error'
                    });
                }
            }
        } else {
            const resp = await apiCreateAsset(data);
            if (resp.code !== 0) {
                await devMsgBox({ title: '创建失败', message: resp.message || '创建失败', type: 'error' });
                return;
            }
            await devMsgBox({ title: '创建成功', message: resp.message || '操作成功', type: 'success' });
        }

        resetForm();
        switchTab('my-assets');
        loadMyAssets();
        loadMyStats();
    } catch (e) {
        alert('请求失败: ' + e.message);
    }
}

async function submitReview(id) {
    if (!confirm('确认重新提交审核？')) return;
    try {
        let submitPayload = null;
        if (state.editingId === id) {
            submitPayload = collectFormData();
        }
        const resp = await apiSubmitReview(id, submitPayload);
        if (resp.code === 0) {
            alert('已提交审核');
            loadMyAssets();
            loadMyStats();
        } else {
            if (resp.cooldownRemaining) {
                alert(`冷却中，请在 ${formatCooldown(resp.cooldownRemaining)} 后重试`);
            } else {
                alert(resp.message || '操作失败');
            }
        }
    } catch (e) {
        alert('请求失败');
    }
}

async function publishAsset(id) {
    if (!confirm('确认发布到商店？')) return;
    try {
        const resp = await apiPublishAsset(id);
        if (resp.code === 0) {
            alert('已发布');
            loadMyAssets();
            loadMyStats();
        } else {
            alert(resp.message || '发布失败');
        }
    } catch (e) {
        alert('请求失败');
    }
}

/* ----------------------------------------------------------------
   审核操作
   ---------------------------------------------------------------- */
async function approveAsset() {
    const id = state.currentReviewAssetId;
    if (!id) return;
    const actionBtn = document.getElementById('modalApproveBtn');
    if (actionBtn?.dataset.loading === '1') return;
    if (!confirm('确认通过此资源审核？')) return;

    const stopLoading = setButtonLoading(actionBtn, '通过中...');
    try {
        const resp = await apiApprove(id);
        if (resp.code === 0) {
            alert('审核通过');
            closeReviewModal();
            loadReviewList();
        } else {
            alert(resp.message || '操作失败');
        }
    } catch (e) {
        alert('请求失败');
    } finally {
        stopLoading();
    }
}

function openRejectModal() {
    if (!state.currentReviewAssetId) return;
    document.getElementById('rejectReason').value = '';
    document.getElementById('rejectModal').style.display = '';
}

function closeRejectModal() {
    document.getElementById('rejectModal').style.display = 'none';
}

async function confirmReject() {
    const id = state.currentReviewAssetId;
    if (!id) return;
    const actionBtn = document.getElementById('confirmRejectBtn');
    if (actionBtn?.dataset.loading === '1') return;
    const reason = document.getElementById('rejectReason').value.trim();
    if (!reason) { alert('请填写拒绝原因'); return; }

    const stopLoading = setButtonLoading(actionBtn, '拒绝中...');
    try {
        const resp = await apiReject(id, reason);
        if (resp.code === 0) {
            alert('已拒绝');
            closeRejectModal();
            closeReviewModal();
            loadReviewList();
        } else {
            alert(resp.message || '操作失败');
        }
    } catch (e) {
        alert('请求失败');
    } finally {
        stopLoading();
    }
}

/* ----------------------------------------------------------------
   系统资产操作
   ---------------------------------------------------------------- */
async function returnSystemAsset() {
    const id = state.currentSystemAssetId;
    if (!id) return;
    const actionBtn = document.getElementById('systemReturnBtn');
    if (actionBtn?.dataset.loading === '1') return;
    const reason = document.getElementById('systemActionReason')?.value.trim() || '';
    if (!reason) { alert('退回原因必填'); return; }
    if (!confirm('确认退回该资产？')) return;

    const stopLoading = setButtonLoading(actionBtn, '退回中...');
    try {
        const resp = await apiSystemReturn(id, reason);
        if (resp.code === 0) {
            alert(resp.message || '已退回');
            await Promise.all([loadSystemAssetList(), openSystemAssetModal(id)]);
        } else {
            alert(resp.message || '退回失败');
        }
    } catch (e) {
        alert('退回失败: ' + e.message);
    } finally {
        stopLoading();
    }
}

async function offShelfSystemAsset() {
    const id = state.currentSystemAssetId;
    if (!id) return;
    const actionBtn = document.getElementById('systemOffShelfBtn');
    if (actionBtn?.dataset.loading === '1') return;
    const reason = document.getElementById('systemActionReason')?.value.trim() || '';
    if (!reason) { alert('下架原因必填'); return; }
    if (!confirm('确认将该资产下架？')) return;

    const stopLoading = setButtonLoading(actionBtn, '下架中...');
    try {
        const resp = await apiSystemOffShelf(id, reason);
        if (resp.code === 0) {
            alert(resp.message || '已下架');
            await Promise.all([loadSystemAssetList(), openSystemAssetModal(id)]);
        } else {
            alert(resp.message || '下架失败');
        }
    } catch (e) {
        alert('下架失败: ' + e.message);
    } finally {
        stopLoading();
    }
}

async function forceReviewSystemAsset() {
    const id = state.currentSystemAssetId;
    if (!id) return;
    const actionBtn = document.getElementById('systemForceReviewBtn');
    if (actionBtn?.dataset.loading === '1') return;
    const reason = document.getElementById('systemActionReason')?.value.trim() || '';
    if (!reason) { alert('重审原因必填'); return; }
    if (!confirm('确认强制重审该资产？')) return;

    const stopLoading = setButtonLoading(actionBtn, '重审中...');
    try {
        const resp = await apiSystemForceReview(id, reason);
        if (resp.code === 0) {
            alert(resp.message || '已进入重审流程');
            await Promise.all([loadSystemAssetList(), openSystemAssetModal(id)]);
        } else {
            alert(resp.message || '强制重审失败');
        }
    } catch (e) {
        alert('强制重审失败: ' + e.message);
    } finally {
        stopLoading();
    }
}

async function hardDeleteSystemAsset() {
    const id = state.currentSystemAssetId;
    if (!id) return;
    const actionBtn = document.getElementById('systemHardDeleteBtn');
    if (actionBtn?.dataset.loading === '1') return;
    const reason = document.getElementById('systemActionReason')?.value.trim() || '';
    if (!reason) { alert('彻底删除原因必填'); return; }
    if (!confirm('确认彻底删除该资产？该操作不可恢复。')) return;

    const stopLoading = setButtonLoading(actionBtn, '删除中...');
    try {
        const resp = await apiSystemHardDelete(id, reason);
        if (resp.code === 0) {
            alert(resp.message || '已彻底删除');
            closeSystemAssetModal();
            await loadSystemAssetList();
        } else {
            alert(resp.message || '彻底删除失败');
        }
    } catch (e) {
        alert('彻底删除失败: ' + e.message);
    } finally {
        stopLoading();
    }
}

/* ----------------------------------------------------------------
   事件绑定 & 初始化
   ---------------------------------------------------------------- */
function bindEvents() {
    document.querySelectorAll('.dev-tab').forEach(tab => {
        tab.addEventListener('click', () => switchTab(tab.dataset.tab));
    });

    const statusFilter = document.getElementById('statusFilter');
    if (statusFilter) {
        statusFilter.addEventListener('change', () => {
            state.myStatusFilter = statusFilter.value;
            state.myPage = 1;
            loadMyAssets();
        });
    }

    const reviewStatusFilter = document.getElementById('reviewStatusFilter');
    if (reviewStatusFilter) {
        reviewStatusFilter.addEventListener('change', () => {
            state.reviewStatusFilter = reviewStatusFilter.value;
            state.reviewPage = 1;
            loadReviewList();
        });
    }

    const systemStatusFilter = document.getElementById('systemAssetStatusFilter');
    if (systemStatusFilter) {
        systemStatusFilter.addEventListener('change', () => {
            state.systemStatusFilter = systemStatusFilter.value;
            state.systemPage = 1;
            loadSystemAssetList();
        });
    }

    const systemKeyword = document.getElementById('systemAssetKeyword');
    const systemAuthorId = document.getElementById('systemAssetAuthorId');
    const systemSearchBtn = document.getElementById('systemAssetSearchBtn');
    const doSearch = () => {
        state.systemKeyword = systemKeyword?.value.trim() || '';
        state.systemAuthorId = systemAuthorId?.value.trim() || '';
        state.systemPage = 1;
        loadSystemAssetList();
    };
    if (systemSearchBtn) systemSearchBtn.addEventListener('click', doSearch);
    if (systemKeyword) systemKeyword.addEventListener('keydown', e => { if (e.key === 'Enter') doSearch(); });
    if (systemAuthorId) systemAuthorId.addEventListener('keydown', e => { if (e.key === 'Enter') doSearch(); });

    const addPriceBtn = document.getElementById('addPriceBtn');
    if (addPriceBtn) addPriceBtn.addEventListener('click', () => addPriceRow());

    const addLanguageSupportBtn = document.getElementById('addLanguageSupportBtn');
    if (addLanguageSupportBtn) {
        addLanguageSupportBtn.addEventListener('click', () => addLanguageSupportRow({ name: '', isSupported: true }));
    }

    const submitBtn = document.getElementById('submitAssetBtn');
    if (submitBtn) submitBtn.addEventListener('click', submitForm);

    const cancelBtn = document.getElementById('cancelEditBtn');
    if (cancelBtn) cancelBtn.addEventListener('click', () => { resetForm(); switchTab('my-assets'); });

    document.getElementById('closeModal')?.addEventListener('click', closeReviewModal);
    document.getElementById('modalApproveBtn')?.addEventListener('click', approveAsset);
    document.getElementById('modalRejectBtn')?.addEventListener('click', openRejectModal);

    document.getElementById('closeRejectModal')?.addEventListener('click', closeRejectModal);
    document.getElementById('cancelRejectBtn')?.addEventListener('click', closeRejectModal);
    document.getElementById('confirmRejectBtn')?.addEventListener('click', confirmReject);

    document.getElementById('closeSystemAssetModal')?.addEventListener('click', closeSystemAssetModal);
    document.getElementById('systemReturnBtn')?.addEventListener('click', returnSystemAsset);
    document.getElementById('systemOffShelfBtn')?.addEventListener('click', offShelfSystemAsset);
    document.getElementById('systemForceReviewBtn')?.addEventListener('click', forceReviewSystemAsset);
    document.getElementById('systemHardDeleteBtn')?.addEventListener('click', hardDeleteSystemAsset);

    document.getElementById('reviewModal')?.addEventListener('click', e => {
        if (e.target.id === 'reviewModal') closeReviewModal();
    });
    document.getElementById('rejectModal')?.addEventListener('click', e => {
        if (e.target.id === 'rejectModal') closeRejectModal();
    });
    document.getElementById('systemAssetModal')?.addEventListener('click', e => {
        if (e.target.id === 'systemAssetModal') closeSystemAssetModal();
    });
}

async function init() {
    if (window.initCustomSelects) window.initCustomSelects();
    if (window.initGlobalTopbar) window.initGlobalTopbar();
    if (window.initGlobalFooter) window.initGlobalFooter();
    if (window.initButtonEffects) window.initButtonEffects();

    const ok = await verifyUser();
    if (!ok) return;

    bindEvents();

    await Promise.all([loadMyAssets(), loadMyStats()]);
}
