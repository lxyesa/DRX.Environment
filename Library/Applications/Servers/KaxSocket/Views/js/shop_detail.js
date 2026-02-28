/** 时间戳或日期字符串 → zh-CN 日期显示 */
        function formatDate(ts) {
            if (!ts || ts === '--') return '--';
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
            }
        };

        // 渲染骨架屏 — 对应新版 HTML 结构（.detail-main-col / .detail-side-col）
        const _skeletonCache = {};

        function renderDetailSkeleton() {
            const mainCol    = document.querySelector('.detail-main-col');
            const sideCol    = document.querySelector('.detail-side-col');
            const relatedGrid = document.getElementById('relatedProductsGrid');

            if (mainCol)    _skeletonCache.mainCol  = mainCol.innerHTML;
            if (sideCol)    _skeletonCache.sideCol  = sideCol.innerHTML;
            if (relatedGrid) _skeletonCache.related = relatedGrid.innerHTML;

            if (mainCol) mainCol.innerHTML = `
                <div class="sk sk-w100" style="aspect-ratio:16/9;border-radius:12px;"></div>
                <div style="display:flex;gap:8px;margin-top:10px;">
                    ${Array.from({length:4}, () => '<div class="sk sk-w100" style="height:48px;border-radius:6px;flex:1;"></div>').join('')}
                </div>
                <div style="background:rgba(255,255,255,0.025);border:1px solid rgba(255,255,255,0.06);border-radius:12px;padding:24px;margin-top:24px;display:flex;flex-direction:column;gap:14px;">
                    <div class="sk sk-h28 sk-w50"></div>
                    <div class="sk sk-h12 sk-w100"></div>
                    <div class="sk sk-h12 sk-w70"></div>
                    <div class="sk sk-h12 sk-w80"></div>
                </div>`;

            if (sideCol) sideCol.innerHTML = `
                <div style="background:rgba(255,255,255,0.025);border:1px solid rgba(255,255,255,0.07);border-radius:14px;padding:24px;display:flex;flex-direction:column;gap:16px;">
                    <div class="sk sk-h44 sk-w50"></div>
                    <div class="sk sk-h16 sk-w30"></div>
                    <div class="sk sk-h36 sk-w100" style="border-radius:8px;"></div>
                    <div class="sk sk-h36 sk-w100" style="border-radius:8px;"></div>
                    <div class="sk sk-h44 sk-w100" style="border-radius:10px;margin-top:4px;"></div>
                    <div style="display:flex;gap:10px;">
                        <div class="sk sk-h36" style="flex:1;border-radius:8px;"></div>
                        <div class="sk sk-h36" style="flex:1;border-radius:8px;"></div>
                    </div>
                </div>`;

            if (relatedGrid) relatedGrid.innerHTML = Array.from({ length: 4 }, () => `
                <div style="width:190px;background:rgba(255,255,255,0.025);border:1px solid rgba(255,255,255,0.06);border-radius:10px;overflow:hidden;flex-shrink:0;">
                    <div class="sk sk-w100" style="aspect-ratio:16/9;"></div>
                    <div style="padding:12px;display:flex;flex-direction:column;gap:8px;">
                        <div class="sk sk-h16" style="width:70%;"></div>
                        <div class="sk sk-h16" style="width:40%;"></div>
                    </div>
                </div>`).join('');
        }

        /** 移除骨架屏，恢复真实 DOM */
        function removeSkeleton() {
            const mainCol    = document.querySelector('.detail-main-col');
            const sideCol    = document.querySelector('.detail-side-col');
            const relatedGrid = document.getElementById('relatedProductsGrid');

            if (mainCol    && _skeletonCache.mainCol)  mainCol.innerHTML  = _skeletonCache.mainCol;
            if (sideCol    && _skeletonCache.sideCol)  sideCol.innerHTML  = _skeletonCache.sideCol;
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
                            originalPrice: originalPrice ?? price,
                            salePrice: salePrice,
                            category: asset.category ?? asset.type ?? '--',
                            // 规格字段：优先从平铺级别获取，回退到 specs 对象
                            rating: (asset.rating !== undefined && asset.rating !== null) ? Number(asset.rating) : ((asset.specs?.rating !== undefined && asset.specs.rating !== null) ? Number(asset.specs.rating) : 0),
                            reviews: asset.reviewCount ?? asset.reviews ?? (asset.specs?.reviewCount ?? 0),
                            reviewCount: asset.reviewCount ?? asset.reviews ?? (asset.specs?.reviewCount ?? 0),
                            version: asset.version ?? '--',
                            compatibility: asset.compatibility ?? (asset.specs?.compatibility ?? '--'),
                            downloads: asset.downloads ?? (asset.specs?.downloads ?? 0),
                            downloadCount: asset.downloads ?? (asset.specs?.downloads ?? 0),
                            purchaseCount: asset.purchaseCount ?? (asset.specs?.purchaseCount ?? 0),
                            fileSize: fileSizeStr,
                            uploadDate: asset.uploadDate ?? (asset.specs?.uploadDate ?? asset.createdAt ?? '--'),
                            author: asset.author ?? (asset.specs?.author ?? '--'),
                            license: asset.license ?? (asset.specs?.license ?? '--'),
                            discountRate: (asset.discountRate !== undefined && asset.discountRate !== null) ? Number(asset.discountRate) : 0,
                            favoriteCount: asset.favoriteCount ?? (asset.specs?.favoriteCount ?? 0),
                            viewCount: asset.viewCount ?? (asset.specs?.viewCount ?? 0),
                            downloadUrl: asset.downloadUrl ?? (asset.specs?.downloadUrl ?? ''),
                            // 价格方案数组
                            prices: Array.isArray(asset.prices) ? asset.prices : (Array.isArray(asset.Prices) ? asset.Prices : []),
                            // 媒体资源
                            primaryImage: asset.primaryImage || '',
                            thumbnailImage: asset.thumbnailImage || '',
                            screenshots: Array.isArray(asset.screenshots) ? asset.screenshots : [],
                            // 标签
                            tags: Array.isArray(asset.tags) ? asset.tags : [],
                            // 规格子表（保持原始数据）
                            specs: asset.specs || null,
                            isDeleted: asset.isDeleted ?? false
                        };
                    }
                }
            } catch (e) {
                console.warn('获取商品详情失败，使用本地回退数据', e);
            }

            removeSkeleton();
            loadProductData(product);
            renderGallery(product);
            setupEventListeners();
            loadRelatedProducts(productId);
            // 全局初始化（如果存在这些函数）
            try { initGlobalTopbar && initGlobalTopbar(); } catch (e) {}
            try { initGlobalFooter && initGlobalFooter(); } catch (e) {}
            try { initButtonEffects && initButtonEffects(); } catch (e) {}
        }

        /** 将后端/本地产品数据渲染到页面所有元素 */
        function loadProductData(product) {
            const orDash = (v) => (v === null || v === undefined) ? '--' : v;
            const showDate = (v) => {
                if (!v || v === '--') return '--';
                if (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}/.test(v)) return v.slice(0, 10);
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
            const setText = (id, val) => { const el = document.getElementById(id); if (el) el.textContent = orDash(val); };

            // ── 面包屑 + 页面标题 ──
            setText('breadcrumbCategory', product.category);
            const pageTitleEl = document.getElementById('pageTitle');
            if (pageTitleEl) pageTitleEl.textContent = (product.name || '商品详情') + ' - KaxHub';

            // ── 英雄区图标 ──
            const heroIconEl = document.getElementById('heroIcon');
            if (heroIconEl && product.primaryImage) {
                heroIconEl.innerHTML = `<img src="${product.primaryImage}" alt="${product.name || ''}" style="width:100%;height:100%;object-fit:cover;border-radius:inherit;">`;
            }

            // ── 英雄区背景 ──
            const heroBgEl = document.getElementById('productHeroBg');
            if (heroBgEl && product.primaryImage) {
                heroBgEl.style.backgroundImage = `url(${product.primaryImage})`;
            }

            // ── 英雄区 ──
            setText('productName',    product.name);
            setText('heroDesc',       product.description);
            setText('heroCategory',   product.category);
            setText('heroAuthor',     product.author);
            setText('heroDownloads',  showDownloads(product.downloads));
            setText('heroVersion',    product.version);

            // 评分（英雄区 + 评价 Tab 同步）
            const ratingNum = (product.rating != null && !isNaN(Number(product.rating))) ? Number(product.rating) : null;
            const starsStr  = ratingNum != null
                ? '★'.repeat(Math.round(ratingNum)) + '☆'.repeat(5 - Math.round(ratingNum))
                : '☆☆☆☆☆';
            const ratingStr = ratingNum != null ? ratingNum.toFixed(1) : '--';
            // reviewCount 和 reviews 都可能存在，优先使用 reviewCount
            const reviewsNum = product.reviewCount ?? product.reviews;
            const reviewsStr = reviewsNum != null ? String(reviewsNum) : '--';

            // 英雄区评分 IDs：heroStars / heroRatingVal / heroRatingCount
            setText('heroStars',       starsStr);
            setText('heroRatingVal',   ratingStr);
            setText('heroRatingCount', '(' + reviewsStr + ')');

            // 评价 Tab IDs：reviewScoreBig / reviewStarsBig / reviewsTotal
            setText('reviewScoreBig',  ratingStr);
            setText('reviewStarsBig',  starsStr);
            setText('reviewsTotal',    reviewsStr + ' 条评价');

            // ── 价格计算 ──
            let displayOriginal = null;
            let displayCurrent  = null;
            if (product.prices && Array.isArray(product.prices) && product.prices.length > 0) {
                const p0 = product.prices[0];
                const origCents = Number(p0.originalPrice ?? p0.price ?? 0);
                const disc      = Number(p0.discountRate) || 0;
                const saleCents = Math.round(origCents * (1 - disc));
                displayOriginal = origCents;
                displayCurrent  = saleCents;
                product.discountRate = disc;
            } else {
                displayOriginal = product.originalPrice ?? null;
                displayCurrent  = product.salePrice ?? product.price ?? null;
            }

            // 当前价
            const priceCurrentEl = document.getElementById('priceCurrent');
            if (priceCurrentEl) priceCurrentEl.textContent = displayCurrent != null ? showCurrency(displayCurrent) : '--';

            // 原价（用 hidden 属性控制）
            const priceOriginalEl = document.getElementById('priceOriginal');
            if (priceOriginalEl) {
                if (displayOriginal != null && displayCurrent != null && Number(displayOriginal) > Number(displayCurrent)) {
                    priceOriginalEl.textContent = showCurrency(displayOriginal);
                    priceOriginalEl.removeAttribute('hidden');
                } else {
                    priceOriginalEl.textContent = '';
                    priceOriginalEl.setAttribute('hidden', '');
                }
            }

            // 折扣徽标（用 hidden 属性控制）
            const priceBadgeEl = document.getElementById('priceBadge');
            if (priceBadgeEl) {
                let discountPercent = null;
                if (product.discountRate != null) {
                    discountPercent = Math.round(Number(product.discountRate) * 100);
                } else if (displayOriginal != null && displayCurrent != null && Number(displayOriginal) > 0) {
                    const d = Math.round(((Number(displayOriginal) - Number(displayCurrent)) / Number(displayOriginal)) * 100);
                    if (d > 0) discountPercent = d;
                }
                if (discountPercent != null && discountPercent > 0) {
                    priceBadgeEl.textContent = discountPercent + '% OFF';
                    priceBadgeEl.removeAttribute('hidden');
                } else {
                    priceBadgeEl.textContent = '';
                    priceBadgeEl.setAttribute('hidden', '');
                }
            }

            // ── 库存状态（根据价格方案的库存汇总） ──
            const stockInfoEl = document.getElementById('stockInfo');
            if (stockInfoEl) {
                let stockText  = '库存：--';
                let isLow      = false;

                // 从价格方案中计算汇总库存（-1 表示无限）
                const prices = product.prices || [];
                if (prices.length > 0) {
                    const hasUnlimited = prices.some(p => (p.stock ?? -1) < 0);
                    if (hasUnlimited) {
                        stockText = '库存：充足';
                    } else {
                        const totalStock = prices.reduce((sum, p) => sum + Math.max(0, p.stock ?? 0), 0);
                        if (totalStock > 50) {
                            stockText = `库存：${totalStock}（充足）`;
                        } else if (totalStock > 0) {
                            stockText = `库存：${totalStock}（有限）`; isLow = true;
                        } else {
                            stockText = '库存：0（暂无）'; isLow = true;
                        }
                    }
                }

                const stockTextEl = stockInfoEl.querySelector('.stock-text');
                if (stockTextEl) {
                    stockTextEl.textContent = stockText;
                } else {
                    stockInfoEl.textContent = stockText;
                }
                stockInfoEl.classList.toggle('low', isLow);
            }

            // ── 规格 Tab —— 使用新 HTML 中预置的独立 ID ──
            setText('specSize',           product.fileSize);
            setText('productVersion',     product.version);
            setText('specDate',           showDate(product.uploadDate));
            setText('specAuthor',         product.author);
            setText('productCompatibility', product.compatibility);
            setText('specLicense',        product.license);
            setText('productDownloads',   showDownloads(product.downloads));
            const purchasesEl = document.getElementById('productPurchases');
            if (purchasesEl) purchasesEl.textContent = showDownloads(product.purchaseCount);

            // 将当前产品数据挂到 window，供事件监听器使用
            window.currentProduct = product;
        }

        /** 绑定页面所有交互事件 */
        function setupEventListeners() {
            // ── Tab 页签切换 ──
            const tabs   = document.querySelectorAll('.detail-tab[data-tab]');
            const panels = document.querySelectorAll('.detail-tab-panel');
            tabs.forEach(tab => {
                tab.addEventListener('click', () => {
                    const target = tab.dataset.tab;
                    tabs.forEach(t => {
                        t.classList.toggle('active', t === tab);
                        t.setAttribute('aria-selected', t === tab ? 'true' : 'false');
                    });
                    panels.forEach(p => {
                        const isTarget = p.id === 'panel-' + target;
                        p.classList.toggle('active', isTarget);
                        if (isTarget) p.removeAttribute('hidden'); else p.setAttribute('hidden', '');
                    });
                });
            });

            // ── 购买面板元素 ──
            const purchaseBtn = document.getElementById('purchaseBtn');
            const plansGrid = document.getElementById('plansGrid');
            const favBtn = document.getElementById('favBtn');
            const shareBtn = document.getElementById('shareBtn');
            
            // 存储当前选中的价格方案ID
            let selectedPriceId = null;

            /** 同步设置购买按钮启用/禁用状态（disabled + aria-disabled） */
            function setPurchaseBtnEnabled(enabled) {
                purchaseBtn.disabled = !enabled;
                if (enabled) {
                    purchaseBtn.removeAttribute('aria-disabled');
                } else {
                    purchaseBtn.setAttribute('aria-disabled', 'true');
                }
            }

            /** 动态生成和显示价格套餐 */
            function loadPricePlans() {
                const product = window.currentProduct;
                let prices = (product && Array.isArray(product.prices) && product.prices.length > 0) ? product.prices : null;

                // 如果后端没有返回 prices 数组但有基础价格，自动构建默认方案
                if (!prices && product && product.price != null) {
                    prices = [{
                        id: '__default__',
                        price: product.price,
                        originalPrice: product.originalPrice ?? product.price,
                        discountRate: product.discountRate ?? 0,
                        unit: null,
                        duration: null
                    }];
                }

                if (!prices || prices.length === 0) {
                    plansGrid.innerHTML = '<div style="color: var(--text-muted); padding: 12px;">暂无价格方案</div>';
                    setPurchaseBtnEnabled(false);
                    return;
                }

                plansGrid.innerHTML = '';
                prices.forEach((price, index) => {
                    let durationText = '';
                    switch (price.unit?.toLowerCase()) {
                        case 'year':  durationText = `${price.duration}年`; break;
                        case 'month': durationText = `${price.duration}个月`; break;
                        case 'day':   durationText = `${price.duration}天`; break;
                        case 'hour':  durationText = `${price.duration}小时`; break;
                        default:      durationText = '一次性';
                    }

                    const salePrice = price.price * (1 - (price.discountRate || 0));
                    const hasDiscount = price.discountRate && price.discountRate > 0;

                    // 每个方案的独立库存（-1 表示无限）
                    const planStock = price.stock ?? -1;
                    const stockHtml = planStock < 0 ? '' : (planStock > 0 ? `<span style="color: var(--text-muted); font-size: 12px;">库存: ${planStock}</span>` : `<span style="color: var(--danger, #e74c3c); font-size: 12px;">已售罄</span>`);
                    const isOutOfStock = planStock === 0;
                    
                    const planItem = document.createElement('button');
                    planItem.className = 'plan-item' + (index === 0 && !isOutOfStock ? ' selected' : '');
                    planItem.type = 'button';
                    if (isOutOfStock) planItem.disabled = true;
                    planItem.innerHTML = `
                        <div class="plan-name">${durationText}</div>
                        <div class="plan-details">
                            ${hasDiscount ? `<span style="text-decoration: line-through; color: var(--text-muted); margin-right: 4px;">💰${Number(price.originalPrice).toFixed(2)}</span>` : ''}
                            <span style="color: var(--accent); font-weight: 600;">💰${Number(salePrice).toFixed(2)}</span>
                        </div>
                        ${stockHtml ? `<div class="plan-stock">${stockHtml}</div>` : ''}
                    `;
                    
                    planItem.addEventListener('click', () => {
                        if (isOutOfStock) return;
                        document.querySelectorAll('.plan-item').forEach(item => item.classList.remove('selected'));
                        planItem.classList.add('selected');
                        selectedPriceId = price.id;
                        setPurchaseBtnEnabled(true);
                    });

                    plansGrid.appendChild(planItem);
                    
                    if (index === 0 && !isOutOfStock) {
                        selectedPriceId = price.id;
                        setPurchaseBtnEnabled(true);
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
                purchaseBtn.innerHTML = '<span class="material-icons" style="font-size:18px;vertical-align:middle;">hourglass_top</span> 处理中…';

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
                        // 更新本地 product 对象的统计数据
                        if (data.data) {
                            if (data.data.purchaseCount != null) product.purchaseCount = data.data.purchaseCount;
                            if (data.data.favoriteCount != null) product.favoriteCount = data.data.favoriteCount;
                            if (data.data.viewCount != null) product.viewCount = data.data.viewCount;
                            if (data.data.rating != null) product.rating = data.data.rating;
                            if (data.data.downloads != null) product.downloads = data.data.downloads;
                        }
                        
                        // 立即刷新页面显示新的统计数据
                        purchaseBtn.innerHTML = '<span class="material-icons" style="font-size:18px;vertical-align:middle;">check_circle</span> 购买成功';
                        setTimeout(() => {
                            alert('购买成功！');
                            // 重新加载商品详情页面以显示最新数据
                            location.reload();
                        }, 1500);
                    } else {
                        alert('购买失败: ' + (data.message || '未知错误'));
                        purchaseBtn.innerHTML = '<span class="material-icons">shopping_bag</span> 立即购买';
                        purchaseBtn.disabled = false;
                    }
                } catch (e) {
                    console.error('购买失败', e);
                    alert('网络错误，请稍后重试');
                    purchaseBtn.innerHTML = '<span class="material-icons">shopping_bag</span> 立即购买';
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
                        shareBtn.innerHTML = '<span class="material-icons">check</span><span>已复制</span>';
                        setTimeout(() => {
                            shareBtn.innerHTML = '<span class="material-icons">share</span>';
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

        /** 渲染截图画廊（主图 + 截图列表），无图片则保留占位 */
        function renderGallery(product) {
            const track = document.getElementById('galleryTrack');
            const thumbContainer = document.getElementById('thumbnailContainer');
            const prevBtn = document.getElementById('galleryPrev');
            const nextBtn = document.getElementById('galleryNext');
            if (!track) return;

            const images = [];
            if (product.primaryImage) images.push(product.primaryImage);
            if (Array.isArray(product.screenshots)) {
                product.screenshots.forEach(url => { if (url) images.push(url); });
            }
            if (images.length === 0) return;

            track.innerHTML = images.map((url, i) =>
                `<div class="gallery-slide${i === 0 ? ' active' : ''}">
                    <img src="${url}" alt="截图 ${i + 1}" style="width:100%;height:100%;object-fit:cover;border-radius:12px;">
                </div>`
            ).join('');

            if (thumbContainer) {
                thumbContainer.innerHTML = images.map((url, i) =>
                    `<div class="gallery-thumb${i === 0 ? ' active' : ''}" role="listitem" data-index="${i}">
                        <img src="${url}" alt="缩略图 ${i + 1}" style="width:100%;height:100%;object-fit:cover;border-radius:inherit;">
                    </div>`
                ).join('');
            }

            let currentSlide = 0;
            const slides = track.querySelectorAll('.gallery-slide');
            const thumbs = thumbContainer ? thumbContainer.querySelectorAll('.gallery-thumb') : [];

            function showSlide(idx) {
                if (idx < 0 || idx >= slides.length) return;
                slides[currentSlide].classList.remove('active');
                if (thumbs[currentSlide]) thumbs[currentSlide].classList.remove('active');
                currentSlide = idx;
                slides[currentSlide].classList.add('active');
                if (thumbs[currentSlide]) thumbs[currentSlide].classList.add('active');
                if (prevBtn) prevBtn.disabled = currentSlide === 0;
                if (nextBtn) nextBtn.disabled = currentSlide === slides.length - 1;
            }

            if (prevBtn) prevBtn.addEventListener('click', () => showSlide(currentSlide - 1));
            if (nextBtn) nextBtn.addEventListener('click', () => showSlide(currentSlide + 1));
            thumbs.forEach(t => t.addEventListener('click', () => showSlide(Number(t.dataset.index))));

            if (prevBtn) prevBtn.disabled = true;
            if (nextBtn) nextBtn.disabled = slides.length <= 1;
        }

        /** 从后端 API 加载相关推荐商品并渲染到推荐区域 */
        async function loadRelatedProducts(currentId) {
            const grid = document.getElementById('relatedProductsGrid');
            if (!grid) return;

            try {
                const resp = await fetch(`/api/asset/related/${currentId}?top=4`, { credentials: 'same-origin' });
                if (!resp.ok) return;

                const json = await resp.json();
                const items = (json && json.data) ? json.data : [];
                if (!Array.isArray(items) || items.length === 0) return;

                const showCurrency = (v) => (v === null || v === undefined) ? '--' : ('💰' + Number(v).toFixed(2));

                grid.innerHTML = items.map(item => {
                    const thumbSrc = item.thumbnailImage || item.primaryImage || '';
                    const thumbHtml = thumbSrc
                        ? `<img src="${thumbSrc}" alt="${item.name || ''}" style="width:100%;height:100%;object-fit:cover;">`
                        : '🎮';
                    const displayPrice = item.salePrice != null ? item.salePrice : item.price;
                    return `<div class="related-card" onclick="goToShopDetail(${item.id})">
                        <div class="related-thumb">${thumbHtml}</div>
                        <div class="related-info">
                            <div class="related-name">${item.name || '--'}</div>
                            <div class="related-meta">${item.category || '--'}</div>
                            <div class="related-price">${showCurrency(displayPrice)}</div>
                        </div>
                    </div>`;
                }).join('');
            } catch (e) {
                console.warn('加载相关推荐失败', e);
            }
        }

        // 页面加载时初始化
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', initPage);
        } else {
            initPage();
        }
