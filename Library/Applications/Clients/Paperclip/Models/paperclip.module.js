/**
 * 模块名: paperclip.module.js
 * 职责: Paperclip 单文件 SDK 模块，内置桥接兜底、模块系统与 log/fs/stream/dotnet 子模块。
 * 依赖: 全局桥接对象（console/logger/File/FileStream/host）与 Net* 类型导出。
 */
(function (global) {
  'use strict';

  function ensureObject(name) {
    if (!global[name] || typeof global[name] !== 'object') {
      global[name] = {};
    }
    return global[name];
  }

  function toArray(argsLike) {
    return Array.prototype.slice.call(argsLike);
  }

  function invokeMethod(target, method, argsLike) {
    switch (argsLike.length) {
      case 0:
        return target[method]();
      case 1:
        return target[method](argsLike[0]);
      case 2:
        return target[method](argsLike[0], argsLike[1]);
      case 3:
        return target[method](argsLike[0], argsLike[1], argsLike[2]);
      case 4:
        return target[method](argsLike[0], argsLike[1], argsLike[2], argsLike[3]);
      default:
        return target[method](argsLike[0], argsLike[1], argsLike[2], argsLike[3], argsLike[4]);
    }
  }

  function createMissingBridge(api, method) {
    return function () {
      throw new Error('[Paperclip] 缺少桥接方法: ' + api + '.' + method + '()');
    };
  }

  function requireBridge(api, method) {
    var target = global[api];
    if (!target || typeof target[method] !== 'function') {
      return createMissingBridge(api, method);
    }

    return function () {
      return invokeMethod(target, method, toArray(arguments));
    };
  }

  function missingBridge(api, method) {
    return function () {
      throw new Error('[Paperclip] 桥接器未注入: ' + api + '.' + method + '()');
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

  function aliasApi(name, alias, source) {
    if (!global[name]) {
      global[name] = {};
    }

    var hasAlias = false;
    var hasSource = false;

    try {
      hasAlias = typeof global[name][alias] === 'function';
    } catch (e) {
      hasAlias = false;
    }

    try {
      hasSource = typeof global[name][source] === 'function';
    } catch (e) {
      hasSource = false;
    }

    if (!hasAlias && hasSource) {
      try {
        global[name][alias] = function () {
          return invokeMethod(global[name], source, arguments);
        };
      } catch (e) {
        // Host object may be non-extensible.
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

  aliasApi('Logger', 'print', 'log');
  aliasApi('Logger', 'trace', 'debug');
  aliasApi('Logger', 'success', 'info');
  aliasApi('Logger', 'fatal', 'error');
  aliasApi('logger', 'print', 'log');
  aliasApi('logger', 'trace', 'debug');
  aliasApi('logger', 'success', 'info');
  aliasApi('logger', 'fatal', 'error');

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

  aliasApi('File', 'readFile', 'readAllText');
  aliasApi('File', 'readFileAsync', 'readAllTextAsync');
  aliasApi('File', 'writeFile', 'writeAllText');
  aliasApi('File', 'writeFileAsync', 'writeAllTextAsync');
  aliasApi('File', 'appendFile', 'appendAllText');
  aliasApi('File', 'appendFileAsync', 'appendAllTextAsync');
  aliasApi('File', 'readBinary', 'readAllBytes');
  aliasApi('File', 'readBinaryAsync', 'readAllBytesAsync');
  aliasApi('File', 'writeBinary', 'writeAllBytes');
  aliasApi('File', 'writeBinaryAsync', 'writeAllBytesAsync');
  aliasApi('File', 'deleteFile', 'delete');
  aliasApi('File', 'deleteFileAsync', 'deleteAsync');

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

  aliasApi('FileStream', 'createReadStream', 'openRead');
  aliasApi('FileStream', 'createReadStreamAsync', 'openReadAsync');
  aliasApi('FileStream', 'createWriteStream', 'openWrite');
  aliasApi('FileStream', 'createWriteStreamAsync', 'openWriteAsync');
  aliasApi('FileStream', 'createAppendStream', 'openAppend');
  aliasApi('FileStream', 'createAppendStreamAsync', 'openAppendAsync');
  aliasApi('FileStream', 'read', 'readBytes');
  aliasApi('FileStream', 'readAsync', 'readBytesAsync');
  aliasApi('FileStream', 'write', 'writeBytes');
  aliasApi('FileStream', 'writeAsync', 'writeBytesAsync');
  aliasApi('FileStream', 'readText', 'readToEnd');
  aliasApi('FileStream', 'readTextAsync', 'readToEndAsync');
  aliasApi('FileStream', 'position', 'getPosition');
  aliasApi('FileStream', 'seek', 'setPosition');
  aliasApi('FileStream', 'length', 'getLength');

  var paperclip = ensureObject('Paperclip');
  var modules = paperclip._modules || Object.create(null);
  paperclip._modules = modules;
  paperclip.version = paperclip.version || '2.2.0';

  paperclip.requireMethod = requireBridge;
  paperclip.requireBridge = requireBridge;

  paperclip.registerModule = function (name, moduleValue, aliases) {
    if (!name || typeof name !== 'string') {
      throw new Error('[Paperclip] registerModule(name, module) 的 name 不能为空。');
    }
    if (!moduleValue || typeof moduleValue !== 'object') {
      throw new Error('[Paperclip] registerModule(name, module) 的 module 不能为空。');
    }

    modules[name] = moduleValue;
    paperclip[name] = moduleValue;

    if (Array.isArray(aliases)) {
      for (var i = 0; i < aliases.length; i++) {
        var alias = aliases[i];
        if (alias && typeof alias === 'string') {
          modules[alias] = moduleValue;
          paperclip[alias] = moduleValue;
        }
      }
    }

    return moduleValue;
  };

  paperclip.createModule = function (name, factory, aliases) {
    if (typeof factory !== 'function') {
      throw new Error('[Paperclip] createModule(name, factory) 的 factory 必须是函数。');
    }

    var moduleValue = factory({
      global: global,
      paperclip: paperclip,
      requireBridge: requireBridge,
      registerModule: paperclip.registerModule
    });

    return paperclip.registerModule(name, moduleValue, aliases);
  };

  paperclip.getModule = function (name) {
    return modules[name];
  };

  paperclip.use = function (names) {
    var bag = {
      console: global.console,
      logger: global.logger || global.Logger
    };

    if (Array.isArray(names) && names.length > 0) {
      for (var i = 0; i < names.length; i++) {
        var key = names[i];
        if (modules[key]) {
          bag[key] = modules[key];
        }
      }
      return bag;
    }

    var moduleNames = Object.keys(modules);
    for (var j = 0; j < moduleNames.length; j++) {
      var moduleName = moduleNames[j];
      bag[moduleName] = modules[moduleName];
    }

    return bag;
  };

  paperclip.createModule('log', function () {
    var api = {
      log: requireBridge('Logger', 'log'),
      info: requireBridge('Logger', 'info'),
      debug: requireBridge('Logger', 'debug'),
      warn: requireBridge('Logger', 'warn'),
      error: requireBridge('Logger', 'error'),
      logAsync: requireBridge('Logger', 'logAsync'),
      infoAsync: requireBridge('Logger', 'infoAsync'),
      debugAsync: requireBridge('Logger', 'debugAsync'),
      warnAsync: requireBridge('Logger', 'warnAsync'),
      errorAsync: requireBridge('Logger', 'errorAsync')
    };

    api.print = api.log;
    api.trace = api.debug;
    api.success = api.info;
    api.fatal = api.error;
    api.traceAsync = api.debugAsync;
    api.successAsync = api.infoAsync;
    api.fatalAsync = api.errorAsync;
    return api;
  });

  paperclip.createModule('fs', function () {
    var api = {
      exists: requireBridge('File', 'exists'),
      readFile: requireBridge('File', 'readAllText'),
      readFileAsync: requireBridge('File', 'readAllTextAsync'),
      writeFile: requireBridge('File', 'writeAllText'),
      writeFileAsync: requireBridge('File', 'writeAllTextAsync'),
      appendFile: requireBridge('File', 'appendAllText'),
      appendFileAsync: requireBridge('File', 'appendAllTextAsync'),
      readBinary: requireBridge('File', 'readAllBytes'),
      readBinaryAsync: requireBridge('File', 'readAllBytesAsync'),
      writeBinary: requireBridge('File', 'writeAllBytes'),
      writeBinaryAsync: requireBridge('File', 'writeAllBytesAsync'),
      deleteFile: requireBridge('File', 'delete'),
      deleteFileAsync: requireBridge('File', 'deleteAsync'),
      copy: requireBridge('File', 'copy'),
      copyAsync: requireBridge('File', 'copyAsync'),
      move: requireBridge('File', 'move'),
      moveAsync: requireBridge('File', 'moveAsync')
    };

    api.readText = api.readFile;
    api.readTextAsync = api.readFileAsync;
    api.writeText = api.writeFile;
    api.writeTextAsync = api.writeFileAsync;
    api.appendText = api.appendFile;
    api.appendTextAsync = api.appendFileAsync;
    api.readBytes = api.readBinary;
    api.readBytesAsync = api.readBinaryAsync;
    api.writeBytes = api.writeBinary;
    api.writeBytesAsync = api.writeBinaryAsync;
    api.remove = api.deleteFile;
    api.removeAsync = api.deleteFileAsync;
    api.copyTo = api.copy;
    api.copyToAsync = api.copyAsync;
    api.moveTo = api.move;
    api.moveToAsync = api.moveAsync;
    return api;
  });

  paperclip.createModule('stream', function () {
    var api = {
      createReadStream: requireBridge('FileStream', 'openRead'),
      createReadStreamAsync: requireBridge('FileStream', 'openReadAsync'),
      createWriteStream: requireBridge('FileStream', 'openWrite'),
      createWriteStreamAsync: requireBridge('FileStream', 'openWriteAsync'),
      createAppendStream: requireBridge('FileStream', 'openAppend'),
      createAppendStreamAsync: requireBridge('FileStream', 'openAppendAsync'),
      read: requireBridge('FileStream', 'readBytes'),
      readAsync: requireBridge('FileStream', 'readBytesAsync'),
      write: requireBridge('FileStream', 'writeBytes'),
      writeAsync: requireBridge('FileStream', 'writeBytesAsync'),
      readText: requireBridge('FileStream', 'readToEnd'),
      readTextAsync: requireBridge('FileStream', 'readToEndAsync'),
      writeText: requireBridge('FileStream', 'writeText'),
      writeTextAsync: requireBridge('FileStream', 'writeTextAsync'),
      position: requireBridge('FileStream', 'getPosition'),
      seek: requireBridge('FileStream', 'setPosition'),
      length: requireBridge('FileStream', 'getLength'),
      flush: requireBridge('FileStream', 'flush'),
      flushAsync: requireBridge('FileStream', 'flushAsync'),
      close: requireBridge('FileStream', 'close'),
      closeAsync: requireBridge('FileStream', 'closeAsync')
    };

    api.openRead = api.createReadStream;
    api.openReadAsync = api.createReadStreamAsync;
    api.openWrite = api.createWriteStream;
    api.openWriteAsync = api.createWriteStreamAsync;
    api.openAppend = api.createAppendStream;
    api.openAppendAsync = api.createAppendStreamAsync;
    api.readBytes = api.read;
    api.readBytesAsync = api.readAsync;
    api.writeBytes = api.write;
    api.writeBytesAsync = api.writeAsync;
    api.readToEnd = api.readText;
    api.readToEndAsync = api.readTextAsync;
    api.getPosition = api.position;
    api.setPosition = api.seek;
    api.getLength = api.length;
    return api;
  });

  paperclip.createModule('dotnet', function () {
    var host = global.host;
    var typeCache = Object.create(null);

    function requireGlobalType(name) {
      if (!global[name]) {
        throw new Error('[Paperclip] 缺少 .NET 类型导出: ' + name);
      }
      return global[name];
    }

    function resolveHostType(fullName) {
      var hostTypeResolver = host && (host.type || host.Type);
      if (typeof hostTypeResolver !== 'function') {
        throw new Error('[Paperclip] 未检测到 host.type()，请使用 paperclip run 运行脚本。');
      }

      if (!typeCache[fullName]) {
        typeCache[fullName] = hostTypeResolver.call(host, fullName);
      }

      return typeCache[fullName];
    }

    return {
      type: resolveHostType,
      requireType: resolveHostType,
      io: {
        File: function () { return requireGlobalType('NetFile'); },
        file: function () { return requireGlobalType('NetFile'); },
        BridgeFile: function () { return requireGlobalType('File'); },
        bridgeFile: function () { return requireGlobalType('File'); },
        Directory: function () { return requireGlobalType('NetDirectory'); },
        directory: function () { return requireGlobalType('NetDirectory'); },
        Path: function () { return requireGlobalType('NetPath'); },
        path: function () { return requireGlobalType('NetPath'); },
        FileInfo: function () { return requireGlobalType('NetFileInfo'); },
        fileInfo: function () { return requireGlobalType('NetFileInfo'); },
        DirectoryInfo: function () { return requireGlobalType('NetDirectoryInfo'); },
        directoryInfo: function () { return requireGlobalType('NetDirectoryInfo'); }
      },
      text: {
        Encoding: function () { return requireGlobalType('NetEncoding'); },
        encoding: function () { return requireGlobalType('NetEncoding'); },
        StringBuilder: function () { return requireGlobalType('NetStringBuilder'); },
        stringBuilder: function () { return requireGlobalType('NetStringBuilder'); },
        JsonSerializer: function () { return requireGlobalType('NetJsonSerializer'); },
        jsonSerializer: function () { return requireGlobalType('NetJsonSerializer'); },
        Regex: function () { return requireGlobalType('NetRegex'); },
        regex: function () { return requireGlobalType('NetRegex'); }
      },
      runtime: {
        Environment: function () { return requireGlobalType('NetEnvironment'); },
        environment: function () { return requireGlobalType('NetEnvironment'); },
        DateTime: function () { return requireGlobalType('NetDateTime'); },
        dateTime: function () { return requireGlobalType('NetDateTime'); },
        DateOnly: function () { return requireGlobalType('NetDateOnly'); },
        dateOnly: function () { return requireGlobalType('NetDateOnly'); },
        TimeOnly: function () { return requireGlobalType('NetTimeOnly'); },
        timeOnly: function () { return requireGlobalType('NetTimeOnly'); },
        Guid: function () { return requireGlobalType('NetGuid'); },
        guid: function () { return requireGlobalType('NetGuid'); },
        TimeSpan: function () { return requireGlobalType('NetTimeSpan'); },
        timeSpan: function () { return requireGlobalType('NetTimeSpan'); },
        Convert: function () { return requireGlobalType('NetConvert'); },
        convert: function () { return requireGlobalType('NetConvert'); },
        Math: function () { return requireGlobalType('NetMath'); },
        math: function () { return requireGlobalType('NetMath'); },
        Console: function () { return requireGlobalType('NetConsole'); },
        console: function () { return requireGlobalType('NetConsole'); },
        Uri: function () { return requireGlobalType('NetUri'); },
        uri: function () { return requireGlobalType('NetUri'); },
        Random: function () { return requireGlobalType('NetRandom'); },
        random: function () { return requireGlobalType('NetRandom'); }
      },
      linq: {
        Enumerable: function () { return requireGlobalType('NetEnumerable'); },
        enumerable: function () { return requireGlobalType('NetEnumerable'); }
      },
      diagnostics: {
        Process: function () { return requireGlobalType('NetProcess'); },
        process: function () { return requireGlobalType('NetProcess'); },
        Stopwatch: function () { return requireGlobalType('NetStopwatch'); },
        stopwatch: function () { return requireGlobalType('NetStopwatch'); }
      },
      globalization: {
        CultureInfo: function () { return requireGlobalType('NetCultureInfo'); },
        cultureInfo: function () { return requireGlobalType('NetCultureInfo'); }
      },
      threading: {
        Task: function () { return requireGlobalType('NetTask'); },
        task: function () { return requireGlobalType('NetTask'); },
        CancellationTokenSource: function () { return requireGlobalType('NetCancellationTokenSource'); },
        cancellationTokenSource: function () { return requireGlobalType('NetCancellationTokenSource'); }
      },
      net: {
        HttpClient: function () { return requireGlobalType('NetHttpClient'); },
        httpClient: function () { return requireGlobalType('NetHttpClient'); },
        HttpRequestMessage: function () { return requireGlobalType('NetHttpRequestMessage'); },
        httpRequestMessage: function () { return requireGlobalType('NetHttpRequestMessage'); },
        HttpMethod: function () { return requireGlobalType('NetHttpMethod'); },
        httpMethod: function () { return requireGlobalType('NetHttpMethod'); },
        HttpStatusCode: function () { return requireGlobalType('NetHttpStatusCode'); },
        httpStatusCode: function () { return requireGlobalType('NetHttpStatusCode'); }
      }
    };
  });

  if (!global.$paperclip) {
    Object.defineProperty(global, '$paperclip', {
      configurable: true,
      enumerable: false,
      get: function () {
        return paperclip.use();
      }
    });
  }
})(typeof globalThis !== 'undefined' ? globalThis : this);
