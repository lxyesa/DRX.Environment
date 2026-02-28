(function () {
    'use strict';

    const template = document.createElement('template');
    template.innerHTML = `
    <style>
      :host {
        display: block;
      }

      /* 卡片容器 */
      .card {
        background: var(--sc-bg, rgba(255,255,255,0.02));
        border: 1px solid var(--sc-border, rgba(255,255,255,0.06));
        border-radius: var(--sc-radius, 6px);
        overflow: hidden;
        transition: border-color 0.18s ease;
      }

      :host(:hover) .card {
        border-color: var(--sc-border-hover, rgba(255,255,255,0.10));
      }

      /* 头部 */
      .card-header {
        display: flex;
        align-items: center;
        gap: 14px;
        padding: 18px 20px;
        border-bottom: 1px solid var(--sc-border, rgba(255,255,255,0.06));
        background: rgba(255,255,255,0.015);
      }

      /* 隐藏头部：无 title 时收起 */
      :host([no-header]) .card-header { display: none; }

      /* 图标徽标 */
      .header-icon {
        display: none;
        flex-shrink: 0;
        width: 36px;
        height: 36px;
        align-items: center;
        justify-content: center;
        background: var(--sc-accent-dim, rgba(59,130,246,0.10));
        border-radius: 8px;
        font-size: 20px;
        color: var(--sc-accent, #3b82f6);
        font-family: 'Material Icons', sans-serif;
        font-feature-settings: 'liga';
        -webkit-font-feature-settings: 'liga';
        line-height: 1;
      }

      :host([icon]) .header-icon { display: flex; }

      /* 标题区 */
      .header-text { flex: 1; min-width: 0; }

      .header-title {
        font-size: 0.95rem;
        font-weight: 700;
        color: var(--sc-text, rgba(255,255,255,0.92));
        margin: 0 0 2px;
        line-height: 1.3;
      }

      .header-desc {
        font-size: 0.80rem;
        color: var(--sc-muted, rgba(255,255,255,0.50));
        margin: 0;
        line-height: 1.4;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      :host(:not([desc])) .header-desc { display: none; }

      /* body 区域 */
      .card-body {
        padding: 20px;
      }

      :host([no-padding]) .card-body { padding: 0; }

      /* footer slot */
      .card-footer {
        display: none;
        align-items: center;
        justify-content: flex-end;
        gap: 8px;
        padding: 14px 20px;
        border-top: 1px solid var(--sc-border, rgba(255,255,255,0.06));
        background: rgba(255,255,255,0.01);
      }

      /* 只要 footer slot 里有内容就显示 */
      .card-footer.has-content { display: flex; }

      /* footer 内的提示文字自动左对齐推开按钮 */
      ::slotted(.edit-hint) {
        margin-right: auto !important;
        font-size: 0.8rem !important;
        color: rgba(255,255,255,0.50) !important;
      }
    </style>

    <div class="card" part="card">
      <div class="card-header" part="header">
        <span class="header-icon material-icons" part="icon" aria-hidden="true"></span>
        <div class="header-text">
          <h3 class="header-title" part="title"></h3>
          <p class="header-desc" part="desc"></p>
        </div>
      </div>
      <div class="card-body" part="body">
        <slot></slot>
      </div>
      <div class="card-footer" part="footer">
        <slot name="footer"></slot>
      </div>
    </div>
    `;

    class SectionCard extends HTMLElement {
        static get observedAttributes() {
            return ['icon', 'title', 'desc', 'no-header', 'no-padding'];
        }

        constructor() {
            super();
            this._shadow = this.attachShadow({ mode: 'open' });
            this._shadow.appendChild(template.content.cloneNode(true));

            this._iconEl   = this._shadow.querySelector('.header-icon');
            this._titleEl  = this._shadow.querySelector('.header-title');
            this._descEl   = this._shadow.querySelector('.header-desc');
            this._footerEl = this._shadow.querySelector('.card-footer');

            // 监听 footer slot 变化，决定是否显示 footer 容器
            const footerSlot = this._shadow.querySelector('slot[name="footer"]');
            footerSlot.addEventListener('slotchange', () => this._syncFooter());
        }

        connectedCallback() {
            this._syncAttributesToDom();
            this._syncFooter();
        }

        attributeChangedCallback(name, oldVal, newVal) {
            if (oldVal === newVal) return;
            this._syncAttribute(name, newVal);
        }

        _syncAttributesToDom() {
            SectionCard.observedAttributes.forEach(a => this._syncAttribute(a, this.getAttribute(a)));
        }

        _syncAttribute(name, val) {
            switch (name) {
                case 'icon':
                    if (this._iconEl) this._iconEl.textContent = val || '';
                    break;
                case 'title':
                    if (this._titleEl) this._titleEl.textContent = val || '';
                    break;
                case 'desc':
                    if (this._descEl) this._descEl.textContent = val || '';
                    break;
            }
        }

        /** 检测 footer slot 是否有实际节点，有则显示 footer 容器 */
        _syncFooter() {
            const slot = this._shadow.querySelector('slot[name="footer"]');
            const hasContent = slot.assignedNodes({ flatten: true }).some(
                n => n.nodeType === Node.ELEMENT_NODE ||
                    (n.nodeType === Node.TEXT_NODE && n.textContent.trim())
            );
            this._footerEl.classList.toggle('has-content', hasContent);
        }
    }

    if (!customElements.get('section-card')) customElements.define('section-card', SectionCard);
    window.SectionCard = SectionCard;
})();
