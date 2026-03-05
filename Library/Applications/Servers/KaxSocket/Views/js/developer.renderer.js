/* ================================================================
 *  developer.renderer.js — DOM 渲染层
 *  包含：state 对象、renderStats、renderMyAssets、renderPagination、
 *        渲染表单（addPriceRow、resetForm、fillForm、collectFormData）、
 *        renderReviewList、renderSystemAssetList、
 *        审核弹窗（openReviewModal、closeReviewModal）、
 *        系统弹窗（openSystemAssetModal、closeSystemAssetModal）、
 *        可视化编辑器（chips、image-list）、
 *        renderSystemFieldCards、saveSystemField
 * ================================================================ */
'use strict';

/* ----------------------------------------------------------------
   全局状态
   ---------------------------------------------------------------- */
const state = {
    // user
    isAdmin: false,
    isSystem: false,
    permissionGroup: 999,
    userName: '',

    // my assets
    myAssets: [],
    myTotal: 0,
    myPage: 1,
    myStatusFilter: '',
    myStats: { all: 0, pending: 0, rejected: 0, active: 0 },

    // review
    reviewList: [],
    reviewTotal: 0,
    reviewPage: 1,
    reviewStatusFilter: '0',

    // system asset management
    systemAssets: [],
    systemTotal: 0,
    systemPage: 1,
    systemStatusFilter: '',
    systemKeyword: '',
    systemAuthorId: '',
    currentSystemAssetId: 0,
    currentSystemAssetDetail: null,

    // editing
    editingId: 0,             // 0 = create mode
    currentReviewAssetId: 0
};

/* ----------------------------------------------------------------
   我的资源
   ---------------------------------------------------------------- */
function renderStats() {
    const s = state.myStats;
    const set = (id, v) => { const el = document.querySelector('#' + id + ' b'); if (el) el.textContent = v; };
    set('pillAll', s.all);
    set('pillPending', s.pending);
    set('pillRejected', s.rejected);
    set('pillActive', s.active);
}

function renderMyAssets() {
    const container = document.getElementById('assetList');
    const empty = document.getElementById('assetEmpty');
    const pag = document.getElementById('assetPagination');

    if (state.myAssets.length === 0) {
        container.innerHTML = '';
        empty.style.display = '';
        pag.innerHTML = '';
        return;
    }
    empty.style.display = 'none';

    container.innerHTML = state.myAssets.map(a => {
        const thumb = a.coverImage || a.iconImage || '';
        const thumbHtml = thumb
            ? `<img class="dev-asset-thumb" src="${escapeHtml(thumb)}" alt="" loading="lazy">`
            : `<div class="dev-asset-thumb" style="display:flex;align-items:center;justify-content:center;color:rgba(255,255,255,0.15);font-size:1.5rem;">📦</div>`;

        const categoryText = (a.category || '').trim() || '未分类';
        const versionText = (a.version || '1.0.0').trim();
        const updatedText = formatDate(a.lastUpdatedAt);
        const submittedText = formatDate(a.lastSubmittedAt);
        const descRaw = (a.description || '').trim();
        const descText = descRaw
            ? (descRaw.length > 96 ? `${descRaw.slice(0, 96)}...` : descRaw)
            : '暂无资源描述，建议补充核心亮点和使用场景，提升审核通过率与转化。';
        const tags = normalizeTags(a.tags).slice(0, 3);
        const tagsHtml = tags.map(t => `<span class="dev-asset-tag">${escapeHtml(t)}</span>`).join('');

        let actions = '';
        if (a.status === 1) {
            actions += `<button class="btn ghost small dev-asset-btn" onclick="DevApp.editAsset(${a.id})" title="编辑"><span class="material-icons">edit</span><span>编辑</span></button>`;
            actions += `<button class="btn ghost small dev-asset-btn" onclick="DevApp.submitReview(${a.id})" title="重新提交审核"><span class="material-icons">send</span><span>重新提交</span></button>`;
        } else if (a.status === 2) {
            actions += `<button class="btn ghost small dev-asset-btn" onclick="DevApp.editAsset(${a.id})" title="编辑"><span class="material-icons">edit</span><span>编辑</span></button>`;
            actions += `<button class="btn ghost small dev-asset-btn dev-action-publish" onclick="DevApp.publishAsset(${a.id})" title="发布上线"><span class="material-icons">publish</span><span>发布</span></button>`;
        } else if (a.status === 3) {
            actions += `<button class="btn ghost small dev-asset-btn" onclick="DevApp.editAsset(${a.id})" title="编辑"><span class="material-icons">edit</span><span>编辑</span></button>`;
        }

        if (!actions) {
            actions = `<span class="dev-asset-waiting"><span class="material-icons">hourglass_top</span>等待审核结果</span>`;
        }

        let rejectHtml = '';
        if (a.status === 1 && a.rejectReason) {
            rejectHtml = `<div class="dev-reject-reason"><b>拒绝原因：</b>${escapeHtml(a.rejectReason)}</div>`;
        }

        return `
        <div class="dev-asset-card">
            <div class="dev-asset-visual">
                ${thumbHtml}
                <div class="dev-asset-status-overlay">${statusBadge(a.status)}</div>
                <div class="dev-asset-visual-meta">
                    <span class="dev-asset-visual-chip">v${escapeHtml(versionText)}</span>
                    <span class="dev-asset-visual-chip">${escapeHtml(categoryText)}</span>
                </div>
            </div>
            <div class="dev-asset-main">
                <div class="dev-asset-head">
                    <div class="dev-asset-title-wrap">
                        <div class="dev-asset-name">${escapeHtml(a.name || '未命名资源')}</div>
                        <div class="dev-asset-subline">
                            <span class="dev-asset-version">版本 v${escapeHtml(versionText)}</span>
                            <span class="dev-asset-dot">•</span>
                            <span class="dev-asset-category">${escapeHtml(categoryText)}</span>
                        </div>
                    </div>
                    <div class="dev-asset-actions">${actions}</div>
                </div>
                <div class="dev-asset-desc">${escapeHtml(descText)}</div>
                <div class="dev-asset-meta-row">
                    <span class="dev-asset-chip"><span class="material-icons">update</span>更新于 ${updatedText}</span>
                    <span class="dev-asset-chip"><span class="material-icons">event</span>提交于 ${submittedText}</span>
                    ${tagsHtml}
                </div>
                <div class="dev-asset-metrics" aria-label="资源数据">
                    <div class="dev-metric"><span class="material-icons">visibility</span><b>${a.viewCount ?? 0}</b><em>浏览</em></div>
                    <div class="dev-metric"><span class="material-icons">download</span><b>${a.downloads ?? 0}</b><em>下载</em></div>
                    <div class="dev-metric"><span class="material-icons">fact_check</span><b>${STATUS_TEXT[a.status] || '未知'}</b><em>当前状态</em></div>
                </div>
                ${rejectHtml}
            </div>
        </div>`;
    }).join('');

    renderPagination(pag, state.myTotal, state.myPage, (p) => { state.myPage = p; loadMyAssets(); });
}

