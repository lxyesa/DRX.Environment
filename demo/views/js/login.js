// 简单客户端验证与交互（界面演示）
        (function () {

            function getRedirectTarget() {
                try {
                    var u = new URL(window.location.href);
                    var target = (u.searchParams.get('redirect') || '').trim();
                    // 仅允许站内相对路径，避免开放重定向
                    if (!target) return '/';
                    if (!target.startsWith('/')) return '/';
                    if (target.startsWith('//')) return '/';
                    return target;
                } catch {
                    return '/';
                }
            }

            var postLoginRedirect = getRedirectTarget();

            // ── 封禁弹窗（动态创建 DOM，无需额外 CSS 文件） ──
            function showBanMsgBox(reason, expires) {
                if (!document.getElementById('kax-msgbox-style')) {
                    const s = document.createElement('style');
                    s.id = 'kax-msgbox-style';
                    s.textContent = `
                        .kax-ban-overlay {
                            position: fixed; inset: 0;
                            background: rgba(0,0,0,.65);
                            backdrop-filter: blur(20px) saturate(1.3);
                            -webkit-backdrop-filter: blur(20px) saturate(1.3);
                            display: flex; align-items: center; justify-content: center;
                            z-index: 9999;
                            animation: kaxBanIn .16s ease;
                        }
                        .kax-ban-overlay.closing { animation: kaxBanOut .14s ease forwards; }

                        .kax-ban-card {
                            position: relative;
                            width: min(380px, 92vw);
                            background: rgba(12,12,14,.97);
                            border: 1px solid rgba(255,255,255,.08);
                            border-radius: 8px;
                            box-shadow: 0 16px 48px rgba(0,0,0,.5);
                            animation: kaxBanUp .18s ease;
                            font-family: 'Poppins', sans-serif;
                        }
                        .kax-ban-card.closing { animation: kaxBanDown .14s ease forwards; }

                        .kax-ban-close {
                            position: absolute; top: 12px; right: 12px;
                            width: 28px; height: 28px;
                            background: transparent;
                            border: none;
                            border-radius: 6px;
                            display: flex; align-items: center; justify-content: center;
                            cursor: pointer; color: rgba(255,255,255,.35);
                            transition: color .12s ease, background .12s ease;
                        }
                        .kax-ban-close:hover { color: rgba(255,255,255,.8); background: rgba(255,255,255,.04); }
                        .kax-ban-close .material-icons { font-size: 16px; }

                        .kax-ban-head {
                            padding: 20px 20px 16px;
                            display: flex; align-items: center; gap: 10px;
                            border-bottom: 1px solid rgba(255,255,255,.06);
                        }
                        .kax-ban-head .material-icons { font-size: 18px; color: #ef4444; flex-shrink: 0; }
                        .kax-ban-title {
                            margin: 0; font-size: .95rem; font-weight: 700;
                            color: #fff;
                        }

                        .kax-ban-body { padding: 16px 20px; display: flex; flex-direction: column; gap: 8px; }

                        .kax-ban-field {
                            border-radius: 8px;
                            border: 1px solid rgba(255,255,255,.06);
                            padding: 10px 12px;
                            background: rgba(255,255,255,.01);
                        }
                        .kax-ban-field-label {
                            display: block;
                            font-size: .78rem;
                            color: rgba(255,255,255,.45);
                            margin-bottom: 4px;
                        }
                        .kax-ban-field-value {
                            font-size: .9rem;
                            color: rgba(255,255,255,.9);
                            font-weight: 500;
                            line-height: 1.4;
                            word-break: break-word;
                        }

                        .kax-ban-footer { padding: 0 20px 20px; }
                        .kax-ban-btn {
                            display: block; width: 100%;
                            padding: 11px 14px;
                            border-radius: 8px;
                            background: #fff;
                            color: #0f0f0f;
                            font-size: .9rem; font-weight: 700;
                            border: none; cursor: pointer;
                            transition: background .12s ease, transform .12s ease, box-shadow .12s ease;
                            font-family: 'Poppins', sans-serif;
                        }
                        .kax-ban-btn:hover {
                            transform: translateY(-2px);
                            box-shadow: 0 8px 26px rgba(59,130,246,.06);
                        }
                        .kax-ban-btn:active { transform: translateY(0); }

                        @keyframes kaxBanIn  { from { opacity: 0; } to { opacity: 1; } }
                        @keyframes kaxBanOut { from { opacity: 1; } to { opacity: 0; } }
                        @keyframes kaxBanUp  { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
                        @keyframes kaxBanDown{ from { opacity: 1; transform: translateY(0); } to { opacity: 0; transform: translateY(6px); } }
                    `;
                    document.head.appendChild(s);
                }

                const overlay = document.createElement('div');
                overlay.className = 'kax-ban-overlay';

                const card = document.createElement('div');
                card.className = 'kax-ban-card';

                // 关闭按钮
                const closeBtn = document.createElement('button');
                closeBtn.className = 'kax-ban-close';
                closeBtn.innerHTML = '<span class="material-icons">close</span>';

                // 头部
                const head = document.createElement('div');
                head.className = 'kax-ban-head';
                head.innerHTML = '<span class="material-icons">gavel</span>';
                const titleEl = document.createElement('p');
                titleEl.className = 'kax-ban-title';
                titleEl.textContent = '账号已被封禁';
                head.appendChild(titleEl);

                // 字段区
                const body = document.createElement('div');
                body.className = 'kax-ban-body';

                const makeField = (label, value) => {
                    const f = document.createElement('div');
                    f.className = 'kax-ban-field';
                    f.innerHTML = `<span class="kax-ban-field-label">${label}</span>
                                   <span class="kax-ban-field-value">${value}</span>`;
                    return f;
                };
                body.appendChild(makeField('封禁原因', reason));
                body.appendChild(makeField('解封时间', expires));

                // 底部按钮
                const footer = document.createElement('div');
                footer.className = 'kax-ban-footer';
                const confirmBtn = document.createElement('button');
                confirmBtn.className = 'kax-ban-btn';
                confirmBtn.textContent = '我知道了';
                footer.appendChild(confirmBtn);

                card.appendChild(closeBtn);
                card.appendChild(head);
                card.appendChild(body);
                card.appendChild(footer);
                overlay.appendChild(card);
                document.body.appendChild(overlay);

                const dismiss = () => {
                    overlay.classList.add('closing');
                    card.classList.add('closing');
                    setTimeout(() => { if (overlay.parentNode) overlay.parentNode.removeChild(overlay); }, 200);
                };
                confirmBtn.addEventListener('click', dismiss);
                closeBtn.addEventListener('click', dismiss);
                overlay.addEventListener('click', (e) => { if (e.target === overlay) dismiss(); });
            }

            // 页面加载时：若本地存在 token 且能通过验证，则视为已登录并重定向到首页
            (async function checkTokenRedirect() {
                try {
                    var token = localStorage.getItem('kax_web_token') || localStorage.getItem('kax_login_token');
                    if (!token) return; // 无 token，继续显示登录页

                    var controller = new AbortController();
                    var timeoutId = setTimeout(function () { controller.abort(); }, 6000);

                    var resp = await fetch('/api/user/verify/account', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                        signal: controller.signal
                    });
                    clearTimeout(timeoutId);

                    if (resp.status === 200) {
                        // token 有效：跳转到首页
                        window.location.href = postLoginRedirect;
                        return;
                    }

                    if (resp.status === 401) {
                        // token 无效或过期，清理并留在当前页
                        try { localStorage.removeItem('kax_login_token'); } catch (_) { }
                        try { localStorage.removeItem('kax_web_token'); } catch (_) { }
                    }
                    // 其他情况不做处理，保持在登录页以便用户登录
                } catch (err) {
                    if (err && err.name === 'AbortError') console.warn('token 验证超时，未自动跳转');
                    else console.error('检查 token 时出错：', err);
                }
            })();
            var form = document.getElementById('loginForm');
            var submitBtn = document.getElementById('submitBtn');
            var backBtn = document.getElementById('backBtn');
            var errorBox = document.getElementById('error');

            function showError(msg) { errorBox.style.display = 'block'; errorBox.textContent = msg }
            function clearError() { errorBox.style.display = 'none'; errorBox.textContent = '' }

            function setLoading(on) {
                if (on) {
                    submitBtn.disabled = true;
                    submitBtn.innerHTML = '<span class="spinner" aria-hidden="true"></span>' + '  登录中';
                } else {
                    submitBtn.disabled = false;
                    submitBtn.textContent = '登录';
                }
            }

            form.addEventListener('submit', async function (e) {
                e.preventDefault();
                clearError();
                var u = document.getElementById('username').value.trim();
                var p = document.getElementById('password').value;
                if (!u || !p) {
                    showError('请填写用户名和密码');
                    return;
                }

                setLoading(true);
                try {
                    // 延迟请求以遵守最小延迟策略
                    await window.ensureMinLoadDelay(1000);

                    const resp = await fetch('/api/user/login', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ username: u, password: p })
                    });

                    const text = await resp.text();
                    if (resp.status === 200) {
                        let json = {};
                        try { json = JSON.parse(text); } catch { }
                        // Web 端优先保存 web_token；客户端登录时回退到 client_token。
                        var token = json.web_token || json.client_token || null;
                        if (token) {
                            try { localStorage.setItem('kax_login_token', token); } catch { }
                            try { localStorage.setItem('kax_web_token', token); } catch { }
                        }

                        window.location.href = postLoginRedirect;
                        return;
                    }

                    if (resp.status === 403) {
                        let json = {};
                        try { json = JSON.parse(text); } catch { }
                        const reason  = json.ban_reason  || '违反服务条款';
                        const expires = json.ban_expires || '永久';
                        showBanMsgBox(reason, expires);
                    } else if (resp.status === 401) {
                        showError(text || '用户名或密码错误。');
                    } else if (resp.status === 400) {
                        showError(text || '请求参数错误。');
                    } else {
                        showError(text || '服务器错误，登录失败。');
                    }
                } catch (err) {
                    console.error('登录请求失败：', err);
                    showError('网络错误，无法连接到服务器。');
                } finally {
                    setLoading(false);
                }
            });

            backBtn.addEventListener('click', function () {
                window.location.href = '/';
            });
        })();

window.initGlobalFooter && window.initGlobalFooter();
