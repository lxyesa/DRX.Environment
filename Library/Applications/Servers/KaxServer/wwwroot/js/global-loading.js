(function () {
  var overlay = null;
  var slowTimer = null;

  // 新增：最小显示时长控制
  var MIN_VISIBLE_MS = 200;           // 至少显示 1.5s
  var visibleSince = 0;                // 本次显示的起始时间戳
  var pendingHide = null;              // 若过早调用 hide，则延后执行的定时器
  var refCount = 0;                    // 多重 show/hide 计数，避免闪烁

  function ensureOverlay() {
    if (overlay) return overlay;
    overlay = document.getElementById('global-loading-overlay');
    return overlay;
  }

  function actuallyShow() {
    var el = ensureOverlay();
    if (!el) return;
    if (!el.classList.contains('show')) {
      el.classList.remove('hiding'); // 取消正在退出的过渡态
      el.style.removeProperty('display'); // 修正可能的 display:none 内联
      el.classList.add('show');
    }
  }

  function actuallyHide() {
    var el = ensureOverlay();
    if (!el) return;
    // 添加淡出类，配合 CSS 过渡
    el.classList.add('hiding');
    el.classList.remove('show');

    // 过渡结束后彻底隐藏（不强制依赖 transitionend，兜底时间与 CSS exit-ms 对齐）
    window.setTimeout(function () {
      if (el.classList.contains('hiding')) {
        el.classList.remove('hiding');
        // 保持可通过 .show 控制，因此不直接写 display:none；CSS 可控
      }
    }, 260);
  }

  function showOverlay() {
    var el = ensureOverlay();
    if (!el) return;

    // 清理可能的延后隐藏
    if (pendingHide) {
      clearTimeout(pendingHide);
      pendingHide = null;
    }

    // 首次从 0 -> 1 时记录开始时间
    if (refCount === 0) {
      visibleSince = performance.now();
      // 重置慢速提示
      clearTimeout(slowTimer);
      slowTimer = setTimeout(function () {
        // 可选：在 UI 里提示网络较慢
        // var text = el.querySelector('.global-loading-text');
        // if (text && !text.dataset.slowShown) {
        //   text.textContent = '正在加载…（网络较慢）';
        //   text.dataset.slowShown = '1';
        // }
      }, 10000);
    }

    refCount++;
    actuallyShow();
  }

  function hideOverlay() {
    var el = ensureOverlay();
    if (!el) return;

    // 仅在计数大于 0 时尝试减少
    if (refCount > 0) refCount--;

    // 仅当 refCount 归零才真正隐藏
    if (refCount === 0) {
      var elapsed = performance.now() - visibleSince;
      var remain = Math.max(0, MIN_VISIBLE_MS - elapsed);

      clearTimeout(slowTimer);
      // 重置“慢”提示文本
      // var text = el.querySelector('.global-loading-text');
      // if (text) {
      //   text.textContent = '正在加载…';
      //   delete text.dataset.slowShown;
      // }

      if (pendingHide) {
        clearTimeout(pendingHide);
        pendingHide = null;
      }

      if (remain === 0) {
        actuallyHide();
      } else {
        pendingHide = setTimeout(function () {
          actuallyHide();
          pendingHide = null;
        }, remain);
      }
    }
  }

  // 导出方法（支持局部流程控制与复用）
  window.showGlobalLoading = showOverlay;
  window.hideGlobalLoading = hideOverlay;

  // 页面开始卸载或跳转时显示（早显示）
  window.addEventListener('beforeunload', function () {
    showOverlay();
  });

  // 修复“加载闪一下就没了”：
  // 1) 初始化即显示，确保首屏至少显示 1.5s
  // 2) 将 DOMContentLoaded 的立即隐藏改为由 load 统一收口
  // 立即显示（开始计时）
  showOverlay();
  // window.addEventListener('DOMContentLoaded', function () {
  //   hideOverlay();
  // });

  // 资源完全加载后再兜底隐藏一次
  window.addEventListener('load', function () {
    hideOverlay();
  });

  // 从 bfcache 恢复：通常无需再次显示，直接确保隐藏并清理计时
  window.addEventListener('pageshow', function (e) {
    if (e.persisted) {
      refCount = 0;
      if (pendingHide) { clearTimeout(pendingHide); pendingHide = null; }
      // 直接结束过渡
      (function () { actuallyHide(); })();
    }
  });

  // 拦截所有 a 标签的常规跳转（同窗口、非锚点、非下载、非 js:）
  document.addEventListener('click', function (e) {
    var a = e.target && e.target.closest ? e.target.closest('a') : null;
    if (!a) return;

    var href = a.getAttribute('href');
    if (!href) return;
    if (href.startsWith('#')) return;                 // 锚点
    if (a.target && a.target !== '_self') return;     // 新窗口不处理
    var rel = (a.getAttribute('rel') || '').toLowerCase();
    if (rel.includes('download')) return;             // 下载链接不处理

    var lower = href.toLowerCase();
    if (lower.startsWith('mailto:') || lower.startsWith('tel:') || lower.startsWith('javascript:')) return;

    // 合法跳转：显示遮罩
    showOverlay();
  }, true);

  // 表单同步提交时显示遮罩
  document.addEventListener('submit', function () {
    showOverlay();
  }, true);

  // 可选：若使用 jQuery，支持 Ajax 统一 Loading（通常用于局部刷新）
  if (window.jQuery) {
    (function ($) {
      $(document).ajaxStart(function () { showOverlay(); });
      $(document).ajaxStop(function () { hideOverlay(); });
      $(document).ajaxError(function () { hideOverlay(); });
    })(window.jQuery);
  }
})();