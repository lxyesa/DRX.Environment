/*
 * DrxHttpClient.hpp
 * ==================
 * C++ Header-Only HTTP Client — 对应 C# DrxHttpClient 的等价实现。
 *
 * 依赖: WinHTTP (Windows 系统自带), BCrypt (SHA256)
 * 编译: 链接 winhttp.lib, bcrypt.lib  (MSVC: #pragma comment 已内置)
 * 标准: C++17
 *
 * 功能清单:
 *   - GET / POST / PUT / DELETE / PATCH 及自定义方法
 *   - JSON / 二进制 / 字符串请求体
 *   - 文件上传 (multipart/form-data, 带进度)
 *   - 文件下载 (带进度, 原子替换, SHA256 哈希校验)
 *   - 流式下载 (写入自定义流)
 *   - Cookie 自动管理、导入/导出 (JSON)
 *   - 会话 Header 自动注入
 *   - SSE (Server-Sent Events) 流式读取
 *   - 默认 Header、超时配置
 *   - 并发请求队列 (线程池 + 信号量)
 */

#ifndef DRX_HTTP_CLIENT_HPP
#define DRX_HTTP_CLIENT_HPP

// ─── Platform ──────────────────────────────────────────────────────────────
#ifndef _WIN32
    #error "DrxHttpClient.hpp requires Windows (WinHTTP)"
#endif

#ifndef WIN32_LEAN_AND_MEAN
    #define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
    #define NOMINMAX
#endif

#include <windows.h>
#include <winhttp.h>
#include <bcrypt.h>

#pragma comment(lib, "winhttp.lib")
#pragma comment(lib, "bcrypt.lib")

// ─── Standard Library ──────────────────────────────────────────────────────
#include <string>
#include <vector>
#include <map>
#include <functional>
#include <fstream>
#include <sstream>
#include <mutex>
#include <thread>
#include <queue>
#include <condition_variable>
#include <atomic>
#include <algorithm>
#include <random>
#include <chrono>
#include <filesystem>
#include <stdexcept>
#include <cstdint>
#include <cassert>

namespace drx { namespace sdk { namespace network { namespace http {

// ═══════════════════════════════════════════════════════════════════════════
//  Forward Declarations & Types
// ═══════════════════════════════════════════════════════════════════════════

/// 键值对 (大小写不敏感比较由调用方确保)
using Headers     = std::map<std::string, std::string>;
using QueryParams = std::map<std::string, std::string>;

/// 进度回调: (已传输字节, 总字节 —— 若未知则 total == -1)
using ProgressCallback = std::function<void(int64_t current, int64_t total)>;

// ═══════════════════════════════════════════════════════════════════════════
//  HttpResponse
// ═══════════════════════════════════════════════════════════════════════════

/// HTTP 响应
struct HttpResponse
{
    int                     statusCode   = 0;
    std::string             body;            ///< 文本响应体
    std::vector<uint8_t>    bodyBytes;       ///< 二进制响应体
    Headers                 headers;         ///< 响应头
    std::string             reasonPhrase;    ///< e.g. "OK", "Not Found"

