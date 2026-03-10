/* 管理员域模块（Task 3 - 资产管理）
 * 目标：零行为改动承接 profile.js 中“管理员-资产”相关逻辑。
 * 暴露：window.ProfileAdmin
 */
(function registerProfileAdmin(global) {
    if (global.ProfileAdmin) return;

    const state = {
        adminAssetPage: 1,
        adminAssetTotalPages: 1,
        adminAssetPageSize: 20,
        cdkAdminPage: 1,
        cdkAdminTotalPages: 1,
        cdkAdminPageSize: 50,
        cdkAdminLastKeyword: '',
        cdkSelectedCodes: new Set(),
        cdkDeleteTargetCode: null,
        assetPricePlans: [],
        assetEditOriginalData: null,
        assetDeleteTargetId: null,
        deps: {
            checkToken: () => null,
            setElementDisplay: () => { },
            setElementsDisplay: () => { },
            withButtonLoading: async (_btn, _text, fn) => await fn(),
            escapeHtml: (v) => String(v ?? ''),
            updatePaginationButtons: () => { },
            formatUnix: (v) => String(v ?? '')
        },
        initialized: false
    };

    function calculateFinalPrice(originalPrice, discountRate) {
        const price = Math.max(0, (originalPrice || 0) * (1 - (discountRate || 0)));
        return Math.round(price * 100) / 100;
    }

    function normalizePricePlansForCompare(plans) {
        const list = Array.isArray(plans) ? plans : [];
        return list.map(p => ({
            id: p.id || '',
            price: Number(p.price || 0),
            originalPrice: Number(p.originalPrice || 0),
            discountRate: Number(p.discountRate || 0),
            duration: Number(p.duration || 0),
            unit: p.unit || '',
            stock: Number(p.stock || 0)
        }));
    }

    function buildSingleAssetFieldPatch(currentPayload, originalPayload) {
        if (!originalPayload) return null;

        const changed = [];
        const fields = ['name', 'version', 'category', 'description', 'downloadUrl', 'license', 'compatibility', 'fileSize'];
        fields.forEach(field => {
            const currentVal = currentPayload[field];
            const originalVal = originalPayload[field];
            if (String(currentVal ?? '') !== String(originalVal ?? '')) {
                changed.push({ field, value: currentVal });
            }
        });

        const currentPrices = JSON.stringify(normalizePricePlansForCompare(currentPayload.prices));
        const originalPrices = JSON.stringify(normalizePricePlansForCompare(originalPayload.prices));
        if (currentPrices !== originalPrices) {
            changed.push({ field: 'prices', value: currentPayload.prices });
        }

        if (changed.length !== 1) return null;
        return changed[0];
    }

    function renderAssetPricePlans() {
        const container = document.getElementById('assetPriceList');
        if (!container) return;
        if (state.assetPricePlans.length === 0) {
            container.innerHTML = '<div style="color:var(--muted-strong);font-size:0.85rem;padding:8px 0;">暂无价格方案，点击「添加」创建</div>';
            return;
        }

        const unitMap = { once: '一次性', hour: '小时', day: '天', month: '月', year: '年' };
        container.innerHTML = state.assetPricePlans.map((p, i) => {
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
                if (field === 'price') return;

                let val = el.value;
                if (['originalPrice', 'duration', 'stock'].includes(field)) val = parseInt(val) || 0;
                if (field === 'discountRate') val = parseFloat(val) || 0;
                state.assetPricePlans[idx][field] = val;

                if (['originalPrice', 'discountRate'].includes(field)) {
                    const plan = state.assetPricePlans[idx];
                    plan.price = calculateFinalPrice(plan.originalPrice, plan.discountRate);
                    renderAssetPricePlans();
                }
            });
        });

        container.querySelectorAll('.admin-price-remove').forEach(btn => {
            btn.addEventListener('click', () => {
                const idx = parseInt(btn.dataset.idx);
                state.assetPricePlans.splice(idx, 1);
                renderAssetPricePlans();
            });
        });
    }

    /**
     * 业务意图：加载管理员资产列表并保持原分页/搜索/软删除展示行为。
     * 异常边界：401 清 token 并跳转；非 2xx 显示空态错误文案；网络异常显示“网络错误”。
     * DOM/API 映射：
     * - API: GET /api/asset/admin/list?page&pageSize&includeDeleted&q
     * - DOM: adminAssetLoading/adminAssetEmpty/adminAssetList/adminAssetPager/adminAssetPageInfo
     */
    async function loadAdminAssets(page = 1) {
        const token = state.deps.checkToken();
        if (!token) return;

        const q = (document.getElementById('adminAssetSearch')?.value || '').trim();
        const includeDeleted = document.getElementById('adminAssetIncludeDeleted')?.checked ? 'true' : 'false';
        const loadingEl = document.getElementById('adminAssetLoading');
        const emptyEl = document.getElementById('adminAssetEmpty');
        const listEl = document.getElementById('adminAssetList');
        const pagerEl = document.getElementById('adminAssetPager');
        const pageInfoEl = document.getElementById('adminAssetPageInfo');

        state.deps.setElementsDisplay({ 'adminAssetLoading': true, 'adminAssetEmpty': false });
        listEl.innerHTML = '';
        state.deps.setElementDisplay(pagerEl, false);

        try {
            const params = new URLSearchParams({ page, pageSize: state.adminAssetPageSize, includeDeleted });
            if (q) params.append('q', q);
            const resp = await ApiClient.request('/api/asset/admin/list?' + params);
            if (resp.status === 401) { return; }
            if (!resp.ok) {
                state.deps.setElementsDisplay({ 'adminAssetLoading': false, 'adminAssetEmpty': true });
                emptyEl.querySelector('span:last-child').textContent = '加载失败';
                return;
            }

            const result = await resp.json().catch(() => ({}));
            const items = result.data || [];
            const total = result.total || 0;
            state.adminAssetPage = page;
            state.adminAssetTotalPages = Math.max(1, Math.ceil(total / state.adminAssetPageSize));

            state.deps.setElementDisplay(loadingEl, false);
            if (items.length === 0) {
                state.deps.setElementDisplay(emptyEl, true);
                emptyEl.querySelector('span:last-child').textContent = '暂无资产';
                return;
            }

            state.deps.setElementDisplay(emptyEl, false);
            listEl.innerHTML = items.map(a => `
            <div class="admin-list-item ${a.isDeleted ? 'admin-list-item--deleted' : ''}">
                <div class="admin-list-item-info">
                    <div class="admin-list-item-name">${state.deps.escapeHtml(a.name)} <span class="admin-list-item-badge">${state.deps.escapeHtml(a.version)}</span>${a.isDeleted ? '<span class="admin-list-item-badge danger">已删除</span>' : ''}</div>
                    <div class="admin-list-item-meta">作者: ${state.deps.escapeHtml(a.author)} · ID: ${a.id}</div>
                </div>
                <div class="admin-list-item-actions">
                    <button class="asset-action-btn" onclick="openAssetEditModal(${a.id})">
                        <span class="material-icons">edit</span>编辑
                    </button>
                    ${a.isDeleted
                    ? `<button class="asset-action-btn" onclick="restoreAdminAsset(${a.id})"><span class="material-icons">restore</span>恢复</button>`
                    : `<button class="asset-action-btn danger" onclick="openAssetDeleteModal(${a.id}, '${state.deps.escapeHtml(a.name)}')"><span class="material-icons">delete</span>删除</button>`}
                </div>
            </div>
        `).join('');

            if (state.adminAssetTotalPages > 1) {
                state.deps.setElementDisplay(pagerEl, true);
                pageInfoEl.textContent = `第 ${state.adminAssetPage} / ${state.adminAssetTotalPages} 页，共 ${total} 条`;
                state.deps.updatePaginationButtons(state.adminAssetPage, state.adminAssetTotalPages, 'adminAssetPrevBtn', 'adminAssetNextBtn');
            }
        } catch (err) {
            console.error('加载资产列表失败:', err);
            state.deps.setElementsDisplay({ 'adminAssetLoading': false, 'adminAssetEmpty': true });
            emptyEl.querySelector('span:last-child').textContent = '网络错误';
        }
    }

    async function openAssetEditModal(assetId) {
        const modal = document.getElementById('assetEditModal');
        const titleEl = document.getElementById('assetEditModalTitle');
        const msgEl = document.getElementById('assetEditMsg');
        const idInput = document.getElementById('assetEditId');

        state.deps.setElementDisplay(msgEl, false);
        state.assetPricePlans = [];

        if (!assetId) {
            state.assetEditOriginalData = null;
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

        const token = state.deps.checkToken();
        if (!token) return;

        try {
            const resp = await ApiClient.request('/api/asset/admin/inspect', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
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

            state.assetPricePlans = (d.prices || []).map(p => {
                const originalPrice = p.originalPrice ?? 0;
                const discountRate = p.discountRate ?? 0;
                return {
                    id: p.id,
                    price: calculateFinalPrice(originalPrice, discountRate),
                    originalPrice,
                    discountRate,
                    duration: p.duration ?? 1,
                    unit: p.unit ?? 'month',
                    stock: p.stock ?? -1
                };
            });

            state.assetEditOriginalData = {
                name: d.name ?? '',
                version: d.version ?? '',
                category: d.category ?? '',
                description: d.description ?? '',
                downloadUrl: d.downloadUrl || d.specs?.downloadUrl || '',
                license: d.license || d.specs?.license || '',
                compatibility: d.compatibility || d.specs?.compatibility || '',
                fileSize: parseInt(String(d.fileSize || d.specs?.fileSize || 0), 10) || 0,
                prices: JSON.parse(JSON.stringify(state.assetPricePlans || []))
            };

            renderAssetPricePlans();
            modal.classList.add('show');
        } catch (err) {
            console.error('加载资产详情失败:', err);
            alert('加载失败');
        }
    }

    function openAssetDeleteModal(id, name) {
        state.assetDeleteTargetId = id;
        document.getElementById('assetDeleteName').textContent = name;
        document.getElementById('assetDeleteModal').classList.add('show');
    }

    async function restoreAdminAsset(id) {
        if (!confirm('确认恢复此资产？')) return;
        const token = state.deps.checkToken();
        if (!token) return;
        try {
            const resp = await ApiClient.request('/api/asset/admin/restore', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id })
            });
            const result = await resp.json().catch(() => ({}));
            if (resp.ok) { loadAdminAssets(state.adminAssetPage); }
            else { alert(result.message || '恢复失败'); }
        } catch (err) { alert('网络错误'); }
    }

    function bindAssetEvents() {
        document.getElementById('adminAssetSearchBtn')?.addEventListener('click', () => loadAdminAssets(1));
        document.getElementById('adminAssetSearch')?.addEventListener('keypress', e => { if (e.key === 'Enter') loadAdminAssets(1); });
        document.getElementById('adminAssetIncludeDeleted')?.addEventListener('change', () => loadAdminAssets(1));
        document.getElementById('adminAssetPrevBtn')?.addEventListener('click', () => { if (state.adminAssetPage > 1) loadAdminAssets(state.adminAssetPage - 1); });
        document.getElementById('adminAssetNextBtn')?.addEventListener('click', () => { if (state.adminAssetPage < state.adminAssetTotalPages) loadAdminAssets(state.adminAssetPage + 1); });
        document.getElementById('adminCreateAssetBtn')?.addEventListener('click', () => openAssetEditModal(null));

        document.getElementById('assetAddPriceBtn')?.addEventListener('click', () => {
            state.assetPricePlans.push({ price: 0, originalPrice: 0, discountRate: 0, duration: 1, unit: 'month', stock: -1 });
            renderAssetPricePlans();
        });

        document.getElementById('assetEditCancelBtn')?.addEventListener('click', () => {
            document.getElementById('assetEditModal').classList.remove('show');
        });

        document.getElementById('assetEditSaveBtn')?.addEventListener('click', async () => {
            const token = state.deps.checkToken();
            if (!token) return;

            const id = document.getElementById('assetEditId').value;
            const name = document.getElementById('assetEditName')?.value?.trim() || '';
            const version = document.getElementById('assetEditVersion')?.value?.trim() || '';
            const category = document.getElementById('assetEditCategory')?.value?.trim() || '';
            const description = document.getElementById('assetEditDesc')?.value?.trim() || '';
            const downloadUrl = document.getElementById('assetEditDownloadUrl')?.value?.trim() || '';
            const license = document.getElementById('assetEditLicense')?.value?.trim() || '';
            const compatibility = document.getElementById('assetEditCompatibility')?.value?.trim() || '';
            const fileSize = parseInt(document.getElementById('assetEditFileSize')?.value || '0') || 0;
            const msgEl = document.getElementById('assetEditMsg');

            const showMsg = (text, ok) => {
                if (typeof state.deps.showErrorMsg === 'function') state.deps.showErrorMsg(msgEl, text, !ok);
                else {
                    msgEl.style.display = 'block';
                    msgEl.textContent = text;
                }
            };

            if (!name) { showMsg('请填写资产名称', false); return; }
            if (!version) { showMsg('请填写版本', false); return; }

            const saveBtn = document.getElementById('assetEditSaveBtn');
            await state.deps.withButtonLoading(saveBtn, '保存中…', async () => {
                try {
                    const isEdit = !!id;
                    let url = isEdit ? '/api/asset/admin/update' : '/api/asset/admin/create';
                    const payload = { name, version, category, description, downloadUrl, license, compatibility, fileSize, prices: state.assetPricePlans };
                    if (isEdit) payload.id = parseInt(id);

                    let finalPayload = payload;
                    if (isEdit) {
                        const singlePatch = buildSingleAssetFieldPatch(payload, state.assetEditOriginalData);
                        if (singlePatch) {
                            url = '/api/asset/admin/update-field';
                            finalPayload = { id: parseInt(id), field: singlePatch.field, value: singlePatch.value };
                        }
                    }

                    const resp = await ApiClient.request(url, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(finalPayload)
                    });
                    const result = await resp.json().catch(() => ({}));
                    if (resp.ok) {
                        showMsg(result.message || (isEdit ? '已更新' : '已创建'), true);

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

                        setTimeout(() => {
                            document.getElementById('assetEditModal').classList.remove('show');
                            loadAdminAssets(state.adminAssetPage);
                        }, 1000);
                    } else {
                        showMsg(result.message || ('保存失败: ' + resp.status), false);
                    }
                } catch (err) {
                    showMsg('网络错误', false);
                }
            });
        });

        document.getElementById('assetDeleteCancelBtn')?.addEventListener('click', () => {
            document.getElementById('assetDeleteModal').classList.remove('show');
            state.assetDeleteTargetId = null;
        });

        document.getElementById('assetDeleteConfirmBtn')?.addEventListener('click', async () => {
            if (!state.assetDeleteTargetId) return;
            const token = state.deps.checkToken();
            if (!token) return;
            const btn = document.getElementById('assetDeleteConfirmBtn');

            await state.deps.withButtonLoading(btn, '删除中...', async () => {
                try {
                    const resp = await ApiClient.request('/api/asset/admin/delete', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ id: state.assetDeleteTargetId })
                    });
                    const result = await resp.json().catch(() => ({}));
                    if (resp.ok) {
                        document.getElementById('assetDeleteModal').classList.remove('show');
                        state.assetDeleteTargetId = null;
                        loadAdminAssets(state.adminAssetPage);
                    } else {
                        alert(result.message || '删除失败');
                    }
                } catch (err) {
                    alert('网络错误');
                }
            });
        });
    }

    function formatCdkExpire(s) {
        if (!s || s === 0) return '永久';
        if (s < 3600) return `${s}秒`;
        if (s < 86400) return `${(s / 3600).toFixed(1)}小时`;
        return `${Math.floor(s / 86400)}天`;
    }

    function updateCdkBatchBar() {
        const count = state.cdkSelectedCodes.size;
        const countEl = document.getElementById('cdkBatchCount');
        if (countEl) countEl.textContent = count > 0 ? `已选 ${count} 项` : '已选 0 项';
        const batchDeleteBtn = document.getElementById('cdkBatchDeleteBtn');
        const batchCopyBtn = document.getElementById('cdkBatchCopyBtn');
        if (batchDeleteBtn) batchDeleteBtn.disabled = count === 0;
        if (batchCopyBtn) batchCopyBtn.disabled = count === 0;
    }

    async function copyCdkCode(code) {
        try {
            await navigator.clipboard.writeText(code);
            const row = document.querySelector(`.admin-list-item[data-code="${CSS.escape(code)}"]`);
            const copyBtn = row?.querySelector('.asset-action-btn:not(.danger)');
            if (copyBtn) {
                const orig = copyBtn.innerHTML;
                copyBtn.innerHTML = '<span class="material-icons">check</span>已复制';
                setTimeout(() => { copyBtn.innerHTML = orig; }, 1500);
            }
        } catch (e) {
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
                params.append('keyword', keyword);
                params.append('searchIn', searchIn);
                url = '/api/cdk/admin/search?' + params;
            } else {
                url = '/api/cdk/admin/list?' + params;
            }
            const resp = await ApiClient.request(url);
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

    /**
     * 业务意图：加载 CDK 列表并保持搜索、分页、批量选择与单项操作行为一致。
     * 异常边界：401 失效跳转登录；非 2xx 进入失败空态；网络异常显示统一错误。
     * DOM/API 映射：
     * - API: GET /api/cdk/admin/list, GET /api/cdk/admin/search
     * - DOM: cdkAdminLoading/cdkAdminEmpty/cdkAdminList/cdkAdminPager/cdkBatchBar
     */
    async function loadAdminCdks(page = 1) {
        const token = state.deps.checkToken();
        if (!token) return;

        const keyword = (document.getElementById('cdkAdminSearch')?.value || '').trim();
        const searchIn = document.getElementById('cdkAdminSearchIn')?.value || 'all';
        const loadingEl = document.getElementById('cdkAdminLoading');
        const emptyEl = document.getElementById('cdkAdminEmpty');
        const listEl = document.getElementById('cdkAdminList');
        const pagerEl = document.getElementById('cdkAdminPager');
        const pageInfoEl = document.getElementById('cdkAdminPageInfo');

        state.deps.setElementsDisplay({ 'cdkAdminLoading': true, 'cdkAdminEmpty': false });
        listEl.innerHTML = '';
        state.deps.setElementDisplay(pagerEl, false);
        state.cdkAdminLastKeyword = keyword;

        try {
            const isSearch = !!keyword;
            const params = new URLSearchParams({ page, pageSize: state.cdkAdminPageSize });
            if (isSearch) { params.append('keyword', keyword); params.append('searchIn', searchIn); }
            const url = isSearch ? '/api/cdk/admin/search?' + params : '/api/cdk/admin/list?' + params;
            const resp = await ApiClient.request(url);
            if (resp.status === 401) { return; }
            if (!resp.ok) {
                state.deps.setElementsDisplay({ 'cdkAdminLoading': false, 'cdkAdminEmpty': true });
                emptyEl.querySelector('span:last-child').textContent = '加载失败';
                return;
            }

            const result = await resp.json().catch(() => ({}));
            const items = result.data || [];
            const total = result.total || 0;
            state.cdkAdminPage = page;
            state.cdkAdminTotalPages = Math.max(1, Math.ceil(total / state.cdkAdminPageSize));

            state.deps.setElementDisplay(loadingEl, false);
            if (items.length === 0) {
                state.deps.setElementDisplay(emptyEl, true);
                emptyEl.querySelector('span:last-child').textContent = '暂无 CDK';
                state.deps.setElementDisplay(document.getElementById('cdkBatchBar'), false);
                return;
            }

            state.deps.setElementDisplay(emptyEl, false);
            state.deps.setElementDisplay(document.getElementById('cdkBatchBar'), true);
            const selectAllCb = document.getElementById('cdkSelectAll');
            if (selectAllCb) selectAllCb.checked = false;

            listEl.innerHTML = items.map(c => {
                const isChecked = state.cdkSelectedCodes.has(c.code);
                return `
            <div class="admin-list-item${isChecked ? ' cdk-selected' : ''}" data-code="${state.deps.escapeHtml(c.code)}">
                <label class="cdk-select-all-label" style="flex-shrink:0;margin-right:4px;" title="选择此 CDK">
                    <input type="checkbox" class="cdk-checkbox cdk-item-cb" data-code="${state.deps.escapeHtml(c.code)}"${isChecked ? ' checked' : ''}>
                </label>
                <div class="admin-list-item-info">
                    <div class="admin-list-item-name" style="font-family:monospace;">${state.deps.escapeHtml(c.code)}
                        ${c.isUsed ? '<span class="admin-list-item-badge">已使用</span>' : '<span class="admin-list-item-badge success">未使用</span>'}
                        ${c.goldValue > 0 ? `<span class="admin-list-item-badge gold">💰 ${c.goldValue}</span>` : ''}
                    </div>
                    <div class="admin-list-item-meta">
                        有效期: ${formatCdkExpire(c.expiresInSeconds)}
                        ${c.description ? ` · ${state.deps.escapeHtml(c.description)}` : ''}
                        · 创建于 ${state.deps.formatUnix(c.createdAt)}
                        ${c.createdBy ? ` by ${state.deps.escapeHtml(c.createdBy)}` : ''}
                        ${c.isUsed ? ` · 使用者: ${state.deps.escapeHtml(c.usedBy || '—')} @ ${state.deps.formatUnix(c.usedAt)}` : ''}
                    </div>
                </div>
                <div class="admin-list-item-actions">
                    <button class="asset-action-btn" onclick="copyCdkCode('${state.deps.escapeHtml(c.code)}')" title="复制代码">
                        <span class="material-icons">content_copy</span>复制
                    </button>
                    <button class="asset-action-btn danger" onclick="openCdkDeleteModal('${state.deps.escapeHtml(c.code)}')">
                        <span class="material-icons">delete</span>删除
                    </button>
                </div>
            </div>`;
            }).join('');

            listEl.querySelectorAll('.cdk-item-cb').forEach(cb => {
                cb.addEventListener('change', () => {
                    const code = cb.dataset.code;
                    const row = cb.closest('.admin-list-item');
                    if (cb.checked) {
                        state.cdkSelectedCodes.add(code);
                        row?.classList.add('cdk-selected');
                    } else {
                        state.cdkSelectedCodes.delete(code);
                        row?.classList.remove('cdk-selected');
                    }
                    updateCdkBatchBar();
                });
            });

            if (state.cdkAdminTotalPages > 1) {
                state.deps.setElementDisplay(pagerEl, true);
                pageInfoEl.textContent = `第 ${state.cdkAdminPage} / ${state.cdkAdminTotalPages} 页，共 ${total} 条`;
                state.deps.updatePaginationButtons(state.cdkAdminPage, state.cdkAdminTotalPages, 'cdkAdminPrevBtn', 'cdkAdminNextBtn');
            }
            updateCdkBatchBar();
        } catch (err) {
            console.error('加载 CDK 列表失败:', err);
            state.deps.setElementsDisplay({ 'cdkAdminLoading': false, 'cdkAdminEmpty': true });
            emptyEl.querySelector('span:last-child').textContent = '网络错误';
        }
    }

    function openCdkDeleteModal(code) {
        state.cdkDeleteTargetCode = code;
        document.getElementById('cdkDeleteCode').textContent = code;
        document.getElementById('cdkDeleteModal').classList.add('show');
    }

    function bindCdkEvents() {
        document.getElementById('cdkAdminSearchBtn')?.addEventListener('click', () => loadAdminCdks(1));
        document.getElementById('cdkAdminSearch')?.addEventListener('keypress', e => { if (e.key === 'Enter') loadAdminCdks(1); });
        document.getElementById('cdkAdminPrevBtn')?.addEventListener('click', () => { if (state.cdkAdminPage > 1) loadAdminCdks(state.cdkAdminPage - 1); });
        document.getElementById('cdkAdminNextBtn')?.addEventListener('click', () => { if (state.cdkAdminPage < state.cdkAdminTotalPages) loadAdminCdks(state.cdkAdminPage + 1); });

        document.getElementById('cdkSelectAll')?.addEventListener('change', function () {
            const checked = this.checked;
            document.querySelectorAll('#cdkAdminList .cdk-item-cb').forEach(cb => {
                cb.checked = checked;
                const code = cb.dataset.code;
                const row = cb.closest('.admin-list-item');
                if (checked) {
                    state.cdkSelectedCodes.add(code);
                    row?.classList.add('cdk-selected');
                } else {
                    state.cdkSelectedCodes.delete(code);
                    row?.classList.remove('cdk-selected');
                }
            });
            updateCdkBatchBar();
        });

        document.getElementById('cdkBatchCopyBtn')?.addEventListener('click', async () => {
            if (state.cdkSelectedCodes.size === 0) return;
            const text = [...state.cdkSelectedCodes].join('\n');
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

        document.getElementById('cdkBatchDeleteBtn')?.addEventListener('click', () => {
            if (state.cdkSelectedCodes.size === 0) return;
            document.getElementById('cdkBatchDeleteCount').textContent = state.cdkSelectedCodes.size;
            document.getElementById('cdkBatchDeleteModal').classList.add('show');
        });

        document.getElementById('cdkBatchDeleteCancelBtn')?.addEventListener('click', () => {
            document.getElementById('cdkBatchDeleteModal').classList.remove('show');
        });

        document.getElementById('cdkBatchDeleteConfirmBtn')?.addEventListener('click', async () => {
            if (state.cdkSelectedCodes.size === 0) return;
            const token = state.deps.checkToken();
            if (!token) return;
            const btn = document.getElementById('cdkBatchDeleteConfirmBtn');
            await state.deps.withButtonLoading(btn, '删除中…', async () => {
                try {
                    const resp = await ApiClient.request('/api/cdk/admin/delete', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ codes: [...state.cdkSelectedCodes] })
                    });
                    const result = await resp.json().catch(() => ({}));
                    if (resp.ok) {
                        document.getElementById('cdkBatchDeleteModal').classList.remove('show');
                        state.cdkSelectedCodes.clear();
                        loadAdminCdks(state.cdkAdminPage);
                    } else {
                        alert(result.message || '批量删除失败');
                    }
                } catch (err) {
                    alert('网络错误');
                }
            });
        });

        document.getElementById('cdkRuleDeleteBtn')?.addEventListener('click', () => {
            document.getElementById('cdkRulePreviewMsg').style.display = 'none';
            document.getElementById('cdkRuleDeleteModal').classList.add('show');
        });

        document.getElementById('cdkRuleDeleteCancelBtn')?.addEventListener('click', () => {
            document.getElementById('cdkRuleDeleteModal').classList.remove('show');
        });

        document.getElementById('cdkRulePreviewCountBtn')?.addEventListener('click', async () => {
            const token = state.deps.checkToken();
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

        document.getElementById('cdkRuleDeleteConfirmBtn')?.addEventListener('click', async () => {
            const token = state.deps.checkToken();
            if (!token) return;
            const scope = document.getElementById('cdkRuleScope')?.value || 'used';
            const scopeLabels = { used: '所有已使用', unused: '所有未使用', search: '当前搜索结果', all: '全部' };
            if (!confirm(`确认删除"${scopeLabels[scope] || scope}"的 CDK？此操作不可撤销。`)) return;

            const btn = document.getElementById('cdkRuleDeleteConfirmBtn');
            const msgEl = document.getElementById('cdkRulePreviewMsg');
            msgEl.style.display = '';
            msgEl.className = 'admin-msg';
            msgEl.textContent = '正在查询符合条件的 CDK…';

            await state.deps.withButtonLoading(btn, '删除中…', async () => {
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

                    const batchSize = 50;
                    let removed = 0;
                    for (let i = 0; i < codes.length; i += batchSize) {
                        const batch = codes.slice(i, i + batchSize);
                        const resp = await ApiClient.request('/api/cdk/admin/delete', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ codes: batch })
                        });
                        const result = await resp.json().catch(() => ({}));
                        if (resp.ok) removed += (result.removed || 0);
                    }
                    msgEl.className = 'admin-msg admin-msg--ok';
                    msgEl.textContent = `删除完成，共删除 ${removed} 个 CDK`;
                    setTimeout(() => {
                        document.getElementById('cdkRuleDeleteModal').classList.remove('show');
                        state.cdkSelectedCodes.clear();
                        loadAdminCdks(1);
                    }, 1200);
                } catch (err) {
                    msgEl.className = 'admin-msg admin-msg--err';
                    msgEl.textContent = '网络错误：' + err.message;
                }
            });
        });

        document.getElementById('cdkAdminPreviewBtn')?.addEventListener('click', async () => {
            const token = state.deps.checkToken();
            if (!token) return;
            const count = Math.min(1000, Math.max(1, parseInt(document.getElementById('cdkAdminCount')?.value || '1') || 1));
            const length = Math.min(256, Math.max(4, parseInt(document.getElementById('cdkAdminLength')?.value || '16') || 16));
            const prefix = document.getElementById('cdkAdminPrefix')?.value?.trim() || '';
            const previewEl = document.getElementById('cdkAdminPreview');
            const msgEl = document.getElementById('cdkAdminGenMsg');

            try {
                const resp = await ApiClient.request('/api/cdk/admin/generate', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ count: Math.min(count, 20), length, prefix })
                });
                const result = await resp.json().catch(() => ({}));
                if (resp.ok && result.codes) {
                    const preview = result.codes.slice(0, 20).join('\n') + (count > 20 ? `\n…（共 ${count} 个）` : '');
                    state.deps.setElementDisplay(previewEl, true);
                    previewEl.textContent = preview;
                    state.deps.setElementDisplay(msgEl, false);
                } else {
                    state.deps.setElementDisplay(msgEl, true);
                    msgEl.className = 'admin-msg admin-msg--err';
                    msgEl.textContent = result.message || '预览失败';
                }
            } catch (err) { alert('网络错误'); }
        });

        document.getElementById('cdkAdminSaveBtn')?.addEventListener('click', async () => {
            const token = state.deps.checkToken();
            if (!token) return;
            const count = Math.min(1000, Math.max(1, parseInt(document.getElementById('cdkAdminCount')?.value || '1') || 1));
            const length = Math.min(256, Math.max(4, parseInt(document.getElementById('cdkAdminLength')?.value || '16') || 16));
            const goldValue = Math.max(0, parseInt(document.getElementById('cdkAdminGold')?.value || '0') || 0);
            const expiresInSeconds = Math.max(0, parseInt(document.getElementById('cdkAdminExpires')?.value || '0') || 0);
            const prefix = document.getElementById('cdkAdminPrefix')?.value?.trim() || '';
            const description = document.getElementById('cdkAdminDesc')?.value?.trim() || '';
            const msgEl = document.getElementById('cdkAdminGenMsg');
            const saveBtn = document.getElementById('cdkAdminSaveBtn');

            await state.deps.withButtonLoading(saveBtn, '生成中…', async () => {
                state.deps.setElementDisplay(msgEl, false);
                try {
                    const resp = await ApiClient.request('/api/cdk/admin/save', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ count, length, prefix, goldValue, expiresInSeconds, description })
                    });
                    const result = await resp.json().catch(() => ({}));
                    state.deps.setElementDisplay(msgEl, true);
                    if (resp.ok) {
                        msgEl.className = 'admin-msg admin-msg--ok';
                        msgEl.textContent = result.message || `已成功生成 ${result.count || count} 个 CDK`;
                        state.deps.setElementDisplay(document.getElementById('cdkAdminPreview'), false);
                        loadAdminCdks(1);
                    } else {
                        msgEl.className = 'admin-msg admin-msg--err';
                        msgEl.textContent = result.message || ('生成失败: ' + resp.status);
                    }
                } catch (err) {
                    state.deps.setElementDisplay(msgEl, true);
                    msgEl.className = 'admin-msg admin-msg--err';
                    msgEl.textContent = '网络错误';
                }
            });
        });

        document.getElementById('cdkDeleteCancelBtn')?.addEventListener('click', () => {
            document.getElementById('cdkDeleteModal').classList.remove('show');
            state.cdkDeleteTargetCode = null;
        });

        document.getElementById('cdkDeleteConfirmBtn')?.addEventListener('click', async () => {
            if (!state.cdkDeleteTargetCode) return;
            const token = state.deps.checkToken();
            if (!token) return;
            const btn = document.getElementById('cdkDeleteConfirmBtn');

            await state.deps.withButtonLoading(btn, '删除中...', async () => {
                try {
                    const resp = await ApiClient.request('/api/cdk/admin/delete', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ code: state.cdkDeleteTargetCode })
                    });
                    const result = await resp.json().catch(() => ({}));
                    if (resp.ok) {
                        document.getElementById('cdkDeleteModal').classList.remove('show');
                        state.cdkDeleteTargetCode = null;
                        loadAdminCdks(state.cdkAdminPage);
                    } else {
                        alert(result.message || '删除失败');
                    }
                } catch (err) {
                    alert('网络错误');
                }
            });
        });
    }

    function init(options = {}) {
        if (state.initialized) return;
        state.deps = { ...state.deps, ...options };

        bindAssetEvents();
        bindCdkEvents();

        // 向全局导出保持原 inline onclick 行为（零行为改动）
        global.openAssetEditModal = openAssetEditModal;
        global.openAssetDeleteModal = openAssetDeleteModal;
        global.restoreAdminAsset = restoreAdminAsset;
        global.loadAdminAssets = loadAdminAssets;
        global.loadAdminCdks = loadAdminCdks;
        global.copyCdkCode = copyCdkCode;
        global.openCdkDeleteModal = openCdkDeleteModal;

        state.initialized = true;
    }

    global.ProfileAdmin = {
        init,
        loadAdminAssets,
        loadAdminCdks,
        openAssetEditModal,
        openAssetDeleteModal,
        restoreAdminAsset,
        copyCdkCode,
        openCdkDeleteModal
    };
})(window);
