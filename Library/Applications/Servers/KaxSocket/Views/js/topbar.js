/**
 * topbar.js — 顶部导航栏模块
 * 包含：topbar HTML 结构注入 + 全部交互行为（token 校验、用户菜单、CDK 面板、注销等）
 * 无需外部 topbar.html 文件，HTML 已内联于此模块中。
 */
(function () {
    'use strict';

    // #region HTML 模板

    /** 顶部导航栏完整 HTML 字符串（原 topbar.html 内容） */
    var TOPBAR_HTML = [
        '<header class="topbar" role="banner" aria-hidden="false">',
        '  <div class="topbar-inner">',
        '    <div class="topbar-left">',
        '      <a class="topbar-logo" href="/">',
        '        <svg class="topbar-logo-svg" viewBox="200 200 850 850" aria-hidden="true">',
        '          <path fill="currentColor" d="M842.223,392.426C747.728,264.592,578.111,220.273,440.649,280.587,288.272,347.445,208.43,528.211,267.543,694.007l36.648-35.48c-35.041-137.6,32.327-276.622,151.273-330.89,98.677-45.021,221.578-26.327,309.563,52.448l-198.058,204.4,70.958,151.176h95.91L662.879,580.625Zm17.155,33.167C947.655,590.739,878.2,795.312,715.9,872.95,562.363,946.4,368.689,885.667,283.918,730.259L560.731,448.732h88.892L527.2,575.226l74.857,158.889-96.69.771L465.6,641.558,356.435,752.626c88.7,103.687,239.838,128.988,354.789,62.476,120.955-69.986,170.954-223.713,111.505-354.029ZM375.149,448.732H467.94l14.815,30.852-61.6,61.7Z"/>',
        '        </svg>',
        '        <span class="topbar-logo-text">KaxHub</span>',
        '      </a>',
        '      <div class="topbar-divider" aria-hidden="true"></div>',
        '      <nav class="topnav" role="navigation" aria-label="\u4e3b\u5bfc\u822a">',
        '        <a href="/explore" class="topnav-link" aria-label="\u6d4f\u89c8">',
        '          <span class="material-icons" aria-hidden="true">explore</span>',
        '          <span class="topnav-label">\u6d4f\u89c8</span>',
        '        </a>',
        '        <a href="/asset" class="topnav-link" aria-label="\u8d44\u6e90">',
        '          <span class="material-icons" aria-hidden="true">folder</span>',
        '          <span class="topnav-label">\u8d44\u6e90</span>',
        '        </a>',
        '        <a id="cdkBtn" href="/cdk" class="topnav-link" aria-label="CDK" aria-expanded="false" data-dropdown="cdkPanel">',
        '          <span class="material-icons" aria-hidden="true">redeem</span>',
        '          <span class="topnav-label">CDK</span>',
        '        </a>',
        '      </nav>',
        '    </div>',
        '    <div class="top-actions">',
        '      <div id="headerAuth" class="top-auth">',
        '        <a href="/login" class="topbar-auth-btn">\u767b\u5f55</a>',
        '        <a href="/register" class="topbar-auth-btn primary">\u6ce8\u518c</a>',
        '      </div>',
        '    </div>',
        '    <div id="userMenu" class="user-menu" role="menu" aria-hidden="true">',
        '      <div class="user-menu-card" role="presentation">',
        '        <div class="user-menu-header">',
        '          <img id="userMenuAvatar" class="menu-avatar" src="https://i.pravatar.cc/40?u=anon" alt="avatar">',
        '          <div class="user-menu-info">',
        '            <div id="userMenuName" class="user-menu-name">\u672a\u767b\u5f55</div>',
        '            <div id="userMenuSignature" class="user-menu-bio" style="display:none;"></div>',
        '          </div>',
        '        </div>',
        '        <div class="user-menu-section">',
        '          <a role="menuitem" tabindex="0" class="user-menu-item" href="/index">',
        '            <span class="material-icons" aria-hidden="true">favorite</span>',
        '            <span>\u8d5e\u52a9\u5546</span>',
        '          </a>',
        '        </div>',
        '        <div class="separator"></div>',
        '        <div id="adminMenuSection" class="user-menu-section" style="display:none;" aria-hidden="true">',
        '          <div class="user-menu-section-label">\u63a7\u5236\u53f0</div>',
        '          <a role="menuitem" tabindex="0" class="user-menu-item" href="/console">',
        '            <span class="material-icons" aria-hidden="true">terminal</span>',
        '            <span>\u547d\u4ee4\u63a7\u5236\u53f0</span>',
        '          </a>',
        '        </div>',
        '        <div id="adminMenuSeparator" class="separator" style="display:none;"></div>',
        '        <div class="user-menu-section">',
        '          <a role="menuitem" tabindex="0" class="user-menu-item" href="/profile">',
        '            <span class="material-icons" aria-hidden="true">person</span>',
        '            <span>\u7b80\u4ecb</span>',
        '          </a>',
        '          <a role="menuitem" tabindex="0" id="signOutBtn" class="user-menu-item" href="#">',
        '            <span class="material-icons" aria-hidden="true">logout</span>',
        '            <span>\u6ce8\u9500</span>',
        '          </a>',
        '        </div>',
        '      </div>',
        '    </div>',
        '    <style>',
        '    .cdk-panel{position:fixed;min-width:280px;width:340px;max-width:calc(100vw - 32px);background:rgba(18,18,20,0.96);border:1px solid rgba(255,255,255,0.06);border-radius:14px;box-shadow:0 20px 60px rgba(0,0,0,0.55),0 0 0 1px rgba(255,255,255,0.03) inset;padding:16px;z-index:1500;transform-origin:top center;transform:translateY(-8px) scale(0.97);opacity:0;pointer-events:none;transition:opacity .22s ease,transform .22s cubic-bezier(.22,.9,.22,1);-webkit-backdrop-filter:blur(24px) saturate(1.4);backdrop-filter:blur(24px) saturate(1.4);}',
        '    .cdk-panel.open{transform:translateY(0) scale(1);opacity:1;pointer-events:auto;}',
        '    .cdk-panel .card{background:transparent;border:none;padding:0;}',
        '    .cdk-panel .card-body{padding:4px 0;}',
        '    .cdk-panel .card-actions{padding-top:12px;}',
        '    .cdk-panel .field{margin:0;}',
        '    .cdk-panel .label{font-size:0.75rem;font-weight:600;letter-spacing:0.06em;text-transform:uppercase;color:rgba(255,255,255,0.45);margin-bottom:6px;}',
        '    .cdk-panel .label-divider{height:1px;background:linear-gradient(90deg,rgba(255,255,255,0.06),transparent);margin:6px 0 10px;border-radius:1px;}',
        '    .cdk-panel input{width:100%;box-sizing:border-box;border:1px solid rgba(255,255,255,0.06);border-radius:8px;background:rgba(255,255,255,0.03);color:var(--muted-strong,rgba(255,255,255,0.92));font-size:0.92rem;padding:10px 12px;outline:none;transition:border-color .2s ease,background .2s ease;}',
        '    .cdk-panel input:focus{border-color:rgba(99,140,255,0.35);background:rgba(255,255,255,0.04);}',
        '    .cdk-panel input::placeholder{color:rgba(255,255,255,0.25);}',
        '    .cdk-panel .btn.block{display:block;width:100%;border-radius:8px;}',
        '    @media(max-width:520px){.cdk-panel{width:calc(100vw - 24px);left:12px !important;right:12px !important;}}',
        '    .user-menu{position:fixed;min-width:260px;width:280px;max-width:calc(100vw - 32px);background:rgba(18,18,20,0.96);border:1px solid rgba(255,255,255,0.06);border-radius:14px;box-shadow:0 20px 60px rgba(0,0,0,0.55),0 0 0 1px rgba(255,255,255,0.03) inset;padding:8px;z-index:1500;transform-origin:top right;transform:translateY(-8px) scale(0.97);opacity:0;pointer-events:none;transition:opacity .22s ease,transform .22s cubic-bezier(.22,.9,.22,1);-webkit-backdrop-filter:blur(24px) saturate(1.4);backdrop-filter:blur(24px) saturate(1.4);}',
        '    .user-menu.open{opacity:1;transform:translateY(0) scale(1);pointer-events:auto;}',
        '    .user-menu.closing{opacity:0;transform:translateY(-6px) scale(0.98);pointer-events:none;}',
        '    .user-menu-card{display:flex;flex-direction:column;gap:2px;}',
        '    .user-menu-header{display:flex;gap:12px;align-items:center;padding:12px 10px;border-bottom:1px solid rgba(255,255,255,0.04);margin-bottom:4px;}',
        '    .menu-avatar{width:42px;height:42px;border-radius:999px;object-fit:cover;border:2px solid rgba(255,255,255,0.08);box-shadow:0 0 0 3px rgba(99,140,255,0.08);}',
        '    .user-menu-info{display:flex;flex-direction:column;min-width:0;gap:4px;}',
        '    .user-menu-name{font-weight:700;color:#fff;font-size:0.92rem;line-height:1;}',
        '    .user-menu-bio{color:rgba(255,255,255,0.5);font-size:0.78rem;line-height:1.3;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;max-width:180px;}',
        '    .user-menu-section{display:flex;flex-direction:column;padding:4px;gap:1px;}',
        '    .user-menu-item{display:flex;gap:10px;align-items:center;padding:9px 10px;border-radius:8px;color:rgba(255,255,255,0.88);text-decoration:none;font-size:0.88rem;transition:background .14s ease,color .14s ease;}',
        '    .user-menu-item .material-icons{font-size:17px;color:rgba(255,255,255,0.45);transition:color .14s ease;}',
        '    .user-menu-item:hover,.user-menu-item:focus{background:rgba(255,255,255,0.05);color:#fff;outline:none;}',
        '    .user-menu-item:hover .material-icons{color:rgba(255,255,255,0.7);}',
        '    .separator{height:1px;background:rgba(255,255,255,0.04);margin:4px 10px;border-radius:2px;}',
        '    .user-menu-section-label{font-size:0.68rem;font-weight:700;letter-spacing:0.08em;text-transform:uppercase;color:rgba(255,255,255,0.25);padding:6px 10px 4px;}',
        '    #signOutBtn{color:rgba(255,100,100,0.9);font-weight:600;}',
        '    #signOutBtn .material-icons{color:rgba(255,100,100,0.5);}',
        '    #signOutBtn:hover{background:rgba(255,80,80,0.08);color:#ff6b6b;}',
        '    #signOutBtn:hover .material-icons{color:rgba(255,100,100,0.7);}',
        '    @media(max-width:768px){.user-menu{right:8px;left:8px;width:auto;}}',
        '    .topbar-left{display:flex;align-items:center;gap:0;flex-shrink:0;}',
        '    .topbar-logo-svg{width:26px;height:26px;color:#738fff;flex-shrink:0;filter:drop-shadow(0 1px 6px rgba(99,140,255,0.3));transition:color .2s ease;}',
        '    .topbar-logo:hover .topbar-logo-svg{color:#93b0ff;}',
        '    .topbar-logo-text{font-weight:700;font-size:1rem;letter-spacing:-0.03em;}',
        '    .topbar-divider{width:1px;height:20px;background:rgba(255,255,255,0.08);margin:0 12px;border-radius:1px;flex-shrink:0;}',
        '    .topnav-link{display:inline-flex;align-items:center;gap:6px;padding:7px 12px;border-radius:8px;color:rgba(255,255,255,0.6);text-decoration:none;font-size:0.85rem;font-weight:500;transition:color .16s ease,background .16s ease;position:relative;}',
        '    .topnav-link .material-icons{font-size:17px;opacity:0.7;transition:opacity .16s ease;}',
        '    .topnav-link:hover{color:rgba(255,255,255,0.95);background:rgba(255,255,255,0.04);}',
        '    .topnav-link:hover .material-icons{opacity:1;}',
        '    .topnav-link.active{color:#fff;background:rgba(255,255,255,0.06);}',
        '    .topbar-auth-btn{display:inline-flex;align-items:center;padding:6px 16px;border-radius:8px;font-size:0.85rem;font-weight:600;text-decoration:none;transition:all .16s ease;color:rgba(255,255,255,0.7);border:1px solid rgba(255,255,255,0.08);background:transparent;}',
        '    .topbar-auth-btn:hover{color:#fff;border-color:rgba(255,255,255,0.16);background:rgba(255,255,255,0.04);}',
        '    .topbar-auth-btn.primary{background:linear-gradient(135deg,#638cff 0%,#5a5fff 100%);color:#fff;border-color:transparent;box-shadow:0 2px 12px rgba(99,140,255,0.2);}',
        '    .topbar-auth-btn.primary:hover{box-shadow:0 4px 20px rgba(99,140,255,0.35);transform:translateY(-1px);}',
        '    @media(max-width:640px){.topnav-label{display:none;}.topnav-link{padding:7px 8px;}.topbar-auth-btn{padding:6px 10px;font-size:0.82rem;}}',
        '    </style>',
        '    <div id="cdkPanel" class="cdk-panel" role="dialog" aria-hidden="true" aria-label="\u6fc0\u6d3b CDK">',
        '      <div class="card">',
        '        <div class="card-body">',
        '          <div class="field">',
        '            <label class="label">CDK</label>',
        '            <div class="label-divider" aria-hidden="true"></div>',
        '            <input id="cdkInput" title="CDK" placeholder="\u8bf7\u8f93\u5165 CDK \u7801" />',
        '          </div>',
        '        </div>',
        '        <div class="card-actions">',
        '          <button id="activateCdkBtn" class="btn block">\u6fc0\u6d3b CDK</button>',
        '        </div>',
        '      </div>',
        '    </div>',
        '  </div>',
        '</header>',
        '<script>',
        '(function() {',
        '  var userMenuSignature = document.getElementById("userMenuSignature");',
        '  if (!userMenuSignature) return;',
        '  var userMenuEl = document.getElementById("userMenu");',
        '  if (!userMenuEl) return;',
        '  var observer = new MutationObserver(function(mutations) {',
        '    mutations.forEach(function(mutation) {',
        '      if (mutation.attributeName === "class" || mutation.attributeName === "aria-hidden") {',
        '        if (userMenuEl.classList.contains("open")) updateSignatureDisplay();',
        '      }',
        '    });',
        '  });',
        '  observer.observe(userMenuEl, { attributes: true });',
        '  async function updateSignatureDisplay() {',
        '    var token = localStorage.getItem("kax_login_token");',
        '    if (!token) return;',
        '    try {',
        '      var resp = await fetch("/api/user/profile", { headers: { "Authorization": "Bearer " + token } });',
        '      if (resp.status === 200) {',
        '        var data = await resp.json();',
        '        var signature = data.signature || "";',
        '        if (signature) { userMenuSignature.textContent = signature; userMenuSignature.style.display = "block"; }',
        '        else { userMenuSignature.style.display = "none"; }',
        '      }',
        '    } catch(err) { console.error("\u83b7\u53d6\u4e2a\u6027\u7b7e\u540d\u5931\u8d25:", err); }',
        '  }',
        '})();',
        '(function() {',
        '  var path = window.location.pathname;',
        '  document.querySelectorAll(".topnav-link").forEach(function(link) {',
        '    var href = link.getAttribute("href");',
        '    if (href && path.startsWith(href) && href !== "/") link.classList.add("active");',
        '  });',
        '})();',
        '<\/script>'
    ].join('\n');

    // #endregion

    // #region DOM 注入

    /** 将 topbar HTML 注入到 body 首位（若尚未存在） */
    function insertTopbarHtml() {
        if (document.querySelector('.topbar')) return false;
        var tmp = document.createElement('div');
        tmp.innerHTML = TOPBAR_HTML;
        var header = tmp.querySelector('.topbar');
        if (!header) return false;
        // 将内联 script 提取并执行（innerHTML 不自动执行 script）
        var scripts = tmp.querySelectorAll('script');
        document.body.insertBefore(header, document.body.firstChild);
        document.body.classList.add('has-global-topbar');
        scripts.forEach(function (s) {
            var el = document.createElement('script');
            el.textContent = s.textContent;
            document.body.appendChild(el);
        });
        return true;
    }

    // #endregion

    // #region 行为初始化

    /** 初始化 topbar 所有交互行为（仅执行一次） */
    function initTopbarBehavior() {
        if (window._kaxTopbarInit) return;
        window._kaxTopbarInit = true;

        // 顶栏滚动样式
        (function () {
            var topbar = document.querySelector('.topbar');
            if (!topbar) return;
            function updateTopbar() {
                if (window.scrollY > 6) topbar.classList.add('topbar--scrolled');
                else topbar.classList.remove('topbar--scrolled');
            }
            var ticking = false;
            window.addEventListener('scroll', function () {
                if (!ticking) { window.requestAnimationFrame(function () { updateTopbar(); ticking = false; }); ticking = true; }
            }, { passive: true });
            document.addEventListener('DOMContentLoaded', updateTopbar);
            updateTopbar();
        })();

        // token 校验 与 header 状态
        (function () {
            var joinBtn = document.getElementById('joinBtn');
            var originalText = joinBtn ? (joinBtn.textContent || '') : '';
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
                    header.innerHTML = '<a href="/login" class="topbar-auth-btn">\u767b\u5f55</a><a href="/register" class="topbar-auth-btn primary">\u6ce8\u518c</a>';
                    try { var um = document.getElementById('userMenu'); if (um) { um.classList.remove('open'); um.setAttribute('aria-hidden', 'true'); } } catch (_) { }
                }

                try {
                    var token = localStorage.getItem('kax_login_token');
                    if (!token) { resetHeaderAuth(); return; }

                    var controller = new AbortController();
                    var timeoutId = setTimeout(function () { controller.abort(); }, 6000);
                    var resp = await fetch('/api/user/verify/account', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                        signal: controller.signal
                    });
                    clearTimeout(timeoutId);

                    try {
                        if (!(resp.status >= 200 && resp.status < 300)) {
                            try { localStorage.removeItem('kax_login_token'); } catch (_) { }
                            try { setBtnDisabled(joinBtn, false); } catch (_) { }
                            try { var um = document.getElementById('userMenu'); if (um) { um.classList.remove('open'); um.setAttribute('aria-hidden', 'true'); } } catch (_) { }
                            resetHeaderAuth();
                        }
                    } catch (_) { }

                    if (resp.status === 200) {
                        try {
                            setBtnDisabled(joinBtn, true);
                            if (joinBtn) { joinBtn.textContent = '\u4f60\u5df2\u53c2\u52a0\u6d4b\u8bd5'; joinBtn.removeAttribute('href'); }

                            try {
                                var j = await resp.json();
                                if (j && j.isAdmin) {
                                    var adminDiv = document.getElementById('adminLinks');
                                    if (adminDiv) adminDiv.classList.remove('hidden');
                                    var adminSection = document.getElementById('adminMenuSection');
                                    var adminSep = document.getElementById('adminMenuSeparator');
                                    if (adminSection) { adminSection.style.display = ''; adminSection.setAttribute('aria-hidden', 'false'); }
                                    if (adminSep) { adminSep.style.display = ''; }
                                }

                                try {
                                    var headerEl = document.getElementById('headerAuth');
                                    if (headerEl) {
                                        var seed = (j && j.user) ? encodeURIComponent(j.user) : (Math.random().toString(36).slice(2, 8));
                                        var avatarUrl = (j && j.avatarUrl) ? j.avatarUrl : ('https://i.pravatar.cc/40?u=' + seed);
                                        var userName = (j && j.user ? j.user : '\u5df2\u767b\u5f55');

                                        headerEl.innerHTML = '<img class="avatar-image" role="button" tabindex="0" aria-haspopup="true" aria-expanded="false" src="' + avatarUrl + '" alt="avatar" title="' + userName + '">';

                                        if (window.AvatarCache && j && j.avatarUrl) {
                                            AvatarCache.getAvatar(avatarUrl).then(function (cachedUrl) {
                                                var avatarImg = headerEl.querySelector('.avatar-image');
                                                if (avatarImg) avatarImg.src = cachedUrl;
                                                var uma = document.querySelector('#userMenuAvatar');
                                                if (uma) uma.src = cachedUrl;
                                            }).catch(function (e) {
                                                console.warn('[Topbar] AvatarCache \u83b7\u53d6\u5931\u8d25:', e);
                                            });
                                        }

                                        try {
                                            var userMenu = document.getElementById('userMenu');
                                            if (userMenu) {
                                                var uma = userMenu.querySelector('#userMenuAvatar'); if (uma) uma.src = avatarUrl;
                                                var unameEl = userMenu.querySelector('#userMenuName'); if (unameEl) unameEl.textContent = userName;
                                                var ubioEl = userMenu.querySelector('#userMenuBio'); if (ubioEl) ubioEl.textContent = (j && j.bio) ? j.bio : '\u6572\u6572\u2026\u2026';
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
                        console.warn('token \u9a8c\u8bc1\u8fd4\u56de\u72b6\u6001\uff1a', resp.status);
                        try { var um = document.getElementById('userMenu'); if (um) { um.classList.remove('open'); um.setAttribute('aria-hidden', 'true'); } } catch (_) { }
                        resetHeaderAuth();
                    }
                } catch (err) {
                    if (err && err.name === 'AbortError') { console.warn('token \u9a8c\u8bc1\u8d85\u65f6\uff0c\u8df3\u8fc7\u81ea\u52a8\u7981\u7528\u3002'); }
                    else { console.error('\u52a0\u8f7d\u65f6\u9a8c\u8bc1 token \u53d1\u751f\u9519\u8bef\uff1a', err); }
                    try { var um = document.getElementById('userMenu'); if (um) { um.classList.remove('open'); um.setAttribute('aria-hidden', 'true'); } } catch (_) { }
                    resetHeaderAuth();
                }
            }

            if (document.readyState === 'complete' || document.readyState === 'interactive') { checkTokenOnLoad(); }
            else { document.addEventListener('DOMContentLoaded', checkTokenOnLoad); }

            // Avatar / 用户菜单行为（事件委托）
            document.addEventListener('click', function (e) {
                var avatar = e.target.closest('.avatar-image');
                var menu = document.getElementById('userMenu');
                if (avatar) {
                    if (!menu) return;
                    var opening = !menu.classList.contains('open');
                    if (opening) {
                        menu.classList.add('open'); menu.classList.remove('closing');
                        avatar.setAttribute('aria-expanded', 'true'); menu.setAttribute('aria-hidden', 'false');
                        var rect = avatar.getBoundingClientRect();
                        menu.style.left = rect.left + 'px'; menu.style.top = (rect.bottom + 8) + 'px';
                        requestAnimationFrame(function () {
                            var mrect = menu.getBoundingClientRect();
                            if (mrect.right > window.innerWidth - 8) { menu.style.left = Math.max(8, window.innerWidth - mrect.width - 8) + 'px'; }
                            var first = menu.querySelector('[role="menuitem"]'); if (first) first.focus();
                        });
                    } else {
                        menu.classList.remove('open'); menu.classList.add('closing');
                        avatar.setAttribute('aria-expanded', 'false'); menu.setAttribute('aria-hidden', 'true');
                        setTimeout(function () { menu.classList.remove('closing'); menu.style.left = ''; menu.style.top = ''; }, 220);
                    }
                    e.stopPropagation();
                    return;
                }

                if (menu && !e.target.closest('#userMenu')) {
                    if (menu.classList.contains('open')) {
                        menu.classList.remove('open'); menu.classList.add('closing'); menu.setAttribute('aria-hidden', 'true');
                        document.querySelectorAll('.avatar-image').forEach(function (a) { a.setAttribute('aria-expanded', 'false'); });
                        setTimeout(function () { menu.classList.remove('closing'); menu.style.left = ''; menu.style.top = ''; }, 220);
                    }
                }
            }, { passive: true });

            // 键盘支持（Esc / Enter on avatar）
            document.addEventListener('keydown', function (e) {
                var menu = document.getElementById('userMenu');
                if (e.key === 'Escape') {
                    if (menu && menu.classList.contains('open')) {
                        menu.classList.remove('open'); menu.classList.add('closing'); menu.setAttribute('aria-hidden', 'true');
                        document.querySelectorAll('.avatar-image').forEach(function (a) { a.setAttribute('aria-expanded', 'false'); });
                        setTimeout(function () { menu.classList.remove('closing'); menu.style.left = ''; menu.style.top = ''; }, 220);
                    }
                    return;
                }
                var active = document.activeElement;
                if (active && active.classList && active.classList.contains('avatar-image') && (e.key === 'Enter' || e.key === ' ')) {
                    e.preventDefault();
                    if (!menu) return;
                    var isOpen = menu.classList.toggle('open');
                    active.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
                    menu.setAttribute('aria-hidden', isOpen ? 'false' : 'true');
                    if (isOpen) { var first = menu.querySelector('[role="menuitem"]'); if (first) first.focus(); }
                }
            });

            // CDK 下拉面板：定位 / 打开 / 关闭 / 激活
            (function () {
                var cdkBtn = document.getElementById('cdkBtn');
                var panel = document.getElementById('cdkPanel');
                if (!cdkBtn || !panel) return;

                function positionPanel() {
                    var rect = cdkBtn.getBoundingClientRect();
                    panel.style.left = Math.max(8, Math.min(rect.left, window.innerWidth - panel.offsetWidth - 8)) + 'px';
                    panel.style.top = (rect.bottom + 8) + 'px';
                    var pr = panel.getBoundingClientRect();
                    if (pr.right > window.innerWidth - 8) {
                        panel.style.left = Math.max(8, window.innerWidth - pr.width - 8) + 'px';
                    }
                }

                function openPanel() {
                    panel.classList.add('open'); panel.setAttribute('aria-hidden', 'false'); cdkBtn.setAttribute('aria-expanded', 'true');
                    requestAnimationFrame(positionPanel);
                    var inp = panel.querySelector('input');
                    if (inp) setTimeout(function () { try { inp.focus(); inp.select && inp.select(); } catch (_) { } }, 80);
                }
                function closePanel() {
                    panel.classList.remove('open'); panel.setAttribute('aria-hidden', 'true'); cdkBtn.setAttribute('aria-expanded', 'false');
                    setTimeout(function () { panel.style.left = ''; panel.style.top = ''; }, 220);
                }
                function togglePanel() { panel.classList.contains('open') ? closePanel() : openPanel(); }

                cdkBtn.addEventListener('click', function (e) {
                    if (e.ctrlKey || e.metaKey || e.button === 1) return;
                    e.preventDefault(); e.stopPropagation(); togglePanel();
                }, false);

                document.addEventListener('click', function (ev) {
                    if (!panel.classList.contains('open')) return;
                    if (panel.contains(ev.target) || cdkBtn.contains(ev.target)) return;
                    closePanel();
                }, { passive: true });

                document.addEventListener('keydown', function (ev) {
                    if (ev.key === 'Escape' && panel.classList.contains('open')) { closePanel(); cdkBtn.focus(); }
                });

                panel.addEventListener('click', function (ev) { ev.stopPropagation(); });

                var activateBtn = document.getElementById('activateCdkBtn');
                activateBtn && activateBtn.addEventListener('click', function () {
                    var input = document.getElementById('cdkInput');
                    var val = input ? input.value.trim() : '';
                    if (!val) { input && input.focus(); return; }
                    window.activateCdkFromTopbar(val, activateBtn, panel);
                }, false);

                var cdkInput = panel.querySelector('input[placeholder*="CDK"]');
                if (cdkInput) {
                    cdkInput.addEventListener('keypress', function (e) {
                        if (e.key === 'Enter') { e.preventDefault(); activateBtn && activateBtn.click(); }
                    });
                }

                window.addEventListener('resize', function () { if (panel.classList.contains('open')) positionPanel(); });
                window.addEventListener('scroll', function () { if (panel.classList.contains('open')) positionPanel(); }, { passive: true });
            })();

            // 注销按钮
            document.addEventListener('click', function (e) {
                var s = e.target.closest('#signOutBtn');
                if (s) { e.preventDefault(); try { localStorage.removeItem('kax_login_token'); } catch (_) { } location.reload(); }
            });

            // joinBtn 点击验证（保持与 index.html 语义一致）
            if (joinBtn) {
                joinBtn.addEventListener('click', async function (e) {
                    try {
                        if (joinBtn.classList.contains('kax-disabled') || joinBtn.getAttribute('aria-disabled') === 'true') { e.preventDefault(); return; }
                        var token = localStorage.getItem('kax_login_token');
                        if (!token) return;

                        e.preventDefault(); joinBtn.textContent = '\u9a8c\u8bc1\u4e2d...'; setBtnDisabled(joinBtn, true);
                        var controller = new AbortController(); var timeoutId = setTimeout(function () { controller.abort(); }, 8000);
                        var resp = await fetch('/api/token/test', { method: 'POST', headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token }, signal: controller.signal });
                        clearTimeout(timeoutId);

                        try { if (!(resp.status >= 200 && resp.status < 300)) { try { localStorage.removeItem('kax_login_token'); } catch (_) { } try { setBtnDisabled(joinBtn, false); } catch (_) { } } } catch (_) { }

                        if (resp.status === 200) {
                            setBtnDisabled(joinBtn, true); joinBtn.textContent = '\u4f60\u5df2\u53c2\u52a0\u6d4b\u8bd5'; joinBtn.removeAttribute('href');
                            try { await checkTokenOnLoad(); } catch (_) { }
                            var features = document.getElementById('features'); if (features) features.scrollIntoView({ behavior: 'smooth' });
                            return;
                        }

                        if (resp.status === 429) {
                            try {
                                var retrySeconds = 60; var ra = resp.headers.get('Retry-After');
                                if (ra) { var n = parseInt(ra, 10); if (!isNaN(n)) retrySeconds = n; else { var t = Date.parse(ra); if (!isNaN(t)) retrySeconds = Math.max(1, Math.ceil((t - Date.now()) / 1000)); } }
                                alert('\u64cd\u4f5c\u8fc7\u4e8e\u9891\u7e41\uff0c\u8bf7\u5728 ' + retrySeconds + ' \u79d2\u540e\u91cd\u8bd5\u3002'); keepDisabled = true; setBtnDisabled(joinBtn, true);
                                var remaining = retrySeconds; joinBtn.textContent = '\u8bf7\u7a0d\u540e\u91cd\u8bd5 (' + remaining + 's)';
                                var intervalId = setInterval(function () {
                                    remaining--;
                                    if (remaining <= 0) { clearInterval(intervalId); try { setBtnDisabled(joinBtn, false); joinBtn.textContent = originalText; } catch (_) { } keepDisabled = false; }
                                    else { try { joinBtn.textContent = '\u8bf7\u7a0d\u540e\u91cd\u8bd5 (' + remaining + 's)'; } catch (_) { } }
                                }, 1000);
                            } catch (err429) { console.error('\u5904\u7406 429 \u65f6\u51fa\u9519\uff1a', err429); }
                            return;
                        }

                        if (resp.status === 401) { localStorage.removeItem('kax_login_token'); location.href = 'login'; return; }
                        if (resp.status === 403) { try { var jf = await resp.json(); if (jf && jf.message) alert(jf.message); } catch (_) { alert('\u60a8\u7684\u8d26\u53f7\u65e0\u6cd5\u8bbf\u95ee\u6b64\u8d44\u6e90\u3002'); } localStorage.removeItem('kax_login_token'); location.href = 'login'; return; }

                        try { var txt = await resp.text(); if (txt) console.warn('token test \u8fd4\u56de\uff1a', txt); } catch (_) { }
                        localStorage.removeItem('kax_login_token'); location.href = 'login';

                    } catch (err) {
                        console.error('\u9a8c\u8bc1\u767b\u5f55\u4ee4\u724c\u65f6\u51fa\u9519', err);
                        try { localStorage.removeItem('kax_login_token'); } catch (_) { }
                        alert('\u65e0\u6cd5\u9a8c\u8bc1\u767b\u5f55\u72b6\u6001\uff0c\u8bf7\u91cd\u65b0\u767b\u5f55\u3002'); location.href = 'login';
                    } finally {
                        try { if (!keepDisabled) { setBtnDisabled(joinBtn, false); joinBtn.textContent = originalText; } } catch (_) { }
                    }
                });
            }

            // 菜单内 Tab 循环（accessibility）
            (function () {
                var menu = document.getElementById('userMenu');
                if (!menu) return;
                menu.addEventListener('keydown', function (e) {
                    if (e.key !== 'Tab') return;
                    var items = Array.from(menu.querySelectorAll('[role="menuitem"]'));
                    if (items.length === 0) return;
                    var idx = items.indexOf(document.activeElement);
                    if (e.shiftKey) { if (idx === 0) { items[items.length - 1].focus(); e.preventDefault(); } }
                    else { if (idx === items.length - 1) { items[0].focus(); e.preventDefault(); } }
                });
            })();
        })();
    }

    // #endregion

    // #region 公开 API

    /**
     * 初始化全局 topbar：若页面已有 .topbar 元素则直接初始化行为；否则注入 HTML 后再初始化。
     * @returns {Promise<boolean>}
     */
    window.initGlobalTopbar = function () {
        if (document.querySelector('.topbar')) {
            initTopbarBehavior();
            return Promise.resolve(true);
        }
        var inserted = insertTopbarHtml();
        if (inserted) initTopbarBehavior();
        return Promise.resolve(inserted);
    };

    /**
     * 全局 CDK 激活函数，可从页面任意位置调用。
     * @param {string} cdkCode
     * @param {HTMLElement} activateBtn
     * @param {HTMLElement} panelElement
     */
    window.activateCdkFromTopbar = async function (cdkCode, activateBtn, panelElement) {
        if (!cdkCode || cdkCode.trim().length === 0) {
            if (activateBtn) activateBtn.textContent = 'CDK\u4e3a\u7a7a';
            setTimeout(function () { if (activateBtn) activateBtn.textContent = '\u6fc0\u6d3b'; }, 1800);
            return;
        }

        var token = localStorage.getItem('kax_login_token');
        if (!token) { alert('\u8bf7\u5148\u767b\u5f55'); location.href = '/login'; return; }

        if (activateBtn) { activateBtn.setAttribute('disabled', 'true'); activateBtn.textContent = '\u6fc0\u6d3b\u4e2d\u2026'; }

        try {
            var resp = await fetch('/api/cdk/activate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                body: JSON.stringify({ code: cdkCode })
            });

            var result = await resp.json().catch(function () { return { code: 500, message: '\u65e0\u6548\u7684\u54cd\u5e94\u683c\u5f0f' }; });

            if (resp.status === 200) {
                if (activateBtn) { activateBtn.textContent = '\u2713 \u6fc0\u6d3b\u6210\u529f'; activateBtn.classList.add('kax-disabled'); }
                var details = [];
                if (result.assetId > 0) details.push('\u8d44\u6e90 #' + result.assetId);
                if (result.goldValue > 0) details.push('+' + result.goldValue + ' \u91d1\u5e01');
                if (result.description) details.push(result.description);
                console.log('[CDK \u6fc0\u6d3b\u6210\u529f]', details.length > 0 ? details.join(' \u2022 ') : '\u8d44\u6e90\u5df2\u6dfb\u52a0\u81f3\u60a8\u7684\u5e93\u4e2d');
                setTimeout(function () {
                    if (activateBtn) { activateBtn.removeAttribute('disabled'); activateBtn.classList.remove('kax-disabled'); activateBtn.textContent = '\u6fc0\u6d3b'; }
                    if (panelElement && panelElement.classList) panelElement.classList.remove('open');
                    var input = document.getElementById('cdkInput'); if (input) input.value = '';
                }, 1500);
            } else if (resp.status === 401) {
                localStorage.removeItem('kax_login_token'); location.href = '/login';
            } else {
                var errorMsg = result.message || '\u6fc0\u6d3b\u5931\u8d25';
                if (result.code === 1) errorMsg = 'CDK\u4e3a\u7a7a';
                else if (result.code === 2) errorMsg = 'CDK\u9519\u8bef';
                else if (result.code === 3) errorMsg = 'CDK\u5df2\u4f7f\u7528';
                if (activateBtn) activateBtn.textContent = errorMsg;
                console.warn('[CDK \u6fc0\u6d3b\u5931\u8d25]', errorMsg);
                setTimeout(function () { if (activateBtn) { activateBtn.removeAttribute('disabled'); activateBtn.textContent = '\u6fc0\u6d3b'; } }, 2000);
            }
        } catch (err) {
            console.error('[CDK \u6fc0\u6d3b\u5f02\u5e38]', err);
            if (activateBtn) {
                activateBtn.textContent = '\u7f51\u7edc\u9519\u8bef';
                setTimeout(function () { activateBtn.removeAttribute('disabled'); activateBtn.textContent = '\u6fc0\u6d3b'; }, 2000);
            }
        }
    };

    // #endregion

    // 页面加载时自动注入并初始化 topbar
    if (document.readyState === 'complete' || document.readyState === 'interactive') {
        window.initGlobalTopbar();
    } else {
        document.addEventListener('DOMContentLoaded', function () { window.initGlobalTopbar(); });
    }

})();
