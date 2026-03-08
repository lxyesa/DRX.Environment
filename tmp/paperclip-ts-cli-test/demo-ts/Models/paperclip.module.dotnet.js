/*
 * Paperclip dotnet module
 */
(function (global) {
  'use strict';

  var paperclip = global.Paperclip;
  if (!paperclip || typeof paperclip.registerModule !== 'function') {
    throw new Error('[Paperclip] dotnet 模块加载失败，请先加载 paperclip.module.js');
  }

  function requireGlobalType(name) {
    if (!global[name]) {
      throw new Error('[Paperclip] 缺少 .NET 类型导出: ' + name);
    }
    return global[name];
  }

  function createDotNetApi() {
    var host = global.host;
    var typeCache = {};

    function ensureHostType(fullName) {
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
      type: ensureHostType,
      io: {
        File: function () { return requireGlobalType('File'); },
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

  paperclip.registerModule('dotnet', createDotNetApi());
})(typeof globalThis !== 'undefined' ? globalThis : this);
