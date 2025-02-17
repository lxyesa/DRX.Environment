#pragma once
#include <windows.h>
#include <winhttp.h>
#include <string>
#include <vector>
#include <map>

class HttpRequest
{
public:
    HttpRequest();
    ~HttpRequest();

    // Main HTTP methods
    bool Post(const std::wstring& url, const std::string& data, std::string& response);
    bool Get(const std::wstring& url, std::string& response);
    bool Put(const std::wstring& url, const std::string& data, std::string& response);
    bool Delete(const std::wstring& url, std::string& response);

    // Header management
    void AddHeader(const std::wstring& name, const std::wstring& value);
    void ClearHeaders();

    // Configuration
    void SetTimeout(DWORD timeoutMs);

private:
    bool SendRequest(const std::wstring& url, const std::wstring& method, 
                    const std::string& data, std::string& response);
    bool InitializeConnection(const std::wstring& url, HINTERNET& hConnect, 
                            HINTERNET& hRequest, const std::wstring& method);
    void CleanupHandles(HINTERNET hRequest, HINTERNET hConnect);

    HINTERNET hSession;
    std::map<std::wstring, std::wstring> headers;
    DWORD timeout;
};

