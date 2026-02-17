// 注册 <user-badge> 组件 —— 支持通过属性控制文字与颜色（variant/bg/color）
// 使用说明：<user-badge text="无" variant="gray"></user-badge>
// 组件使用 Shadow DOM 隔离样式，遵循项目的暗色扁平化配色（支持 var(--accent)、var(--success) 等 CSS 变量）。
(function () {
    'use strict';

    const VARIANTS = {
        gray: { bg: 'rgba(255,255,255,0.03)', color: 'rgba(255,255,255,0.9)' },
        accent: { bg: 'var(--accent)', color: '#ffffff' },
        success: { bg: 'var(--success)', color: '#ffffff' },
        danger: { bg: 'var(--danger)', color: '#ffffff' }
    };

    class UserBadge extends HTMLElement {
        static get observedAttributes() { return ['text', 'variant', 'bg', 'color']; }

        constructor() {
            super();
            this._shadow = this.attachShadow({ mode: 'open' });
            this._shadow.innerHTML = `
                <style>
                    :host{ --ub-bg: var(--ub-bg, rgba(255,255,255,0.03)); --ub-color: var(--ub-color, rgba(255,255,255,0.9)); display:inline-flex; align-items:center; justify-content:center; padding:4px 8px; border-radius:999px; font-size:0.78rem; font-weight:700; line-height:1; box-sizing:border-box; min-height:20px; min-width:20px; color:var(--ub-color); background:var(--ub-bg); border:1px solid rgba(255,255,255,0.02); }
                    .label{ display:inline-block; padding:6px; white-space:nowrap; }
                </style>
                <span class="label" part="label"></span>
            `;
            this._labelEl = this._shadow.querySelector('.label');
            this.setAttribute('role', this.getAttribute('role') || 'status');
        }

        connectedCallback() {
            this._render();
        }

        attributeChangedCallback(name, oldVal, newVal) {
            if (oldVal === newVal) return;
            this._render();
        }

        // 属性/内容优先级：属性 text > 元素子节点文本 > 属性 textContent
        _render() {
            const attrText = this.getAttribute('text');
            const lightText = (this.textContent || '').trim();
            const label = attrText || lightText || '';
            this._labelEl.textContent = label;

            const variant = (this.getAttribute('variant') || '').toLowerCase();
            const explicitBg = this.getAttribute('bg');
            const explicitColor = this.getAttribute('color');
            const explicitPadding = this.getAttribute('padding');

            let bg, color;
            if (explicitBg || explicitColor) {
                // 支持任意 CSS 颜色字符串（包括 rgb(...)、rgba(...)、hex 等）
                bg = explicitBg || VARIANTS.gray.bg;
                color = explicitColor || VARIANTS.gray.color;
            } else if (variant && VARIANTS[variant]) {
                bg = VARIANTS[variant].bg;
                color = VARIANTS[variant].color;
            } else {
                bg = VARIANTS.gray.bg;
                color = VARIANTS.gray.color;
            }

            // 支持通过属性覆盖 padding（如写法： padding="6px 10px"）
            if (explicitPadding) {
                this.style.setProperty('padding', explicitPadding);
            }

            // 将颜色写入 host 的 CSS 变量，shadow 样式会读取
            this.style.setProperty('--ub-bg', bg);
            this.style.setProperty('--ub-color', color);
        }

        // 便捷属性访问器
        get text() { return this.getAttribute('text') || this.textContent; }
        set text(v) { this.setAttribute('text', v); }

        get variant() { return this.getAttribute('variant'); }
        set variant(v) { this.setAttribute('variant', v); }
    }

    if (!customElements.get('user-badge')) {
        customElements.define('user-badge', UserBadge);
    }

    // 导出到全局以便调试（可选）
    window.UserBadge = UserBadge;
})();
