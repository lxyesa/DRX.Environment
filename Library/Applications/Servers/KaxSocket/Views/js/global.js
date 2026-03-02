// 全局脚本：放置可复用的 UI 组件逻辑（例如自定义下拉）
// 注意：暴露为 window.initCustomSelects() 供各页面调用（非自动初始化，页面可按需调用）。
(function () {
    'use strict';

    // 防止 global.js 被重复引入时二次执行（部分页面存在重复 script）
    if (window.__kaxGlobalScriptLoaded) return;
    window.__kaxGlobalScriptLoaded = true;

    // 动态加载头像缓存模块（若尚未加载）
    if (!window.AvatarCache) {
        var s = document.createElement('script');
        s.src = '/js/avatarCache.js';
        document.head.appendChild(s);
    }

    // 尽早记录页面加载开始时间（供骨架屏计时使用）
    if (!window._pageLoadStartTime) window._pageLoadStartTime = Date.now();

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

    // 暴露 initCustomSelects 到全局作用域
    window.initCustomSelects = initCustomSelects;

    // 页面加载延迟工具 - 防止过快发送请求
    // 用法: await window.ensureMinLoadDelay() 在页面初始化时调用
    window.ensureMinLoadDelay = function (minDelayMs) {
        minDelayMs = minDelayMs || 750;
        var startTime = window._pageLoadStartTime || Date.now();
        window._pageLoadStartTime = startTime;
        var elapsed = Date.now() - startTime;
        var remaining = Math.max(0, minDelayMs - elapsed);
        return remaining > 0 ? new Promise(function (resolve) { setTimeout(resolve, remaining); }) : Promise.resolve();
    };

    // 全局加载动画系统（GlobalLoader）
    (function () {
        if (!window._pageLoadStartTime) window._pageLoadStartTime = Date.now();

        var LOGO_PATH = 'M842.223,392.426C747.728,264.592,578.111,220.273,440.649,280.587,288.272,347.445,208.43,528.211,267.543,694.007l36.648-35.48c-35.041-137.6,32.327-276.622,151.273-330.89,98.677-45.021,221.578-26.327,309.563,52.448l-198.058,204.4,70.958,151.176h95.91L662.879,580.625Zm17.155,33.167C947.655,590.739,878.2,795.312,715.9,872.95,562.363,946.4,368.689,885.667,283.918,730.259L560.731,448.732h88.892L527.2,575.226l74.857,158.889-96.69.771L465.6,641.558,356.435,752.626c88.7,103.687,239.838,128.988,354.789,62.476,120.955-69.986,170.954-223.713,111.505-354.029ZM375.149,448.732H467.94l14.815,30.852-61.6,61.7Z';
        var MIN_DURATION = 900;
        var LEAVE_DURATION = 520;
        var hidden = false;

        function markup() {
            return ''
                + '<div class="kax-loader-overlay" id="kaxGlobalLoader" aria-hidden="true">'
                + '  <div class="kax-loader-noise" aria-hidden="true"></div>'
                + '  <div class="kax-loader-aurora kax-loader-aurora-a" aria-hidden="true"></div>'
                + '  <div class="kax-loader-aurora kax-loader-aurora-b" aria-hidden="true"></div>'
                + '  <div class="kax-loader-grid" aria-hidden="true"></div>'
                + '  <div class="kax-loader-core">'
                + '    <svg class="kax-loader-ring" viewBox="0 0 320 320" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">'
                + '      <circle class="ring ring-back" cx="160" cy="160" r="128"/>'
                + '      <circle class="ring ring-mid" cx="160" cy="160" r="112"/>'
                + '      <circle class="ring ring-front" cx="160" cy="160" r="92"/>'
                + '    </svg>'
                + '    <svg class="kax-loader-logo" viewBox="170 170 850 850" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">'
                + '      <defs>'
                + '        <linearGradient id="kaxLoaderGrad" x1="0%" y1="0%" x2="100%" y2="100%">'
                + '          <stop offset="0%" stop-color="#9bc0ff"/>'
                + '          <stop offset="55%" stop-color="#79a0ff"/>'
                + '          <stop offset="100%" stop-color="#4a6fff"/>'
                + '        </linearGradient>'
                + '        <clipPath id="kaxLoaderClip">'
                + '          <path d="' + LOGO_PATH + '"/>'
                + '        </clipPath>'
                + '      </defs>'
                + '      <path class="logo-fill" d="' + LOGO_PATH + '"/>'
                + '      <g clip-path="url(#kaxLoaderClip)">'
                + '        <rect class="logo-scan" x="80" y="120" width="1020" height="980"></rect>'
                + '      </g>'
                + '      <path class="logo-stroke" d="' + LOGO_PATH + '"/>'
                + '    </svg>'
                + '    <div class="kax-loader-badge">KAXHUB</div>'
                + '    <div class="kax-loader-text" aria-live="polite">LOADING<span class="dots"></span></div>'
                + '  </div>'
                + '</div>';
        }

        function setLoadingClass(name, enabled) {
            var html = document.documentElement;
            var body = document.body;
            if (enabled) {
                if (html) html.classList.add(name);
                if (body) body.classList.add(name);
            } else {
                if (html) html.classList.remove(name);
                if (body) body.classList.remove(name);
            }
        }

        function ensureOverlay() {
            var overlay = document.getElementById('kaxGlobalLoader');
            if (overlay) return overlay;

            var host = document.createElement('div');
            host.innerHTML = markup();
            overlay = host.firstElementChild;
            if (!overlay) return null;

            var parent = document.body || document.documentElement;
            if (!parent) return null;
            if (parent.firstChild) parent.insertBefore(overlay, parent.firstChild);
            else parent.appendChild(overlay);
            return overlay;
        }

        function show() {
            var overlay = ensureOverlay();
            if (!overlay) return false;
            hidden = false;
            overlay.classList.remove('is-leaving');
            overlay.classList.add('is-visible');
            setLoadingClass('skeleton-loading', true);
            return true;
        }

        function hide(force) {
            if (hidden) return;
            var overlay = document.getElementById('kaxGlobalLoader');
            if (!overlay) return;

            var elapsed = Date.now() - (window._pageLoadStartTime || Date.now());
            if (!force && elapsed < MIN_DURATION) {
                setTimeout(function () { hide(false); }, MIN_DURATION - elapsed);
                return;
            }

            hidden = true;
            overlay.classList.add('is-leaving');
            overlay.classList.remove('is-visible');

            setLoadingClass('skeleton-loading', false);
            setLoadingClass('skeleton-done', true);

            setTimeout(function () {
                try { overlay.remove(); } catch (_) { }
                setTimeout(function () { setLoadingClass('skeleton-done', false); }, 500);
            }, LEAVE_DURATION);
        }

        window.GlobalLoader = {
            show: show,
            hide: hide,
            isVisible: function () {
                var overlay = document.getElementById('kaxGlobalLoader');
                return !!(overlay && !overlay.classList.contains('is-leaving'));
            },
            trackPromise: function (promise) {
                show();
                return Promise.resolve(promise).finally(function () { hide(false); });
            }
        };

        // 兼容旧调用名称
        window.finishSkeletonLoading = function () { hide(false); };

        var bootstrapped = false;
        function bootstrapLoader() {
            if (bootstrapped) return;
            bootstrapped = true;
            if (!show()) return;
            // 与旧行为兼容：页面进入后最少显示 MIN_DURATION 再淡出
            setTimeout(function () { hide(false); }, MIN_DURATION);
        }

        // 立即启动：刷新瞬间显示（即使 body 尚未创建也先挂到 html）
        bootstrapLoader();
    })();

})();