    bool ok() const { return statusCode >= 200 && statusCode < 300; }
};

// ═══════════════════════════════════════════════════════════════════════════
//  HttpRequest  (高级用法)
// ═══════════════════════════════════════════════════════════════════════════

struct HttpRequest
{
    std::string             method = "GET";
    std::string             url;
    std::string             body;             ///< 字符串请求体 (JSON 等)
    std::vector<uint8_t>    bodyBytes;        ///< 二进制请求体
    Headers                 headers;
    QueryParams             query;
};

// ═══════════════════════════════════════════════════════════════════════════
//  Cookie
// ═══════════════════════════════════════════════════════════════════════════

struct Cookie
{
    std::string name;
    std::string value;
    std::string domain;
    std::string path = "/";
    bool        secure   = false;
    bool        httpOnly = false;
};

// ═══════════════════════════════════════════════════════════════════════════
//  DownloadResult (含元数据)
// ═══════════════════════════════════════════════════════════════════════════

struct DownloadResult
{
    int         statusCode      = 0;
    int64_t     totalBytes      = -1;
    int64_t     downloadedBytes = 0;
    std::string contentType;
    std::string fileName;
    std::string fileHash;       ///< SHA256 hex
    std::string savedFilePath;
    std::string etag;
    Headers     serverMetadata;
};

// ═══════════════════════════════════════════════════════════════════════════
//  SSE Event
// ═══════════════════════════════════════════════════════════════════════════

struct SseEvent
{
    std::string event;   ///< event name (默认 "message")
    std::string data;
    std::string id;
    int         retry = -1;
};

// ═══════════════════════════════════════════════════════════════════════════
//  Internal Helpers  (匿名 namespace，HPP 内部可见)
// ═══════════════════════════════════════════════════════════════════════════

namespace detail {

// ---------- 宽窄字符转换 ----------

inline std::wstring to_wide(const std::string& s)
{
    if (s.empty()) return {};
    int len = MultiByteToWideChar(CP_UTF8, 0, s.data(), (int)s.size(), nullptr, 0);
    std::wstring ws(len, 0);
    MultiByteToWideChar(CP_UTF8, 0, s.data(), (int)s.size(), ws.data(), len);
    return ws;
}

inline std::string to_utf8(const std::wstring& ws)
{
    if (ws.empty()) return {};
    int len = WideCharToMultiByte(CP_UTF8, 0, ws.data(), (int)ws.size(), nullptr, 0, nullptr, nullptr);
    std::string s(len, 0);
    WideCharToMultiByte(CP_UTF8, 0, ws.data(), (int)ws.size(), s.data(), len, nullptr, nullptr);
    return s;
}

// ---------- URL 编码 ----------

inline std::string url_encode(const std::string& s)
{
    std::ostringstream oss;
    for (unsigned char c : s) {
        if (isalnum(c) || c == '-' || c == '_' || c == '.' || c == '~')
            oss << c;
        else {
            oss << '%';
            const char hex[] = "0123456789ABCDEF";
            oss << hex[(c >> 4) & 0x0F] << hex[c & 0x0F];
        }
    }
    return oss.str();
}

// ---------- 构建 URL 含 Query ----------

inline std::string build_url(const std::string& url, const QueryParams& query)
{
    if (query.empty()) return url;
    std::ostringstream oss;
    oss << url << (url.find('?') != std::string::npos ? "&" : "?");
    bool first = true;
    for (auto& [k, v] : query) {
        if (!first) oss << "&";
        oss << url_encode(k) << "=" << url_encode(v);
        first = false;
    }
    return oss.str();
}

// ---------- 解析 URL 组件 ----------

struct UrlParts
{
    bool        isHttps = false;
    std::string host;
    uint16_t    port    = 0;
    std::string path;       // 含 query string
};

inline UrlParts parse_url(const std::string& fullUrl)
{
    UrlParts p;
    std::string url = fullUrl;

    // scheme
    if (url.substr(0, 8) == "https://") { p.isHttps = true;  url = url.substr(8); }
    else if (url.substr(0, 7) == "http://") { p.isHttps = false; url = url.substr(7); }
    else { p.isHttps = false; } // 没有 scheme 时默认 http

    // host[:port] / path
    auto slashPos = url.find('/');
    std::string hostPart = (slashPos != std::string::npos) ? url.substr(0, slashPos) : url;
    p.path = (slashPos != std::string::npos) ? url.substr(slashPos) : "/";

    auto colonPos = hostPart.find(':');
    if (colonPos != std::string::npos) {
        p.host = hostPart.substr(0, colonPos);
        p.port = (uint16_t)std::stoi(hostPart.substr(colonPos + 1));
    } else {
        p.host = hostPart;
        p.port = p.isHttps ? 443 : 80;
    }

    return p;
}

// ---------- 合并相对/绝对 URL ----------

inline std::string resolve_url(const std::string& baseAddress, const std::string& url)
{
    if (url.find("://") != std::string::npos) return url;      // 绝对 URL
    if (baseAddress.empty()) return url;

    std::string base = baseAddress;
    if (base.back() == '/') base.pop_back();
    std::string relative = url;
    if (!relative.empty() && relative.front() != '/') relative = "/" + relative;
    return base + relative;
}

// ---------- 生成 multipart boundary ----------

inline std::string generate_boundary()
{
    static const char alphanum[] = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    std::string boundary = "----DrxBoundary";
    std::mt19937 rng((unsigned)std::chrono::steady_clock::now().time_since_epoch().count());
    std::uniform_int_distribution<int> dist(0, (int)(sizeof(alphanum) - 2));
    for (int i = 0; i < 16; ++i) boundary += alphanum[dist(rng)];
    return boundary;
}

// ---------- SHA256 via BCrypt ----------

inline std::string sha256_hex(const std::vector<uint8_t>& data)
{
    BCRYPT_ALG_HANDLE hAlg = nullptr;
    BCRYPT_HASH_HANDLE hHash = nullptr;
    DWORD hashLen = 0, resultLen = 0;

    BCryptOpenAlgorithmProvider(&hAlg, BCRYPT_SHA256_ALGORITHM, nullptr, 0);
    BCryptGetProperty(hAlg, BCRYPT_HASH_LENGTH, (PUCHAR)&hashLen, sizeof(hashLen), &resultLen, 0);

    std::vector<uint8_t> hashBuf(hashLen);
    BCryptCreateHash(hAlg, &hHash, nullptr, 0, nullptr, 0, 0);
    BCryptHashData(hHash, (PUCHAR)data.data(), (ULONG)data.size(), 0);
    BCryptFinishHash(hHash, hashBuf.data(), hashLen, 0);

    BCryptDestroyHash(hHash);
    BCryptCloseAlgorithmProvider(hAlg, 0);

    const char hex[] = "0123456789abcdef";
    std::string out;
    out.reserve(hashLen * 2);
    for (auto b : hashBuf) { out += hex[(b >> 4) & 0x0F]; out += hex[b & 0x0F]; }
    return out;
}

inline std::string sha256_file(const std::string& filePath)
{
    std::ifstream ifs(filePath, std::ios::binary);
    if (!ifs) return {};

    BCRYPT_ALG_HANDLE hAlg = nullptr;
    BCRYPT_HASH_HANDLE hHash = nullptr;
    DWORD hashLen = 0, resultLen = 0;

    BCryptOpenAlgorithmProvider(&hAlg, BCRYPT_SHA256_ALGORITHM, nullptr, 0);
    BCryptGetProperty(hAlg, BCRYPT_HASH_LENGTH, (PUCHAR)&hashLen, sizeof(hashLen), &resultLen, 0);

    BCryptCreateHash(hAlg, &hHash, nullptr, 0, nullptr, 0, 0);

    char buf[81920];
    while (ifs.read(buf, sizeof(buf)) || ifs.gcount() > 0) {
        BCryptHashData(hHash, (PUCHAR)buf, (ULONG)ifs.gcount(), 0);
    }

    std::vector<uint8_t> hashBuf(hashLen);
    BCryptFinishHash(hHash, hashBuf.data(), hashLen, 0);
    BCryptDestroyHash(hHash);
    BCryptCloseAlgorithmProvider(hAlg, 0);

    const char hex[] = "0123456789abcdef";
    std::string out;
    out.reserve(hashLen * 2);
    for (auto b : hashBuf) { out += hex[(b >> 4) & 0x0F]; out += hex[b & 0x0F]; }
    return out;
}

// ---------- 简易 JSON 工具 (仅用于 Cookie 导入/导出) ----------

inline std::string json_escape(const std::string& s)
{
    std::string out;
    for (char c : s) {
        switch (c) {
            case '"':  out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\n': out += "\\n";  break;
            case '\r': out += "\\r";  break;
            case '\t': out += "\\t";  break;
            default:   out += c;
        }
    }
    return out;
}

/// 极简 JSON 值提取 (仅支持字符串和布尔, 用于 cookie 导入)
inline std::string json_get_string(const std::string& json, const std::string& key)
{
    std::string search = "\"" + key + "\"";
    auto pos = json.find(search);
    if (pos == std::string::npos) return {};
    pos = json.find(':', pos + search.size());
    if (pos == std::string::npos) return {};
    pos = json.find('"', pos + 1);
    if (pos == std::string::npos) return {};
    auto end = json.find('"', pos + 1);
    if (end == std::string::npos) return {};
    return json.substr(pos + 1, end - pos - 1);
}

inline bool json_get_bool(const std::string& json, const std::string& key)
{
    std::string search = "\"" + key + "\"";
    auto pos = json.find(search);
    if (pos == std::string::npos) return false;
    pos = json.find(':', pos + search.size());
    if (pos == std::string::npos) return false;
    auto rest = json.substr(pos + 1, 10);
    return rest.find("true") != std::string::npos;
}

// ---------- 大小写不敏感比较 ----------

inline bool iequals(const std::string& a, const std::string& b)
{
    if (a.size() != b.size()) return false;
    for (size_t i = 0; i < a.size(); ++i)
        if (::tolower((unsigned char)a[i]) != ::tolower((unsigned char)b[i])) return false;
    return true;
}

// ---------- ASCII Header 转义 ----------

inline std::string ensure_ascii_header(const std::string& value)
{
    bool allAscii = true;
    for (unsigned char c : value) { if (c > 127) { allAscii = false; break; } }
    if (allAscii) return value;

    const char hex[] = "0123456789ABCDEF";
    std::string out;
    for (unsigned char c : value) {
        if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '-' || c == '_' || c == '.' || c == '~')
            out += (char)c;
        else { out += '%'; out += hex[(c >> 4) & 0x0F]; out += hex[c & 0x0F]; }
    }
    return out;
}

} // namespace detail

// ═══════════════════════════════════════════════════════════════════════════
//  DrxHttpClient
// ═══════════════════════════════════════════════════════════════════════════

class DrxHttpClient
{
public:
    // ──────────────────────────── 构造 / 析构 ────────────────────────────