function renderPagination(container, total, currentPage, onPageChange) {
    const totalPages = Math.ceil(total / PAGE_SIZE);
    if (totalPages <= 1) { container.innerHTML = ''; return; }
    container.innerHTML = Array.from({ length: totalPages }, (_, i) => {
        const p = i + 1;
        return `<button class="dev-page-btn${p === currentPage ? ' active' : ''}" data-page="${p}">${p}</button>`;
    }).join('');
    container.querySelectorAll('.dev-page-btn').forEach(btn => {
        btn.addEventListener('click', () => onPageChange(parseInt(btn.dataset.page)));
    });
}

async function loadMyAssets() {
    try {
        const body = await apiGetMyAssets(state.myPage, state.myStatusFilter);
        if (body.code !== 0) { console.warn(body.message); return; }
        state.myAssets = body.data?.items ?? [];
        state.myTotal = body.data?.total ?? 0;
        renderMyAssets();
    } catch (e) {
        console.error(e);
    }
}

async function loadMyStats() {
    try {
        const [allBody, pendBody, rejBody, actBody] = await Promise.all([
            apiGetMyAssets(1, ''),
            apiGetMyAssets(1, '0'),
            apiGetMyAssets(1, '1'),
            apiGetMyAssets(1, '3')
        ]);
        state.myStats.all = allBody.data?.total ?? 0;
        state.myStats.pending = pendBody.data?.total ?? 0;
        state.myStats.rejected = rejBody.data?.total ?? 0;
        state.myStats.active = actBody.data?.total ?? 0;
        renderStats();
    } catch (e) {
        console.warn('加载统计失败', e);
    }
}

/* ----------------------------------------------------------------
   表单渲染
   ---------------------------------------------------------------- */
function addPriceRow(data) {
    const list = document.getElementById('pricesList');
    const div = document.createElement('div');
    div.className = 'dev-price-item';
    div.innerHTML = `
        <div class="dev-price-item-head">
            <span class="dev-price-chip">授权方案</span>
            <button class="dev-price-remove" type="button" title="删除此方案" aria-label="删除此价格方案"><span class="material-icons">delete</span></button>
        </div>
        <div class="dev-price-grid">
            <div class="dev-field full-width">
                <label>方案名称</label>
                <input type="text" class="price-label" placeholder="例如：月度授权 / 专业版" maxlength="50" value="${escapeHtml(data?.label || '')}">
            </div>
            <div class="dev-field">
                <label>现价</label>
                <div class="dev-input-affix">
                    <span>¥</span>
                    <input type="number" class="price-value" placeholder="0.00" min="0" step="0.01" value="${data?.price ?? ''}">
                </div>
            </div>
            <div class="dev-field">
                <label>原价（可选）</label>
                <div class="dev-input-affix">
                    <span>¥</span>
                    <input type="number" class="price-orig" placeholder="0.00" min="0" step="0.01" value="${data?.originalPrice ?? ''}">
                </div>
            </div>
            <div class="dev-field full-width">
                <label>时长与单位</label>
                <div style="display:grid;grid-template-columns:1fr 140px;gap:8px;">
                    <input type="number" class="price-duration" placeholder="例如：30（填 0 表示永久）" min="0" value="${data?.duration ?? data?.durationDays ?? ''}">
                    <select class="price-unit">
                        <option value="once" ${((data?.unit || (data?.durationDays > 0 ? 'day' : 'once')) === 'once') ? 'selected' : ''}>永久授权</option>
                        <option value="hour" ${((data?.unit || '') === 'hour') ? 'selected' : ''}>小时</option>
                        <option value="day" ${((data?.unit || (data?.durationDays > 0 ? 'day' : '')) === 'day') ? 'selected' : ''}>天</option>
                        <option value="month" ${((data?.unit || '') === 'month') ? 'selected' : ''}>月</option>
                        <option value="year" ${((data?.unit || '') === 'year') ? 'selected' : ''}>年</option>
                    </select>
                </div>
            </div>
        </div>
    `;
    div.querySelector('.dev-price-remove').addEventListener('click', () => div.remove());
    list.appendChild(div);
}

