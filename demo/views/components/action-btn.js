(function () {
    'use strict';

    const template = document.createElement('template');
    template.innerHTML = `
    <style>
      :host {
        display: inline-block;
      }

      :host([block]) {
        display: block;
        width: 100%;
      }

      :host([disabled]) {
        opacity: 0.5;
        cursor: not-allowed;
        pointer-events: none;
      }

      /* 注入图标库字体 */
      @import url(var(--_icon-lib));

      button {
        position: relative;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        gap: var(--_gap, 5px);
        width: var(--_width, auto);
        height: var(--_height, auto);
        min-width: var(--_min-width, 0);
        padding: var(--_padding, 0 12px);
        border-radius: var(--_radius, 4px);
        background: var(--_bg, rgba(99,140,255,0.08));
        color: var(--_color, var(--dev-accent, #638cff));
        border: var(--_border, 1px solid rgba(99,140,255,0.2));
        font-size: var(--_font-size, 0.9rem);
        font-family: inherit;
        font-weight: 600;
        cursor: pointer;
        box-sizing: border-box;
        overflow: hidden;
        white-space: nowrap;
        transition: background 0.14s ease, color 0.12s ease, border-color 0.14s ease, box-shadow 0.14s ease;
        outline: none;
      }

      :host([block]) button {
        width: 100%;
      }

      button:hover {
        background: var(--_bg-hover, rgba(99,140,255,0.16));
        color: var(--_color-hover, var(--dev-accent, #638cff));
        box-shadow: var(--_hover-shadow, none);
      }

      button:active {
        transform: scale(var(--btn-press-scale, 0.985));
        transition-duration: var(--btn-press-duration, 120ms);
        transition-timing-function: var(--btn-press-ease, cubic-bezier(.2,.9,.2,1));
      }

      button:focus-visible {
        box-shadow: 0 0 0 3px var(--_focus-ring, rgba(99,140,255,0.35));
      }

      /* 图标 */
      .btn-icon {
        display: none;
        flex-shrink: 0;
        font-size: var(--_icon-size, 18px);
        line-height: 1;
        /* 支持 Material Icons 字体连字 */
        font-family: var(--_icon-font-family, 'Material Icons');
        font-weight: normal;
        font-style: normal;
        letter-spacing: normal;
        text-transform: none;
        word-wrap: normal;
        white-space: nowrap;
        direction: ltr;
        font-feature-settings: 'liga';
        -webkit-font-feature-settings: 'liga';
        -webkit-font-smoothing: antialiased;
      }
      :host([icon]) .btn-icon { display: inline-flex; align-items: center; }

      /* 文字 */
      .btn-label {
        display: contents;
      }
      :host(:not([label])) .btn-label,
      :host([label=""]) .btn-label { display: none; }

      /* 加载状态 */
      :host([loading]) button { cursor: wait; }
      :host([loading]) .btn-icon,
      :host([loading]) .btn-label { opacity: 0.6; }

      /* 幽灵变体 */
      :host([variant="ghost"]) button {
        background: transparent;
        border: 1px solid var(--_color);
        color: var(--_color);
      }
      :host([variant="ghost"]) button:hover {
        background: var(--_bg);
        color: var(--_color-hover);
      }

      /* 危险变体 */
      :host([variant="danger"]) button {
        background: var(--_danger-bg, rgba(239,68,68,0.1));
        color: var(--_danger-color, #ef4444);
        border: 1px solid var(--_danger-border, rgba(239,68,68,0.25));
      }
      :host([variant="danger"]) button:hover {
        background: var(--_danger-bg-hover, rgba(239,68,68,0.18));
        color: var(--_danger-color, #ef4444);
      }
    </style>
    <button type="button" part="button">
      <span class="btn-icon material-icons" part="icon" aria-hidden="true"></span>
      <span class="btn-label" part="label"></span>
    </button>
    `;

    class ActionBtn extends HTMLElement {
        /**
         * action-btn 支持参数说明
         *
         * 1) HTML Attributes（可直接写在标签上）
         * - 文本/图标: label, icon, icon-lib, icon-font
         * - 尺寸/布局: width, height, min-width, padding, radius, font-size, gap, icon-size
         * - 颜色/边框: color, bg, border, hover-bg, hover-color, hover-shadow, focus-ring
         * - 状态/行为: disabled, loading, type(button|submit|reset), variant(default|ghost|danger), block
         *
         * 2) CSS Variables（可在外部通过 action-btn 选择器传入）
         * - --dev-radius（项目通用圆角变量，作为 --_radius 默认来源）
         * - --dev-accent（默认主色来源）
         * - 组件内部变量: --_bg, --_bg-hover, --_color, --_color-hover, --_border,
         *   --_radius, --_padding, --_font-size, --_icon-size, --_gap, --_focus-ring,
         *   --_width, --_height, --_min-width, --_icon-font-family
         *
         * 3) JavaScript API
         * - disabled: boolean
         * - loading: boolean
         * - value: string（映射到 label）
         */
        static get observedAttributes() {
            return [
                'label', 'icon', 'icon-lib',
                'color', 'bg', 'border',
                'width', 'height', 'min-width',
                'padding', 'radius', 'font-size',
                'gap', 'icon-size', 'icon-font',
                'disabled', 'loading', 'type', 'variant',
                'hover-bg', 'hover-color', 'hover-shadow', 'focus-ring',
            ];
        }

        constructor() {
            super();
            this._shadow = this.attachShadow({ mode: 'open' });
            this._shadow.appendChild(template.content.cloneNode(true));
            this._btn = this._shadow.querySelector('button');
            this._iconEl = this._shadow.querySelector('.btn-icon');
            this._labelEl = this._shadow.querySelector('.btn-label');

            // 代理原生点击事件
            this._btn.addEventListener('click', e => {
                if (this.hasAttribute('disabled') || this.hasAttribute('loading')) {
                    e.preventDefault();
                    e.stopImmediatePropagation();
                    return;
                }
            });

            this._applyDefaults();
        }

        connectedCallback() {
            ActionBtn.observedAttributes.forEach(a => this._syncAttribute(a, this.getAttribute(a)));
        }

        attributeChangedCallback(name, oldVal, newVal) {
            if (oldVal === newVal) return;
            this._syncAttribute(name, newVal);
        }

        /** 默认视觉风格（与页面 .btn 风格一致） */
        _applyDefaults() {
            const h = this._shadow.host;
            this._setCss('--_bg',           'rgba(99,140,255,0.08)');
            this._setCss('--_bg-hover',     'rgba(99,140,255,0.16)');
            this._setCss('--_color',        'var(--dev-accent, #638cff)');
            this._setCss('--_color-hover',  'var(--dev-accent, #638cff)');
            this._setCss('--_border',       '1px solid rgba(99,140,255,0.2)');
            this._setCss('--_radius',       'var(--dev-radius, 4px)');
            this._setCss('--_padding',      '0 12px');
            this._setCss('--_font-size',    '0.9rem');
            this._setCss('--_icon-size',    '18px');
            this._setCss('--_gap',          '5px');
            this._setCss('--_focus-ring',   'rgba(99,140,255,0.35)');
            this._setCss('--_min-width',    '0');
        }

        _setCss(prop, val) {
            this._shadow.host.style.setProperty(prop, val);
        }

        _removeCss(prop) {
            this._shadow.host.style.removeProperty(prop);
        }

        _normalizeCssSize(val) {
            if (val == null) return '';
            const str = String(val).trim();
            if (!str) return '';
            if (/^-?\d+(\.\d+)?$/.test(str)) return `${str}px`;
            return str;
        }

        _syncAttribute(name, val) {
            switch (name) {
                case 'label':
                    this._labelEl.textContent = val || '';
                    break;
                case 'icon':
                    this._iconEl.textContent = val || '';
                    break;
                case 'icon-lib':
                    // 动态注入图标库 <link>；默认已在外部页面载入 Material Icons，此处仅在用户指定时追加
                    if (val) this._injectIconLib(val);
                    break;
                case 'icon-font':
                    val ? this._setCss('--_icon-font-family', val) : this._removeCss('--_icon-font-family');
                    break;
                case 'color':
                    val ? this._setCss('--_color', val) : this._removeCss('--_color');
                    if (val && !this.hasAttribute('hover-color')) this._setCss('--_color-hover', val);
                    break;
                case 'hover-color':
                    val ? this._setCss('--_color-hover', val) : this._removeCss('--_color-hover');
                    break;
                case 'bg':
                    if (val) {
                        this._setCss('--_bg', val);
                        if (!this.hasAttribute('hover-bg')) this._setCss('--_bg-hover', val);
                    } else {
                        this._removeCss('--_bg');
                    }
                    break;
                case 'hover-bg':
                    val ? this._setCss('--_bg-hover', val) : this._removeCss('--_bg-hover');
                    break;
                case 'hover-shadow':
                    val ? this._setCss('--_hover-shadow', val) : this._removeCss('--_hover-shadow');
                    break;
                case 'focus-ring':
                    val ? this._setCss('--_focus-ring', val) : this._removeCss('--_focus-ring');
                    break;
                case 'border':
                    val ? this._setCss('--_border', val) : this._removeCss('--_border');
                    break;
                case 'width':
                    val ? this._setCss('--_width', this._normalizeCssSize(val)) : this._removeCss('--_width');
                    break;
                case 'height':
                    val ? this._setCss('--_height', this._normalizeCssSize(val)) : this._removeCss('--_height');
                    break;
                case 'min-width':
                    val ? this._setCss('--_min-width', this._normalizeCssSize(val)) : this._removeCss('--_min-width');
                    break;
                case 'padding':
                    val ? this._setCss('--_padding', val) : this._removeCss('--_padding');
                    break;
                case 'radius':
                    val ? this._setCss('--_radius', this._normalizeCssSize(val)) : this._removeCss('--_radius');
                    break;
                case 'font-size':
                    val ? this._setCss('--_font-size', this._normalizeCssSize(val)) : this._removeCss('--_font-size');
                    break;
                case 'icon-size':
                    val ? this._setCss('--_icon-size', this._normalizeCssSize(val)) : this._removeCss('--_icon-size');
                    break;
                case 'gap':
                    val ? this._setCss('--_gap', this._normalizeCssSize(val)) : this._removeCss('--_gap');
                    break;
                case 'type':
                    if (val) this._btn.type = val;
                    break;
                case 'disabled':
                    this._btn.disabled = val !== null;
                    break;
                case 'loading':
                    this._btn.setAttribute('aria-busy', val !== null ? 'true' : 'false');
                    if (val !== null) this._btn.setAttribute('aria-disabled', 'true');
                    else this._btn.removeAttribute('aria-disabled');
                    break;
            }
        }

        /** 动态注入图标库 <link> 到文档 head（去重） */
        _injectIconLib(href) {
            if (document.querySelector(`link[data-action-btn-icon-lib="${CSS.escape(href)}"]`)) return;
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = href;
            link.dataset.actionBtnIconLib = href;
            document.head.appendChild(link);
        }

        /** JavaScript API */
        get disabled() { return this.hasAttribute('disabled'); }
        set disabled(v) { v ? this.setAttribute('disabled', '') : this.removeAttribute('disabled'); }

        get loading() { return this.hasAttribute('loading'); }
        set loading(v) { v ? this.setAttribute('loading', '') : this.removeAttribute('loading'); }

        get value() { return this.getAttribute('label') || ''; }
        set value(v) { this.setAttribute('label', v); }
    }

    if (!customElements.get('action-btn')) {
        customElements.define('action-btn', ActionBtn);
    }
})();
