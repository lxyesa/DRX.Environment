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
        if (permissionGroup === 0) return 'permission-system';
        if (permissionGroup === 2) return 'permission-console';
        if (permissionGroup === 3) return 'permission-admin';
        return 'permission-user';
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

        var rows = items.map(function (u) {
            var permissionLabel = escapeHtml(u.permissionGroupText || 'User');
            var permissionCls = permissionClass(u.permissionGroup);
            var statusLabel = u.isBanned ? '封禁中' : '正常';
            var statusCls = u.isBanned ? 'status-banned' : 'status-active';
            return '<tr>' +
                '<td>' + escapeHtml(u.id) + '</td>' +
                '<td>' + escapeHtml(u.userName || '--') + '</td>' +
                '<td>' + escapeHtml(u.displayName || '--') + '</td>' +
                '<td>' + escapeHtml(u.email || '--') + '</td>' +
                '<td><span class="manage-users-badge ' + permissionCls + '">' + permissionLabel + '</span></td>' +
                '<td><span class="manage-users-badge ' + statusCls + '">' + statusLabel + '</span></td>' +
                '<td>' + escapeHtml(formatTime(u.lastLoginAt)) + '</td>' +
                '</tr>';
        }).join('');

        userListBody.innerHTML = rows;
    }

    async function verifySystemPermission() {
        var token = localStorage.getItem('kax_login_token');
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

        var token = localStorage.getItem('kax_login_token');
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
