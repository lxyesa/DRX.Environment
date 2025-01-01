#pragma once

#include <string>
#include <WinSock2.h>
#include <WS2tcpip.h>
#include <stdexcept>
#include <thread>
#include <functional>
#include <unordered_map>
#include <vector>
#include <memory>
#include <sstream>
#include "NetworkEventBus.h"
#pragma comment(lib, "ws2_32.lib")

/**
 * @brief 网络核心客户端类,用于建立和管理TCP socket连接
 */
class NetworkSocketClient {
public:

    /**
     * @brief 构造一个新的网络客户端实例
     * @param serverIp 服务器IP地址
     * @param serverPort 服务器端口号
     * @throws std::runtime_error 如果Windows Socket初始化失败
     */
    NetworkSocketClient(const std::string& serverIp, int serverPort);

    /**
     * @brief 析构函数,清理socket资源并关闭Windows Socket
     */
    ~NetworkSocketClient();

    /**
     * @brief 连接到指定的服务器
     * @throws std::runtime_error 如果创建Socket失败或连接服务器失败
     */
    void Connect();

    /**
	* @brief 发送数据包到服务器
	* @param packet 要发送的数据包
	* @param key 数据包加密密钥
	* @throws std::runtime_error 如果发送数据失败
	* @note 该方法是线程安全的
    */
    void Send(const NetworkPacket packet, const std::string& key);

    /**
    * @brief 创建并连接UDP套接字
    * @throws std::runtime_error 如果创建Socket失败或连接服务器失败
    */
    void ConnectUDP();

    /**
     * @brief 添加事件监听器
     * @param eventName 事件名称
     * @param callback 事件回调函数
     */
    void AddEventListener(const std::string& eventName, NetworkEventBus::EventCallback callback);

private:
    std::string serverIp_;    ///< 服务器IP地址
    int serverPort_;          ///< 服务器端口号
    SOCKET socket_;           ///< Socket句柄
    bool isRunning_;          ///< 客户端主循环运行状态
    std::thread listenThread_; ///< 监听线程
    NetworkEventBus eventBus_; ///< 事件总线

    /**
     * @brief 启动客户端主循环
     * @note 在新线程中启动数据接收循环
     */
    void Start();

    /**
     * @brief 停止客户端主循环
     * @note 终止数据接收并等待线程结束
     */
    void Stop();

    /**
     * @brief 客户端主循环,监听服务器发送的数据
     * @note 该方法运行在单独的线程中
     */
    void ListenLoop();
};