function resetForm() {
    state.editingId = 0;
    document.getElementById('formTitle').textContent = '提交新资源';
    document.getElementById('editAssetId').value = '';
    document.getElementById('assetName').value = '';
    document.getElementById('assetVersion').value = '';
    document.getElementById('assetCategory').value = '';
    document.getElementById('assetTags').value = '';
    document.getElementById('assetDescription').value = '';
    document.getElementById('assetCoverImage').value = '';
    document.getElementById('assetIconImage').value = '';
    document.getElementById('assetScreenshots').value = '';
    const badgesInput = document.getElementById('assetBadges');
    if (badgesInput) badgesInput.value = '';
    const featuresInput = document.getElementById('assetFeatures');
    if (featuresInput) featuresInput.value = '';
    document.getElementById('pricesList').innerHTML = '';
    document.getElementById('cancelEditBtn').style.display = 'none';
    document.getElementById('submitAssetBtn').textContent = '提交资源';
    const aside = document.getElementById('cancelEditBtnAside');
    if (aside) aside.style.display = 'none';
    const submitAside = document.getElementById('submitAssetBtnAside');
    if (submitAside) submitAside.querySelector('span.material-icons').nextSibling.textContent = '提交资源';
}

function fillForm(asset) {
    state.editingId = asset.id;
    document.getElementById('formTitle').textContent = `编辑资源 — ${asset.name}`;
    document.getElementById('editAssetId').value = asset.id;
    document.getElementById('assetName').value = asset.name || '';
    document.getElementById('assetVersion').value = asset.version || '';
    document.getElementById('assetCategory').value = asset.category || '';
    document.getElementById('assetTags').value = Array.isArray(asset.tags) ? asset.tags.join(',') : (asset.tags || '');
    document.getElementById('assetDescription').value = asset.description || '';
    document.getElementById('assetCoverImage').value = asset.coverImage || '';
    document.getElementById('assetIconImage').value = asset.iconImage || '';
    document.getElementById('assetScreenshots').value = Array.isArray(asset.screenshots) ? asset.screenshots.join(';') : (asset.screenshots || '');
    const badgesInput = document.getElementById('assetBadges');
    if (badgesInput) badgesInput.value = formatBadgesForEditor(asset.badges || '');
    const featuresInput = document.getElementById('assetFeatures');
    if (featuresInput) featuresInput.value = formatFeaturesForEditor(asset.features || '');
    document.getElementById('cancelEditBtn').style.display = '';
    document.getElementById('submitAssetBtn').textContent = '保存并重审';
    const aside = document.getElementById('cancelEditBtnAside');
    if (aside) aside.style.display = '';
    const submitAside = document.getElementById('submitAssetBtnAside');
    if (submitAside) submitAside.querySelector('span.material-icons').nextSibling.textContent = '保存并重审';

    const list = document.getElementById('pricesList');
    list.innerHTML = '';
    if (asset.prices && asset.prices.length > 0) {
        asset.prices.forEach(p => addPriceRow(p));
    }
}

function collectFormData() {
    const name = document.getElementById('assetName').value.trim();
    const version = document.getElementById('assetVersion').value.trim();
    const description = document.getElementById('assetDescription').value.trim();
    const category = document.getElementById('assetCategory').value.trim();
    const tags = document.getElementById('assetTags').value.trim();
    const coverImage = document.getElementById('assetCoverImage').value.trim();
    const iconImage = document.getElementById('assetIconImage').value.trim();
    const screenshots = document.getElementById('assetScreenshots').value.trim();
    const badgesEditorText = document.getElementById('assetBadges')?.value?.trim() || '';
    const featuresEditorText = document.getElementById('assetFeatures')?.value?.trim() || '';
    const badges = serializeBadgesFromEditor(badgesEditorText);
    const features = serializeFeaturesFromEditor(featuresEditorText);

    const priceItems = document.querySelectorAll('#pricesList .dev-price-item');
    const prices = [];
    priceItems.forEach(item => {
        const label = item.querySelector('.price-label').value.trim();
        const price = parseFloat(item.querySelector('.price-value').value) || 0;
        const originalPrice = parseFloat(item.querySelector('.price-orig').value) || 0;
        const duration = parseInt(item.querySelector('.price-duration').value) || 0;
        const unit = item.querySelector('.price-unit')?.value || (duration > 0 ? 'day' : 'once');
        const durationDays = (() => {
            if (duration <= 0 || unit === 'once') return 0;
            if (unit === 'day') return duration;
            if (unit === 'month') return duration * 30;
            if (unit === 'year') return duration * 365;
            if (unit === 'hour') return Math.ceil(duration / 24);
            return 0;
        })();
        prices.push({ label, price, originalPrice, unit, duration, durationDays });
    });

    return { name, version, description, category, tags, coverImage, iconImage, screenshots, badges, features, prices };
}

/* ----------------------------------------------------------------
   审核列表
   ---------------------------------------------------------------- */
