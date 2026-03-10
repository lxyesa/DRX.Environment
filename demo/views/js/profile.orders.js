/* 订单域模块（Task 5）
 * 目标：零行为改动承接 profile.js 中订单列表/搜索/详情弹窗逻辑。
 * 暴露：window.ProfileOrders
 */
(function registerProfileOrders(global) {
    if (global.ProfileOrders) return;

    const state = {
        ordersPage: 1,
        ordersPageSize: 10,
        initialized: false,
        deps: {
            checkToken: () => null,
            setElementDisplay: () => { },
            escapeHtml: (v) => String(v ?? '')
        }
    };

    function createOrderCard(order) {
        const ORDER_TYPES = {
            'purchase': { label: '购买资产', color: '#3b82f6', bg: 'rgba(59,130,246,0.15)' },
            'cdk': { label: 'CDK兑换', color: '#10b981', bg: 'rgba(16,185,129,0.15)' },
            'cancel_subscription': { label: '取消订阅', color: '#f59e0b', bg: 'rgba(245,158,11,0.15)' },
            'change_plan': { label: '更变计划', color: '#8b5cf6', bg: 'rgba(139,92,246,0.15)' },
            'gold_adjust': { label: '金币调整', color: '#ec4899', bg: 'rgba(236,72,153,0.15)' }
        };
        const typeInfo = ORDER_TYPES[order.orderType] ?? { label: order.orderType ?? '未知', color: '#6b7280', bg: 'rgba(107,114,128,0.15)' };

        let goldClass = 'neutral', goldText = '—';
        if (order.goldChange > 0) { goldClass = 'positive'; goldText = '+' + order.goldChange + ' 💰'; }
        else if (order.goldChange < 0) { goldClass = 'negative'; goldText = order.goldChange + ' 💰'; }

        const d = new Date(order.createdAt);
        const dateStr = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
        const timeStr = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        const assetName = order.assetName || (order.orderType === 'cdk' ? 'CDK 兑换' : order.orderType === 'gold_adjust' ? '金币调整' : '资产操作');

        const card = document.createElement('div');
        card.className = 'order-card';
        card.innerHTML = `
        <div class="order-card-stripe" style="background:${typeInfo.color};"></div>
        <div class="order-card-inner">
            <span class="order-type-badge" style="color:${typeInfo.color};background:${typeInfo.bg};">${typeInfo.label}</span>
            <div class="order-card-body">
                <div class="order-asset-name">${state.deps.escapeHtml(assetName)}</div>
                <div class="order-meta-row">
                    <span class="order-id-chip">${(order.id ?? '').substring(0, 10)}…</span>
                    <span class="order-date">${dateStr} ${timeStr}</span>
                </div>
            </div>
            <div class="order-card-right">
                <span class="order-gold-change ${goldClass}">${goldText}</span>
                <span class="order-chevron material-icons">chevron_right</span>
            </div>
        </div>`;

        card.addEventListener('click', () => showOrderDetail(order));
        return card;
    }

    function showOrderDetail(order) {
        const ORDER_TYPES = {
            'purchase': { label: '购买资产', color: '#3b82f6' },
            'cdk': { label: 'CDK兑换', color: '#10b981' },
            'cancel_subscription': { label: '取消订阅', color: '#f59e0b' },
            'change_plan': { label: '更变计划', color: '#8b5cf6' },
            'gold_adjust': { label: '金币调整', color: '#ec4899' }
        };
        const typeInfo = ORDER_TYPES[order.orderType] ?? { label: order.orderType ?? '未知', color: '#6b7280' };

        let goldClass = '', goldText = '—';
        if (order.goldChange > 0) { goldClass = 'gold-pos'; goldText = '+' + order.goldChange + ' 金币'; }
        else if (order.goldChange < 0) { goldClass = 'gold-neg'; goldText = order.goldChange + ' 金币'; }
        else { goldClass = 'muted'; goldText = '无变化'; }

        const REASON_MAP = {
            'purchase': '购买资产', 'cdk_redeem': 'CDK 兑换', 'plan_change': '更变套餐',
            'plan_extension': '续期套餐', 'admin': '管理员操作', 'refund': '退款', 'bonus': '奖励发放'
        };
        const reasonLabel = REASON_MAP[order.goldChangeReason] ?? order.goldChangeReason ?? '—';

        const renderField = (label, value, extra = '') =>
            `<div class="order-detail-field${extra}">
                <div class="order-detail-label">${label}</div>
                <div class="order-detail-value ${value.cls ?? ''}">${value.html ?? state.deps.escapeHtml(value.text ?? value)}</div>
            </div>`;

        const modal = document.createElement('div');
        modal.className = 'modal-overlay show order-detail-modal';
        modal.id = 'orderDetailModal';
        modal.innerHTML = `
        <div class="modal-card">
            <div class="order-detail-header-strip">
                <div class="order-detail-type-dot" style="background:${typeInfo.color};"></div>
                <div class="order-detail-title">${typeInfo.label}</div>
                <span class="order-detail-gold-badge ${goldClass}">${goldText}</span>
            </div>
            <div class="order-detail-grid">
                ${renderField('订单 ID', { text: order.id ?? '-', cls: 'mono' }, ' span-full')}
                ${renderField('资产名称', { text: order.assetName ?? '-' })}
                ${renderField('创建时间', { text: new Date(order.createdAt).toLocaleString() })}
                ${renderField('金币加减方式', { text: reasonLabel })}
                ${renderField('计划变更', { text: order.planTransition || '—' })}
                ${order.cdkCode ? renderField('CDK 代码', { text: order.cdkCode, cls: 'mono' }) : ''}
                ${renderField('备注', { text: order.description || '—' }, ' span-full')}
            </div>
            <div class="modal-footer" style="margin-top:20px;">
                <button class="btn" id="orderDetailClose">关闭</button>
            </div>
        </div>`;

        document.body.appendChild(modal);
        const close = () => modal.remove();
        modal.querySelector('#orderDetailClose').addEventListener('click', close);
        modal.addEventListener('click', e => { if (e.target === modal) close(); });
    }

    /**
     * 业务意图：加载用户订单列表并保持分页/筛选/空态表现一致。
     * 异常边界：401 清 token 并跳转；请求失败显示空态失败文案。
     * DOM/API 映射：
     * - API: GET /api/user/orders?page&pageSize
     * - DOM: ordersLoading/ordersEmpty/ordersList/ordersPager/ordersPageInfo
     */
    async function loadUserOrders(page = 1) {
        const token = state.deps.checkToken();
        if (!token) return;

        const ordersLoading = document.getElementById('ordersLoading');
        const ordersEmpty = document.getElementById('ordersEmpty');
        const ordersList = document.getElementById('ordersList');

        state.deps.setElementDisplay(ordersLoading, true);
        state.deps.setElementDisplay(ordersEmpty, false);
        ordersList.innerHTML = '';

        try {
            const resp = await fetch(`/api/user/orders?page=${page}&pageSize=${state.ordersPageSize}`, {
                headers: { 'Authorization': 'Bearer ' + token }
            });

            if (!resp.ok) {
                if (resp.status === 401) {
                    localStorage.removeItem('kax_login_token');
                    location.href = '/login';
                }
                throw new Error(`HTTP ${resp.status}`);
            }

            const result = await resp.json();
            state.deps.setElementDisplay(ordersLoading, false);

            if (result.code !== 0 || !result.data) {
                state.deps.setElementDisplay(ordersEmpty, true);
                return;
            }

            const orders = result.data;
            const total = result.total;
            const totalPages = Math.ceil(total / state.ordersPageSize);

            if (orders.length === 0) {
                state.deps.setElementDisplay(ordersEmpty, true);
                return;
            }

            const statusFilter = document.getElementById('orderStatusFilter')?.value || 'all';
            let filteredOrders = orders;
            if (statusFilter !== 'all') {
                filteredOrders = orders.filter(order => {
                    if (statusFilter === 'pending') return order.orderType === 'purchase';
                    if (statusFilter === 'paid') return order.orderType === 'cdk';
                    return true;
                });
            }

            if (filteredOrders.length === 0) {
                state.deps.setElementDisplay(ordersEmpty, true);
                return;
            }

            filteredOrders.forEach(order => ordersList.appendChild(createOrderCard(order)));

            document.getElementById('ordersCount').textContent = `共 ${total} 条订单`;
            state.ordersPage = page;

            const pagerEl = document.getElementById('ordersPager');
            if (totalPages > 1) {
                state.deps.setElementDisplay(pagerEl, true);
                document.getElementById('ordersPageInfo').textContent = `第 ${page} / ${totalPages} 页`;
                document.getElementById('ordersPrevBtn').disabled = page === 1;
                document.getElementById('ordersNextBtn').disabled = page === totalPages;
            } else {
                state.deps.setElementDisplay(pagerEl, false);
            }
        } catch (err) {
            console.error('加载订单失败:', err);
            state.deps.setElementDisplay(ordersLoading, false);
            state.deps.setElementDisplay(ordersEmpty, true);
            document.getElementById('ordersEmpty').querySelector('span:last-child').textContent = '加载失败，请重试';
        }
    }

    function searchOrders() {
        const keyword = document.getElementById('orderSearch')?.value?.trim() || '';
        if (!keyword) {
            loadUserOrders(1);
            return;
        }

        const token = state.deps.checkToken();
        if (!token) return;

        const ordersLoading = document.getElementById('ordersLoading');
        const ordersEmpty = document.getElementById('ordersEmpty');
        const ordersList = document.getElementById('ordersList');

        state.deps.setElementDisplay(ordersLoading, true);
        state.deps.setElementDisplay(ordersEmpty, false);
        ordersList.innerHTML = '';

        (async () => {
            try {
                const resp = await fetch('/api/user/orders?page=1&pageSize=999', { headers: { 'Authorization': 'Bearer ' + token } });
                if (!resp.ok) throw new Error(`HTTP ${resp.status}`);

                const result = await resp.json();
                state.deps.setElementDisplay(ordersLoading, false);

                if (result.code !== 0 || !result.data) {
                    state.deps.setElementDisplay(ordersEmpty, true);
                    return;
                }

                const filtered = result.data.filter(order =>
                    order.assetName?.toLowerCase().includes(keyword.toLowerCase()) ||
                    order.cdkCode?.toLowerCase().includes(keyword.toLowerCase()) ||
                    order.description?.toLowerCase().includes(keyword.toLowerCase()) ||
                    order.id?.toLowerCase().includes(keyword.toLowerCase())
                );

                if (filtered.length === 0) {
                    state.deps.setElementDisplay(ordersEmpty, true);
                    return;
                }

                filtered.slice(0, state.ordersPageSize).forEach(order => ordersList.appendChild(createOrderCard(order)));
                document.getElementById('ordersCount').textContent = `搜索结果: ${filtered.length} 条`;
                state.deps.setElementDisplay(document.getElementById('ordersPager'), false);
            } catch (err) {
                console.error('搜索失败:', err);
                state.deps.setElementDisplay(ordersLoading, false);
                state.deps.setElementDisplay(ordersEmpty, true);
            }
        })();
    }

    function bindOrderEvents() {
        const tabOrders = document.querySelector('[data-tab="orders"]');
        if (tabOrders) {
            tabOrders.addEventListener('click', () => {
                if (!window.ordersTabLoaded) {
                    window.ordersTabLoaded = true;
                    loadUserOrders(1);
                }
            });
        }

        document.getElementById('orderSearchBtn')?.addEventListener('click', searchOrders);
        document.getElementById('orderSearch')?.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') searchOrders();
        });
        document.getElementById('orderStatusFilter')?.addEventListener('change', () => {
            state.ordersPage = 1;
            loadUserOrders(1);
        });
        document.getElementById('ordersPrevBtn')?.addEventListener('click', () => {
            if (state.ordersPage > 1) loadUserOrders(state.ordersPage - 1);
        });
        document.getElementById('ordersNextBtn')?.addEventListener('click', () => {
            loadUserOrders(state.ordersPage + 1);
        });
    }

    function init(options = {}) {
        if (state.initialized) return;
        state.deps = { ...state.deps, ...options };
        bindOrderEvents();
        state.initialized = true;
    }

    global.ProfileOrders = {
        init,
        loadUserOrders,
        searchOrders,
        showOrderDetail
    };
})(window);
