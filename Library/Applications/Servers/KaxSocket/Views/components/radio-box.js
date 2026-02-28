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

      /* 隐藏原生 radio */
      input[type="radio"] {
        position: absolute;
        opacity: 0;
        width: 0;
        height: 0;
        pointer-events: none;
      }

      /* 自定义单选框容器 */
      .radio {
        flex-shrink: 0;
        width: 18px;
        height: 18px;
        border: 1px solid rgba(255,255,255,0.25);
        border-radius: 50%;
        background: rgba(255,255,255,0.02);
        display: flex;
        align-items: center;
        justify-content: center;
        transition: all 0.15s ease;
        position: relative;
      }

      :host(:hover:not([disabled])) .radio {
        border-color: rgba(255,255,255,0.4);
        background: rgba(255,255,255,0.04);
      }

      :host(:focus-within) .radio {
        border-color: rgba(59,130,246,0.8);
        box-shadow: 0 0 0 3px rgba(59,130,246,0.12);
      }

      /* 选中状态 */
      :host([checked]) .radio {
        background: rgba(59,130,246,0.9);
        border-color: rgba(59,130,246,0.9);
      }

      :host([checked]:hover:not([disabled])) .radio {
        background: rgba(59,130,246,1);
        border-color: rgba(59,130,246,1);
      }

      /* 中心圆点 */
      .radio-dot {
        width: 8px;
        height: 8px;
        background: #fff;
        border-radius: 50%;
        opacity: 0;
        transform: scale(0);
        transition: opacity 0.12s ease, transform 0.12s ease;
      }

      :host([checked]) .radio-dot {
        opacity: 1;
        transform: scale(1);
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
      :host([size="large"]) .radio { width: 22px; height: 22px; }
      :host([size="large"]) .radio-dot { width: 10px; height: 10px; }
      :host([size="large"]) .label-text { font-size: 1rem; }
      :host([size="large"]) .label-desc { font-size: 0.85rem; }

      /* size 档位：small */
      :host([size="small"]) { gap: 8px; }
      :host([size="small"]) .radio { width: 14px; height: 14px; }
      :host([size="small"]) .radio-dot { width: 6px; height: 6px; }
      :host([size="small"]) .label-text { font-size: 0.82rem; }
      :host([size="small"]) .label-desc { font-size: 0.72rem; }
      :host([size="small"]) .label-content { padding-top: 0; }
    </style>
    <input type="radio" part="input" />
    <div class="radio" part="radio">
      <span class="radio-dot" aria-hidden="true"></span>
    </div>
    <div class="label-content" part="label">
      <span class="label-text"></span>
      <span class="label-desc"></span>
    </div>
    `;

    class RadioBox extends HTMLElement {
        static get observedAttributes() {
            return ['label', 'desc', 'checked', 'disabled', 'name', 'value', 'size'];
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
                if (this.disabled || this.checked) return;
                e.preventDefault();
                this._selectThisRadio();
            });

            // 代理原生事件
            ['change', 'focus', 'blur'].forEach(ev => {
                this._inputEl.addEventListener(ev, (e) => {
                    const ne = new e.constructor(e.type, e);
                    this.dispatchEvent(ne);
                });
            });

            // 键盘支持：空格/回车键选中
            this.addEventListener('keydown', (e) => {
                if ((e.key === ' ' || e.key === 'Enter') && !this.disabled) {
                    e.preventDefault();
                    if (!this.checked) {
                        this._selectThisRadio();
                    }
                }
                // 方向键导航（同组内切换）
                if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.key) && this.name) {
                    e.preventDefault();
                    this._navigateRadioGroup(e.key === 'ArrowUp' || e.key === 'ArrowLeft' ? -1 : 1);
                }
            });

            // 使组件可聚焦
            if (!this.hasAttribute('tabindex')) {
                this.setAttribute('tabindex', '0');
            }
        }

        connectedCallback() {
            this._upgradeProperty('checked');
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
            RadioBox.observedAttributes.forEach(a => this._syncAttribute(a, this.getAttribute(a)));
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
                    break;
                case 'disabled':
                    if (val === null) {
                        this._inputEl.removeAttribute('disabled');
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
                    this._inputEl.value = val || '';
                    break;
            }
        }

        /**
         * 选中当前单选框，同时取消同组其他选项
         */
        _selectThisRadio() {
            const name = this.name;
            if (name) {
                // 取消同组内其他 radio-box 的选中状态
                const siblings = document.querySelectorAll(`radio-box[name="${name}"]`);
                siblings.forEach(radio => {
                    if (radio !== this && radio.checked) {
                        radio.checked = false;
                    }
                });
            }
            this.checked = true;
            this._inputEl.dispatchEvent(new Event('change', { bubbles: true }));
        }

        /**
         * 同组内键盘导航
         * @param {number} direction - 1 向下/右，-1 向上/左
         */
        _navigateRadioGroup(direction) {
            const name = this.name;
            if (!name) return;

            const siblings = Array.from(document.querySelectorAll(`radio-box[name="${name}"]:not([disabled])`));
            if (siblings.length <= 1) return;

            const currentIndex = siblings.indexOf(this);
            let nextIndex = currentIndex + direction;

            // 循环导航
            if (nextIndex < 0) nextIndex = siblings.length - 1;
            if (nextIndex >= siblings.length) nextIndex = 0;

            const nextRadio = siblings[nextIndex];
            if (nextRadio) {
                nextRadio.focus();
                nextRadio._selectThisRadio();
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

        get disabled() { return this.hasAttribute('disabled'); }
        set disabled(v) {
            if (v) this.setAttribute('disabled', '');
            else this.removeAttribute('disabled');
        }

        get value() { return this._inputEl.value; }
        set value(v) {
            this.setAttribute('value', v == null ? '' : String(v));
            this._inputEl.value = v == null ? '' : String(v);
        }

        get name() { return this._inputEl.name; }
        set name(v) {
            if (v) this.setAttribute('name', v);
            else this.removeAttribute('name');
        }

        // 焦点代理
        focus(options) { 
            HTMLElement.prototype.focus.call(this, options);
        }
        blur() { 
            HTMLElement.prototype.blur.call(this);
        }

        // 暴露内部元素
        get input() { return this._inputEl; }
    }

    if (!customElements.get('radio-box')) customElements.define('radio-box', RadioBox);
    window.RadioBox = RadioBox;
})();