function renderReviewList() {
    const container = document.getElementById('reviewList');
    const empty = document.getElementById('reviewEmpty');
    const pag = document.getElementById('reviewPagination');

    if (state.reviewList.length === 0) {
        container.innerHTML = '';
        empty.style.display = '';
        pag.innerHTML = '';
        return;
    }
    empty.style.display = 'none';

    container.innerHTML = state.reviewList.map(a => {
        const thumb = a.coverImage || a.iconImage || '';
        const thumbHtml = thumb
            ? `<img class="dev-review-thumb" src="${escapeHtml(thumb)}" alt="" loading="lazy">`
            : `<div class="dev-review-thumb" style="display:flex;align-items:center;justify-content:center;color:rgba(255,255,255,0.15);font-size:1.2rem;">📦</div>`;

        return `
        <div class="dev-review-card" onclick="DevApp.openReviewModal(${a.id})">
            ${thumbHtml}
            <div class="dev-review-info">
                <div class="dev-review-title">${escapeHtml(a.name)} <span style="color:rgba(255,255,255,0.3);font-size:0.78rem;margin-left:4px;">v${escapeHtml(a.version)}</span></div>
                <div class="dev-review-meta">
                    <span><span class="material-icons">person</span> ${escapeHtml(a.authorName)}</span>
                    <span><span class="material-icons">schedule</span> ${formatDateTime(a.lastSubmittedAt)}</span>
                    <span><span class="material-icons">category</span> ${escapeHtml(a.category) || '—'}</span>
                </div>
            </div>
            ${statusBadge(a.status)}
        </div>`;
    }).join('');

    renderPagination(pag, state.reviewTotal, state.reviewPage, (p) => { state.reviewPage = p; loadReviewList(); });
}

async function loadReviewList() {
    try {
        const body = await apiGetReviewList(state.reviewPage, state.reviewStatusFilter);
        if (body.code !== 0) { console.warn(body.message); return; }
        state.reviewList = body.data?.items ?? [];
        state.reviewTotal = body.data?.total ?? 0;
        renderReviewList();
    } catch (e) {
        console.error('加载审核列表失败', e);
    }
}

/* ----------------------------------------------------------------
   系统资产列表
   ---------------------------------------------------------------- */
function renderSystemAssetList() {
    const container = document.getElementById('systemAssetList');
    const empty = document.getElementById('systemAssetEmpty');
    const pag = document.getElementById('systemAssetPagination');
    if (!container || !empty || !pag) return;

    if (state.systemAssets.length === 0) {
        container.innerHTML = '';
        empty.style.display = '';
        pag.innerHTML = '';
        return;
    }
    empty.style.display = 'none';

    container.innerHTML = state.systemAssets.map(a => {
        const title = escapeHtml(a.name || '未命名资源');
        const version = escapeHtml(a.version || '—');
        const category = escapeHtml(a.category || '未分类');
        const descRaw = (a.description || '').trim();
        const desc = escapeHtml(descRaw.length > 120 ? `${descRaw.slice(0, 120)}...` : (descRaw || '暂无描述'));
        const thumb = a.coverImage || a.iconImage || '';
        const thumbHtml = thumb
            ? `<img class="dev-asset-thumb" src="${escapeHtml(thumb)}" alt="" loading="lazy">`
            : `<div class="dev-asset-thumb dev-asset-thumb-fallback"><span class="material-icons">inventory_2</span></div>`;
        const authorId = escapeHtml(String(a.authorId ?? '—'));
        const statusText = STATUS_TEXT[a.status] || '未知';
        const updatedText = formatDate(a.lastUpdatedAt);

        return `
            <div class="dev-asset-card dev-system-asset-card">
                <div class="dev-asset-visual dev-system-asset-visual">
                    ${thumbHtml}
                    <div class="dev-asset-status-overlay">${statusBadge(a.status)}</div>
                    <div class="dev-asset-visual-meta">
                        <span class="dev-asset-visual-chip">v${version}</span>
                        <span class="dev-asset-visual-chip">${category}</span>
                    </div>
                </div>
                <div class="dev-asset-main">
                    <div class="dev-asset-head">
                        <div class="dev-asset-title-wrap">
                            <div class="dev-asset-name">${title}</div>
                            <div class="dev-asset-subline">
                                <span class="dev-asset-version">版本 v${version}</span>
                                <span class="dev-asset-dot">•</span>
                                <span class="dev-asset-category">${category}</span>
                            </div>
                        </div>
                        <div class="dev-asset-actions">
                            <button class="btn ghost small dev-asset-btn" onclick="DevApp.openSystemAssetModal(${a.id})" type="button">
                                <span class="material-icons">visibility</span><span>详情 / 编辑</span>
                            </button>
                        </div>
                    </div>
                    <div class="dev-asset-desc">${desc}</div>
                    <div class="dev-asset-meta-row">
                        <span class="dev-asset-chip"><span class="material-icons">person</span>开发者ID: ${authorId}</span>
                        <span class="dev-asset-chip"><span class="material-icons">schedule</span>更新于 ${updatedText}</span>
                        <span class="dev-asset-chip"><span class="material-icons">category</span>${category}</span>
                    </div>
                    <div class="dev-asset-metrics" aria-label="系统资产数据">
                        <div class="dev-metric"><span class="material-icons">tag</span><b>#${a.id}</b><em>资产ID</em></div>
                        <div class="dev-metric"><span class="material-icons">person</span><b>${authorId}</b><em>开发者ID</em></div>
                        <div class="dev-metric"><span class="material-icons">fact_check</span><b>${statusText}</b><em>当前状态</em></div>
                    </div>
                </div>
            </div>`;
    }).join('');

    renderPagination(pag, state.systemTotal, state.systemPage, (p) => {
        state.systemPage = p;
        loadSystemAssetList();
    });
}

