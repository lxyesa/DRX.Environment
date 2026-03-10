(function () {
    'use strict';

    var tabs = Array.from(document.querySelectorAll('.dev-tab'));
    var panels = Array.from(document.querySelectorAll('.dev-tab-content'));

    if (tabs.length === 0 || panels.length === 0) return;

    var userListKeyword = document.getElementById('userListKeyword');
    var userListPermissionFilter = document.getElementById('userListPermissionFilter');
    var userListSearchBtn = document.getElementById('userListSearchBtn');
    var userListBody = document.getElementById('userListBody');
    var userListState = document.getElementById('userListState');
    var userListTableWrap = document.getElementById('userListTableWrap');
    var userListEmpty = document.getElementById('userListEmpty');
    var userListPagination = document.getElementById('userListPagination');

    var userListStateData = {
        page: 1,
        pageSize: 20,
        total: 0,
        loaded: false,
        isSystem: false
    };

    // ── 弹窗 DOM ──
    var userDetailModal = document.getElementById('userDetailModal');
    var closeUserModalBtn = document.getElementById('closeUserModal');
    var userModalTitle = document.getElementById('userModalTitle');
    var userModalSub = document.getElementById('userModalSub');
    var userModalIcon = document.getElementById('userModalIcon');
    var userModalStatusBadge = document.getElementById('userModalStatusBadge');
    var userModalInfoList = document.getElementById('userModalInfoList');
    var userModalTabs = document.getElementById('userModalTabs');
    var userModalPanels = document.getElementById('userModalPanels');
    var userModalOverview = document.getElementById('userModalOverview');
    var userModalStats = document.getElementById('userModalStats');
    var userModalActions = document.getElementById('userModalActions');
    var userModalFooterInfo = document.getElementById('userModalFooterInfo');
    var userModalBanBtn = document.getElementById('userModalBanBtn');
    var userModalUnbanBtn = document.getElementById('userModalUnbanBtn');
    var userModalForceLogoutBtn = document.getElementById('userModalForceLogoutBtn');

    // 封禁弹窗
    var userBanModal = document.getElementById('userBanModal');
    var closeUserBanModal = document.getElementById('closeUserBanModal');
    var banReasonInput = document.getElementById('banReason');
    var banDurationInput = document.getElementById('banDuration');
    var cancelBanBtn = document.getElementById('cancelBanBtn');
    var confirmBanBtn = document.getElementById('confirmBanBtn');

    var currentModalUserId = null;

    function activate(tabKey) {
        tabs.forEach(function (tab) {
            var active = tab.getAttribute('data-tab') === tabKey;
            tab.classList.toggle('active', active);
            tab.setAttribute('aria-selected', active ? 'true' : 'false');
        });

        panels.forEach(function (panel) {
            var active = panel.id === ('tab-' + tabKey);
            panel.classList.toggle('active', active);
        });
    }

    tabs.forEach(function (tab) {
        tab.addEventListener('click', function () {
            activate(tab.getAttribute('data-tab'));
        });
    });

    function escapeHtml(value) {
        return String(value == null ? '' : value)
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#39;');
    }

    function formatTime(ts) {
        if (!ts || ts <= 0) return '--';
        var ms = ts > 1e12 ? ts : ts * 1000;
        var d = new Date(ms);
        if (Number.isNaN(d.getTime())) return '--';
        var y = d.getFullYear();
        var m = String(d.getMonth() + 1).padStart(2, '0');
        var day = String(d.getDate()).padStart(2, '0');
        var hh = String(d.getHours()).padStart(2, '0');
        var mm = String(d.getMinutes()).padStart(2, '0');
        return y + '-' + m + '-' + day + ' ' + hh + ':' + mm;
    }

    function permissionClass(permissionGroup) {
        // 委托到 AuthState 统一映射，消除魔法数字
        if (window.AuthState) return window.AuthState.permissionGroupToCssClass(permissionGroup);
        if (permissionGroup === 0) return 'permission-system';
        if (permissionGroup === 2) return 'permission-console';
        if (permissionGroup === 3) return 'permission-admin';
        return 'permission-user';
    }

    function permissionText(permissionGroup) {
        // 委托到 AuthState 统一映射，消除魔法数字
        if (window.AuthState) return window.AuthState.permissionGroupToEnglishText(permissionGroup);
        if (permissionGroup === 0) return 'System';
        if (permissionGroup === 2) return 'Console';
        if (permissionGroup === 3) return 'Admin';
        return 'User';
    }

    function getAuthHeaders() {
        var token = (window.AuthState ? window.AuthState.getToken() : null) || localStorage.getItem('kax_login_token');
        return {
            'Content-Type': 'application/json',
            'Authorization': 'Bearer ' + (token || '')
        };
    }

    function setStateText(text) {
        if (!userListState) return;
        userListState.textContent = text || '';
        userListState.style.display = text ? 'block' : 'none';
    }

    function renderPagination() {
        if (!userListPagination) return;

        var totalPages = Math.max(1, Math.ceil(userListStateData.total / userListStateData.pageSize));
        var page = Math.min(userListStateData.page, totalPages);
        userListStateData.page = page;

        userListPagination.innerHTML = '';
        userListPagination.classList.add('manage-users-pagination');

        var prevBtn = document.createElement('button');
        prevBtn.className = 'btn ghost small';
        prevBtn.type = 'button';
        prevBtn.textContent = '上一页';
        prevBtn.disabled = page <= 1;
        prevBtn.addEventListener('click', function () {
            if (userListStateData.page <= 1) return;
            userListStateData.page -= 1;
            loadUserList();
        });

        var nextBtn = document.createElement('button');
        nextBtn.className = 'btn ghost small';
        nextBtn.type = 'button';
        nextBtn.textContent = '下一页';
        nextBtn.disabled = page >= totalPages;
        nextBtn.addEventListener('click', function () {
            if (userListStateData.page >= totalPages) return;
            userListStateData.page += 1;
            loadUserList();
        });

        var text = document.createElement('span');
        text.className = 'manage-users-page-text';
        text.textContent = '第 ' + page + ' / ' + totalPages + ' 页 · 共 ' + userListStateData.total + ' 条';

        userListPagination.appendChild(text);
        userListPagination.appendChild(prevBtn);
        userListPagination.appendChild(nextBtn);
    }

    function renderUsers(items) {
        if (!userListBody || !userListTableWrap || !userListEmpty) return;

        userListBody.innerHTML = '';
        if (!items || items.length === 0) {
            userListTableWrap.style.display = 'none';
            userListEmpty.style.display = 'flex';
            return;
        }

        userListTableWrap.style.display = 'block';
        userListEmpty.style.display = 'none';

        items.forEach(function (u) {
            var tr = document.createElement('tr');
            tr.setAttribute('data-user-id', u.id);

            var permissionLabel = escapeHtml(u.permissionGroupText || 'User');
            var permissionCls = permissionClass(u.permissionGroup);
            var statusLabel = u.isBanned ? '封禁中' : '正常';
            var statusCls = u.isBanned ? 'status-banned' : 'status-active';

            tr.innerHTML =
                '<td>' + escapeHtml(u.id) + '</td>' +
                '<td>' + escapeHtml(u.userName || '--') + '</td>' +
                '<td>' + escapeHtml(u.displayName || '--') + '</td>' +
                '<td>' + escapeHtml(u.email || '--') + '</td>' +
                '<td><span class="manage-users-badge ' + permissionCls + '">' + permissionLabel + '</span></td>' +
                '<td><span class="manage-users-badge ' + statusCls + '">' + statusLabel + '</span></td>' +
                '<td>' + escapeHtml(formatTime(u.lastLoginAt)) + '</td>';

            tr.addEventListener('click', function () {
                openUserDetail(u.id);
            });

            userListBody.appendChild(tr);
        });
    }

    async function verifySystemPermission() {
        var token = (window.AuthState ? window.AuthState.getToken() : null) || localStorage.getItem('kax_login_token');
        if (!token) {
            setStateText('未登录，无法访问用户管理。');
            return false;
        }

        try {
            var resp = await fetch('/api/user/verify/account', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer ' + token
                }
            });

            if (resp.status !== 200) {
                setStateText('登录态无效，请重新登录。');
                return false;
            }

            var data = await resp.json().catch(function () { return {}; });
            if (!data || data.isSystem !== true) {
                setStateText('权限不足：仅 System 用户可访问此页面。');
                return false;
            }

            userListStateData.isSystem = true;
            return true;
        } catch (_) {
            setStateText('权限校验失败，请稍后重试。');
            return false;
        }
    }

    async function loadUserList() {
        if (!userListStateData.isSystem) return;

        var token = (window.AuthState ? window.AuthState.getToken() : null) || localStorage.getItem('kax_login_token');
        if (!token) {
            setStateText('未登录，无法加载用户列表。');
            return;
        }

        var q = userListKeyword ? userListKeyword.value.trim() : '';
        var permissionGroup = userListPermissionFilter ? userListPermissionFilter.value : '';
        var query = new URLSearchParams();
        query.set('page', String(userListStateData.page));
        query.set('pageSize', String(userListStateData.pageSize));
        if (q) query.set('q', q);
        if (permissionGroup) query.set('permissionGroup', permissionGroup);

        setStateText('加载中...');
        if (userListTableWrap) userListTableWrap.style.display = 'none';
        if (userListEmpty) userListEmpty.style.display = 'none';

        try {
            var resp = await fetch('/api/system/users?' + query.toString(), {
                headers: { 'Authorization': 'Bearer ' + token }
            });

            if (resp.status === 403) {
                userListStateData.isSystem = false;
                setStateText('权限不足：仅 System 用户可访问此接口。');
                return;
            }

            if (resp.status !== 200) {
                setStateText('加载失败，请稍后重试。');
                return;
            }

            var payload = await resp.json();
            var data = (payload && payload.data) || {};
            var items = data.items || [];

            userListStateData.total = Number(data.total || 0);
            userListStateData.page = Number(data.page || userListStateData.page);
            userListStateData.pageSize = Number(data.pageSize || userListStateData.pageSize);
            userListStateData.loaded = true;

            renderUsers(items);
            renderPagination();

            if (items.length > 0) {
                setStateText('已加载 ' + items.length + ' 条，本页展示完成。');
            } else {
                setStateText('未查询到符合条件的用户。');
            }
        } catch (_) {
            setStateText('网络异常，加载失败。');
        }
    }

    if (userListSearchBtn) {
        userListSearchBtn.addEventListener('click', function () {
            userListStateData.page = 1;
            loadUserList();
        });
    }

    if (userListKeyword) {
        userListKeyword.addEventListener('keydown', function (e) {
            if (e.key !== 'Enter') return;
            userListStateData.page = 1;
            loadUserList();
        });
    }

    if (userListPermissionFilter) {
        userListPermissionFilter.addEventListener('change', function () {
            userListStateData.page = 1;
            loadUserList();
        });
    }

    // ================================================================
    //  用户详情弹窗
    // ================================================================

    function showModal(overlay) {
        if (overlay) overlay.style.display = 'flex';
    }

    function hideModal(overlay) {
        if (overlay) overlay.style.display = 'none';
    }

    // 弹窗 Tab 切换
    if (userModalTabs) {
        userModalTabs.addEventListener('click', function (e) {
            var btn = e.target.closest('.rv-tab');
            if (!btn) return;
            var panel = btn.getAttribute('data-panel');
            userModalTabs.querySelectorAll('.rv-tab').forEach(function (t) {
                t.classList.toggle('active', t === btn);
            });
            userModalPanels.querySelectorAll('.rv-panel').forEach(function (p) {
                p.classList.toggle('active', p.getAttribute('data-panel') === panel);
            });
        });
    }

    // 关闭弹窗
    if (closeUserModalBtn) {
        closeUserModalBtn.addEventListener('click', function () { hideModal(userDetailModal); });
    }
    if (userDetailModal) {
        userDetailModal.addEventListener('click', function (e) {
            if (e.target === userDetailModal) hideModal(userDetailModal);
        });
    }

    async function openUserDetail(userId) {
        if (!userId) return;
        currentModalUserId = userId;

        // 重置到概要 Tab
        if (userModalTabs) {
            userModalTabs.querySelectorAll('.rv-tab').forEach(function (t, i) {
                t.classList.toggle('active', i === 0);
            });
        }
        if (userModalPanels) {
            userModalPanels.querySelectorAll('.rv-panel').forEach(function (p, i) {
                p.classList.toggle('active', i === 0);
            });
        }

        // 显示加载态
        if (userModalTitle) userModalTitle.textContent = '加载中…';
        if (userModalSub) userModalSub.textContent = '';
        if (userModalInfoList) userModalInfoList.innerHTML = '<div style="padding:20px;color:var(--dev-text-muted);font-size:0.84rem;">正在获取用户信息…</div>';
        if (userModalOverview) userModalOverview.innerHTML = '';
        if (userModalStats) userModalStats.innerHTML = '';
        if (userModalActions) userModalActions.innerHTML = '';
        if (userModalStatusBadge) userModalStatusBadge.innerHTML = '';
        if (userModalFooterInfo) userModalFooterInfo.textContent = '';
        if (userModalBanBtn) userModalBanBtn.style.display = 'none';
        if (userModalUnbanBtn) userModalUnbanBtn.style.display = 'none';

        showModal(userDetailModal);

        try {
            var resp = await fetch('/api/system/user/' + encodeURIComponent(userId), {
                headers: getAuthHeaders()
            });

            if (resp.status !== 200) {
                if (userModalInfoList) userModalInfoList.innerHTML = '<div style="padding:20px;color:#f87171;">获取失败（HTTP ' + resp.status + '）</div>';
                return;
            }

            var payload = await resp.json();
            var u = (payload && payload.data) || {};
            renderUserModal(u);
        } catch (err) {
            if (userModalInfoList) userModalInfoList.innerHTML = '<div style="padding:20px;color:#f87171;">网络异常</div>';
        }
    }

    function renderUserModal(u) {
        // 头部
        if (userModalTitle) userModalTitle.textContent = u.displayName || u.userName || '--';
        if (userModalSub) userModalSub.textContent = '@' + (u.userName || '--') + '  ·  ID ' + u.id;

        // 状态徽标
        if (userModalStatusBadge) {
            if (u.isBanned) {
                userModalStatusBadge.innerHTML = '<span class="manage-users-badge status-banned">封禁中</span>';
            } else {
                userModalStatusBadge.innerHTML = '<span class="manage-users-badge status-active">正常</span>';
            }
        }

        // 左侧信息
        if (userModalInfoList) {
            var infoHtml = '';
            infoHtml += infoItem('ID', u.id);
            infoHtml += infoItem('邮箱', u.email || '--');
            infoHtml += infoItem('邮箱验证', u.emailVerified ? '✓ 已验证' : '✗ 未验证');
            infoHtml += infoItem('权限组', '<span class="manage-users-badge ' + permissionClass(u.permissionGroup) + '">' + escapeHtml(u.permissionGroupText || permissionText(u.permissionGroup)) + '</span>');
            infoHtml += infoItem('注册时间', formatTime(u.registeredAt));
            infoHtml += infoItem('最近登录', formatTime(u.lastLoginAt));
            userModalInfoList.innerHTML = infoHtml;
        }

        // 底栏
        if (userModalFooterInfo) {
            userModalFooterInfo.innerHTML =
                '<span class="material-icons">person</span>' +
                escapeHtml(u.userName || '--') + ' · ID ' + u.id;
        }

        // 封禁/解封按钮
        if (userModalBanBtn) userModalBanBtn.style.display = u.isBanned ? 'none' : '';
        if (userModalUnbanBtn) userModalUnbanBtn.style.display = u.isBanned ? '' : 'none';

        // ── 概要面板 ──
        if (userModalOverview) {
            var overviewHtml = '';

            if (u.isBanned) {
                overviewHtml += '<div class="user-ban-banner"><span class="material-icons">gpp_bad</span><p>';
                overviewHtml += '<b>账号封禁中</b>';
                if (u.banReason) overviewHtml += '<br>原因：' + escapeHtml(u.banReason);
                if (u.bannedAt) overviewHtml += '<br>封禁时间：' + escapeHtml(formatTime(u.bannedAt));
                if (u.banExpiresAt > 0) overviewHtml += '<br>到期时间：' + escapeHtml(formatTime(u.banExpiresAt));
                else overviewHtml += '<br>到期时间：永久';
                overviewHtml += '</p></div>';
            }

            // 个人简介
            overviewHtml += '<div class="user-overview-section">';
            overviewHtml += '<div class="user-overview-section-title">个人简介</div>';
            overviewHtml += '<div class="user-overview-bio">' + escapeHtml(u.bio || '暂无简介') + '</div>';
            overviewHtml += '</div>';

            // 签名
            if (u.signature) {
                overviewHtml += '<div class="user-overview-section">';
                overviewHtml += '<div class="user-overview-section-title">签名</div>';
                overviewHtml += '<div class="user-overview-bio">' + escapeHtml(u.signature) + '</div>';
                overviewHtml += '</div>';
            }

            // 徽章
            overviewHtml += '<div class="user-overview-section">';
            overviewHtml += '<div class="user-overview-section-title">徽章</div>';
            overviewHtml += renderBadges(u.badges);
            overviewHtml += '</div>';

            userModalOverview.innerHTML = overviewHtml;
        }

        // ── 数据面板 ──
        if (userModalStats) {
            userModalStats.innerHTML = '<div class="user-stats-grid">' +
                statCard('金币', u.gold) +
                statCard('资源数', u.resourceCount) +
                statCard('近期活动', u.recentActivity) +
                statCard('激活资产', u.activeAssetsCount) +
                statCard('收藏', u.favoriteAssetsCount) +
                statCard('购物车', u.cartItemsCount) +
                statCard('订单记录', u.orderRecordsCount) +
                statCard('Token 基线', u.tokenInvalidBefore > 0 ? formatTime(u.tokenInvalidBefore) : '无') +
                '</div>';
        }

        // ── 操作面板 ──
        if (userModalActions) {
            var actionsHtml = '';
            var permissionValue = [0, 2, 3, 999].indexOf(Number(u.permissionGroup)) >= 0
                ? Number(u.permissionGroup)
                : 999;
            var permissionOptions = JSON.stringify([
                { value: '0', label: 'System' },
                { value: '2', label: 'Console' },
                { value: '3', label: 'Admin' },
                { value: '999', label: 'User' }
            ]);

            // 权限变更
            actionsHtml += '<div class="user-action-group">';
            actionsHtml += '<div class="user-action-group-title">权限管理</div>';
            actionsHtml += '<div class="user-action-row">';
            actionsHtml += '<select-box id="actionPermissionSelect" class="user-action-permission-select" size="small-headerless" icon="admin_panel_settings" value="' + permissionValue + '" options=\'' + escapeHtml(permissionOptions) + '\'></select-box>';
            actionsHtml += '<action-btn id="actionPermissionBtn" height="36" class="user-action-btn" label="变更权限"></action-btn>';
            actionsHtml += '</div></div>';

            // 金币调整
            actionsHtml += '<div class="user-action-group">';
            actionsHtml += '<div class="user-action-group-title">金币调整</div>';
            actionsHtml += '<div class="user-action-row">';
            actionsHtml += '<input-box id="actionGoldAmount" class="user-action-gold-amount" size="small-headerless" type="number" placeholder="正数增加 / 负数扣减"></input-box>';
            actionsHtml += '<input-box id="actionGoldReason" class="user-action-gold-reason" size="small-headerless" placeholder="调整原因"></input-box>';
            actionsHtml += '<action-btn id="actionGoldBtn" height="36" class="user-action-btn" label="调整金币"></action-btn>';
            actionsHtml += '</div></div>';

            // 强制登出
            actionsHtml += '<div class="user-action-group">';
            actionsHtml += '<div class="user-action-group-title">会话管理</div>';
            actionsHtml += '<div class="user-action-row">';
            actionsHtml += '<action-btn id="actionForceLogoutBtn" height="36" class="user-action-btn" label="强制登出所有会话" icon="logout"></action-btn>';
            actionsHtml += '</div></div>';

            userModalActions.innerHTML = actionsHtml;

            // 绑定操作面板内按钮事件
            bindActionPanelEvents(u.id);
        }
    }

    function infoItem(label, value) {
        return '<div class="rv-info-item"><span class="rv-info-label">' + escapeHtml(label) + '</span><span class="rv-info-value">' + value + '</span></div>';
    }

    function statCard(label, value) {
        return '<div class="user-stat-card"><span class="user-stat-label">' + escapeHtml(label) + '</span><span class="user-stat-value">' + escapeHtml(String(value ?? 0)) + '</span></div>';
    }

    function renderBadges(badgesInput) {
        if (!badgesInput) return '<span style="color:var(--dev-text-subtle);font-size:0.84rem;">暂无徽章</span>';

        var badges = [];
        if (Array.isArray(badgesInput)) {
            badges = badgesInput;
        } else if (typeof badgesInput === 'string') {
            try {
                badges = JSON.parse(badgesInput);
            } catch (_) {
                // 兼容旧格式 badge1[r,g,b];badge2[r,g,b]
                var parts = badgesInput.split(';').filter(Boolean);
                parts.forEach(function (part) {
                    var match = part.match(/^(.+?)\[(\d+),(\d+),(\d+)\]$/);
                    if (match) {
                        badges.push({ text: match[1], color: [parseInt(match[2]), parseInt(match[3]), parseInt(match[4])] });
                    }
                });
            }
        }

        if (!badges.length) return '<span style="color:var(--dev-text-subtle);font-size:0.84rem;">暂无徽章</span>';

        var html = '<div class="user-overview-badges">';
        badges.forEach(function (b) {
            var color = Array.isArray(b.color) ? 'rgb(' + b.color.join(',') + ')' : 'var(--dev-accent)';
            html += '<span class="user-overview-badge" style="color:' + color + ';background:rgba(' +
                (Array.isArray(b.color) ? b.color.join(',') : '99,140,255') + ',0.14);border-color:rgba(' +
                (Array.isArray(b.color) ? b.color.join(',') : '99,140,255') + ',0.3);">' + escapeHtml(b.text) + '</span>';
        });
        html += '</div>';
        return html;
    }

    function bindActionPanelEvents(userId) {
        var permissionBtn = document.getElementById('actionPermissionBtn');
        var goldBtn = document.getElementById('actionGoldBtn');
        var forceLogoutBtn = document.getElementById('actionForceLogoutBtn');

        if (permissionBtn) {
            permissionBtn.addEventListener('click', async function () {
                var select = document.getElementById('actionPermissionSelect');
                if (!select) return;
                var val = parseInt(select.value, 10);
                if (isNaN(val)) return;

                permissionBtn.disabled = true;
                try {
                    var resp = await fetch('/api/system/user/' + encodeURIComponent(userId) + '/permission', {
                        method: 'POST',
                        headers: getAuthHeaders(),
                        body: JSON.stringify({ permissionGroup: val })
                    });
                    var r = await resp.json().catch(function () { return {}; });
                    alert(r.message || (resp.ok ? '成功' : '失败'));
                    if (resp.ok) { openUserDetail(userId); loadUserList(); }
                } finally { permissionBtn.disabled = false; }
            });
        }

        if (goldBtn) {
            goldBtn.addEventListener('click', async function () {
                var amountEl = document.getElementById('actionGoldAmount');
                var reasonEl = document.getElementById('actionGoldReason');
                var amount = parseInt(amountEl ? amountEl.value : '0', 10);
                if (!amount || isNaN(amount)) { alert('请输入有效的金币数量'); return; }
                var reason = reasonEl ? reasonEl.value.trim() : '';

                goldBtn.disabled = true;
                try {
                    var resp = await fetch('/api/system/user/' + encodeURIComponent(userId) + '/gold', {
                        method: 'POST',
                        headers: getAuthHeaders(),
                        body: JSON.stringify({ amount: amount, reason: reason || undefined })
                    });
                    var r = await resp.json().catch(function () { return {}; });
                    alert(r.message || (resp.ok ? '成功' : '失败'));
                    if (resp.ok) { openUserDetail(userId); loadUserList(); }
                } finally { goldBtn.disabled = false; }
            });
        }

        if (forceLogoutBtn) {
            forceLogoutBtn.addEventListener('click', function () {
                doForceLogout(userId);
            });
        }
    }

    // ── 底栏快捷操作 ──

    // 封禁按钮 → 打开封禁弹窗
    if (userModalBanBtn) {
        userModalBanBtn.addEventListener('click', function () {
            if (banReasonInput) banReasonInput.value = '';
            if (banDurationInput) banDurationInput.value = '';
            showModal(userBanModal);
        });
    }

    // 封禁弹窗关闭
    if (closeUserBanModal) closeUserBanModal.addEventListener('click', function () { hideModal(userBanModal); });
    if (cancelBanBtn) cancelBanBtn.addEventListener('click', function () { hideModal(userBanModal); });
    if (userBanModal) userBanModal.addEventListener('click', function (e) { if (e.target === userBanModal) hideModal(userBanModal); });

    // 确认封禁
    if (confirmBanBtn) {
        confirmBanBtn.addEventListener('click', async function () {
            if (!currentModalUserId) return;
            var reason = banReasonInput ? banReasonInput.value.trim() : '';
            var durationHours = parseInt(banDurationInput ? banDurationInput.value : '0', 10) || 0;

            confirmBanBtn.disabled = true;
            try {
                var resp = await fetch('/api/system/user/' + encodeURIComponent(currentModalUserId) + '/ban', {
                    method: 'POST',
                    headers: getAuthHeaders(),
                    body: JSON.stringify({ reason: reason, durationHours: durationHours })
                });
                var r = await resp.json().catch(function () { return {}; });
                alert(r.message || (resp.ok ? '成功' : '失败'));
                if (resp.ok) {
                    hideModal(userBanModal);
                    openUserDetail(currentModalUserId);
                    loadUserList();
                }
            } finally { confirmBanBtn.disabled = false; }
        });
    }

    // 解封
    if (userModalUnbanBtn) {
        userModalUnbanBtn.addEventListener('click', async function () {
            if (!currentModalUserId) return;
            if (!confirm('确认解封该用户？')) return;

            userModalUnbanBtn.disabled = true;
            try {
                var resp = await fetch('/api/system/user/' + encodeURIComponent(currentModalUserId) + '/unban', {
                    method: 'POST',
                    headers: getAuthHeaders(),
                    body: '{}'
                });
                var r = await resp.json().catch(function () { return {}; });
                alert(r.message || (resp.ok ? '成功' : '失败'));
                if (resp.ok) { openUserDetail(currentModalUserId); loadUserList(); }
            } finally { userModalUnbanBtn.disabled = false; }
        });
    }

    // 底栏强制登出
    if (userModalForceLogoutBtn) {
        userModalForceLogoutBtn.addEventListener('click', function () {
            if (currentModalUserId) doForceLogout(currentModalUserId);
        });
    }

    async function doForceLogout(userId) {
        if (!confirm('确认强制登出该用户所有会话？')) return;

        try {
            var resp = await fetch('/api/system/user/' + encodeURIComponent(userId) + '/force-logout', {
                method: 'POST',
                headers: getAuthHeaders(),
                body: '{}'
            });
            var r = await resp.json().catch(function () { return {}; });
            alert(r.message || (resp.ok ? '成功' : '失败'));
            if (resp.ok) openUserDetail(userId);
        } catch (_) {
            alert('网络异常');
        }
    }

    // Esc 关闭弹窗
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        if (userBanModal && userBanModal.style.display !== 'none') {
            hideModal(userBanModal);
        } else if (userDetailModal && userDetailModal.style.display !== 'none') {
            hideModal(userDetailModal);
        }
    });

    // ── 初始化 ──
    (async function initUserList() {
        var ok = await verifySystemPermission();
        if (!ok) {
            if (userListEmpty) {
                userListEmpty.style.display = 'flex';
                userListEmpty.querySelector('h3').textContent = '无法访问';
                userListEmpty.querySelector('p').textContent = '当前账号没有 System 权限，无法查看用户列表。';
            }
            return;
        }
        loadUserList();
    })();
})();
