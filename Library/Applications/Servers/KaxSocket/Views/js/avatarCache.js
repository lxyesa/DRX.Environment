/**
 * 头像缓存模块
 * 使用浏览器 Cache API 存储头像图片数据，localStorage 记录版本号。
 * topbar 与 profile 共享同一份缓存，避免重复拉取头像。
 *
 * 缓存策略：
 *   服务器下发的头像 URL 格式为 /api/user/avatar/{userId}?v={stamp}，
 *   其中 ?v= 参数在头像更新时会变化，等价于 CRC 校验。
 *   若本地缓存的版本号与服务器一致，则直接使用缓存图片；
 *   若不一致或无缓存，则从服务器拉取并更新缓存。
 *   所有命中/未命中均会输出到浏览器控制台用于 debug。
 */
(function () {
    'use strict';

    var CACHE_NAME = 'kax-avatar-cache';
    var VERSION_KEY = 'kax_avatar_versions';
    var TAG = '[AvatarCache]';

    /** Cache API 是否可用（仅在安全上下文 HTTPS / localhost 下存在） */
    var hasCacheApi = (typeof caches !== 'undefined');

    /** 从头像 URL 中解析 userId 和版本号 */
    function parseAvatarUrl(url) {
        if (!url) return null;
        var match = url.match(/\/api\/user\/avatar\/(\d+)(?:\?v=(\w+))?/);
        if (!match) return null;
        return { userId: match[1], version: match[2] || '0' };
    }

    /** 读取 localStorage 中的版本映射表 */
    function getVersionMap() {
        try {
            return JSON.parse(localStorage.getItem(VERSION_KEY) || '{}');
        } catch (e) { return {}; }
    }

    /** 写入版本映射表到 localStorage */
    function setVersionMap(map) {
        try {
            localStorage.setItem(VERSION_KEY, JSON.stringify(map));
        } catch (e) { /* 忽略写入失败 */ }
    }

    /** 构造统一的缓存键（不含版本参数，按 userId 唯一） */
    function cacheKeyForUser(userId) {
        return 'https://avatar-cache/' + userId;
    }

    /**
     * 获取头像：优先使用缓存，版本不一致时从服务器拉取
     * @param {string} avatarUrl 服务器下发的完整头像 URL（含 ?v=）
     * @returns {Promise<string>} 返回可直接设为 img.src 的 URL（Object URL 或原始 URL）
     */
    async function getAvatar(avatarUrl) {
        var parsed = parseAvatarUrl(avatarUrl);
        if (!parsed) {
            console.log(TAG, '非标准头像URL，跳过缓存:', avatarUrl);
            return avatarUrl;
        }

        var userId = parsed.userId;
        var serverVersion = parsed.version;
        var versionMap = getVersionMap();
        var cachedVersion = versionMap[userId];

        if (hasCacheApi && cachedVersion && cachedVersion === serverVersion) {
            try {
                var cache = await caches.open(CACHE_NAME);
                var cachedResp = await cache.match(cacheKeyForUser(userId));
                if (cachedResp) {
                    var blob = await cachedResp.blob();
                    var objectUrl = URL.createObjectURL(blob);
                    console.log(TAG, '缓存命中 ✓', 'userId=' + userId, 'version=' + serverVersion);
                    return objectUrl;
                }
            } catch (e) {
                console.warn(TAG, '缓存读取失败，将重新拉取:', e.message);
            }
        }

        var reason = !hasCacheApi
            ? '非安全上下文，Cache API 不可用'
            : cachedVersion
                ? ('版本不一致 cached=' + cachedVersion + ' server=' + serverVersion)
                : '无本地缓存';
        console.log(TAG, '从服务器拉取 ↓', 'userId=' + userId, 'reason=' + reason);

        try {
            var resp = await fetch(avatarUrl);
            if (!resp.ok) throw new Error('HTTP ' + resp.status);

            var blob = await resp.blob();

            if (hasCacheApi) {
                var cache = await caches.open(CACHE_NAME);
                await cache.put(cacheKeyForUser(userId), new Response(blob.slice(), {
                    headers: { 'Content-Type': blob.type || 'image/png' }
                }));
            }

            versionMap[userId] = serverVersion;
            setVersionMap(versionMap);

            var objectUrl = URL.createObjectURL(blob);
            console.log(TAG, '拉取成功' + (hasCacheApi ? '并已缓存' : '') + ' ✓', 'userId=' + userId, 'version=' + serverVersion);
            return objectUrl;
        } catch (e) {
            console.error(TAG, '拉取失败 ✗', 'userId=' + userId, e.message);
            return avatarUrl;
        }
    }

    /**
     * 头像上传成功后，主动更新缓存（避免上传后再次拉取）
     * @param {string} newAvatarUrl 上传后服务器返回的新头像 URL
     * @param {Blob|File} imageBlob 上传使用的原始文件
     */
    async function updateCache(newAvatarUrl, imageBlob) {
        var parsed = parseAvatarUrl(newAvatarUrl);
        if (!parsed) return;

        try {
            if (hasCacheApi) {
                var cache = await caches.open(CACHE_NAME);
                await cache.put(cacheKeyForUser(parsed.userId), new Response(imageBlob.slice(), {
                    headers: { 'Content-Type': imageBlob.type || 'image/png' }
                }));
            }

            var versionMap = getVersionMap();
            versionMap[parsed.userId] = parsed.version;
            setVersionMap(versionMap);

            console.log(TAG, '上传后主动更新缓存 ✓', 'userId=' + parsed.userId, 'version=' + parsed.version);
        } catch (e) {
            console.warn(TAG, '上传后更新缓存失败:', e.message);
        }
    }

    async function clearCache() {
        try {
            if (hasCacheApi) await caches.delete(CACHE_NAME);
            localStorage.removeItem(VERSION_KEY);
            console.log(TAG, '全部头像缓存已清除');
        } catch (e) {
            console.warn(TAG, '清除缓存失败:', e.message);
        }
    }

    async function invalidateUser(userId) {
        try {
            if (hasCacheApi) {
                var cache = await caches.open(CACHE_NAME);
                await cache.delete(cacheKeyForUser(String(userId)));
            }
            var versionMap = getVersionMap();
            delete versionMap[String(userId)];
            setVersionMap(versionMap);
            console.log(TAG, '已清除用户缓存', 'userId=' + userId);
        } catch (e) {
            console.warn(TAG, '清除用户缓存失败:', e.message);
        }
    }

    window.AvatarCache = {
        getAvatar: getAvatar,
        updateCache: updateCache,
        clearCache: clearCache,
        invalidateUser: invalidateUser,
        parseAvatarUrl: parseAvatarUrl
    };
})();