async function loadSystemAssetList() {
    if (!state.isSystem) return;
    try {
        const body = await apiGetSystemAssetList(
            state.systemPage,
            state.systemStatusFilter,
            state.systemKeyword,
            state.systemAuthorId
        );
        const list = Array.isArray(body.data) ? body.data : (body.data?.items ?? []);
        state.systemAssets = list;
        state.systemTotal = body.total ?? body.data?.total ?? list.length;
        renderSystemAssetList();
    } catch (e) {
        console.error('加载 system 资产列表失败', e);
        alert('加载资产管理列表失败');
    }
}

/* ----------------------------------------------------------------
   审核弹窗
   ---------------------------------------------------------------- */
function initModalTabs() {
    const tabs = document.querySelectorAll('#modalTabs .rv-tab');
    const panels = document.querySelectorAll('#modalBody .rv-panel');
    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            tabs.forEach(t => t.classList.toggle('active', t === tab));
            const target = tab.dataset.panel;
            panels.forEach(p => p.classList.toggle('active', p.dataset.panel === target));
        });
    });
}

async function openReviewModal(assetId) {
    state.currentReviewAssetId = assetId;
    const modal = document.getElementById('reviewModal');

    document.querySelectorAll('#modalTabs .rv-tab').forEach((t, i) => t.classList.toggle('active', i === 0));

    document.getElementById('modalInfoList').innerHTML = '';
    document.getElementById('modalBody').innerHTML = `
        <div class="rv-panel active" data-panel="overview">
            <div class="rv-empty"><span class="material-icons">hourglass_empty</span><p>加载中…</p></div>
        </div>
        <div class="rv-panel" data-panel="prices"></div>
        <div class="rv-panel" data-panel="screenshots"></div>
        <div class="rv-panel" data-panel="specs"></div>`;

    const headerIcon = document.getElementById('modalHeaderIcon');
    const modalThumb = document.getElementById('modalThumb');
    headerIcon.innerHTML = '<span class="material-icons">inventory_2</span>';
    modalThumb.innerHTML = '<span class="material-icons">image</span>';

    modal.style.display = '';
    initModalTabs();

    try {
        const resp = await apiGetReviewAssetDetail(assetId);
        if (resp.code !== 0) {
            document.getElementById('modalBody').innerHTML = `<div class="rv-panel active" data-panel="overview"><div class="rv-empty"><span class="material-icons">error_outline</span><p>${escapeHtml(resp.message)}</p></div></div><div class="rv-panel" data-panel="prices"></div><div class="rv-panel" data-panel="screenshots"></div><div class="rv-panel" data-panel="specs"></div>`;
            return;
        }

        const a = resp.data;
        document.getElementById('modalAssetName').textContent = a.name;
        document.getElementById('modalAssetSub').textContent = `v${a.version || '—'}  ·  ${a.category || '未分类'}`;
        document.getElementById('modalStatusBadge').innerHTML = statusBadge(a.status);

        const thumb = a.coverImage || a.iconImage || '';
        if (thumb) {
            headerIcon.innerHTML = `<img src="${escapeHtml(thumb)}" alt="">`;
        }

        const reviewActions = document.querySelector('.rv-footer-actions');
        if (reviewActions) reviewActions.style.display = a.status === 0 ? '' : 'none';

        document.getElementById('modalFooterAuthor').innerHTML =
            `<span class="material-icons">person</span> ${escapeHtml(a.authorName || '—')}  (ID: ${a.authorId ?? '—'})`;

        if (thumb) {
            modalThumb.innerHTML = `<img src="${escapeHtml(thumb)}" alt="">`;
        }

        const infoItems = [
            { label: 'ID', value: a.id },
            { label: '版本', value: a.version || '—' },
            { label: '分类', value: a.category || '—' },
            { label: '提交时间', value: formatDateTime(a.lastSubmittedAt) },
        ];

        let infoHtml = infoItems.map(it => `
            <div class="rv-info-item">
                <span class="rv-info-label">${it.label}</span>
                <span class="rv-info-value">${escapeHtml(String(it.value))}</span>
            </div>`).join('');

        const tags = Array.isArray(a.tags) ? a.tags : [];
        if (tags.length > 0) {
            infoHtml += `<div class="rv-info-item">
                <span class="rv-info-label">标签</span>
                <div class="rv-info-tags">${tags.map(t => `<span class="rv-info-tag">${escapeHtml(t)}</span>`).join('')}</div>
            </div>`;
        }
        document.getElementById('modalInfoList').innerHTML = infoHtml;

        // 概要
        let overviewHtml = '';
        if (a.status === 1 && a.rejectReason) {
            overviewHtml += `<div class="rv-reject-banner">
                <span class="material-icons">warning_amber</span>
                <p><b>拒绝原因：</b>${escapeHtml(a.rejectReason)}</p>
            </div>`;
        }
        overviewHtml += a.description
            ? `<div class="rv-overview-desc">${escapeHtml(a.description)}</div>`
            : `<div class="rv-empty"><span class="material-icons">article</span><p>暂无描述</p></div>`;

        // 价格
        let pricesHtml = '';
        if (a.prices && a.prices.length > 0) {
            pricesHtml = `<div class="rv-price-cards">` + a.prices.map(p => {
                const dur = p.durationDays > 0 ? `${p.durationDays} 天` : '永久授权';
                return `<div class="rv-price-card">
                    <div class="rv-price-card-icon"><span class="material-icons">sell</span></div>
                    <div class="rv-price-card-info">
                        <div class="rv-price-card-name">${escapeHtml(p.label || '默认方案')}</div>
                        <div class="rv-price-card-duration">${dur}</div>
                    </div>
                    <div class="rv-price-card-amount">
                        <div class="rv-price-current">¥${(p.price ?? 0).toFixed(2)}</div>
                        ${p.originalPrice > 0 ? `<div class="rv-price-original">¥${p.originalPrice.toFixed(2)}</div>` : ''}
                    </div>
                </div>`;
            }).join('') + `</div>`;
        } else {
            pricesHtml = `<div class="rv-empty"><span class="material-icons">price_change</span><p>未配置价格方案</p></div>`;
        }

        // 截图
        let screenshotsHtml = '';
        if (a.screenshots && a.screenshots.length > 0) {
            screenshotsHtml = `<div class="rv-screenshots">` +
                a.screenshots.map(s => `<div class="rv-screenshot-item"><img src="${escapeHtml(s)}" alt="" loading="lazy"></div>`).join('') +
                `</div>`;
        } else {
            screenshotsHtml = `<div class="rv-empty"><span class="material-icons">photo_library</span><p>暂无截图</p></div>`;
        }

        // 数据
        let specsHtml = '';
        if (a.specs) {
            const specFields = [
                { label: '下载量', icon: 'download', value: a.specs.downloads ?? 0 },
                { label: '浏览量', icon: 'visibility', value: a.specs.viewCount ?? 0 },
                { label: '购买量', icon: 'shopping_bag', value: a.specs.purchaseCount ?? 0 },
                { label: '收藏量', icon: 'favorite', value: a.specs.favoriteCount ?? 0 },
                { label: '评分', icon: 'star', value: a.specs.rating ?? 0 },
            ];
            specsHtml = `<div class="rv-specs-grid">` +
                specFields.map(f => `<div class="rv-spec-card">
                    <span class="rv-spec-label">${f.label}</span>
                    <span class="rv-spec-value">${f.value}</span>
                </div>`).join('') + `</div>`;
        } else {
            specsHtml = `<div class="rv-empty"><span class="material-icons">bar_chart</span><p>暂无数据</p></div>`;
        }

        document.getElementById('modalBody').innerHTML = `
            <div class="rv-panel active" data-panel="overview">${overviewHtml}</div>
            <div class="rv-panel" data-panel="prices">${pricesHtml}</div>
            <div class="rv-panel" data-panel="screenshots">${screenshotsHtml}</div>
            <div class="rv-panel" data-panel="specs">${specsHtml}</div>`;

        initModalTabs();

    } catch (e) {
        document.getElementById('modalBody').innerHTML = `<div class="rv-panel active" data-panel="overview"><div class="rv-empty"><span class="material-icons">error_outline</span><p>加载失败: ${escapeHtml(e.message)}</p></div></div><div class="rv-panel" data-panel="prices"></div><div class="rv-panel" data-panel="screenshots"></div><div class="rv-panel" data-panel="specs"></div>`;
    }
}

