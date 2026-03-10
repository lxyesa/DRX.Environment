/**
 * Module: auth-state.js
 * Responsibility: Centralized authentication state and role semantics adapter.
 *   Provides a single source of truth for token access, current-user fetching,
 *   and permission-group-to-semantic-value mappings.
 * Dependencies: /api/user/verify/account (POST), localStorage.
 */

/** @type {{ isAdmin: boolean, isSystem: boolean, role: string, permissionGroup: number, label: string } | null} */
var _cachedRoleState = null;

/**
 * Returns the active auth token.
 * Prefers kax_web_token (browser-only JWT); falls back to kax_login_token.
 * @returns {string|null}
 */
function getToken() {
    return localStorage.getItem('kax_web_token') || localStorage.getItem('kax_login_token') || null;
}

/**
 * Removes both auth tokens from localStorage and clears cached role state.
 */
function clearToken() {
    localStorage.removeItem('kax_web_token');
    localStorage.removeItem('kax_login_token');
    _cachedRoleState = null;
}

/**
 * Calls /api/user/verify/account and caches the role state for the page session.
 * Subsequent calls within the same page load return the cached result.
 * @param {{ force?: boolean }} [opts] - Pass { force: true } to bypass cache.
 * @returns {Promise<{ isAdmin: boolean, isSystem: boolean, role: string, permissionGroup: number, label: string } | null>}
 */
async function fetchCurrentUser(opts) {
    if (_cachedRoleState && !(opts && opts.force)) return _cachedRoleState;

    var token = getToken();
    if (!token) return null;

    try {
        var resp = await fetch('/api/user/verify/account', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': 'Bearer ' + token
            }
        });
        if (!resp.ok) return null;
        var data = await resp.json().catch(function () { return null; });
        if (!data) return null;

        _cachedRoleState = {
            isAdmin: data.isAdmin === true,
            isSystem: data.isSystem === true,
            role: mapPermissionToLabel(data.permissionGroup),
            permissionGroup: typeof data.permissionGroup === 'number' ? data.permissionGroup : -1,
            label: mapPermissionToLabel(data.permissionGroup)
        };
        return _cachedRoleState;
    } catch (_) {
        return null;
    }
}

/**
 * Returns the cached semantic role state without making a network call.
 * Returns null if fetchCurrentUser has not been called yet.
 * @returns {{ isAdmin: boolean, isSystem: boolean, role: string, permissionGroup: number, label: string } | null}
 */
function getRoleState() {
    return _cachedRoleState;
}

/**
 * Maps a raw permissionGroup integer to a Chinese label string.
 * Single source of truth replacing all magic-number switch/if chains.
 * @param {number} permissionGroup
 * @returns {string}
 */
function mapPermissionToLabel(permissionGroup) {
    switch (Number(permissionGroup)) {
        case 0: return '系统';
        case 2: return '控制台';
        case 3: return '管理员';
        default: return '普通用户';
    }
}

/**
 * Maps a raw permissionGroup integer to a CSS badge class string.
 * @param {number} permissionGroup
 * @returns {string}
 */
function permissionGroupToCssClass(permissionGroup) {
    switch (Number(permissionGroup)) {
        case 0: return 'permission-system';
        case 2: return 'permission-console';
        case 3: return 'permission-admin';
        default: return 'permission-user';
    }
}

/**
 * Maps a raw permissionGroup integer to an English role label string.
 * @param {number} permissionGroup
 * @returns {string}
 */
function permissionGroupToEnglishText(permissionGroup) {
    switch (Number(permissionGroup)) {
        case 0: return 'System';
        case 2: return 'Console';
        case 3: return 'Admin';
        default: return 'User';
    }
}

// Expose to global scope for consumption by profile.js and manage-users.js.
window.AuthState = {
    getToken: getToken,
    clearToken: clearToken,
    fetchCurrentUser: fetchCurrentUser,
    getRoleState: getRoleState,
    mapPermissionToLabel: mapPermissionToLabel,
    permissionGroupToCssClass: permissionGroupToCssClass,
    permissionGroupToEnglishText: permissionGroupToEnglishText
};
