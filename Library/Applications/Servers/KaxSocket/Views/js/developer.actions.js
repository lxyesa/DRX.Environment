/* ================================================================
 *  developer.actions.js — 用户交互操作层
 *  包含：switchTab、editAsset、submitForm、submitReview、
 *        publishAsset、审核操作（approve / reject）、
 *        系统操作（return / off-shelf / force-review / hard-delete）
 *        bindEvents、init
 *  依赖：developer.utils.js、developer.renderer.js、developer.api.js
 * ================================================================ */
'use strict';

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
            const updateResp = await apiUpdateAsset({ id: state.editingId, ...data });
            if (updateResp.code !== 0) {
                alert(updateResp.message || '保存失败');
                return;
            }
            const submitResp = await apiSubmitReview(state.editingId);
            if (submitResp.code === 0) {
                alert('资源已保存并提交重审，等待审核结果');
            } else if (submitResp.cooldownRemaining) {
                alert(`资源已保存，但提交冷却中，请在 ${formatCooldown(submitResp.cooldownRemaining)} 后手动重新提交`);
            } else {
                alert(`资源已保存，但提交审核失败：${submitResp.message || '未知错误'}`);
            }
        } else {
            const resp = await apiCreateAsset(data);
            if (resp.code !== 0) {
                alert(resp.message || '创建失败');
                return;
            }
            alert(resp.message || '操作成功');
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
