/* ================================================================
         *  ShopApp — 商城页面核心控制器
         *  职责划分：
         *    • Auth     — 登录令牌与请求头
         *    • Api      — 与后端通信（资产列表、收藏、购物车）
         *    • State    — 页面状态管理（产品、筛选、分页、用户数据）
         *    • Renderer — DOM 渲染（产品网格、分页、购物车徽章）
         *    • Actions  — 用户交互（加购、收藏、翻页、搜索）
         * ================================================================ */
        const ShopApp = (() => {
            const ITEMS_PER_PAGE = 12;
            const TOKEN_KEY = 'kax_login_token';
            const LOCAL_CART_KEY = 'kax_cart';

            // #region State —— 页面状态
            const state = {
                products: [],
                filteredProducts: [],
                currentPage: 1,
                userFavorites: new Set(),
                userCartItems: []
            };
            // #endregion

            // #region Auth —— 登录令牌
            function getToken() {
                return localStorage.getItem(TOKEN_KEY);
            }

            function requireLogin(featureName) {
                const token = getToken();
                if (!token) {
                    alert(`请先登录以使用${featureName}功能`);
                    location.href = '/login';
                    return false;
                }
                return true;
            }

            function authHeaders(includeContentType = true) {
                const token = getToken();
                const headers = {};
                if (token) headers['Authorization'] = 'Bearer ' + token;
                if (includeContentType) headers['Content-Type'] = 'application/json';
                return headers;
            }
            // #endregion

            // #region Utils —— 工具函数

            /** 将后端返回的异构 ID 数据（number / string / { id } / { assetId }）归一化为数字数组 */
            function normalizeIdList(rawArray) {
                return (Array.isArray(rawArray) ? rawArray : [])
                    .map(item => {
                        if (item == null) return NaN;
                        if (typeof item === 'number' || typeof item === 'string') return Number(item);
                        if (typeof item === 'object') {
                            if (item.id != null) return Number(item.id);
                            if (item.assetId != null) return Number(item.assetId);
                        }
                        return NaN;
                    })
                    .filter(n => !isNaN(n));
            }

            /** 将时间戳转换为可读的日期格式 */
            function formatDate(timestamp) {
                if (!timestamp) return '未知';
                const ms = timestamp > 9999999999 ? timestamp : timestamp * 1000;
                const date = new Date(ms);
                if (isNaN(date.getTime())) return '未知';
                return date.toLocaleDateString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit' });
            }

            /** HTML 转义 */
            function escapeHtml(value) {
                const div = document.createElement('div');
                div.textContent = String(value ?? '');
                return div.innerHTML;
            }

            /** 兼容后端 camelCase/PascalCase 字段读取 */
            function getAssetField(asset, camelKey, pascalKey) {
                return asset?.[camelKey] ?? asset?.[pascalKey];
            }

            /** 兼容截图字段：数组 或 分号/逗号分隔字符串 */
            function normalizeMediaArray(raw) {
                if (Array.isArray(raw)) {
                    return raw.map(v => String(v || '').trim()).filter(Boolean);
                }
                if (typeof raw === 'string') {
                    return raw
                        .split(/[;,，\n\r]+/)
                        .map(v => v.trim())
                        .filter(Boolean);
                }
                return [];
            }

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

            // #region Api —— 后端接口交互

            /** 拉取资产列表并映射为前端产品模型 */
            async function fetchAssets() {
                try {
                    const resp = await fetch('/api/asset/list?page=1&pageSize=200');
                    if (!resp.ok) { console.warn('获取资产列表失败', resp.status); return; }

                    const body = await resp.json();
                    // 后端返回分页对象 { items: [...], total: ... }
                    const items = body?.data?.items ?? body?.items ?? [];

                    state.products = items.map(a => ({
                        // 媒体资源字段兼容：AssetModel 可能为 camelCase 或 PascalCase
                        coverImage: getAssetField(a, 'coverImage', 'CoverImage') || '',
                        iconImage: getAssetField(a, 'iconImage', 'IconImage') || '',
                        screenshots: normalizeMediaArray(getAssetField(a, 'screenshots', 'Screenshots')),
                        id: Number(a.id) || a.id,
                        name: a.name,
                        desc: a.description || '',
                        price: a.priceYuan ?? a.price ?? (a.prices?.length > 0 ? (a.prices[0].priceYuan ?? a.prices[0].price) : 0),
                        prices: a.prices || [],
                        category: a.category || '',
                        icon: '📦',
                        // 规格字段既可能在平铺级别，也可能在 specs 对象中
                        downloads: a.downloads ?? (a.specs?.downloads ?? 0),
                        purchaseCount: a.purchaseCount ?? (a.specs?.purchaseCount ?? 0),
                        lastUpdatedAt: a.lastUpdatedAt ?? (a.specs?.lastUpdatedAt ?? 0),
                        rating: a.rating ?? (a.specs?.rating ?? 0),
                        reviewCount: a.reviewCount ?? (a.specs?.reviewCount ?? 0)
                    }));
                    state.filteredProducts = [...state.products];
                } catch (e) {
                    console.error('拉取资产出错', e);
                }
            }

            /** 拉取当前登录用户的收藏与购物车状态 */
            async function fetchUserState() {
                const token = getToken();
                if (!token) return;

                try {
                    const [favResp, cartResp] = await Promise.all([
                        fetch('/api/user/favorites', { headers: { 'Authorization': 'Bearer ' + token } }),
                        fetch('/api/user/cart', { headers: { 'Authorization': 'Bearer ' + token } })
                    ]);

                    if (favResp.ok) {
                        const favBody = await favResp.json();
                        state.userFavorites = new Set(normalizeIdList(favBody?.data ?? []));
                    }
                    if (cartResp.ok) {
                        const cartBody = await cartResp.json();
                        state.userCartItems = normalizeIdList(cartBody?.data ?? []);
                    }
                } catch (e) {
                    console.warn('获取用户收藏/购物车失败', e);
                }
            }

            /** 向后端发送购物车加入请求并返回解析后的资产 ID */
            async function apiAddToCart(token, assetId) {
                const resp = await fetch('/api/user/cart', {
                    method: 'POST',
                    headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' },
                    body: JSON.stringify({ assetId })
                });
                if (!resp.ok) { console.warn('加入购物车失败', resp.status); return null; }

                try {
                    const body = await resp.json();
                    const added = body?.data?.id ?? body?.data?.assetId ?? body?.id ?? assetId;
                    return Number(added);
                } catch {
                    return assetId;
                }
            }

            /** 向后端发送购物车移除请求 */
            async function apiRemoveFromCart(token, assetId) {
                const resp = await fetch(`/api/user/cart/${assetId}`, {
                    method: 'DELETE',
                    headers: { 'Authorization': 'Bearer ' + token }
                });
                if (!resp.ok) console.warn('从购物车移除失败', resp.status);
                return resp.ok;
            }

            /** 向后端发送收藏 / 取消收藏请求 */
            async function apiToggleFavorite(token, assetId, shouldAdd) {
                if (shouldAdd) {
                    const resp = await fetch('/api/user/favorites', {
                        method: 'POST',
                        headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' },
                        body: JSON.stringify({ assetId })
                    });
                    return resp.ok;
                }
                const resp = await fetch(`/api/user/favorites/${assetId}`, {
                    method: 'DELETE',
                    headers: { 'Authorization': 'Bearer ' + token }
                });
                return resp.ok;
            }
            // #endregion

            // #region Renderer —— DOM 渲染

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
                const localCartIds = token ? [] : getLocalCartIds();

                grid.innerHTML = pageProducts.map(p => buildProductCard(p, token, localCartIds)).join('');
                renderPagination();
            }

            /** 获取本地 localStorage 购物车中的 ID 列表 */
            function getLocalCartIds() {
                try {
                    const cart = JSON.parse(localStorage.getItem(LOCAL_CART_KEY) || '[]');
                    return cart.map(ci => Number(ci.id));
                } catch { return []; }
            }

            /** 构建单张产品卡片 HTML */
            function buildProductCard(product, token, localCartIds) {
                const isFav = state.userFavorites.has(product.id);
                const inCart = token
                    ? state.userCartItems.includes(product.id)
                    : (state.userCartItems.includes(product.id) || localCartIds.includes(product.id));

                const favIcon = isFav ? 'favorite' : 'favorite_border';
                const favClass = isFav ? ' active' : '';
                const favAria = isFav ? ' aria-pressed="true"' : '';
                const cartClass = inCart ? ' active has-count' : '';
                const cartIcon = inCart ? 'shopping_cart' : 'add_shopping_cart';
                const cartTitle = inCart ? '已在购物车中，点击可移除' : '加入购物车';
                const cartDisabled = '';
                const downloadsText = product.downloads > 0 ? `${product.downloads}` : '0';
                const imageUrl = product.iconImage || product.coverImage || product.screenshots?.[0] || '';
                const imageHtml = imageUrl
                    ? `<img src="${escapeHtml(imageUrl)}" alt="${escapeHtml(product.name || '')}" loading="lazy" style="width:100%;height:100%;object-fit:cover;">`
                    : product.icon;

                return `
                <div class="product-card">
                    <div class="product-image">${imageHtml}</div>
                    <div class="product-content">
                        ${product.category ? `<span class="product-category-tag">${product.category}</span>` : ''}
                        <div class="product-name">${product.name}</div>
                        <div class="product-desc">${product.desc}</div>
                        <div class="product-meta">
                            <div class="product-price">💰${Number(product.price).toFixed(2)}</div>
                            <div class="product-meta-row">
                                <span class="meta-item"><span class="meta-label">下载</span> ${downloadsText}</span>
                                <span class="meta-item"><span class="meta-label">更新</span> ${formatDate(product.lastUpdatedAt)}</span>
                            </div>
                        </div>
                        <div class="product-actions">
                            <button class="btn" onclick="ShopApp.viewDetail(${product.id})">查看详细</button>
                            <button class="btn icon cart-icon${cartClass}" onclick="ShopApp.toggleCart(${product.id})" title="${cartTitle}" data-asset-id="${product.id}"${cartDisabled}>
                                <span class="material-icons">${cartIcon}</span>
                            </button>
                            <button class="btn icon${favClass}" onclick="ShopApp.toggleFavorite(this, ${product.id})" title="收藏" data-asset-id="${product.id}"${favAria}>
                                <span class="material-icons">${favIcon}</span>
                            </button>
                        </div>
                    </div>
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
                    el.textContent = state.userCartItems.length || 0;
                    return;
                }
                try {
                    const cart = JSON.parse(localStorage.getItem(LOCAL_CART_KEY) || '[]');
                    el.textContent = cart.reduce((sum, item) => sum + (item.quantity || 1), 0);
                } catch { el.textContent = 0; }
            }
            // #endregion

            // #region Actions —— 用户交互

            /** 按搜索关键词、分类与排序规则过滤产品 */
            function filterProducts() {
                const search = document.getElementById('searchInput').value.toLowerCase();
                const category = document.getElementById('categorySelect').value;
                const sort = document.getElementById('sortSelect').value;

                state.filteredProducts = state.products.filter(p => {
                    const matchSearch = p.name.toLowerCase().includes(search) || p.desc.toLowerCase().includes(search);
                    const matchCategory = !category || p.category === category;
                    return matchSearch && matchCategory;
                });

                const sortStrategies = {
                    'price-low': (a, b) => a.price - b.price,
                    'price-high': (a, b) => b.price - a.price,
                    'popular': (a, b) => (b.downloads + b.purchaseCount) - (a.downloads + a.purchaseCount),
                    'newest': (a, b) => b.id - a.id
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
            async function toggleCart(productId) {
                if (!requireLogin('购物车')) return;

                const token = getToken();
                const product = state.products.find(p => p.id === productId);
                if (!product) return;

                const inCart = state.userCartItems.includes(productId);
                try {
                    if (!inCart) {
                        const addedId = await apiAddToCart(token, productId);
                        if (addedId != null && !isNaN(addedId) && !state.userCartItems.includes(addedId)) {
                            state.userCartItems.push(addedId);
                        }
                    } else {
                        const ok = await apiRemoveFromCart(token, productId);
                        if (ok) state.userCartItems = state.userCartItems.filter(id => id !== productId);
                    }
                    updateCartBadge();
                    renderProducts();
                } catch (e) {
                    console.error('购物车操作出错', e);
                }
            }

            /** 切换收藏状态 */
            async function toggleFavorite(btn, assetId) {
                assetId = Number(assetId);
                if (!requireLogin('收藏')) return;

                const token = getToken();
                const icon = btn.querySelector('.material-icons');
                const isActive = state.userFavorites.has(assetId) || btn.classList.contains('active');

                try {
                    const ok = await apiToggleFavorite(token, assetId, !isActive);
                    if (!ok) return;

                    if (!isActive) {
                        btn.classList.add('active');
                        icon.textContent = 'favorite';
                        state.userFavorites.add(assetId);
                    } else {
                        btn.classList.remove('active');
                        icon.textContent = 'favorite_border';
                        state.userFavorites.delete(assetId);
                    }
                } catch (e) {
                    console.error('收藏操作失败', e);
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
            // #endregion

            // 暴露给 HTML onclick 使用的公共接口
            return { init, goToPage, toggleCart, toggleFavorite, viewDetail, goToCart };
        })();

        // 页面加载时启动应用
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => ShopApp.init());
        } else {
            ShopApp.init();
        }
