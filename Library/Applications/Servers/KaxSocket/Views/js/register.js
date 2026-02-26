(function () {
            // 页面加载时：若本地存在 token 且能通过验证，则重定向到首页，避免已登录用户重复注册
            (async function checkTokenRedirect() {
                try {
                    var token = localStorage.getItem('kax_login_token');
                    if (!token) return;

                    var controller = new AbortController();
                    var timeoutId = setTimeout(function () { controller.abort(); }, 6000);

                    var resp = await fetch('/api/token/test', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
                        signal: controller.signal
                    });
                    clearTimeout(timeoutId);

                    if (resp.status === 200) {
                        window.location.href = '/';
                        return;
                    }

                    if (resp.status === 401) {
                        try { localStorage.removeItem('kax_login_token'); } catch (_) { }
                    }
                } catch (err) {
                    if (err && err.name === 'AbortError') console.warn('token 验证超时，未自动跳转');
                    else console.error('检查 token 时出错：', err);
                }
            })();
            var form = document.getElementById('registerForm');
            var submitBtn = document.getElementById('registerSubmit');
            var backBtn = document.getElementById('backToLogin');
            var errorBox = document.getElementById('error');

            function showError(msg) { errorBox.style.display = 'block'; errorBox.textContent = msg }
            function clearError() { errorBox.style.display = 'none'; errorBox.textContent = '' }

            function setLoading(on) {
                if (on) { submitBtn.disabled = true; submitBtn.innerHTML = '<span class="spinner" aria-hidden="true"></span>' + '  注册中' }
                else { submitBtn.disabled = false; submitBtn.textContent = '创建账号' }
            }

            function validateEmail(email) { return /\S+@\S+\.\S+/.test(email) }

            form.addEventListener('submit', async function (e) {
                e.preventDefault();
                clearError();
                var u = document.getElementById('r-username').value.trim();
                var em = document.getElementById('r-email').value.trim();
                var p1 = document.getElementById('r-password').value;
                var p2 = document.getElementById('r-password2').value;

                if (!u || !em || !p1 || !p2) { showError('请填写所有字段'); return }
                if (!validateEmail(em)) { showError('请输入有效的邮箱地址'); return }
                if (p1.length < 8) { showError('密码长度至少 8 位'); return }
                if (p1 !== p2) { showError('两次输入的密码不一致'); return }

                setLoading(true);
                try {
                    // 延迟请求以遵守最小延迟策略
                    await window.ensureMinLoadDelay(1000);

                    const resp = await fetch('/api/user/register', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ username: u, password: p1, email: em })
                    });

                    const text = await resp.text();

                    if (resp.status === 201) {
                        // 注册成功
                        alert('注册成功，请使用新账号登录');
                        window.location.href = 'login';
                        return;
                    }

                    // 处理常见错误码并显示消息
                    if (resp.status === 409) {
                        showError(text || '用户名或电子邮箱已被注册。');
                    } else if (resp.status === 400) {
                        showError(text || '请求参数错误，请检查输入。');
                    } else {
                        showError(text || '服务器错误，注册失败，请稍后重试。');
                    }
                } catch (err) {
                    console.error('注册请求失败：', err);
                    showError('网络错误，无法连接到服务器。');
                } finally {
                    setLoading(false);
                }
            });

            backBtn.addEventListener('click', function () { window.location.href = 'login' });
        })();

window.initGlobalFooter && window.initGlobalFooter();