    /// 默认构造
    DrxHttpClient()
    {
        init_session();
    }

    /// 指定基础地址构造
    explicit DrxHttpClient(const std::string& baseAddress)
        : baseAddress_(baseAddress)
    {
        init_session();
    }

    ~DrxHttpClient()
    {
        stop_queue();
        if (hSession_) WinHttpCloseHandle(hSession_);
    }

    // 不可复制，可移动
    DrxHttpClient(const DrxHttpClient&) = delete;
    DrxHttpClient& operator=(const DrxHttpClient&) = delete;

    DrxHttpClient(DrxHttpClient&& o) noexcept
        : hSession_(o.hSession_), baseAddress_(std::move(o.baseAddress_)),
          defaultHeaders_(std::move(o.defaultHeaders_)), cookies_(std::move(o.cookies_)),
          autoManageCookies(o.autoManageCookies), sessionCookieName(std::move(o.sessionCookieName)),
          sessionHeaderName(std::move(o.sessionHeaderName)), timeoutMs_(o.timeoutMs_)
    {
        o.hSession_ = nullptr;
    }

    // ──────────────────────────── 属性 ──────────────────────────────────

    bool        autoManageCookies = true;
    std::string sessionCookieName = "session_id";
    std::string sessionHeaderName;              ///< 为空时不使用 header 传递会话

    // ──────────────────────────── 配置方法 ──────────────────────────────

    /// 设置默认请求头
    void setDefaultHeader(const std::string& name, const std::string& value)
    {
        std::lock_guard<std::mutex> lock(mu_);
        defaultHeaders_[name] = detail::ensure_ascii_header(value);
    }

    /// 移除默认请求头
    void removeDefaultHeader(const std::string& name)
    {
        std::lock_guard<std::mutex> lock(mu_);
        defaultHeaders_.erase(name);
    }

    /// 设置超时 (毫秒)
    void setTimeout(int timeoutMs)
    {
        timeoutMs_ = timeoutMs;
    }