function closeReviewModal() {
    document.getElementById('reviewModal').style.display = 'none';
    state.currentReviewAssetId = 0;
}

/* ----------------------------------------------------------------
   系统资产详情弹窗
   ---------------------------------------------------------------- */
async function openSystemAssetModal(assetId) {
    state.currentSystemAssetId = assetId;
    state.currentSystemAssetDetail = null;
    const modal = document.getElementById('systemAssetModal');
    if (!modal) return;

    document.getElementById('systemAssetModalTitle').textContent = `资产 #${assetId}`;
    document.getElementById('systemAssetModalMeta').innerHTML = '<div class="rv-info-item"><span class="rv-info-label">状态</span><span class="rv-info-value">加载中...</span></div>';
    document.getElementById('systemAssetModalDesc').textContent = '';
    const fieldCards = document.getElementById('systemFieldCards');
    if (fieldCards) fieldCards.innerHTML = '<div class="system-field-loading">字段卡片加载中...</div>';
    const thumbBox = document.getElementById('systemAssetModalThumb');
    if (thumbBox) thumbBox.innerHTML = '<span class="material-icons">inventory_2</span>';
    document.getElementById('systemActionReason').value = '';
    modal.style.display = '';

    try {
        const resp = await apiGetSystemAssetDetail(assetId);
        if (!resp?.data) {
            alert(resp?.message || '加载详情失败');
            return;
        }

        const a = resp.data;
        state.currentSystemAssetDetail = a;
        document.getElementById('systemAssetModalTitle').textContent = `${a.name || '未命名资源'}（#${a.id}）`;

        const thumb = a.coverImage || a.iconImage || '';
        if (thumbBox) {
            thumbBox.innerHTML = thumb
                ? `<img src="${escapeHtml(thumb)}" alt="">`
                : '<span class="material-icons">inventory_2</span>';
        }

        document.getElementById('systemAssetModalMeta').innerHTML = `
            <div class="rv-info-item">
                <span class="rv-info-label">状态</span>
                <span class="rv-info-value">${statusBadge(a.status)}</span>
            </div>
            <div class="rv-info-item">
                <span class="rv-info-label">开发者ID</span>
                <span class="rv-info-value">${escapeHtml(String(a.authorId ?? '—'))}</span>
            </div>
            <div class="rv-info-item">
                <span class="rv-info-label">版本</span>
                <span class="rv-info-value">${escapeHtml(a.version || '—')}</span>
            </div>
            <div class="rv-info-item">
                <span class="rv-info-label">分类</span>
                <span class="rv-info-value">${escapeHtml(a.category || '未分类')}</span>
            </div>
            <div class="rv-info-item">
                <span class="rv-info-label">软删除</span>
                <span class="rv-info-value">${a.isDeleted ? '是' : '否'}</span>
            </div>`;
        document.getElementById('systemAssetModalDesc').textContent = a.description || '暂无描述';
        renderSystemFieldCards();
    } catch (e) {
        alert('加载详情失败: ' + e.message);
    }
}

