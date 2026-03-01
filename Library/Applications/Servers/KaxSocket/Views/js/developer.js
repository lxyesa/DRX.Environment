/* ================================================================
 *  DevApp — 开发者中心核心控制器
 *  职责划分：
 *    • Auth     — 登录令牌与用户鉴权
 *    • Api      — 与后端开发者+审核 API 通信
 *    • State    — 页面状态（我的资产、审核列表、表单）
 *    • Renderer — DOM 渲染（资产列表、审核卡片、表单、弹窗）
 *    • Actions  — 用户交互（Tab 切换、筛选、CRUD、审核操作）
 * ================================================================ */
const DevApp = (() => {
    'use strict';

    const TOKEN_KEY  = 'kax_login_token';
    const PAGE_SIZE  = 20;

    const STATUS_TEXT = { 0: '审核中', 1: '已拒绝', 2: '待发布', 3: '已上线' };

    // #region State
    const state = {
        // user
        isAdmin: false,
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

        // editing
        editingId: 0, // 0 = create mode
        currentReviewAssetId: 0
    };
    // #endregion

    // #region Auth
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

    /** 验证令牌并获取用户权限信息 */
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

            // 如果是管理员则显示审核 Tab
            if (state.isAdmin) {
                const tab = document.getElementById('reviewTab');
                if (tab) tab.style.display = '';
            }
            return true;
        } catch (e) {
            console.error('验证用户失败', e);
            location.href = '/login';
            return false;
        }
    }
    // #endregion

    // #region Utils
    function formatDate(ts) {
        if (!ts) return '—';
        const ms = ts > 9999999999 ? ts : ts * 1000;
        const d = new Date(ms);
        if (isNaN(d.getTime())) return '—';
        return d.toLocaleDateString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit' });
    }

    function formatDateTime(ts) {
        if (!ts) return '—';
        const ms = ts > 9999999999 ? ts : ts * 1000;
        const d = new Date(ms);
        if (isNaN(d.getTime())) return '—';
        return d.toLocaleString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
    }

    function formatCooldown(seconds) {
        const h = Math.floor(seconds / 3600);
        const m = Math.floor((seconds % 3600) / 60);
        return h > 0 ? `${h}小时${m}分钟` : `${m}分钟`;
    }

    function escapeHtml(str) {
        if (!str) return '';
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    function statusBadge(statusVal) {
        const text = STATUS_TEXT[statusVal] || '未知';
        return `<span class="dev-status" data-status="${statusVal}">${text}</span>`;
    }

    function normalizeTags(raw) {
        if (Array.isArray(raw)) return raw.filter(Boolean).map(t => String(t).trim()).filter(Boolean);
        if (typeof raw === 'string') {
            return raw
                .split(/[;,，]/)
                .map(t => t.trim())
                .filter(Boolean);
        }
        return [];
    }
    // #endregion

    // #region Api —— 后端接口
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
    // #endregion

    // #region Renderer —— 我的资源

    /** 渲染统计 pill */
    function renderStats() {
        const s = state.myStats;
        const set = (id, v) => { const el = document.querySelector('#' + id + ' b'); if (el) el.textContent = v; };
        set('pillAll', s.all);
        set('pillPending', s.pending);
        set('pillRejected', s.rejected);
        set('pillActive', s.active);
    }

    /** 渲染我的资产列表 */
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
            const thumb = a.iconImage || a.coverImage || '';
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

            // 操作按钮
            let actions = '';
            if (a.status === 1) {
                // 被拒绝 — 编辑 + 重新提交
                actions += `<button class="btn ghost small dev-asset-btn" onclick="DevApp.editAsset(${a.id})" title="编辑"><span class="material-icons">edit</span><span>编辑</span></button>`;
                actions += `<button class="btn ghost small dev-asset-btn" onclick="DevApp.submitReview(${a.id})" title="重新提交审核"><span class="material-icons">send</span><span>重新提交</span></button>`;
            } else if (a.status === 2) {
                // 待发布 — 编辑 + 发布
                actions += `<button class="btn ghost small dev-asset-btn" onclick="DevApp.editAsset(${a.id})" title="编辑"><span class="material-icons">edit</span><span>编辑</span></button>`;
                actions += `<button class="btn ghost small dev-asset-btn dev-action-publish" onclick="DevApp.publishAsset(${a.id})" title="发布上线"><span class="material-icons">publish</span><span>发布</span></button>`;
            } else if (a.status === 3) {
                // 已上线 — 仅编辑
                actions += `<button class="btn ghost small dev-asset-btn" onclick="DevApp.editAsset(${a.id})" title="编辑"><span class="material-icons">edit</span><span>编辑</span></button>`;
            }
            // 审核中不显示操作

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

    /** 通用分页渲染 */
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

    /** 加载我的资产数据 */
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

    /** 加载统计数据（分别请求各状态的总数） */
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
    // #endregion

    // #region Renderer —— 提交 / 编辑表单

    /** 添加一个新的价格方案行 */
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

    /** 清空表单到初始状态 */
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
        document.getElementById('pricesList').innerHTML = '';
        document.getElementById('cancelEditBtn').style.display = 'none';
        document.getElementById('submitAssetBtn').textContent = '提交资源';
        // 同步侧边栏辅助按钮
        const aside = document.getElementById('cancelEditBtnAside');
        if (aside) aside.style.display = 'none';
        const submitAside = document.getElementById('submitAssetBtnAside');
        if (submitAside) submitAside.querySelector('span.material-icons').nextSibling.textContent = '提交资源';
    }

    /** 填充表单用于编辑 */
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
        document.getElementById('cancelEditBtn').style.display = '';
        document.getElementById('submitAssetBtn').textContent = '保存修改';
        // 同步侧边栏辅助按钮
        const aside = document.getElementById('cancelEditBtnAside');
        if (aside) aside.style.display = '';
        const submitAside = document.getElementById('submitAssetBtnAside');
        if (submitAside) submitAside.querySelector('span.material-icons').nextSibling.textContent = '保存修改';

        // 填充价格
        const list = document.getElementById('pricesList');
        list.innerHTML = '';
        if (asset.prices && asset.prices.length > 0) {
            asset.prices.forEach(p => addPriceRow(p));
        }
    }

    /** 收集表单数据 */
    function collectFormData() {
        const name = document.getElementById('assetName').value.trim();
        const version = document.getElementById('assetVersion').value.trim();
        const description = document.getElementById('assetDescription').value.trim();
        const category = document.getElementById('assetCategory').value.trim();
        const tags = document.getElementById('assetTags').value.trim();
        const coverImage = document.getElementById('assetCoverImage').value.trim();
        const iconImage = document.getElementById('assetIconImage').value.trim();
        const screenshots = document.getElementById('assetScreenshots').value.trim();

        // 收集价格方案
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

        return { name, version, description, category, tags, coverImage, iconImage, screenshots, prices };
    }
    // #endregion

    // #region Renderer —— 审核列表

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
            const thumb = a.iconImage || a.coverImage || '';
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
    // #endregion

    // #region Renderer —— 审核弹窗

    /** 初始化弹窗内 Tab 切换 */
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

        // 重置 tabs 到第一个
        document.querySelectorAll('#modalTabs .rv-tab').forEach((t, i) => t.classList.toggle('active', i === 0));

        // 占位 loading
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

            // ── 顶栏 ──
            document.getElementById('modalAssetName').textContent = a.name;
            document.getElementById('modalAssetSub').textContent =
                `v${a.version || '—'}  ·  ${a.category || '未分类'}`;

            document.getElementById('modalStatusBadge').innerHTML = statusBadge(a.status);

            // 头部图标
            const thumb = a.iconImage || a.coverImage || '';
            if (thumb) {
                headerIcon.innerHTML = `<img src="${escapeHtml(thumb)}" alt="">`;
            }

            // ── 审核按钮区 ──
            const reviewActions = document.querySelector('.rv-footer-actions');
            if (reviewActions) reviewActions.style.display = a.status === 0 ? '' : 'none';

            // ── 底栏作者 ──
            document.getElementById('modalFooterAuthor').innerHTML =
                `<span class="material-icons">person</span> ${escapeHtml(a.authorName || '—')}  (ID: ${a.authorId ?? '—'})`;

            // ── 左侧信息栏 ──
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

            // 标签
            const tags = Array.isArray(a.tags) ? a.tags : [];
            if (tags.length > 0) {
                infoHtml += `<div class="rv-info-item">
                    <span class="rv-info-label">标签</span>
                    <div class="rv-info-tags">${tags.map(t => `<span class="rv-info-tag">${escapeHtml(t)}</span>`).join('')}</div>
                </div>`;
            }

            document.getElementById('modalInfoList').innerHTML = infoHtml;

            // ── 右侧面板 ──

            // 概要
            let overviewHtml = '';
            if (a.status === 1 && a.rejectReason) {
                overviewHtml += `<div class="rv-reject-banner">
                    <span class="material-icons">warning_amber</span>
                    <p><b>拒绝原因：</b>${escapeHtml(a.rejectReason)}</p>
                </div>`;
            }
            if (a.description) {
                overviewHtml += `<div class="rv-overview-desc">${escapeHtml(a.description)}</div>`;
            } else {
                overviewHtml += `<div class="rv-empty"><span class="material-icons">article</span><p>暂无描述</p></div>`;
            }

            // 价格方案
            let pricesHtml = '';
            if (a.prices && a.prices.length > 0) {
                pricesHtml = `<div class="rv-price-cards">` +
                    a.prices.map(p => {
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

            // 写入面板
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
    // #endregion

    // #region Actions —— Tab 切换
    function switchTab(tabName) {
        document.querySelectorAll('.dev-tab').forEach(t => {
            const active = t.dataset.tab === tabName;
            t.classList.toggle('active', active);
            t.setAttribute('aria-selected', active ? 'true' : 'false');
        });
        document.querySelectorAll('.dev-tab-content').forEach(c => {
            c.classList.toggle('active', c.id === 'tab-' + tabName);
        });

        // 切换到 review-panel 时加载审核列表
        if (tabName === 'review-panel' && state.isAdmin) {
            loadReviewList();
        }
        // 切换到 create-asset 时如果不在编辑模式则重置
        if (tabName === 'create-asset' && !state.editingId) {
            resetForm();
        }
    }
    // #endregion

    // #region Actions —— 我的资源操作

    /** 编辑按钮 — 加载资产详情填入表单并切换 Tab */
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

    /** 提交/更新表单 */
    async function submitForm() {
        if (!requireLogin()) return;

        const data = collectFormData();
        if (!data.name) { alert('请填写资源名称'); return; }
        if (!data.version) { alert('请填写版本号'); return; }

        try {
            let resp;
            if (state.editingId) {
                // 更新模式
                resp = await apiUpdateAsset({ id: state.editingId, ...data });
            } else {
                // 新建模式
                resp = await apiCreateAsset(data);
            }

            if (resp.code === 0) {
                alert(resp.message || '操作成功');
                resetForm();
                switchTab('my-assets');
                loadMyAssets();
                loadMyStats();
            } else {
                alert(resp.message || '操作失败');
            }
        } catch (e) {
            alert('请求失败: ' + e.message);
        }
    }

    /** 重新提交审核 */
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
                // 可能是冷却中
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

    /** 发布资产 */
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
    // #endregion

    // #region Actions —— 审核操作

    /** 通过审核 */
    async function approveAsset() {
        const id = state.currentReviewAssetId;
        if (!id) return;
        if (!confirm('确认通过此资源审核？')) return;
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
        }
    }

    /** 打开拒绝原因弹窗 */
    function openRejectModal() {
        if (!state.currentReviewAssetId) return;
        document.getElementById('rejectReason').value = '';
        document.getElementById('rejectModal').style.display = '';
    }

    function closeRejectModal() {
        document.getElementById('rejectModal').style.display = 'none';
    }

    /** 确认拒绝 */
    async function confirmReject() {
        const id = state.currentReviewAssetId;
        if (!id) return;
        const reason = document.getElementById('rejectReason').value.trim();
        if (!reason) { alert('请填写拒绝原因'); return; }
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
        }
    }
    // #endregion

    // #region Init

    function bindEvents() {
        // Tab 切换
        document.querySelectorAll('.dev-tab').forEach(tab => {
            tab.addEventListener('click', () => switchTab(tab.dataset.tab));
        });

        // 状态筛选
        const statusFilter = document.getElementById('statusFilter');
        if (statusFilter) {
            statusFilter.addEventListener('change', () => {
                state.myStatusFilter = statusFilter.value;
                state.myPage = 1;
                loadMyAssets();
            });
        }

        // 审核状态筛选
        const reviewStatusFilter = document.getElementById('reviewStatusFilter');
        if (reviewStatusFilter) {
            reviewStatusFilter.addEventListener('change', () => {
                state.reviewStatusFilter = reviewStatusFilter.value;
                state.reviewPage = 1;
                loadReviewList();
            });
        }

        // 添加价格方案
        const addPriceBtn = document.getElementById('addPriceBtn');
        if (addPriceBtn) addPriceBtn.addEventListener('click', () => addPriceRow());

        // 提交表单
        const submitBtn = document.getElementById('submitAssetBtn');
        if (submitBtn) submitBtn.addEventListener('click', submitForm);

        // 取消编辑
        const cancelBtn = document.getElementById('cancelEditBtn');
        if (cancelBtn) cancelBtn.addEventListener('click', () => { resetForm(); switchTab('my-assets'); });

        // 审核弹窗
        document.getElementById('closeModal')?.addEventListener('click', closeReviewModal);
        document.getElementById('modalApproveBtn')?.addEventListener('click', approveAsset);
        document.getElementById('modalRejectBtn')?.addEventListener('click', openRejectModal);

        // 拒绝弹窗
        document.getElementById('closeRejectModal')?.addEventListener('click', closeRejectModal);
        document.getElementById('cancelRejectBtn')?.addEventListener('click', closeRejectModal);
        document.getElementById('confirmRejectBtn')?.addEventListener('click', confirmReject);

        // 点击 overlay 关闭弹窗
        document.getElementById('reviewModal')?.addEventListener('click', (e) => {
            if (e.target.id === 'reviewModal') closeReviewModal();
        });
        document.getElementById('rejectModal')?.addEventListener('click', (e) => {
            if (e.target.id === 'rejectModal') closeRejectModal();
        });
    }

    async function init() {
        // 全局组件
        if (window.initCustomSelects) window.initCustomSelects();
        if (window.initGlobalTopbar) window.initGlobalTopbar();
        if (window.initGlobalFooter) window.initGlobalFooter();
        if (window.initButtonEffects) window.initButtonEffects();

        // 鉴权
        const ok = await verifyUser();
        if (!ok) return;

        bindEvents();

        // 加载我的资产 & 统计
        await Promise.all([loadMyAssets(), loadMyStats()]);
    }
    // #endregion

    return {
        init,
        editAsset,
        submitReview,
        publishAsset,
        openReviewModal,
        switchTab
    };
})();

// 启动
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => DevApp.init());
} else {
    DevApp.init();
}
