#include "../Library/Environments/SDK/Drx.Sdk.Network/Socket/DrxTcpClient.h"
#include <iostream>
#include <thread>
#include <chrono>

using namespace Drx::Sdk::Network::Socket;

// 简单的AES加密器实现示例（实际项目中应该使用更完整的实现）
class SimpleAesEncryptor : public IPacketEncryptor
{
public:
    std::vector<uint8_t> Encrypt(const std::vector<uint8_t>& data) override
    {
        // 简化的加密实现（实际应该使用真正的AES加密）
        std::vector<uint8_t> encrypted = data;
        for (auto& byte : encrypted)
        {
            byte ^= 0xAA; // 简单的XOR加密
        }
        return encrypted;
    }

    std::vector<uint8_t> Decrypt(const std::vector<uint8_t>& data) override
    {
        // 简化的解密实现（与加密相同，因为是XOR）
        return Encrypt(data);
    }
};

int main()
{
    try
    {
        // 创建 DrxTcpClient 实例
        DrxTcpClient client;
        
        // 启用日志
        client.SetLogging(true);
        
        // 设置加密器
        auto encryptor = std::make_shared<SimpleAesEncryptor>();
        client.SetEncryptor(encryptor);

        // 连接到服务器
        client.ConnectAsync("1.116.135.26", 8463).get();
        std::cout << "已连接到服务器 1.116.135.26:8463" << std::endl;

        // 设置数据接收事件处理器
        client.OnDataReceived = [](DrxTcpClient* c, const std::vector<uint8_t>& data) {
            try
            {
                std::string json(data.begin(), data.end());
                
                // 简单的JSON解析（实际项目中应该使用JSON库）
                size_t messagePos = json.find("\"message\":");
                if (messagePos != std::string::npos)
                {
                    size_t startQuote = json.find("\"", messagePos + 10);
                    size_t endQuote = json.find("\"", startQuote + 1);
                    if (startQuote != std::string::npos && endQuote != std::string::npos)
                    {
                        std::string msg = json.substr(startQuote + 1, endQuote - startQuote - 1);
                        std::cout << "[服务器消息] " << msg << std::endl;
                    }
                }
                else
                {
                    std::cout << "[服务器数据] " << json << std::endl;
                }
            }
            catch (const std::exception& ex)
            {
                std::cout << "解析数据失败: " << ex.what() << std::endl;
            }
        };

        // 发送心跳包（简化的JSON）
        std::string heartbeat = R"({"command":"heartbeat"})";
        client.SendMessageAsync(heartbeat).get();

        // 发送登录包
        std::string login = R"({"command":"login","args":{"username":"admin","password":"123456"}})";
        client.SendMessageAsync(login).get();

        // 开始接收数据
        auto receiveTask = client.StartReceivingAsync();

        // 测试映射功能
        client.PushMap("userInfo", "username", std::string("admin")).get();
        client.PushMap("userInfo", "userId", 12345).get();
        
        auto username = client.GetMap<std::string>("userInfo", "username").get();
        auto userId = client.GetMap<int>("userInfo", "userId").get();
        
        std::cout << "存储的用户名: " << username << std::endl;
        std::cout << "存储的用户ID: " << userId << std::endl;

        // 等待一段时间接收数据
        std::this_thread::sleep_for(std::chrono::seconds(10));

        // 断开连接
        client.Disconnect();
        std::cout << "已断开连接" << std::endl;
    }
    catch (const std::exception& ex)
    {
        std::cout << "发生异常: " << ex.what() << std::endl;
    }

    return 0;
}
