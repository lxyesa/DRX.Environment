/** 时间戳或日期字符串 → zh-CN 日期显示 */
        function formatDate(ts) {
            if (!ts || ts === '--') return '--';
            const ms = ts > 9999999999 ? ts : ts * 1000;
            const date = new Date(ms);
            if (isNaN(date.getTime())) return '--';
            return date.toLocaleDateString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit' });
        }

        // ================================================================
        // 统一错误码映射与提示语义（FR-8, FR-10, NFR-3, NFR-4）
        // 与 shop.js 保持一致的错误处理体系
        // ================================================================

        /**
         * 统一错误码映射表：将后端 HTTP 状态码/业务 code 映射为用户友好提示
         * 商店域关键接口错误响应统一出口
         */
        const ERROR_CODE_MAP = {
            // HTTP 状态码
            0:   { title: '成功', message: '操作成功', type: 'success' },
            400: { title: '参数错误', message: '请求参数有误，请检查后重试。', type: 'error' },
            401: { title: '未登录', message: '请先登录以继续操作。', type: 'warn', action: 'login' },
            403: { title: '无权限', message: '您没有执行此操作的权限。', type: 'error' },
            404: { title: '未找到', message: '请求的资源不存在或已被删除。', type: 'error' },
            409: { title: '操作冲突', message: '资源已存在或状态冲突，请稍后重试。', type: 'warn' },
            500: { title: '服务异常', message: '服务器出现问题，请稍后重试。', type: 'error' },
            502: { title: '服务异常', message: '服务暂时不可用，请稍后重试。', type: 'error' },
            503: { title: '服务繁忙', message: '系统繁忙，请稍后重试。', type: 'error' },
            // 业务错误码（1000+）
            1001: { title: '余额不足', message: '您的账户余额不足，请先充值。', type: 'warn' },
            1002: { title: '已购买', message: '您已拥有该资产，无需重复购买。', type: 'info' },
            1003: { title: '库存不足', message: '该商品已售罄，请选择其他方案。', type: 'warn' },
            1004: { title: '已下架', message: '该商品已下架，暂时无法购买。', type: 'warn' },
            // 网络相关
            'NETWORK_ERROR': { title: '网络错误', message: '网络连接异常，请检查网络后重试。', type: 'error' },
            'TIMEOUT':       { title: '请求超时', message: '服务响应超时，请稍后重试。', type: 'error' },
            'PARSE_ERROR':   { title: '数据异常', message: '服务返回数据格式错误，请稍后重试。', type: 'error' },
            'UNKNOWN':       { title: '未知错误', message: '发生未知错误，请稍后重试。', type: 'error' }
        };

        /**
         * 根据错误码/HTTP状态获取统一错误提示
         * @param {number|string} code - HTTP 状态码或业务错误码
         * @param {string} [fallbackMsg] - 后端返回的原始消息（兜底文案）
         * @returns {{ title: string, message: string, type: string, action?: string }}
         */
        function getErrorInfo(code, fallbackMsg) {
            const mapped = ERROR_CODE_MAP[code] || ERROR_CODE_MAP['UNKNOWN'];
            return {
                title: mapped.title,
                message: fallbackMsg || mapped.message,
                type: mapped.type,
                action: mapped.action
            };
        }

        /**
         * 显示统一错误消息弹窗（使用 showMsgBox）
         * @param {number|string} code - 错误码
         * @param {string} [serverMessage] - 服务端返回的错误消息
         */
        function showUnifiedError(code, serverMessage) {
            const info = getErrorInfo(code, serverMessage);
            showMsgBox({
                title: info.title,
                message: info.message,
                type: info.type,
                onConfirm: info.action === 'login' ? () => { location.href = '/login'; } : null
            });
        }

        // ================================================================
        // 映射层：AssetDetail DTO → ShopDetailVM（单一契约，见 design.md 附录 B）
        // ================================================================

        /**
         * 将后端 /api/asset/detail/{id} 的 data 对象映射为页面稳定 ViewModel。
         * 价格字段已统一为元（decimal），前端不再做分↔元猜测换算。
         *
         * @param {object} dto  - 后端返回的 data 对象
         * @returns {object}    - ShopDetailVM
         */
        function mapDetailToVM(dto) {
            // 截图：数组或分号/逗号分隔字符串均可
            const parseMediaArray = (raw) => {
                if (Array.isArray(raw)) return raw.map(v => String(v || '').trim()).filter(Boolean);
                if (typeof raw === 'string') {
                    return raw.split(/[;,，\n\r]+/).map(v => v.trim()).filter(Boolean);
                }
                return [];
            };

            // 价格方案：PriceOption[]，直接映射（priceYuan 已是元）
            const priceOptions = Array.isArray(dto.prices) ? dto.prices.map(p => ({
                id: String(p.id ?? ''),
                name: String(p.name ?? ''),
                priceYuan: Number(p.priceYuan ?? 0),
                originalPriceYuan: Number(p.originalPriceYuan ?? p.priceYuan ?? 0),
                duration: p.duration ?? null,
                unit: p.unit ?? null,
                stock: p.stock ?? -1
            })) : [];

            // 作者显示名：兼容多版本字段命名（authorName / authorDisplayName / author / developerDisplayName）
            const resolvedAuthorName = String(
                dto.authorName
                ?? dto.authorDisplayName
                ?? dto.displayAuthorName
                ?? dto.developerDisplayName
                ?? dto.developerName
                ?? dto.author
                ?? dto.authorInfo?.displayName
                ?? '--'
            );

            return {
                // 主键（页面内统一使用 assetId）
                assetId: Number(dto.id ?? 0),
                displayName: String(dto.name ?? '--'),
                description: String(dto.description ?? '--'),
                category: String(dto.category ?? '--'),
                coverImage: String(dto.coverImage ?? ''),
                iconImage: String(dto.iconImage ?? ''),
                screenshots: parseMediaArray(dto.screenshots),
                badges: dto.badges ?? '',
                features: dto.features ?? '',
                // 价格方案
                priceOptions,
                // 规格信息（服务端嵌套于 dto.specs）
                version: String(dto.version ?? '--'),
                compatibility: String(dto.specs?.compatibility ?? '--'),
                fileSize: String(dto.specs?.fileSize ?? '--'),
                uploadDate: dto.specs?.uploadDate ?? '--',
                license: String(dto.specs?.license ?? '--'),
                lastUpdatedAt: Number(dto.specs?.lastUpdatedAt ?? 0),
                // 统计
                authorName: resolvedAuthorName,
                downloadCount: Number(dto.specs?.downloads ?? 0),
                purchaseCount: Number(dto.specs?.purchaseCount ?? 0),
                favoriteCount: Number(dto.specs?.favoriteCount ?? 0),
                viewCount: Number(dto.specs?.viewCount ?? 0),
                rating: Number(dto.specs?.rating ?? 0),
                reviewCount: Number(dto.specs?.reviewCount ?? 0),
                isDeleted: Boolean(dto.isDeleted),
                downloadUrl: String(dto.specs?.downloadUrl ?? '')
            };
        }

        /**
         * 将相关推荐 DTO → RelatedCardVM（design.md 附录 C）
         */
        function mapRelatedToVM(dto) {
            return {
                assetId: Number(dto.id ?? 0),
                displayName: String(dto.name ?? '--'),
                category: String(dto.category ?? '--'),
                coverImage: String(dto.coverImage ?? ''),
                priceYuan: Number(dto.priceYuan ?? 0)
            };
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

        // 模拟产品数据已移除 — 详情页现在仅使用后端 API 契约数据（见 design.md 附录 B）

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

        // 初始化页面
        async function initPage() {
            renderDetailSkeleton();

            // ── 请求层：从后端获取详情 ──────────────────────────────────────
            let vm = null;
            try {
                const resp = await fetch(`/api/asset/detail/${productId}`, {
                    headers: { 'Authorization': 'Bearer ' + (localStorage.getItem('kax_login_token') || '') }
                });
                
                // 根据 HTTP 状态码提供精确错误提示
                if (!resp.ok) {
                    const errorInfo = getErrorInfo(resp.status);
                    console.error('[shop_detail] 获取详情失败, HTTP', resp.status);
                    removeSkeleton();
                    showDetailError(errorInfo.title, errorInfo.message);
                    return;
                }
                
                let json;
                try {
                    json = await resp.json();
                } catch (parseErr) {
                    const errorInfo = getErrorInfo('PARSE_ERROR');
                    console.error('[shop_detail] JSON 解析失败', parseErr);
                    removeSkeleton();
                    showDetailError(errorInfo.title, errorInfo.message);
                    return;
                }
                
                // 处理业务错误码
                if (!json || (json.code !== 0 && json.code !== undefined)) {
                    const errorInfo = getErrorInfo(json?.code || 'UNKNOWN', json?.message);
                    console.error('[shop_detail] 业务错误, code:', json?.code);
                    removeSkeleton();
                    showDetailError(errorInfo.title, errorInfo.message);
                    return;
                }
                
                if (!json.data) {
                    const errorInfo = getErrorInfo(404);
                    removeSkeleton();
                    showDetailError(errorInfo.title, '商品数据为空，可能已被删除。');
                    return;
                }
                
                // ── 映射层：DTO → ShopDetailVM ──────────────────────────────
                vm = mapDetailToVM(json.data);
            } catch (e) {
                console.error('[shop_detail] 获取详情失败', e);
                removeSkeleton();
                // 网络异常
                const errorInfo = e.name === 'TypeError' && e.message.includes('fetch')
                    ? getErrorInfo('NETWORK_ERROR')
                    : getErrorInfo('UNKNOWN');
                showDetailError(errorInfo.title, errorInfo.message);
                return;
            }

            // 挂载到 window 供事件监听器使用（保持向后兼容命名）
            window.currentProduct = vm;

            removeSkeleton();

            // ── 视图层：渲染 ─────────────────────────────────────────────────
            try {
                loadProductData(vm);
                renderBadges(vm);
                renderFeatures(vm);
                renderGallery(vm);
                setupEventListeners();
                initHeroScrollMorph();
            } catch (renderErr) {
                console.error('[shop_detail] 页面渲染出错，部分功能可能不可用', renderErr);
            }
            try { loadRelatedProducts(productId); } catch (e) { console.warn('[shop_detail] 加载相关推荐出错', e); }
            try { initGlobalTopbar && initGlobalTopbar(); } catch (e) {}
            try { initGlobalFooter && initGlobalFooter(); } catch (e) {}
            try { initButtonEffects && initButtonEffects(); } catch (e) {}

            // System 权限检测 → 初始化编辑模式
            await checkSystemPermission();
        }

        /**
         * 详情页加载失败时展示错误态（替代旧的静态模拟数据兜底）
         * 提供统一样式的错误展示与重试入口
         */
        function showDetailError(title, message) {
            const mainCol = document.querySelector('.detail-main-col');
            if (mainCol) {
                mainCol.innerHTML = `
                    <div style="display:flex;flex-direction:column;align-items:center;justify-content:center;min-height:260px;gap:16px;color:var(--text-muted,#888);">
                        <span class="material-icons" style="font-size:48px;opacity:0.4;">error_outline</span>
                        <div style="font-size:18px;font-weight:600;">${escHtml(title)}</div>
                        <div style="font-size:14px;">${escHtml(message)}</div>
                        <div style="display:flex;gap:12px;margin-top:8px;">
                            <button onclick="location.reload()" style="padding:8px 20px;border-radius:8px;background:var(--accent,#638cff);color:#fff;border:none;cursor:pointer;font-size:14px;">
                                <span class="material-icons" style="font-size:16px;vertical-align:middle;margin-right:4px;">refresh</span>重试
                            </button>
                            <button onclick="history.back()" style="padding:8px 20px;border-radius:8px;background:rgba(255,255,255,0.1);color:var(--text-muted,#888);border:1px solid rgba(255,255,255,0.1);cursor:pointer;font-size:14px;">返回</button>
                        </div>
                    </div>`;
            }
        }

        /** 将 ShopDetailVM 渲染到页面所有元素（仅消费 ViewModel 字段，不再兼容旧字段名） */
        function loadProductData(vm) {
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
            setText('breadcrumbCategory', vm.category);
            const pageTitleEl = document.getElementById('pageTitle');
            if (pageTitleEl) pageTitleEl.textContent = (vm.displayName || '商品详情') + ' - KaxHub';

            // ── 英雄区图标（显示 iconImage）──
            const heroIconEl = document.getElementById('heroIcon');
            const heroRowEl = document.querySelector('.hero-product-row');
            const heroPanelAmbientCopyEl = document.getElementById('heroPanelAmbientCopy');
            const heroVisualImage = vm.iconImage || vm.coverImage || '';
            if (heroIconEl && heroVisualImage) {
                heroIconEl.innerHTML = `<img class="hero-icon-media" src="${heroVisualImage}" alt="${vm.displayName || ''}">`;
                if (heroPanelAmbientCopyEl) {
                    heroPanelAmbientCopyEl.src = heroVisualImage;
                }
                if (heroRowEl) {
                    heroRowEl.classList.add('has-ambient-media');
                }
            } else {
                if (heroPanelAmbientCopyEl) {
                    heroPanelAmbientCopyEl.removeAttribute('src');
                }
                if (heroRowEl) {
                    heroRowEl.classList.remove('has-ambient-media');
                }
            }

            // ── 英雄区背景（显示 coverImage 封面）──
            const heroBgEl = document.getElementById('productHeroBg');
            if (heroBgEl && vm.coverImage) {
                heroBgEl.style.backgroundImage = `url(${vm.coverImage})`;
                heroBgEl.style.backgroundSize = 'cover';
                heroBgEl.style.backgroundRepeat = 'no-repeat';
                heroBgEl.style.backgroundPosition = 'center';
            }

            // ── 英雄区 ──
            setText('productName',    vm.displayName);
            setText('heroDesc',       vm.description);
            setText('heroCategory',   vm.category);
            setText('heroAuthor',     vm.authorName);
            setText('heroDownloads',  showDownloads(vm.downloadCount));
            setText('heroVersion',    vm.version);

            // 评分
            const ratingNum = (vm.rating != null && !isNaN(Number(vm.rating))) ? Number(vm.rating) : null;
            const starsStr  = ratingNum != null
                ? '★'.repeat(Math.round(ratingNum)) + '☆'.repeat(5 - Math.round(ratingNum))
                : '☆☆☆☆☆';
            const ratingStr = ratingNum != null ? ratingNum.toFixed(1) : '--';
            const reviewsStr = vm.reviewCount != null ? String(vm.reviewCount) : '--';

            setText('heroStars',       starsStr);
            setText('heroRatingVal',   ratingStr);
            setText('heroRatingCount', '(' + reviewsStr + ')');
            setText('reviewScoreBig',  ratingStr);
            setText('reviewStarsBig',  starsStr);
            setText('reviewsTotal',    reviewsStr + ' 条评价');

            // ── 价格计算（使用 priceOptions[0] 作为默认展示价） ──
            let displayOriginal = null;
            let displayCurrent  = null;
            if (vm.priceOptions && vm.priceOptions.length > 0) {
                const p0 = vm.priceOptions[0];
                displayOriginal = p0.originalPriceYuan;
                displayCurrent  = p0.priceYuan;
            }

            // 当前价
            const isFree = !vm.priceOptions || vm.priceOptions.length === 0;
            const priceCurrentEl = document.getElementById('priceCurrent');
            if (priceCurrentEl) priceCurrentEl.textContent = isFree ? '免费' : (displayCurrent != null ? showCurrency(displayCurrent) : '--');

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

            // 折扣徽标
            const priceBadgeEl = document.getElementById('priceBadge');
            if (priceBadgeEl) {
                let discountPercent = null;
                if (!isFree && displayOriginal != null && displayCurrent != null && Number(displayOriginal) > 0) {
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

            // ── 库存状态（根据 priceOptions 汇总，无方案时隐藏） ──
            const stockInfoEl = document.getElementById('stockInfo');
            if (stockInfoEl) {
                if (isFree) {
                    stockInfoEl.setAttribute('hidden', '');
                } else {
                stockInfoEl.removeAttribute('hidden');
                let stockText = '库存：--';
                let isLow = false;
                const opts = vm.priceOptions || [];
                if (opts.length > 0) {
                    const hasUnlimited = opts.some(p => (p.stock ?? -1) < 0);
                    if (hasUnlimited) {
                        stockText = '库存：充足';
                    } else {
                        const totalStock = opts.reduce((sum, p) => sum + Math.max(0, p.stock ?? 0), 0);
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
                if (stockTextEl) stockTextEl.textContent = stockText;
                else stockInfoEl.textContent = stockText;
                stockInfoEl.classList.toggle('low', isLow);
                } // end else (not free)
            }

            // ── 规格 Tab ──
            setText('specSize',             vm.fileSize);
            setText('productVersion',       vm.version);
            setText('specDate',             showDate(vm.uploadDate));
            setText('specLastUpdated',      showDate(vm.lastUpdatedAt));
            setText('specAuthor',           vm.authorName);
            setText('productCompatibility', vm.compatibility);
            setText('specLicense',          vm.license);
            setText('productDownloads',     showDownloads(vm.downloadCount));
            const purchasesEl = document.getElementById('productPurchases');
            if (purchasesEl) purchasesEl.textContent = showDownloads(vm.purchaseCount);

            // ── 概述 Tab 描述 ──
            setText('productDescription', vm.description);

            // 挂载给事件监听器使用
            window.currentProduct = vm;

            // ── 下载按钮 ──
            const _dlBtn = document.getElementById('downloadBtn');
            if (_dlBtn) {
                _dlBtn.removeAttribute('hidden');
                if (vm.downloadUrl) {
                    _dlBtn.href = vm.downloadUrl;
                    _dlBtn.target = '_blank';
                    _dlBtn.rel = 'noopener noreferrer';
                    _dlBtn.removeAttribute('aria-disabled');
                    _dlBtn.classList.remove('is-disabled');
                } else {
                    _dlBtn.removeAttribute('href');
                    _dlBtn.setAttribute('aria-disabled', 'true');
                    _dlBtn.classList.add('is-disabled');
                }
            }
        }

        /** 根据选中的价格方案对象（PriceOptionVM）更新顶部价格显示 */
        function updatePriceDisplay(priceOpt) {
            const showCurrency = (v) => (v === null || v === undefined) ? '--' : ('💰' + Number(v).toFixed(2));
            let displayOriginal = null;
            let displayCurrent  = null;

            if (priceOpt) {
                displayOriginal = priceOpt.originalPriceYuan ?? priceOpt.priceYuan ?? null;
                displayCurrent  = priceOpt.priceYuan ?? null;
            }

            // 当前价
            const priceCurrentEl = document.getElementById('priceCurrent');
            if (priceCurrentEl) priceCurrentEl.textContent = displayCurrent != null ? showCurrency(displayCurrent) : '--';

            // 原价
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

            // 折扣徽标
            const priceBadgeEl = document.getElementById('priceBadge');
            if (priceBadgeEl) {
                let discountPercent = null;
                if (displayOriginal != null && displayCurrent != null && Number(displayOriginal) > 0) {
                    const d = Math.round(((Number(displayOriginal) - Number(displayCurrent)) / Number(displayOriginal)) * 100);
                    if (d > 0) discountPercent = d;
                }

                if (discountPercent != null && discountPercent > 0) {
                    priceBadgeEl.textContent = discountPercent + '% OFF';
                    priceBadgeEl.classList.remove('no-discount');
                    priceBadgeEl.removeAttribute('hidden');
                } else {
                    priceBadgeEl.textContent = '';
                    priceBadgeEl.classList.remove('no-discount');
                    priceBadgeEl.setAttribute('hidden', '');
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
            const downloadBtn = document.getElementById('downloadBtn');
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

            /** 动态生成和显示价格套餐（消费 ViewModel.priceOptions） */
            function loadPricePlans() {
                const vm = window.currentProduct;
                const opts = (vm && Array.isArray(vm.priceOptions) && vm.priceOptions.length > 0) ? vm.priceOptions : null;

                const pricePlansCard = document.getElementById('pricePlansCard');
                if (!opts || opts.length === 0) {
                    if (pricePlansCard) pricePlansCard.setAttribute('hidden', '');
                    setPurchaseBtnEnabled(false);
                    if (purchaseBtn) purchaseBtn.setAttribute('hidden', '');
                    return;
                }
                if (pricePlansCard) pricePlansCard.removeAttribute('hidden');
                if (purchaseBtn) purchaseBtn.removeAttribute('hidden');

                plansGrid.innerHTML = '';
                opts.forEach((opt, index) => {
                    let durationText = '';
                    switch ((opt.unit ?? '').toLowerCase()) {
                        case 'year':  durationText = `${opt.duration}年`; break;
                        case 'month': durationText = `${opt.duration}个月`; break;
                        case 'day':   durationText = `${opt.duration}天`; break;
                        case 'hour':  durationText = `${opt.duration}小时`; break;
                        default:      durationText = opt.name || '一次性';
                    }

                    const priceYuan = opt.priceYuan;
                    const origYuan  = opt.originalPriceYuan;
                    const hasDiscount = origYuan != null && Number(origYuan) > Number(priceYuan);

                    const planStock = opt.stock ?? -1;
                    const stockHtml = planStock < 0 ? '' : (planStock > 0
                        ? `<span style="color: var(--text-muted); font-size: 12px;">库存: ${planStock}</span>`
                        : `<span style="color: var(--danger, #e74c3c); font-size: 12px;">已售罄</span>`);
                    const isOutOfStock = planStock === 0;

                    const planItem = document.createElement('button');
                    planItem.className = 'plan-item' + (index === 0 && !isOutOfStock ? ' selected' : '');
                    planItem.type = 'button';
                    if (isOutOfStock) planItem.disabled = true;
                    planItem.innerHTML = `
                        <div class="plan-name">${durationText}</div>
                        <div class="plan-details">
                            ${hasDiscount ? `<span style="text-decoration: line-through; color: var(--text-muted); margin-right: 4px;">💰${Number(origYuan).toFixed(2)}</span>` : ''}
                            <span style="color: var(--accent); font-weight: 600;">💰${Number(priceYuan).toFixed(2)}</span>
                        </div>
                        ${stockHtml ? `<div class="plan-stock">${stockHtml}</div>` : ''}
                    `;

                    planItem.addEventListener('click', () => {
                        if (isOutOfStock) return;
                        document.querySelectorAll('.plan-item').forEach(item => item.classList.remove('selected'));
                        planItem.classList.add('selected');
                        selectedPriceId = opt.id;
                        setPurchaseBtnEnabled(true);
                        updatePriceDisplay(opt);
                    });

                    plansGrid.appendChild(planItem);

                    if (index === 0 && !isOutOfStock) {
                        selectedPriceId = opt.id;
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

                const vm = window.currentProduct;
                const token = localStorage.getItem('kax_login_token');

                if (!token) {
                    showUnifiedError(401);
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
                        // 请求体使用契约字段 assetId（design.md 12.2 节）
                        body: JSON.stringify({
                            assetId: vm.assetId,
                            priceId: selectedPriceId
                        })
                    });

                    let data;
                    try {
                        data = await resp.json();
                    } catch (parseErr) {
                        showUnifiedError('PARSE_ERROR');
                        purchaseBtn.innerHTML = '<span class="material-icons">shopping_bag</span> 立即购买';
                        purchaseBtn.disabled = false;
                        return;
                    }

                    if (resp.ok && data.code === 0) {
                        purchaseBtn.innerHTML = '<span class="material-icons" style="font-size:18px;vertical-align:middle;">check_circle</span> 购买成功';
                        showMsgBox({
                            title: '购买成功',
                            message: '恭喜！你已成功购买该资产，页面即将刷新。',
                            type: 'success',
                            onConfirm: () => { location.reload(); }
                        });
                    } else {
                        // 使用统一错误码映射
                        const errorCode = data.code || resp.status;
                        showUnifiedError(errorCode, data.message);
                        purchaseBtn.innerHTML = '<span class="material-icons">shopping_bag</span> 立即购买';
                        purchaseBtn.disabled = false;
                    }
                } catch (e) {
                    console.error('购买失败', e);
                    showUnifiedError('NETWORK_ERROR');
                    purchaseBtn.innerHTML = '<span class="material-icons">shopping_bag</span> 立即购买';
                    purchaseBtn.disabled = false;
                }
            });

            // 收藏按钮 — 使用后端 API（登录用户），未登录则跳转登录
            favBtn.addEventListener('click', async () => {
                const vm = window.currentProduct;
                const token = localStorage.getItem('kax_login_token');
                const icon = favBtn.querySelector('.material-icons');

                if (!token) {
                    showUnifiedError(401);
                    return;
                }

                try {
                    const isActive = favBtn.classList.contains('active');
                    if (!isActive) {
                        const resp = await fetch('/api/user/favorites', {
                            method: 'POST',
                            headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' },
                            body: JSON.stringify({ assetId: vm.assetId })
                        });
                        if (resp.ok) {
                            const body = await resp.json();
                            if (body.code === 0 || body.code === undefined) {
                                favBtn.classList.add('active');
                                icon.textContent = 'favorite';
                            } else {
                                showUnifiedError(body.code, body.message);
                            }
                        } else {
                            showUnifiedError(resp.status);
                        }
                    } else {
                        const resp = await fetch(`/api/user/favorites/${vm.assetId}`, {
                            method: 'DELETE',
                            headers: { 'Authorization': 'Bearer ' + token }
                        });
                        if (resp.ok) {
                            favBtn.classList.remove('active');
                            icon.textContent = 'favorite_border';
                        } else {
                            showUnifiedError(resp.status);
                        }
                    }
                } catch (e) {
                    console.error('收藏操作失败', e);
                    showUnifiedError('NETWORK_ERROR');
                }
            });

            // 检查是否已收藏
            checkIfFavorited();

            // 分享按钮
            shareBtn.addEventListener('click', () => {
                const vm = window.currentProduct;
                const shareUrl = window.location.href;
                const shareText = `我发现了一个不错的模组：${vm.displayName}，快来看看吧！`;

                if (navigator.share) {
                    navigator.share({
                        title: vm.displayName,
                        text: shareText,
                        url: shareUrl
                    }).catch(err => console.log('分享取消或出错:', err));
                } else {
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

        // 检查是否已收藏（/api/user/favorites 返回 number[] — design.md 附录 D）
        async function checkIfFavorited() {
            const vm = window.currentProduct;
            const favBtn = document.getElementById('favBtn');
            const icon = favBtn && favBtn.querySelector('.material-icons');
            const token = localStorage.getItem('kax_login_token');

            if (!token || !vm) return;

            try {
                const resp = await fetch('/api/user/favorites', {
                    headers: { 'Authorization': 'Bearer ' + token }
                });
                if (!resp.ok) return;
                const j = await resp.json();
                // 契约：data 为 number[]（资产 ID 数组）
                const raw = (j && j.code === 0 && Array.isArray(j.data)) ? j.data : [];
                const favSet = new Set(raw.map(id => Number(id)).filter(n => !isNaN(n)));
                if (favSet.has(vm.assetId)) {
                    favBtn.classList.add('active');
                    if (icon) icon.textContent = 'favorite';
                }
            } catch (e) {
                console.warn('[shop_detail] 检查收藏状态失败', e);
            }
        }

        // 更新购物车计数（/api/user/cart 返回 CartItem[] — design.md 附录 D）
        async function updateCartBadge() {
            const cartCountEl = document.getElementById('cartCount');
            const token = localStorage.getItem('kax_login_token');
            if (!cartCountEl || !token) return;

            try {
                const resp = await fetch('/api/user/cart', {
                    headers: { 'Authorization': 'Bearer ' + token }
                });
                if (!resp.ok) return;
                const j = await resp.json();
                // 契约：data 为 CartItem[]，每项包含 assetId/priceId/quantity
                if (j && j.code === 0 && Array.isArray(j.data)) {
                    const count = j.data.reduce((sum, item) => sum + Number(item.quantity || 1), 0);
                    cartCountEl.textContent = count;
                }
            } catch (e) {
                console.warn('[shop_detail] 获取购物车失败', e);
            }
        }

        // 导航到其他商品详情
        function goToShopDetail(id) {
            window.location.href = `/asset/detail/${id}`;
        }

        /** 渲染截图画廊（消费 ShopDetailVM.screenshots） */
        function renderGallery(vm) {
            const track = document.getElementById('galleryTrack');
            const thumbContainer = document.getElementById('thumbnailContainer');
            const prevBtn = document.getElementById('galleryPrev');
            const nextBtn = document.getElementById('galleryNext');
            if (!track) return;

            const images = Array.isArray(vm.screenshots) ? vm.screenshots.filter(Boolean) : [];
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

        /**
         * 英雄区滚动形态切换 v3 — 极简纯 CSS 驱动
         *
         * 原理：
         *   1. 面板在文档流中（position: relative）
         *   2. 滚动超出面板底部 → 添加 .is-docked → position: fixed，placeholder 撑位
         *   3. JS 只做两件事：切 class + 写 --t (0→1)
         *   4. 所有视觉变化都由 CSS 的 --t 和 .is-docked 驱动
         */
        function initHeroScrollMorph() {
            const panel = document.querySelector('.hero-product-row');
            const heroInner = document.querySelector('.product-hero-inner');
            if (!panel || !heroInner) return;

            let placeholder = null;
            let docked = false;
            let ticking = false;
            let anchorY = 0; // 面板在页面中的初始 offsetTop

            /* ── 占位符：保持布局不跳 ── */
            const addPlaceholder = () => {
                if (placeholder) return;
                const r = panel.getBoundingClientRect();
                placeholder = document.createElement('div');
                placeholder.className = 'hero-row-ph';
                placeholder.style.cssText =
                    `width:${r.width}px;height:${r.height}px;visibility:hidden;pointer-events:none;`;
                panel.after(placeholder);
            };
            const removePlaceholder = () => {
                if (!placeholder) return;
                placeholder.remove();
                placeholder = null;
            };

            /* ── 测量锚点位置（面板自然状态的 pageY） ── */
            const measureAnchor = () => {
                if (docked && placeholder) {
                    anchorY = placeholder.getBoundingClientRect().top + window.scrollY;
                } else {
                    anchorY = panel.getBoundingClientRect().top + window.scrollY;
                }
            };

            /* ── 进/出坞 ── */
            const dock = () => {
                if (docked) return;
                addPlaceholder();
                docked = true;
                panel.classList.add('is-docked');
                heroInner.classList.add('panel-docked');
            };
            const undock = () => {
                if (!docked) return;
                docked = false;
                panel.classList.remove('is-docked');
                heroInner.classList.remove('panel-docked');
                removePlaceholder();
            };

            /* ── 常量 ── */
            const DOCK_OFFSET = 8;                // 面板顶部距 topbar 多少 px 时触发进坞
            const UNDOCK_MARGIN = 6;               // 回弹容差

            /* ── 核心更新 ── */
            const update = () => {
                ticking = false;
                // 小屏降级
                if (window.innerWidth <= 640) {
                    if (docked) undock();
                    return;
                }

                const sy = window.scrollY;
                const topbarH = parseFloat(getComputedStyle(document.documentElement)
                    .getPropertyValue('--topbar-height')) || 60;
                const triggerY = anchorY - topbarH - DOCK_OFFSET;

                if (!docked && sy >= triggerY) {
                    dock();
                } else if (docked && sy < triggerY - UNDOCK_MARGIN) {
                    undock();
                    measureAnchor();              // 脱坞后重新测量
                }
            };

            const onScroll = () => {
                if (ticking) return;
                ticking = true;
                requestAnimationFrame(update);
            };

            measureAnchor();
            window.addEventListener('scroll', onScroll, { passive: true });
            window.addEventListener('resize', () => {
                measureAnchor();
                onScroll();
            }, { passive: true });
            update();
        }

        /** 从后端 API 加载相关推荐商品并渲染到推荐区域（使用 RelatedCardVM） */
        async function loadRelatedProducts(currentId) {
            const grid = document.getElementById('relatedProductsGrid');
            if (!grid) return;

            try {
                const resp = await fetch(`/api/asset/related/${currentId}?top=4`, {
                    headers: { 'Authorization': 'Bearer ' + (localStorage.getItem('kax_login_token') || '') }
                });
                
                if (!resp.ok) {
                    // 相关推荐加载失败不阻断主流程，仅展示空态
                    console.warn('[shop_detail] 加载相关推荐失败, HTTP', resp.status);
                    grid.innerHTML = '<div style="color:var(--text-muted,#888);padding:12px;text-align:center;">暂无相关推荐</div>';
                    return;
                }

                let json;
                try {
                    json = await resp.json();
                } catch (parseErr) {
                    console.warn('[shop_detail] 相关推荐 JSON 解析失败', parseErr);
                    return;
                }

                if (!json || (json.code !== 0 && json.code !== undefined) || !Array.isArray(json.data) || json.data.length === 0) {
                    // 空结果展示空态
                    grid.innerHTML = '<div style="color:var(--text-muted,#888);padding:12px;text-align:center;">暂无相关推荐</div>';
                    return;
                }

                // ── 映射层：RelatedDTO → RelatedCardVM ──────────────────────
                const items = json.data.map(mapRelatedToVM);

                const showCurrency = (v) => (v === null || v === undefined) ? '--' : ('💰' + Number(v).toFixed(2));

                grid.innerHTML = items.map(item => {
                    const thumbHtml = item.coverImage
                        ? `<img src="${item.coverImage}" alt="${item.displayName}" style="width:100%;height:100%;object-fit:cover;">`
                        : '🎮';
                    return `<div class="related-card" onclick="goToShopDetail(${item.assetId})">
                        <div class="related-thumb">${thumbHtml}</div>
                        <div class="related-info">
                            <div class="related-name">${item.displayName}</div>
                            <div class="related-meta">${item.category}</div>
                            <div class="related-price">${showCurrency(item.priceYuan)}</div>
                        </div>
                    </div>`;
                }).join('');
            } catch (e) {
                console.warn('[shop_detail] 加载相关推荐失败', e);
                // 网络异常不阻断主流程
                grid.innerHTML = '<div style="color:var(--text-muted,#888);padding:12px;text-align:center;">加载失败</div>';
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

        /** 获取字段当前值（从 ShopDetailVM 读取） */
        function getFieldCurrentValue(fieldKey) {
            const vm = window.currentProduct;
            if (!vm) return '';

            switch (fieldKey) {
                case 'name':          return vm.displayName || '';
                case 'description':   return vm.description || '';
                case 'category':      return vm.category || '';
                case 'author':        return vm.authorName || '';
                case 'version':       return vm.version || '';
                case 'fullDesc':      return vm.description || '';
                case 'license':       return vm.license || '';
                case 'compatibility': return vm.compatibility || '';
                default:              return '';
            }
        }

        /** 应用字段编辑到 DOM 和 _editedFields（同步 ViewModel 字段） */
        function applyFieldEdit(fieldKey, cfg, el, newValue) {
            el.textContent = newValue || '--';

            window._editedFields[fieldKey] = newValue;

            const vm = window.currentProduct;
            if (vm) {
                switch (fieldKey) {
                    case 'name':          vm.displayName = newValue; break;
                    case 'description':   vm.description = newValue; break;
                    case 'category':      vm.category = newValue; break;
                    case 'author':        vm.authorName = newValue; break;
                    case 'version':       vm.version = newValue; break;
                    case 'fullDesc':      vm.description = newValue; break;
                    case 'license':       vm.license = newValue; break;
                    case 'compatibility': vm.compatibility = newValue; break;
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

            const vm = window.currentProduct;
            const saveBtn = document.getElementById('editSaveBtn');
            saveBtn.disabled = true;
            saveBtn.innerHTML = '<span class="material-icons">hourglass_top</span> 保存中…';

            try {
                // 使用 ViewModel 的 assetId 作为请求主键
                const body = { id: vm.assetId };

                const fieldApiMap = {
                    name:          'name',
                    description:   'description',
                    fullDesc:      'description',
                    category:      'category',
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

