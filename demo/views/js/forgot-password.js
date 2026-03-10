(function () {
    'use strict';

    var form        = document.getElementById('fpForm');
    var submitBtn   = document.getElementById('submitBtn');
    var backBtn     = document.getElementById('backBtn');
    var errorEl     = document.getElementById('error');
    var successPanel = document.getElementById('successPanel');
    var cooldownTip = document.getElementById('cooldownTip');
    var cooldownTimer = document.getElementById('cooldownTimer');
    var resendBtn   = document.getElementById('resendBtn');
    var identifierInput = document.getElementById('identifier');

    var cooldownInterval = null;

    // ── 工具函数 ──────────────────────────────────────────────

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
        submitBtn.textContent = loading ? '发送中…' : '发送重置邮件';
    }

    function showSuccess(cooldownSeconds) {
        form.style.display = 'none';
        successPanel.style.display = 'block';
        startCooldown(cooldownSeconds || 30);
    }

    function startCooldown(seconds) {
        if (cooldownInterval) clearInterval(cooldownInterval);

        var remaining = seconds;
        resendBtn.disabled = true;
        cooldownTip.style.display = 'block';
        cooldownTimer.textContent = remaining;

        cooldownInterval = setInterval(function () {
            remaining -= 1;
            cooldownTimer.textContent = remaining;
            if (remaining <= 0) {
                clearInterval(cooldownInterval);
                cooldownInterval = null;
                cooldownTip.style.display = 'none';
                resendBtn.disabled = false;
            }
        }, 1000);
    }

    // ── 发送请求 ──────────────────────────────────────────────

    function sendRequest(identifier) {
        setLoading(true);
        hideError();

        fetch('/api/user/password-reset/request', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ identifier: identifier })
        })
        .then(function (res) { return res.json(); })
        .then(function (data) {
            setLoading(false);
            if (data.code === 0) {
                // 统一受理文案，不区分账号是否存在
                showSuccess(data.data && data.data.cooldownSeconds ? data.data.cooldownSeconds : 30);
            } else if (data.code === 47002) {
                // 频率限制
                var match = (data.message || '').match(/(\d+)\s*秒/);
                var secs = match ? parseInt(match[1], 10) : 30;
                showError(data.message || '操作频繁，请稍后再试');
                startCooldown(secs);
            } else {
                showError(data.message || '发送失败，请稍后重试');
            }
        })
        .catch(function () {
            setLoading(false);
            showError('网络错误，请检查连接后重试');
        });
    }

    // ── 事件绑定 ──────────────────────────────────────────────

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        hideError();

        var identifier = identifierInput.value.trim();
        if (!identifier) {
            showError('请输入用户名或邮箱');
            identifierInput.focus();
            return;
        }

        sendRequest(identifier);
    });

    backBtn.addEventListener('click', function () {
        window.location.href = '/login';
    });

    // 重发按钮：回到输入阶段
    resendBtn.addEventListener('click', function () {
        successPanel.style.display = 'none';
        form.style.display = 'block';
        identifierInput.focus();
    });

})();
