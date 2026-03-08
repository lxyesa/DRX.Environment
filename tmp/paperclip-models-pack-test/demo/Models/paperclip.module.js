/*
 * Paperclip core module
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

  var paperclip = ensureObject('Paperclip');
  var modules = paperclip._modules || {};

  paperclip._modules = modules;
  paperclip.version = '2.0.0';
  paperclip.requireMethod = requireMethod;

  paperclip.registerModule = function (name, moduleValue) {
    if (!name) {
      throw new Error('[Paperclip] registerModule(name, module) 的 name 不能为空。');
    }

    modules[name] = moduleValue;
    paperclip[name] = moduleValue;
    return moduleValue;
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

  if (!global.$paperclip) {
    global.$paperclip = paperclip.use();
  }
})(typeof globalThis !== 'undefined' ? globalThis : this);
