/*
 * Paperclip high-level SDK module
 * Auto released and preloaded by Paperclip host at runtime.
 */
(function (global) {
  'use strict';

  function ensureObject(name) {
    if (!global[name]) {
      global[name] = {};
    }
    return global[name];
  }

  function requireMethod(api, method) {
    var target = global[api];
    if (!target || typeof target[method] !== 'function') {
      return function () {
        throw new Error('[Paperclip] 缺少桥接方法: ' + api + '.' + method + '()');
      };
    }

    return function () {
      switch (arguments.length) {
        case 0:
          return target[method]();
        case 1:
          return target[method](arguments[0]);
        case 2:
          return target[method](arguments[0], arguments[1]);
        case 3:
          return target[method](arguments[0], arguments[1], arguments[2]);
        case 4:
          return target[method](arguments[0], arguments[1], arguments[2], arguments[3]);
        default:
          return target[method](arguments[0], arguments[1], arguments[2], arguments[3], arguments[4]);
      }
    };
  }

  function createDotNetApi() {
    var host = global.host;
    var typeCache = {};

    function requireGlobalType(name) {
      if (!global[name]) {
        throw new Error('[Paperclip] 缺少 .NET 类型导出: ' + name);
      }

      return global[name];
    }

    function ensureHostType(fullName) {
      var hostTypeResolver = host && (host.type || host.Type);
      var resolver = typeof hostTypeResolver === 'function'
        ? function (name) { return hostTypeResolver.call(host, name); }
        : null;

      if (!resolver) {
        throw new Error('[Paperclip] 未检测到 host.type()，请使用 paperclip run 运行脚本。');
      }

      if (!typeCache[fullName]) {
        typeCache[fullName] = resolver(fullName);
      }

      return typeCache[fullName];
    }

    return {
      type: ensureHostType,
      io: {
        File: function () { return global.File; },
        Directory: function () { return requireGlobalType('NetDirectory'); },
        Path: function () { return requireGlobalType('NetPath'); },
        FileInfo: function () { return requireGlobalType('NetFileInfo'); },
        DirectoryInfo: function () { return requireGlobalType('NetDirectoryInfo'); }
      },
      text: {
        Encoding: function () { return requireGlobalType('NetEncoding'); },
        StringBuilder: function () { return requireGlobalType('NetStringBuilder'); },
        JsonSerializer: function () { return requireGlobalType('NetJsonSerializer'); }
      },
      runtime: {
        Environment: function () { return requireGlobalType('NetEnvironment'); },
        DateTime: function () { return requireGlobalType('NetDateTime'); },
        Guid: function () { return requireGlobalType('NetGuid'); },
        TimeSpan: function () { return requireGlobalType('NetTimeSpan'); },
        Convert: function () { return requireGlobalType('NetConvert'); },
        Math: function () { return requireGlobalType('NetMath'); }
      }
    };
  }

  var paperclip = ensureObject('Paperclip');

  paperclip.version = '1.0.0';

  paperclip.log = {
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
  };

  paperclip.fs = {
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
  };

  paperclip.stream = {
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
  };

  paperclip.dotnet = createDotNetApi();

  paperclip.use = function () {
    return {
      log: paperclip.log,
      fs: paperclip.fs,
      stream: paperclip.stream,
      dotnet: paperclip.dotnet,
      console: global.console,
      logger: global.logger || global.Logger
    };
  };

  if (!global.$paperclip) {
    global.$paperclip = paperclip.use();
  }
})(typeof globalThis !== 'undefined' ? globalThis : this);
