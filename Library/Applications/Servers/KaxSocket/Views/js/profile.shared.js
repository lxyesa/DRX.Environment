/* profile 公共工具模块
 * 说明：
 * - 仅提供跨 profile 子模块可复用的无状态工具；
 * - 不持有业务状态，不修改现有业务语义；
 * - 通过 window.ProfileShared 暴露，便于逐步拆分 profile.js。
 */
(function registerProfileShared(global) {
    if (global.ProfileShared) return;

    /** 检查登录态：未登录时保持原行为跳转到 /login */
    function checkToken() {
        const token = localStorage.getItem('kax_login_token');
        if (!token) {
            location.href = '/login';
            return null;
        }
        return token;
    }

    /** 统一提示样式（危险/成功） */
    function showErrorMsg(el, text, isDanger = true) {
        if (!el) return;
        el.style.display = 'block';
        el.style.background = isDanger ? 'rgba(239,68,68,0.1)' : 'rgba(34,197,94,0.1)';
        el.style.borderColor = isDanger ? 'rgba(239,68,68,0.3)' : 'rgba(34,197,94,0.3)';
        el.style.color = isDanger ? 'var(--profile-danger)' : 'var(--profile-success)';
        el.textContent = text;
    }

    /** 设置元素显示状态 */
    function setElementDisplay(el, show) {
        if (el) el.style.display = show ? 'block' : 'none';
    }

    /** 批量设置元素显示状态 */
    function setElementsDisplay(displayMap) {
        Object.entries(displayMap).forEach(([id, show]) => {
            setElementDisplay(document.getElementById(id), show);
        });
    }

    /** 按钮 loading 包装：保持原始按钮文案与状态回滚 */
    async function withButtonLoading(btn, loadingText, fn) {
        const originalText = btn.textContent;
        btn.disabled = true;
        btn.textContent = loadingText;
        try {
            return await fn();
        } finally {
            btn.disabled = false;
            btn.textContent = originalText;
        }
    }

    /** HTML 转义（保持现有页面实际行为：单引号转义为 &#039;） */
    function escapeHtml(text) {
        if (!text) return '';
        const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
        return String(text).replace(/[&<>"']/g, m => map[m]);
    }

    global.ProfileShared = {
        checkToken,
        showErrorMsg,
        setElementDisplay,
        setElementsDisplay,
        withButtonLoading,
        escapeHtml
    };
})(window);
