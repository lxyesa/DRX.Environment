(function () {
            const page = document.getElementById('page');
            const loading = document.getElementById('loading');
            const logoutBtn = document.getElementById('logoutBtn');
            const homeBtn = document.getElementById('homeBtn');

            const createAssetForm = document.getElementById('createAssetForm');
            const updateAssetForm = document.getElementById('updateAssetForm');
            const loadAssetBtn = document.getElementById('loadAssetBtn');
            const updateBtn = document.getElementById('updateBtn');
            const assetList = document.getElementById('assetList');
            const searchInput = document.getElementById('searchInput');
            const searchSpinner = document.getElementById('searchSpinner');

            // 快速创建 CDK 元素
            const createCdkForm = document.getElementById('createCdkForm');
            const createCdkResult = document.getElementById('createCdkResult');
            const createCdkBtn = document.getElementById('createCdkBtn');
            const createCdkPreviewBtn = document.getElementById('createCdkPreviewBtn');
            const createCdkSaveBtn = document.getElementById('createCdkSaveBtn');
            const createCdkDownloadBtn = document.getElementById('createCdkDownloadBtn');
            const cdkAssetIdInput = document.getElementById('cdkAssetId');

            let currentSearchController = null;

            function setSearchLoading(on) {
                if (searchSpinner) {
                    searchSpinner.classList.toggle('active', !!on);
                    assetList.setAttribute('aria-busy', on ? 'true' : 'false');
                }
            }

            async function performSearch(q, page = 1) {
                // 取消上一次未完成的搜索请求
                if (currentSearchController) {
                    try { currentSearchController.abort(); } catch (e) { }
                    currentSearchController = null;
                }
                currentSearchController = new AbortController();
                const signal = currentSearchController.signal;

                setSearchLoading(true);
                try {
                    currentPage = page;
                    await loadAssetList(page, q, signal);
                } catch (err) {
                    if (err && err.name === 'AbortError') {
                        // 请求被取消，静默返回
                        return;
                    }
                    throw err;
                } finally {
                    setSearchLoading(false);
                    currentSearchController = null;
                }
            }
            function debounce(fn, wait) {
                let timeout = null;
                function debounced(...args) {
                    clearTimeout(timeout);
                    timeout = setTimeout(() => fn.apply(this, args), wait);
                }
                debounced.cancel = function () { clearTimeout(timeout); timeout = null; };
                return debounced;
            }

            const debouncedSearch = debounce(function () { performSearch(searchInput.value.trim(), 1); }, 300);

            if (searchInput) {
                searchInput.addEventListener('input', debouncedSearch);
                searchInput.addEventListener('keydown', function (ev) {
                    if (ev.key === 'Enter') { ev.preventDefault(); debouncedSearch.cancel && debouncedSearch.cancel(); performSearch(searchInput.value.trim(), 1); }
                });
            }

            const prevPageBtn = document.getElementById('prevPageBtn');
            const nextPageBtn = document.getElementById('nextPageBtn');
            const pageInfo = document.getElementById('pageInfo');
            let currentPage = 1;
            const pageSize = 20;

            const detailBackdrop = document.getElementById('assetDetailBackdrop');
            const detailPanel = document.getElementById('assetDetailPanel');
            const closeDetailBtn = document.getElementById('closeDetailPanelBtn');
            const detailContent = document.getElementById('assetDetailContent');

            /* initCustomSelects 已移入 /global.js（全局可复用） */
            /**
             * 验证 token 有效性
             */
            async function verifyToken() {
                try {
                    const token = localStorage.getItem('kax_login_token');
                    if (!token) {
                        location.href = '/login';
                        return;
                    }

                    const resp = await fetch('/api/user/verify/account', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': 'Bearer ' + token
                        }
                    });

                    if (resp.status === 200) {
                        page.classList.remove('hidden');
                        loading.classList.add('hidden');
                        initCustomSelects();
                        loadAssetList();
                        try { window.initGlobalFooter && window.initGlobalFooter(); } catch (_) { }
                        return;
                    } else {
                        location.href = '/login';
                    }
                } catch (err) {
                    console.error('验证失败', err);
                    location.href = '/login';
                }
            }

            /**
             * 加载资源列表
             */
            async function loadAssetList(page = 1, q = '', signal) {
                try {
                    const token = localStorage.getItem('kax_login_token');
                    const params = new URLSearchParams();
                    params.append('page', page.toString());
                    params.append('pageSize', pageSize.toString());
                    if (q) params.append('q', q);

                    const sortSelect = document.getElementById('sortSelect');
                    const statusFilter = document.getElementById('statusFilter');
                    if (sortSelect.value !== 'name') params.append('sort', sortSelect.value);
                    if (statusFilter.value !== 'all') params.append('status', statusFilter.value);

                    const resp = await fetch('/api/asset/admin/list?' + params.toString(), {
                        method: 'GET',
                        headers: {
                            'Authorization': 'Bearer ' + token
                        },
                        signal: signal
                    });

                    if (!resp.ok) throw new Error('加载失败');

                    const data = await resp.json();
                    const assets = data.data || [];

                    assetList.innerHTML = '';
                    if (assets.length === 0) {
                        assetList.innerHTML = '<p class="hint" style="text-align: center; padding: 20px 0;">暂无资源</p>';
                        return;
                    }

                    // 更新分页 UI
                    currentPage = data.page || page;
                    pageInfo.textContent = `第 ${currentPage} 页，共 ${data.total || assets.length} 条`;
                    prevPageBtn.disabled = currentPage <= 1;
                    nextPageBtn.disabled = (currentPage * pageSize) >= (data.total || assets.length);

                    assets.forEach(asset => {
                        const item = document.createElement('div');
                        item.className = 'asset-list-item';
                        const descriptionPreview = asset.description ? asset.description.substring(0, 100) + (asset.description.length > 100 ? '...' : '') : '无描述';
                        // 状态徽章放到右侧固定区域（保证对齐）
                        const statusBadgeHtml = asset.isDeleted
                            ? '<span class="status-badge">已删除</span>'
                            : '<span class="status-badge active">活跃</span>';

                        item.innerHTML = `
                            <div class="asset-left">
                                <span class="material-icons asset-icon">folder</span>
                                <div class="asset-text">
                                    <div class="asset-name">${escapeHtml(asset.name)}</div>
                                    <div class="asset-meta">v${escapeHtml(asset.version)} • ${escapeHtml(asset.author)}</div>
                                    <div class="asset-description">${escapeHtml(descriptionPreview)}</div>
                                </div>
                            </div>
                            <div class="asset-actions">
                                <div class="asset-badge">${statusBadgeHtml}</div>
                                <button class="info-btn" aria-label="查看详情">
                                    <span class="material-icons" style="font-size: 1rem;">info</span>
                                </button>
                                <button class="info-btn danger-icon" aria-label="删除" style="color: #ef4444;">
                                    <span class="material-icons" style="font-size: 1rem;">delete</span>
                                </button>
                            </div>
                        `;

                        const infoBtn = item.querySelector('.info-btn:not(.danger-icon)');
                        const deleteBtn = item.querySelector('.danger-icon');
                        // 如果已删除，显示恢复按钮而非删除
                        if (asset.isDeleted) {
                            deleteBtn.innerHTML = '<span class="material-icons" style="font-size: 1rem;">restore</span>';
                            deleteBtn.style.color = '#16a34a';
                        }

                        infoBtn.addEventListener('click', (e) => {
                            e.stopPropagation();
                            showAssetDetail(asset);
                        });

                        deleteBtn.addEventListener('click', async (e) => {
                            e.stopPropagation();
                            if (asset.isDeleted) {
                                if (confirm(`确定要恢复资源 "${asset.name}" 吗？`)) {
                                    await restoreAsset(asset.id);
                                }
                            } else {
                                if (confirm(`确定要删除资源 "${asset.name}" 吗？`)) {
                                    await deleteAsset(asset.id);
                                }
                            }
                        });

                        item.addEventListener('click', () => {
                            document.querySelectorAll('.asset-list-item').forEach(el => {
                                el.classList.remove('selected');
                            });
                            item.classList.add('selected');
                            document.getElementById('updateAssetId').value = asset.id;
                            // 同步到快速创建 CDK 表单（如果存在）
                            var _cdkEl = document.getElementById('cdkAssetId');
                            if (_cdkEl) _cdkEl.value = asset.id;
                            // 同步到价格计划管理器
                            var _priceEl = document.getElementById('priceAssetId');
                            if (_priceEl) {
                                _priceEl.value = asset.id;
                                loadPricePlans(asset.id);
                            }
                        });

                        assetList.appendChild(item);
                    });
                } catch (err) {
                    if (err && err.name === 'AbortError') {
                        // 请求被取消 —— 静默返回
                        return;
                    }
                    console.error('加载资源列表失败', err);
                    assetList.innerHTML = '<p class="hint" style="text-align: center; padding: 20px 0;">加载失败</p>';
                }
            }

            /**
             * 显示资源详情
             */
            function showAssetDetail(asset) {
                const updatedTime = new Date(asset.lastUpdatedAt * 1000).toLocaleString('zh-CN');
                detailContent.innerHTML = `
                    <div class="panel-section">
                        <label>资源ID</label>
                        <pre>${asset.id}</pre>
                    </div>
                    <div class="panel-section">
                        <label>资源名称</label>
                        <pre>${escapeHtml(asset.name)}</pre>
                    </div>
                    <div class="panel-section">
                        <label>版本</label>
                        <pre>${escapeHtml(asset.version)}</pre>
                    </div>
                    <div class="panel-section">
                        <label>作者</label>
                        <pre>${escapeHtml(asset.author)}</pre>
                    </div>
                    <div class="panel-section">
                        <label>描述</label>
                        <pre>${escapeHtml(asset.description)}</pre>
                    </div>
                    <div class="panel-section">
                        <label>最后更新</label>
                        <p class="hint">${updatedTime}</p>
                    </div>
                    <div class="panel-actions">
                        <button class="btn" style="flex: 1;" onclick="loadToUpdateForm(${asset.id})">编辑</button>
                        <button class="btn danger" style="flex: 1;" onclick="confirmDelete(${asset.id})">删除</button>
                    </div>
                `;
                detailPanel.classList.add('open');
                detailBackdrop.classList.add('open');
            }

            /**
             * 关闭详情面板
             */
            function closeDetailPanel() {
                detailPanel.classList.remove('open');
                detailBackdrop.classList.remove('open');
            }

            /**
             * 加载资源到修改表单
             */
            async function loadToUpdateForm(assetId) {
                try {
                    const token = localStorage.getItem('kax_login_token');
                    const resp = await fetch('/api/asset/admin/inspect', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': 'Bearer ' + token
                        },
                        body: JSON.stringify({ id: assetId })
                    });

                    if (!resp.ok) throw new Error('加载失败');
                    const data = await resp.json();
                    const asset = data.data;

                    document.getElementById('updateAssetId').value = asset.id;
                    document.getElementById('updateName').value = asset.name;
                    document.getElementById('updateVersion').value = asset.version;
                    document.getElementById('updateAuthor').value = asset.author;
                    document.getElementById('updateDescription').value = asset.description;
                    // 填充已有字段（注意：价格数据由价格计划管理器维护）
                    try { document.getElementById('updateStock').value = asset.stock || 0; } catch (_) { }
                    try { document.getElementById('updateCategory').value = asset.category || asset.type || ''; } catch (_) { }
                    try { document.getElementById('updateFileSize').value = asset.fileSize || 0; } catch (_) { }
                    try { document.getElementById('updateCompatibility').value = asset.compatibility || ''; } catch (_) { }
                    try { document.getElementById('updateDownloads').value = asset.downloads || 0; } catch (_) { }
                    try { document.getElementById('updateUploadDate').value = asset.uploadDate || asset.lastUpdatedAt || 0; } catch (_) { }
                    try { document.getElementById('updateLicense').value = asset.license || ''; } catch (_) { }
                    try { document.getElementById('updateDownloadUrl').value = asset.downloadUrl || ''; } catch (_) { }
                    try { document.getElementById('updateCompatibility').value = asset.compatibility || ''; } catch (_) { }
                    try { document.getElementById('updateDownloads').value = asset.downloads || 0; } catch (_) { }
                    try { document.getElementById('updateUploadDate').value = asset.uploadDate || asset.lastUpdatedAt || 0; } catch (_) { }
                    try { document.getElementById('updateLicense').value = asset.license || ''; } catch (_) { }
                    try { document.getElementById('updateDownloadUrl').value = asset.downloadUrl || ''; } catch (_) { }
                    document.getElementById('updateBtn').disabled = false;

                    closeDetailPanel();
                } catch (err) {
                    console.error('加载资源失败', err);
                    alert('加载资源失败');
                }
            }

            /**
             * 确认删除
             */
            async function confirmDelete(assetId) {
                if (confirm('确定要删除此资源吗？此操作不可恢复。')) {
                    await deleteAsset(assetId);
                }
            }

            /**
             * 删除资源
             */
            async function deleteAsset(assetId) {
                try {
                    const token = localStorage.getItem('kax_login_token');
                    const resp = await fetch('/api/asset/admin/delete', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': 'Bearer ' + token
                        },
                        body: JSON.stringify({ id: assetId })
                    });

                    if (resp.status === 200) {
                        alert('资源已删除');
                        closeDetailPanel();
                        loadAssetList();
                    } else {
                        const error = await resp.json();
                        alert(`删除失败: ${error.message || '未知错误'}`);
                    }
                } catch (err) {
                    console.error('删除资源失败', err);
                    alert('删除失败');
                }
            }

            /**
             * 恢复资源（软删除恢复）
             */
            async function restoreAsset(assetId) {
                try {
                    const token = localStorage.getItem('kax_login_token');
                    const resp = await fetch('/api/asset/admin/restore', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': 'Bearer ' + token
                        },
                        body: JSON.stringify({ id: assetId })
                    });

                    if (resp.status === 200) {
                        alert('资源已恢复');
                        closeDetailPanel();
                        loadAssetList(currentPage, searchInput.value.trim());
                    } else {
                        const error = await resp.json();
                        alert(`恢复失败: ${error.message || '未知错误'}`);
                    }
                } catch (err) {
                    console.error('恢复资源失败', err);
                    alert('恢复失败');
                }
            }

            /**
             * 转义 HTML 特殊字符
             */
            function escapeHtml(text) {
                if (!text) return '';
                const map = {
                    '&': '&amp;',
                    '<': '&lt;',
                    '>': '&gt;',
                    '"': '&quot;',
                    "'": '&#039;'
                };
                return text.replace(/[&<>"']/g, m => map[m]);
            }

            /**
             * 创建资源
             */
            createAssetForm.addEventListener('submit', async (e) => {
                e.preventDefault();

                const name = document.getElementById('createName').value.trim();
                const version = document.getElementById('createVersion').value.trim();
                const author = document.getElementById('createAuthor').value.trim();
                const description = document.getElementById('createDescription').value.trim();
                // 价格由价格计划管理器维护，创建资源时不再提交单一 price 字段
                const price = undefined;
                const stock = parseInt(document.getElementById('createStock').value, 10) || 0;
                const category = document.getElementById('createCategory').value.trim();
                const originalPrice = undefined;
                const fileSize = parseInt(document.getElementById('createFileSize').value, 10) || 0;
                const discountRate = undefined;
                const salePrice = undefined;
                const compatibility = document.getElementById('createCompatibility').value.trim();
                const downloads = parseInt(document.getElementById('createDownloads').value, 10) || 0;
                const uploadDate = parseInt(document.getElementById('createUploadDate').value, 10) || 0;
                const license = document.getElementById('createLicense').value.trim();
                const downloadUrl = document.getElementById('createDownloadUrl').value.trim();

                try {
                    const token = localStorage.getItem('kax_login_token');
                    const resp = await fetch('/api/asset/admin/create', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': 'Bearer ' + token
                        },
                        body: JSON.stringify({
                            name, version, author, description, stock,
                            category, fileSize, compatibility, downloads, uploadDate, license, downloadUrl
                        })
                    });

                    if (resp.status === 200) {
                        alert('资源创建成功');
                        createAssetForm.reset();
                        loadAssetList();
                    } else {
                        const error = await resp.json().catch(() => ({}));
                        alert(`创建失败: ${error.message || '未知错误'}`);
                    }
                } catch (err) {
                    console.error('创建资源失败', err);
                    alert('创建失败');
                }
            });

            // ========== 快速创建 CDK ==========
            if (createCdkForm) {
                createCdkForm.addEventListener('submit', async (e) => {
                    e.preventDefault();
                    var assetId = parseInt(document.getElementById('cdkAssetId').value, 10) || 0;
                    var contributionValue = parseInt(document.getElementById('cdkContributionValue').value, 10) || 0;
                    var expiresInValue = parseInt(document.getElementById('cdkExpiresInValue').value, 10) || 0;
                    var expiresInUnit = parseInt(document.getElementById('cdkExpiresInUnit').value, 10) || 0;
                    var expiresInSeconds = expiresInValue > 0 ? expiresInValue * expiresInUnit : 0;
                    var prefix = document.getElementById('cdkPrefix').value.trim();
                    var count = parseInt(document.getElementById('cdkCount').value, 10) || 1;
                    var length = parseInt(document.getElementById('cdkLength').value, 10) || 8;
                    var description = document.getElementById('cdkDesc').value.trim();

                    if (assetId <= 0) { alert('请输入有效的 AssetId'); return; }
                    count = Math.max(1, Math.min(1000, count));
                    length = Math.max(4, Math.min(256, length));

                    try {
                        var btn = document.getElementById('createCdkBtn');
                        var orig = btn.textContent;
                        btn.disabled = true;
                        btn.textContent = '生成并保存...';

                        var token = localStorage.getItem('kax_login_token');
                        var genResp = await fetch('/api/cdk/admin/generate', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                            body: JSON.stringify({ prefix: prefix, count: count, length: length })
                        });
                        if (!genResp.ok) throw new Error('生成失败');
                        var genData = await genResp.json();
                        var codes = genData.codes || [];

                        var saveResp = await fetch('/api/cdk/admin/save', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                            body: JSON.stringify({ codes: codes, assetId: assetId, goldValue: goldValue, expiresInSeconds: expiresInSeconds, description: description })
                        });

                        if (saveResp.status === 200 || saveResp.status === 201) {
                            alert('已生成并保存 ' + (codes.length) + ' 条 CDK');
                            document.getElementById('createCdkResult').value = codes.join('\n');
                            loadAssetList();
                        } else {
                            var err = await saveResp.json();
                            throw new Error(err.message || '保存失败');
                        }
                    } catch (err) {
                        alert('操作失败：' + (err.message || err));
                    } finally {
                        var btn = document.getElementById('createCdkBtn');
                        if (btn) { btn.disabled = false; btn.textContent = '生成并保存'; }
                    }
                });

                // 仅生成预览（不保存）
                createCdkPreviewBtn && createCdkPreviewBtn.addEventListener('click', async function () {
                    var prefix = document.getElementById('cdkPrefix').value.trim();
                    var count = parseInt(document.getElementById('cdkCount').value, 10) || 1;
                    var length = parseInt(document.getElementById('cdkLength').value, 10) || 8;
                    count = Math.max(1, Math.min(1000, count));
                    length = Math.max(4, Math.min(256, length));
                    try {
                        var token = localStorage.getItem('kax_login_token');
                        var resp = await fetch('/api/cdk/admin/generate', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                            body: JSON.stringify({ prefix: prefix, count: count, length: length })
                        });
                        if (!resp.ok) throw new Error('生成失败');
                        var data = await resp.json();
                        document.getElementById('createCdkResult').value = (data.codes || []).join('\n');
                    } catch (err) {
                        alert('生成失败：' + (err.message || err));
                    }
                });

                // 单独保存（从结果区域）
                createCdkSaveBtn && createCdkSaveBtn.addEventListener('click', async function () {
                    var txt = document.getElementById('createCdkResult').value || '';
                    if (!txt) { alert('无生成代码可保存'); return; }
                    var codes = txt.split(/\r?\n/).filter(Boolean);
                    var assetId = parseInt(document.getElementById('cdkAssetId').value, 10) || 0;
                    var goldValue = parseInt(document.getElementById('cdkGoldValue').value, 10) || 0;
                    var expiresInValue = parseInt(document.getElementById('cdkExpiresInValue').value, 10) || 0;
                    var expiresInUnit = parseInt(document.getElementById('cdkExpiresInUnit').value, 10) || 0;
                    var expiresInSeconds = expiresInValue > 0 ? expiresInValue * expiresInUnit : 0;
                    var description = document.getElementById('cdkDesc').value.trim();
                    if (assetId <= 0) { alert('请填写有效的 AssetId'); return; }

                    try {
                        var btn = document.getElementById('createCdkSaveBtn');
                        var orig = btn.textContent;
                        btn.disabled = true;
                        btn.textContent = '保存中...';

                        var token = localStorage.getItem('kax_login_token');
                        var resp = await fetch('/api/cdk/admin/save', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                            body: JSON.stringify({ codes: codes, assetId: assetId, goldValue: goldValue, expiresInSeconds: expiresInSeconds, description: description })
                        });
                        var data = await resp.json();
                        if (resp.status === 200 || resp.status === 201) {
                            alert('已保存 ' + (data.count || codes.length) + ' 条 CDK');
                            loadAssetList();
                        } else {
                            alert('保存失败: ' + (data.message || '未知错误'));
                        }
                    } catch (err) {
                        alert('保存失败: ' + (err.message || err));
                    } finally {
                        var btn = document.getElementById('createCdkSaveBtn');
                        if (btn) { btn.disabled = false; btn.textContent = '保存'; }
                    }
                });

                // 下载结果
                createCdkDownloadBtn && createCdkDownloadBtn.addEventListener('click', function () {
                    var txt = document.getElementById('createCdkResult').value;
                    if (!txt) { alert('无内容可下载'); return; }
                    var blob = new Blob([txt], { type: 'text/plain' });
                    var url = URL.createObjectURL(blob);
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = 'cdks_' + new Date().getTime() + '.txt';
                    document.body.appendChild(a);
                    a.click();
                    document.body.removeChild(a);
                    URL.revokeObjectURL(url);
                });
            }

            /**
             * 加载按钮
             */
            loadAssetBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                const assetId = document.getElementById('updateAssetId').value;
                if (!assetId) {
                    alert('请输入资源ID');
                    return;
                }
                await loadToUpdateForm(parseInt(assetId));
            });

            // ========== 价格计划管理 ==========
            const priceAssetIdInput = document.getElementById('priceAssetId');
            const addPricePlanBtn = document.getElementById('addPricePlanBtn');
            const pricePlanForm = document.getElementById('pricePlanForm');
            const cancelPricePlanBtn = document.getElementById('cancelPricePlanBtn');
            const priceList = document.getElementById('priceList');
            const editingPriceId = document.getElementById('editingPriceId');
            const planPrice = document.getElementById('planPrice');
            const planUnit = document.getElementById('planUnit');
            const planDuration = document.getElementById('planDuration');
            const planOriginalPrice = document.getElementById('planOriginalPrice');
            const planDiscountRate = document.getElementById('planDiscountRate');
            const planSalePrice = document.getElementById('planSalePrice');

            let currentAssetData = null;

            function formatPrice(cents) {
                return (cents / 100).toFixed(2);
            }

            function calculateSalePrice() {
                const price = parseInt(planPrice.value) || 0;
                const discountRate = parseFloat(planDiscountRate.value) || 0;
                const salePrice = Math.round(price * (1 - discountRate));
                planSalePrice.value = salePrice;
            }

            planPrice.addEventListener('input', calculateSalePrice);
            planDiscountRate.addEventListener('input', calculateSalePrice);

            // 根据单位显示/隐藏持续时间输入
            function updateDurationVisibility() {
                const container = document.getElementById('planDurationContainer');
                if (!container) return;
                if (planUnit.value === 'once') {
                    container.style.display = 'none';
                    // optional: clear或设置默认值
                    planDuration.value = '';
                } else {
                    container.style.display = '';
                }
            }
            planUnit.addEventListener('change', () => {
                updateDurationVisibility();
                calculateSalePrice();
            });
            // 初始化可见性
            updateDurationVisibility();

            function showPricePlanForm(priceId = null) {
                pricePlanForm.style.display = 'block';
                editingPriceId.value = priceId || '';

                if (priceId && currentAssetData && currentAssetData.prices) {
                    const price = currentAssetData.prices.find(p => p.id === priceId);
                    if (price) {
                        planPrice.value = price.price || 0;
                        planUnit.value = price.unit || 'month';
                        planDuration.value = price.duration || 1;
                        planOriginalPrice.value = price.originalPrice || 0;
                        planDiscountRate.value = price.discountRate || 0;
                        calculateSalePrice();
                    }
                } else {
                    planPrice.value = '';
                    planUnit.value = 'month';
                    planDuration.value = '1';
                    planOriginalPrice.value = '';
                    planDiscountRate.value = '0';
                    planSalePrice.value = '';
                }
            }

            function hidePricePlanForm() {
                pricePlanForm.style.display = 'none';
                editingPriceId.value = '';
            }

            async function loadPricePlans(assetId) {
                if (!assetId) {
                    priceList.innerHTML = '<p class="hint" style="text-align: center; padding: 20px 0;">请先选择资源</p>';
                    return;
                }

                try {
                    const token = localStorage.getItem('kax_login_token');
                    const resp = await fetch('/api/asset/admin/inspect', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': 'Bearer ' + token
                        },
                        body: JSON.stringify({ id: assetId })
                    });

                    if (!resp.ok) throw new Error('加载失败');
                    const data = await resp.json();
                    currentAssetData = data.data;

                    const prices = currentAssetData.prices || [];
                    priceList.innerHTML = '';

                    if (prices.length === 0) {
                        priceList.innerHTML = '<p class="hint" style="text-align: center; padding: 20px 0;">暂无价格计划</p>';
                        return;
                    }

                    prices.forEach(price => {
                        const item = document.createElement('div');
                        item.className = 'price-list-item';
                        const unitMap = { 'once': '一次性', 'hour': '小时', 'day': '天', 'month': '月', 'year': '年' };
                        const unitLabel = unitMap[price.unit] || price.unit;
                        const durationText = price.unit === 'once' ? '' : `${price.duration}${unitLabel}`;
                        const salePrice = Math.round(price.price * (1 - (price.discountRate || 0)));

                        item.innerHTML = `
                            <div class="price-item-info">
                                <div class="price-item-main">¥${formatPrice(price.price)} ${durationText}</div>
                                <div class="price-item-meta">原价: ¥${formatPrice(price.originalPrice || price.price)} | 折扣: ${((price.discountRate || 0) * 100).toFixed(0)}% | 折后: ¥${formatPrice(salePrice)}</div>
                            </div>
                            <div class="price-item-actions">
                                <button class="info-btn" aria-label="编辑" style="padding: 4px 8px;">
                                    <span class="material-icons" style="font-size: 1rem;">edit</span>
                                </button>
                                <button class="info-btn danger-icon" aria-label="删除" style="color: #ef4444; padding: 4px 8px;">
                                    <span class="material-icons" style="font-size: 1rem;">delete</span>
                                </button>
                            </div>
                        `;

                        const editBtn = item.querySelector('.info-btn:not(.danger-icon)');
                        const deleteBtn = item.querySelector('.danger-icon');

                        editBtn.addEventListener('click', (e) => {
                            e.stopPropagation();
                            showPricePlanForm(price.id);
                        });

                        deleteBtn.addEventListener('click', async (e) => {
                            e.stopPropagation();
                            if (confirm('确定要删除此价格计划吗？')) {
                                await deletePricePlan(price.id);
                            }
                        });

                        priceList.appendChild(item);
                    });
                } catch (err) {
                    console.error('加载价格计划失败', err);
                    priceList.innerHTML = '<p class="hint" style="text-align: center; padding: 20px 0;">加载失败</p>';
                }
            }

            async function savePricePlan() {
                const assetId = parseInt(priceAssetIdInput.value);
                const price = parseInt(planPrice.value) || 0;
                const unit = planUnit.value;
                const duration = parseInt(planDuration.value) || 1;
                const originalPrice = parseInt(planOriginalPrice.value) || price;
                const discountRate = parseFloat(planDiscountRate.value) || 0;
                const priceId = editingPriceId.value;

                if (!assetId || price <= 0) {
                    alert('请输入有效的资源ID和价格');
                    return;
                }

                if (discountRate < 0 || discountRate > 1) {
                    alert('折扣率必须在 0.0-1.0 之间');
                    return;
                }

                try {
                    const token = localStorage.getItem('kax_login_token');
                    const prices = currentAssetData.prices || [];

                    if (priceId) {
                        const idx = prices.findIndex(p => p.id === priceId);
                        if (idx >= 0) {
                            prices[idx] = { id: priceId, price, unit, duration, originalPrice, discountRate };
                        }
                    } else {
                        prices.push({ id: 'new_' + Date.now(), price, unit, duration, originalPrice, discountRate });
                    }

                    const resp = await fetch('/api/asset/admin/update', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': 'Bearer ' + token
                        },
                        body: JSON.stringify({
                            id: assetId,
                            name: currentAssetData.name,
                            version: currentAssetData.version,
                            author: currentAssetData.author,
                            description: currentAssetData.description,
                            prices: prices
                        })
                    });

                    if (resp.status === 200) {
                        alert(priceId ? '价格计划已更新' : '价格计划已创建');
                        hidePricePlanForm();
                        await loadPricePlans(assetId);
                    } else {
                        const error = await resp.json().catch(() => ({}));
                        alert(`保存失败: ${error.message || '未知错误'}`);
                    }
                } catch (err) {
                    console.error('保存价格计划失败', err);
                    alert('保存失败');
                }
            }

            async function deletePricePlan(priceId) {
                const assetId = parseInt(priceAssetIdInput.value);
                if (!assetId) return;

                try {
                    const token = localStorage.getItem('kax_login_token');
                    const prices = (currentAssetData.prices || []).filter(p => p.id !== priceId);

                    const resp = await fetch('/api/asset/admin/update', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': 'Bearer ' + token
                        },
                        body: JSON.stringify({
                            id: assetId,
                            name: currentAssetData.name,
                            version: currentAssetData.version,
                            author: currentAssetData.author,
                            description: currentAssetData.description,
                            prices: prices
                        })
                    });

                    if (resp.status === 200) {
                        alert('价格计划已删除');
                        await loadPricePlans(assetId);
                    } else {
                        const error = await resp.json().catch(() => ({}));
                        alert(`删除失败: ${error.message || '未知错误'}`);
                    }
                } catch (err) {
                    console.error('删除价格计划失败', err);
                    alert('删除失败');
                }
            }

            addPricePlanBtn.addEventListener('click', (e) => {
                e.preventDefault();
                if (!priceAssetIdInput.value) {
                    alert('请先选择资源');
                    return;
                }
                showPricePlanForm();
            });

            cancelPricePlanBtn.addEventListener('click', (e) => {
                e.preventDefault();
                hidePricePlanForm();
            });

            pricePlanForm.addEventListener('submit', async (e) => {
                e.preventDefault();
                await savePricePlan();
            });

            // 当在修改资源表单中加载资源时，同时加载价格计划
            const originalLoadToUpdateForm = loadToUpdateForm;
            window.loadToUpdateForm = async function(assetId) {
                await originalLoadToUpdateForm(assetId);
                priceAssetIdInput.value = assetId;
                await loadPricePlans(assetId);
            };

            // 当选择资源列表项时，同步到价格计划管理器
            const originalAssetListClick = function() {};
            document.addEventListener('assetSelected', (e) => {
                priceAssetIdInput.value = e.detail.assetId;
                loadPricePlans(e.detail.assetId);
            });

            // 搜索由输入变更（debounced）或按 Enter 触发；不再使用独立按钮

            prevPageBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                if (currentPage > 1) {
                    currentPage--;
                    await loadAssetList(currentPage, searchInput.value.trim());
                }
            });

            nextPageBtn.addEventListener('click', async (e) => {
                e.preventDefault();
                currentPage++;
                await loadAssetList(currentPage, searchInput.value.trim());
            });

            /**
             * 更新资源
             */
            updateAssetForm.addEventListener('submit', async (e) => {
                e.preventDefault();
                const id = parseInt(document.getElementById('updateAssetId').value);
                const name = document.getElementById('updateName').value.trim();
                const version = document.getElementById('updateVersion').value.trim();
                const author = document.getElementById('updateAuthor').value.trim();
                const description = document.getElementById('updateDescription').value.trim();
                // 价格字段由价格计划管理器维护，更新资源时不再提交单一 price 字段
                const price = undefined;
                const stock = parseInt(document.getElementById('updateStock').value, 10) || 0;
                const category = document.getElementById('updateCategory').value.trim();
                const originalPrice = undefined;
                const fileSize = parseInt(document.getElementById('updateFileSize').value, 10) || 0;
                const discountRate = undefined;
                const salePrice = undefined;
                const compatibility = document.getElementById('updateCompatibility').value.trim();
                const downloads = parseInt(document.getElementById('updateDownloads').value, 10) || 0;
                const uploadDate = parseInt(document.getElementById('updateUploadDate').value, 10) || 0;
                const license = document.getElementById('updateLicense').value.trim();
                const downloadUrl = document.getElementById('updateDownloadUrl').value.trim();

                try {
                    const token = localStorage.getItem('kax_login_token');
                    const resp = await fetch('/api/asset/admin/update', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            'Authorization': 'Bearer ' + token
                        },
                        body: JSON.stringify({
                            id, name, version, author, description, stock,
                            category, fileSize, compatibility, downloads, uploadDate, license, downloadUrl
                        })
                    });

                    if (resp.status === 200) {
                        alert('资源已更新');
                        updateAssetForm.reset();
                        document.getElementById('updateBtn').disabled = true;
                        loadAssetList();
                    } else {
                        const error = await resp.json().catch(() => ({}));
                        alert(`更新失败: ${error.message || '未知错误'}`);
                    }
                } catch (err) {
                    console.error('更新资源失败', err);
                    alert('更新失败');
                }
            });

            /**
             * 导航 — 返回首页 / 登出
             */
            if (homeBtn) {
                homeBtn.addEventListener('click', () => { location.href = '/'; });
            }
            logoutBtn.addEventListener('click', () => {
                localStorage.removeItem('kax_login_token');
                location.href = '/login';
            });

            /**
             * 关闭面板
             */
            closeDetailBtn.addEventListener('click', closeDetailPanel);
            detailBackdrop.addEventListener('click', closeDetailPanel);

            /**
             * 排序和过滤事件
             */
            document.getElementById('sortSelect').addEventListener('change', () => {
                currentPage = 1;
                loadAssetList(currentPage, searchInput.value.trim());
            });

            document.getElementById('statusFilter').addEventListener('change', () => {
                currentPage = 1;
                loadAssetList(currentPage, searchInput.value.trim());
            });

            /**
             * 全局函数供 HTML 调用
             */
            window.loadToUpdateForm = loadToUpdateForm;
            window.confirmDelete = confirmDelete;

            // 验证 token
            verifyToken();
        })();
