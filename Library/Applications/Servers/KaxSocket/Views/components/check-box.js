(function () {
    'use strict';

    const template = document.createElement('template');
    template.innerHTML = `
    <style>
      :host {
        display: inline-flex;
        align-items: flex-start;
        gap: 10px;
        cursor: pointer;
        user-select: none;
        -webkit-user-select: none;
        vertical-align: middle;
      }

      :host([disabled]) {
        opacity: 0.5;
        cursor: not-allowed;
        pointer-events: none;
      }

      /* 隐藏原生 checkbox */
      input[type="checkbox"] {
        position: absolute;
        opacity: 0;
        width: 0;
        height: 0;
        pointer-events: none;
      }

      /* 自定义复选框容器 */
      .checkbox {
        flex-shrink: 0;
        width: 18px;
        height: 18px;
        border: 1px solid rgba(255,255,255,0.25);
        border-radius: 4px;
        background: rgba(255,255,255,0.02);
        display: flex;
        align-items: center;
        justify-content: center;
        transition: all 0.15s ease;
        position: relative;
      }

      :host(:hover:not([disabled])) .checkbox {
        border-color: rgba(255,255,255,0.4);
        background: rgba(255,255,255,0.04);
      }

      :host(:focus-within) .checkbox {
        border-color: rgba(59,130,246,0.8);
        box-shadow: 0 0 0 3px rgba(59,130,246,0.12);
      }

      /* 选中状态 */
      :host([checked]) .checkbox {
        background: rgba(59,130,246,0.9);
        border-color: rgba(59,130,246,0.9);
      }

      :host([checked]:hover:not([disabled])) .checkbox {
        background: rgba(59,130,246,1);
        border-color: rgba(59,130,246,1);
      }

      /* 勾选图标 */
      .checkmark {
        width: 12px;
        height: 12px;
        color: #fff;
        opacity: 0;
        transform: scale(0.5);
        transition: opacity 0.12s ease, transform 0.12s ease;
      }

      :host([checked]) .checkmark {
        opacity: 1;
        transform: scale(1);
      }

      .checkmark svg {
        display: block;
        width: 100%;
        height: 100%;
      }

      /* 不确定状态（indeterminate） */
      .indeterminate-mark {
        position: absolute;
        width: 10px;
        height: 2px;
        background: #fff;
        border-radius: 1px;
        opacity: 0;
        transform: scaleX(0);
        transition: opacity 0.12s ease, transform 0.12s ease;
      }

      :host([indeterminate]) .checkbox {
        background: rgba(59,130,246,0.9);
        border-color: rgba(59,130,246,0.9);
      }

      :host([indeterminate]) .indeterminate-mark {
        opacity: 1;
        transform: scaleX(1);
      }

      :host([indeterminate]) .checkmark {
        opacity: 0;
        transform: scale(0.5);
      }

      /* 标签文本 */
      .label-content {
        display: flex;
        flex-direction: column;
        gap: 2px;
        padding-top: 1px;
      }

      .label-text {
        font-size: 0.92rem;
        color: rgba(255,255,255,0.92);
        line-height: 1.4;
      }

      .label-desc {
        font-size: 0.78rem;
        color: rgba(255,255,255,0.45);
        line-height: 1.4;
      }

      :host(:not([desc])) .label-desc { display: none; }

      /* size 档位：large */
      :host([size="large"]) { gap: 12px; }
      :host([size="large"]) .checkbox { width: 22px; height: 22px; border-radius: 5px; }
      :host([size="large"]) .checkmark { width: 14px; height: 14px; }
      :host([size="large"]) .indeterminate-mark { width: 12px; height: 2.5px; }
      :host([size="large"]) .label-text { font-size: 1rem; }
      :host([size="large"]) .label-desc { font-size: 0.85rem; }

      /* size 档位：small */
      :host([size="small"]) { gap: 8px; }
      :host([size="small"]) .checkbox { width: 14px; height: 14px; border-radius: 3px; }
      :host([size="small"]) .checkmark { width: 10px; height: 10px; }
      :host([size="small"]) .indeterminate-mark { width: 8px; height: 1.5px; }
      :host([size="small"]) .label-text { font-size: 0.82rem; }
      :host([size="small"]) .label-desc { font-size: 0.72rem; }
      :host([size="small"]) .label-content { padding-top: 0; }
    </style>
    <input type="checkbox" part="input" />
    <div class="checkbox" part="checkbox">
      <span class="checkmark" aria-hidden="true">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
          <polyline points="20 6 9 17 4 12"></polyline>
        </svg>
      </span>
      <span class="indeterminate-mark" aria-hidden="true"></span>
    </div>
    <div class="label-content" part="label">
      <span class="label-text"></span>
      <span class="label-desc"></span>
    </div>
    `;

    class CheckBox extends HTMLElement {
        static get observedAttributes() {
            return ['label', 'desc', 'checked', 'disabled', 'name', 'value', 'size', 'indeterminate'];
        }

        constructor() {
            super();
            this._shadow = this.attachShadow({ mode: 'open' });
            this._shadow.appendChild(template.content.cloneNode(true));
            this._inputEl = this._shadow.querySelector('input');
            this._labelTextEl = this._shadow.querySelector('.label-text');
            this._labelDescEl = this._shadow.querySelector('.label-desc');

            // 点击整个组件切换状态
            this.addEventListener('click', (e) => {
                if (this.disabled) return;
                e.preventDefault();
                this.checked = !this.checked;
                // 触发 change 事件
                this._inputEl.dispatchEvent(new Event('change', { bubbles: true }));
            });

            // 代理原生事件
            ['change', 'focus', 'blur'].forEach(ev => {
                this._inputEl.addEventListener(ev, (e) => {
                    const ne = new e.constructor(e.type, e);
                    this.dispatchEvent(ne);
                });
            });

            // 键盘支持：空格键切换
            this.addEventListener('keydown', (e) => {
                if (e.key === ' ' && !this.disabled) {
                    e.preventDefault();
                    this.checked = !this.checked;
                    this._inputEl.dispatchEvent(new Event('change', { bubbles: true }));
                }
            });

            // 使组件可聚焦
            if (!this.hasAttribute('tabindex')) {
                this.setAttribute('tabindex', '0');
            }
        }

        connectedCallback() {
            this._upgradeProperty('checked');
            this._upgradeProperty('indeterminate');
            this._syncAttributesToInput();
        }

        attributeChangedCallback(name, oldVal, newVal) {
            if (oldVal === newVal) return;
            this._syncAttribute(name, newVal);
        }

        _upgradeProperty(prop) {
            if (this.hasOwnProperty(prop)) {
                let val = this[prop];
                delete this[prop];
                this[prop] = val;
            }
        }

        _syncAttributesToInput() {
            CheckBox.observedAttributes.forEach(a => this._syncAttribute(a, this.getAttribute(a)));
        }

        _syncAttribute(name, val) {
            switch (name) {
                case 'label':
                    this._labelTextEl.textContent = val || '';
                    break;
                case 'desc':
                    this._labelDescEl.textContent = val || '';
                    break;
                case 'checked':
                    this._inputEl.checked = val !== null;
                    // 选中时清除 indeterminate 状态
                    if (val !== null) {
                        this.removeAttribute('indeterminate');
                        this._inputEl.indeterminate = false;
                    }
                    break;
                case 'disabled':
                    if (val === null) {
                        this._inputEl.removeAttribute('disabled');
                        this.removeAttribute('tabindex');
                        this.setAttribute('tabindex', '0');
                    } else {
                        this._inputEl.setAttribute('disabled', '');
                        this.setAttribute('tabindex', '-1');
                    }
                    break;
                case 'name':
                    if (val == null) {
                        this._inputEl.removeAttribute('name');
                    } else {
                        this._inputEl.setAttribute('name', val);
                    }
                    break;
                case 'value':
                    this._inputEl.value = val || 'on';
                    break;
                case 'indeterminate':
                    this._inputEl.indeterminate = val !== null;
                    break;
            }
        }

        // 属性代理
        get checked() { return this.hasAttribute('checked'); }
        set checked(v) {
            if (v) {
                this.setAttribute('checked', '');
            } else {
                this.removeAttribute('checked');
            }
            this._inputEl.checked = v;
        }

        get indeterminate() { return this.hasAttribute('indeterminate'); }
        set indeterminate(v) {
            if (v) {
                this.setAttribute('indeterminate', '');
                this.removeAttribute('checked');
            } else {
                this.removeAttribute('indeterminate');
            }
            this._inputEl.indeterminate = v;
        }

        get disabled() { return this.hasAttribute('disabled'); }
        set disabled(v) {
            if (v) this.setAttribute('disabled', '');
            else this.removeAttribute('disabled');
        }

        get value() { return this._inputEl.value; }
        set value(v) {
            this.setAttribute('value', v == null ? 'on' : String(v));
            this._inputEl.value = v == null ? 'on' : String(v);
        }

        get name() { return this._inputEl.name; }
        set name(v) {
            if (v) this.setAttribute('name', v);
            else this.removeAttribute('name');
        }

        // 焦点代理
        focus(options) { this.focus(options); }
        blur() { this.blur(); }

        // 暴露内部元素
        get input() { return this._inputEl; }
    }

    if (!customElements.get('check-box')) customElements.define('check-box', CheckBox);
    window.CheckBox = CheckBox;
})();
