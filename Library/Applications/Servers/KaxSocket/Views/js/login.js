// 简单客户端验证与交互（界面演示）
        (function () {
            // 页面加载时：若本地存在 token 且能通过验证，则视为已登录并重定向到首页
            (async function checkTokenRedirect() {
                try {
                    var token = localStorage.getItem('kax_login_token');
                    if (!token) return; // 无 token，继续显示登录页

                    var controller = new AbortController();
                    var timeoutId = setTimeout(function () { controller.abort(); }, 6000);

                    var resp = await fetch('/api/token/test', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                        signal: controller.signal
                    });
                    clearTimeout(timeoutId);

                    if (resp.status === 200) {
                        // token 有效：跳转到首页
                        window.location.href = '/';
                        return;
                    }

                    if (resp.status === 401) {
                        // token 无效或过期，清理并留在当前页
                        try { localStorage.removeItem('kax_login_token'); } catch (_) { }
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
                        var token = json.login_token || null;
                        if (token) {
                            try { localStorage.setItem('kax_login_token', token); } catch { }
                        }

                        window.location.href = '/';
                        return;
                    }

                    if (resp.status === 401) {
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
