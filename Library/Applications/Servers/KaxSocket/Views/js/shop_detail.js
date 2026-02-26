// 时间戳转日期格式化
        function formatDate(ts) {
            if (!ts || ts === '--') return '--';
            // 支持秒/毫秒
            const ms = ts > 9999999999 ? ts : ts * 1000;
            const date = new Date(ms);
            if (isNaN(date.getTime())) return '--';
            return date.toLocaleDateString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit' });
        }

        // 未登录直接跳转登录页
        const token = localStorage.getItem('kax_login_token');
        if (!token) {
            window.location.href = '/login';
        }

        // 从 URL 参数或路径中获取产品 ID（兼容 /asset/detail?id=1 与 /asset/detail/1）
        function getProductIdFromUrl() {
            const urlParams = new URLSearchParams(window.location.search);
            const qid = urlParams.get('id');
            if (qid && /^\d+$/.test(qid)) return qid;

            const parts = window.location.pathname.split('/').filter(Boolean);
            const last = parts.length ? parts[parts.length - 1] : '';
            if (/^\d+$/.test(last)) return last;

            return '1';
        }

        const productId = parseInt(getProductIdFromUrl(), 10) || 1;

        // 模拟产品数据（作为回退，实际优先从后端 API 获取）
        const productData = {
            1: {
                id: 1,
                name: '高级游戏模组包',
                description: '一个功能完整的游戏模组，包含多项高级功能和优化。提供完整的文档和技术支持。',
                price: 79.99,
                originalPrice: 99.99,
                category: '模组',
                rating: 4.8,
                reviews: 128,
                version: '2.1.0',
                compatibility: '98%',
                downloads: 1243,
                fileSize: '256 MB',
                uploadDate: '2026-02-20',
                author: '开发者团队',
                license: 'MIT',
                stock: 150,
            },
            2: {
                id: 2,
                name: '轻量化优化模组',
                description: '专注于性能优化的轻量级模组，为低配置设备提供最佳体验。',
                price: 49.99,
                originalPrice: 69.99,
                category: '模组',
                rating: 4.6,
                reviews: 89,
                version: '1.8.5',
                compatibility: '95%',
                downloads: 856,
                fileSize: '128 MB',
                uploadDate: '2026-02-18',
                author: '优化团队',
                license: 'MIT',
                stock: 200,
            }
        };

        // 渲染详情页骨架屏占位
        // 缓存真实 DOM 结构的引用，供骨架屏恢复使用
        const _skeletonCache = {};

        function renderDetailSkeleton() {
            const left = document.querySelector('.detail-left');
            const right = document.querySelector('.detail-right');
            const relatedGrid = document.getElementById('relatedProductsGrid');

            // 保存原始 HTML
            if (left)  _skeletonCache.left  = left.innerHTML;
            if (right) _skeletonCache.right  = right.innerHTML;
            if (relatedGrid) _skeletonCache.related = relatedGrid.innerHTML;

            // 左侧骨架
            if (left) left.innerHTML = `
                <div class="sk sk-main-image"></div>
                <div class="sk-info-card">
                    <div class="sk sk-h28 sk-w60"></div>
                    <div class="sk sk-h12 sk-w80"></div>
                    <div class="sk sk-h12 sk-w70"></div>
                    <div class="sk sk-h12 sk-w50"></div>
                    <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px;padding-top:12px;">
                        <div class="sk sk-h36"></div>
                        <div class="sk sk-h36"></div>
                        <div class="sk sk-h36"></div>
                    </div>
                </div>
                <div class="sk-specs-card">
                    <div class="sk sk-h16 sk-w30"></div>
                    <div class="sk sk-h12 sk-w100"></div>
                    <div class="sk sk-h12 sk-w100"></div>
                    <div class="sk sk-h12 sk-w80"></div>
                    <div class="sk sk-h12" style="width:90%"></div>
                </div>`;

            // 右侧骨架
            if (right) right.innerHTML = `
                <div class="sk-purchase-panel">
                    <div class="sk sk-h20 sk-w40" style="margin:0 auto;"></div>
                    <div class="sk sk-h44 sk-w60" style="margin:0 auto;"></div>
                    <div class="sk sk-h12 sk-w50" style="margin:0 auto;"></div>
                    <div class="sk sk-h80 sk-w100" style="margin-top:8px;border-radius:4px;"></div>
                    <div class="sk sk-h44 sk-w100" style="margin-top:4px;border-radius:4px;"></div>
                    <div style="display:flex;gap:12px;margin-top:4px;">
                        <div class="sk sk-h36" style="flex:1;border-radius:4px;"></div>
                        <div class="sk sk-h36" style="flex:1;border-radius:4px;"></div>
                    </div>
                </div>`;

            // 相关产品骨架
            if (relatedGrid) relatedGrid.innerHTML = Array.from({ length: 4 }, () => `
                <div class="sk-related-card">
                    <div class="sk sk-related-image"></div>
                    <div class="sk-related-content">
                        <div class="sk sk-h16" style="width:70%;"></div>
                        <div class="sk sk-h16" style="width:40%;"></div>
                    </div>
                </div>`).join('');
        }

        // 移除骨架屏，恢复真实 DOM 结构
        function removeSkeleton() {
            const left = document.querySelector('.detail-left');
            const right = document.querySelector('.detail-right');
            const relatedGrid = document.getElementById('relatedProductsGrid');

            if (left  && _skeletonCache.left)    left.innerHTML  = _skeletonCache.left;
            if (right && _skeletonCache.right)   right.innerHTML = _skeletonCache.right;
            if (relatedGrid && _skeletonCache.related) relatedGrid.innerHTML = _skeletonCache.related;
        }

        // 初始化页面（优先从后端获取）
        async function initPage() {
            renderDetailSkeleton();
            let product = productData[productId] || productData[1];

            try {
                const resp = await fetch(`/api/asset/detail/${productId}`, { credentials: 'same-origin' });
                if (resp.ok) {
                    const json = await resp.json();
                    const asset = (json && typeof json === 'object') ? (json.data || json) : null;
                    if (asset) {
                        // Helper: normalize price units (backend may return integer cents)
                        const toNumber = v => (v === null || v === undefined) ? null : (typeof v === 'number' ? v : (isNaN(Number(v)) ? null : Number(v)));
                        const normalizeCurrency = v => {
                            const n = toNumber(v);
                            if (n === null) return null;
                            return n;
                        };

                        const rawPrice = toNumber(asset.price ?? asset.priceCents ?? asset.price_in_cents);
                        const rawOriginal = toNumber(asset.originalPrice ?? asset.priceOriginal ?? asset.original_price);
                        const rawSale = toNumber(asset.salePrice ?? asset.sale_price);

                        const price = normalizeCurrency(rawPrice);
                        const originalPrice = normalizeCurrency(rawOriginal);
                        const salePrice = rawSale != null ? normalizeCurrency(rawSale) : null;

                        // fileSize: prefer human-readable if provided, otherwise convert bytes number to readable
                        const rawFileSize = asset.fileSize ?? asset.size ?? null;
                        const fileSizeStr = (() => {
                            if (rawFileSize == null) return '--';
                            if (typeof rawFileSize === 'string') return rawFileSize;
                            const n = Number(rawFileSize);
                            if (isNaN(n)) return '--';
                            const units = ['B','KB','MB','GB','TB'];
                            let idx = 0; let val = n;
                            while (val >= 1024 && idx < units.length-1) { val /= 1024; idx++; }
                            return (idx === 0 ? val.toFixed(0) : val.toFixed(2)) + ' ' + units[idx];
                        })();

                        product = {
                            id: asset.id ?? productId,
                            name: asset.name ?? asset.title ?? '--',
                            description: asset.description ?? '--',
                            price: price,
                            originalPrice: price,
                            salePrice: salePrice,
                            category: asset.category ?? asset.type ?? '--',
                            rating: (asset.rating !== undefined && asset.rating !== null) ? Number(asset.rating) : null,
                            reviews: asset.reviews ?? asset.reviewCount ?? '--',
                            version: asset.version ?? '--',
                            compatibility: asset.compatibility ?? '--',
                            downloads: asset.downloads ?? '--',
                            purchaseCount: asset.purchaseCount ?? '--',
                            fileSize: fileSizeStr,
                            uploadDate: asset.uploadDate ?? asset.createdAt ?? '--',
                            author: asset.author ?? asset.uploader ?? '--',
                            license: asset.license ?? '--',
                            stock: (asset.stock !== undefined && asset.stock !== null) ? asset.stock : '--',
                            discountRate: (asset.discountRate !== undefined && asset.discountRate !== null) ? Number(asset.discountRate) : null,
                            prices: Array.isArray(asset.prices) ? asset.prices : (Array.isArray(asset.Prices) ? asset.Prices : [])
                        };
                    }
                }
            } catch (e) {
                console.warn('获取商品详情失败，使用本地回退数据', e);
            }

            removeSkeleton();
            loadProductData(product);
            setupEventListeners();
            // 全局初始化（如果存在这些函数）
            try { initGlobalTopbar && initGlobalTopbar(); } catch (e) {}
            try { initGlobalFooter && initGlobalFooter(); } catch (e) {}
            try { initButtonEffects && initButtonEffects(); } catch (e) {}
        }

        // 加载产品数据
        function loadProductData(product) {
            const orDash = (v) => (v === null || v === undefined) ? '--' : v;
            // 日期字段格式化
            const showDate = (v) => {
                if (!v || v === '--') return '--';
                // 支持字符串/数字
                if (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}/.test(v)) return v;
                const n = Number(v);
                if (!isNaN(n)) return formatDate(n);
                return v;
            };
            const showCurrency = (v) => (v === null || v === undefined) ? '--' : ('💰' + Number(v).toFixed(2));
            const showDownloads = (v) => {
                if (v === null || v === undefined || v === '--') return '--';
                const n = Number(v);
                if (isNaN(n)) return '--';
                return n >= 1000 ? (n / 1000).toFixed(1) + 'K' : String(n);
            };

            document.getElementById('breadcrumbCategory').textContent = orDash(product.category);
            document.getElementById('productName').textContent = orDash(product.name);
            document.getElementById('productDescription').textContent = orDash(product.description);
            document.getElementById('productVersion').textContent = orDash(product.version);
            document.getElementById('productCompatibility').textContent = orDash(product.compatibility);
            document.getElementById('productDownloads').textContent = showDownloads(product.downloads);
            // 显示购买次数
            const purchasesEl = document.getElementById('productPurchases');
            if (purchasesEl) purchasesEl.textContent = showDownloads(product.purchaseCount);
            // 显示价格：优先使用价格表第一个方案（若存在），否则使用后端兼容字段
            let displayOriginal = null;
            let displayCurrent = null;
            if (product.prices && Array.isArray(product.prices) && product.prices.length > 0) {
                const p0 = product.prices[0];
                const origCents = (p0.originalPrice != null ? Number(p0.originalPrice) : Number(p0.price || 0));
                const saleCents = Math.round(Number(p0.price || 0) * (1 - (Number(p0.discountRate) || 0)));
                displayOriginal = origCents;
                displayCurrent = saleCents;
                // expose discountRate for badge
                product.discountRate = (p0.discountRate != null) ? Number(p0.discountRate) : product.discountRate;
            } else {
                displayOriginal = product.originalPrice != null ? product.originalPrice : null;
                displayCurrent = (product.salePrice != null) ? product.salePrice : product.price;
            }

            const priceOriginalEl = document.getElementById('priceOriginal');
            const priceCurrentEl = document.getElementById('priceCurrent');
            const priceBadgeEl = document.getElementById('priceBadge');

            priceCurrentEl.textContent = displayCurrent != null ? showCurrency(displayCurrent) : '--';

            // 是否显示原始价格：只有在原价存在且大于当前价时才显示
            if (displayOriginal != null && displayCurrent != null && Number(displayOriginal) > Number(displayCurrent)) {
                priceOriginalEl.textContent = showCurrency(displayOriginal);
                priceOriginalEl.style.display = '';
            } else {
                priceOriginalEl.textContent = '';
                priceOriginalEl.style.display = 'none';
            }

            // 计算并显示折扣徽标：只有存在有效折扣 (>0) 时显示，否则隐藏
            let discountPercent = null;
            if (product.discountRate != null) {
                discountPercent = Math.round(Number(product.discountRate) * 100);
            } else if (displayOriginal != null && displayCurrent != null && Number(displayOriginal) > 0) {
                const disc = Math.round(((Number(displayOriginal) - Number(displayCurrent)) / Number(displayOriginal)) * 100);
                if (disc > 0) discountPercent = disc;
            }

            if (discountPercent != null && discountPercent > 0) {
                priceBadgeEl.textContent = discountPercent + '% OFF';
                priceBadgeEl.style.display = '';
            } else {
                priceBadgeEl.textContent = '';
                priceBadgeEl.style.display = 'none';
            }

            // 更新评分
            const ratingEl = document.getElementById('ratingStars');
            const ratingScoreEl = document.getElementById('ratingScore');
            const ratingCountEl = document.getElementById('ratingCount');
            if (product.rating == null || isNaN(Number(product.rating))) {
                ratingEl.textContent = '--';
                ratingScoreEl.textContent = '--';
            } else {
                const ratingStars = Math.round(Number(product.rating));
                ratingEl.textContent = '⭐'.repeat(ratingStars) + '☆'.repeat(5 - ratingStars);
                ratingScoreEl.textContent = Number(product.rating).toFixed(1);
            }
            ratingCountEl.textContent = '(' + (product.reviews != null ? product.reviews : '--') + ' 条评价)';

            // 库存状态
            const stockInfo = document.getElementById('stockInfo');
            if (product.stock === '--' || product.stock === null || product.stock === undefined) {
                stockInfo.textContent = '库存：--';
                stockInfo.classList.add('low');
            } else if (Number(product.stock) > 50) {
                stockInfo.textContent = `库存：${product.stock}（充足）`;
                stockInfo.classList.remove('low');
            } else if (Number(product.stock) > 0) {
                stockInfo.textContent = `库存：${product.stock}（有限）`;
                stockInfo.classList.add('low');
            } else {
                stockInfo.textContent = '库存：0（暂无）';
                stockInfo.classList.add('low');
            }

            // 更规格
            const specsList = document.getElementById('specsList');
            specsList.innerHTML = `
                <div class="spec-item">
                    <span class="spec-label">文件大小</span>
                    <span class="spec-value">${orDash(product.fileSize)}</span>
                </div>
                <div class="spec-item">
                    <span class="spec-label">上传时间</span>
                    <span class="spec-value">${showDate(product.uploadDate)}</span>
                </div>
                <div class="spec-item">
                    <span class="spec-label">作者</span>
                    <span class="spec-value">${orDash(product.author)}</span>
                </div>
                <div class="spec-item">
                    <span class="spec-label">许可证</span>
                    <span class="spec-value">${orDash(product.license)}</span>
                </div>
            `;

            // 存储当前产品数据到 window
            window.currentProduct = product;
        }

        // 设置事件监听器
        function setupEventListeners() {
            // 获取购买按钮
            const purchaseBtn = document.getElementById('purchaseBtn');
            const plansGrid = document.getElementById('plansGrid');
            const favBtn = document.getElementById('favBtn');
            const shareBtn = document.getElementById('shareBtn');
            
            // 存储当前选中的价格方案ID
            let selectedPriceId = null;

            // 动态生成和显示价格套餐
            function loadPricePlans() {
                const product = window.currentProduct;
                if (!product || !product.prices || product.prices.length === 0) {
                    plansGrid.innerHTML = '<div style="color: var(--text-muted); padding: 12px;">暂无价格方案</div>';
                    purchaseBtn.disabled = true;
                    return;
                }

                plansGrid.innerHTML = '';
                product.prices.forEach((price, index) => {
                    // 根据unit和duration生成显示文本
                    let durationText = '';
                    switch (price.unit?.toLowerCase()) {
                        case 'year': durationText = `${price.duration}年`; break;
                        case 'month': durationText = `${price.duration}个月`; break;
                        case 'day': durationText = `${price.duration}天`; break;
                        case 'hour': durationText = `${price.duration}小时`; break;
                        default: durationText = '一次性';
                    }

                    const salePrice = price.price * (1 - (price.discountRate || 0));
                    const hasDiscount = price.discountRate && price.discountRate > 0;
                    
                    const planItem = document.createElement('button');
                    planItem.className = 'plan-item' + (index === 0 ? ' selected' : '');
                    planItem.type = 'button';
                    planItem.innerHTML = `
                        <div class="plan-name">${durationText}</div>
                        <div class="plan-details">
                            ${hasDiscount ? `<span style="text-decoration: line-through; color: var(--text-muted); margin-right: 4px;">💰${Number(price.originalPrice).toFixed(2)}</span>` : ''}
                            <span style="color: var(--accent); font-weight: 600;">💰${Number(salePrice).toFixed(2)}</span>
                        </div>
                    `;
                    
                    planItem.addEventListener('click', () => {
                        // 移除其他选中状态
                        document.querySelectorAll('.plan-item').forEach(item => item.classList.remove('selected'));
                        planItem.classList.add('selected');
                        selectedPriceId = price.id;
                        purchaseBtn.disabled = false;
                    });

                    plansGrid.appendChild(planItem);
                    
                    // 默认选中第一个
                    if (index === 0) {
                        selectedPriceId = price.id;
                        purchaseBtn.disabled = false;
                    }
                });
            }

            // 加载价格套餐
            loadPricePlans();

            // 立即购买按钮
            purchaseBtn.addEventListener('click', async (e) => {
                if (!selectedPriceId) {
                    alert('请选择购买方案');
                    return;
                }

                const product = window.currentProduct;
                const token = localStorage.getItem('kax_login_token');

                if (!token) {
                    alert('请先登录');
                    location.href = '/login';
                    return;
                }

                purchaseBtn.disabled = true;
                purchaseBtn.textContent = '处理中...';

                try {
                    const resp = await fetch('/api/shop/purchase', {
                        method: 'POST',
                        headers: {
                            'Authorization': 'Bearer ' + token,
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify({
                            assetId: product.id,
                            priceId: selectedPriceId
                        })
                    });

                    const data = await resp.json();

                    if (resp.ok && data.code === 0) {
                        purchaseBtn.textContent = '✓ 购买成功';
                        setTimeout(() => {
                            alert('购买成功！');
                            location.reload();
                        }, 1500);
                    } else {
                        alert('购买失败: ' + (data.message || '未知错误'));
                        purchaseBtn.textContent = '立即购买';
                        purchaseBtn.disabled = false;
                    }
                } catch (e) {
                    console.error('购买失败', e);
                    alert('网络错误，请稍后重试');
                    purchaseBtn.textContent = '立即购买';
                    purchaseBtn.disabled = false;
                }
            });

            // 收藏按钮 — 使用后端 API（登录用户），未登录则跳转登录
            favBtn.addEventListener('click', async () => {
                const product = window.currentProduct;
                const token = localStorage.getItem('kax_login_token');
                const icon = favBtn.querySelector('.material-icons');

                if (!token) { alert('请先登录以使用收藏功能'); location.href = '/login'; return; }

                try {
                    const isActive = favBtn.classList.contains('active');
                    if (!isActive) {
                        const resp = await fetch('/api/user/favorites', {
                            method: 'POST',
                            headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' },
                            body: JSON.stringify({ assetId: product.id })
                        });
                        if (resp.ok) {
                            favBtn.classList.add('active');
                            icon.textContent = 'favorite';
                        } else {
                            console.warn('收藏失败', resp.status);
                        }
                    } else {
                        const resp = await fetch(`/api/user/favorites/${product.id}`, {
                            method: 'DELETE',
                            headers: { 'Authorization': 'Bearer ' + token }
                        });
                        if (resp.ok) {
                            favBtn.classList.remove('active');
                            icon.textContent = 'favorite_border';
                        } else {
                            console.warn('取消收藏失败', resp.status);
                        }
                    }
                } catch (e) {
                    console.error('收藏操作失败', e);
                }
            });

            // 检查是否已收藏
            checkIfFavorited();

            // 分享按钮
            shareBtn.addEventListener('click', () => {
                const product = window.currentProduct;
                const shareUrl = window.location.href;
                const shareText = `我发现了一个不错的模组：${product.name}，快来看看吧！`;

                if (navigator.share) {
                    navigator.share({
                        title: product.name,
                        text: shareText,
                        url: shareUrl
                    }).catch(err => console.log('分享取消或出错:', err));
                } else {
                    // 复制到剪贴板
                    navigator.clipboard.writeText(shareUrl).then(() => {
                        shareBtn.textContent = '✓ 链接已复制';
                        setTimeout(() => {
                            shareBtn.innerHTML = '<span class="material-icons">share</span><span>分享</span>';
                        }, 2000);
                    });
                }
            });
        }

        // 创建飞行效果
        function createFlyingIcon(fromRect, toRect) {
            const flying = document.createElement('div');
            flying.style.cssText = `
                position: fixed;
                left: ${fromRect.left}px;
                top: ${fromRect.top}px;
                width: 32px;
                height: 32px;
                font-size: 28px;
                z-index: 10000;
                pointer-events: none;
                display: flex;
                align-items: center;
                justify-content: center;
            `;
            flying.textContent = '🛒';
            document.body.appendChild(flying);

            // 动画
            requestAnimationFrame(() => {
                flying.style.transition = 'all 0.6s cubic-bezier(0.2, 0.8, 0.2, 1)';
                flying.style.left = (toRect.left + toRect.width / 2 - 16) + 'px';
                flying.style.top = (toRect.top + toRect.height / 2 - 16) + 'px';
                flying.style.opacity = '0';
                flying.style.transform = 'scale(0.5)';
            });

            setTimeout(() => flying.remove(), 600);
        }

        // 检查是否已收藏（优先使用后端 API，未登录回退本地）
        async function checkIfFavorited() {
            const product = window.currentProduct;
            const favBtn = document.getElementById('favBtn');
            const icon = favBtn && favBtn.querySelector('.material-icons');
            const token = localStorage.getItem('kax_login_token');

            if (token) {
                try {
                    const resp = await fetch('/api/user/favorites', { headers: { 'Authorization': 'Bearer ' + token } });
                    if (resp.ok) {
                        const j = await resp.json();
                        const arr = (j && j.data) ? j.data : [];
                        const ids = (Array.isArray(arr) ? arr : []).map(item => {
                            if (typeof item === 'number') return Number(item);
                            if (typeof item === 'string') return Number(item);
                            if (typeof item === 'object' && item != null) return Number(item.id != null ? item.id : (item.assetId != null ? item.assetId : NaN));
                            return NaN;
                        }).filter(n => !isNaN(n));
                        if (ids.includes(Number(product.id))) {
                            favBtn.classList.add('active');
                            if (icon) icon.textContent = 'favorite';
                            return;
                        }
                    }
                } catch (e) {
                    console.warn('检查收藏状态失败，回退到本地存储', e);
                }
            }

            // 本地回退
            const favorites = JSON.parse(localStorage.getItem('favorites') || '[]');
            if (favorites && favorites.includes(product.id)) {
                favBtn.classList.add('active');
                if (icon) icon.textContent = 'favorite';
            }
        }

        // 更新购物车计数（优先使用后端 API，未登录回退本地）
        async function updateCartBadge() {
            const cartCountEl = document.getElementById('cartCount');
            const token = localStorage.getItem('kax_login_token');
            if (!cartCountEl) return;

            if (token) {
                try {
                    const resp = await fetch('/api/user/cart', { headers: { 'Authorization': 'Bearer ' + token } });
                    if (resp.ok) {
                        const j = await resp.json();
                        const raw = (j && j.data) ? j.data : [];
                        // raw may be array of objects or ids
                        let count = 0;
                        if (Array.isArray(raw)) {
                            raw.forEach(item => {
                                if (item && typeof item === 'object') count += Number(item.quantity || 1);
                                else if (!isNaN(Number(item))) count += 1;
                            });
                        }
                        cartCountEl.textContent = count;
                        return;
                    }
                } catch (e) {
                    console.warn('获取远端购物车失败，回退本地', e);
                }
            }

            // 本地回退
            const cart = JSON.parse(localStorage.getItem('cart') || '[]');
            const count = cart.reduce((sum, item) => sum + (item.quantity || 1), 0);
            cartCountEl.textContent = count;
        }

        // 导航到其他商品详情
        function goToShopDetail(id) {
            window.location.href = `/shop/detail?id=${id}`;
        }

        // 页面加载时初始化
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', initPage);
        } else {
            initPage();
        }
