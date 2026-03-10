/**
 * collapsible-section — 可折叠分区控制器
 *
 * 用法：
 *   1. 给容器元素加 [data-section] 属性，内部须包含：
 *        .create-section-header    — 点击触发折叠/展开的按钮
 *        .create-section-body      — 使用 grid-template-rows 做动画的包裹层
 *        .create-section-body-inner — overflow:hidden 的内层
 *
 *   2. 可选属性：
 *        [data-collapsed]          — 存在时初始状态为折叠
 *
 *   3. JS API（拿到元素后调用）：
 *        CollapsibleSection.collapse(el)   — 折叠
 *        CollapsibleSection.expand(el)     — 展开
 *        CollapsibleSection.toggle(el)     — 切换
 *        CollapsibleSection.isCollapsed(el)— 返回是否折叠
 *
 *   4. 事件：
 *        section:toggle  — 切换后在容器元素上触发，detail: { collapsed: boolean }
 *
 * 示例：
 *   <div class="create-section" data-section>
 *     <button class="create-section-header" type="button" aria-expanded="true">
 *       <span class="material-icons">tune</span>
 *       <span>基本信息</span>
 *       <span class="create-section-chevron">...</span>
 *     </button>
 *     <div class="create-section-body">
 *       <div class="create-section-body-inner">
 *         <!-- 内容 -->
 *       </div>
 *     </div>
 *   </div>
 */
(function () {
    'use strict';

    const COLLAPSED_CLASS = 'is-collapsed';

    /**
     * 折叠一个分区元素
     * @param {HTMLElement} section
     */
    function collapse(section) {
        section.classList.add(COLLAPSED_CLASS);
        const header = section.querySelector('.create-section-header');
        if (header) header.setAttribute('aria-expanded', 'false');
        section.dispatchEvent(new CustomEvent('section:toggle', {
            bubbles: true,
            detail: { collapsed: true }
        }));
    }

    /**
     * 展开一个分区元素
     * @param {HTMLElement} section
     */
    function expand(section) {
        section.classList.remove(COLLAPSED_CLASS);
        const header = section.querySelector('.create-section-header');
        if (header) header.setAttribute('aria-expanded', 'true');
        section.dispatchEvent(new CustomEvent('section:toggle', {
            bubbles: true,
            detail: { collapsed: false }
        }));
    }

    /**
     * 切换一个分区元素的折叠状态
     * @param {HTMLElement} section
     */
    function toggle(section) {
        if (isCollapsed(section)) {
            expand(section);
        } else {
            collapse(section);
        }
    }

    /**
     * 判断一个分区是否处于折叠状态
     * @param {HTMLElement} section
     * @returns {boolean}
     */
    function isCollapsed(section) {
        return section.classList.contains(COLLAPSED_CLASS);
    }

    /**
     * 初始化一个 [data-section] 元素
     * @param {HTMLElement} section
     */
    function initOne(section) {
        const header = section.querySelector('.create-section-header');
        if (!header) return;

        // 根据 data-collapsed 属性决定初始态
        if (section.hasAttribute('data-collapsed')) {
            section.classList.add(COLLAPSED_CLASS);
            header.setAttribute('aria-expanded', 'false');
        } else {
            header.setAttribute('aria-expanded', 'true');
        }

        // 点击切换
        header.addEventListener('click', () => toggle(section));

        // Enter / Space 键盘支持（header 已是 <button>，浏览器默认处理 Enter；
        // 此处额外处理 Space，防止页面滚动）
        header.addEventListener('keydown', (e) => {
            if (e.key === ' ') {
                e.preventDefault();
                toggle(section);
            }
        });
    }

    /**
     * 初始化页面上所有 [data-section] 元素（或指定根元素下的）
     * @param {HTMLElement|Document} [root=document]
     */
    function init(root) {
        (root || document).querySelectorAll('[data-section]').forEach(initOne);
    }

    // 自动初始化
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => init());
    } else {
        init();
    }

    // 暴露公共 API
    window.CollapsibleSection = { init, initOne, collapse, expand, toggle, isCollapsed };
})();
