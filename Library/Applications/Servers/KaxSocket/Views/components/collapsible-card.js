(function () {
    'use strict';

    const template = document.createElement('template');
    template.innerHTML = `
    <style>
      *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

      :host { display: block; }

      /* 外层卡片容器 */
      .card {
        border: 1px solid rgba(255,255,255,0.06);
        border-radius: 4px;
        background: rgba(255,255,255,0.02);
        overflow: hidden;
        transition: border-color 0.18s ease;
      }

      /* 标题栏（点击区域） */
      .header {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 14px 16px;
        cursor: pointer;
        user-select: none;
        -webkit-user-select: none;
        background: transparent;
        border: none;
        width: 100%;
        text-align: left;
        color: rgba(255,255,255,0.92);
        font-family: inherit;
        transition: background 0.15s ease;
      }
      .header:hover {
        background: rgba(255,255,255,0.03);
      }
      .header:focus-visible {
        outline: 2px solid rgba(59,130,246,0.8);
        outline-offset: -2px;
      }

      /* 左侧图标插槽 */
      .header-icon {
        display: flex;
        align-items: center;
        justify-content: center;
        width: 18px;
        height: 18px;
        color: rgba(255,255,255,0.55);
        flex-shrink: 0;
        font-size: 18px;
      }

      /* 标题文字区 */
      .header-text {
        flex: 1;
        min-width: 0;
      }
      .header-title {
        font-size: 0.93rem;
        font-weight: 600;
        color: rgba(255,255,255,0.92);
        line-height: 1.3;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .header-subtitle {
        font-size: 0.78rem;
        color: rgba(255,255,255,0.42);
        margin-top: 2px;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      :host(:not([subtitle])) .header-subtitle { display: none; }

      /* 右侧展开/收起箭头 */
      .chevron {
        flex-shrink: 0;
        width: 16px;
        height: 16px;
        color: rgba(255,255,255,0.35);
        transition: transform 0.2s ease, color 0.15s ease;
      }
      :host([open]) .chevron {
        transform: rotate(180deg);
        color: rgba(255,255,255,0.6);
      }
      .chevron svg {
        display: block;
        width: 100%;
        height: 100%;
      }

      /* 分割线（展开时显示） */
      .divider {
        height: 1px;
        background: rgba(255,255,255,0.05);
        margin: 0 16px;
        display: none;
      }
      :host([open]) .divider { display: block; }

      /* 内容包裹层（负责动画） */
      .body-wrapper {
        display: grid;
        grid-template-rows: 0fr;
        transition: grid-template-rows 0.22s ease;
      }
      :host([open]) .body-wrapper {
        grid-template-rows: 1fr;
      }
      .body-inner {
        overflow: hidden;
      }

      /* 内容插槽 */
      .body-content {
        padding: 16px;
      }

      /* 悬停时高亮边框 */
      :host(:hover) .card {
        border-color: rgba(255,255,255,0.09);
      }
      :host([open]) .card {
        border-color: rgba(255,255,255,0.08);
      }
    </style>

    <div class="card" part="card">
      <button class="header" part="header" type="button" aria-expanded="false">
        <span class="header-icon" part="icon" aria-hidden="true">
          <slot name="icon"></slot>
        </span>
        <div class="header-text">
          <div class="header-title" part="title"></div>
          <div class="header-subtitle" part="subtitle"></div>
        </div>
        <span class="chevron" aria-hidden="true">
          <svg viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
            <path d="M4 6l4 4 4-4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
        </span>
      </button>

      <div class="divider" part="divider" aria-hidden="true"></div>

      <div class="body-wrapper" part="body-wrapper">
        <div class="body-inner">
          <div class="body-content" part="body">
            <slot></slot>
          </div>
        </div>
      </div>
    </div>
    `;

    /**
     * 可折叠设置卡片组件
     * 
     * 属性：
     *   title      — 卡片标题（必填）
     *   subtitle   — 副标题/提示文字（可选）
     *   open       — 布尔属性，存在时默认展开
     * 
     * 插槽：
     *   icon       — 标题左侧图标（可放 <span class="material-icons">）
     *   (default)  — 折叠区域内容
     * 
     * 事件：
     *   toggle     — 展开/收起时触发，detail: { open: boolean }
     * 
     * 示例：
     *   <collapsible-card title="安全设置" subtitle="密码与登录" open>
     *     <span slot="icon" class="material-icons">security</span>
     *     ...内容...
     *   </collapsible-card>
     */
    class CollapsibleCard extends HTMLElement {
        static get observedAttributes() {
            return ['title', 'subtitle', 'open'];
        }

        constructor() {
            super();
            this._shadow = this.attachShadow({ mode: 'open' });
            this._shadow.appendChild(template.content.cloneNode(true));

            this._headerBtn  = this._shadow.querySelector('.header');
            this._titleEl    = this._shadow.querySelector('.header-title');
            this._subtitleEl = this._shadow.querySelector('.header-subtitle');

            this._headerBtn.addEventListener('click', () => this._toggle());
            this._headerBtn.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    this._toggle();
                }
            });
        }

        connectedCallback() {
            this._syncTitle();
            this._syncSubtitle();
            this._syncOpen();
        }

        attributeChangedCallback(name, oldVal, newVal) {
            if (oldVal === newVal) return;
            switch (name) {
                case 'title':    this._syncTitle();    break;
                case 'subtitle': this._syncSubtitle(); break;
                case 'open':     this._syncOpen();     break;
            }
        }

        /** 切换展开/收起状态 */
        _toggle() {
            if (this.hasAttribute('open')) {
                this.removeAttribute('open');
            } else {
                this.setAttribute('open', '');
            }
            this.dispatchEvent(new CustomEvent('toggle', {
                bubbles: true,
                composed: true,
                detail: { open: this.hasAttribute('open') }
            }));
        }

        _syncTitle() {
            this._titleEl.textContent = this.getAttribute('title') || '';
        }

        _syncSubtitle() {
            this._subtitleEl.textContent = this.getAttribute('subtitle') || '';
        }

        _syncOpen() {
            const isOpen = this.hasAttribute('open');
            this._headerBtn.setAttribute('aria-expanded', String(isOpen));
        }

        /** 编程式展开 */
        open() { this.setAttribute('open', ''); }

        /** 编程式收起 */
        close() { this.removeAttribute('open'); }

        /** 编程式切换 */
        toggle() { this._toggle(); }

        get isOpen() { return this.hasAttribute('open'); }
    }

    if (!customElements.get('collapsible-card')) {
        customElements.define('collapsible-card', CollapsibleCard);
    }
    window.CollapsibleCard = CollapsibleCard;
})();
