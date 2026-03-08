/*
 * Paperclip file stream module
 */
(function (global) {
  'use strict';

  var paperclip = global.Paperclip;
  if (!paperclip || typeof paperclip.registerModule !== 'function') {
    throw new Error('[Paperclip] stream 模块加载失败，请先加载 paperclip.module.js');
  }

  var requireMethod = paperclip.requireMethod;

  paperclip.registerModule('stream', {
    openRead: requireMethod('FileStream', 'openRead'),
    openReadAsync: requireMethod('FileStream', 'openReadAsync'),
    openWrite: requireMethod('FileStream', 'openWrite'),
    openWriteAsync: requireMethod('FileStream', 'openWriteAsync'),
    openAppend: requireMethod('FileStream', 'openAppend'),
    openAppendAsync: requireMethod('FileStream', 'openAppendAsync'),
    readBytes: requireMethod('FileStream', 'readBytes'),
    readBytesAsync: requireMethod('FileStream', 'readBytesAsync'),
    writeBytes: requireMethod('FileStream', 'writeBytes'),
    writeBytesAsync: requireMethod('FileStream', 'writeBytesAsync'),
    readToEnd: requireMethod('FileStream', 'readToEnd'),
    readToEndAsync: requireMethod('FileStream', 'readToEndAsync'),
    writeText: requireMethod('FileStream', 'writeText'),
    writeTextAsync: requireMethod('FileStream', 'writeTextAsync'),
    getPosition: requireMethod('FileStream', 'getPosition'),
    setPosition: requireMethod('FileStream', 'setPosition'),
    getLength: requireMethod('FileStream', 'getLength'),
    flush: requireMethod('FileStream', 'flush'),
    flushAsync: requireMethod('FileStream', 'flushAsync'),
    close: requireMethod('FileStream', 'close'),
    closeAsync: requireMethod('FileStream', 'closeAsync')
  });
})(typeof globalThis !== 'undefined' ? globalThis : this);