    /// 设置超时 (秒, 浮点)
    void setTimeoutSeconds(double seconds)
    {
        timeoutMs_ = (int)(seconds * 1000.0);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  便捷请求方法
    // ══════════════════════════════════════════════════════════════════════

    /// GET
    HttpResponse get(const std::string& url,
                     const Headers& headers = {},
                     const QueryParams& query = {})
    {
        return send("GET", url, "", {}, headers, query);
    }

    /// POST (字符串体, 默认 JSON)
    HttpResponse post(const std::string& url,
                      const std::string& body = "",
                      const Headers& headers = {},
                      const QueryParams& query = {})
    {
        return send("POST", url, body, {}, headers, query);
    }

    /// POST (二进制体)
    HttpResponse post(const std::string& url,
                      const std::vector<uint8_t>& bodyBytes,
                      const Headers& headers = {},
                      const QueryParams& query = {})
    {
        return send("POST", url, "", bodyBytes, headers, query);
    }

    /// PUT (字符串体)
    HttpResponse put(const std::string& url,
                     const std::string& body = "",
                     const Headers& headers = {},
                     const QueryParams& query = {})
    {
        return send("PUT", url, body, {}, headers, query);
    }

    /// PUT (二进制体)
    HttpResponse put(const std::string& url,
                     const std::vector<uint8_t>& bodyBytes,
                     const Headers& headers = {},
                     const QueryParams& query = {})
    {
        return send("PUT", url, "", bodyBytes, headers, query);
    }

    /// DELETE
    HttpResponse del(const std::string& url,
                     const Headers& headers = {},
                     const QueryParams& query = {})
    {
        return send("DELETE", url, "", {}, headers, query);
    }

    /// PATCH (字符串体)
    HttpResponse patch(const std::string& url,
                       const std::string& body = "",
                       const Headers& headers = {},
                       const QueryParams& query = {})
    {
        return send("PATCH", url, body, {}, headers, query);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  通用 Send
    // ══════════════════════════════════════════════════════════════════════

    /// 发送 HTTP 请求
    HttpResponse send(const std::string& method,
                      const std::string& url,
                      const std::string& body = "",
                      const std::vector<uint8_t>& bodyBytes = {},
                      const Headers& headers = {},
                      const QueryParams& query = {})
    {
        auto fullUrl = detail::resolve_url(baseAddress_, detail::build_url(url, query));
        auto parts   = detail::parse_url(fullUrl);

        auto wHost   = detail::to_wide(parts.host);
        auto wPath   = detail::to_wide(parts.path);
        auto wMethod = detail::to_wide(method);

        // ── 连接 ──
        HINTERNET hConnect = WinHttpConnect(hSession_, wHost.c_str(),
                                            (INTERNET_PORT)parts.port, 0);
        if (!hConnect)
            throw std::runtime_error("WinHttpConnect failed: " + parts.host);

        // ── 打开请求 ──
        DWORD flags = parts.isHttps ? WINHTTP_FLAG_SECURE : 0;
        HINTERNET hRequest = WinHttpOpenRequest(hConnect, wMethod.c_str(),
                                                 wPath.c_str(), nullptr,
                                                 WINHTTP_NO_REFERER,
                                                 WINHTTP_DEFAULT_ACCEPT_TYPES, flags);
        if (!hRequest) {
            WinHttpCloseHandle(hConnect);
            throw std::runtime_error("WinHttpOpenRequest failed");
        }

        // ── 超时 ──
        if (timeoutMs_ > 0) {
            WinHttpSetTimeouts(hRequest, timeoutMs_, timeoutMs_, timeoutMs_, timeoutMs_);
        }

        // ── 设置 Headers ──
        std::wstring allHeaders;
        {
            std::lock_guard<std::mutex> lock(mu_);
            for (auto& [k, v] : defaultHeaders_) {
                allHeaders += detail::to_wide(k) + L": " + detail::to_wide(v) + L"\r\n";
            }
        }
        for (auto& [k, v] : headers) {
            allHeaders += detail::to_wide(k) + L": " + detail::to_wide(detail::ensure_ascii_header(v)) + L"\r\n";
        }

        // Content-Type 默认 JSON (当有 body 时)
        if (!body.empty() && headers.find("Content-Type") == headers.end()) {
            allHeaders += L"Content-Type: application/json; charset=utf-8\r\n";
        }

        // Cookie header
        auto cookieHeader = build_cookie_header(parts.host);
        if (!cookieHeader.empty()) {
            allHeaders += L"Cookie: " + detail::to_wide(cookieHeader) + L"\r\n";
        }

        // Session header
        if (!sessionHeaderName.empty()) {
            auto sid = getSessionId();
            if (!sid.empty()) {
                allHeaders += detail::to_wide(sessionHeaderName) + L": " + detail::to_wide(detail::ensure_ascii_header(sid)) + L"\r\n";
            }
        }

        if (!allHeaders.empty()) {
            WinHttpAddRequestHeaders(hRequest, allHeaders.c_str(), (DWORD)allHeaders.size(), WINHTTP_ADDREQ_FLAG_ADD);
        }

        // ── 发送 ──
        const void* bodyPtr    = nullptr;
        DWORD       bodyLen    = 0;

        if (!bodyBytes.empty()) {
            bodyPtr = bodyBytes.data();
            bodyLen = (DWORD)bodyBytes.size();
        } else if (!body.empty()) {
            bodyPtr = body.data();
            bodyLen = (DWORD)body.size();
        }

        BOOL ok = WinHttpSendRequest(hRequest,
                                      WINHTTP_NO_ADDITIONAL_HEADERS, 0,
                                      (LPVOID)bodyPtr, bodyLen, bodyLen, 0);
        if (!ok) {
            DWORD err = GetLastError();
            WinHttpCloseHandle(hRequest);
            WinHttpCloseHandle(hConnect);
            throw std::runtime_error("WinHttpSendRequest failed, error=" + std::to_string(err));
        }

        // ── 接收响应 ──
        ok = WinHttpReceiveResponse(hRequest, nullptr);
        if (!ok) {
            DWORD err = GetLastError();
            WinHttpCloseHandle(hRequest);
            WinHttpCloseHandle(hConnect);
            throw std::runtime_error("WinHttpReceiveResponse failed, error=" + std::to_string(err));
        }

        HttpResponse resp = read_response(hRequest);

        // 提取 Set-Cookie
        if (autoManageCookies) {
            parse_set_cookies(hRequest, parts.host);
        }

        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        return resp;
    }

    /// 发送 HttpRequest 结构体
    HttpResponse send(const HttpRequest& req)
    {
        return send(req.method, req.url, req.body, req.bodyBytes, req.headers, req.query);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  文件上传
    // ══════════════════════════════════════════════════════════════════════

    /// 上传本地文件 (multipart/form-data)
    HttpResponse uploadFile(const std::string& url,
                            const std::string& filePath,
                            const std::string& fieldName = "file",
                            const Headers& headers = {},
                            const QueryParams& query = {},
                            ProgressCallback progress = nullptr)
    {
        namespace fs = std::filesystem;
        if (!fs::exists(filePath))
            throw std::runtime_error("Upload file not found: " + filePath);

        std::ifstream ifs(filePath, std::ios::binary);
        std::vector<uint8_t> fileData((std::istreambuf_iterator<char>(ifs)),
                                       std::istreambuf_iterator<char>());
        auto fileName = fs::path(filePath).filename().string();

        return uploadFile(url, fileData.data(), fileData.size(), fileName, fieldName, headers, query, progress);
    }

    /// 上传内存数据 (multipart/form-data)
    HttpResponse uploadFile(const std::string& url,
                            const uint8_t* data, size_t dataSize,
                            const std::string& fileName,
                            const std::string& fieldName = "file",
                            const Headers& headers = {},
                            const QueryParams& query = {},
                            ProgressCallback progress = nullptr)
    {
        auto boundary = detail::generate_boundary();
        std::vector<uint8_t> multipartBody;

        // ── 构建 multipart body ──
        {
            std::ostringstream oss;
            oss << "--" << boundary << "\r\n"
                << "Content-Disposition: form-data; name=\"" << fieldName << "\"; filename=\"" << fileName << "\"\r\n"
                << "Content-Type: application/octet-stream\r\n\r\n";
            auto header = oss.str();
            multipartBody.insert(multipartBody.end(), header.begin(), header.end());
        }
        multipartBody.insert(multipartBody.end(), data, data + dataSize);
        {
            std::ostringstream oss;
            oss << "\r\n--" << boundary << "--\r\n";
            auto tail = oss.str();
            multipartBody.insert(multipartBody.end(), tail.begin(), tail.end());
        }

        // ── 构建 headers ──
        Headers uploadHeaders = headers;
        uploadHeaders["Content-Type"] = "multipart/form-data; boundary=" + boundary;

        if (progress) progress((int64_t)dataSize, (int64_t)dataSize);

        return send("POST", url, "", multipartBody, uploadHeaders, query);
    }

    /// 上传文件并附带 metadata (JSON 字符串)
    HttpResponse uploadFileWithMetadata(const std::string& url,
                                        const std::string& filePath,
                                        const std::string& metadataJson = "{}",
                                        const Headers& headers = {},
                                        ProgressCallback progress = nullptr)
    {
        namespace fs = std::filesystem;
        if (!fs::exists(filePath))
            throw std::runtime_error("Upload file not found: " + filePath);

        std::ifstream ifs(filePath, std::ios::binary);
        std::vector<uint8_t> fileData((std::istreambuf_iterator<char>(ifs)),
                                       std::istreambuf_iterator<char>());
        auto fileName = fs::path(filePath).filename().string();
        auto boundary = detail::generate_boundary();

        // ── 构建 multipart body (file + metadata) ──
        std::vector<uint8_t> body;
        {
            std::ostringstream oss;
            oss << "--" << boundary << "\r\n"
                << "Content-Disposition: form-data; name=\"file\"; filename=\"" << fileName << "\"\r\n"
                << "Content-Type: application/octet-stream\r\n\r\n";
            auto s = oss.str();
            body.insert(body.end(), s.begin(), s.end());
        }
        body.insert(body.end(), fileData.begin(), fileData.end());
        {
            std::ostringstream oss;
            oss << "\r\n--" << boundary << "\r\n"
                << "Content-Disposition: form-data; name=\"metadata\"\r\n"
                << "Content-Type: application/json; charset=utf-8\r\n\r\n"
                << metadataJson
                << "\r\n--" << boundary << "--\r\n";
            auto s = oss.str();
            body.insert(body.end(), s.begin(), s.end());
        }

        Headers uploadHeaders = headers;
        uploadHeaders["Content-Type"] = "multipart/form-data; boundary=" + boundary;
        uploadHeaders["X-File-Name"]  = fileName;

        if (progress) progress((int64_t)fileData.size(), (int64_t)fileData.size());

        return send("POST", url, "", body, uploadHeaders, {});
    }

    // ══════════════════════════════════════════════════════════════════════
    //  文件下载
    // ══════════════════════════════════════════════════════════════════════

    /// 下载文件到本地路径 (带进度, 原子替换)
    void downloadFile(const std::string& url,
                      const std::string& destPath,
                      const Headers& headers = {},
                      const QueryParams& query = {},
                      ProgressCallback progress = nullptr)
    {
        auto fullUrl = detail::resolve_url(baseAddress_, detail::build_url(url, query));
        auto parts   = detail::parse_url(fullUrl);

        HINTERNET hConnect, hRequest;
        open_download_request(parts, headers, hConnect, hRequest);

        int64_t totalBytes = get_content_length(hRequest);

        // 确保目录存在
        namespace fs = std::filesystem;
        auto dir = fs::path(destPath).parent_path();
        if (!dir.empty()) fs::create_directories(dir);

        auto tempFile = destPath + ".download.tmp";
        {
            std::ofstream ofs(tempFile, std::ios::binary);
            if (!ofs) {
                WinHttpCloseHandle(hRequest); WinHttpCloseHandle(hConnect);
                throw std::runtime_error("Cannot create temp file: " + tempFile);
            }

            char buf[81920];
            DWORD bytesRead = 0;
            int64_t totalRead = 0;
            while (WinHttpReadData(hRequest, buf, sizeof(buf), &bytesRead) && bytesRead > 0) {
                ofs.write(buf, bytesRead);
                totalRead += bytesRead;
                if (progress) progress(totalRead, totalBytes);
                bytesRead = 0;
            }
        }

        // Set-Cookie
        if (autoManageCookies) parse_set_cookies(hRequest, parts.host);
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);

        // 原子替换
        atomic_file_replace(tempFile, destPath);
    }

    /// 下载文件并返回 SHA256 哈希, 可指定期望哈希进行校验
    std::string downloadFileWithHash(const std::string& url,
                                     const std::string& destPath,
                                     const std::string& expectedHash = "",
                                     const Headers& headers = {},
                                     const QueryParams& query = {},
                                     ProgressCallback progress = nullptr)
    {
        downloadFile(url, destPath, headers, query, progress);
        auto fileHash = detail::sha256_file(destPath);

        if (!expectedHash.empty()) {
            // 大小写不敏感比较
            std::string lowerExpected = expectedHash, lowerActual = fileHash;
            std::transform(lowerExpected.begin(), lowerExpected.end(), lowerExpected.begin(), ::tolower);
            std::transform(lowerActual.begin(), lowerActual.end(), lowerActual.begin(), ::tolower);
            if (lowerExpected != lowerActual) {
                std::filesystem::remove(destPath);
                throw std::runtime_error("Hash mismatch: expected " + expectedHash + ", got " + fileHash);
            }
        }

        return fileHash;
    }

    /// 下载文件并返回 DownloadResult (含元数据)
    DownloadResult downloadFileWithMetadata(const std::string& url,
                                            const std::string& destPath,
                                            const Headers& headers = {},
                                            const QueryParams& query = {},
                                            ProgressCallback progress = nullptr)
    {
        auto fullUrl = detail::resolve_url(baseAddress_, detail::build_url(url, query));
        auto parts   = detail::parse_url(fullUrl);

        HINTERNET hConnect, hRequest;
        open_download_request(parts, headers, hConnect, hRequest);

        DownloadResult result;
        result.statusCode = get_status_code(hRequest);
        result.totalBytes = get_content_length(hRequest);
        result.contentType = get_header(hRequest, L"Content-Type");
        result.etag = get_header(hRequest, L"ETag");

        // X-MetaData header
        auto meta = get_header(hRequest, L"X-MetaData");
        if (!meta.empty()) result.serverMetadata["X-MetaData"] = meta;

        namespace fs = std::filesystem;
        auto dir = fs::path(destPath).parent_path();
        if (!dir.empty()) fs::create_directories(dir);

        auto tempFile = destPath + ".download.tmp";
        {
            std::ofstream ofs(tempFile, std::ios::binary);
            char buf[81920];
            DWORD bytesRead = 0;
            int64_t totalRead = 0;
            while (WinHttpReadData(hRequest, buf, sizeof(buf), &bytesRead) && bytesRead > 0) {
                ofs.write(buf, bytesRead);
                totalRead += bytesRead;
                if (progress) progress(totalRead, result.totalBytes);
                bytesRead = 0;
            }
            result.downloadedBytes = totalRead;
        }

        if (autoManageCookies) parse_set_cookies(hRequest, parts.host);
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);

        atomic_file_replace(tempFile, destPath);
        result.savedFilePath = destPath;
        result.fileHash = detail::sha256_file(destPath);
        result.fileName = fs::path(destPath).filename().string();

        return result;
    }

    /// 下载到输出流
    void downloadToStream(const std::string& url,
                          std::ostream& destination,
                          const Headers& headers = {},
                          const QueryParams& query = {},
                          ProgressCallback progress = nullptr)
    {
        auto fullUrl = detail::resolve_url(baseAddress_, detail::build_url(url, query));
        auto parts   = detail::parse_url(fullUrl);

        HINTERNET hConnect, hRequest;
        open_download_request(parts, headers, hConnect, hRequest);

        int64_t totalBytes = get_content_length(hRequest);
        char buf[81920];
        DWORD bytesRead = 0;
        int64_t totalRead = 0;

        while (WinHttpReadData(hRequest, buf, sizeof(buf), &bytesRead) && bytesRead > 0) {
            destination.write(buf, bytesRead);
            totalRead += bytesRead;
            if (progress) progress(totalRead, totalBytes);
            bytesRead = 0;
        }

        if (autoManageCookies) parse_set_cookies(hRequest, parts.host);
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  SSE (Server-Sent Events)
    // ══════════════════════════════════════════════════════════════════════

    /// 连接 SSE 端点, 通过回调逐个接收事件。阻塞直到连接关闭或 shouldStop 返回 true。
    void connectSse(const std::string& url,
                    std::function<void(const SseEvent&)> onEvent,
                    std::function<bool()> shouldStop = nullptr,
                    const Headers& headers = {})
    {
        auto fullUrl = detail::resolve_url(baseAddress_, url);
        auto parts   = detail::parse_url(fullUrl);

        HINTERNET hConnect, hRequest;
        {
            auto wHost = detail::to_wide(parts.host);
            hConnect = WinHttpConnect(hSession_, wHost.c_str(), (INTERNET_PORT)parts.port, 0);
            if (!hConnect) throw std::runtime_error("SSE: WinHttpConnect failed");

            auto wPath = detail::to_wide(parts.path);
            DWORD flags = parts.isHttps ? WINHTTP_FLAG_SECURE : 0;
            hRequest = WinHttpOpenRequest(hConnect, L"GET", wPath.c_str(), nullptr,
                                           WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, flags);
            if (!hRequest) { WinHttpCloseHandle(hConnect); throw std::runtime_error("SSE: WinHttpOpenRequest failed"); }

            // Headers
            std::wstring wHeaders = L"Accept: text/event-stream\r\nCache-Control: no-cache\r\n";
            for (auto& [k, v] : headers) {
                wHeaders += detail::to_wide(k) + L": " + detail::to_wide(v) + L"\r\n";
            }
            auto cookieHdr = build_cookie_header(parts.host);
            if (!cookieHdr.empty()) wHeaders += L"Cookie: " + detail::to_wide(cookieHdr) + L"\r\n";

            WinHttpAddRequestHeaders(hRequest, wHeaders.c_str(), (DWORD)wHeaders.size(), WINHTTP_ADDREQ_FLAG_ADD);

            if (!WinHttpSendRequest(hRequest, WINHTTP_NO_ADDITIONAL_HEADERS, 0, nullptr, 0, 0, 0) ||
                !WinHttpReceiveResponse(hRequest, nullptr)) {
                WinHttpCloseHandle(hRequest); WinHttpCloseHandle(hConnect);
                throw std::runtime_error("SSE: send/receive failed");
            }
        }

        // 逐行读取 SSE 流
        std::string lineBuffer;
        SseEvent currentEvent;
        char buf[4096];
        DWORD bytesRead = 0;

        while (WinHttpReadData(hRequest, buf, sizeof(buf), &bytesRead) && bytesRead > 0) {
            if (shouldStop && shouldStop()) break;

            lineBuffer.append(buf, bytesRead);
            bytesRead = 0;

            // 按行处理
            size_t pos;
            while ((pos = lineBuffer.find('\n')) != std::string::npos) {
                auto line = lineBuffer.substr(0, pos);
                lineBuffer.erase(0, pos + 1);
                if (!line.empty() && line.back() == '\r') line.pop_back();

                if (line.empty()) {
                    // 空行 = 分发事件
                    if (!currentEvent.data.empty()) {
                        if (currentEvent.event.empty()) currentEvent.event = "message";
                        onEvent(currentEvent);
                    }
                    currentEvent = {};
                } else if (line.substr(0, 5) == "data:") {
                    auto data = line.substr(5);
                    if (!data.empty() && data[0] == ' ') data = data.substr(1);
                    if (!currentEvent.data.empty()) currentEvent.data += "\n";
                    currentEvent.data += data;
                } else if (line.substr(0, 6) == "event:") {
                    auto ev = line.substr(6);
                    if (!ev.empty() && ev[0] == ' ') ev = ev.substr(1);
                    currentEvent.event = ev;
                } else if (line.substr(0, 3) == "id:") {
                    auto id = line.substr(3);
                    if (!id.empty() && id[0] == ' ') id = id.substr(1);
                    currentEvent.id = id;
                } else if (line.substr(0, 6) == "retry:") {
                    auto r = line.substr(6);
                    if (!r.empty() && r[0] == ' ') r = r.substr(1);
                    try { currentEvent.retry = std::stoi(r); } catch (...) {}
                }
                // 其他行忽略 (SSE 规范: 以 ':' 开头为注释)
            }
        }

        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Cookie 管理
    // ══════════════════════════════════════════════════════════════════════

    /// 获取当前会话 ID
    std::string getSessionId() const
    {
        std::lock_guard<std::mutex> lock(mu_);
        for (auto& c : cookies_) {
            if (detail::iequals(c.name, sessionCookieName)) return c.value;
        }
        return {};
    }

    /// 设置会话 ID
    void setSessionId(const std::string& sessionId, const std::string& domain = "", const std::string& path = "/")
    {
        if (sessionId.empty()) return;
        std::lock_guard<std::mutex> lock(mu_);
        for (auto& c : cookies_) {
            if (detail::iequals(c.name, sessionCookieName)) {
                c.value = sessionId;
                return;
            }
        }
        Cookie c;
        c.name = sessionCookieName;
        c.value = sessionId;
        c.domain = domain.empty() ? extract_host(baseAddress_) : domain;
        c.path = path;
        cookies_.push_back(c);
    }

    /// 清空所有 Cookie
    void clearCookies()
    {
        std::lock_guard<std::mutex> lock(mu_);
        cookies_.clear();
    }

    /// 导出 Cookie 为 JSON
    std::string exportCookies() const
    {
        std::lock_guard<std::mutex> lock(mu_);
        std::ostringstream oss;
        oss << "[";
        for (size_t i = 0; i < cookies_.size(); ++i) {
            auto& c = cookies_[i];
            if (i > 0) oss << ",";
            oss << "{\"Name\":\"" << detail::json_escape(c.name)
                << "\",\"Value\":\"" << detail::json_escape(c.value)
                << "\",\"Domain\":\"" << detail::json_escape(c.domain)
                << "\",\"Path\":\"" << detail::json_escape(c.path)
                << "\",\"Secure\":" << (c.secure ? "true" : "false")
                << ",\"HttpOnly\":" << (c.httpOnly ? "true" : "false")
                << "}";
        }
        oss << "]";
        return oss.str();
    }

    /// 从 JSON 导入 Cookie
    void importCookies(const std::string& json)
    {
        if (json.empty()) return;
        std::lock_guard<std::mutex> lock(mu_);

        // 简易解析: 按 '{' ... '}' 分段
        size_t pos = 0;
        while (true) {
            auto start = json.find('{', pos);
            if (start == std::string::npos) break;
            auto end = json.find('}', start);
            if (end == std::string::npos) break;

            auto item = json.substr(start, end - start + 1);
            Cookie c;
            c.name     = detail::json_get_string(item, "Name");
            c.value    = detail::json_get_string(item, "Value");
            c.domain   = detail::json_get_string(item, "Domain");
            c.path     = detail::json_get_string(item, "Path");
            c.secure   = detail::json_get_bool(item, "Secure");
            c.httpOnly = detail::json_get_bool(item, "HttpOnly");
            if (c.path.empty()) c.path = "/";
            if (!c.name.empty()) cookies_.push_back(c);
            pos = end + 1;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  请求队列 (异步后台处理, 可选)
    // ══════════════════════════════════════════════════════════════════════

    /// 启动后台请求队列处理 (默认不启动, 可选调用)
    void startQueue(int maxConcurrent = 10)
    {
        if (queueRunning_) return;
        queueRunning_ = true;
        maxConcurrent_ = maxConcurrent;
        activeTasks_ = 0;

        queueThread_ = std::thread([this]() { queue_worker(); });
    }

    /// 将请求排入队列, 通过回调返回结果
    void enqueue(const HttpRequest& req, std::function<void(HttpResponse)> callback)
    {
        std::lock_guard<std::mutex> lock(queueMu_);
        queue_.push({req, std::move(callback)});
        queueCv_.notify_one();
    }

    /// 停止队列
    void stopQueue()
    {
        stop_queue();
    }

private:

    // ──────────────────────────── 字段 ──────────────────────────────────

    HINTERNET               hSession_       = nullptr;
    std::string             baseAddress_;
    Headers                 defaultHeaders_;
    mutable std::mutex      mu_;

    // Cookie
    std::vector<Cookie>     cookies_;

    // 超时
    int                     timeoutMs_      = 0; // 0 = 使用 WinHTTP 默认

    // 请求队列
    struct QueueEntry {
        HttpRequest                         request;
        std::function<void(HttpResponse)>   callback;
    };
    std::queue<QueueEntry>          queue_;
    std::mutex                      queueMu_;
    std::condition_variable         queueCv_;
    std::thread                     queueThread_;
    std::atomic<bool>               queueRunning_{false};
    int                             maxConcurrent_ = 10;
    std::atomic<int>                activeTasks_{0};

    // ──────────────────────────── 初始化 ────────────────────────────────

    void init_session()
    {
        hSession_ = WinHttpOpen(L"DrxHttpClient/1.0",
                                WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
                                WINHTTP_NO_PROXY_NAME,
                                WINHTTP_NO_PROXY_BYPASS, 0);
        if (!hSession_)
            throw std::runtime_error("WinHttpOpen failed");
    }

    // ──────────────────── 响应读取 ─────────────────────────────────────

    HttpResponse read_response(HINTERNET hRequest)
    {
        HttpResponse resp;
        resp.statusCode = get_status_code(hRequest);
        resp.reasonPhrase = get_header(hRequest, L"Status");

        // 读取所有响应头
        DWORD headerSize = 0;
        WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_RAW_HEADERS_CRLF,
                            WINHTTP_HEADER_NAME_BY_INDEX, nullptr, &headerSize,
                            WINHTTP_NO_HEADER_INDEX);
        if (headerSize > 0) {
            std::wstring rawHeaders(headerSize / sizeof(wchar_t), 0);
            WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_RAW_HEADERS_CRLF,
                                WINHTTP_HEADER_NAME_BY_INDEX, rawHeaders.data(), &headerSize,
                                WINHTTP_NO_HEADER_INDEX);
            auto headersStr = detail::to_utf8(rawHeaders);

            // 解析每行 header
            std::istringstream iss(headersStr);
            std::string line;
            while (std::getline(iss, line)) {
                if (!line.empty() && line.back() == '\r') line.pop_back();
                auto sepPos = line.find(':');
                if (sepPos != std::string::npos) {
                    auto key = line.substr(0, sepPos);
                    auto val = line.substr(sepPos + 1);
                    while (!val.empty() && val[0] == ' ') val = val.substr(1);
                    resp.headers[key] = val;
                }
            }
        }

        // 读取 body
        std::vector<uint8_t> allData;
        char buf[81920];
        DWORD bytesRead = 0;
        while (WinHttpReadData(hRequest, buf, sizeof(buf), &bytesRead) && bytesRead > 0) {
            allData.insert(allData.end(), buf, buf + bytesRead);
            bytesRead = 0;
        }

        resp.bodyBytes = std::move(allData);
        resp.body.assign(resp.bodyBytes.begin(), resp.bodyBytes.end());
        return resp;
    }

    // ──────────────────── 下载辅助 ─────────────────────────────────────

    void open_download_request(const detail::UrlParts& parts, const Headers& headers,
                               HINTERNET& outConnect, HINTERNET& outRequest)
    {
        auto wHost = detail::to_wide(parts.host);
        outConnect = WinHttpConnect(hSession_, wHost.c_str(), (INTERNET_PORT)parts.port, 0);
        if (!outConnect) throw std::runtime_error("Download: WinHttpConnect failed");

        auto wPath = detail::to_wide(parts.path);
        DWORD flags = parts.isHttps ? WINHTTP_FLAG_SECURE : 0;
        outRequest = WinHttpOpenRequest(outConnect, L"GET", wPath.c_str(), nullptr,
                                         WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, flags);
        if (!outRequest) { WinHttpCloseHandle(outConnect); throw std::runtime_error("Download: WinHttpOpenRequest failed"); }

        if (timeoutMs_ > 0) WinHttpSetTimeouts(outRequest, timeoutMs_, timeoutMs_, timeoutMs_, timeoutMs_);

        // Headers
        std::wstring wHeaders;
        {
            std::lock_guard<std::mutex> lock(mu_);
            for (auto& [k, v] : defaultHeaders_)
                wHeaders += detail::to_wide(k) + L": " + detail::to_wide(v) + L"\r\n";
        }
        for (auto& [k, v] : headers)
            wHeaders += detail::to_wide(k) + L": " + detail::to_wide(detail::ensure_ascii_header(v)) + L"\r\n";
        auto cookieHdr = build_cookie_header(parts.host);
        if (!cookieHdr.empty()) wHeaders += L"Cookie: " + detail::to_wide(cookieHdr) + L"\r\n";

        if (!wHeaders.empty())
            WinHttpAddRequestHeaders(outRequest, wHeaders.c_str(), (DWORD)wHeaders.size(), WINHTTP_ADDREQ_FLAG_ADD);

        if (!WinHttpSendRequest(outRequest, WINHTTP_NO_ADDITIONAL_HEADERS, 0, nullptr, 0, 0, 0) ||
            !WinHttpReceiveResponse(outRequest, nullptr)) {
            DWORD err = GetLastError();
            WinHttpCloseHandle(outRequest); WinHttpCloseHandle(outConnect);
            throw std::runtime_error("Download: send/receive failed, error=" + std::to_string(err));
        }
    }

    // ──────────────────── WinHTTP 辅助 ─────────────────────────────────

    static int get_status_code(HINTERNET hRequest)
    {
        DWORD statusCode = 0, size = sizeof(statusCode);
        WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                            WINHTTP_HEADER_NAME_BY_INDEX, &statusCode, &size,
                            WINHTTP_NO_HEADER_INDEX);
        return (int)statusCode;
    }

    static int64_t get_content_length(HINTERNET hRequest)
    {
        DWORD size = 0;
        WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_CONTENT_LENGTH,
                            WINHTTP_HEADER_NAME_BY_INDEX, nullptr, &size,
                            WINHTTP_NO_HEADER_INDEX);
        if (size > 0) {
            std::wstring val(size / sizeof(wchar_t), 0);
            WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_CONTENT_LENGTH,
                                WINHTTP_HEADER_NAME_BY_INDEX, val.data(), &size,
                                WINHTTP_NO_HEADER_INDEX);
            try { return std::stoll(detail::to_utf8(val)); } catch (...) {}
        }
        return -1;
    }

