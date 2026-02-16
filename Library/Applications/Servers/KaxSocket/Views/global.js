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
})();
