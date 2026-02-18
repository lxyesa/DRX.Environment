(function(){
    'use strict';

    const template = document.createElement('template');
    template.innerHTML = `
    <style>
      :host{display:block;} 
      .field{ border-radius:4px; border:1px solid rgba(255,255,255,0.06); padding:10px 12px; background: rgba(255,255,255,0.01); box-sizing:border-box; }
      .label{ display:block; font-size:0.82rem; color:rgba(255,255,255,0.72); margin-bottom:6px; }
      /* 分割线：放在 label 下面以增强视觉分区 */
      .label-divider{ height:1px; background:rgba(255,255,255,0.03); margin:6px 0 8px; border-radius:1px; }
      input, textarea{ width:100%; border:none; background:transparent; color:var(--muted-strong, rgba(255,255,255,0.92)); font-size:0.95rem; padding:6px 0; outline:none; font-family:inherit; }
      input:disabled, textarea:disabled{ opacity:0.6; }
      textarea{ resize:vertical; }
      :host(:focus-within) .field{ border-color: rgba(59,130,246,0.95); box-shadow: 0 0 0 4px rgba(59,130,246,0.04); }
      /* Support compact variant when host sets compact attribute */
      :host([compact]) .field{ padding:8px 10px; }
    </style>
    <div class="field">
      <label class="label" part="label"></label>
      <div class="label-divider" part="divider" aria-hidden="true"></div>
      <input part="input" />
      <textarea part="textarea" style="display:none;"></textarea>
    </div>
    `;

    class InputBox extends HTMLElement {
        static get observedAttributes(){ return ['label','placeholder','type','value','readonly','disabled','minlength','name','autocomplete','textarea','rows']; }
        constructor(){
            super();
            this._shadow = this.attachShadow({mode:'open'});
            this._shadow.appendChild(template.content.cloneNode(true));
            this._labelEl = this._shadow.querySelector('.label');
            this._inputEl = this._shadow.querySelector('input');
            this._textareaEl = this._shadow.querySelector('textarea');
            this._isTextarea = this.hasAttribute('textarea');
            this._setupElement();

            // Proxy native events so consumers can listen on the host
            ['input','change','focus','blur','keydown','keyup'].forEach(ev=>{
                const el = this._isTextarea ? this._textareaEl : this._inputEl;
                el.addEventListener(ev, (e)=>{
                    const ne = new e.constructor(e.type, e);
                    this.dispatchEvent(ne);
                });
            });
        }

        _setupElement(){
            if (this._isTextarea) {
                this._inputEl.style.display = 'none';
                this._textareaEl.style.display = '';
            } else {
                this._inputEl.style.display = '';
                this._textareaEl.style.display = 'none';
            }
        }

        connectedCallback(){
            // initialize from attributes
            this._upgradeProperty('value');
            this._syncAttributesToInput();
        }

        attributeChangedCallback(name, oldVal, newVal){
            if (oldVal === newVal) return;
            this._syncAttribute(name, newVal);
        }

        // allow setting properties before element is defined
        _upgradeProperty(prop){
            if (this.hasOwnProperty(prop)){
                let val = this[prop];
                delete this[prop];
                this[prop] = val;
            }
        }

        _syncAttributesToInput(){
            InputBox.observedAttributes.forEach(a=> this._syncAttribute(a, this.getAttribute(a)));
            // if host has id, keep it on host; internal input gets a stable internal id for accessibility
            const hostId = this.getAttribute('id');
            if (hostId && !this._inputEl.id) this._inputEl.id = hostId + '_native';
            if (hostId && !this._textareaEl.id) this._textareaEl.id = hostId + '_native';
            // wrap label click focusing already works because label is in shadow DOM and contains input
        }

        _syncAttribute(name, val){
            switch(name){
                case 'label':
                    this._labelEl.textContent = val || '';
                    break;
                case 'placeholder':
                    this._inputEl.placeholder = val || '';
                    this._textareaEl.placeholder = val || '';
                    break;
                case 'type':
                    this._inputEl.type = val || 'text';
                    break;
                case 'value':
                    if (this._inputEl.value !== (val || '')) this._inputEl.value = val || '';
                    if (this._textareaEl.value !== (val || '')) this._textareaEl.value = val || '';
                    break;
                case 'readonly':
                    if (val === null) {
                        this._inputEl.removeAttribute('readonly');
                        this._textareaEl.removeAttribute('readonly');
                    } else {
                        this._inputEl.setAttribute('readonly','');
                        this._textareaEl.setAttribute('readonly','');
                    }
                    break;
                case 'disabled':
                    if (val === null) {
                        this._inputEl.removeAttribute('disabled');
                        this._textareaEl.removeAttribute('disabled');
                    } else {
                        this._inputEl.setAttribute('disabled','');
                        this._textareaEl.setAttribute('disabled','');
                    }
                    break;
                case 'minlength':
                    if (val==null) {
                        this._inputEl.removeAttribute('minlength');
                        this._textareaEl.removeAttribute('minlength');
                    } else {
                        this._inputEl.setAttribute('minlength', val);
                        this._textareaEl.setAttribute('minlength', val);
                    }
                    break;
                case 'name':
                    if (val==null) {
                        this._inputEl.removeAttribute('name');
                        this._textareaEl.removeAttribute('name');
                    } else {
                        this._inputEl.setAttribute('name', val);
                        this._textareaEl.setAttribute('name', val);
                    }
                    break;
                case 'autocomplete':
                    if (val==null) this._inputEl.removeAttribute('autocomplete'); else this._inputEl.setAttribute('autocomplete', val);
                    break;
                case 'rows':
                    if (val==null) this._textareaEl.removeAttribute('rows'); else this._textareaEl.setAttribute('rows', val);
                    break;
                case 'textarea':
                    this._isTextarea = val !== null;
                    this._setupElement();
                    break;
            }
        }

        // proxy value property
        get value(){ 
            const el = this._isTextarea ? this._textareaEl : this._inputEl;
            return el.value; 
        }
        set value(v){ 
            this.setAttribute('value', v == null ? '' : String(v)); 
            this._inputEl.value = v == null ? '' : String(v);
            this._textareaEl.value = v == null ? '' : String(v);
        }

        // proxy disabled/readonly
        get disabled(){ return this._inputEl.disabled; }
        set disabled(v){ if (v) this.setAttribute('disabled',''); else this.removeAttribute('disabled'); }

        get readOnly(){ return this._inputEl.readOnly; }
        set readOnly(v){ if (v) this.setAttribute('readonly',''); else this.removeAttribute('readonly'); }

        // proxy focus
        focus(options){ 
            const el = this._isTextarea ? this._textareaEl : this._inputEl;
            el.focus(options); 
        }
        blur(){ 
            const el = this._isTextarea ? this._textareaEl : this._inputEl;
            el.blur(); 
        }

        // expose the internal input for advanced use (not enumerable)
        get input(){ return this._inputEl; }
        get textarea(){ return this._textareaEl; }
    }

    if (!customElements.get('input-box')) customElements.define('input-box', InputBox);
    window.InputBox = InputBox;
})();
