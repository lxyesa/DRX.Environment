(function () {
    'use strict';

    // ── DOM 引用 ──────────────────────────────────────────────

    var loadingPanel  = document.getElementById('loadingPanel');
    var invalidPanel  = document.getElementById('invalidPanel');
    var invalidMsg    = document.getElementById('invalidMsg');
    var formPanel     = document.getElementById('formPanel');
    var accountHint   = document.getElementById('accountHint');
    var successPanel  = document.getElementById('successPanel');
    var countdownNum  = document.getElementById('countdownNum');

    var rpForm        = document.getElementById('rpForm');
    var submitBtn     = document.getElementById('submitBtn');
    var errorEl       = document.getElementById('error');
    var newPasswordEl = document.getElementById('newPassword');
    var confirmPassEl = document.getElementById('confirmPassword');

    // ── 从 URL 读取 token ──────────────────────────────────────

    function getToken() {
        try {
            return new URLSearchParams(window.location.search).get('token') || '';
        } catch (e) {
            return '';
        }
    }

    var token = getToken();

    // ── 工具函数 ──────────────────────────────────────────────

    function showPanel(name) {
        var panels = ['loadingPanel', 'invalidPanel', 'formPanel', 'successPanel'];
        panels.forEach(function (id) {
            var el = document.getElementById(id);
            if (el) el.style.display = id === name ? (id === 'formPanel' ? 'block' : 'block') : 'none';
        });
    }

    function showError(msg) {
        errorEl.textContent = msg;
        errorEl.style.display = 'block';
    }

    function hideError() {
        errorEl.style.display = 'none';
        errorEl.textContent = '';
    }

    function setLoading(loading) {
        submitBtn.disabled = loading;
        submitBtn.textContent = loading ? '提交中…' : '确认重置';
    }

    // ── 初始化：预检令牌 ──────────────────────────────────────

    function validateToken() {
        if (!token) {
            showPanel('invalidPanel');
            invalidMsg.textContent = '重置链接缺少必要参数，请重新发起找回申请。';
            return;
        }

        fetch('/api/user/password-reset/validate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ token: token })
        })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            if (data.code === 0) {
                var email = (data.data && data.data.maskedEmail) ? data.data.maskedEmail : '';
                if (email) {
                    accountHint.textContent = '正在重置账号 ' + email + ' 的密码';
                }
                showPanel('formPanel');
                newPasswordEl.focus();
            } else {
                showPanel('invalidPanel');
                invalidMsg.textContent = data.message || '重置链接无效或已使用，请重新发起找回申请。';
            }
        })
        .catch(function () {
            showPanel('invalidPanel');
            invalidMsg.textContent = '网络错误，请检查连接后刷新重试。';
        });
    }

    // ── 提交新密码 ────────────────────────────────────────────

    rpForm.addEventListener('submit', function (e) {
        e.preventDefault();
        hideError();

        var newPwd     = newPasswordEl.value;
        var confirmPwd = confirmPassEl.value;

        if (!newPwd) {
            showError('请输入新密码');
            newPasswordEl.focus();
            return;
        }
        if (newPwd.length < 8) {
            showError('密码长度不能少于 8 位');
            newPasswordEl.focus();
            return;
        }
        if (newPwd !== confirmPwd) {
            showError('两次输入的密码不一致');
            confirmPassEl.focus();
            return;
        }

        setLoading(true);

        fetch('/api/user/password-reset/confirm', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                token: token,
                newPassword: newPwd,
                confirmPassword: confirmPwd
            })
        })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            setLoading(false);
            if (data.code === 0) {
                showPanel('successPanel');
                startLoginCountdown();
            } else if (data.code === 47004 || data.code === 47003 || data.code === 47006) {
                // 令牌状态异常，跳到无效面板
                showPanel('invalidPanel');
                invalidMsg.textContent = data.message || '重置链接已失效，请重新发起申请。';
            } else {
                showError(data.message || '提交失败，请稍后重试');
            }
        })
        .catch(function () {
            setLoading(false);
            showError('网络错误，请检查连接后重试');
        });
    });

    // ── 成功后倒计时跳转 ──────────────────────────────────────

    function startLoginCountdown() {
        var remaining = 5;
        var interval = setInterval(function () {
            remaining -= 1;
            countdownNum.textContent = remaining;
            if (remaining <= 0) {
                clearInterval(interval);
                window.location.href = '/login';
            }
        }, 1000);
    }

    // ── 启动 ──────────────────────────────────────────────────
    validateToken();

})();
