/*
 * Paperclip log module
 */
(function (global) {
  'use strict';

  var paperclip = global.Paperclip;
  if (!paperclip || typeof paperclip.registerModule !== 'function') {
    throw new Error('[Paperclip] log 模块加载失败，请先加载 paperclip.module.js');
  }

  var requireMethod = paperclip.requireMethod;

  paperclip.registerModule('log', {
    log: requireMethod('Logger', 'log'),
    info: requireMethod('Logger', 'info'),
    debug: requireMethod('Logger', 'debug'),
    warn: requireMethod('Logger', 'warn'),
    error: requireMethod('Logger', 'error'),
    logAsync: requireMethod('Logger', 'logAsync'),
    infoAsync: requireMethod('Logger', 'infoAsync'),
    debugAsync: requireMethod('Logger', 'debugAsync'),
    warnAsync: requireMethod('Logger', 'warnAsync'),
    errorAsync: requireMethod('Logger', 'errorAsync')
  });
})(typeof globalThis !== 'undefined' ? globalThis : this);