function closeSystemAssetModal() {
    const modal = document.getElementById('systemAssetModal');
    if (modal) modal.style.display = 'none';
    state.currentSystemAssetId = 0;
    state.currentSystemAssetDetail = null;
}

/* ----------------------------------------------------------------
   可视化编辑器辅助函数
   ---------------------------------------------------------------- */
function collectVisualValue(fieldKey) {
    const card = document.querySelector(`[data-field-card="${fieldKey}"]`);
    if (!card) return '';
    const items = card.querySelectorAll('[data-visual-item]');
    return Array.from(items).map(el => el.dataset.visualItem).join(';');
}

function renderChipsEditor(fieldKey, items) {
    const chipsHtml = items.map((item, idx) =>
        `<span class="sfc-chip" data-visual-item="${escapeAttr(item)}">
            <span class="sfc-chip-text">${escapeHtml(item)}</span>
            <button class="sfc-chip-del" type="button" data-del-idx="${idx}" title="移除">
                <span class="material-icons">close</span>
            </button>
        </span>`
    ).join('');

    return `<div class="sfc-chips-editor" id="sfc-editor-${fieldKey}">
        <div class="sfc-chips-list" id="sfc-chips-${fieldKey}">${chipsHtml}</div>
        <div class="sfc-add-row">
            <input class="sfc-add-input system-field-input" type="text"
                placeholder="输入后按 Enter 或点击 ＋ 添加"
                id="sfc-addinput-${fieldKey}">
            <button class="sfc-add-btn" type="button" data-add-chips="${fieldKey}" title="添加">
                <span class="material-icons">add</span>
            </button>
        </div>
    </div>`;
}

function renderImageListEditor(fieldKey, items) {
    const itemsHtml = items.map((url, idx) =>
        `<div class="sfc-imgitem" data-visual-item="${escapeAttr(url)}">
            <img class="sfc-imgitem-thumb" src="${escapeAttr(url)}" alt="" loading="lazy"
                onerror="this.style.display='none';this.nextElementSibling.style.display='flex'">
            <div class="sfc-imgitem-fallback" style="display:none">
                <span class="material-icons">broken_image</span>
            </div>
            <div class="sfc-imgitem-url" title="${escapeHtml(url)}">${escapeHtml(url)}</div>
            <button class="sfc-imgitem-del" type="button" data-del-idx="${idx}" title="移除">
                <span class="material-icons">delete_outline</span>
            </button>
        </div>`
    ).join('');

    return `<div class="sfc-imglist-editor" id="sfc-editor-${fieldKey}">
        <div class="sfc-imglist" id="sfc-imglist-${fieldKey}">${itemsHtml}</div>
        <div class="sfc-add-row">
            <input class="sfc-add-input system-field-input" type="text"
                placeholder="粘贴图片 URL 后按 Enter 或点击 ＋"
                id="sfc-addinput-${fieldKey}">
            <button class="sfc-add-btn" type="button" data-add-imglist="${fieldKey}" title="添加">
                <span class="material-icons">add_photo_alternate</span>
            </button>
        </div>
    </div>`;
}

function rebuildDelIdx(container, itemSelector) {
    container.querySelectorAll(itemSelector).forEach((el, i) => {
        const btn = el.querySelector('[data-del-idx]');
        if (btn) btn.dataset.delIdx = i;
    });
}

function bindChipsEditorEvents(card, fieldKey) {
    const list = card.querySelector(`#sfc-chips-${fieldKey}`);
    const addInput = card.querySelector(`#sfc-addinput-${fieldKey}`);
    const addBtn = card.querySelector(`[data-add-chips="${fieldKey}"]`);

    function refreshChips() {
        list.querySelectorAll('[data-del-idx]').forEach(btn => {
            btn.addEventListener('click', () => {
                btn.closest('.sfc-chip')?.remove();
                rebuildDelIdx(list, '.sfc-chip');
            });
        });
    }

    function addItem() {
        const val = addInput.value.trim();
        if (!val) return;
        const chip = document.createElement('span');
        chip.className = 'sfc-chip';
        chip.dataset.visualItem = val;
        chip.innerHTML = `<span class="sfc-chip-text">${escapeHtml(val)}</span>
            <button class="sfc-chip-del" type="button" title="移除">
                <span class="material-icons">close</span>
            </button>`;
        chip.querySelector('.sfc-chip-del').addEventListener('click', () => {
            chip.remove();
            rebuildDelIdx(list, '.sfc-chip');
        });
        list.appendChild(chip);
        addInput.value = '';
    }

    addBtn.addEventListener('click', addItem);
    addInput.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); addItem(); } });
    refreshChips();
}

