(function () {
    'use strict';

    // ========== DOM 元素 ==========
    var page = document.getElementById('page');
    var terminalBody = document.getElementById('terminalBody');
    var terminalOutput = document.getElementById('terminalOutput');
    var terminalInput = document.getElementById('terminalInput');
    var clearBtn = document.getElementById('clearBtn');
    var homeBtn = document.getElementById('homeBtn');
    var helpBtn = document.getElementById('helpBtn');
    var fullscreenBtn = document.getElementById('fullscreenBtn');
    var logToggleBtn = document.getElementById('logToggleBtn');
    var referenceToggle = document.getElementById('referenceToggle');
    var referenceBody = document.getElementById('referenceBody');
    var referenceCaret = document.getElementById('referenceCaret');
    var commandListEl = document.getElementById('commandList');
    var autocompleteDropdown = document.getElementById('autocompleteDropdown');
    var connectionStatus = document.getElementById('connectionStatus');
    var container = document.querySelector('.console-container');

    // ========== 状态 ==========
    var commandHistory = [];
    var historyIndex = -1;
    var commandCache = [];
    var isExecuting = false;
    var autocompleteIndex = -1;
    var isFullscreen = false;
    var showLogs = false;
    var logEventSource = null;

    // ========== 常量 ==========
    var MAX_OUTPUT_LINES = 2000;
    var BANNER = [
        '╔═══════════════════════════════════════════════════════════╗',
        '║     _  __           _   _       _       ____            ║',
        '║    | |/ /__ ___  __| | | |_   _| |__   / ___|___  _ __  ║',
        '║    |   // _` \\ \\/ /| |_| | | | |  _ \\ | |   / _ \\| \'_ \\ ║',
        '║    |  \\| (_| |>  < |  _  | |_| | |_) || |__| (_) | | | |║',
        '║    |_|\\_\\__,_/_/\\_\\|_| |_|\\__,_|_.__/  \\____\\___/|_| |_|║',
        '║                                                         ║',
        '║           KaxHub 服务器远程命令控制台 v1.0               ║',
        '╚═══════════════════════════════════════════════════════════╝',
    ];

    // ========== 初始化 ==========
    verifyToken();

    function forceLogout() {
        try { localStorage.removeItem('kax_login_token'); } catch (_) { }
        location.href = '/login';
    }

    /** 验证用户令牌，确保有管理员权限 */
    async function verifyToken() {
        try {
            var token = localStorage.getItem('kax_login_token');
            if (!token) { location.href = '/login'; return; }

            var resp = await fetch('/api/user/verify/account', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token }
            });

            if (resp.status === 200) {
                page.classList.remove('hidden');
                initConsole();
            } else {
                location.href = '/login';
            }
        } catch (err) {
            console.error('验证 token 时出错：', err);
            location.href = '/login';
        }
    }

    /** 初始化控制台 */
    function initConsole() {
        printBanner();
        loadCommandList();
        bindEvents();
        terminalInput.focus();

        // 从缓存恢复日志开关状态（刷新页面后自动重连）
        try {
            if (localStorage.getItem('kax_console_show_logs') === '1') {
                toggleLogs();
            }
        } catch (_) { }
    }

    // ========== 输出方法 ==========

    /** 向终端追加一行输出 */
    function printLine(text, className) {
        var line = document.createElement('div');
        line.className = 'output-line ' + (className || 'info');
        line.textContent = text;
        terminalOutput.appendChild(line);
        trimOutput();
        scrollToBottom();
    }

    /** 向终端追加多行输出 */
    function printLines(lines, className) {
        for (var i = 0; i < lines.length; i++) {
            printLine(lines[i], className);
        }
    }

    /** 追加原始 HTML 内容行 */
    function printHtml(html, className) {
        var line = document.createElement('div');
        line.className = 'output-line ' + (className || 'info');
        line.innerHTML = html;
        terminalOutput.appendChild(line);
        trimOutput();
        scrollToBottom();
    }

    /** 追加分隔线 */
    function printSeparator() {
        var hr = document.createElement('hr');
        hr.className = 'output-separator';
        terminalOutput.appendChild(hr);
        scrollToBottom();
    }

    /** 打印欢迎 Banner */
    function printBanner() {
        printLines(BANNER, 'system');
        printLine('');
        printLine('输入 help 查看可用命令，输入 clear 清空终端。', 'muted');
        printLine('使用 ↑↓ 箭头键浏览历史命令，Tab 键自动补全。', 'muted');
        printLine('');
    }

    /** 限制最大行数 */
    function trimOutput() {
        while (terminalOutput.children.length > MAX_OUTPUT_LINES) {
            terminalOutput.removeChild(terminalOutput.firstChild);
        }
    }

    /** 滚动到底部 */
    function scrollToBottom() {
        requestAnimationFrame(function () {
            terminalBody.scrollTop = terminalBody.scrollHeight;
        });
    }

    /** 清空终端 */
    function clearTerminal() {
        terminalOutput.innerHTML = '';
        // 清屏后分组 DOM 全部消失，重置状态
        clearTimeout(logGroupTimer);
        logCurrentGroupEl = null;
        logCurrentGroupBody = null;
        logCurrentGroupHeader = null;
        logCurrentMinuteKey = null;
        logLastContent = null;
        logLastEntryEl = null;
        logLastCountEl = null;
        logLastCount = 0;
        logGroupEntryCount = 0;
        printLine('终端已清空。', 'muted');
        printLine('');
    }

    // ========== 状态显示 ==========

    /** 更新顶栏连接状态指示：connected=true 已连接，false 断开，'connecting' 连接中 */
    function setConnected(connected) {
        var dot = connectionStatus.querySelector('.status-dot');
        var text = connectionStatus.querySelector('.status-text');
        if (connected === 'connecting') {
            dot.className = 'status-dot connecting';
            text.textContent = '连接中…';
        } else if (connected) {
            dot.className = 'status-dot online';
            text.textContent = '已连接';
        } else {
            dot.className = 'status-dot offline';
            text.textContent = '未连接';
        }
    }

    // ========== 日志流 ==========

    /**
     * 解析日志条目，返回结构化对象：
     * { time, lvl, levelClass, content, minuteKey, html }
     * time       — 格式化时间字符串
     * lvl        — 日志级别标签（大写）
     * levelClass — CSS 类名
     * content    — 纯文本消息内容（用于去重比对）
     * minuteKey  — "HH:MM" 用于分组（取自 entry.time 或解析消息内时间）
     * html       — 渲染好的 HTML 字符串（不含来源行号）
     */
    function parseLogEntry(entry) {
        var msg = entry.message || '';

        // 解析日志结构: [时间][来源:行号][级别]消息
        var match = msg.match(/^\[([^\]]+)\]\[([^\]]+)\]\[([^\]]+)\](.*)$/);

        var time, lvl, content, minuteKey;
        if (match) {
            time = match[1];
            // match[2] 是来源:行号，直接丢弃
            lvl = match[3].toUpperCase();
            content = match[4];
        } else {
            time = entry.time || '';
            lvl = (entry.level || 'INFO').toUpperCase();
            content = msg;
        }

        // 从时间字段中提取 HH:MM 作为分组 key
        var timeMatch = time.match(/(\d{2}:\d{2})/);
        minuteKey = timeMatch ? timeMatch[1] : (entry.time ? String(entry.time).substring(0, 5) : '--:--');

        var levelClass = 'log-level-info';
        if (lvl === 'WARN') levelClass = 'log-level-warn';
        else if (lvl === 'FAIL' || lvl === 'ERROR') levelClass = 'log-level-error';
        else if (lvl === 'DBUG' || lvl === 'DEBUG') levelClass = 'log-level-debug';
        else if (lvl === 'FATAL') levelClass = 'log-level-fatal';

        var html = '<span class="log-time">[' + escapeHtml(time) + ']</span>' +
            '<span class="' + levelClass + '">[' + escapeHtml(lvl) + ']</span>' +
            '<span class="log-text"> ' + escapeHtml(content) + '</span>';

        return { time: time, lvl: lvl, levelClass: levelClass, content: content, minuteKey: minuteKey, html: html };
    }

    // ========== 日志分组与去重状态 ==========
    var logCurrentGroupEl = null;    // 当前分组的 .log-group DOM 元素
    var logCurrentGroupBody = null;  // 当前分组的 .log-group-body DOM 元素
    var logCurrentGroupHeader = null; // 当前分组的 .log-group-header
    var logCurrentMinuteKey = null;  // 当前分组的分钟 key (HH:MM)
    var logGroupTimer = null;        // 分组超时计时器
    var logLastContent = null;       // 上一条日志的 content（用于去重）
    var logLastEntryEl = null;       // 上一条日志的 DOM 元素（用于更新去重角标）
    var logLastCountEl = null;       // 上一条日志的 .log-count 元素
    var logLastCount = 0;            // 上一条日志的重复次数
    var logGroupEntryCount = 0;      // 当前分组的条目数量（含去重）

    /**
     * 将一条日志追加到终端，实现：
     * - 1 分钟时间窗口分组（新条目到来时重置计时器）
     * - 同级别同内容连续去重（显示 ×N 角标）
     * - 不再显示来源代码行号
     */
    function appendLogEntry(entry) {
        var parsed = parseLogEntry(entry);

        // ---- 判断是否需要新建分组 ----
        var needNewGroup = !logCurrentGroupEl ||
            logCurrentMinuteKey !== parsed.minuteKey;

        if (needNewGroup) {
            _createLogGroup(parsed.minuteKey);
        }

        // 重置分组超时计时器（1 分钟无新日志则下次新建分组）
        clearTimeout(logGroupTimer);
        logGroupTimer = setTimeout(function () {
            logCurrentGroupEl = null;
            logCurrentGroupBody = null;
            logCurrentGroupHeader = null;
            logCurrentMinuteKey = null;
            logLastContent = null;
            logLastEntryEl = null;
            logLastCountEl = null;
            logLastCount = 0;
            logGroupEntryCount = 0;
        }, 60000);

        // ---- 判断是否与上条内容相同（去重） ----
        if (logLastContent !== null && logLastContent === parsed.content && logLastEntryEl) {
            logLastCount++;
            if (!logLastCountEl) {
                logLastCountEl = document.createElement('span');
                logLastCountEl.className = 'log-count';
                logLastEntryEl.appendChild(logLastCountEl);
            }
            logLastCountEl.textContent = '×' + logLastCount;
            logLastCountEl.className = 'log-count' + (logLastCount >= 10 ? ' many' : '');
        } else {
            // 新条目
            var lineEl = document.createElement('div');
            lineEl.className = 'output-line log-entry';
            lineEl.innerHTML = parsed.html;
            logCurrentGroupBody.appendChild(lineEl);

            logLastContent = parsed.content;
            logLastEntryEl = lineEl;
            logLastCountEl = null;
            logLastCount = 1;

            logGroupEntryCount++;
            _updateGroupCount();
        }

        trimOutput();
        scrollToBottom();
    }

    /** 创建新的日志分组 */
    function _createLogGroup(minuteKey) {
        // 上一条去重状态清零
        logLastContent = null;
        logLastEntryEl = null;
        logLastCountEl = null;
        logLastCount = 0;
        logGroupEntryCount = 0;

        var groupEl = document.createElement('div');
        groupEl.className = 'log-group collapsed';

        var headerEl = document.createElement('div');
        headerEl.className = 'log-group-header';
        headerEl.innerHTML =
            '<span class="material-icons log-group-caret">expand_more</span>' +
            '<span class="log-group-time">' + escapeHtml(minuteKey) + '</span>' +
            '<span class="log-group-count"></span>';

        var bodyEl = document.createElement('div');
        bodyEl.className = 'log-group-body';

        // 点击标题折叠/展开
        headerEl.addEventListener('click', function () {
            groupEl.classList.toggle('collapsed');
        });

        groupEl.appendChild(headerEl);
        groupEl.appendChild(bodyEl);
        terminalOutput.appendChild(groupEl);

        logCurrentGroupEl = groupEl;
        logCurrentGroupBody = bodyEl;
        logCurrentGroupHeader = headerEl;
        logCurrentMinuteKey = minuteKey;
    }

    /** 更新分组标题的条目数显示 */
    function _updateGroupCount() {
        if (!logCurrentGroupHeader) return;
        var countEl = logCurrentGroupHeader.querySelector('.log-group-count');
        if (countEl) {
            countEl.textContent = logGroupEntryCount + ' 条';
        }
    }

    /** 通过 SSE 连接服务器日志流 */
    function startLogStream() {
        if (logEventSource) return;

        var token = localStorage.getItem('kax_login_token');
        if (!token) return;

        setConnected('connecting');
        logEventSource = new EventSource('/api/console/logs/stream?token=' + encodeURIComponent(token));

        logEventSource.addEventListener('log', function (event) {
            try {
                var entry = JSON.parse(event.data);
                appendLogEntry(entry);
            } catch (_) { }
        });

        logEventSource.addEventListener('connected', function (event) {
            setConnected(true);
            printLine('SSE 已连接，正在加载历史日志...', 'muted');
        });

        logEventSource.addEventListener('history-end', function (event) {
            try {
                var data = JSON.parse(event.data);
                var count = data.count || 0;
                var total = data.total || count;
                if (total > count) {
                    printLine('已加载最近 ' + count + ' 条历史日志（共 ' + total + ' 条，仅显示最新部分）', 'muted');
                } else {
                    printLine('已加载 ' + count + ' 条历史日志', 'muted');
                }
            } catch (_) {
                printLine('历史日志加载完成', 'muted');
            }
            scrollToBottom();
        });

        logEventSource.onerror = function () {
            setConnected(false);
            stopLogStream();
            // 3 秒后自动重连
            if (showLogs) {
                setTimeout(function () {
                    if (showLogs) startLogStream();
                }, 3000);
            }
        };
    }

    /** 断开 SSE 日志流 */
    function stopLogStream() {
        if (logEventSource) {
            logEventSource.close();
            logEventSource = null;
        }
        setConnected(false);
        // 重置分组状态，下次开启时从头建立新分组
        clearTimeout(logGroupTimer);
        logCurrentGroupEl = null;
        logCurrentGroupBody = null;
        logCurrentGroupHeader = null;
        logCurrentMinuteKey = null;
        logLastContent = null;
        logLastEntryEl = null;
        logLastCountEl = null;
        logLastCount = 0;
        logGroupEntryCount = 0;
    }

    /** 切换日志显示 */
    function toggleLogs() {
        showLogs = !showLogs;
        try { localStorage.setItem('kax_console_show_logs', showLogs ? '1' : '0'); } catch (_) { }
        var icon = logToggleBtn.querySelector('.material-icons');
        if (showLogs) {
            icon.textContent = 'visibility_off';
            logToggleBtn.title = '隐藏日志';
            logToggleBtn.classList.add('active');
            printSeparator();
            printLine('日志流已开启 — 实时显示服务器日志 (SSE)', 'system');
            printLine('');
            startLogStream();
        } else {
            icon.textContent = 'receipt_long';
            logToggleBtn.title = '显示日志';
            logToggleBtn.classList.remove('active');
            stopLogStream();
            printSeparator();
            printLine('日志流已关闭', 'muted');
            printLine('');
        }
    }

    // ========== 命令执行 ==========

    /** 提交命令到服务器 */
    async function executeCommand(input) {
        var trimmed = input.trim();
        if (!trimmed) return;

        // 回显命令
        printHtml(
            '<span class="prompt-user">admin</span><span class="prompt-at">@</span><span class="prompt-host">kaxhub</span><span class="prompt-colon">:</span><span class="prompt-path">~</span><span class="prompt-dollar">$ </span>' + escapeHtml(trimmed),
            'command-echo'
        );

        // 添加到历史记录
        if (commandHistory.length === 0 || commandHistory[commandHistory.length - 1] !== trimmed) {
            commandHistory.push(trimmed);
        }
        historyIndex = commandHistory.length;

        // 内置命令处理
        if (trimmed === 'clear' || trimmed === 'cls') {
            clearTerminal();
            return;
        }
        if (trimmed === 'history') {
            printClientHistory();
            return;
        }

        isExecuting = true;
        terminalInput.classList.add('busy');
        terminalInput.value = '';

        try {
            var token = localStorage.getItem('kax_login_token');
            var resp = await fetch('/api/console/execute', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': 'Bearer ' + token
                },
                body: JSON.stringify({ command: trimmed })
            });

            if (resp.status === 401) {
                printLine('错误：身份验证已过期，请重新登录。', 'error');
                setTimeout(forceLogout, 1500);
                return;
            }
            if (resp.status === 403) {
                printLine('错误：权限不足，仅管理员可使用控制台。', 'error');
                return;
            }

            var data = await resp.json();

            if (data.success) {
                var result = data.result || '';
                if (result) {
                    var lines = result.split('\n');
                    for (var i = 0; i < lines.length; i++) {
                        var line = lines[i];
                        if (line.startsWith('错误') || line.startsWith('Error')) {
                            printLine(line, 'error');
                        } else if (line.startsWith('警告') || line.startsWith('Warning')) {
                            printLine(line, 'warn');
                        } else if (line.indexOf('成功') >= 0 || line.indexOf('Success') >= 0) {
                            printLine(line, 'success');
                        } else {
                            printLine(line, 'info');
                        }
                    }
                } else {
                    printLine('命令执行完成。', 'success');
                }
            } else {
                printLine(data.error || '命令执行失败。', 'error');
            }

            setConnected(true);
        } catch (err) {
            printLine('错误：无法连接到服务器 — ' + err.message, 'error');
            setConnected(false);
        } finally {
            isExecuting = false;
            terminalInput.classList.remove('busy');
            printLine('');
        }
    }

    /** 显示客户端命令历史 */
    function printClientHistory() {
        if (commandHistory.length === 0) {
            printLine('暂无命令历史。', 'muted');
        } else {
            printLine('命令历史：', 'system');
            for (var i = 0; i < commandHistory.length; i++) {
                printLine('  ' + (i + 1) + '  ' + commandHistory[i], 'info');
            }
        }
        printLine('');
    }

    // ========== 命令列表与自动补全 ==========

    /** 从服务端加载已注册的命令列表 */
    async function loadCommandList() {
        try {
            var token = localStorage.getItem('kax_login_token');
            var resp = await fetch('/api/console/commands', {
                method: 'GET',
                headers: { 'Authorization': 'Bearer ' + token }
            });

            if (resp.status === 200) {
                commandCache = await resp.json();
                renderCommandReference();
            } else {
                commandListEl.innerHTML = '<p class="hint">无法加载命令列表。</p>';
            }
        } catch (err) {
            commandListEl.innerHTML = '<p class="hint">加载命令列表失败。</p>';
        }
    }

    /** 渲染命令参考面板中的命令卡片 */
    function renderCommandReference() {
        if (!commandCache || commandCache.length === 0) {
            commandListEl.innerHTML = '<p class="hint">暂无已注册的命令。</p>';
            return;
        }

        commandListEl.innerHTML = '';
        for (var i = 0; i < commandCache.length; i++) {
            var cmd = commandCache[i];
            var card = document.createElement('div');
            card.className = 'cmd-card';
            card.setAttribute('data-format', cmd.format || '');

            card.innerHTML =
                '<div class="cmd-card-name">' + escapeHtml(cmd.format || cmd.name) + '</div>' +
                '<div class="cmd-card-desc">' + escapeHtml(cmd.description || '') + '</div>' +
                (cmd.category ? '<span class="cmd-card-category">' + escapeHtml(cmd.category) + '</span>' : '');

            card.addEventListener('click', (function (format) {
                return function () {
                    var cmdName = (format || '').split(' ')[0];
                    terminalInput.value = cmdName + ' ';
                    terminalInput.focus();
                };
            })(cmd.format));

            commandListEl.appendChild(card);
        }
    }

    /** 更新自动补全下拉 */
    function updateAutocomplete(value) {
        if (!value || !commandCache || commandCache.length === 0) {
            hideAutocomplete();
            return;
        }

        var query = value.toLowerCase().split(' ')[0];
        var matches = [];
        for (var i = 0; i < commandCache.length; i++) {
            var name = (commandCache[i].name || '').toLowerCase();
            if (name.indexOf(query) === 0) {
                matches.push(commandCache[i]);
            }
        }

        // 内置命令
        var builtins = [
            { name: 'clear', description: '清空终端输出' },
            { name: 'history', description: '显示命令历史' },
            { name: 'help', description: '显示帮助信息' }
        ];
        for (var j = 0; j < builtins.length; j++) {
            if (builtins[j].name.indexOf(query) === 0) {
                matches.push(builtins[j]);
            }
        }

        if (matches.length === 0 || (matches.length === 1 && matches[0].name.toLowerCase() === query)) {
            hideAutocomplete();
            return;
        }

        autocompleteDropdown.innerHTML = '';
        autocompleteIndex = -1;

        for (var k = 0; k < matches.length && k < 10; k++) {
            var item = document.createElement('div');
            item.className = 'autocomplete-item';
            item.setAttribute('data-name', matches[k].name);
            item.innerHTML =
                '<span class="ac-name">' + escapeHtml(matches[k].name) + '</span>' +
                '<span class="ac-desc">' + escapeHtml(matches[k].description || '') + '</span>';

            item.addEventListener('click', (function (name) {
                return function () {
                    terminalInput.value = name + ' ';
                    hideAutocomplete();
                    terminalInput.focus();
                };
            })(matches[k].name));

            autocompleteDropdown.appendChild(item);
        }

        autocompleteDropdown.classList.remove('hidden');
    }

    function hideAutocomplete() {
        autocompleteDropdown.classList.add('hidden');
        autocompleteDropdown.innerHTML = '';
        autocompleteIndex = -1;
    }

    /** 自动补全导航 */
    function navigateAutocomplete(direction) {
        var items = autocompleteDropdown.querySelectorAll('.autocomplete-item');
        if (items.length === 0) return false;

        autocompleteIndex += direction;
        if (autocompleteIndex < 0) autocompleteIndex = items.length - 1;
        if (autocompleteIndex >= items.length) autocompleteIndex = 0;

        for (var i = 0; i < items.length; i++) {
            items[i].classList.toggle('active', i === autocompleteIndex);
        }
        return true;
    }

    /** 选择当前自动补全项 */
    function selectAutocomplete() {
        var items = autocompleteDropdown.querySelectorAll('.autocomplete-item');
        if (autocompleteIndex >= 0 && autocompleteIndex < items.length) {
            var name = items[autocompleteIndex].getAttribute('data-name');
            terminalInput.value = name + ' ';
            hideAutocomplete();
            return true;
        }
        return false;
    }

    // ========== 事件绑定 ==========

    function bindEvents() {
        // 终端区域点击自动聚焦输入
        terminalBody.addEventListener('click', function (e) {
            if (e.target === terminalBody || e.target === terminalOutput || e.target.classList.contains('output-line')) {
                terminalInput.focus();
            }
        });

        // 键盘交互
        terminalInput.addEventListener('keydown', function (e) {
            if (isExecuting) {
                e.preventDefault();
                return;
            }

            // Tab：自动补全
            if (e.key === 'Tab') {
                e.preventDefault();
                if (!autocompleteDropdown.classList.contains('hidden')) {
                    selectAutocomplete();
                } else {
                    updateAutocomplete(terminalInput.value);
                }
                return;
            }

            // Enter：执行命令
            if (e.key === 'Enter') {
                e.preventDefault();
                if (!autocompleteDropdown.classList.contains('hidden') && autocompleteIndex >= 0) {
                    selectAutocomplete();
                } else {
                    hideAutocomplete();
                    var val = terminalInput.value;
                    terminalInput.value = '';
                    executeCommand(val);
                }
                return;
            }

            // 上下箭头：历史 / 自动补全导航
            if (e.key === 'ArrowUp') {
                e.preventDefault();
                if (!autocompleteDropdown.classList.contains('hidden')) {
                    navigateAutocomplete(-1);
                } else if (commandHistory.length > 0) {
                    if (historyIndex > 0) historyIndex--;
                    terminalInput.value = commandHistory[historyIndex] || '';
                }
                return;
            }
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                if (!autocompleteDropdown.classList.contains('hidden')) {
                    navigateAutocomplete(1);
                } else if (commandHistory.length > 0) {
                    if (historyIndex < commandHistory.length - 1) {
                        historyIndex++;
                        terminalInput.value = commandHistory[historyIndex] || '';
                    } else {
                        historyIndex = commandHistory.length;
                        terminalInput.value = '';
                    }
                }
                return;
            }

            // Escape：关闭自动补全
            if (e.key === 'Escape') {
                hideAutocomplete();
                return;
            }

            // Ctrl+L：清屏
            if (e.ctrlKey && e.key === 'l') {
                e.preventDefault();
                clearTerminal();
                return;
            }
        });

        // 输入时更新自动补全
        terminalInput.addEventListener('input', function () {
            var val = terminalInput.value;
            if (val.indexOf(' ') === -1) {
                updateAutocomplete(val);
            } else {
                hideAutocomplete();
            }
        });

        // 清空按钮
        clearBtn.addEventListener('click', function () {
            clearTerminal();
            terminalInput.focus();
        });

        // 返回首页
        homeBtn.addEventListener('click', function () {
            location.href = '/';
        });

        // 帮助按钮
        helpBtn.addEventListener('click', function () {
            executeCommand('help');
        });

        // 日志显示/隐藏切换
        logToggleBtn.addEventListener('click', function () {
            toggleLogs();
            terminalInput.focus();
        });

        // 全屏切换
        fullscreenBtn.addEventListener('click', function () {
            isFullscreen = !isFullscreen;
            container.classList.toggle('fullscreen', isFullscreen);
            var icon = fullscreenBtn.querySelector('.material-icons');
            icon.textContent = isFullscreen ? 'fullscreen_exit' : 'fullscreen';
            terminalInput.focus();
        });

        // 命令参考折叠
        referenceToggle.addEventListener('click', function () {
            var isHidden = referenceBody.classList.contains('hidden');
            referenceBody.classList.toggle('hidden');
            referenceCaret.classList.toggle('rotated', isHidden);
        });

        // 点击外部关闭自动补全
        document.addEventListener('click', function (e) {
            if (!e.target.closest('.terminal-input-line')) {
                hideAutocomplete();
            }
        });

        // 全局快捷键
        document.addEventListener('keydown', function (e) {
            // 按 / 聚焦输入框
            if (e.key === '/' && document.activeElement !== terminalInput) {
                e.preventDefault();
                terminalInput.focus();
            }
            // F11 切换全屏
            if (e.key === 'F11') {
                e.preventDefault();
                fullscreenBtn.click();
            }
        });
    }

    // ========== 工具方法 ==========

    function escapeHtml(str) {
        var div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }

})();
