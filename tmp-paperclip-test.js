console.log('typeof logger =', typeof logger);
try { console.log('logger keys =', Object.keys(logger)); } catch (e) { console.log('keys err', e.message); }
console.log('logger.info typeof =', typeof logger?.info);
