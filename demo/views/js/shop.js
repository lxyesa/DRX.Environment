/* ================================================================
         *  ShopApp — 商城页面核心控制器
         *  架构分层（单一新契约主路径）：
         *    • Auth      — 登录令牌与请求头
         *    • Request   — 与后端通信，处理传输层（不做字段兜底）
         *    • Mapping   — DTO -> ViewModel（ShopAssetCardVM）唯一转换入口
         *    • State     — 页面状态管理（ViewModel 列表、筛选、分页、用户数据）
         *    • Renderer  — DOM 渲染，仅依赖 ViewModel 字段
         *    • Actions   — 用户交互（加购、收藏、翻页、搜索）
         *
         *  ViewModel 字段（ShopAssetCardVM）：
         *    assetId, displayName, category, coverImage, priceYuan,
         *    authorName, purchaseCount, favoriteCount, description, lastUpdatedAt, tags
         * ================================================================ */
        const ShopApp = (() => {
            const ITEMS_PER_PAGE = 12;
            const TOKEN_KEY = 'kax_login_token';
            const LOCAL_CART_KEY = 'kax_cart';

            // #region ErrorHandling —— 错误处理委托给 ErrorPresenter (core/error-presenter.js)

            /**
             * 显示空态界面
             * @param {string} [message] - 自定义空态消息
             */
            function showEmptyState(message = '暂无数据') {
                const grid = document.getElementById('productGrid');
                const empty = document.getElementById('emptyState');
                if (grid) grid.innerHTML = '';
                if (empty) {
                    empty.style.display = 'block';
                    const emptyText = empty.querySelector('.empty-message, p');
                    if (emptyText) emptyText.textContent = message;
                }
            }

            /**
             * 显示列表加载失败态，支持重试
             * @param {string} message - 错误消息
             */
            function showListError(message) {
                const grid = document.getElementById('productGrid');
                const empty = document.getElementById('emptyState');
                const pagination = document.getElementById('pagination');
                if (empty) empty.style.display = 'none';
                if (pagination) pagination.innerHTML = '';
                if (grid) {
                    grid.innerHTML = `
                        <div class="list-error-state" style="grid-column: 1 / -1; display: flex; flex-direction: column; align-items: center; justify-content: center; min-height: 260px; gap: 16px; color: var(--text-muted, #888);">
                            <span class="material-icons" style="font-size: 48px; opacity: 0.4;">error_outline</span>
                            <div style="font-size: 16px; font-weight: 500;">${escapeHtml(message)}</div>
                            <button onclick="ShopApp.retryLoad()" style="margin-top: 8px; padding: 8px 24px; border-radius: 8px; background: var(--accent, #638cff); color: #fff; border: none; cursor: pointer; font-size: 14px;">
                                <span class="material-icons" style="font-size: 16px; vertical-align: middle; margin-right: 4px;">refresh</span>重试
                            </button>
                        </div>`;
                }
            }

            // #endregion

            // #region State —— 页面状态
            const state = {
                products: [],          // ShopAssetCardVM[]
                filteredProducts: [],  // ShopAssetCardVM[]（经筛选+排序后）
                currentPage: 1,
                userFavorites: new Set(),  // Set<number> — assetId 集合
                userCartIds: new Set()     // Set<number> — assetId 集合
            };
            // #endregion

            // #region Auth —— 登录令牌（委托给 AuthState / ApiClient）
            /** Returns active token. Delegates to AuthState when loaded, else localStorage fallback. */
            function getToken() {
                if (window.AuthState && typeof window.AuthState.getToken === 'function') {
                    return window.AuthState.getToken();
                }
                return localStorage.getItem('kax_web_token') || localStorage.getItem(TOKEN_KEY) || null;
            }

            function requireLogin(featureName) {
                const token = getToken();
                if (!token) {
                    window.ErrorPresenter
                        ? window.ErrorPresenter.notifyError(401)
                        : (alert(`请先登录以使用${featureName}功能`), location.href = '/login');
                    return false;
                }
                return true;
            }
            // #endregion

            // #region Utils —— 通用工具函数（格式化函数委托给 shared/utils.js）

            /** HTML 转义（委托 ShopUtils.escapeHtml） */
            const escapeHtml = ShopUtils.escapeHtml;

            /** 从产品列表中提取唯一的分类并排序 */
            function extractCategories() {
                const categories = new Set();
                state.products.forEach(p => {
                    if (p.category && p.category.trim()) {
                        categories.add(p.category.trim());
                    }
                });
                return Array.from(categories).sort();
            }
            // #endregion

            // #region Mapping —— DTO -> ViewModel（唯一映射入口）

            /**
             * 将 GET /api/asset/list 返回的单个条目映射为 ShopAssetCardVM。
             * 严格按附录 A 字段对应；不做字段猜测兜底。
             * @param {object} a — AssetListItem DTO
             * @returns {ShopAssetCardVM}
             */
            function mapListItem(a) {
                const normalizedDescription = String(a.description ?? '').replace(/\s+/g, ' ').trim();
                const rawTags = String(a.tags ?? '');
                const parsedTags = rawTags ? rawTags.split(/[,;，；]/).map(t => t.trim()).filter(Boolean) : [];
                return {
                    assetId: Number(a.id),
                    displayName: a.name || '',
                    category: a.category || '',
                    coverImage: a.coverImage || '',
                    iconImage: a.iconImage || '',
                    priceYuan: typeof a.priceYuan === 'number' ? a.priceYuan : 0,
                    authorName: a.authorName || '',
                    purchaseCount: a.purchaseCount || 0,
                    favoriteCount: a.favoriteCount || 0,
                    description: normalizedDescription,
                    lastUpdatedAt: a.lastUpdatedAt || 0,
                    tags: parsedTags
                };
            }
            // #endregion

            // #region Request —— 请求层（传输处理，不做业务字段兜底）

            /** 拉取资产列表并通过映射层转换为 ViewModel 数组 */
            async function fetchAssets() {
                try {
                    const body = await ApiClient.requestJson('/api/asset/list?page=1&pageSize=200');
                    if (body.code !== 0 && body.code !== undefined) {
                        console.warn('[Request] 获取资产列表业务错误, code:', body.code);
                        showListError(ErrorPresenter.resolveError(body.code, body.message).message);
                        return;
                    }
                    const items = body?.data?.items ?? [];
                    if (items.length === 0) {
                        showEmptyState('暂无商品，请稍后再来');
                        return;
                    }
                    state.products = items.map(mapListItem);
                    state.filteredProducts = [...state.products];
                } catch (e) {
                    console.error('[Request] 拉取资产出错', e);
                    const code = (e && e.apiCode) ? e.apiCode : 'UNKNOWN';
                    showListError(ErrorPresenter.resolveError(code).message);
                }
            }

            /** 拉取当前登录用户的收藏与购物车状态 */
            async function fetchUserState() {
                if (!getToken()) return;

                try {
                    const [favBody, cartBody] = await Promise.all([
                        ApiClient.requestJson('/api/user/favorites'),
                        ApiClient.requestJson('/api/user/cart')
                    ]);

                    // favorites: number[] — 直接是 assetId 数组
                    const ids = Array.isArray(favBody?.data) ? favBody.data : [];
                    state.userFavorites = new Set(ids.map(Number).filter(n => !isNaN(n)));

                    // cart: CartItem[] — 每项含 assetId 字段
                    const cartItems = Array.isArray(cartBody?.data) ? cartBody.data : [];
                    state.userCartIds = new Set(
                        cartItems.map(ci => Number(ci.assetId)).filter(n => !isNaN(n))
                    );
                } catch (e) {
                    // 401 由 ApiClient 自动处理重定向；其他错误静默降级
                    console.warn('[Request] 获取用户收藏/购物车失败', e);
                }
            }

            /** 向后端发送购物车加入请求，返回 { success, code?, message? } */
            async function apiAddToCart(_token, assetId) {
                try {
                    const body = await ApiClient.requestJsonPost('/api/user/cart', { assetId });
                    if (body.code !== 0 && body.code !== undefined) {
                        return { success: false, code: body.code, message: body.message };
                    }
                    return { success: true, assetId };
                } catch (e) {
                    console.error('[Request] 加入购物车异常', e);
                    return { success: false, code: (e && e.apiCode) || 'NETWORK_ERROR' };
                }
            }

            /** 向后端发送购物车移除请求，返回 { success, code? } */
            async function apiRemoveFromCart(_token, assetId) {
                try {
                    await ApiClient.request(`/api/user/cart/${assetId}`, { method: 'DELETE' });
                    return { success: true };
                } catch (e) {
                    console.error('[Request] 移除购物车异常', e);
                    return { success: false, code: (e && e.apiCode) || 'NETWORK_ERROR' };
                }
            }

            /** 向后端发送收藏 / 取消收藏请求，返回 { success, code?, message? } */
            async function apiToggleFavorite(_token, assetId, shouldAdd) {
                try {
                    if (shouldAdd) {
                        const body = await ApiClient.requestJsonPost('/api/user/favorites', { assetId });
                        if (body.code !== 0 && body.code !== undefined) {
                            return { success: false, code: body.code, message: body.message };
                        }
                        return { success: true };
                    }
                    await ApiClient.request(`/api/user/favorites/${assetId}`, { method: 'DELETE' });
                    return { success: true };
                } catch (e) {
                    console.error('[Request] 收藏操作异常', e);
                    return { success: false, code: (e && e.apiCode) || 'NETWORK_ERROR' };
                }
            }
            // #endregion

            // #region Renderer —— DOM 渲染（仅依赖 ShopAssetCardVM 字段）

            /** 渲染骨架屏占位卡片，count 为卡片数量 */
            function renderSkeletons(count = 12) {
                const grid = document.getElementById('productGrid');
                const empty = document.getElementById('emptyState');
                if (empty) empty.style.display = 'none';

                grid.innerHTML = Array.from({ length: count }, () => `
                <div class="skeleton-card" aria-hidden="true">
                    <div class="skeleton-image"></div>
                    <div class="skeleton-content">
                        <div class="skeleton-line title"></div>
                        <div class="skeleton-line desc"></div>
                        <div class="skeleton-line desc2"></div>
                        <div class="skeleton-line price"></div>
                        <div class="skeleton-line meta"></div>
                        <div class="skeleton-line actions"></div>
                    </div>
                </div>`).join('');
            }

            /** 填充分类下拉选项 */
            function renderCategories() {
                const el = document.getElementById('categorySelect');
                if (!el) return;

                const categories = extractCategories();
                el.innerHTML = '<option value="">全部分类</option>'
                    + categories.map(cat => `<option value="${cat}">${cat}</option>`).join('');
            }

            /** 渲染产品网格 */
            function renderProducts() {
                const grid = document.getElementById('productGrid');
                const empty = document.getElementById('emptyState');
                const paginationEl = document.getElementById('pagination');

                if (state.filteredProducts.length === 0) {
                    grid.innerHTML = '';
                    empty.style.display = 'block';
                    paginationEl.innerHTML = '';
                    return;
                }
                empty.style.display = 'none';

                const start = (state.currentPage - 1) * ITEMS_PER_PAGE;
                const pageProducts = state.filteredProducts.slice(start, start + ITEMS_PER_PAGE);

                const token = getToken();
                const localCartIds = token ? new Set() : getLocalCartIds();

                grid.innerHTML = pageProducts.map(vm => buildProductCard(vm, token, localCartIds)).join('');
                renderPagination();
            }

            /** 获取本地 localStorage 购物车中的 assetId 集合（未登录时使用） */
            function getLocalCartIds() {
                try {
                    const cart = JSON.parse(localStorage.getItem(LOCAL_CART_KEY) || '[]');
                    return new Set(cart.map(ci => Number(ci.assetId || ci.id)).filter(n => !isNaN(n)));
                } catch { return new Set(); }
            }

            /** 构建单张产品卡片 HTML（严格使用 ShopAssetCardVM 字段） */
            function buildProductCard(vm, token, localCartIds) {
                const isFav = state.userFavorites.has(vm.assetId);
                const inCart = token
                    ? state.userCartIds.has(vm.assetId)
                    : localCartIds.has(vm.assetId);

                const favIcon = isFav ? 'favorite' : 'favorite_border';
                const favClass = isFav ? ' active' : '';
                const favAria = isFav ? ' aria-pressed="true"' : '';
                const cartClass = inCart ? ' active has-count' : '';
                const cartIcon = inCart ? 'shopping_cart' : 'add_shopping_cart';
                const cartTitle = inCart ? '已在购物车中，点击可移除' : '加入购物车';
                const authorText = escapeHtml(vm.authorName || '未知作者');
                const imageUrl = vm.coverImage || '';
                const iconUrl = vm.iconImage || vm.coverImage || '';
                const descText = escapeHtml(vm.description || '暂无描述');
                const ctaLabel = vm.priceYuan === 0 ? '免费获取' : `¥${vm.priceYuan.toFixed(2)}`;

                const iconHtml = iconUrl
                    ? `
                        <span class="pc-icon-backdrop" aria-hidden="true">
                            <img class="pc-icon-copy" src="${escapeHtml(iconUrl)}" alt="" loading="lazy">
                        </span>
                        <span class="pc-icon-fill" aria-hidden="true"></span>
                        <img class="pc-icon" src="${escapeHtml(iconUrl)}" alt="" aria-hidden="true" loading="lazy">
                    `
                    : `<span class="pc-icon-placeholder">📦</span>`;

                const cardAmbientHtml = iconUrl
                    ? `
                        <span class="pc-card-ambient" aria-hidden="true">
                            <img class="pc-card-ambient-copy" src="${escapeHtml(iconUrl)}" alt="" loading="lazy">
                            <span class="pc-card-ambient-fill"></span>
                        </span>
                    `
                    : '';

                const coverHtml = imageUrl
                    ? `<img class="product-cover" src="${escapeHtml(imageUrl)}" alt="${escapeHtml(vm.displayName)}" loading="lazy">`
                    : `<span class="pc-cover-empty">📦</span>`;

                const tagHtml = vm.category
                    ? `<span class="pc-tag">${escapeHtml(vm.category)}</span>`
                    : '';

                const MAX_VISIBLE_TAGS = 6;
                let cardTagBadgesHtml = '';
                if (vm.tags && vm.tags.length > 0) {
                    const visible = vm.tags.slice(0, MAX_VISIBLE_TAGS);
                    const extra = vm.tags.length - MAX_VISIBLE_TAGS;
                    const badgesHtml = visible.map(t => `<span class="pc-tag-badge">${escapeHtml(t)}</span>`).join('');
                    const moreHtml = extra > 0
                        ? `<span class="pc-tag-more" onclick="event.stopPropagation();ShopApp.expandTags(this, ${vm.assetId})">更多${extra}个</span>`
                        : '';
                    cardTagBadgesHtml = `<div class="pc-card-tag-row"><div class="pc-tag-badges" data-collapsed="true" data-all='${escapeHtml(JSON.stringify(vm.tags))}'>${badgesHtml}${moreHtml}</div></div>`;
                }

                return `
                <div class="product-card" role="button" tabindex="0" onclick="ShopApp.viewDetail(${vm.assetId})" onkeydown="if(event.key==='Enter'||event.key===' '){event.preventDefault();ShopApp.viewDetail(${vm.assetId});}">
                    ${cardAmbientHtml}
                    <div class="pc-header">
                        <div class="pc-icon-wrap">${iconHtml}</div>
                        <div class="pc-title-block">
                            <div class="pc-name">${escapeHtml(vm.displayName)}</div>
                            <div class="pc-author">${authorText}</div>
                        </div>
                        <div class="pc-header-actions">
                            <button class="btn icon${favClass}" onclick="event.stopPropagation();ShopApp.toggleFavorite(this, ${vm.assetId})" title="收藏" data-asset-id="${vm.assetId}"${favAria}>
                                <span class="material-icons">${favIcon}</span>
                            </button>
                            <button class="btn icon cart-icon${cartClass}" onclick="event.stopPropagation();ShopApp.toggleCart(${vm.assetId})" title="${cartTitle}" data-asset-id="${vm.assetId}">
                                <span class="material-icons">${cartIcon}</span>
                            </button>
                            <span class="pc-price-badge">${ctaLabel}</span>
                        </div>
                    </div>
                    ${tagHtml ? `<div class="pc-tags">${tagHtml}</div>` : ''}
                    <div class="pc-desc">${descText}</div>
                    ${cardTagBadgesHtml}
                    <div class="pc-cover-wrap">${coverHtml}</div>
                </div>`;
            }

            /** 渲染分页按钮 */
            function renderPagination() {
                const el = document.getElementById('pagination');
                const totalPages = Math.ceil(state.filteredProducts.length / ITEMS_PER_PAGE);

                if (totalPages <= 1) { el.innerHTML = ''; return; }

                el.innerHTML = Array.from({ length: totalPages }, (_, i) => {
                    const page = i + 1;
                    const active = page === state.currentPage ? ' active' : '';
                    return `<button class="btn${active}" onclick="ShopApp.goToPage(${page})">${page}</button>`;
                }).join('');
            }

            /** 更新购物车悬浮按钮上的计数 */
            function updateCartBadge() {
                const el = document.getElementById('cartCount');
                if (!el) return;

                if (getToken()) {
                    el.textContent = state.userCartIds.size || 0;
                    return;
                }
                try {
                    const cart = JSON.parse(localStorage.getItem(LOCAL_CART_KEY) || '[]');
                    el.textContent = cart.reduce((sum, item) => sum + (item.quantity || 1), 0);
                } catch { el.textContent = 0; }
            }
            // #endregion

            // #region Actions —— 用户交互

            /** 按搜索关键词、分类与排序规则过滤产品（基于 ViewModel 字段） */
            function filterProducts() {
                const search = document.getElementById('searchInput').value.toLowerCase();
                const category = document.getElementById('categorySelect').value;
                const sort = document.getElementById('sortSelect').value;

                state.filteredProducts = state.products.filter(vm => {
                    const matchSearch = vm.displayName.toLowerCase().includes(search)
                        || vm.description.toLowerCase().includes(search);
                    const matchCategory = !category || vm.category === category;
                    return matchSearch && matchCategory;
                });

                const sortStrategies = {
                    'price-low':  (a, b) => a.priceYuan - b.priceYuan,
                    'price-high': (a, b) => b.priceYuan - a.priceYuan,
                    'popular':    (a, b) => b.purchaseCount - a.purchaseCount,
                    'newest':     (a, b) => b.assetId - a.assetId
                };
                state.filteredProducts.sort(sortStrategies[sort] || sortStrategies['newest']);

                state.currentPage = 1;
                renderProducts();
            }

            /** 翻页 */
            function goToPage(page) {
                state.currentPage = page;
                renderProducts();
                window.scrollTo({ top: 0, behavior: 'smooth' });
            }

            /** 切换购物车状态（加入 / 移除） */
            async function toggleCart(assetId) {
                if (!requireLogin('购物车')) return;

                const token = getToken();
                const inCart = state.userCartIds.has(assetId);
                try {
                    if (!inCart) {
                        const result = await apiAddToCart(token, assetId);
                        if (result.success) {
                            state.userCartIds.add(assetId);
                        } else {
                            ErrorPresenter.notifyError(result.code, result.message);
                            return;
                        }
                    } else {
                        const result = await apiRemoveFromCart(token, assetId);
                        if (result.success) {
                            state.userCartIds.delete(assetId);
                        } else {
                            ErrorPresenter.notifyError(result.code, result.message);
                            return;
                        }
                    }
                    updateCartBadge();
                    renderProducts();
                } catch (e) {
                    console.error('[Action] 购物车操作出错', e);
                    ErrorPresenter.notifyError('UNKNOWN');
                }
            }

            /** 切换收藏状态 */
            async function toggleFavorite(btn, assetId) {
                assetId = Number(assetId);
                if (!requireLogin('收藏')) return;

                const token = getToken();
                const icon = btn.querySelector('.material-icons');
                const isActive = state.userFavorites.has(assetId);

                try {
                    const result = await apiToggleFavorite(token, assetId, !isActive);
                    if (!result.success) {
                        ErrorPresenter.notifyError(result.code, result.message);
                        return;
                    }

                    if (!isActive) {
                        btn.classList.add('active');
                        btn.setAttribute('aria-pressed', 'true');
                        icon.textContent = 'favorite';
                        state.userFavorites.add(assetId);
                    } else {
                        btn.classList.remove('active');
                        btn.removeAttribute('aria-pressed');
                        icon.textContent = 'favorite_border';
                        state.userFavorites.delete(assetId);
                    }
                } catch (e) {
                    console.error('[Action] 收藏操作失败', e);
                    ErrorPresenter.notifyError('UNKNOWN');
                }
            }

            /** 查看商品详情 */
            function viewDetail(productId) {
                location.href = `/asset/detail/${productId}`;
            }

            /** 跳转购物车页面 */
            function goToCart() {
                alert('购物车功能开发中...');
            }

            /** 切换卡片抽屉展开（移动端点击展开，桌面端 hover 仍可触发） */
            function toggleExpand(event, assetId) {
                event.preventDefault();
                event.stopPropagation();

                const toggle = event.currentTarget;
                const card = toggle?.closest('.product-card');
                if (!card) return;

                const shouldExpand = !card.classList.contains('is-expanded');

                document.querySelectorAll('.product-card.is-expanded').forEach(openCard => {
                    if (openCard === card) return;
                    openCard.classList.remove('is-expanded');
                    const openToggle = openCard.querySelector('.card-drawer-toggle');
                    if (openToggle) openToggle.setAttribute('aria-expanded', 'false');
                });

                card.classList.toggle('is-expanded', shouldExpand);
                toggle.setAttribute('aria-expanded', shouldExpand ? 'true' : 'false');
            }
            // #endregion

            // #region Init —— 初始化入口

            async function init() {
                if (window.initCustomSelects) window.initCustomSelects();
                if (window.initGlobalTopbar) window.initGlobalTopbar();
                if (window.initGlobalFooter) window.initGlobalFooter();
                if (window.initButtonEffects) window.initButtonEffects();

                document.getElementById('searchInput').addEventListener('input', filterProducts);
                document.getElementById('categorySelect').addEventListener('change', filterProducts);
                document.getElementById('sortSelect').addEventListener('change', filterProducts);

                document.addEventListener('click', (event) => {
                    if (event.target.closest('.product-card')) return;

                    document.querySelectorAll('.product-card.is-expanded').forEach(card => {
                        card.classList.remove('is-expanded');
                        const toggle = card.querySelector('.card-drawer-toggle');
                        if (toggle) toggle.setAttribute('aria-expanded', 'false');
                    });
                });

                document.addEventListener('keydown', (event) => {
                    if (event.key !== 'Escape') return;
                    document.querySelectorAll('.product-card.is-expanded').forEach(card => {
                        card.classList.remove('is-expanded');
                        const toggle = card.querySelector('.card-drawer-toggle');
                        if (toggle) toggle.setAttribute('aria-expanded', 'false');
                    });
                });

                const cartBtn = document.getElementById('cartButton');
                if (cartBtn) cartBtn.addEventListener('click', goToCart);

                // 显示骨架屏占位
                renderSkeletons(ITEMS_PER_PAGE);

                await fetchAssets();
                renderCategories();
                await fetchUserState();
                renderProducts();
                updateCartBadge();
            }

            /** 重新加载列表（用于错误态重试） */
            async function retryLoad() {
                renderSkeletons(ITEMS_PER_PAGE);
                await fetchAssets();
                renderCategories();
                await fetchUserState();
                renderProducts();
                updateCartBadge();
            }
            // #endregion

            /** 展开卡片上被折叠的标签 */
            function expandTags(moreEl, assetId) {
                const container = moreEl.parentElement;
                if (!container) return;
                const vm = state.products.find(p => p.assetId === assetId);
                if (!vm || !vm.tags) return;
                container.innerHTML = vm.tags.map(t => `<span class="pc-tag-badge">${escapeHtml(t)}</span>`).join('');
                container.dataset.collapsed = 'false';
                container.classList.add('pc-tag-badges-expanded');
            }

            // 暴露给 HTML onclick 使用的公共接口
            return { init, goToPage, toggleCart, toggleFavorite, viewDetail, goToCart, toggleExpand, retryLoad, expandTags };
        })();

        // 页面加载时启动应用
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => ShopApp.init());
        } else {
            ShopApp.init();
        }
