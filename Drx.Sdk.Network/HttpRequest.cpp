#include "pch.h"
// Remove these lines from the end of the file:
HttpRequest request;
std::string response;

// Set custom headers if needed
request.AddHeader(L"Content-Type", L"application/json");

// Set custom timeout if needed (in milliseconds)
request.SetTimeout(5000);

// Make requests
bool success = request.Get(L"https://api.example.com/data", response);
// or
success = request.Post(L"https://api.example.com/data", "{\"key\":\"value\"}", response);
#include "HttpRequest.h"
#include <vector>

HttpRequest::HttpRequest() : hSession(NULL), timeout(30000) // 30 seconds default timeout
{
    hSession = WinHttpOpen(L"DRX SDK/1.0", 
                          WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
                          WINHTTP_NO_PROXY_NAME, 
                          WINHTTP_NO_PROXY_BYPASS, 0);
}

HttpRequest::~HttpRequest()
{
    if (hSession)
    {
        WinHttpCloseHandle(hSession);
    }
}

bool HttpRequest::Post(const std::wstring& url, const std::string& data, std::string& response)
{
    return SendRequest(url, L"POST", data, response);
}

bool HttpRequest::Get(const std::wstring& url, std::string& response)
{
    return SendRequest(url, L"GET", "", response);
}

bool HttpRequest::Put(const std::wstring& url, const std::string& data, std::string& response)
{
    return SendRequest(url, L"PUT", data, response);
}

bool HttpRequest::Delete(const std::wstring& url, std::string& response)
{
    return SendRequest(url, L"DELETE", "", response);
}

void HttpRequest::AddHeader(const std::wstring& name, const std::wstring& value)
{
    headers[name] = value;
}

void HttpRequest::ClearHeaders()
{
    headers.clear();
}

void HttpRequest::SetTimeout(DWORD timeoutMs)
{
    timeout = timeoutMs;
}

bool HttpRequest::SendRequest(const std::wstring& url, const std::wstring& method, 
                            const std::string& data, std::string& response)
{
    HINTERNET hConnect = NULL;
    HINTERNET hRequest = NULL;
    bool success = false;

    if (!InitializeConnection(url, hConnect, hRequest, method))
    {
        return false;
    }

    // Set timeouts
    WinHttpSetTimeouts(hRequest, timeout, timeout, timeout, timeout);

    // Add headers
    for (const auto& header : headers)
    {
        std::wstring headerString = header.first + L": " + header.second;
        WinHttpAddRequestHeaders(hRequest, headerString.c_str(), -1L, 
                               WINHTTP_ADDREQ_FLAG_ADD | WINHTTP_ADDREQ_FLAG_REPLACE);
    }

    // Send the request
    if (!data.empty())
    {
        if (!WinHttpSendRequest(hRequest, WINHTTP_NO_ADDITIONAL_HEADERS, 0, 
            (LPVOID)data.c_str(), data.length(), data.length(), 0))
        {
            CleanupHandles(hRequest, hConnect);
            return false;
        }
    }
    else
    {
        if (!WinHttpSendRequest(hRequest, WINHTTP_NO_ADDITIONAL_HEADERS, 0, 
            WINHTTP_NO_REQUEST_DATA, 0, 0, 0))
        {
            CleanupHandles(hRequest, hConnect);
            return false;
        }
    }

    // Receive response
    if (WinHttpReceiveResponse(hRequest, NULL))
    {
        DWORD bytesAvailable = 0;
        std::vector<char> buffer;

        do
        {
            bytesAvailable = 0;
            if (!WinHttpQueryDataAvailable(hRequest, &bytesAvailable))
                break;

            if (bytesAvailable == 0)
                break;

            buffer.resize(bytesAvailable);
            DWORD bytesRead = 0;

            if (WinHttpReadData(hRequest, buffer.data(), bytesAvailable, &bytesRead))
            {
                response.append(buffer.data(), bytesRead);
                success = true;
            }
        } while (bytesAvailable > 0);
    }

    CleanupHandles(hRequest, hConnect);
    return success;
}

bool HttpRequest::InitializeConnection(const std::wstring& url, HINTERNET& hConnect, 
                                     HINTERNET& hRequest, const std::wstring& method)
{
    URL_COMPONENTS urlComp = { 0 };
    wchar_t hostName[256] = { 0 };
    wchar_t urlPath[1024] = { 0 };

    urlComp.dwStructSize = sizeof(urlComp);
    urlComp.lpszHostName = hostName;
    urlComp.dwHostNameLength = sizeof(hostName) / sizeof(hostName[0]);
    urlComp.lpszUrlPath = urlPath;
    urlComp.dwUrlPathLength = sizeof(urlPath) / sizeof(urlPath[0]);
    urlComp.dwSchemeLength = 1;

    if (!WinHttpCrackUrl(url.c_str(), url.length(), 0, &urlComp))
        return false;

    hConnect = WinHttpConnect(hSession, hostName, urlComp.nPort, 0);
    if (!hConnect)
        return false;

    DWORD flags = (urlComp.nScheme == INTERNET_SCHEME_HTTPS) ? WINHTTP_FLAG_SECURE : 0;
    hRequest = WinHttpOpenRequest(hConnect, method.c_str(), urlPath, 
                                NULL, WINHTTP_NO_REFERER, 
                                WINHTTP_DEFAULT_ACCEPT_TYPES, flags);
    
    return (hRequest != NULL);
}

void HttpRequest::CleanupHandles(HINTERNET hRequest, HINTERNET hConnect)
{
    if (hRequest)
        WinHttpCloseHandle(hRequest);
    if (hConnect)
        WinHttpCloseHandle(hConnect);
}
