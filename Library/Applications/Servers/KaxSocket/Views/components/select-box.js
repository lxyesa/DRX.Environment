(function () {
    'use strict';

    const template = document.createElement('template');
    template.innerHTML = `
    <style>
      :host { display: block; }

      .field {
        border-radius: 4px;
        border: 1px solid rgba(255,255,255,0.06);
        padding: 10px 12px;
        background: rgba(255,255,255,0.01);
        box-sizing: border-box;
        transition: border-color 0.18s ease, box-shadow 0.18s ease;
      }

      .label {
        display: block;
        font-size: 0.82rem;
        color: rgba(255,255,255,0.72);
        margin-bottom: 6px;
      }

      .label-divider {
        height: 1px;
        background: rgba(255,255,255,0.03);
        margin: 6px 0 8px;
        border-radius: 1px;
      }

      .select-row {
        display: flex;
        align-items: center;
        gap: 8px;
        position: relative;
      }

      /* 图标 */
      .field-icon {
        display: none;
        flex-shrink: 0;
        align-items: center;
        justify-content: center;
        margin-right: 8px;
        color: rgba(255,255,255,0.45);
        font-size: 18px;
        line-height: 1;
      }
      :host([icon]) .field-icon { display: flex; }

      /* 原生 select 样式 */
      select {
        flex: 1;
        min-width: 0;
        border: none;
        background: transparent;
        color: var(--muted-strong, rgba(255,255,255,0.92));
        font-size: 0.95rem;
        padding: 6px 28px 6px 0;
        outline: none;
        font-family: inherit;
        cursor: pointer;
        -webkit-appearance: none;
        -moz-appearance: none;
        appearance: none;
      }

      select:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }

      /* 下拉箭头 */
      .chevron {
        position: absolute;
        right: 0;
        top: 50%;
        transform: translateY(-50%);
        width: 16px;
        height: 16px;
        color: rgba(255,255,255,0.35);
        pointer-events: none;
        transition: color 0.15s ease;
      }
      :host(:focus-within) .chevron { color: rgba(59,130,246,0.9); }

      .chevron svg {
        display: block;
        width: 100%;
        height: 100%;
      }

      /* option 样式（部分浏览器支持） */
      select option {
        background: #1a1a1a;
        color: rgba(255,255,255,0.92);
        padding: 8px 12px;
      }

      :host(:focus-within) .field {
        border-color: rgba(59,130,246,0.95);
        box-shadow: 0 0 0 4px rgba(59,130,246,0.04);
      }

      :host([compact]) .field { padding: 8px 10px; }

      /* size 档位：large */
      :host([size="large"]) .field { padding: 14px 16px; }
      :host([size="large"]) .label { font-size: 0.92rem; margin-bottom: 8px; }
      :host([size="large"]) select { font-size: 1.08rem; padding: 8px 32px 8px 0; }
      :host([size="large"]) .field-icon { font-size: 22px; }
      :host([size="large"]) .chevron { width: 20px; height: 20px; }

      /* size 档位：small */
      :host([size="small"]) .field { padding: 6px 8px; }
      :host([size="small"]) .label { font-size: 0.75rem; margin-bottom: 4px; }
      :host([size="small"]) .label-divider { margin: 4px 0 5px; }
      :host([size="small"]) select { font-size: 0.85rem; padding: 4px 24px 4px 0; }
      :host([size="small"]) .field-icon { font-size: 15px; margin-right: 6px; }
      :host([size="small"]) .chevron { width: 14px; height: 14px; }

      /* size 档位：headerless — 隐藏 label 和分割线 */
      :host([size="headerless"]) .label,
      :host([size="headerless"]) .label-divider { display: none; }
      :host([size="headerless"]) .field { padding: 6px 10px; }
    </style>
    <div class="field" part="field">
      <label class="label" part="label"></label>
      <div class="label-divider" part="divider" aria-hidden="true"></div>
      <div class="select-row">
        <span class="field-icon material-icons" part="icon" aria-hidden="true"></span>
        <select part="select"></select>
        <span class="chevron" aria-hidden="true">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="6 9 12 15 18 9"></polyline>
          </svg>
        </span>
      </div>
    </div>
    `;

    class SelectBox extends HTMLElement {
        static get observedAttributes() {
            return ['label', 'placeholder', 'value', 'disabled', 'name', 'size', 'icon', 'options'];
        }

        constructor() {
            super();
            this._shadow = this.attachShadow({ mode: 'open' });
            this._shadow.appendChild(template.content.cloneNode(true));
            this._labelEl = this._shadow.querySelector('.label');
            this._selectEl = this._shadow.querySelector('select');
            this._iconEl = this._shadow.querySelector('.field-icon');

            // 代理原生事件
            ['change', 'focus', 'blur'].forEach(ev => {
                this._selectEl.addEventListener(ev, (e) => {
                    const ne = new e.constructor(e.type, e);
                    this.dispatchEvent(ne);
                });
            });
        }

        connectedCallback() {
            this._upgradeProperty('value');
            this._upgradeProperty('options');
            this._syncAttributesToSelect();
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

        _syncAttributesToSelect() {
            SelectBox.observedAttributes.forEach(a => this._syncAttribute(a, this.getAttribute(a)));
        }

        _syncAttribute(name, val) {
            switch (name) {
                case 'label':
                    this._labelEl.textContent = val || '';
                    break;
                case 'placeholder':
                    this._updatePlaceholder(val);
                    break;
                case 'value':
                    if (this._selectEl.value !== (val || '')) {
                        this._selectEl.value = val || '';
                    }
                    break;
                case 'disabled':
                    if (val === null) {
                        this._selectEl.removeAttribute('disabled');
                    } else {
                        this._selectEl.setAttribute('disabled', '');
                    }
                    break;
                case 'name':
                    if (val == null) {
                        this._selectEl.removeAttribute('name');
                    } else {
                        this._selectEl.setAttribute('name', val);
                    }
                    break;
                case 'icon':
                    if (this._iconEl) this._iconEl.textContent = val || '';
                    break;
                case 'options':
                    this._renderOptions(val);
                    break;
            }
        }

        _updatePlaceholder(placeholder) {
            // 如果有占位符，在选项最前面添加一个禁用的占位选项
            const existingPlaceholder = this._selectEl.querySelector('option[data-placeholder]');
            if (placeholder) {
                if (existingPlaceholder) {
                    existingPlaceholder.textContent = placeholder;
                } else {
                    const opt = document.createElement('option');
                    opt.value = '';
                    opt.textContent = placeholder;
                    opt.disabled = true;
                    opt.selected = true;
                    opt.setAttribute('data-placeholder', '');
                    this._selectEl.insertBefore(opt, this._selectEl.firstChild);
                }
            } else if (existingPlaceholder) {
                existingPlaceholder.remove();
            }
        }

        /**
         * 渲染选项
         * @param {string|Array} options - JSON 字符串或数组
         * 支持格式：
         * - ["选项1", "选项2"] - 简单字符串数组
         * - [{value: "val1", label: "显示1"}, ...] - 对象数组
         * - [{value: "val1", label: "显示1", disabled: true}, ...] - 带禁用状态
         */
        _renderOptions(options) {
            if (!options) return;

            let optionsArray;
            if (typeof options === 'string') {
                try {
                    optionsArray = JSON.parse(options);
                } catch (e) {
                    console.warn('SelectBox: Invalid options JSON', e);
                    return;
                }
            } else {
                optionsArray = options;
            }

            if (!Array.isArray(optionsArray)) return;

            // 保留 placeholder 选项
            const placeholder = this._selectEl.querySelector('option[data-placeholder]');
            const currentValue = this._selectEl.value;

            // 清空现有选项（保留 placeholder）
            while (this._selectEl.firstChild) {
                if (this._selectEl.firstChild.hasAttribute && this._selectEl.firstChild.hasAttribute('data-placeholder')) {
                    break;
                }
                if (this._selectEl.firstChild === placeholder) {
                    break;
                }
                this._selectEl.removeChild(this._selectEl.firstChild);
            }
            // 移除 placeholder 之后的所有选项
            while (placeholder && placeholder.nextSibling) {
                placeholder.nextSibling.remove();
            }

            optionsArray.forEach(item => {
                const opt = document.createElement('option');
                if (typeof item === 'string') {
                    opt.value = item;
                    opt.textContent = item;
                } else if (typeof item === 'object' && item !== null) {
                    opt.value = item.value ?? '';
                    opt.textContent = item.label ?? item.value ?? '';
                    if (item.disabled) opt.disabled = true;
                }
                this._selectEl.appendChild(opt);
            });

            // 恢复之前的值
            if (currentValue) {
                this._selectEl.value = currentValue;
            }
        }

        /**
         * 通过 JavaScript 设置选项
         * @param {Array} options - 选项数组
         */
        setOptions(options) {
            this._renderOptions(options);
        }

        // 属性代理
        get value() { return this._selectEl.value; }
        set value(v) {
            this.setAttribute('value', v == null ? '' : String(v));
            this._selectEl.value = v == null ? '' : String(v);
        }

        get disabled() { return this._selectEl.disabled; }
        set disabled(v) {
            if (v) this.setAttribute('disabled', '');
            else this.removeAttribute('disabled');
        }

        get options() { return this._selectEl.options; }
        set options(v) {
            this._renderOptions(v);
        }

        get selectedIndex() { return this._selectEl.selectedIndex; }
        set selectedIndex(v) { this._selectEl.selectedIndex = v; }

        get selectedOptions() { return this._selectEl.selectedOptions; }

        // 焦点代理
        focus(options) { this._selectEl.focus(options); }
        blur() { this._selectEl.blur(); }

        // 暴露内部元素
        get select() { return this._selectEl; }
    }

    if (!customElements.get('select-box')) customElements.define('select-box', SelectBox);
    window.SelectBox = SelectBox;
})();