    static std::string get_header(HINTERNET hRequest, const wchar_t* headerName)
    {
        DWORD size = 0;
        WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_CUSTOM, headerName, nullptr, &size, WINHTTP_NO_HEADER_INDEX);
        if (GetLastError() == ERROR_INSUFFICIENT_BUFFER && size > 0) {
            std::wstring val(size / sizeof(wchar_t), 0);
            if (WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_CUSTOM, headerName, val.data(), &size, WINHTTP_NO_HEADER_INDEX))
                return detail::to_utf8(val);
        }
        return {};
    }

    // ──────────────────── Cookie 辅助 ──────────────────────────────────

    std::string build_cookie_header(const std::string& host) const
    {
        std::lock_guard<std::mutex> lock(mu_);
        std::ostringstream oss;
        bool first = true;
        for (auto& c : cookies_) {
            bool domainMatch = c.domain.empty() || host.find(c.domain) != std::string::npos || detail::iequals(c.domain, host);
            if (domainMatch) {
                if (!first) oss << "; ";
                oss << c.name << "=" << c.value;
                first = false;
            }
        }
        return oss.str();
    }

    void parse_set_cookies(HINTERNET hRequest, const std::string& host)
    {
        // WinHTTP 可能返回多个 Set-Cookie header
        DWORD index = 0;
        while (true) {
            DWORD size = 0;
            WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_SET_COOKIE, WINHTTP_HEADER_NAME_BY_INDEX,
                                nullptr, &size, &index);
            if (GetLastError() != ERROR_INSUFFICIENT_BUFFER || size == 0) break;

            std::wstring val(size / sizeof(wchar_t), 0);
            if (!WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_SET_COOKIE, WINHTTP_HEADER_NAME_BY_INDEX,
                                     val.data(), &size, &index))
                break;