function bindImageListEditorEvents(card, fieldKey) {
    const list = card.querySelector(`#sfc-imglist-${fieldKey}`);
    const addInput = card.querySelector(`#sfc-addinput-${fieldKey}`);
    const addBtn = card.querySelector(`[data-add-imglist="${fieldKey}"]`);

    function refreshDelBtns() {
        list.querySelectorAll('[data-del-idx]').forEach(btn => {
            btn.addEventListener('click', () => {
                btn.closest('.sfc-imgitem').remove();
                rebuildDelIdx(list, '.sfc-imgitem');
            });
        });
    }

    function addItem() {
        const val = addInput.value.trim();
        if (!val) return;
        const item = document.createElement('div');
        item.className = 'sfc-imgitem';
        item.dataset.visualItem = val;
        item.innerHTML = `<img class="sfc-imgitem-thumb" src="${escapeAttr(val)}" alt="" loading="lazy"
                onerror="this.style.display='none';this.nextElementSibling.style.display='flex'">
            <div class="sfc-imgitem-fallback" style="display:none">
                <span class="material-icons">broken_image</span>
            </div>
            <div class="sfc-imgitem-url" title="${escapeHtml(val)}">${escapeHtml(val)}</div>
            <button class="sfc-imgitem-del" type="button" title="移除" data-del-idx="0">
                <span class="material-icons">delete_outline</span>
            </button>`;
        item.querySelector('.sfc-imgitem-del').addEventListener('click', () => {
            item.remove();
            rebuildDelIdx(list, '.sfc-imgitem');
        });
        list.appendChild(item);
        addInput.value = '';
    }

    addBtn.addEventListener('click', addItem);
    addInput.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); addItem(); } });
    refreshDelBtns();
}

function renderSystemFieldCards() {
    const detail = state.currentSystemAssetDetail;
    const container = document.getElementById('systemFieldCards');
    if (!container) return;

    if (!detail) {
        container.innerHTML = '<div class="system-field-loading">暂无可编辑字段</div>';
        return;
    }

    container.innerHTML = SYSTEM_FIELD_CONFIG.map(field => {
        const raw = getSystemFieldRawValue(detail, field.key);
        const value = normalizeSystemFieldValue(raw);
        const preview = formatSystemFieldPreview(raw);

        let editorHtml;
        if (field.visualType === 'chips') {
            editorHtml = renderChipsEditor(field.key, parseSemicolon(value));
        } else if (field.visualType === 'image-list') {
            editorHtml = renderImageListEditor(field.key, parseSemicolon(value));
        } else if (field.multiline) {
            editorHtml = `<textarea class="system-field-input system-field-input-multiline" id="systemFieldInput-${field.key}" data-field="${field.key}" rows="3" placeholder="请输入 ${escapeHtml(field.label)}">${escapeHtml(value)}</textarea>`;
        } else {
            editorHtml = `<input class="system-field-input" id="systemFieldInput-${field.key}" data-field="${field.key}" type="text" value="${escapeHtml(value)}" placeholder="请输入 ${escapeHtml(field.label)}">`;
        }

        return `
            <article class="system-field-card${field.visualType ? ' system-field-card--visual' : ''}" data-field-card="${field.key}">
                <div class="system-field-card-head">
                    <span class="system-field-card-name">${escapeHtml(field.label)}</span>
                </div>
                <p class="system-field-card-current" title="${escapeHtml(value)}">${escapeHtml(preview)}</p>
                ${editorHtml}
                <div class="system-field-card-actions">
                    <button class="btn small system-rv-save-btn" type="button" data-system-save-field="${field.key}">
                        <span class="material-icons">save</span>保存
                    </button>
                </div>
            </article>`;
    }).join('');

    SYSTEM_FIELD_CONFIG.forEach(field => {
        const card = container.querySelector(`[data-field-card="${field.key}"]`);
        if (!card) return;
        const raw = getSystemFieldRawValue(detail, field.key);
        const value = normalizeSystemFieldValue(raw);
        if (field.visualType === 'chips') {
            bindChipsEditorEvents(card, field.key, parseSemicolon(value));
        } else if (field.visualType === 'image-list') {
            bindImageListEditorEvents(card, field.key);
        }
    });

    container.querySelectorAll('[data-system-save-field]').forEach(btn => {
        btn.addEventListener('click', () => saveSystemField(btn.dataset.systemSaveField, btn));
    });
}

async function saveSystemField(field, triggerBtn = null) {
    const detail = state.currentSystemAssetDetail;
    const id = state.currentSystemAssetId;
    if (!id) return;
    if (!field) { alert('字段无效'); return; }
    if (triggerBtn?.dataset.loading === '1') return;

    const fieldCfg = SYSTEM_FIELD_CONFIG.find(f => f.key === field);
    let value;
    if (fieldCfg?.visualType) {
        value = collectVisualValue(field);
    } else {
        const input = document.getElementById(`systemFieldInput-${field}`);
        value = input?.value ?? '';
    }

    if (!detail) { alert('资产详情未加载，请稍后重试'); return; }

    const stopLoading = setButtonLoading(triggerBtn, '保存中...');
    try {
        const resp = await apiSystemUpdateField(id, field, value);
        if (resp.code === 0 || /已更新/.test(resp.message || '')) {
            alert(resp.message || '字段已更新');
            await Promise.all([loadSystemAssetList(), openSystemAssetModal(id)]);
        } else {
            alert(resp.message || '字段更新失败');
        }
    } catch (e) {
        alert('字段更新失败: ' + e.message);
    } finally {
        stopLoading();
    }
}
