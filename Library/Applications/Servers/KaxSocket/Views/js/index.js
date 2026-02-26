// 使用全局脚本注入统一 footer（由 global.js 提供）
        if (window.initGlobalFooter) window.initGlobalFooter();
        else {
            var _s = document.createElement('script');
            _s.src = '/global.js';
            _s.onload = function () { window.initGlobalFooter && window.initGlobalFooter(); };
            document.head.appendChild(_s);
        }

// 平滑滚动到锚点
        document.querySelectorAll('a[href^="#"]').forEach(anchor => {
            anchor.addEventListener('click', function (e) {
                e.preventDefault();
                document.querySelector(this.getAttribute('href')).scrollIntoView({
                    behavior: 'smooth'
                });
            });
        });
