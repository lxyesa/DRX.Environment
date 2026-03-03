/*
 * DrxHttpClient.hpp  v2.0
 * ========================
 * C++ Header-Only HTTP Client — 对应 C# DrxHttpClient 的等价实现。
 *
 * 依赖: WinHTTP (Windows 系统自带), BCrypt (SHA256)
 * 编译: 链接 winhttp.lib, bcrypt.lib  (MSVC: #pragma comment 已内置)
 * 标准: C++17
 *
 * v2.0 改进:
 *   - RAII WinHTTP / BCrypt handle 管理（杜绝泄漏）
 *   - 日志系统 (LogLevel + 回调)
 *   - 线程安全修复 (atomic, mutex 保护)
 *   - 请求队列改用 join 而非 detach（消除 UAF）
 *   - 重试机制 (setRetryPolicy)
 *   - 代理支持 (setProxy)
 *   - 取消令牌 (CancelToken)
 *   - HEAD 方法
 *   - SSE lineBuffer O(n²) 修复
 *   - URL scheme 大小写不敏感
 *   - boundary RNG 改用 random_device + thread_local
 *   - 响应 body 延迟生成 (bodyAsString)
 *   - downloadFileWithMetadata 文件创建失败检查
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
#include <io.h>
#include <fcntl.h>

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
#include <memory>
#include <cctype>

namespace drx { namespace sdk { namespace network { namespace http {

// ═══════════════════════════════════════════════════════════════════════════
//  Forward Declarations & Types
// ═══════════════════════════════════════════════════════════════════════════

using Headers     = std::map<std::string, std::string>;
using QueryParams = std::map<std::string, std::string>;

namespace detail {
std::string decode_body_to_utf8(const std::vector<uint8_t>& bodyBytes, const Headers& headers);
}

/// 进度回调: (已传输字节, 总字节 —— 若未知则 total == -1)
using ProgressCallback = std::function<void(int64_t current, int64_t total)>;

// ═══════════════════════════════════════════════════════════════════════════
//  日志
// ═══════════════════════════════════════════════════════════════════════════

enum class LogLevel { Debug, Info, Warn, Error };

using LogCallback = std::function<void(LogLevel level, const std::string& message)>;

// ═══════════════════════════════════════════════════════════════════════════
//  取消令牌
// ═══════════════════════════════════════════════════════════════════════════

/// 线程安全的取消令牌。多个请求可共享同一 token。
class CancelToken
{
public:
    CancelToken() : cancelled_(std::make_shared<std::atomic<bool>>(false)) {}
    void cancel() { cancelled_->store(true); }
    bool isCancelled() const { return cancelled_->load(); }
    void reset() { cancelled_->store(false); }
private:
    std::shared_ptr<std::atomic<bool>> cancelled_;
};

// ═══════════════════════════════════════════════════════════════════════════
//  HttpResponse
// ═══════════════════════════════════════════════════════════════════════════

struct HttpResponse
{
    int                     statusCode   = 0;
    std::vector<uint8_t>    bodyBytes;       ///< 原始响应体
    Headers                 headers;         ///< 响应头
    std::string             reasonPhrase;    ///< e.g. "OK", "Not Found"

    bool ok() const { return statusCode >= 200 && statusCode < 300; }

    /// 按需转为 UTF-8 字符串（避免双存储）
    std::string bodyAsString() const
    {
        return detail::decode_body_to_utf8(bodyBytes, headers);
    }

    /// 兼容旧代码的 body 字段 —— 调用 bodyAsString()
    std::string body() const { return bodyAsString(); }
};

// ═══════════════════════════════════════════════════════════════════════════
//  HttpRequest  (高级用法)
// ═══════════════════════════════════════════════════════════════════════════

struct HttpRequest
{
    std::string             method = "GET";
    std::string             url;
    std::string             body;
    std::vector<uint8_t>    bodyBytes;
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
//  DownloadResult
// ═══════════════════════════════════════════════════════════════════════════

struct DownloadResult
{
    int         statusCode      = 0;
    int64_t     totalBytes      = -1;
    int64_t     downloadedBytes = 0;
    std::string contentType;
    std::string fileName;
    std::string fileHash;
    std::string savedFilePath;
    std::string etag;
    Headers     serverMetadata;
};

// ═══════════════════════════════════════════════════════════════════════════
//  SSE Event
// ═══════════════════════════════════════════════════════════════════════════

struct SseEvent
{
    std::string event;
    std::string data;
    std::string id;
    int         retry = -1;
};

// ═══════════════════════════════════════════════════════════════════════════
//  重试策略
// ═══════════════════════════════════════════════════════════════════════════

struct RetryPolicy
{
    int  maxRetries      = 0;     ///< 0 = 不重试
    int  baseDelayMs     = 500;   ///< 首次重试延迟
    bool exponentialBackoff = true;
    /// 是否应重试此状态码 (默认 5xx + 408 + 429)
    std::function<bool(int statusCode)> shouldRetry = [](int code) {
        return code >= 500 || code == 408 || code == 429;
    };
};

// ═══════════════════════════════════════════════════════════════════════════
//  Internal Helpers
// ═══════════════════════════════════════════════════════════════════════════

namespace detail {

// ──────── RAII WinHTTP Handle ────────

class WinHttpHandle
{
public:
    WinHttpHandle() = default;
    explicit WinHttpHandle(HINTERNET h) : h_(h) {}
    ~WinHttpHandle() { close(); }

    WinHttpHandle(const WinHttpHandle&) = delete;
    WinHttpHandle& operator=(const WinHttpHandle&) = delete;

    WinHttpHandle(WinHttpHandle&& o) noexcept : h_(o.h_) { o.h_ = nullptr; }
    WinHttpHandle& operator=(WinHttpHandle&& o) noexcept { close(); h_ = o.h_; o.h_ = nullptr; return *this; }

    HINTERNET get() const { return h_; }
    explicit operator bool() const { return h_ != nullptr; }
    HINTERNET release() { auto tmp = h_; h_ = nullptr; return tmp; }
    void reset(HINTERNET h = nullptr) { close(); h_ = h; }

private:
    HINTERNET h_ = nullptr;
    void close() { if (h_) { WinHttpCloseHandle(h_); h_ = nullptr; } }
};

// ──────── RAII BCrypt Handles ────────

class BcryptAlg
{
public:
    BcryptAlg() = default;
    ~BcryptAlg() { if (h_) BCryptCloseAlgorithmProvider(h_, 0); }
    BcryptAlg(const BcryptAlg&) = delete;
    BcryptAlg& operator=(const BcryptAlg&) = delete;

    NTSTATUS open(LPCWSTR algId) { return BCryptOpenAlgorithmProvider(&h_, algId, nullptr, 0); }
    BCRYPT_ALG_HANDLE get() const { return h_; }
private:
    BCRYPT_ALG_HANDLE h_ = nullptr;
};

class BcryptHash
{
public:
    BcryptHash() = default;
    ~BcryptHash() { if (h_) BCryptDestroyHash(h_); }
    BcryptHash(const BcryptHash&) = delete;
    BcryptHash& operator=(const BcryptHash&) = delete;

    NTSTATUS create(BCRYPT_ALG_HANDLE alg) { return BCryptCreateHash(alg, &h_, nullptr, 0, nullptr, 0, 0); }
    NTSTATUS update(const void* data, ULONG len) { return BCryptHashData(h_, (PUCHAR)data, len, 0); }
    NTSTATUS finish(std::vector<uint8_t>& out, DWORD hashLen) { out.resize(hashLen); return BCryptFinishHash(h_, out.data(), hashLen, 0); }
    BCRYPT_HASH_HANDLE get() const { return h_; }
private:
    BCRYPT_HASH_HANDLE h_ = nullptr;
};

// ──────── 字符转换 ────────

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

// ──────── 字符串辅助 ────────

inline std::string to_lower(const std::string& s)
{
    std::string out = s;
    std::transform(out.begin(), out.end(), out.begin(), [](unsigned char c) { return (char)std::tolower(c); });
    return out;
}

inline bool iequals(const std::string& a, const std::string& b)
{
    if (a.size() != b.size()) return false;
    for (size_t i = 0; i < a.size(); ++i)
        if (std::tolower((unsigned char)a[i]) != std::tolower((unsigned char)b[i])) return false;
    return true;
}

inline std::string trim_copy(const std::string& s)
{
    size_t begin = 0;
    while (begin < s.size() && std::isspace((unsigned char)s[begin])) ++begin;
    size_t end = s.size();
    while (end > begin && std::isspace((unsigned char)s[end - 1])) --end;
    return s.substr(begin, end - begin);
}

inline std::string get_header_ci(const Headers& headers, const std::string& name)
{
    for (const auto& kv : headers) {
        if (iequals(kv.first, name)) return kv.second;
    }
    return {};
}

inline std::string extract_charset_from_content_type(const std::string& contentType)
{
    if (contentType.empty()) return {};

    size_t pos = 0;
    while (pos < contentType.size()) {
        size_t next = contentType.find(';', pos);
        std::string part = (next == std::string::npos)
            ? contentType.substr(pos)
            : contentType.substr(pos, next - pos);
        part = trim_copy(part);

        auto eq = part.find('=');
        if (eq != std::string::npos) {
            auto key = trim_copy(part.substr(0, eq));
            auto val = trim_copy(part.substr(eq + 1));

            if ((val.size() >= 2) && ((val.front() == '"' && val.back() == '"') || (val.front() == '\'' && val.back() == '\''))) {
                val = val.substr(1, val.size() - 2);
            }

            if (iequals(key, "charset")) {
                return trim_copy(val);
            }
        }

        if (next == std::string::npos) break;
        pos = next + 1;
    }
    return {};
}

inline std::string normalize_charset_token(const std::string& charset)
{
    std::string s = to_lower(trim_copy(charset));

    if ((s.size() >= 2) && ((s.front() == '"' && s.back() == '"') || (s.front() == '\'' && s.back() == '\''))) {
        s = s.substr(1, s.size() - 2);
    }

    std::string out;
    out.reserve(s.size());
    for (unsigned char c : s) {
        if (c == ' ' || c == '\t' || c == '-' || c == '_' || c == '.') continue;
        out.push_back((char)c);
    }
    return out;
}

enum class CharsetKind {
    Unknown,
    CodePage,
    Utf16Le,
    Utf16Be
};

struct CharsetSpec {
    CharsetKind kind = CharsetKind::Unknown;
    UINT codePage = 0;
};

inline bool parse_uint(const std::string& s, UINT& out)
{
    if (s.empty()) return false;
    unsigned long long v = 0;
    for (unsigned char c : s) {
        if (c < '0' || c > '9') return false;
        v = v * 10 + (c - '0');
        if (v > 65535ULL) return false;
    }
    out = (UINT)v;
    return true;
}

inline CharsetSpec parse_charset_codepage(const std::string& charset)
{
    CharsetSpec spec;
    auto n = normalize_charset_token(charset);
    if (n.empty()) return spec;

    if (n == "utf8") return { CharsetKind::CodePage, CP_UTF8 };
    if (n == "utf16" || n == "utf16le" || n == "ucs2" || n == "unicode") return { CharsetKind::Utf16Le, 0 };
    if (n == "utf16be") return { CharsetKind::Utf16Be, 0 };

    if (n == "gbk" || n == "gb2312" || n == "gb18030" || n == "cp936" || n == "ms936") return { CharsetKind::CodePage, 936 };
    if (n == "big5" || n == "cp950") return { CharsetKind::CodePage, 950 };
    if (n == "latin1" || n == "iso88591") return { CharsetKind::CodePage, 28591 };
    if (n == "windows1252" || n == "cp1252") return { CharsetKind::CodePage, 1252 };
    if (n == "usascii" || n == "ascii") return { CharsetKind::CodePage, 20127 };
    if (n == "shiftjis" || n == "sjis" || n == "cp932" || n == "mskanji") return { CharsetKind::CodePage, 932 };

    UINT parsed = 0;
    if (parse_uint(n, parsed)) return { CharsetKind::CodePage, parsed };
    if (n.rfind("cp", 0) == 0 && parse_uint(n.substr(2), parsed)) return { CharsetKind::CodePage, parsed };
    if (n.rfind("windows", 0) == 0 && parse_uint(n.substr(7), parsed)) return { CharsetKind::CodePage, parsed };
    if (n.rfind("ibm", 0) == 0 && parse_uint(n.substr(3), parsed)) return { CharsetKind::CodePage, parsed };

    return spec;
}

inline bool bytes_to_utf8_via_codepage(const std::vector<uint8_t>& bytes, UINT codePage, bool strictUtf8, std::string& out)
{
    out.clear();
    if (bytes.empty()) return true;

    DWORD flags = (strictUtf8 && codePage == CP_UTF8) ? MB_ERR_INVALID_CHARS : 0;

    int wlen = MultiByteToWideChar(codePage,
                                   flags,
                                   reinterpret_cast<const char*>(bytes.data()),
                                   (int)bytes.size(),
                                   nullptr,
                                   0);
    if (wlen <= 0) return false;

    std::wstring ws((size_t)wlen, 0);
    if (MultiByteToWideChar(codePage,
                            flags,
                            reinterpret_cast<const char*>(bytes.data()),
                            (int)bytes.size(),
                            ws.data(),
                            wlen) <= 0) {
        return false;
    }

    int u8len = WideCharToMultiByte(CP_UTF8, 0, ws.data(), (int)ws.size(), nullptr, 0, nullptr, nullptr);
    if (u8len <= 0) return false;

    out.assign((size_t)u8len, 0);
    if (WideCharToMultiByte(CP_UTF8, 0, ws.data(), (int)ws.size(), out.data(), u8len, nullptr, nullptr) <= 0) {
        out.clear();
        return false;
    }
    return true;
}

inline bool utf16_bytes_to_utf8(const std::vector<uint8_t>& bytes, bool bigEndian, std::string& out)
{
    out.clear();
    if (bytes.empty()) return true;
    if ((bytes.size() % 2) != 0) return false;

    std::wstring ws;
    ws.resize(bytes.size() / 2);

    for (size_t i = 0, j = 0; i + 1 < bytes.size(); i += 2, ++j) {
        uint16_t cu = bigEndian
            ? (uint16_t)(((uint16_t)bytes[i] << 8) | (uint16_t)bytes[i + 1])
            : (uint16_t)(((uint16_t)bytes[i + 1] << 8) | (uint16_t)bytes[i]);
        ws[j] = (wchar_t)cu;
    }

    int u8len = WideCharToMultiByte(CP_UTF8, 0, ws.data(), (int)ws.size(), nullptr, 0, nullptr, nullptr);
    if (u8len <= 0) return false;

    out.assign((size_t)u8len, 0);
    if (WideCharToMultiByte(CP_UTF8, 0, ws.data(), (int)ws.size(), out.data(), u8len, nullptr, nullptr) <= 0) {
        out.clear();
        return false;
    }
    return true;
}

inline std::string decode_body_to_utf8(const std::vector<uint8_t>& bodyBytes, const Headers& headers)
{
    if (bodyBytes.empty()) return {};

    std::string out;

    // 1) BOM 优先
    if (bodyBytes.size() >= 3 && bodyBytes[0] == 0xEF && bodyBytes[1] == 0xBB && bodyBytes[2] == 0xBF) {
        std::vector<uint8_t> payload(bodyBytes.begin() + 3, bodyBytes.end());
        if (bytes_to_utf8_via_codepage(payload, CP_UTF8, false, out)) return out;
    }
    if (bodyBytes.size() >= 2 && bodyBytes[0] == 0xFF && bodyBytes[1] == 0xFE) {
        std::vector<uint8_t> payload(bodyBytes.begin() + 2, bodyBytes.end());
        if (utf16_bytes_to_utf8(payload, false, out)) return out;
    }
    if (bodyBytes.size() >= 2 && bodyBytes[0] == 0xFE && bodyBytes[1] == 0xFF) {
        std::vector<uint8_t> payload(bodyBytes.begin() + 2, bodyBytes.end());
        if (utf16_bytes_to_utf8(payload, true, out)) return out;
    }

    // 2) Content-Type charset
    auto contentType = get_header_ci(headers, "Content-Type");
    auto charset = extract_charset_from_content_type(contentType);
    if (!charset.empty()) {
        auto spec = parse_charset_codepage(charset);
        if (spec.kind == CharsetKind::Utf16Le) {
            if (utf16_bytes_to_utf8(bodyBytes, false, out)) return out;
        } else if (spec.kind == CharsetKind::Utf16Be) {
            if (utf16_bytes_to_utf8(bodyBytes, true, out)) return out;
        } else if (spec.kind == CharsetKind::CodePage && spec.codePage != 0) {
            if (bytes_to_utf8_via_codepage(bodyBytes, spec.codePage, false, out)) return out;
        }
    }

    // 3) 回退: UTF-8(严格) -> ACP -> 原始字节
    if (bytes_to_utf8_via_codepage(bodyBytes, CP_UTF8, true, out)) return out;
    if (bytes_to_utf8_via_codepage(bodyBytes, CP_ACP, false, out)) return out;

    return std::string(bodyBytes.begin(), bodyBytes.end());
}

// ──────── URL 编码 ────────

inline std::string url_encode(const std::string& s)
{
    static const char hex[] = "0123456789ABCDEF";
    std::string out;
    out.reserve(s.size());
    for (unsigned char c : s) {
        if (std::isalnum(c) || c == '-' || c == '_' || c == '.' || c == '~')
            out += (char)c;
        else { out += '%'; out += hex[(c >> 4) & 0x0F]; out += hex[c & 0x0F]; }
    }
    return out;
}

inline std::string build_url(const std::string& url, const QueryParams& query)
{
    if (query.empty()) return url;
    std::string out = url + (url.find('?') != std::string::npos ? "&" : "?");
    bool first = true;
    for (auto& [k, v] : query) {
        if (!first) out += "&";
        out += url_encode(k) + "=" + url_encode(v);
        first = false;
    }
    return out;
}

// ──────── URL 解析（大小写不敏感 scheme） ────────

struct UrlParts
{
    bool        isHttps = false;
    std::string host;
    uint16_t    port    = 0;
    std::string path;
};

inline UrlParts parse_url(const std::string& fullUrl)
{
    UrlParts p;
    std::string url = fullUrl;

    // scheme (大小写不敏感)
    auto lower8 = (url.size() >= 8) ? to_lower(url.substr(0, 8)) : "";
    auto lower7 = (url.size() >= 7) ? to_lower(url.substr(0, 7)) : "";

    if (lower8 == "https://") { p.isHttps = true;  url = url.substr(8); }
    else if (lower7 == "http://") { p.isHttps = false; url = url.substr(7); }

    auto slashPos = url.find('/');
    std::string hostPart = (slashPos != std::string::npos) ? url.substr(0, slashPos) : url;
    p.path = (slashPos != std::string::npos) ? url.substr(slashPos) : "/";

    auto colonPos = hostPart.rfind(':');
    if (colonPos != std::string::npos && colonPos > 0) {
        p.host = hostPart.substr(0, colonPos);
        try {
            int port = std::stoi(hostPart.substr(colonPos + 1));
            if (port > 0 && port <= 65535) p.port = (uint16_t)port;
            else p.port = p.isHttps ? 443 : 80;
        } catch (...) {
            p.port = p.isHttps ? 443 : 80;
        }
    } else {
        p.host = hostPart;
        p.port = p.isHttps ? 443 : 80;
    }
    return p;
}

inline std::string resolve_url(const std::string& baseAddress, const std::string& url)
{
    if (url.find("://") != std::string::npos) return url;
    if (baseAddress.empty()) return url;
    std::string base = baseAddress;
    if (!base.empty() && base.back() == '/') base.pop_back();
    std::string rel = url;
    if (!rel.empty() && rel.front() != '/') rel = "/" + rel;
    return base + rel;
}

// ──────── Multipart boundary (thread_local + random_device) ────────

inline std::string generate_boundary()
{
    static const char alphanum[] = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    thread_local std::mt19937 rng(std::random_device{}());
    std::uniform_int_distribution<int> dist(0, (int)(sizeof(alphanum) - 2));

    std::string boundary = "----DrxBoundary";
    for (int i = 0; i < 16; ++i) boundary += alphanum[dist(rng)];
    return boundary;
}

// ──────── SHA256 (RAII) ────────

inline std::string sha256_hex(const void* data, size_t size)
{
    BcryptAlg alg;
    if (!BCRYPT_SUCCESS(alg.open(BCRYPT_SHA256_ALGORITHM)))
        throw std::runtime_error("BCryptOpenAlgorithmProvider failed");

    DWORD hashLen = 0, resultLen = 0;
    BCryptGetProperty(alg.get(), BCRYPT_HASH_LENGTH, (PUCHAR)&hashLen, sizeof(hashLen), &resultLen, 0);

    BcryptHash hash;
    if (!BCRYPT_SUCCESS(hash.create(alg.get())))
        throw std::runtime_error("BCryptCreateHash failed");

    if (!BCRYPT_SUCCESS(hash.update(data, (ULONG)size)))
        throw std::runtime_error("BCryptHashData failed");

    std::vector<uint8_t> hashBuf;
    if (!BCRYPT_SUCCESS(hash.finish(hashBuf, hashLen)))
        throw std::runtime_error("BCryptFinishHash failed");

    static const char hex[] = "0123456789abcdef";
    std::string out;
    out.reserve(hashLen * 2);
    for (auto b : hashBuf) { out += hex[(b >> 4) & 0x0F]; out += hex[b & 0x0F]; }
    return out;
}

inline std::string sha256_hex(const std::vector<uint8_t>& data)
{
    return sha256_hex(data.data(), data.size());
}

inline std::string sha256_file(const std::string& filePath)
{
    std::ifstream ifs(filePath, std::ios::binary);
    if (!ifs) return {};

    BcryptAlg alg;
    if (!BCRYPT_SUCCESS(alg.open(BCRYPT_SHA256_ALGORITHM))) return {};

    DWORD hashLen = 0, resultLen = 0;
    BCryptGetProperty(alg.get(), BCRYPT_HASH_LENGTH, (PUCHAR)&hashLen, sizeof(hashLen), &resultLen, 0);

    BcryptHash hash;
    if (!BCRYPT_SUCCESS(hash.create(alg.get()))) return {};

    char buf[81920];
    while (ifs.read(buf, sizeof(buf)) || ifs.gcount() > 0) {
        if (!BCRYPT_SUCCESS(hash.update(buf, (ULONG)ifs.gcount()))) return {};
    }

    std::vector<uint8_t> hashBuf;
    if (!BCRYPT_SUCCESS(hash.finish(hashBuf, hashLen))) return {};

    static const char hex[] = "0123456789abcdef";
    std::string out;
    out.reserve(hashLen * 2);
    for (auto b : hashBuf) { out += hex[(b >> 4) & 0x0F]; out += hex[b & 0x0F]; }
    return out;
}

// ──────── JSON 辅助 (Cookie 导入/导出) ────────

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
    return json.substr(pos + 1, 10).find("true") != std::string::npos;
}

// ──────── ASCII header 转义 ────────

inline std::string ensure_ascii_header(const std::string& value)
{
    bool allAscii = true;
    for (unsigned char c : value) { if (c > 127) { allAscii = false; break; } }
    if (allAscii) return value;

    static const char hex[] = "0123456789ABCDEF";
    std::string out;
    for (unsigned char c : value) {
        if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '-' || c == '_' || c == '.' || c == '~')
            out += (char)c;
        else { out += '%'; out += hex[(c >> 4) & 0x0F]; out += hex[c & 0x0F]; }
    }
    return out;
}

// ──────── WinHTTP 错误描述 ────────

inline std::string winhttp_error_string(DWORD err)
{
    switch (err) {
        case 12001: return "error=12001 (OUT_OF_HANDLES)";
        case 12002: return "error=12002 (TIMEOUT)";
        case 12004: return "error=12004 (INTERNAL_ERROR)";
        case 12005: return "error=12005 (INVALID_URL)";
        case 12007: return "error=12007 (NAME_NOT_RESOLVED) DNS lookup failed";
        case 12009: return "error=12009 (INVALID_OPTION)";
        case 12029: return "error=12029 (CANNOT_CONNECT) Connection refused or unreachable";
        case 12030: return "error=12030 (CONNECTION_ERROR)";
        case 12038: return "error=12038 (SECURE_CERT_CN_INVALID) Certificate CN mismatch";
        case 12044: return "error=12044 (SECURE_CERT_DATE_INVALID) Certificate expired";
        case 12045: return "error=12045 (SECURE_CERT_REV_FAILED)";
        case 12057: return "error=12057 (SECURE_CERT_REVOKED)";
        case 12157: return "error=12157 (SECURE_CHANNEL_ERROR) TLS/SSL handshake failed";
        case 12175: return "error=12175 (SECURE_FAILURE) SSL certificate error — try setting ignoreSslErrors=true";
        case 12178: return "error=12178 (AUTO_PROXY_SERVICE_ERROR)";
        default:    return "error=" + std::to_string(err);
    }
}

} // namespace detail

// ═══════════════════════════════════════════════════════════════════════════
//  DrxHttpClient
// ═══════════════════════════════════════════════════════════════════════════

class DrxHttpClient
{
public:
    // ──────────────────────────── 构造 / 析构 ────────────────────────────

    DrxHttpClient()
    {
        init_session();
    }

    explicit DrxHttpClient(const std::string& baseAddress)
        : baseAddress_(baseAddress)
    {
        init_session();
    }

    ~DrxHttpClient()
    {
        stop_queue();
        // hSession_ 由 RAII handle 自动关闭
    }

    // 不可复制，不可移动（队列线程绑定了 this）
    DrxHttpClient(const DrxHttpClient&) = delete;
    DrxHttpClient& operator=(const DrxHttpClient&) = delete;
    DrxHttpClient(DrxHttpClient&&) = delete;
    DrxHttpClient& operator=(DrxHttpClient&&) = delete;

    // ──────────────────────────── 日志 ──────────────────────────────────

    /// 设置日志回调
    void setLogCallback(LogCallback cb)
    {
        std::lock_guard<std::mutex> lock(mu_);
        logCallback_ = std::move(cb);
    }

    // ──────────────────────────── 属性 (线程安全) ───────────────────────

    void setAutoManageCookies(bool v) { autoManageCookies_.store(v); }
    bool getAutoManageCookies() const { return autoManageCookies_.load(); }

    void setIgnoreSslErrors(bool v) { ignoreSslErrors_.store(v); }
    bool getIgnoreSslErrors() const { return ignoreSslErrors_.load(); }

    void setSessionCookieName(const std::string& name)
    {
        std::lock_guard<std::mutex> lock(mu_);
        sessionCookieName_ = name;
    }
    std::string getSessionCookieName() const
    {
        std::lock_guard<std::mutex> lock(mu_);
        return sessionCookieName_;
    }

    void setSessionHeaderName(const std::string& name)
    {
        std::lock_guard<std::mutex> lock(mu_);
        sessionHeaderName_ = name;
    }
    std::string getSessionHeaderName() const
    {
        std::lock_guard<std::mutex> lock(mu_);
        return sessionHeaderName_;
    }

    // 兼容旧代码：public 字段已移除，改用 get/set 方法。
    // 如需快速迁移可用宏，但推荐使用 get/set。

    // ──────────────────────────── 配置方法 ──────────────────────────────

    /// 设置默认请求头
    void setDefaultHeader(const std::string& name, const std::string& value)
    {
        std::lock_guard<std::mutex> lock(mu_);
        defaultHeaders_[name] = detail::ensure_ascii_header(value);
        log(LogLevel::Debug, "Set default header: " + name);
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
        timeoutMs_.store(timeoutMs);
        log(LogLevel::Debug, "Timeout set to " + std::to_string(timeoutMs) + "ms");
    }

    /// 设置超时 (秒)
    void setTimeoutSeconds(double seconds)
    {
        setTimeout((int)(seconds * 1000.0));
    }

    /// 设置重试策略
    void setRetryPolicy(const RetryPolicy& policy)
    {
        std::lock_guard<std::mutex> lock(mu_);
        retryPolicy_ = policy;
    }

    void setRetryPolicy(int maxRetries, int baseDelayMs = 500, bool exponentialBackoff = true)
    {
        RetryPolicy p;
        p.maxRetries = maxRetries;
        p.baseDelayMs = baseDelayMs;
        p.exponentialBackoff = exponentialBackoff;
        setRetryPolicy(p);
    }

    /// 设置 HTTP 代理
    void setProxy(const std::string& proxyUrl)
    {
        if (proxyUrl.empty()) return;
        auto wProxy = detail::to_wide(proxyUrl);
        WINHTTP_PROXY_INFO proxyInfo;
        proxyInfo.dwAccessType = WINHTTP_ACCESS_TYPE_NAMED_PROXY;
        proxyInfo.lpszProxy = const_cast<LPWSTR>(wProxy.c_str());
        proxyInfo.lpszProxyBypass = WINHTTP_NO_PROXY_BYPASS;
        WinHttpSetOption(hSession_.get(), WINHTTP_OPTION_PROXY, &proxyInfo, sizeof(proxyInfo));
        log(LogLevel::Info, "Proxy set to: " + proxyUrl);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  便捷请求方法
    // ══════════════════════════════════════════════════════════════════════

    HttpResponse get(const std::string& url,
                     const Headers& headers = {},
                     const QueryParams& query = {},
                     CancelToken* cancel = nullptr)
    {
        return send("GET", url, "", {}, headers, query, cancel);
    }

    HttpResponse head(const std::string& url,
                      const Headers& headers = {},
                      const QueryParams& query = {},
                      CancelToken* cancel = nullptr)
    {
        return send("HEAD", url, "", {}, headers, query, cancel);
    }

    HttpResponse post(const std::string& url,
                      const std::string& body = "",
                      const Headers& headers = {},
                      const QueryParams& query = {},
                      CancelToken* cancel = nullptr)
    {
        return send("POST", url, body, {}, headers, query, cancel);
    }

    HttpResponse post(const std::string& url,
                      const std::vector<uint8_t>& bodyBytes,
                      const Headers& headers = {},
                      const QueryParams& query = {},
                      CancelToken* cancel = nullptr)
    {
        return send("POST", url, "", bodyBytes, headers, query, cancel);
    }

    HttpResponse put(const std::string& url,
                     const std::string& body = "",
                     const Headers& headers = {},
                     const QueryParams& query = {},
                     CancelToken* cancel = nullptr)
    {
        return send("PUT", url, body, {}, headers, query, cancel);
    }

    HttpResponse put(const std::string& url,
                     const std::vector<uint8_t>& bodyBytes,
                     const Headers& headers = {},
                     const QueryParams& query = {},
                     CancelToken* cancel = nullptr)
    {
        return send("PUT", url, "", bodyBytes, headers, query, cancel);
    }

    HttpResponse del(const std::string& url,
                     const Headers& headers = {},
                     const QueryParams& query = {},
                     CancelToken* cancel = nullptr)
    {
        return send("DELETE", url, "", {}, headers, query, cancel);
    }

    HttpResponse patch(const std::string& url,
                       const std::string& body = "",
                       const Headers& headers = {},
                       const QueryParams& query = {},
                       CancelToken* cancel = nullptr)
    {
        return send("PATCH", url, body, {}, headers, query, cancel);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  通用 Send (含重试)
    // ══════════════════════════════════════════════════════════════════════

    HttpResponse send(const std::string& method,
                      const std::string& url,
                      const std::string& body = "",
                      const std::vector<uint8_t>& bodyBytes = {},
                      const Headers& headers = {},
                      const QueryParams& query = {},
                      CancelToken* cancel = nullptr)
    {
        RetryPolicy policy;
        {
            std::lock_guard<std::mutex> lock(mu_);
            policy = retryPolicy_;
        }

        int attempt = 0;
        while (true) {
            if (cancel && cancel->isCancelled())
                throw std::runtime_error("Request cancelled");

            try {
                auto resp = send_internal(method, url, body, bodyBytes, headers, query, cancel);

                // 检查是否需要重试
                if (attempt < policy.maxRetries && policy.shouldRetry && policy.shouldRetry(resp.statusCode)) {
                    int delay = policy.baseDelayMs;
                    if (policy.exponentialBackoff) delay *= (1 << attempt);
                    log(LogLevel::Warn, "Retrying [" + std::to_string(attempt + 1) + "/" + std::to_string(policy.maxRetries)
                                        + "] after " + std::to_string(delay) + "ms, status=" + std::to_string(resp.statusCode));
                    std::this_thread::sleep_for(std::chrono::milliseconds(delay));
                    ++attempt;
                    continue;
                }
                return resp;
            } catch (const std::runtime_error& ex) {
                if (attempt < policy.maxRetries) {
                    int delay = policy.baseDelayMs;
                    if (policy.exponentialBackoff) delay *= (1 << attempt);
                    log(LogLevel::Warn, "Retrying [" + std::to_string(attempt + 1) + "/" + std::to_string(policy.maxRetries)
                                        + "] after " + std::to_string(delay) + "ms, error: " + ex.what());
                    std::this_thread::sleep_for(std::chrono::milliseconds(delay));
                    ++attempt;
                    continue;
                }
                throw;
            }
        }
    }

    HttpResponse send(const HttpRequest& req, CancelToken* cancel = nullptr)
    {
        return send(req.method, req.url, req.body, req.bodyBytes, req.headers, req.query, cancel);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  文件上传
    // ══════════════════════════════════════════════════════════════════════

    HttpResponse uploadFile(const std::string& url,
                            const std::string& filePath,
                            const std::string& fieldName = "file",
                            const Headers& headers = {},
                            const QueryParams& query = {},
                            ProgressCallback progress = nullptr,
                            CancelToken* cancel = nullptr)
    {
        namespace fs = std::filesystem;
        if (!fs::exists(filePath))
            throw std::runtime_error("Upload file not found: " + filePath);

        std::ifstream ifs(filePath, std::ios::binary);
        std::vector<uint8_t> fileData((std::istreambuf_iterator<char>(ifs)),
                                       std::istreambuf_iterator<char>());
        auto fileName = fs::path(filePath).filename().string();
        return uploadFile(url, fileData.data(), fileData.size(), fileName, fieldName, headers, query, progress, cancel);
    }

    HttpResponse uploadFile(const std::string& url,
                            const uint8_t* data, size_t dataSize,
                            const std::string& fileName,
                            const std::string& fieldName = "file",
                            const Headers& headers = {},
                            const QueryParams& query = {},
                            ProgressCallback progress = nullptr,
                            CancelToken* cancel = nullptr)
    {
        auto boundary = detail::generate_boundary();
        std::vector<uint8_t> multipartBody;

        {
            std::string header = "--" + boundary + "\r\n"
                + "Content-Disposition: form-data; name=\"" + fieldName + "\"; filename=\"" + fileName + "\"\r\n"
                + "Content-Type: application/octet-stream\r\n\r\n";
            multipartBody.insert(multipartBody.end(), header.begin(), header.end());
        }
        multipartBody.insert(multipartBody.end(), data, data + dataSize);
        {
            std::string tail = "\r\n--" + boundary + "--\r\n";
            multipartBody.insert(multipartBody.end(), tail.begin(), tail.end());
        }

        Headers uploadHeaders = headers;
        uploadHeaders["Content-Type"] = "multipart/form-data; boundary=" + boundary;

        if (progress) progress((int64_t)dataSize, (int64_t)dataSize);
        log(LogLevel::Info, "Uploading file: " + fileName + " (" + std::to_string(dataSize) + " bytes)");
        return send("POST", url, "", multipartBody, uploadHeaders, query, cancel);
    }

    HttpResponse uploadFileWithMetadata(const std::string& url,
                                        const std::string& filePath,
                                        const std::string& metadataJson = "{}",
                                        const Headers& headers = {},
                                        ProgressCallback progress = nullptr,
                                        CancelToken* cancel = nullptr)
    {
        namespace fs = std::filesystem;
        if (!fs::exists(filePath))
            throw std::runtime_error("Upload file not found: " + filePath);

        std::ifstream ifs(filePath, std::ios::binary);
        std::vector<uint8_t> fileData((std::istreambuf_iterator<char>(ifs)),
                                       std::istreambuf_iterator<char>());
        auto fileName = fs::path(filePath).filename().string();
        auto boundary = detail::generate_boundary();

        std::vector<uint8_t> body;
        {
            std::string s = "--" + boundary + "\r\n"
                + "Content-Disposition: form-data; name=\"file\"; filename=\"" + fileName + "\"\r\n"
                + "Content-Type: application/octet-stream\r\n\r\n";
            body.insert(body.end(), s.begin(), s.end());
        }
        body.insert(body.end(), fileData.begin(), fileData.end());
        {
            std::string s = "\r\n--" + boundary + "\r\n"
                + "Content-Disposition: form-data; name=\"metadata\"\r\n"
                + "Content-Type: application/json; charset=utf-8\r\n\r\n"
                + metadataJson
                + "\r\n--" + boundary + "--\r\n";
            body.insert(body.end(), s.begin(), s.end());
        }

        Headers uploadHeaders = headers;
        uploadHeaders["Content-Type"] = "multipart/form-data; boundary=" + boundary;
        uploadHeaders["X-File-Name"]  = fileName;

        if (progress) progress((int64_t)fileData.size(), (int64_t)fileData.size());
        log(LogLevel::Info, "Uploading file with metadata: " + fileName);
        return send("POST", url, "", body, uploadHeaders, {}, cancel);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  文件下载
    // ══════════════════════════════════════════════════════════════════════

    void downloadFile(const std::string& url,
                      const std::string& destPath,
                      const Headers& headers = {},
                      const QueryParams& query = {},
                      ProgressCallback progress = nullptr,
                      CancelToken* cancel = nullptr)
    {
        auto fullUrl = detail::resolve_url(baseAddress_, detail::build_url(url, query));
        auto parts   = detail::parse_url(fullUrl);

        detail::WinHttpHandle hConnect, hRequest;
        open_download_request(parts, headers, hConnect, hRequest);

        int64_t totalBytes = get_content_length(hRequest.get());

        namespace fs = std::filesystem;
        auto dir = fs::path(destPath).parent_path();
        if (!dir.empty()) fs::create_directories(dir);

        auto tempFile = destPath + ".download.tmp";
        {
            std::ofstream ofs(tempFile, std::ios::binary);
            if (!ofs)
                throw std::runtime_error("Cannot create temp file: " + tempFile);

            char buf[81920];
            DWORD bytesRead = 0;
            int64_t totalRead = 0;
            while (WinHttpReadData(hRequest.get(), buf, sizeof(buf), &bytesRead) && bytesRead > 0) {
                if (cancel && cancel->isCancelled()) {
                    ofs.close();
                    fs::remove(tempFile);
                    throw std::runtime_error("Download cancelled");
                }
                ofs.write(buf, bytesRead);
                totalRead += bytesRead;
                if (progress) progress(totalRead, totalBytes);
                bytesRead = 0;
            }
        }

        if (autoManageCookies_.load()) parse_set_cookies(hRequest.get(), parts.host);

        atomic_file_replace(tempFile, destPath);
        log(LogLevel::Info, "Downloaded: " + url + " -> " + destPath);
    }

    std::string downloadFileWithHash(const std::string& url,
                                     const std::string& destPath,
                                     const std::string& expectedHash = "",
                                     const Headers& headers = {},
                                     const QueryParams& query = {},
                                     ProgressCallback progress = nullptr,
                                     CancelToken* cancel = nullptr)
    {
        downloadFile(url, destPath, headers, query, progress, cancel);
        auto fileHash = detail::sha256_file(destPath);

        if (!expectedHash.empty() && !detail::iequals(fileHash, expectedHash)) {
            std::filesystem::remove(destPath);
            throw std::runtime_error("Hash mismatch: expected " + expectedHash + ", got " + fileHash);
        }
        log(LogLevel::Info, "Download hash verified: " + fileHash);
        return fileHash;
    }

    DownloadResult downloadFileWithMetadata(const std::string& url,
                                            const std::string& destPath,
                                            const Headers& headers = {},
                                            const QueryParams& query = {},
                                            ProgressCallback progress = nullptr,
                                            CancelToken* cancel = nullptr)
    {
        auto fullUrl = detail::resolve_url(baseAddress_, detail::build_url(url, query));
        auto parts   = detail::parse_url(fullUrl);

        detail::WinHttpHandle hConnect, hRequest;
        open_download_request(parts, headers, hConnect, hRequest);

        DownloadResult result;
        result.statusCode = get_status_code(hRequest.get());
        result.totalBytes = get_content_length(hRequest.get());
        result.contentType = get_header(hRequest.get(), L"Content-Type");
        result.etag = get_header(hRequest.get(), L"ETag");

        auto meta = get_header(hRequest.get(), L"X-MetaData");
        if (!meta.empty()) result.serverMetadata["X-MetaData"] = meta;

        namespace fs = std::filesystem;
        auto dir = fs::path(destPath).parent_path();
        if (!dir.empty()) fs::create_directories(dir);

        auto tempFile = destPath + ".download.tmp";
        {
            std::ofstream ofs(tempFile, std::ios::binary);
            if (!ofs)
                throw std::runtime_error("Cannot create temp file: " + tempFile);

            char buf[81920];
            DWORD bytesRead = 0;
            int64_t totalRead = 0;
            while (WinHttpReadData(hRequest.get(), buf, sizeof(buf), &bytesRead) && bytesRead > 0) {
                if (cancel && cancel->isCancelled()) {
                    ofs.close();
                    fs::remove(tempFile);
                    throw std::runtime_error("Download cancelled");
                }
                ofs.write(buf, bytesRead);
                totalRead += bytesRead;
                if (progress) progress(totalRead, result.totalBytes);
                bytesRead = 0;
            }
            result.downloadedBytes = totalRead;
        }

        if (autoManageCookies_.load()) parse_set_cookies(hRequest.get(), parts.host);

        atomic_file_replace(tempFile, destPath);
        result.savedFilePath = destPath;
        result.fileHash = detail::sha256_file(destPath);
        result.fileName = fs::path(destPath).filename().string();
        return result;
    }

    void downloadToStream(const std::string& url,
                          std::ostream& destination,
                          const Headers& headers = {},
                          const QueryParams& query = {},
                          ProgressCallback progress = nullptr,
                          CancelToken* cancel = nullptr)
    {
        auto fullUrl = detail::resolve_url(baseAddress_, detail::build_url(url, query));
        auto parts   = detail::parse_url(fullUrl);

        detail::WinHttpHandle hConnect, hRequest;
        open_download_request(parts, headers, hConnect, hRequest);

        int64_t totalBytes = get_content_length(hRequest.get());
        char buf[81920];
        DWORD bytesRead = 0;
        int64_t totalRead = 0;

        while (WinHttpReadData(hRequest.get(), buf, sizeof(buf), &bytesRead) && bytesRead > 0) {
            if (cancel && cancel->isCancelled()) throw std::runtime_error("Download cancelled");
            destination.write(buf, bytesRead);
            totalRead += bytesRead;
            if (progress) progress(totalRead, totalBytes);
            bytesRead = 0;
        }

        if (autoManageCookies_.load()) parse_set_cookies(hRequest.get(), parts.host);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  SSE (Server-Sent Events)
    // ══════════════════════════════════════════════════════════════════════

    void connectSse(const std::string& url,
                    std::function<void(const SseEvent&)> onEvent,
                    std::function<bool()> shouldStop = nullptr,
                    const Headers& headers = {},
                    CancelToken* cancel = nullptr)
    {
        auto fullUrl = detail::resolve_url(baseAddress_, url);
        auto parts   = detail::parse_url(fullUrl);

        auto wHost = detail::to_wide(parts.host);
        detail::WinHttpHandle hConnect(WinHttpConnect(hSession_.get(), wHost.c_str(), (INTERNET_PORT)parts.port, 0));
        if (!hConnect) throw std::runtime_error("SSE: WinHttpConnect failed");

        auto wPath = detail::to_wide(parts.path);
        DWORD flags = parts.isHttps ? WINHTTP_FLAG_SECURE : 0;
        detail::WinHttpHandle hRequest(WinHttpOpenRequest(hConnect.get(), L"GET", wPath.c_str(), nullptr,
                                       WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, flags));
        if (!hRequest) throw std::runtime_error("SSE: WinHttpOpenRequest failed");

        // SSL
        apply_ssl_flags(hRequest.get(), parts.isHttps);

        // Headers: defaultHeaders + SSE headers + user headers + cookies
        std::wstring wHeaders;
        {
            std::lock_guard<std::mutex> lock(mu_);
            for (auto& [k, v] : defaultHeaders_)
                wHeaders += detail::to_wide(k) + L": " + detail::to_wide(v) + L"\r\n";
        }
        wHeaders += L"Accept: text/event-stream\r\nCache-Control: no-cache\r\n";
        for (auto& [k, v] : headers)
            wHeaders += detail::to_wide(k) + L": " + detail::to_wide(v) + L"\r\n";

        auto cookieHdr = build_cookie_header(parts.host);
        if (!cookieHdr.empty()) wHeaders += L"Cookie: " + detail::to_wide(cookieHdr) + L"\r\n";

        // Session header
        apply_session_header(wHeaders);

        if (!wHeaders.empty())
            WinHttpAddRequestHeaders(hRequest.get(), wHeaders.c_str(), (DWORD)wHeaders.size(), WINHTTP_ADDREQ_FLAG_ADD);

        if (!WinHttpSendRequest(hRequest.get(), WINHTTP_NO_ADDITIONAL_HEADERS, 0, nullptr, 0, 0, 0) ||
            !WinHttpReceiveResponse(hRequest.get(), nullptr)) {
            throw std::runtime_error("SSE: send/receive failed: " + detail::winhttp_error_string(GetLastError()));
        }

        log(LogLevel::Info, "SSE connected: " + url);

        // 逐行读取 (优化: 用 consumed 偏移避免 O(n²))
        std::string lineBuffer;
        SseEvent currentEvent;
        char buf[4096];
        DWORD bytesRead = 0;

        while (WinHttpReadData(hRequest.get(), buf, sizeof(buf), &bytesRead) && bytesRead > 0) {
            if ((shouldStop && shouldStop()) || (cancel && cancel->isCancelled())) break;

            lineBuffer.append(buf, bytesRead);
            bytesRead = 0;

            size_t consumed = 0;
            size_t pos;
            while ((pos = lineBuffer.find('\n', consumed)) != std::string::npos) {
                auto line = lineBuffer.substr(consumed, pos - consumed);
                consumed = pos + 1;
                if (!line.empty() && line.back() == '\r') line.pop_back();

                if (line.empty()) {
                    if (!currentEvent.data.empty()) {
                        if (currentEvent.event.empty()) currentEvent.event = "message";
                        onEvent(currentEvent);
                    }
                    currentEvent = {};
                } else if (line.size() >= 5 && line.substr(0, 5) == "data:") {
                    auto data = line.substr(5);
                    if (!data.empty() && data[0] == ' ') data = data.substr(1);
                    if (!currentEvent.data.empty()) currentEvent.data += "\n";
                    currentEvent.data += data;
                } else if (line.size() >= 6 && line.substr(0, 6) == "event:") {
                    auto ev = line.substr(6);
                    if (!ev.empty() && ev[0] == ' ') ev = ev.substr(1);
                    currentEvent.event = ev;
                } else if (line.size() >= 3 && line.substr(0, 3) == "id:") {
                    auto id = line.substr(3);
                    if (!id.empty() && id[0] == ' ') id = id.substr(1);
                    currentEvent.id = id;
                } else if (line.size() >= 6 && line.substr(0, 6) == "retry:") {
                    auto r = line.substr(6);
                    if (!r.empty() && r[0] == ' ') r = r.substr(1);
                    try { currentEvent.retry = std::stoi(r); } catch (...) {}
                }
            }
            lineBuffer.erase(0, consumed);
        }

        log(LogLevel::Info, "SSE disconnected: " + url);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Cookie 管理
    // ══════════════════════════════════════════════════════════════════════

    std::string getSessionId() const
    {
        std::lock_guard<std::mutex> lock(mu_);
        for (auto& c : cookies_) {
            if (detail::iequals(c.name, sessionCookieName_)) return c.value;
        }
        return {};
    }

    void setSessionId(const std::string& sessionId, const std::string& domain = "", const std::string& path = "/")
    {
        if (sessionId.empty()) return;
        std::lock_guard<std::mutex> lock(mu_);
        for (auto& c : cookies_) {
            if (detail::iequals(c.name, sessionCookieName_)) {
                c.value = sessionId;
                return;
            }
        }
        Cookie c;
        c.name = sessionCookieName_;
        c.value = sessionId;
        c.domain = domain.empty() ? extract_host(baseAddress_) : domain;
        c.path = path;
        cookies_.push_back(c);
    }

    void clearCookies()
    {
        std::lock_guard<std::mutex> lock(mu_);
        cookies_.clear();
    }

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

    void importCookies(const std::string& json)
    {
        if (json.empty()) return;
        std::lock_guard<std::mutex> lock(mu_);
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
    //  请求队列
    // ══════════════════════════════════════════════════════════════════════

    /// 启动后台请求队列
    void startQueue(int maxConcurrent = 10)
    {
        if (queueRunning_.load()) return;
        queueRunning_.store(true);
        maxConcurrent_ = maxConcurrent;
        activeTasks_.store(0);
        queueThread_ = std::thread([this]() { queue_worker(); });
    }

    /// 排入队列
    void enqueue(const HttpRequest& req, std::function<void(HttpResponse)> callback)
    {
        {
            std::lock_guard<std::mutex> lock(queueMu_);
            queue_.push({req, std::move(callback)});
        }
        queueCv_.notify_one();
    }

    /// 停止队列（等待所有进行中的请求完成）
    void stopQueue()
    {
        stop_queue();
    }

private:

    // ──────────────────────────── 字段 ──────────────────────────────────

    detail::WinHttpHandle   hSession_;
    std::string             baseAddress_;
    Headers                 defaultHeaders_;
    mutable std::mutex      mu_;
    LogCallback             logCallback_;

    // Cookie
    std::vector<Cookie>     cookies_;
    std::string             sessionCookieName_ = "session_id";
    std::string             sessionHeaderName_;

    // 线程安全属性
    std::atomic<bool>       autoManageCookies_{true};
    std::atomic<bool>       ignoreSslErrors_{false};
    std::atomic<int>        timeoutMs_{0};

    // 重试
    RetryPolicy             retryPolicy_;

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
    std::mutex                      concurrencyMu_;
    std::condition_variable         concurrencyCv_;

    // 工作线程追踪 (防止 UAF)
    std::vector<std::thread>        workerThreads_;
    std::mutex                      workerMu_;

    // ──────────────────────────── 日志 ──────────────────────────────────

    void log(LogLevel level, const std::string& message) const
    {
        std::lock_guard<std::mutex> lock(mu_);
        if (logCallback_) {
            try { logCallback_(level, message); } catch (...) {}
        }
    }

    // ──────────────────────────── 初始化 ────────────────────────────────

    void init_session()
    {
        hSession_.reset(WinHttpOpen(L"DrxHttpClient/2.0",
                                    WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
                                    WINHTTP_NO_PROXY_NAME,
                                    WINHTTP_NO_PROXY_BYPASS, 0));
        if (!hSession_)
            throw std::runtime_error("WinHttpOpen failed");
    }

    // ──────────────────── SSL 标志 ─────────────────────────────────────

    void apply_ssl_flags(HINTERNET hRequest, bool isHttps) const
    {
        if (isHttps && ignoreSslErrors_.load()) {
            DWORD sslFlags = SECURITY_FLAG_IGNORE_UNKNOWN_CA |
                             SECURITY_FLAG_IGNORE_CERT_DATE_INVALID |
                             SECURITY_FLAG_IGNORE_CERT_CN_INVALID |
                             SECURITY_FLAG_IGNORE_CERT_WRONG_USAGE;
            WinHttpSetOption(hRequest, WINHTTP_OPTION_SECURITY_FLAGS, &sslFlags, sizeof(sslFlags));
        }
    }

    // ──────────────────── Session Header 注入 ──────────────────────────

    void apply_session_header(std::wstring& outHeaders) const
    {
        std::string headerName;
        {
            std::lock_guard<std::mutex> lock(mu_);
            headerName = sessionHeaderName_;
        }
        if (!headerName.empty()) {
            auto sid = getSessionId();
            if (!sid.empty()) {
                outHeaders += detail::to_wide(headerName) + L": " + detail::to_wide(detail::ensure_ascii_header(sid)) + L"\r\n";
            }
        }
    }

    // ──────────────────── 核心发送 ─────────────────────────────────────

    HttpResponse send_internal(const std::string& method,
                               const std::string& url,
                               const std::string& body,
                               const std::vector<uint8_t>& bodyBytes,
                               const Headers& headers,
                               const QueryParams& query,
                               CancelToken* cancel)
    {
        auto fullUrl = detail::resolve_url(baseAddress_, detail::build_url(url, query));
        auto parts   = detail::parse_url(fullUrl);

        log(LogLevel::Debug, method + " " + fullUrl);

        auto wHost   = detail::to_wide(parts.host);
        auto wPath   = detail::to_wide(parts.path);
        auto wMethod = detail::to_wide(method);

        detail::WinHttpHandle hConnect(WinHttpConnect(hSession_.get(), wHost.c_str(),
                                                       (INTERNET_PORT)parts.port, 0));
        if (!hConnect)
            throw std::runtime_error("WinHttpConnect failed: " + parts.host);

        DWORD flags = parts.isHttps ? WINHTTP_FLAG_SECURE : 0;
        detail::WinHttpHandle hRequest(WinHttpOpenRequest(hConnect.get(), wMethod.c_str(),
                                                           wPath.c_str(), nullptr,
                                                           WINHTTP_NO_REFERER,
                                                           WINHTTP_DEFAULT_ACCEPT_TYPES, flags));
        if (!hRequest)
            throw std::runtime_error("WinHttpOpenRequest failed");

        // SSL
        apply_ssl_flags(hRequest.get(), parts.isHttps);

        // 超时
        int timeout = timeoutMs_.load();
        if (timeout > 0)
            WinHttpSetTimeouts(hRequest.get(), timeout, timeout, timeout, timeout);

        // Headers
        std::wstring allHeaders;
        {
            std::lock_guard<std::mutex> lock(mu_);
            for (auto& [k, v] : defaultHeaders_)
                allHeaders += detail::to_wide(k) + L": " + detail::to_wide(v) + L"\r\n";
        }
        for (auto& [k, v] : headers)
            allHeaders += detail::to_wide(k) + L": " + detail::to_wide(detail::ensure_ascii_header(v)) + L"\r\n";

        // Content-Type 默认 JSON
        if (!body.empty() && headers.find("Content-Type") == headers.end())
            allHeaders += L"Content-Type: application/json; charset=utf-8\r\n";

        // Cookie header
        auto cookieHeader = build_cookie_header(parts.host);
        if (!cookieHeader.empty())
            allHeaders += L"Cookie: " + detail::to_wide(cookieHeader) + L"\r\n";

        // Session header
        apply_session_header(allHeaders);

        if (!allHeaders.empty())
            WinHttpAddRequestHeaders(hRequest.get(), allHeaders.c_str(), (DWORD)allHeaders.size(), WINHTTP_ADDREQ_FLAG_ADD);

        // 检查取消
        if (cancel && cancel->isCancelled())
            throw std::runtime_error("Request cancelled");

        // 发送
        const void* bodyPtr = nullptr;
        DWORD bodyLen = 0;
        if (!bodyBytes.empty()) { bodyPtr = bodyBytes.data(); bodyLen = (DWORD)bodyBytes.size(); }
        else if (!body.empty()) { bodyPtr = body.data(); bodyLen = (DWORD)body.size(); }

        BOOL ok = WinHttpSendRequest(hRequest.get(),
                                      WINHTTP_NO_ADDITIONAL_HEADERS, 0,
                                      (LPVOID)bodyPtr, bodyLen, bodyLen, 0);
        if (!ok)
            throw std::runtime_error("WinHttpSendRequest failed: " + detail::winhttp_error_string(GetLastError()));

        ok = WinHttpReceiveResponse(hRequest.get(), nullptr);
        if (!ok)
            throw std::runtime_error("WinHttpReceiveResponse failed: " + detail::winhttp_error_string(GetLastError()));

        HttpResponse resp = read_response(hRequest.get());

        if (autoManageCookies_.load())
            parse_set_cookies(hRequest.get(), parts.host);

        log(LogLevel::Debug, "Response: " + std::to_string(resp.statusCode) + " " + resp.reasonPhrase);
        return resp;
        // RAII: hRequest 和 hConnect 自动关闭
    }

    // ──────────────────── 响应读取 ─────────────────────────────────────

    static HttpResponse read_response(HINTERNET hRequest)
    {
        HttpResponse resp;
        resp.statusCode = get_status_code(hRequest);

        // Reason phrase
        {
            DWORD size = 0;
            WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_STATUS_TEXT,
                                WINHTTP_HEADER_NAME_BY_INDEX, nullptr, &size,
                                WINHTTP_NO_HEADER_INDEX);
            if (GetLastError() == ERROR_INSUFFICIENT_BUFFER && size > 0) {
                std::wstring val(size / sizeof(wchar_t), 0);
                if (WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_STATUS_TEXT,
                                        WINHTTP_HEADER_NAME_BY_INDEX, val.data(), &size,
                                        WINHTTP_NO_HEADER_INDEX))
                    resp.reasonPhrase = detail::to_utf8(val);
            }
        }

        // 所有响应头
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

        // Body
        std::vector<uint8_t> allData;
        char buf[81920];
        DWORD bytesRead = 0;
        while (WinHttpReadData(hRequest, buf, sizeof(buf), &bytesRead) && bytesRead > 0) {
            allData.insert(allData.end(), buf, buf + bytesRead);
            bytesRead = 0;
        }
        resp.bodyBytes = std::move(allData);
        return resp;
    }

    // ──────────────────── 下载辅助 ─────────────────────────────────────

    void open_download_request(const detail::UrlParts& parts, const Headers& headers,
                               detail::WinHttpHandle& outConnect, detail::WinHttpHandle& outRequest)
    {
        auto wHost = detail::to_wide(parts.host);
        outConnect.reset(WinHttpConnect(hSession_.get(), wHost.c_str(), (INTERNET_PORT)parts.port, 0));
        if (!outConnect) throw std::runtime_error("Download: WinHttpConnect failed");

        auto wPath = detail::to_wide(parts.path);
        DWORD flags = parts.isHttps ? WINHTTP_FLAG_SECURE : 0;
        outRequest.reset(WinHttpOpenRequest(outConnect.get(), L"GET", wPath.c_str(), nullptr,
                                             WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, flags));
        if (!outRequest) throw std::runtime_error("Download: WinHttpOpenRequest failed");

        apply_ssl_flags(outRequest.get(), parts.isHttps);

        int timeout = timeoutMs_.load();
        if (timeout > 0) WinHttpSetTimeouts(outRequest.get(), timeout, timeout, timeout, timeout);

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

        apply_session_header(wHeaders);

        if (!wHeaders.empty())
            WinHttpAddRequestHeaders(outRequest.get(), wHeaders.c_str(), (DWORD)wHeaders.size(), WINHTTP_ADDREQ_FLAG_ADD);

        if (!WinHttpSendRequest(outRequest.get(), WINHTTP_NO_ADDITIONAL_HEADERS, 0, nullptr, 0, 0, 0) ||
            !WinHttpReceiveResponse(outRequest.get(), nullptr)) {
            throw std::runtime_error("Download: send/receive failed: " + detail::winhttp_error_string(GetLastError()));
        }
    }

    // ──────────────────── WinHTTP 查询辅助 ─────────────────────────────

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
        std::string out;
        for (auto& c : cookies_) {
            bool match = c.domain.empty() || host.find(c.domain) != std::string::npos || detail::iequals(c.domain, host);
            if (match) {
                if (!out.empty()) out += "; ";
                out += c.name + "=" + c.value;
            }
        }
        return out;
    }

    void parse_set_cookies(HINTERNET hRequest, const std::string& host)
    {
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

            parse_single_set_cookie(detail::to_utf8(val), host);
        }
    }

    void parse_single_set_cookie(const std::string& setCookie, const std::string& defaultDomain)
    {
        auto semiPos = setCookie.find(';');
        auto nameVal = (semiPos != std::string::npos) ? setCookie.substr(0, semiPos) : setCookie;
        auto eqPos = nameVal.find('=');
        if (eqPos == std::string::npos) return;

        Cookie c;
        c.name  = nameVal.substr(0, eqPos);
        c.value = nameVal.substr(eqPos + 1);
        c.domain = defaultDomain;
        c.path = "/";

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
        return detail::parse_url(url).host;
    }

    // ──────────────────── 文件辅助 ─────────────────────────────────────

    static void atomic_file_replace(const std::string& tempFile, const std::string& destPath)
    {
        namespace fs = std::filesystem;
        if (fs::exists(destPath)) {
            auto wTemp = detail::to_wide(tempFile);
            auto wDest = detail::to_wide(destPath);
            if (!MoveFileExW(wTemp.c_str(), wDest.c_str(), MOVEFILE_REPLACE_EXISTING)) {
                fs::remove(destPath);
                fs::rename(tempFile, destPath);
            }
        } else {
            fs::rename(tempFile, destPath);
        }
    }

    // ──────────────────── 请求队列 (无 detach, 安全析构) ──────────────

    void queue_worker()
    {
        while (queueRunning_.load()) {
            QueueEntry entry;
            {
                std::unique_lock<std::mutex> lock(queueMu_);
                queueCv_.wait(lock, [this]() { return !queue_.empty() || !queueRunning_.load(); });
                if (!queueRunning_.load() && queue_.empty()) break;
                if (queue_.empty()) continue;
                entry = std::move(queue_.front());
                queue_.pop();
            }

            // 等待并发槽位 (condition_variable, 不再 spin-wait)
            {
                std::unique_lock<std::mutex> lock(concurrencyMu_);
                concurrencyCv_.wait(lock, [this]() { return activeTasks_.load() < maxConcurrent_; });
            }

            activeTasks_++;

            // 用 joinable thread 代替 detach
            auto worker = std::thread([this, entry = std::move(entry)]() mutable {
                try {
                    auto resp = send(entry.request);
                    if (entry.callback) entry.callback(std::move(resp));
                } catch (...) {
                    if (entry.callback) {
                        HttpResponse errResp;
                        errResp.statusCode = -1;
                        entry.callback(std::move(errResp));
                    }
                }
                activeTasks_--;
                concurrencyCv_.notify_one();
            });

            // 追踪工作线程
            {
                std::lock_guard<std::mutex> lock(workerMu_);
                // 清理已完成的线程
                workerThreads_.erase(
                    std::remove_if(workerThreads_.begin(), workerThreads_.end(),
                                   [](std::thread& t) {
                                       if (t.joinable()) {
                                           // 尝试快速 join (已完成的线程)
                                           // 注意: std::thread 没有 try_join, 我们在 stop_queue 中统一 join
                                           return false;
                                       }
                                       return true;
                                   }),
                    workerThreads_.end());
                workerThreads_.push_back(std::move(worker));
            }
        }
    }

    void stop_queue()
    {
        if (!queueRunning_.load()) return;
        queueRunning_.store(false);
        queueCv_.notify_all();

        if (queueThread_.joinable())
            queueThread_.join();

        // 等待所有工作线程完成
        {
            std::lock_guard<std::mutex> lock(workerMu_);
            for (auto& t : workerThreads_) {
                if (t.joinable()) t.join();
            }
            workerThreads_.clear();
        }
    }
};

}}}} // namespace drx::sdk::network::http

// ═══════════════════════════════════════════════════════════════════════════
//  控制台 UTF-8 配置工具
// ═══════════════════════════════════════════════════════════════════════════

namespace drx { namespace sdk { namespace network { namespace http {

/// 设置 Windows 控制台输出编码为 UTF-8（无 BOM）
/// 用法：在 main() 最开始调用此函数即可
/// 
/// 示例：
///   int main()
///   {
///       setupConsoleUtf8();  // 一行代码配置控制台
///       
///       DrxHttpClient client("https://api.example.com");
///       auto resp = client.get("/api/data");
///       std::cout << resp.bodyAsString() << std::endl;  // 中文正常显示
///       return 0;
///   }
inline void setupConsoleUtf8()
{
    // 设置控制台输入/输出 codepage 为 UTF-8
    SetConsoleCP(CP_UTF8);
    SetConsoleOutputCP(CP_UTF8);
    
    // 设置 stdout 为 UTF-8 文本模式（不加 BOM）
    _setmode(_fileno(stdout), _O_U8TEXT);
    
    // 可选：也配置 stderr
    _setmode(_fileno(stderr), _O_U8TEXT);
}

}}}} // namespace drx::sdk::network::http

#endif // DRX_HTTP_CLIENT_HPP
