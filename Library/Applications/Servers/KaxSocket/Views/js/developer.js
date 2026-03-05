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

    const STATUS_TEXT = { 0: '审核中', 1: '已拒绝', 2: '待发布', 3: '已上线', 4: '已下架' };
    const SYSTEM_FIELD_CONFIG = [
        { key: 'name', label: '名称', multiline: false },
        { key: 'version', label: '版本', multiline: false },
        { key: 'description', label: '描述', multiline: true },
        { key: 'category', label: '分类', multiline: false },
        { key: 'tags', label: '标签', multiline: true, visualType: 'chips' },
        { key: 'badges', label: '徽章', multiline: true, visualType: 'chips' },
        { key: 'features', label: '特性', multiline: true, visualType: 'chips' },
        { key: 'coverImage', label: '封面图 URL', multiline: false },
        { key: 'iconImage', label: '图标 URL', multiline: false },
        { key: 'screenshots', label: '截图', multiline: true, visualType: 'image-list' },
        { key: 'downloadUrl', label: '下载地址', multiline: false },
        { key: 'license', label: '许可证', multiline: false },
        { key: 'compatibility', label: '兼容性', multiline: true, visualType: 'chips' }
    ];

    // #region State
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
            state.isSystem = body.isSystem === true || state.permissionGroup === 0;

            // 如果是管理员则显示审核 Tab
            if (state.isAdmin) {
                const tab = document.getElementById('reviewTab');
                if (tab) tab.style.display = '';
            }

            // 仅 system 用户显示资产管理 Tab
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

    function tryParseJsonArray(raw) {
        if (!raw || typeof raw !== 'string') return [];
        try {
            const parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed : [];
        } catch {
            return [];
        }
    }

    function formatBadgesForEditor(raw) {
        const list = tryParseJsonArray(raw);
        if (!list.length) return '';
        return list
            .map(item => {
                const icon = String(item?.icon ?? '').trim();
                const text = String(item?.text ?? '').trim();
                if (!text) return '';
                return `${icon}|${text}`;
            })
            .filter(Boolean)
            .join('\n');
    }

    function formatFeaturesForEditor(raw) {
        const list = tryParseJsonArray(raw);
        if (!list.length) return '';
        return list
            .map(item => {
                const icon = String(item?.icon ?? '').trim();
                const title = String(item?.title ?? '').trim();
                const desc = String(item?.desc ?? '').trim();
                if (!title) return '';
                return `${icon}|${title}|${desc}`;
            })
            .filter(Boolean)
            .join('\n');
    }

    function serializeBadgesFromEditor(raw) {
        const text = String(raw ?? '').trim();
        if (!text) return '';

        // 兼容用户直接粘贴 JSON
        const parsedJson = tryParseJsonArray(text);
        if (parsedJson.length) {
            const normalized = parsedJson
                .map(item => ({
                    icon: String(item?.icon ?? '').trim() || 'info',
                    text: String(item?.text ?? '').trim()
                }))
                .filter(item => item.text);
            return normalized.length ? JSON.stringify(normalized) : '';
        }

        const list = text
            .split(/\r?\n/)
            .map(line => line.trim())
            .filter(Boolean)
            .map(line => {
                const [iconRaw = '', ...rest] = line.split('|');
                const icon = iconRaw.trim() || 'info';
                const value = rest.join('|').trim();
                return { icon, text: value };
            })
            .filter(item => item.text);

        return list.length ? JSON.stringify(list) : '';
    }

    function serializeFeaturesFromEditor(raw) {
        const text = String(raw ?? '').trim();
        if (!text) return '';

        // 兼容用户直接粘贴 JSON
        const parsedJson = tryParseJsonArray(text);
        if (parsedJson.length) {
            const normalized = parsedJson
                .map(item => ({
                    icon: String(item?.icon ?? '').trim() || 'star',
                    title: String(item?.title ?? '').trim(),
                    desc: String(item?.desc ?? '').trim()
                }))
                .filter(item => item.title);
            return normalized.length ? JSON.stringify(normalized) : '';
        }

        const list = text
            .split(/\r?\n/)
            .map(line => line.trim())
            .filter(Boolean)
            .map(line => {
                const [iconRaw = '', titleRaw = '', ...descParts] = line.split('|');
                const icon = iconRaw.trim() || 'star';
                const title = titleRaw.trim();
                const desc = descParts.join('|').trim();
                return { icon, title, desc };
            })
            .filter(item => item.title);

        return list.length ? JSON.stringify(list) : '';
    }

    function getSystemFieldRawValue(detail, field) {
        if (!detail || !field) return '';
        if (Object.prototype.hasOwnProperty.call(detail, field)) return detail[field];
        if (detail.specs && Object.prototype.hasOwnProperty.call(detail.specs, field)) return detail.specs[field];
        return '';
    }

    function normalizeSystemFieldValue(value) {
        if (Array.isArray(value)) return value.join(';');
        if (typeof value === 'object' && value !== null) return JSON.stringify(value);
        return value == null ? '' : String(value);
    }

    function formatSystemFieldPreview(value) {
        const text = normalizeSystemFieldValue(value).trim();
        if (!text) return '当前值：空';
        const clipped = text.length > 42 ? `${text.slice(0, 42)}...` : text;
        return `当前值：${clipped}`;
    }

    function setButtonLoading(button, loadingText) {
        if (!button) return () => {};
        if (button.dataset.loading === '1') return () => {};

        button.dataset.loading = '1';
        button.dataset.originalHtml = button.innerHTML;
        button.dataset.originalDisabled = button.disabled ? '1' : '0';

        button.disabled = true;
        button.classList.add('is-loading');
        if (loadingText) {
            button.textContent = loadingText;
        }

        return () => {
            button.classList.remove('is-loading');
            if (button.dataset.originalHtml != null) {
                button.innerHTML = button.dataset.originalHtml;
            }
            button.disabled = button.dataset.originalDisabled === '1';
            delete button.dataset.loading;
            delete button.dataset.originalHtml;
            delete button.dataset.originalDisabled;
        };
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
        const badgesInput = document.getElementById('assetBadges');
        if (badgesInput) badgesInput.value = '';
        const featuresInput = document.getElementById('assetFeatures');
        if (featuresInput) featuresInput.value = '';
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
        const badgesInput = document.getElementById('assetBadges');
        if (badgesInput) badgesInput.value = formatBadgesForEditor(asset.badges || '');
        const featuresInput = document.getElementById('assetFeatures');
        if (featuresInput) featuresInput.value = formatFeaturesForEditor(asset.features || '');
        document.getElementById('cancelEditBtn').style.display = '';
        document.getElementById('submitAssetBtn').textContent = '保存并重审';
        // 同步侧边栏辅助按钮
        const aside = document.getElementById('cancelEditBtnAside');
        if (aside) aside.style.display = '';
        const submitAside = document.getElementById('submitAssetBtnAside');
        if (submitAside) submitAside.querySelector('span.material-icons').nextSibling.textContent = '保存并重审';

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
        const badgesEditorText = document.getElementById('assetBadges')?.value?.trim() || '';
        const featuresEditorText = document.getElementById('assetFeatures')?.value?.trim() || '';
        const badges = serializeBadgesFromEditor(badgesEditorText);
        const features = serializeFeaturesFromEditor(featuresEditorText);

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

        return { name, version, description, category, tags, coverImage, iconImage, screenshots, badges, features, prices };
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

            const list = Array.isArray(body.data)
                ? body.data
                : (body.data?.items ?? []);

            state.systemAssets = list;
            state.systemTotal = body.total ?? body.data?.total ?? list.length;
            renderSystemAssetList();
        } catch (e) {
            console.error('加载 system 资产列表失败', e);
            alert('加载资产管理列表失败');
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
            const thumb = a.coverImage || a.iconImage || '';
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

    async function openSystemAssetModal(assetId) {
        state.currentSystemAssetId = assetId;
        state.currentSystemAssetDetail = null;
        const modal = document.getElementById('systemAssetModal');
        if (!modal) return;

        document.getElementById('systemAssetModalTitle').textContent = `资产 #${assetId}`;
        document.getElementById('systemAssetModalMeta').innerHTML = '<div class="rv-info-item"><span class="rv-info-label">状态</span><span class="rv-info-value">加载中...</span></div>';
        document.getElementById('systemAssetModalDesc').textContent = '';
        const fieldCards = document.getElementById('systemFieldCards');
        if (fieldCards) {
            fieldCards.innerHTML = '<div class="system-field-loading">字段卡片加载中...</div>';
        }
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

    // ---------- 可视化编辑器辅助函数 ----------

    /**
     * 将分号分隔字符串解析为条目数组（过滤空项）
     */
    function parseSemicolon(raw) {
        if (!raw) return [];
        return String(raw).split(';').map(s => s.trim()).filter(Boolean);
    }

    /**
     * 从可视化编辑器容器收集当前条目，返回分号分隔字符串
     */
    function collectVisualValue(fieldKey) {
        const card = document.querySelector(`[data-field-card="${fieldKey}"]`);
        if (!card) return '';
        const items = card.querySelectorAll('[data-visual-item]');
        return Array.from(items).map(el => el.dataset.visualItem).join(';');
    }

    /**
     * 渲染 chips 编辑器（标签/徽章/特性/兼容性）
     */
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

    /**
     * 渲染图片列表编辑器（screenshots）
     */
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

    /**
     * 绑定 chips 编辑器事件（添加 / 删除）
     */
    function bindChipsEditorEvents(card, fieldKey, items) {
        const list = card.querySelector(`#sfc-chips-${fieldKey}`);
        const addInput = card.querySelector(`#sfc-addinput-${fieldKey}`);
        const addBtn = card.querySelector(`[data-add-chips="${fieldKey}"]`);

        function refreshChips() {
            list.querySelectorAll('[data-del-idx]').forEach(btn => {
                btn.addEventListener('click', () => {
                    const idx = parseInt(btn.dataset.delIdx, 10);
                    const chip = btn.closest('.sfc-chip');
                    if (chip) chip.remove();
                    // 重新建立 idx（删除后刷新 data-del-idx）
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

    /**
     * 绑定图片列表编辑器事件
     */
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

    /**
     * 重新为列表中的条目分配 data-del-idx（用于保证删除索引正确）
     */
    function rebuildDelIdx(container, itemSelector) {
        container.querySelectorAll(itemSelector).forEach((el, i) => {
            const btn = el.querySelector('[data-del-idx]');
            if (btn) btn.dataset.delIdx = i;
        });
    }

    /**
     * 对 HTML 属性值做最小转义
     */
    function escapeAttr(str) {
        return String(str ?? '').replace(/&/g, '&amp;').replace(/"/g, '&quot;');
    }

    // ---------- 主渲染函数 ----------

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

        // 绑定可视化编辑器事件
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

        // 可视化字段从 DOM 条目收集，普通字段从 input/textarea 读取
        const fieldCfg = SYSTEM_FIELD_CONFIG.find(f => f.key === field);
        let value;
        if (fieldCfg?.visualType) {
            value = collectVisualValue(field);
        } else {
            const input = document.getElementById(`systemFieldInput-${field}`);
            value = input?.value ?? '';
        }

        if (!detail) {
            alert('资产详情未加载，请稍后重试');
            return;
        }

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
        // 切换到 asset-management 时加载 system 资产列表
        if (tabName === 'asset-management' && state.isSystem) {
            loadSystemAssetList();
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
            if (state.editingId) {
                // 编辑模式：先保存再强制提交重审
                const updateResp = await apiUpdateAsset({ id: state.editingId, ...data });
                if (updateResp.code !== 0) {
                    alert(updateResp.message || '保存失败');
                    return;
                }
                // 保存成功后自动提交审核，进入重审流程
                const submitResp = await apiSubmitReview(state.editingId);
                if (submitResp.code === 0) {
                    alert('资源已保存并提交重审，等待审核结果');
                } else if (submitResp.cooldownRemaining) {
                    alert(`资源已保存，但提交冷却中，请在 ${formatCooldown(submitResp.cooldownRemaining)} 后手动重新提交`);
                } else {
                    alert(`资源已保存，但提交审核失败：${submitResp.message || '未知错误'}`);
                }
            } else {
                // 新建模式
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

        // system 资产管理筛选
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
        if (systemKeyword) systemKeyword.addEventListener('keydown', (e) => { if (e.key === 'Enter') doSearch(); });
        if (systemAuthorId) systemAuthorId.addEventListener('keydown', (e) => { if (e.key === 'Enter') doSearch(); });

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

        // system 资产详情弹窗
        document.getElementById('closeSystemAssetModal')?.addEventListener('click', closeSystemAssetModal);
        document.getElementById('systemReturnBtn')?.addEventListener('click', returnSystemAsset);
        document.getElementById('systemOffShelfBtn')?.addEventListener('click', offShelfSystemAsset);
        document.getElementById('systemForceReviewBtn')?.addEventListener('click', forceReviewSystemAsset);
        document.getElementById('systemHardDeleteBtn')?.addEventListener('click', hardDeleteSystemAsset);

        // 点击 overlay 关闭弹窗
        document.getElementById('reviewModal')?.addEventListener('click', (e) => {
            if (e.target.id === 'reviewModal') closeReviewModal();
        });
        document.getElementById('rejectModal')?.addEventListener('click', (e) => {
            if (e.target.id === 'rejectModal') closeRejectModal();
        });
        document.getElementById('systemAssetModal')?.addEventListener('click', (e) => {
            if (e.target.id === 'systemAssetModal') closeSystemAssetModal();
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
        openSystemAssetModal,
        switchTab
    };
})();

// 启动
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => DevApp.init());
} else {
    DevApp.init();
}
