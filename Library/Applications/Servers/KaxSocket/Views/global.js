// 全局脚本：放置可复用的 UI 组件逻辑（例如自定义下拉）
// 注意：暴露为 window.initCustomSelects() 供各页面调用（非自动初始化，页面可按需调用）。
(function () {
    'use strict';

    function initCustomSelects() {
        document.querySelectorAll('select[data-custom-select]').forEach(function (select) {
            if (select._customInit) return; select._customInit = true;

            // wrapper + trigger + list
            var wrapper = document.createElement('div'); wrapper.className = 'custom-dropdown';
            var trigger = document.createElement('button'); trigger.type = 'button'; trigger.className = 'custom-dropdown__trigger btn ghost small';
            trigger.setAttribute('aria-haspopup', 'listbox'); trigger.setAttribute('aria-expanded', 'false'); trigger.setAttribute('aria-controls', select.id + '-list');
            var valueSpan = document.createElement('span'); valueSpan.className = 'custom-dropdown__value';
            var caret = document.createElement('span'); caret.className = 'custom-dropdown__caret material-icons'; caret.textContent = 'expand_more';
            trigger.appendChild(valueSpan); trigger.appendChild(caret);

            var list = document.createElement('ul'); list.className = 'custom-dropdown__list'; list.id = select.id + '-list'; list.setAttribute('role', 'listbox'); list.tabIndex = -1;

            Array.prototype.forEach.call(select.options, function (opt) {
                var li = document.createElement('li');
                li.className = 'custom-dropdown__item' + (opt.disabled ? ' disabled' : '');
                li.setAttribute('role', 'option'); li.setAttribute('data-value', opt.value);
                li.setAttribute('aria-selected', opt.selected ? 'true' : 'false'); li.textContent = opt.text;
                li.addEventListener('click', function (e) {
                    if (opt.disabled) return; select.value = opt.value; select.dispatchEvent(new Event('change', { bubbles: true })); updateUI(); close();
                });
                list.appendChild(li);
            });

            select.parentNode.insertBefore(wrapper, select.nextSibling);
            wrapper.appendChild(trigger); wrapper.appendChild(list);

            // 保留原生 select（用于脚本/表单），但从视觉上隐藏并移除 tab 聚焦
            select.setAttribute('aria-hidden', 'true'); select.tabIndex = -1; select.style.position = 'absolute'; select.style.left = '-9999px';

            function updateUI() {
                var si = select.selectedIndex; valueSpan.textContent = (si >= 0 ? (select.options[si] && select.options[si].text) || '' : '');
                Array.prototype.forEach.call(list.children, function (li) {
                    li.setAttribute('aria-selected', li.getAttribute('data-value') === select.value ? 'true' : 'false');
                    li.classList.remove('focused');
                });
            }

            function open() { wrapper.classList.add('open'); wrapper.setAttribute('aria-expanded', 'true'); trigger.setAttribute('aria-expanded', 'true'); list.focus(); }
            function close() { wrapper.classList.remove('open'); wrapper.setAttribute('aria-expanded', 'false'); trigger.setAttribute('aria-expanded', 'false'); }
            function toggle() { wrapper.classList.contains('open') ? close() : open(); }

            trigger.addEventListener('click', function (e) { e.stopPropagation(); toggle(); });

            // 键盘交互（基本）
            var focused = -1;
            function focusOption(idx) {
                var items = Array.prototype.filter.call(list.querySelectorAll('.custom-dropdown__item'), function (i) { return !i.classList.contains('disabled'); });
                if (!items.length) return; if (idx < 0) idx = 0; if (idx >= items.length) idx = items.length - 1;
                items.forEach(function (it) { it.classList.remove('focused'); });
                items[idx].classList.add('focused'); items[idx].scrollIntoView({ block: 'nearest' }); focused = idx;
            }
            trigger.addEventListener('keydown', function (e) {
                if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); open(); focusOption(e.key === 'ArrowDown' ? 0 : list.children.length - 1); }
                else if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggle(); }
                else if (e.key === 'Escape') { close(); }
            });
            list.addEventListener('keydown', function (e) {
                var items = Array.prototype.filter.call(list.querySelectorAll('.custom-dropdown__item'), function (i) { return !i.classList.contains('disabled'); });
                if (e.key === 'ArrowDown') { e.preventDefault(); focusOption(Math.min(focused + 1, items.length - 1)); }
                else if (e.key === 'ArrowUp') { e.preventDefault(); focusOption(Math.max(focused - 1, 0)); }
                else if (e.key === 'Enter') { e.preventDefault(); var it = items[focused] || items[0]; it && it.click(); }
                else if (e.key === 'Escape') { e.preventDefault(); close(); trigger.focus(); }
            });

            list.addEventListener('click', function (e) { e.stopPropagation(); });
            document.addEventListener('click', function (ev) { if (!wrapper.contains(ev.target)) close(); });

            // 同步：当原生 select 值被程序或表单改变时更新 UI
            select.addEventListener('change', updateUI);

            // 初始化显示
            updateUI();
        });
    }

    // 导出到全局作用域
    window.initCustomSelects = initCustomSelects;

    // 可选：初始化高级按钮交互（动态光泽 + JS 驱动点击波纹）
    // 用法：window.initButtonEffects() — 页面按需调用；会尊重 prefers-reduced-motion
    function initButtonEffects(opts) {
        opts = opts || {};
        var selector = opts.selector || '.btn, .btn.icon, .copy-icon, .info-btn, .panel-close, .cta-button';
        var reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        var nodes = Array.prototype.slice.call(document.querySelectorAll(selector));

        nodes.forEach(function (el) {
            if (el._btnFxInit) return; el._btnFxInit = true;
            if (el.getAttribute('data-advanced-effects') === 'false') return; // opt-out

            // 确保裁剪（避免光泽溢出）并开启合成隔离
            if (getComputedStyle(el).position === 'static') el.style.position = 'relative';
            el.style.overflow = 'hidden';
            el.style.willChange = 'transform';
            el.classList.add('js-sheen-active');

            if (!reduced) {
                // JS 驱动的 sheen 元素（跟随鼠标 / focus）
                var sheen = document.createElement('div');
                sheen.className = 'js-sheen';
                sheen.style.cssText = 'position:absolute;left:-120%;top:-10%;width:260%;height:120%;background:linear-gradient(110deg, rgba(255,255,255,0.02), rgba(255,255,255,0.08) 50%, rgba(255,255,255,0.02));transform:skewX(-12deg) translateX(-15%);pointer-events:none;mix-blend-mode:overlay;opacity:.95;transition:transform .45s cubic-bezier(.2,.9,.2,1),opacity .3s ease;';
                el.insertBefore(sheen, el.firstChild);
                el._sheen = sheen;

                var raf = null; var target = -15;
                function onMove(e) {
                    var r = el.getBoundingClientRect();
                    var px = ((e.clientX - r.left) / r.width) * 100;
                    target = Math.max(-40, Math.min(140, (px - 50) * 2 + 50));
                    if (!raf) {
                        raf = requestAnimationFrame(function () { raf = null; sheen.style.transform = 'skewX(-12deg) translateX(' + target + '%)'; });
                    }
                }
                el.addEventListener('mousemove', onMove);
                el._onMove = onMove;

                el.addEventListener('mouseenter', function () { sheen.style.opacity = '1'; });
                el.addEventListener('mouseleave', function () { sheen.style.transform = 'skewX(-12deg) translateX(-15%)'; sheen.style.opacity = '0.95'; });
                el.addEventListener('focus', function () { sheen.style.transform = 'skewX(-12deg) translateX(25%)'; }, true);
                el.addEventListener('blur', function () { sheen.style.transform = 'skewX(-12deg) translateX(-15%)'; }, true);
            }

            // 点击波纹（纯 JS，自动移除）
            el.addEventListener('click', function (ev) {
                if (el.getAttribute('data-ripple') === 'false') return;
                var r = el.getBoundingClientRect();
                var size = Math.max(r.width, r.height) * 1.6;
                var ripple = document.createElement('span');
                ripple.className = 'js-ripple';
                ripple.style.cssText = 'position:absolute;border-radius:50%;background:rgba(255,255,255,0.12);pointer-events:none;left:' + (ev.clientX - r.left - size / 2) + 'px;top:' + (ev.clientY - r.top - size / 2) + 'px;width:' + size + 'px;height:' + size + 'px;transform:scale(0);opacity:1;transition:transform .55s cubic-bezier(.2,.9,.2,1),opacity .6s ease;';
                el.appendChild(ripple);
                requestAnimationFrame(function () { ripple.style.transform = 'scale(1)'; ripple.style.opacity = '0'; });
                setTimeout(function () { try { ripple.remove(); } catch (e) { } }, 700);
            }, false);
        });

        return {
            destroy: function () {
                nodes.forEach(function (el) {
                    if (!el._btnFxInit) return;
                    el._btnFxInit = false;
                    el.classList.remove('js-sheen-active');
                    if (el._sheen) { el._sheen.remove(); delete el._sheen; }
                    if (el._onMove) { el.removeEventListener('mousemove', el._onMove); delete el._onMove; }
                });
            }
        };
    }
    window.initButtonEffects = initButtonEffects;

    // 在页面底部注入统一 footer（复用 index.html 的样式与文本）
    // 变更：footer 初始为隐藏，仅在页面可滚动且用户滚动到页面底部时显示（避免一直占用视图）
    window.initGlobalFooter = function () {
        try {
            if (document.getElementById('kax-global-footer')) return;
            var footer = document.createElement('footer');
            footer.id = 'kax-global-footer';
            footer.className = 'kax-global-footer';

            // 扁平纯色风格，隐藏在视口外（通过 transform 控制显隐）
            footer.style.cssText = 'position:fixed; left:0; right:0; bottom:0; width:100%; box-sizing:border-box; padding:14px 20px; text-align:center; color:#cbd5e1; font-size:0.9rem; border-top:1px solid #111; background:#0f0f0f; z-index:9999; transform:translateY(100%); transition:transform .28s cubic-bezier(.2,.9,.2,1),opacity .18s linear; opacity:0; pointer-events:none;';
            footer.setAttribute('aria-hidden', 'true');
            footer.innerHTML = '开发中 • 功能有限 • 欢迎反馈与建议<br>© 2026 KaxHub Team';
            document.body.appendChild(footer);

            // 记录原始 body padding-bottom（以便短页面不被多余填充）
            var baseBodyPad = parseFloat(getComputedStyle(document.body).paddingBottom) || 0;
            var footerHeight = Math.ceil(footer.getBoundingClientRect().height || 0);
            var appliedFooterPadding = 0;

            function applyFooterPaddingIfScrollable() {
                var docH = Math.max(document.documentElement.scrollHeight, document.body.scrollHeight);
                var winH = window.innerHeight || document.documentElement.clientHeight;
                var scrollable = docH > winH + 1;
                if (scrollable) {
                    var want = Math.max(baseBodyPad, footerHeight + 8);
                    if (appliedFooterPadding !== want) {
                        document.body.style.paddingBottom = want + 'px';
                        appliedFooterPadding = want;
                    }
                } else {
                    if (appliedFooterPadding !== 0) {
                        document.body.style.paddingBottom = baseBodyPad + 'px';
                        appliedFooterPadding = 0;
                    }
                }
            }

            // 根据滚动位置显示/隐藏 footer（仅当页面可滚动时在底部显示）
            var rafId = null;
            function checkVisibility() {
                var doc = document.documentElement;
                var docH = Math.max(doc.scrollHeight, document.body.scrollHeight);
                var winH = window.innerHeight || doc.clientHeight;
                var scTop = window.pageYOffset || doc.scrollTop || document.body.scrollTop || 0;
                var atBottom = (scTop + winH) >= (docH - 24);
                var scrollable = docH > winH + 1;

                applyFooterPaddingIfScrollable();

                if (scrollable && atBottom) {
                    footer.style.transform = 'translateY(0)';
                    footer.style.opacity = '1';
                    footer.style.pointerEvents = 'auto';
                    footer.setAttribute('aria-hidden', 'false');
                } else {
                    footer.style.transform = 'translateY(100%)';
                    footer.style.opacity = '0';
                    footer.style.pointerEvents = 'none';
                    footer.setAttribute('aria-hidden', 'true');
                }
            }

            function scheduleCheck() {
                if (rafId) return;
                rafId = requestAnimationFrame(function () { rafId = null; checkVisibility(); });
            }

            window.addEventListener('scroll', scheduleCheck, { passive: true });
            window.addEventListener('resize', function () {
                footerHeight = Math.ceil(footer.getBoundingClientRect().height || 0);
                scheduleCheck();
            });

            // 初始化检查（短页面保持隐藏，长页面滚到底部才显示）
            scheduleCheck();
        } catch (e) {
            console.error('initGlobalFooter error', e);
        }
    };

    /* ---------------------------
       Global topbar partial + behavior
       - 把 index.html 中的 topbar markup 提取为 `/components/topbar.html`
       - 自动注入（若页面未包含 topbar）并初始化行为（token 校验 / 菜单 / 按钮）
       --------------------------- */
    function insertTopbarHtml(html) {
        if (document.querySelector('.topbar')) return false;
        var tmp = document.createElement('div'); tmp.innerHTML = html;
        var header = tmp.querySelector('.topbar');
        if (!header) return false;
        document.body.insertBefore(header, document.body.firstChild);
        document.body.classList.add('has-global-topbar');
        return true;
    }

    function initTopbarBehavior() {
        if (window._kaxTopbarInit) return; window._kaxTopbarInit = true;

        // 顶栏滚动样式
        (function () {
            const topbar = document.querySelector('.topbar');
            if (!topbar) return;
            function updateTopbar() { if (window.scrollY > 6) topbar.classList.add('topbar--scrolled'); else topbar.classList.remove('topbar--scrolled'); }
            let ticking = false;
            window.addEventListener('scroll', () => { if (!ticking) { window.requestAnimationFrame(() => { updateTopbar(); ticking = false; }); ticking = true; } }, { passive: true });
            document.addEventListener('DOMContentLoaded', updateTopbar);
            updateTopbar();
        })();

        // joinBtn / token 校验 与 header 状态
        (function () {
            var btn = document.getElementById('joinBtn');
            var originalText = btn ? (btn.textContent || '') : '';
            var keepDisabled = false;

            function setBtnDisabled(el, disabled) {
                if (!el) return;
                if (disabled) {
                    el.classList.add('kax-disabled');
                    el.setAttribute('aria-disabled', 'true');
                    el.setAttribute('tabindex', '-1');
                } else {
                    el.classList.remove('kax-disabled');
                    el.setAttribute('aria-disabled', 'false');
                    el.removeAttribute('tabindex');
                }
            }

            async function checkTokenOnLoad() {
                function resetHeaderAuth() {
                    var header = document.getElementById('headerAuth');
                    if (!header) return;
                    header.innerHTML = '<a href="/register" class="cta-button small">注册</a>' + '<a href="/login" class="cta-button small">登录</a>';
                    try { var um = document.getElementById('userMenu'); if (um) { um.classList.remove('open'); um.setAttribute('aria-hidden', 'true'); } } catch (_) { }
                }

                try {
                    var token = localStorage.getItem('kax_login_token');
                    if (!token) { resetHeaderAuth(); return; }

                    var controller = new AbortController();
                    var timeoutId = setTimeout(function () { controller.abort(); }, 6000);
                    var resp = await fetch('/api/user/verify/account', { method: 'POST', headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token }, signal: controller.signal });
                    clearTimeout(timeoutId);

                    try { if (!(resp.status >= 200 && resp.status < 300)) { try { localStorage.removeItem('kax_login_token'); } catch (_) { } try { setBtnDisabled(btn, false); } catch (_) { } try { var um = document.getElementById('userMenu'); if (um) { um.classList.remove('open'); um.setAttribute('aria-hidden', 'true'); } } catch (_) { } resetHeaderAuth(); } } catch (_) { }

                    if (resp.status === 200) {
                        try {
                            setBtnDisabled(btn, true);
                            if (btn) { btn.textContent = '你已参加测试'; btn.removeAttribute('href'); }

                            try {
                                var j = await resp.json();
                                if (j && j.isAdmin) { var adminDiv = document.getElementById('adminLinks'); if (adminDiv) adminDiv.classList.remove('hidden'); }

                                try {
                                    var header = document.getElementById('headerAuth');
                                    if (header) {
                                        var seed = (j && j.user) ? encodeURIComponent(j.user) : (Math.random().toString(36).slice(2, 8));
                                        // 优先使用后端返回的持久化头像（若无则回退到 pravatar 占位图）
                                        var avatarUrl = (j && j.avatarUrl) ? j.avatarUrl : ('https://i.pravatar.cc/40?u=' + seed);
                                        header.innerHTML = '<img class="avatar-image" role="button" tabindex="0" aria-haspopup="true" aria-expanded="false" src="' + avatarUrl + '" alt="avatar" title="' + (j && j.user ? j.user : '已登录') + '">';

                                        try {
                                            var userMenu = document.getElementById('userMenu');
                                            if (userMenu) {
                                                var uma = userMenu.querySelector('#userMenuAvatar'); if (uma) uma.src = avatarUrl;
                                                var unameEl = userMenu.querySelector('#userMenuName'); if (unameEl) unameEl.textContent = (j && j.user) ? j.user : '已登录';
                                                var ubioEl = userMenu.querySelector('#userMenuBio'); if (ubioEl) ubioEl.textContent = (j && j.bio) ? j.bio : '敲敲……';
                                            }
                                        } catch (_) { }
                                    }
                                } catch (_) { }
                            } catch (_) { }
                        } catch (_) { }
                    } else if (resp.status === 401) {
                        try { localStorage.removeItem('kax_login_token'); } catch (_) { }
                        try { var um = document.getElementById('userMenu'); if (um) { um.classList.remove('open'); um.setAttribute('aria-hidden', 'true'); } } catch (_) { }
                        resetHeaderAuth();
                    } else {
                        console.warn('token 验证返回状态：', resp.status);
                        try { var um = document.getElementById('userMenu'); if (um) { um.classList.remove('open'); um.setAttribute('aria-hidden', 'true'); } } catch (_) { }
                        resetHeaderAuth();
                    }
                } catch (err) {
                    if (err && err.name === 'AbortError') { console.warn('token 验证超时，跳过自动禁用。'); }
                    else { console.error('加载时验证 token 发生错误：', err); }
                    try { var um = document.getElementById('userMenu'); if (um) { um.classList.remove('open'); um.setAttribute('aria-hidden', 'true'); } } catch (_) { }
                    resetHeaderAuth();
                }
            }

            if (document.readyState === 'complete' || document.readyState === 'interactive') { checkTokenOnLoad(); }
            else { document.addEventListener('DOMContentLoaded', checkTokenOnLoad); }

            // Avatar / menu 行为（事件委托）
            document.addEventListener('click', function (e) {
                var avatar = e.target.closest('.avatar-image');
                var menu = document.getElementById('userMenu');
                if (avatar) {
                    if (!menu) return;
                    var opening = !menu.classList.contains('open');
                    if (opening) {
                        menu.classList.add('open'); menu.classList.remove('closing'); avatar.setAttribute('aria-expanded', 'true'); menu.setAttribute('aria-hidden', 'false');
                        var rect = avatar.getBoundingClientRect(); menu.style.left = rect.left + 'px'; menu.style.top = (rect.bottom + 8) + 'px';
                        requestAnimationFrame(function () {
                            var mrect = menu.getBoundingClientRect(); if (mrect.right > window.innerWidth - 8) { var newLeft = Math.max(8, window.innerWidth - mrect.width - 8); menu.style.left = newLeft + 'px'; }
                            var first = menu.querySelector('[role="menuitem"]'); if (first) first.focus();
                        });
                    } else {
                        menu.classList.remove('open'); menu.classList.add('closing'); avatar.setAttribute('aria-expanded', 'false'); menu.setAttribute('aria-hidden', 'true');
                        setTimeout(function () { menu.classList.remove('closing'); menu.style.left = ''; menu.style.top = ''; }, 220);
                    }
                    e.stopPropagation();
                    return;
                }

                if (menu && !e.target.closest('#userMenu')) {
                    if (menu.classList.contains('open')) {
                        menu.classList.remove('open'); menu.classList.add('closing'); menu.setAttribute('aria-hidden', 'true');
                        document.querySelectorAll('.avatar-image').forEach(a => a.setAttribute('aria-expanded', 'false'));
                        setTimeout(function () { menu.classList.remove('closing'); menu.style.left = ''; menu.style.top = ''; }, 220);
                    }
                }
            }, { passive: true });

            // 键盘支持（Esc / Enter on avatar）
            document.addEventListener('keydown', function (e) {
                var menu = document.getElementById('userMenu');
                if (e.key === 'Escape') {
                    if (menu && menu.classList.contains('open')) { menu.classList.remove('open'); menu.classList.add('closing'); menu.setAttribute('aria-hidden', 'true'); document.querySelectorAll('.avatar-image').forEach(a => a.setAttribute('aria-expanded', 'false')); setTimeout(function () { menu.classList.remove('closing'); menu.style.left = ''; menu.style.top = ''; }, 220); }
                    return;
                }
                var active = document.activeElement;
                if (active && active.classList && active.classList.contains('avatar-image') && (e.key === 'Enter' || e.key === ' ')) {
                    e.preventDefault(); if (!menu) return; var isOpen = menu.classList.toggle('open'); active.setAttribute('aria-expanded', isOpen ? 'true' : 'false'); menu.setAttribute('aria-hidden', isOpen ? 'false' : 'true'); if (isOpen) { var first = menu.querySelector('[role="menuitem"]'); if (first) first.focus(); }
                }
            });

            // CDK 下拉面板：定位 / 打开 / 关闭 / 激活交互
            (function () {
                var btn = document.getElementById('cdkBtn');
                var panel = document.getElementById('cdkPanel');
                if (!btn || !panel) return;

                function positionPanel() {
                    var rect = btn.getBoundingClientRect();
                    // 若面板尚未计算宽度，先暂时显示以测量（通过 open class）
                    panel.style.left = Math.max(8, Math.min(rect.left, window.innerWidth - panel.offsetWidth - 8)) + 'px';
                    panel.style.top = (rect.bottom + 8) + 'px';
                    var pr = panel.getBoundingClientRect();
                    if (pr.right > window.innerWidth - 8) {
                        panel.style.left = Math.max(8, window.innerWidth - pr.width - 8) + 'px';
                    }
                }

                function open() {
                    panel.classList.add('open');
                    panel.setAttribute('aria-hidden', 'false');
                    btn.setAttribute('aria-expanded', 'true');
                    // 延迟定位以确保 CSS 过渡与尺寸计算稳定
                    requestAnimationFrame(positionPanel);
                    var inp = panel.querySelector('input');
                    if (inp) setTimeout(function () { try { inp.focus(); inp.select && inp.select(); } catch (_) { } }, 80);
                }
                function close() {
                    panel.classList.remove('open');
                    panel.setAttribute('aria-hidden', 'true');
                    btn.setAttribute('aria-expanded', 'false');
                    setTimeout(function () { panel.style.left = ''; panel.style.top = ''; }, 220);
                }
                function toggle() { panel.classList.contains('open') ? close() : open(); }

                btn.addEventListener('click', function (e) {
                    // 允许用户使用 Ctrl/Cmd/中键在新标签打开链接（保持原始 href 行为）
                    if (e.ctrlKey || e.metaKey || e.button === 1) return;
                    e.preventDefault(); e.stopPropagation(); toggle();
                }, false);

                // 点击面板外部关闭
                document.addEventListener('click', function (ev) {
                    if (!panel.classList.contains('open')) return;
                    if (panel.contains(ev.target) || btn.contains(ev.target)) return;
                    close();
                }, { passive: true });

                // Esc 关闭
                document.addEventListener('keydown', function (ev) { if (ev.key === 'Escape' && panel.classList.contains('open')) { close(); btn.focus(); } });

                // 阻止面板内部点击冒泡（避免 document click 立即关闭）
                panel.addEventListener('click', function (ev) { ev.stopPropagation(); });

                // 激活按钮交互（真实 API 调用）
                var activate = document.getElementById('activateCdkBtn');
                activate && activate.addEventListener('click', function (ev) {
                    var input = document.getElementById('cdkInput');
                    var val = input ? input.value.trim() : '';
                    if (!val) { input && input.focus(); return; }
                    window.activateCdkFromTopbar(val, activate, panel);
                }, false);

                // 对 CDK 输入框支持回车键激活
                var cdkInputInTopbar = panel.querySelector('input[placeholder*="CDK"]');
                if (cdkInputInTopbar) {
                    cdkInputInTopbar.addEventListener('keypress', function (e) {
                        if (e.key === 'Enter') {
                            e.preventDefault();
                            activate && activate.click();
                        }
                    });
                }

                // 调整位置：窗口变化时保持面板对齐
                window.addEventListener('resize', function () { if (panel.classList.contains('open')) positionPanel(); });
                window.addEventListener('scroll', function () { if (panel.classList.contains('open')) positionPanel(); }, { passive: true });
            })();

            // 注销按钮
            document.addEventListener('click', function (e) { var s = e.target.closest('#signOutBtn'); if (s) { e.preventDefault(); try { localStorage.removeItem('kax_login_token'); } catch (_) { } location.reload(); return; } });

            // joinBtn 点击验证接口（保持与 index.html 语义一致）
            if (btn) {
                btn.addEventListener('click', async function (e) {
                    try {
                        if (btn.classList.contains('kax-disabled') || btn.getAttribute('aria-disabled') === 'true') { e.preventDefault(); return; }
                        var token = localStorage.getItem('kax_login_token');
                        if (!token) return; // 保持 <a> 的默认跳转

                        e.preventDefault(); btn.textContent = '验证中...'; setBtnDisabled(btn, true);
                        var controller = new AbortController(); var timeoutId = setTimeout(function () { controller.abort(); }, 8000);
                        var resp = await fetch('/api/token/test', { method: 'POST', headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token }, signal: controller.signal });
                        clearTimeout(timeoutId);

                        try { if (!(resp.status >= 200 && resp.status < 300)) { try { localStorage.removeItem('kax_login_token'); } catch (_) { } try { setBtnDisabled(btn, false); } catch (_) { } } } catch (_) { }

                        if (resp.status === 200) {
                            setBtnDisabled(btn, true); btn.textContent = '你已参加测试'; btn.removeAttribute('href'); try { await checkTokenOnLoad(); } catch (_) { }
                            var features = document.getElementById('features'); if (features) features.scrollIntoView({ behavior: 'smooth' }); return;
                        }

                        if (resp.status === 429) {
                            try {
                                var retrySeconds = 60; var ra = resp.headers.get('Retry-After'); if (ra) { var n = parseInt(ra, 10); if (!isNaN(n)) retrySeconds = n; else { var t = Date.parse(ra); if (!isNaN(t)) retrySeconds = Math.max(1, Math.ceil((t - Date.now()) / 1000)); } }
                                alert('操作过于频繁，请在 ' + retrySeconds + ' 秒后重试。'); keepDisabled = true; setBtnDisabled(btn, true);
                                var remaining = retrySeconds; btn.textContent = '请稍后重试 (' + remaining + 's)';
                                var intervalId = setInterval(function () { remaining--; if (remaining <= 0) { clearInterval(intervalId); try { setBtnDisabled(btn, false); btn.textContent = originalText; } catch (_) { } keepDisabled = false; } else { try { btn.textContent = '请稍后重试 (' + remaining + 's)'; } catch (_) { } } }, 1000);
                            } catch (err429) { console.error('处理 429 时出错：', err429); }
                            return;
                        }

                        if (resp.status === 401) { localStorage.removeItem('kax_login_token'); location.href = 'login'; return; }
                        if (resp.status === 403) { try { var j = await resp.json(); if (j && j.message) alert(j.message); } catch (_) { alert('您的账号无法访问此资源。'); } localStorage.removeItem('kax_login_token'); location.href = 'login'; return; }

                        try { var txt = await resp.text(); if (txt) console.warn('token test 返回：', txt); } catch (_) { }
                        localStorage.removeItem('kax_login_token'); location.href = 'login';

                    } catch (err) { console.error('验证登录令牌时出错', err); try { localStorage.removeItem('kax_login_token'); } catch (_) { } alert('无法验证登录状态，请重新登录。'); location.href = 'login'; }
                    finally { try { if (!keepDisabled) { setBtnDisabled(btn, false); btn.textContent = originalText; } } catch (_) { } }
                });
            }

            // 菜单内 Tab 循环（accessibility）
            (function () { const menu = document.getElementById('userMenu'); if (!menu) return; menu.addEventListener('keydown', function (e) { if (e.key !== 'Tab') return; const items = Array.from(menu.querySelectorAll('[role="menuitem"]')); if (items.length === 0) return; const idx = items.indexOf(document.activeElement); if (e.shiftKey) { if (idx === 0) { items[items.length - 1].focus(); e.preventDefault(); } } else { if (idx === items.length - 1) { items[0].focus(); e.preventDefault(); } } }); })();
        })();
    }

    // initGlobalTopbar — 可手动调用
    window.initGlobalTopbar = function () {
        if (document.querySelector('.topbar')) { initTopbarBehavior(); return Promise.resolve(); }
        return fetch('/components/topbar.html', { cache: 'no-cache' }).then(function (r) { if (!r.ok) return Promise.reject(new Error('failed to load topbar')); return r.text(); }).then(function (html) { var inserted = insertTopbarHtml(html); if (inserted) initTopbarBehavior(); return inserted; }).catch(function (err) { console.warn('initGlobalTopbar failed', err); return false; });
    };

    // 全局 CDK 激活函数（可从任何需要激活 CDK 的地方调用）
    window.activateCdkFromTopbar = async function (cdkCode, activateBtn, panelElement) {
        if (!cdkCode || cdkCode.trim().length === 0) {
            if (activateBtn) activateBtn.textContent = 'CDK为空';
            setTimeout(function () {
                if (activateBtn) activateBtn.textContent = '激活';
            }, 1800);
            return;
        }

        var token = localStorage.getItem('kax_login_token');
        if (!token) {
            alert('请先登录');
            location.href = '/login';
            return;
        }

        if (activateBtn) {
            activateBtn.setAttribute('disabled', 'true');
            activateBtn.textContent = '激活中…';
        }

        try {
            var resp = await fetch('/api/cdk/activate', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer ' + token
                },
                body: JSON.stringify({ code: cdkCode })
            });

            var result = await resp.json().catch(function () { return { code: 500, message: '无效的响应格式' }; });

            if (resp.status === 200) {
                // 激活成功
                if (activateBtn) {
                    activateBtn.textContent = '✓ 激活成功';
                    activateBtn.classList.add('kax-disabled');
                }
                
                // 显示详情
                var details = [];
                if (result.assetId > 0) details.push('资源 #' + result.assetId);
                if (result.contributionValue > 0) details.push('+' + result.contributionValue + ' 贡献值');
                if (result.description) details.push(result.description);
                var msg = details.length > 0 ? details.join(' • ') : '资源已添加至您的库中';
                console.log('[CDK 激活成功]', msg);

                // 延迟关闭面板和恢复按钮
                setTimeout(function () {
                    if (activateBtn) {
                        activateBtn.removeAttribute('disabled');
                        activateBtn.classList.remove('kax-disabled');
                        activateBtn.textContent = '激活';
                    }
                    if (panelElement && panelElement.classList) {
                        panelElement.classList.remove('open');
                    }
                    // 清空输入框
                    var input = document.getElementById('cdkInput');
                    if (input) input.value = '';
                }, 1500);
            } else if (resp.status === 401) {
                // 未授权
                localStorage.removeItem('kax_login_token');
                location.href = '/login';
            } else {
                // 处理各种错误
                var errorMsg = result.message || '激活失败';
                if (result.code === 1) errorMsg = 'CDK为空';
                else if (result.code === 2) errorMsg = 'CDK错误';
                else if (result.code === 3) errorMsg = 'CDK已使用';

                if (activateBtn) {
                    activateBtn.textContent = errorMsg;
                }
                console.warn('[CDK 激活失败]', errorMsg);

                setTimeout(function () {
                    if (activateBtn) {
                        activateBtn.removeAttribute('disabled');
                        activateBtn.textContent = '激活';
                    }
                }, 2000);
            }
        } catch (err) {
            console.error('[CDK 激活异常]', err);
            if (activateBtn) {
                activateBtn.textContent = '网络错误';
                setTimeout(function () {
                    activateBtn.removeAttribute('disabled');
                    activateBtn.textContent = '激活';
                }, 2000);
            }
        }
    };

    // 页面加载时尝试自动注入并初始化 topbar（友好降级）
    if (document.readyState === 'complete' || document.readyState === 'interactive') {
        window.initGlobalTopbar && window.initGlobalTopbar();
    } else {
        document.addEventListener('DOMContentLoaded', function () { window.initGlobalTopbar && window.initGlobalTopbar(); });
    }

    // 暴露 initCustomSelects 到全局作用域
    window.initCustomSelects = initCustomSelects;

})();
