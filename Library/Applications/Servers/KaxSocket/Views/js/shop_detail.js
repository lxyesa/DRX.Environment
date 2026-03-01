/** 时间戳或日期字符串 → zh-CN 日期显示 */
        function formatDate(ts) {
            if (!ts || ts === '--') return '--';
            const ms = ts > 9999999999 ? ts : ts * 1000;
            const date = new Date(ms);
            if (isNaN(date.getTime())) return '--';
            return date.toLocaleDateString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit' });
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
                        const coverImage = getAssetField(asset, 'coverImage', 'CoverImage') || '';
                        const iconImage = getAssetField(asset, 'iconImage', 'IconImage') || '';
                        const screenshots = normalizeMediaArray(getAssetField(asset, 'screenshots', 'Screenshots'));

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
                            coverImage: coverImage,
                            iconImage: iconImage,
                            screenshots: screenshots,
                            // 标签
                            tags: Array.isArray(asset.tags) ? asset.tags : [],
                            // 徽章与特性（JSON 字符串，空则使用默认值）
                            badges: asset.badges || '',
                            features: asset.features || '',
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
            try {
                loadProductData(product);
                renderBadges(product);
                renderFeatures(product);
                renderGallery(product);
                setupEventListeners();
            } catch (renderErr) {
                console.error('[shop_detail] 页面渲染出错，部分功能可能不可用', renderErr);
            }
            try { loadRelatedProducts(productId); } catch (e) { console.warn('[shop_detail] 加载相关推荐出错', e); }
            try { initGlobalTopbar && initGlobalTopbar(); } catch (e) {}
            try { initGlobalFooter && initGlobalFooter(); } catch (e) {}
            try { initButtonEffects && initButtonEffects(); } catch (e) {}

            // System 权限检测 → 初始化编辑模式（await 确保完成初始化）
            await checkSystemPermission();
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

            // ── 英雄区图标（显示 iconImage）──
            const heroIconEl = document.getElementById('heroIcon');
            if (heroIconEl && product.iconImage) {
                heroIconEl.innerHTML = `<img src="${product.iconImage}" alt="${product.name || ''}" style="width:100%;height:100%;object-fit:cover;border-radius:inherit;">`;
            }

            // ── 英雄区背景（显示 coverImage 封面）──
            const heroBgEl = document.getElementById('productHeroBg');
            if (heroBgEl && product.coverImage) {
                heroBgEl.style.backgroundImage = `url(${product.coverImage})`;
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

            // ── 概述 Tab 描述 ──
            setText('productDescription', product.description);

            // 将当前产品数据挂到 window，供事件监听器使用
            window.currentProduct = product;
        }

        /** 根据选中的价格对象更新顶部价格显示 */
        function updatePriceDisplay(priceObj) {
            let displayOriginal = null;
            let displayCurrent  = null;

            if (priceObj) {
                const origCents = Number(priceObj.originalPrice ?? priceObj.price ?? 0);
                const disc      = Number(priceObj.discountRate) || 0;
                const saleCents = origCents * (1 - disc);
                displayOriginal = origCents;
                displayCurrent  = saleCents;
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
                if (priceObj && priceObj.discountRate != null) {
                    discountPercent = Math.round(Number(priceObj.discountRate) * 100);
                } else if (displayOriginal != null && displayCurrent != null && Number(displayOriginal) > 0) {
                    const d = Math.round(((Number(displayOriginal) - Number(displayCurrent)) / Number(displayOriginal)) * 100);
                    if (d > 0) discountPercent = d;
                }
                
                if (discountPercent != null && discountPercent > 0) {
                    // 有折扣：显示折扣百分比
                    priceBadgeEl.textContent = discountPercent + '% OFF';
                    priceBadgeEl.classList.remove('no-discount');
                    priceBadgeEl.removeAttribute('hidden');
                } else {
                    // 无折扣：显示"无折扣"提示
                    priceBadgeEl.textContent = '无折扣';
                    priceBadgeEl.classList.add('no-discount');
                    priceBadgeEl.removeAttribute('hidden');
                }
            }
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

                    // price.price 已是最终价格（originalPrice × (1 - discountRate)），直接使用
                    const salePrice = price.price;
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
                        // 更新顶部价格显示
                        updatePriceDisplay(price);
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
                    showMsgBox({ title: '请选择方案', message: '请先选择一个购买方案后再继续。', type: 'warn' });
                    return;
                }

                const product = window.currentProduct;
                const token = localStorage.getItem('kax_login_token');

                if (!token) {
                    showMsgBox({ title: '未登录', message: '请先登录后再进行购买。', type: 'warn', onConfirm: () => { location.href = '/login'; } });
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
                        showMsgBox({
                            title: '购买成功',
                            message: '恭喜！你已成功购买该资产，页面即将刷新。',
                            type: 'success',
                            onConfirm: () => { location.reload(); }
                        });
                    } else {
                        showMsgBox({ title: '购买失败', message: data.message || '未知错误，请稍后再试。', type: 'error' });
                        purchaseBtn.innerHTML = '<span class="material-icons">shopping_bag</span> 立即购买';
                        purchaseBtn.disabled = false;
                    }
                } catch (e) {
                    console.error('购买失败', e);
                    showMsgBox({ title: '网络错误', message: '网络连接异常，请稍后重试。', type: 'error' });
                    purchaseBtn.innerHTML = '<span class="material-icons">shopping_bag</span> 立即购买';
                    purchaseBtn.disabled = false;
                }
            });

            // 收藏按钮 — 使用后端 API（登录用户），未登录则跳转登录
            favBtn.addEventListener('click', async () => {
                const product = window.currentProduct;
                const token = localStorage.getItem('kax_login_token');
                const icon = favBtn.querySelector('.material-icons');

                if (!token) { showMsgBox({ title: '未登录', message: '请先登录以使用收藏功能。', type: 'warn', onConfirm: () => { location.href = '/login'; } }); return; }

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

        /** 渲染截图画廊（仅显示截图，不含封面），无图片则保留占位 */
        function renderGallery(product) {
            const track = document.getElementById('galleryTrack');
            const thumbContainer = document.getElementById('thumbnailContainer');
            const prevBtn = document.getElementById('galleryPrev');
            const nextBtn = document.getElementById('galleryNext');
            if (!track) return;

            // 只收集截图，不加入封面
            const images = [];
            if (Array.isArray(product.screenshots)) {
                product.screenshots.forEach(url => { if (url) images.push(url); });
            }
            if (images.length === 0) return;

            track.innerHTML = images.map((url, i) =>
                `<div class="gallery-slide${i === 0 ? ' active' : ''}">
                    <img src="${url}" alt="截图 ${i + 1}" style="width:100%;height:100%;object-fit:cover;border-radius:12px;">
                </div>`
            ).join('');

            // 不使用 tile（隐藏缩略图导航条）
            if (thumbContainer) {
                thumbContainer.style.display = 'none';
            }

            let currentSlide = 0;
            const slides = track.querySelectorAll('.gallery-slide');

            function showSlide(idx) {
                if (idx < 0 || idx >= slides.length) return;
                slides[currentSlide].classList.remove('active');
                currentSlide = idx;
                slides[currentSlide].classList.add('active');
                track.style.transform = `translateX(-${currentSlide * 100}%)`;
                if (prevBtn) prevBtn.disabled = currentSlide === 0;
                if (nextBtn) nextBtn.disabled = currentSlide === slides.length - 1;
            }

            if (prevBtn) prevBtn.addEventListener('click', () => showSlide(currentSlide - 1));
            if (nextBtn) nextBtn.addEventListener('click', () => showSlide(currentSlide + 1));

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
                    const thumbSrc = item.iconImage || item.IconImage || item.coverImage || item.CoverImage || normalizeMediaArray(item.screenshots || item.Screenshots)[0] || '';
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

        // ================================================================
        // 默认徽章与特性数据（向后兼容）
        // ================================================================
        const DEFAULT_BADGES = [
            { icon: 'verified_user', text: '安全认证资源' },
            { icon: 'support_agent', text: '官方技术支持' },
            { icon: 'history', text: '版本更新保障' }
        ];

        const DEFAULT_FEATURES = [
            { icon: 'bolt', title: '高性能', desc: '优化的运行效率，流畅无卡顿' },
            { icon: 'shield', title: '安全可靠', desc: '通过安全扫描，保护你的游戏环境' },
            { icon: 'update', title: '持续更新', desc: '定期维护，跟进游戏版本' },
            { icon: 'people', title: '社区支持', desc: '活跃社区，问题快速响应' }
        ];

        /** 解析 JSON 字符串，失败返回 null */
        function tryParseJson(str) {
            if (!str || typeof str !== 'string') return null;
            try { return JSON.parse(str); } catch { return null; }
        }

        /** 渲染右侧购买面板的徽章列表 */
        function renderBadges(product) {
            const container = document.getElementById('panelBadges');
            if (!container) return;

            let badges = tryParseJson(product.badges);
            if (!Array.isArray(badges) || badges.length === 0) badges = DEFAULT_BADGES;

            // 保存到 product 对象供编辑模式使用
            product._parsedBadges = badges;

            container.innerHTML = badges.map(b =>
                `<div class="pqi-item">
                    <span class="material-icons pqi-icon">${escHtml(b.icon || 'info')}</span>
                    <span>${escHtml(b.text || '')}</span>
                </div>`
            ).join('');
        }

        /** 渲染概述 Tab 的亮点特性卡片 */
        function renderFeatures(product) {
            const container = document.getElementById('featuresGrid');
            if (!container) return;

            let features = tryParseJson(product.features);
            if (!Array.isArray(features) || features.length === 0) features = DEFAULT_FEATURES;

            // 保存到 product 对象供编辑模式使用
            product._parsedFeatures = features;

            container.innerHTML = features.map(f =>
                `<div class="feature-item">
                    <span class="feature-icon material-icons">${escHtml(f.icon || 'star')}</span>
                    <div>
                        <div class="feature-title">${escHtml(f.title || '')}</div>
                        <div class="feature-desc">${escHtml(f.desc || '')}</div>
                    </div>
                </div>`
            ).join('');
        }

        /** HTML 转义 */
        function escHtml(str) {
            const div = document.createElement('div');
            div.textContent = String(str);
            return div.innerHTML.replace(/"/g, '&quot;');
        }

        // ================================================================
        // System 权限检测与编辑模式
        // ================================================================
        window._isSystemUser = false;
        window._editMode = false;
        window._editedFields = {};

        /** 检查当前用户是否为 System 权限组 */
        async function checkSystemPermission() {
            const token = localStorage.getItem('kax_login_token');
            if (!token) { console.log('[编辑模式] 未检测到登录令牌，跳过权限检测'); return; }

            try {
                const resp = await fetch('/api/user/verify/account', {
                    method: 'POST',
                    headers: { 'Authorization': 'Bearer ' + token }
                });
                if (!resp.ok) { console.warn('[编辑模式] 权限接口返回非200:', resp.status); return; }

                const data = await resp.json();
                const permGroup = data.permissionGroup ?? data.permission_group ?? 999;
                console.log('[编辑模式] 权限检测结果: permissionGroup =', permGroup);

                if (Number(permGroup) === 0) {
                    window._isSystemUser = true;
                    console.log('[编辑模式] System 用户确认，初始化编辑模式');
                    initEditMode();
                }
            } catch (e) {
                console.warn('[编辑模式] 权限检测失败', e);
            }
        }

        /** 初始化编辑模式（显示切换按钮，绑定事件） */
        function initEditMode() {
            const toggleBtn = document.getElementById('editModeToggle');
            const toolbar = document.getElementById('editToolbar');
            const saveBtn = document.getElementById('editSaveBtn');
            const cancelBtn = document.getElementById('editCancelBtn');
            if (!toggleBtn) { console.warn('[编辑模式] 未找到 editModeToggle 按钮'); return; }
            console.log('[编辑模式] 编辑模式 UI 已初始化，切换按钮已显示');

            // 显示编辑模式切换按钮
            toggleBtn.style.display = 'flex';

            // 切换编辑模式
            toggleBtn.addEventListener('click', () => {
                if (window._editMode) {
                    exitEditMode();
                } else {
                    enterEditMode();
                }
            });

            // 保存
            saveBtn.addEventListener('click', () => saveProductChanges());

            // 退出
            cancelBtn.addEventListener('click', () => {
                if (Object.keys(window._editedFields).length > 0) {
                    if (!confirm('有未保存的更改，确定退出编辑模式？')) return;
                }
                exitEditMode();
            });
        }

        /** 进入编辑模式 */
        function enterEditMode() {
            window._editMode = true;
            window._editedFields = {};
            document.body.classList.add('edit-mode');

            const toggleBtn = document.getElementById('editModeToggle');
            const toolbar = document.getElementById('editToolbar');

            toggleBtn.classList.add('active');
            toggleBtn.querySelector('.edit-mode-label').textContent = '退出编辑';
            toggleBtn.title = '退出编辑模式';
            toolbar.style.display = 'flex';

            // 给可编辑的文本元素加上 editable 类和点击事件
            attachEditableHandlers();
        }

        /** 退出编辑模式 */
        function exitEditMode() {
            window._editMode = false;
            window._editedFields = {};
            document.body.classList.remove('edit-mode');

            const toggleBtn = document.getElementById('editModeToggle');
            const toolbar = document.getElementById('editToolbar');

            toggleBtn.classList.remove('active');
            toggleBtn.querySelector('.edit-mode-label').textContent = '编辑模式';
            toggleBtn.title = '进入编辑模式';
            toolbar.style.display = 'none';

            // 移除 editable 类和事件
            detachEditableHandlers();
        }

        // 可编辑字段映射：{ fieldKey: { elementId, type, label, maxLength? } }
        const EDITABLE_FIELDS = {
            name:          { elementId: 'productName',       type: 'input',    label: '商品名称',   maxLength: 200 },
            description:   { elementId: 'heroDesc',          type: 'textarea', label: '商品描述',   maxLength: 500 },
            category:      { elementId: 'heroCategory',      type: 'input',    label: '分类',       maxLength: 50 },
            author:        { elementId: 'heroAuthor',        type: 'input',    label: '作者',       maxLength: 100 },
            version:       { elementId: 'heroVersion',       type: 'input',    label: '版本号',     maxLength: 50 },
            fullDesc:      { elementId: 'productDescription', type: 'textarea', label: '详细描述',   maxLength: 500 },
            license:       { elementId: 'specLicense',       type: 'input',    label: '许可证',     maxLength: 100 },
            compatibility: { elementId: 'productCompatibility', type: 'input', label: '兼容性',     maxLength: 100 },
        };

        /** 存储事件处理器的引用，以便 detach */
        const _editHandlers = new Map();

        /** 给可编辑元素绑定点击事件和 editable 类 */
        function attachEditableHandlers() {
            // 文本字段
            for (const [fieldKey, cfg] of Object.entries(EDITABLE_FIELDS)) {
                const el = document.getElementById(cfg.elementId);
                if (!el) continue;

                el.classList.add('editable');
                const handler = (e) => {
                    e.stopPropagation();
                    openFieldEditor(fieldKey, cfg, el);
                };
                el.addEventListener('click', handler);
                _editHandlers.set(cfg.elementId, handler);
            }

            // 徽章列表
            const badgesEl = document.getElementById('panelBadges');
            if (badgesEl) {
                const handler = (e) => {
                    e.stopPropagation();
                    openBadgesEditor();
                };
                badgesEl.addEventListener('click', handler);
                _editHandlers.set('panelBadges', handler);
            }

            // 特性列表
            const featuresEl = document.getElementById('featuresGrid');
            if (featuresEl) {
                const handler = (e) => {
                    e.stopPropagation();
                    openFeaturesEditor();
                };
                featuresEl.addEventListener('click', handler);
                _editHandlers.set('featuresGrid', handler);
            }
        }

        /** 移除所有可编辑事件和 editable 类 */
        function detachEditableHandlers() {
            for (const [fieldKey, cfg] of Object.entries(EDITABLE_FIELDS)) {
                const el = document.getElementById(cfg.elementId);
                if (!el) continue;
                el.classList.remove('editable');
                const handler = _editHandlers.get(cfg.elementId);
                if (handler) el.removeEventListener('click', handler);
            }

            // 徽章
            const badgesEl = document.getElementById('panelBadges');
            if (badgesEl) {
                const handler = _editHandlers.get('panelBadges');
                if (handler) badgesEl.removeEventListener('click', handler);
            }

            // 特性
            const featuresEl = document.getElementById('featuresGrid');
            if (featuresEl) {
                const handler = _editHandlers.get('featuresGrid');
                if (handler) featuresEl.removeEventListener('click', handler);
            }

            _editHandlers.clear();
        }

        // ================================================================
        // 字段编辑弹窗
        // ================================================================

        /** 打开单字段编辑弹窗 */
        function openFieldEditor(fieldKey, cfg, el) {
            const overlay = document.getElementById('editOverlay');
            const titleEl = document.getElementById('editPopupTitle');
            const bodyEl = document.getElementById('editPopupBody');
            const confirmBtn = document.getElementById('editPopupConfirm');
            const cancelBtn = document.getElementById('editPopupCancel');
            const closeBtn = document.getElementById('editPopupClose');

            titleEl.textContent = '编辑 — ' + cfg.label;

            const currentValue = getFieldCurrentValue(fieldKey);

            if (cfg.type === 'textarea') {
                bodyEl.innerHTML = `<div class="edit-field-group">
                    <label>${escHtml(cfg.label)}</label>
                    <textarea id="editFieldInput" rows="5" maxlength="${cfg.maxLength || 500}" placeholder="请输入${cfg.label}">${escHtml(currentValue)}</textarea>
                    <div style="text-align:right;font-size:11px;color:var(--text-muted);margin-top:4px;">
                        <span id="editCharCount">${currentValue.length}</span>/${cfg.maxLength || 500}
                    </div>
                </div>`;
            } else {
                bodyEl.innerHTML = `<div class="edit-field-group">
                    <label>${escHtml(cfg.label)}</label>
                    <input id="editFieldInput" type="text" maxlength="${cfg.maxLength || 200}" value="${escHtml(currentValue)}" placeholder="请输入${cfg.label}">
                </div>`;
            }

            overlay.style.display = 'flex';

            // 聚焦
            const inputEl = document.getElementById('editFieldInput');
            setTimeout(() => inputEl.focus(), 50);

            // 字符计数
            if (cfg.type === 'textarea') {
                inputEl.addEventListener('input', () => {
                    const counter = document.getElementById('editCharCount');
                    if (counter) counter.textContent = inputEl.value.length;
                });
            }

            // 清除旧事件
            const newConfirm = confirmBtn.cloneNode(true);
            confirmBtn.parentNode.replaceChild(newConfirm, confirmBtn);
            const newCancel = cancelBtn.cloneNode(true);
            cancelBtn.parentNode.replaceChild(newCancel, cancelBtn);
            const newClose = closeBtn.cloneNode(true);
            closeBtn.parentNode.replaceChild(newClose, closeBtn);

            const dismiss = () => { overlay.style.display = 'none'; };

            newConfirm.addEventListener('click', () => {
                const newValue = inputEl.value;
                applyFieldEdit(fieldKey, cfg, el, newValue);
                dismiss();
            });
            newCancel.addEventListener('click', dismiss);
            newClose.addEventListener('click', dismiss);
            overlay.addEventListener('click', (e) => { if (e.target === overlay) dismiss(); }, { once: true });
        }

        /** 获取字段当前值 */
        function getFieldCurrentValue(fieldKey) {
            const product = window.currentProduct;
            if (!product) return '';

            switch (fieldKey) {
                case 'name':          return product.name || '';
                case 'description':   return product.description || '';
                case 'category':      return product.category || '';
                case 'author':        return product.author || '';
                case 'version':       return product.version || '';
                case 'fullDesc':      return product.description || '';
                case 'license':       return product.license || '';
                case 'compatibility': return product.compatibility || '';
                default:              return '';
            }
        }

        /** 应用字段编辑到 DOM 和 _editedFields */
        function applyFieldEdit(fieldKey, cfg, el, newValue) {
            el.textContent = newValue || '--';

            // 跟踪已编辑字段
            window._editedFields[fieldKey] = newValue;

            // 同步 currentProduct
            const product = window.currentProduct;
            if (product) {
                switch (fieldKey) {
                    case 'name':          product.name = newValue; break;
                    case 'description':   product.description = newValue; break;
                    case 'category':      product.category = newValue; break;
                    case 'author':        product.author = newValue; break;
                    case 'version':       product.version = newValue; break;
                    case 'fullDesc':      product.description = newValue; break;
                    case 'license':       product.license = newValue; break;
                    case 'compatibility': product.compatibility = newValue; break;
                }
            }

            // description 和 fullDesc 共享同一数据，同步两个 DOM 元素
            if (fieldKey === 'description') {
                const fullDescEl = document.getElementById('productDescription');
                if (fullDescEl) fullDescEl.textContent = newValue || '--';
            } else if (fieldKey === 'fullDesc') {
                const heroDescEl = document.getElementById('heroDesc');
                if (heroDescEl) heroDescEl.textContent = newValue || '--';
                window._editedFields['description'] = newValue;
            }

            // 更新保存按钮状态
            updateSaveBtnState();
        }

        /** 更新保存按钮禁用状态 */
        function updateSaveBtnState() {
            const saveBtn = document.getElementById('editSaveBtn');
            if (saveBtn) {
                saveBtn.disabled = Object.keys(window._editedFields).length === 0;
            }
        }

        // ================================================================
        // 徽章编辑器
        // ================================================================

        function openBadgesEditor() {
            const overlay = document.getElementById('listEditOverlay');
            const titleEl = document.getElementById('listEditTitle');
            const bodyEl = document.getElementById('listEditBody');
            const addBtn = document.getElementById('listEditAdd');
            const confirmBtn = document.getElementById('listEditConfirm');
            const cancelBtn = document.getElementById('listEditCancel');
            const closeBtn = document.getElementById('listEditClose');

            titleEl.textContent = '编辑徽章列表';

            const product = window.currentProduct;
            let badges = (product && product._parsedBadges) ? JSON.parse(JSON.stringify(product._parsedBadges)) : JSON.parse(JSON.stringify(DEFAULT_BADGES));

            function renderItems() {
                bodyEl.innerHTML = badges.map((b, i) =>
                    `<div class="list-edit-item" data-index="${i}">
                        <div class="list-edit-fields">
                            <input type="text" value="${escHtml(b.icon || '')}" placeholder="Material Icon 名称（如 verified_user）" data-field="icon" data-index="${i}">
                            <input type="text" value="${escHtml(b.text || '')}" placeholder="徽章文字（如 安全认证资源）" data-field="text" data-index="${i}">
                        </div>
                        <button class="list-edit-remove" data-index="${i}" title="删除此条"><span class="material-icons">delete</span></button>
                    </div>`
                ).join('');

                // 绑定输入事件
                bodyEl.querySelectorAll('input').forEach(input => {
                    input.addEventListener('input', () => {
                        const idx = Number(input.dataset.index);
                        const field = input.dataset.field;
                        if (badges[idx]) badges[idx][field] = input.value;
                    });
                });

                // 绑定删除事件
                bodyEl.querySelectorAll('.list-edit-remove').forEach(btn => {
                    btn.addEventListener('click', () => {
                        const idx = Number(btn.dataset.index);
                        badges.splice(idx, 1);
                        renderItems();
                    });
                });
            }

            renderItems();
            overlay.style.display = 'flex';

            // 清除旧事件（用 clone 方式）
            const newAdd = addBtn.cloneNode(true);
            addBtn.parentNode.replaceChild(newAdd, addBtn);
            const newConfirm = confirmBtn.cloneNode(true);
            confirmBtn.parentNode.replaceChild(newConfirm, confirmBtn);
            const newCancel = cancelBtn.cloneNode(true);
            cancelBtn.parentNode.replaceChild(newCancel, cancelBtn);
            const newClose = closeBtn.cloneNode(true);
            closeBtn.parentNode.replaceChild(newClose, closeBtn);

            const dismiss = () => { overlay.style.display = 'none'; };

            newAdd.addEventListener('click', () => {
                badges.push({ icon: 'info', text: '新徽章' });
                renderItems();
            });

            newConfirm.addEventListener('click', () => {
                // 过滤空条目
                const cleaned = badges.filter(b => b.text && b.text.trim());
                if (product) product._parsedBadges = cleaned;

                // 保存为 JSON 字符串
                window._editedFields['badges'] = JSON.stringify(cleaned);

                // 重新渲染 DOM
                renderBadges(Object.assign({}, product, { badges: JSON.stringify(cleaned) }));

                updateSaveBtnState();
                dismiss();
            });

            newCancel.addEventListener('click', dismiss);
            newClose.addEventListener('click', dismiss);
            overlay.addEventListener('click', (e) => { if (e.target === overlay) dismiss(); }, { once: true });
        }

        // ================================================================
        // 特性编辑器
        // ================================================================

        function openFeaturesEditor() {
            const overlay = document.getElementById('listEditOverlay');
            const titleEl = document.getElementById('listEditTitle');
            const bodyEl = document.getElementById('listEditBody');
            const addBtn = document.getElementById('listEditAdd');
            const confirmBtn = document.getElementById('listEditConfirm');
            const cancelBtn = document.getElementById('listEditCancel');
            const closeBtn = document.getElementById('listEditClose');

            titleEl.textContent = '编辑亮点特性';

            const product = window.currentProduct;
            let features = (product && product._parsedFeatures) ? JSON.parse(JSON.stringify(product._parsedFeatures)) : JSON.parse(JSON.stringify(DEFAULT_FEATURES));

            function renderItems() {
                bodyEl.innerHTML = features.map((f, i) =>
                    `<div class="list-edit-item" data-index="${i}">
                        <div class="list-edit-fields">
                            <input type="text" value="${escHtml(f.icon || '')}" placeholder="Material Icon 名称（如 bolt）" data-field="icon" data-index="${i}">
                            <input type="text" value="${escHtml(f.title || '')}" placeholder="特性标题（如 高性能）" data-field="title" data-index="${i}">
                            <input type="text" value="${escHtml(f.desc || '')}" placeholder="特性描述（如 优化的运行效率...）" data-field="desc" data-index="${i}">
                        </div>
                        <button class="list-edit-remove" data-index="${i}" title="删除此条"><span class="material-icons">delete</span></button>
                    </div>`
                ).join('');

                bodyEl.querySelectorAll('input').forEach(input => {
                    input.addEventListener('input', () => {
                        const idx = Number(input.dataset.index);
                        const field = input.dataset.field;
                        if (features[idx]) features[idx][field] = input.value;
                    });
                });

                bodyEl.querySelectorAll('.list-edit-remove').forEach(btn => {
                    btn.addEventListener('click', () => {
                        const idx = Number(btn.dataset.index);
                        features.splice(idx, 1);
                        renderItems();
                    });
                });
            }

            renderItems();
            overlay.style.display = 'flex';

            // 清除旧事件
            const newAdd = addBtn.cloneNode(true);
            addBtn.parentNode.replaceChild(newAdd, addBtn);
            const newConfirm = confirmBtn.cloneNode(true);
            confirmBtn.parentNode.replaceChild(newConfirm, confirmBtn);
            const newCancel = cancelBtn.cloneNode(true);
            cancelBtn.parentNode.replaceChild(newCancel, cancelBtn);
            const newClose = closeBtn.cloneNode(true);
            closeBtn.parentNode.replaceChild(newClose, closeBtn);

            const dismiss = () => { overlay.style.display = 'none'; };

            newAdd.addEventListener('click', () => {
                features.push({ icon: 'star', title: '新特性', desc: '特性描述' });
                renderItems();
            });

            newConfirm.addEventListener('click', () => {
                const cleaned = features.filter(f => f.title && f.title.trim());
                if (product) product._parsedFeatures = cleaned;

                window._editedFields['features'] = JSON.stringify(cleaned);

                renderFeatures(Object.assign({}, product, { features: JSON.stringify(cleaned) }));

                updateSaveBtnState();
                dismiss();
            });

            newCancel.addEventListener('click', dismiss);
            newClose.addEventListener('click', dismiss);
            overlay.addEventListener('click', (e) => { if (e.target === overlay) dismiss(); }, { once: true });
        }

        // ================================================================
        // 保存更改到后端
        // ================================================================

        async function saveProductChanges() {
            const edited = window._editedFields;
            if (Object.keys(edited).length === 0) {
                showEditToast('warn', '没有需要保存的更改');
                return;
            }

            const token = localStorage.getItem('kax_login_token');
            if (!token) {
                showEditToast('error', '登录已过期，请重新登录');
                return;
            }

            const product = window.currentProduct;
            const saveBtn = document.getElementById('editSaveBtn');
            saveBtn.disabled = true;
            saveBtn.innerHTML = '<span class="material-icons">hourglass_top</span> 保存中…';

            try {
                // 构建请求体：只发送修改过的字段
                const body = { id: product.id };

                // 简单文本字段映射到 API 字段名
                const fieldApiMap = {
                    name:          'name',
                    description:   'description',
                    fullDesc:      'description',
                    category:      'category',
                    author:        'author',
                    version:       'version',
                    license:       'license',
                    compatibility: 'compatibility',
                    badges:        'badges',
                    features:      'features',
                };

                for (const [key, val] of Object.entries(edited)) {
                    const apiKey = fieldApiMap[key];
                    if (apiKey) body[apiKey] = val;
                }

                const resp = await fetch('/api/asset/admin/update', {
                    method: 'POST',
                    headers: {
                        'Authorization': 'Bearer ' + token,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(body)
                });

                const data = await resp.json();

                if (resp.ok) {
                    showEditToast('success', '保存成功！');
                    window._editedFields = {};
                    updateSaveBtnState();
                } else {
                    showEditToast('error', '保存失败: ' + (data.message || '未知错误'));
                }
            } catch (e) {
                console.error('保存失败', e);
                showEditToast('error', '网络错误，请稍后重试');
            } finally {
                saveBtn.disabled = false;
                saveBtn.innerHTML = '<span class="material-icons">save</span> 保存更改';
                updateSaveBtnState();
            }
        }

        // ================================================================
        // Toast 通知
        // ================================================================

        function showEditToast(type, text) {
            const toast = document.getElementById('editToast');
            const icon = document.getElementById('editToastIcon');
            const textEl = document.getElementById('editToastText');
            if (!toast) return;

            toast.className = 'edit-toast ' + (type || 'success');
            icon.textContent = type === 'success' ? 'check_circle' : (type === 'error' ? 'error' : 'warning');
            textEl.textContent = text;
            toast.style.display = 'flex';

            clearTimeout(toast._timer);
            toast._timer = setTimeout(() => { toast.style.display = 'none'; }, 3000);
        }

        // ================================================================
        // 通用消息弹窗 (MessageBox) — 重构版，完全动态创建 DOM
        // ================================================================

        /**
         * 显示自定义消息弹窗（动态创建 DOM，关闭时自动移除）
         * @param {object}   options
         * @param {string}   [options.title='提示']    弹窗标题
         * @param {string}   [options.message='']      消息正文
         * @param {string}   [options.type='info']     类型: success / error / warn / info
         * @param {Function} [options.onConfirm]       点击确定后的回调
         */
        function showMsgBox({ title = '提示', message = '', type = 'info', onConfirm = null } = {}) {
            const TYPE_MAP = {
                success: { icon: 'check_circle', color: '#22c55e', colorBg: 'rgba(34,197,94,0.12)'  },
                error:   { icon: 'error',         color: '#ef4444', colorBg: 'rgba(239,68,68,0.12)'  },
                warn:    { icon: 'warning',        color: '#f59e0b', colorBg: 'rgba(245,158,11,0.12)' },
                info:    { icon: 'info',           color: '#638cff', colorBg: 'rgba(99,140,255,0.12)' },
            };
            const cfg = TYPE_MAP[type] || TYPE_MAP.info;

            // ── 创建遮罩层 ──
            const overlay = document.createElement('div');
            overlay.className = 'kax-msgbox-overlay';

            // ── 创建卡片 ──
            const card = document.createElement('div');
            card.className = 'kax-msgbox-card';
            card.style.setProperty('--kax-msg-color',    cfg.color);
            card.style.setProperty('--kax-msg-color-bg', cfg.colorBg);

            // ── 关闭按钮 ──
            const closeBtn = document.createElement('button');
            closeBtn.className = 'kax-msgbox-close-btn';
            closeBtn.innerHTML = '<span class="material-icons">close</span>';

            // ── 内容区 ──
            const body = document.createElement('div');
            body.className = 'kax-msgbox-body';

            const iconWrap = document.createElement('div');
            iconWrap.className = 'kax-msgbox-icon-wrap';
            iconWrap.innerHTML = `<span class="material-icons">${cfg.icon}</span>`;

            const titleEl = document.createElement('p');
            titleEl.className = 'kax-msgbox-title';
            titleEl.textContent = title;

            const contentEl = document.createElement('p');
            contentEl.className = 'kax-msgbox-content';
            contentEl.textContent = message;

            body.appendChild(iconWrap);
            body.appendChild(titleEl);
            if (message) body.appendChild(contentEl);

            // ── 底部按钮 ──
            const footer = document.createElement('div');
            footer.className = 'kax-msgbox-footer';

            const confirmBtn = document.createElement('button');
            confirmBtn.className = 'kax-msgbox-confirm-btn';
            confirmBtn.textContent = '确定';
            footer.appendChild(confirmBtn);

            // ── 组装 ──
            card.appendChild(closeBtn);
            card.appendChild(body);
            card.appendChild(footer);
            overlay.appendChild(card);
            document.body.appendChild(overlay);

            // ── 关闭逻辑（带退出动画） ──
            const dismiss = (triggerConfirm = false) => {
                overlay.classList.add('closing');
                card.classList.add('closing');
                const duration = 180;
                setTimeout(() => {
                    if (overlay.parentNode) overlay.parentNode.removeChild(overlay);
                    if (triggerConfirm && typeof onConfirm === 'function') onConfirm();
                }, duration);
            };

            confirmBtn.addEventListener('click', () => dismiss(true));
            closeBtn.addEventListener('click',   () => dismiss(false));
            overlay.addEventListener('click', (e) => { if (e.target === overlay) dismiss(false); });
        }