            auto setCookieStr = detail::to_utf8(val);
            parse_single_set_cookie(setCookieStr, host);
        }
    }

    void parse_single_set_cookie(const std::string& setCookie, const std::string& defaultDomain)
    {
        // 格式: name=value; path=/; domain=...; secure; httponly
        auto semiPos = setCookie.find(';');
        auto nameVal = (semiPos != std::string::npos) ? setCookie.substr(0, semiPos) : setCookie;
        auto eqPos = nameVal.find('=');
        if (eqPos == std::string::npos) return;

        Cookie c;
        c.name  = nameVal.substr(0, eqPos);
        c.value = nameVal.substr(eqPos + 1);
        c.domain = defaultDomain;
        c.path = "/";

        // 解析属性
        auto rest = (semiPos != std::string::npos) ? setCookie.substr(semiPos + 1) : "";
        std::istringstream iss(rest);
        std::string attr;
        while (std::getline(iss, attr, ';')) {
            while (!attr.empty() && attr[0] == ' ') attr = attr.substr(1);
            if (detail::iequals(attr, "secure")) c.secure = true;
            else if (detail::iequals(attr, "httponly")) c.httpOnly = true;
            else {
                auto aEq = attr.find('=');
                if (aEq != std::string::npos) {
                    auto key = attr.substr(0, aEq);
                    auto val = attr.substr(aEq + 1);
                    if (detail::iequals(key, "domain")) c.domain = val;
                    else if (detail::iequals(key, "path")) c.path = val;
                }
            }
        }

        // 更新或新增
        std::lock_guard<std::mutex> lock(mu_);
        for (auto& existing : cookies_) {
            if (detail::iequals(existing.name, c.name) && detail::iequals(existing.domain, c.domain)) {
                existing.value = c.value;
                existing.path = c.path;
                existing.secure = c.secure;
                existing.httpOnly = c.httpOnly;
                return;
            }
        }
        cookies_.push_back(c);
    }

    static std::string extract_host(const std::string& url)
    {
        auto parts = detail::parse_url(url);
        return parts.host;
    }

    // ──────────────────── 文件辅助 ─────────────────────────────────────

    static void atomic_file_replace(const std::string& tempFile, const std::string& destPath)
    {
        namespace fs = std::filesystem;
        if (fs::exists(destPath)) {
            // 尝试 MoveFileEx 原子替换
            auto wTemp = detail::to_wide(tempFile);
            auto wDest = detail::to_wide(destPath);
            if (!MoveFileExW(wTemp.c_str(), wDest.c_str(), MOVEFILE_REPLACE_EXISTING)) {
                // 回退: 删除后移动
                fs::remove(destPath);
                fs::rename(tempFile, destPath);
            }
        } else {
            fs::rename(tempFile, destPath);
        }
    }

    // ──────────────────── 请求队列 ─────────────────────────────────────

    void queue_worker()
    {
        while (queueRunning_) {
            QueueEntry entry;
            {
                std::unique_lock<std::mutex> lock(queueMu_);
                queueCv_.wait(lock, [this]() { return !queue_.empty() || !queueRunning_; });
                if (!queueRunning_ && queue_.empty()) return;
                if (queue_.empty()) continue;
                entry = std::move(queue_.front());
                queue_.pop();
            }

            // 等待并发限制
            while (activeTasks_.load() >= maxConcurrent_) {
                std::this_thread::sleep_for(std::chrono::milliseconds(10));
            }

            activeTasks_++;
            std::thread([this, entry = std::move(entry)]() mutable {
                try {
                    auto resp = send(entry.request);
                    if (entry.callback) entry.callback(std::move(resp));
                } catch (...) {
                    if (entry.callback) {
                        HttpResponse errResp;
                        errResp.statusCode = -1;
                        errResp.body = "Request failed with exception";
                        entry.callback(std::move(errResp));
                    }
                }
                activeTasks_--;
            }).detach();
        }
    }

    void stop_queue()
    {
        if (!queueRunning_) return;
        queueRunning_ = false;
        queueCv_.notify_all();
        if (queueThread_.joinable()) queueThread_.join();
    }
};

}}}} // namespace drx::sdk::network::http

#endif // DRX_HTTP_CLIENT_HPP
