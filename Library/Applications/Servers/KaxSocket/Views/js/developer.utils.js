/* ================================================================
 *  developer.utils.js — 工具函数与常量
 *  包含：常量定义、formatDate、escapeHtml、statusBadge、
 *        normalizeTags、tryParseJsonArray、
 *        setButtonLoading
 * ================================================================ */
'use strict';

const TOKEN_KEY = 'kax_login_token';
const PAGE_SIZE = 20;

const STATUS_TEXT = { 0: '审核中', 1: '已拒绝', 2: '待发布', 3: '已上线', 4: '已下架' };

const SYSTEM_FIELD_CONFIG = [
    { key: 'name', label: '名称', multiline: false },
    { key: 'version', label: '版本', multiline: false },
    { key: 'description', label: '描述', multiline: true },
    { key: 'category', label: '分类', multiline: false },
    { key: 'tags', label: '标签', multiline: true, visualType: 'chips' },
    { key: 'coverImage', label: '封面图 URL', multiline: false },
    { key: 'iconImage', label: '图标 URL', multiline: false },
    { key: 'screenshots', label: '截图', multiline: true, visualType: 'image-list' },
    { key: 'downloadUrl', label: '下载地址', multiline: false },
    { key: 'license', label: '许可证', multiline: false },
    { key: 'compatibility', label: '兼容性', multiline: true, visualType: 'chips' },
    { key: 'languageSupportsJson', label: '语言支持', multiline: true, visualType: 'language-support' }
];

function formatDate(ts) {
    if (!ts) return '—';
    const ms = ts > 9999999999 ? ts : ts * 1000;
    const d = new Date(ms);
    if (isNaN(d.getTime())) return '—';
    return d.toLocaleDateString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit' });
}

function formatDateTime(ts) {
    if (!ts) return '—';
    const ms = ts > 9999999999 ? ts : ts * 1000;
    const d = new Date(ms);
    if (isNaN(d.getTime())) return '—';
    return d.toLocaleString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
}

function formatCooldown(seconds) {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    return h > 0 ? `${h}小时${m}分钟` : `${m}分钟`;
}

function escapeHtml(str) {
    if (!str) return '';
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

function escapeAttr(str) {
    return String(str ?? '').replace(/&/g, '&amp;').replace(/"/g, '&quot;');
}

function statusBadge(statusVal) {
    const text = STATUS_TEXT[statusVal] || '未知';
    return `<span class="dev-status" data-status="${statusVal}">${text}</span>`;
}

function normalizeTags(raw) {
    if (Array.isArray(raw)) return raw.filter(Boolean).map(t => String(t).trim()).filter(Boolean);
    if (typeof raw === 'string') {
        return raw.split(/[;,，]/).map(t => t.trim()).filter(Boolean);
    }
    return [];
}

function tryParseJsonArray(raw) {
    if (!raw || typeof raw !== 'string') return [];
    try {
        const parsed = JSON.parse(raw);
        return Array.isArray(parsed) ? parsed : [];
    } catch {
        return [];
    }
}

function getSystemFieldRawValue(detail, field) {
    if (!detail || !field) return '';
    if (Object.prototype.hasOwnProperty.call(detail, field)) return detail[field];
    if (detail.specs && Object.prototype.hasOwnProperty.call(detail.specs, field)) return detail.specs[field];
    return '';
}

function normalizeSystemFieldValue(value) {
    if (Array.isArray(value)) return value.join(';');
    if (typeof value === 'object' && value !== null) return JSON.stringify(value);
    return value == null ? '' : String(value);
}

function formatSystemFieldPreview(value) {
    const text = normalizeSystemFieldValue(value).trim();
    if (!text) return '当前值：空';
    const clipped = text.length > 42 ? `${text.slice(0, 42)}...` : text;
    return `当前值：${clipped}`;
}

function setButtonLoading(button, loadingText) {
    if (!button) return () => {};
    if (button.dataset.loading === '1') return () => {};

    button.dataset.loading = '1';
    button.dataset.originalHtml = button.innerHTML;
    button.dataset.originalDisabled = button.disabled ? '1' : '0';
    button.disabled = true;
    button.classList.add('is-loading');
    if (loadingText) button.textContent = loadingText;

    return () => {
        button.classList.remove('is-loading');
        if (button.dataset.originalHtml != null) button.innerHTML = button.dataset.originalHtml;
        button.disabled = button.dataset.originalDisabled === '1';
        delete button.dataset.loading;
        delete button.dataset.originalHtml;
        delete button.dataset.originalDisabled;
    };
}

function parseSemicolon(raw) {
    if (!raw) return [];
    return String(raw).split(/[;,]/).map(s => s.trim()).filter(Boolean);
}
