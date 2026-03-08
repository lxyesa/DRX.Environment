/*
 * Paperclip bridge preset file
 * Auto released by Paperclip host at runtime.
 */
(function (global) {
  'use strict';

  function missingBridge(api, fn) {
    return function () {
      throw new Error('[Paperclip] 桥接器未注入: ' + api + '.' + fn + '()');
    };
  }

  function ensureApi(name, methods) {
    if (!global[name]) {
      global[name] = {};
    }

    for (var i = 0; i < methods.length; i++) {
      var method = methods[i];
      if (typeof global[name][method] !== 'function') {
        global[name][method] = missingBridge(name, method);
      }
    }
  }

  ensureApi('console', [
    'log', 'info', 'debug', 'warn', 'error',
    'logAsync', 'infoAsync', 'debugAsync', 'warnAsync', 'errorAsync'
  ]);

  ensureApi('Logger', [
    'log', 'info', 'debug', 'warn', 'error',
    'logAsync', 'infoAsync', 'debugAsync', 'warnAsync', 'errorAsync'
  ]);

  if (!global.logger) {
    global.logger = global.Logger;
  }

  ensureApi('File', [
    'exists',
    'readAllText', 'readAllTextAsync',
    'writeAllText', 'writeAllTextAsync',
    'appendAllText', 'appendAllTextAsync',
    'readAllBytes', 'readAllBytesAsync',
    'writeAllBytes', 'writeAllBytesAsync',
    'delete', 'deleteAsync',
    'copy', 'copyAsync',
    'move', 'moveAsync'
  ]);

  ensureApi('FileStream', [
    'openRead', 'openReadAsync',
    'openWrite', 'openWriteAsync',
    'openAppend', 'openAppendAsync',
    'readBytes', 'readBytesAsync',
    'writeBytes', 'writeBytesAsync',
    'readToEnd', 'readToEndAsync',
    'writeText', 'writeTextAsync',
    'getPosition', 'setPosition', 'getLength',
    'flush', 'flushAsync',
    'close', 'closeAsync'
  ]);
})(typeof globalThis !== 'undefined' ? globalThis : this);
