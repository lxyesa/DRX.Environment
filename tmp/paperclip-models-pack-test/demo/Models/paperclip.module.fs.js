/*
 * Paperclip file system module
 */
(function (global) {
  'use strict';

  var paperclip = global.Paperclip;
  if (!paperclip || typeof paperclip.registerModule !== 'function') {
    throw new Error('[Paperclip] fs 模块加载失败，请先加载 paperclip.module.js');
  }

  var requireMethod = paperclip.requireMethod;

  paperclip.registerModule('fs', {
    exists: requireMethod('File', 'exists'),
    readText: requireMethod('File', 'readAllText'),
    readTextAsync: requireMethod('File', 'readAllTextAsync'),
    writeText: requireMethod('File', 'writeAllText'),
    writeTextAsync: requireMethod('File', 'writeAllTextAsync'),
    appendText: requireMethod('File', 'appendAllText'),
    appendTextAsync: requireMethod('File', 'appendAllTextAsync'),
    readBytes: requireMethod('File', 'readAllBytes'),
    readBytesAsync: requireMethod('File', 'readAllBytesAsync'),
    writeBytes: requireMethod('File', 'writeAllBytes'),
    writeBytesAsync: requireMethod('File', 'writeAllBytesAsync'),
    remove: requireMethod('File', 'delete'),
    removeAsync: requireMethod('File', 'deleteAsync'),
    copyTo: requireMethod('File', 'copy'),
    copyToAsync: requireMethod('File', 'copyAsync'),
    moveTo: requireMethod('File', 'move'),
    moveToAsync: requireMethod('File', 'moveAsync')
  });
})(typeof globalThis !== 'undefined' ? globalThis : this);
