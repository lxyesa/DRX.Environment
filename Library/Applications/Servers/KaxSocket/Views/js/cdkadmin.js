(function () {
            // ========== UI 元素 ==========
            var page = document.getElementById('page');
            var loading = document.getElementById('loading');
            var logoutBtn = document.getElementById('logoutBtn');
            var homeBtn = document.getElementById('homeBtn');
            var generateForm = document.getElementById('generateForm');
            var searchInput = document.getElementById('searchInput');
            var cdkList = document.getElementById('cdkList');
            var pageInfo = document.getElementById('pageInfo');
            var prevPageBtn = document.getElementById('prevPageBtn');
            var nextPageBtn = document.getElementById('nextPageBtn');
            var detailPanel = document.getElementById('cdkDetailPanel');
            var detailBackdrop = document.getElementById('cdkDetailBackdrop');
            var detailContent = document.getElementById('cdkDetailContent');
            var resultArea = document.getElementById('resultArea');
            var saveBtn = document.getElementById('saveBtn');
            var downloadBtn = document.getElementById('downloadBtn');
            var searchSpinner = document.getElementById('searchSpinner');
            var deleteAllBtn = document.getElementById('deleteAllBtn');

            var currentPage = 1;
            var allCdks = [];
            var filteredCdks = [];
            var itemsPerPage = 20;

            function showPage() { loading.classList.add('hidden'); page.classList.remove('hidden'); }
            function showLoading() { loading.classList.remove('hidden'); page.classList.add('hidden'); }

            function forceLogout() {
                try { localStorage.removeItem('kax_login_token'); } catch (_) { }
                location.href = '/login';
            }

            // ========== Token 验证 ==========
            async function verifyToken() {
                try {
                    var token = localStorage.getItem('kax_login_token');
                    if (!token) { location.href = '/login'; return; }

                    var resp = await fetch('/api/user/verify/account', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token }
                    });

                    if (resp.status === 200) {
                        showPage();
                        initPage();
                        return;
                    } else {
                        // 未授权或其他错误，跳转登录
                        console.warn('/api/user/verify 返回状态:', resp.status);
                        location.href = '/login';
                    }
                } catch (err) {
                    console.error('验证 token 时出错：', err);
                    location.href = '/login';
                }
            }

            // Called after successful token verification — 将页面交互初始化放在这里
            function initPage() {
                try { searchInput && searchInput.focus(); } catch (_) { }
                // 加载 CDK 列表并确保 UI 就绪
                loadCdkList();
                try { window.initGlobalFooter && window.initGlobalFooter(); } catch (_) { }
            }

            // ========== 加载 CDK 列表 ==========
            async function loadCdkList() {
                try {
                    var token = localStorage.getItem('kax_login_token');
                    var resp = await fetch('/api/cdk/admin/list', {
                        method: 'GET',
                        headers: { 'Authorization': 'Bearer ' + token }
                    });

                    if (resp.status === 200) {
                        allCdks = await resp.json();
                        filteredCdks = [...allCdks];
                        renderCdkList(1);
                    } else {
                        cdkList.innerHTML = '<p class="hint" style="text-align: center; padding: 20px;">加载列表失败</p>';
                    }
                } catch (err) {
                    console.error('加载 CDK 列表失败:', err);
                    cdkList.innerHTML = '<p class="hint" style="text-align: center; padding: 20px;">加载失败</p>';
                }
            }

            // ========== 搜索过滤 ==========
            function filterCdks(query) {
                if (!query.trim()) {
                    filteredCdks = [...allCdks];
                } else {
                    var q = query.toLowerCase();
                    filteredCdks = allCdks.filter(cdk => 
                        cdk.code.toLowerCase().includes(q) || 
                        (cdk.description && cdk.description.toLowerCase().includes(q)) ||
                        String(cdk.assetId).includes(q)
                    );
                }
                currentPage = 1;
                renderCdkList(1);
            }

            // ========== 渲染 CDK 列表 ==========
            function renderCdkList(page) {
                currentPage = page;
                var start = (page - 1) * itemsPerPage;
                var end = start + itemsPerPage;
                var items = filteredCdks.slice(start, end);

                cdkList.innerHTML = '';

                if (items.length === 0) {
                    cdkList.innerHTML = '<p class="hint" style="text-align: center; padding: 40px 20px;">暂无 CDK</p>';
                    pageInfo.textContent = '第 ' + page + ' 页';
                    return;
                }

                items.forEach(cdk => {
                    var item = document.createElement('div');
                    item.className = 'cdk-list-item';
                    
                    var statusBadge = cdk.isUsed 
                        ? '<span class="cdk-badge used">已使用</span>'
                        : '<span class="cdk-badge">未使用</span>';

                    item.innerHTML = `
                        <div class="cdk-text" data-full="${escapeHtml(cdk.code)}"><span class="cdk-start"></span><span class="cdk-ellipsis">...</span><span class="cdk-end"></span></div>
                        <div class="cdk-actions">
                            ${statusBadge}
                            <button class="info-btn" type="button" title="查看详情">
                                <span class="material-icons" style="font-size: 16px;">info</span>
                            </button>
                            <button class="info-btn danger-icon" type="button" title="删除 CDK" style="color: #ef4444;">
                                <span class="material-icons" style="font-size: 16px;">delete</span>
                            </button>
                        </div>
                    `; 

                    var infoBtn = item.querySelector('.info-btn');
                    infoBtn.addEventListener('click', (e) => {
                        e.stopPropagation();
                        showCdkDetail(cdk);
                    });

                    var deleteBtn = item.querySelector('.danger-icon');
                    deleteBtn.addEventListener('click', async (e) => {
                        e.stopPropagation();
                        if (!confirm('确定要删除此 CDK 吗？此操作不可恢复。')) return;
                        try {
                            deleteBtn.disabled = true;
                            await deleteCdk(cdk.code);
                        } finally {
                            deleteBtn.disabled = false;
                        }
                    });

                    cdkList.appendChild(item);
                });

                pageInfo.textContent = '第 ' + page + ' 页 / 共 ' + Math.ceil(filteredCdks.length / itemsPerPage) + ' 页';
                // 在渲染后应用中间省略逻辑（自适应）
                setTimeout(applyMiddleEllipsisAll, 0);
            }

            // 中间省略辅助函数 — 固定截断为 16 个可见字符（前 8 + 后 8），避免容器被内容撑开
            function applyMiddleEllipsis(el) {
                if (!el) return;
                var full = el.dataset.full || el.textContent || '';
                if (!full) return;
                var VISIBLE = 32; // 显示的字符数（不包括 '...'）
                if (full.length <= VISIBLE) {
                    el.textContent = full;
                    el.title = full;
                    return;
                }
                var left = Math.ceil(VISIBLE / 2); // 8
                var right = VISIBLE - left;        // 8
                var start = escapeHtml(full.slice(0, left));
                var end = escapeHtml(full.slice(-right));
                el.innerHTML = '<span class="cdk-start">' + start + '</span><span class="cdk-ellipsis">...</span><span class="cdk-end">' + end + '</span>';
                el.title = full;
            }

            function applyMiddleEllipsisAll() {
                var els = document.querySelectorAll('.cdk-text');
                els.forEach(function (el) { applyMiddleEllipsis(el); });
            }

            var resizeTimeout;
            window.addEventListener('resize', function () {
                clearTimeout(resizeTimeout);
                resizeTimeout = setTimeout(applyMiddleEllipsisAll, 120);
            });

            // ========== 显示详情面板 ==========
            function showCdkDetail(cdk) {
                var createdDate = new Date(cdk.createdAt * 1000).toLocaleString('zh-CN');
                var usedStatus = cdk.isUsed ? '已使用' : '未使用';

                detailContent.innerHTML = `
                    <div class="panel-section">
                        <div class="hint" style="font-size: 0.85rem; margin-bottom: 8px;">CDK 代码</div>
                        <pre>${escapeHtml(cdk.code)}</pre>
                    </div>
                    <div class="panel-section">
                        <div class="hint">AssetId</div>
                        <p style="margin: 6px 0; font-size: 0.95rem;">${cdk.assetId}</p>
                    </div>
                    <div class="panel-section">
                        <div class="hint">状态</div>
                        <p style="margin: 6px 0; font-size: 0.95rem;">${usedStatus}</p>
                    </div>
                    <div class="panel-section">
                        <div class="hint">创建时间</div>
                        <p style="margin: 6px 0; font-size: 0.95rem;">${createdDate}</p>
                    </div>
                    ${cdk.description ? `
                    <div class="panel-section">
                        <div class="hint">描述</div>
                        <p style="margin: 6px 0; font-size: 0.95rem;">${escapeHtml(cdk.description)}</p>
                    </div>
                    ` : ''}
                    <div class="panel-actions">
                        <button class="btn" id="copyCdkBtn" type="button">复制代码</button>
                        <button class="btn danger" id="deleteCdkBtn" type="button">删除</button>
                    </div>
                `;

                document.getElementById('copyCdkBtn').addEventListener('click', async () => {
                    try {
                        if (navigator.clipboard && navigator.clipboard.writeText) {
                            await navigator.clipboard.writeText(cdk.code);
                            alert('已复制: ' + cdk.code);
                        } else {
                            // 降级方案：使用传统的 execCommand
                            var textarea = document.createElement('textarea');
                            textarea.value = cdk.code;
                            textarea.style.position = 'fixed';
                            textarea.style.opacity = '0';
                            document.body.appendChild(textarea);
                            textarea.select();
                            document.execCommand('copy');
                            document.body.removeChild(textarea);
                            alert('已复制: ' + cdk.code);
                        }
                    } catch (err) {
                        console.error('复制失败:', err);
                        alert('复制失败，请手动复制');
                    }
                });

                document.getElementById('deleteCdkBtn').addEventListener('click', async () => {
                    if (confirm('删除此 CDK？')) {
                        await deleteCdk(cdk.code);
                        closeDetailPanel();
                    }
                });

                detailPanel.classList.add('open');
                detailBackdrop.classList.add('open');
            }

            function closeDetailPanel() {
                detailPanel.classList.remove('open');
                detailBackdrop.classList.remove('open');
            }

            function escapeHtml(text) {
                if (!text) return '';
                var map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
                return text.replace(/[&<>"']/g, m => map[m]);
            }

            // ========== 删除 CDK ==========
            async function deleteCdk(code) {
                try {
                    var token = localStorage.getItem('kax_login_token');
                    var resp = await fetch('/api/cdk/admin/delete', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                        body: JSON.stringify({ code: code })
                    });

                    if (resp.status === 200) {
                        alert('删除成功');
                        loadCdkList();
                    } else {
                        var data = await resp.json();
                        alert('删除失败: ' + (data.message || '未知错误'));
                    }
                } catch (err) {
                    console.error('删除失败:', err);
                    alert('删除失败');
                }
            }

            // Silent delete（无提示），供批量删除使用
            async function deleteCdkSilent(code) {
                try {
                    var token = localStorage.getItem('kax_login_token');
                    var resp = await fetch('/api/cdk/admin/delete', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                        body: JSON.stringify({ code: code })
                    });
                    return resp.status === 200;
                } catch (err) {
                    console.error('deleteCdkSilent error', err);
                    return false;
                }
            }

            // ========== 批量删除（删除当前筛选出的所有 CDK） ==========
            if (deleteAllBtn) {
                deleteAllBtn.addEventListener('click', async function () {
                    var count = filteredCdks.length;
                    if (count === 0) { alert('当前没有可删除的 CDK'); return; }
                    if (!confirm('确定要删除当前筛选出的 ' + count + ' 条 CDK 吗？此操作不可恢复。')) return;

                    try {
                        deleteAllBtn.disabled = true;
                        var orig = deleteAllBtn.textContent;
                        deleteAllBtn.textContent = '删除中...';

                        var token = localStorage.getItem('kax_login_token');
                        var codes = filteredCdks.map(function(c){ return c.code; });
                        var resp = await fetch('/api/cdk/admin/delete', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                            body: JSON.stringify({ codes: codes })
                        });

                        if (resp.status === 200) {
                            var data = await resp.json().catch(function(){ return {}; });
                            var removed = data.removed || 0;
                            alert('操作完成：已删除 ' + removed + ' / ' + count + ' 条 CDK');
                            loadCdkList();
                        } else {
                            var data = await resp.json().catch(function(){ return { message: '未知错误' }; });
                            alert('批量删除失败: ' + (data.message || '未知错误'));
                        }
                    } catch (err) {
                        console.error('批量删除错误：', err);
                        alert('删除过程中发生错误：' + (err.message || err));
                    } finally {
                        deleteAllBtn.disabled = false;
                        deleteAllBtn.textContent = orig;
                    }
                });
            }

            // ========== 生成 CDK ==========
            async function apiGenerate(payload) {
                var token = localStorage.getItem('kax_login_token');
                var resp = await fetch('/api/cdk/admin/generate', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                    body: JSON.stringify(payload)
                });
                if (resp.status === 200) return (await resp.json()).codes || [];
                var txt = await resp.text();
                throw new Error(txt || ('服务器返回 ' + resp.status));
            }

            generateForm.addEventListener('submit', async function (e) {
                e.preventDefault();

                var prefix = document.getElementById('prefix').value.trim();
                var count = parseInt(document.getElementById('count').value, 10) || 1;
                var length = parseInt(document.getElementById('length').value, 10) || 8;
                var assetId = parseInt(document.getElementById('assetId').value, 10) || 0;

                // 允许不填写 assetId，仅生成代码；保存时会校验 assetId 或 goldValue
                count = Math.max(1, Math.min(1000, count));
                length = Math.max(4, Math.min(256, length));

                try {
                    var generateBtn = document.querySelector('#generateForm button[type=submit]');
                    var orig = generateBtn.textContent;
                    generateBtn.disabled = true;
                    generateBtn.textContent = '生成中...';

                    var codes = await apiGenerate({ prefix: prefix, count: count, length: length });
                    resultArea.value = codes.join('\n');
                } catch (err) {
                    alert('生成失败: ' + (err.message || err));
                } finally {
                    generateBtn.disabled = false;
                    generateBtn.textContent = orig;
                }
            });

            // ========== 保存 CDK ==========
            saveBtn.addEventListener('click', async function () {
                var txt = resultArea.value || '';
                if (!txt) { alert('无生成代码可保存'); return; }
                var codes = txt.split(/\r?\n/).filter(Boolean);

                var assetId = parseInt(document.getElementById('assetId').value, 10) || 0;
                var goldValue = parseInt(document.getElementById('goldValue').value, 10) || 0;
                var expiresInValue = parseInt(document.getElementById('expiresInValue').value, 10) || 0;
                var expiresInUnit = parseInt(document.getElementById('expiresInUnit').value, 10) || 0;
                var expiresInSeconds = expiresInValue > 0 ? expiresInValue * expiresInUnit : 0;

                // 至少需要 assetId 或 goldValue
                if (assetId <= 0 && goldValue <= 0) { alert('请填写 AssetId 或金币值（goldValue），两者不能同时为空或为 0'); return; }
                var description = document.getElementById('cdkDescription').value.trim();

                try {
                    var orig = saveBtn.textContent;
                    saveBtn.disabled = true;
                    saveBtn.textContent = '保存中...';

                    var token = localStorage.getItem('kax_login_token');
                    var resp = await fetch('/api/cdk/admin/save', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                        body: JSON.stringify({ codes: codes, assetId: assetId, goldValue: goldValue, expiresInSeconds: expiresInSeconds, description: description })
                    });

                    var data = await resp.json();
                    if (resp.status === 200 || resp.status === 201) {
                        alert('已保存 ' + (data.count || codes.length) + ' 条 CDK');
                        loadCdkList();
                        resultArea.value = '';
                    } else {
                        alert('保存失败: ' + (data.message || '未知错误'));
                    }
                } catch (err) {
                    alert('保存失败: ' + err.message);
                } finally {
                    saveBtn.disabled = false;
                    saveBtn.textContent = orig;
                }
            });

            // ========== 下载 CDK ==========
            downloadBtn.addEventListener('click', function () {
                var txt = resultArea.value;
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

            // ========== 搜索去抖 ==========
            var searchTimeout;
            searchInput.addEventListener('input', function (e) {
                clearTimeout(searchTimeout);
                searchSpinner.classList.add('active');
                searchTimeout = setTimeout(function () {
                    filterCdks(e.target.value);
                    searchSpinner.classList.remove('active');
                }, 300);
            });

            // ========== 分页 ==========
            prevPageBtn.addEventListener('click', function () {
                if (currentPage > 1) renderCdkList(currentPage - 1);
            });

            nextPageBtn.addEventListener('click', function () {
                var maxPage = Math.ceil(filteredCdks.length / itemsPerPage);
                if (currentPage < maxPage) renderCdkList(currentPage + 1);
            });

            // ========== 导航 ==========
            homeBtn.addEventListener('click', function () { location.href = '/'; });
            logoutBtn.addEventListener('click', function () {
                try { localStorage.removeItem('kax_login_token'); } catch (_) { }
                location.href = '/login';
            });

            document.getElementById('closeDetailPanelBtn').addEventListener('click', closeDetailPanel);
            detailBackdrop.addEventListener('click', closeDetailPanel);

            // ========== 初始化 ==========
            verifyToken();
        })();
