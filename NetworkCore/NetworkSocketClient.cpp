#include "NetworkSocketClient.h"
#include "NetworkEventArgs.h"

NetworkSocketClient::NetworkSocketClient(const std::string& serverIp, int serverPort)
    : serverIp_(serverIp)
    , serverPort_(serverPort)
    , socket_(INVALID_SOCKET)
    , isRunning_(false) {
    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        throw std::runtime_error("WSAStartup failed");
    }
}

NetworkSocketClient::~NetworkSocketClient() {
    Stop();
    if (socket_ != INVALID_SOCKET) {
        closesocket(socket_);
        socket_ = INVALID_SOCKET;
    }
    WSACleanup();
}

void NetworkSocketClient::Connect() {
    try {
        socket_ = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        if (socket_ == INVALID_SOCKET) {
            throw std::runtime_error("Failed to create socket");
        }

        sockaddr_in serverAddr{};
        serverAddr.sin_family = AF_INET;
        serverAddr.sin_port = htons(serverPort_);
        inet_pton(AF_INET, serverIp_.c_str(), &serverAddr.sin_addr);

        if (connect(socket_, (sockaddr*)&serverAddr, sizeof(serverAddr)) == SOCKET_ERROR) {
            closesocket(socket_);
            socket_ = INVALID_SOCKET;
            throw std::runtime_error("Failed to connect to server");
        }

        // 发布连接成功事件
        NetworkEventArgs args(socket_, "Connected to server", 0, 0);
        eventBus_.Publish("Connected", args);

        Start();
    }
    catch (const std::exception& e) {
        // 发布连接失败事件
        NetworkEventArgs args(socket_, e.what(), 1, 0);
        eventBus_.Publish("ConnectionFailed", args);
    }
}

void NetworkSocketClient::Send(const NetworkPacket packet, const std::string& key) {
    try {
        // 将 NetworkPacket 序列化为字节数组
        std::vector<uint8_t> serializedData = NetworkPacket::Serialize(packet, key);

        // 发送数据
        int bytesSent = send(socket_, reinterpret_cast<const char*>(serializedData.data()), serializedData.size(), 0);
        if (bytesSent == SOCKET_ERROR) {
            throw std::runtime_error("Failed to send data");
        }

        // 发布数据发送成功事件
        NetworkEventArgs args(socket_, "", 0, 0, packet.ToJson());
        eventBus_.Publish("DataSent", args);
    }
    catch (const std::exception& e) {
        // 发布数据发送失败事件
        NetworkEventArgs args(socket_, e.what(), 1, 0);
        eventBus_.Publish("DataSendFailed", args);
    }
}


void NetworkSocketClient::ConnectUDP() {
    // 创建UDP socket
    socket_ = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (socket_ == INVALID_SOCKET) {
        throw std::runtime_error("Failed to create socket");
    }

    // 设置服务器地址
    sockaddr_in serverAddr{};
    serverAddr.sin_family = AF_INET;
    serverAddr.sin_port = htons(serverPort_);
    inet_pton(AF_INET, serverIp_.c_str(), &serverAddr.sin_addr);

    // 连接到服务器
    if (connect(socket_, (sockaddr*)&serverAddr, sizeof(serverAddr)) == SOCKET_ERROR) {
        closesocket(socket_);
        socket_ = INVALID_SOCKET;
        throw std::runtime_error("Failed to connect to server");
    }

    // 发布连接成功事件
    NetworkEventArgs args(socket_, "Connected to server", 0, 0);
    eventBus_.Publish("Connected", args);

    // 触发连接事件并启动监听
    Start();
}

void NetworkSocketClient::AddEventListener(const std::string& eventName, NetworkEventBus::EventCallback callback) {
    eventBus_.Subscribe(eventName, std::move(callback));
}

void NetworkSocketClient::Start() {
    isRunning_ = true;
    listenThread_ = std::thread(&NetworkSocketClient::ListenLoop, this);
}

void NetworkSocketClient::Stop() {
    isRunning_ = false;
    if (listenThread_.joinable()) {
        listenThread_.join();
    }
}

void NetworkSocketClient::ListenLoop() {
    constexpr size_t BUFFER_SIZE = 8192;  // 使用更大的缓冲区
    std::unique_ptr<char[]> buffer(new char[BUFFER_SIZE]);
    int retryCount = 0;
    constexpr int MAX_RETRIES = 3;

    while (isRunning_) {
        int bytesReceived = recv(socket_, buffer.get(), BUFFER_SIZE, 0);

        if (bytesReceived > 0) {
            // 成功接收数据
            std::string message(buffer.get(), bytesReceived);
            NetworkEventArgs args(socket_, message, 0, 0, message);
            eventBus_.Publish("DataReceived", args);
            retryCount = 0;  // 重置重试计数
        }
        else if (bytesReceived == 0) {
            // 对方正常关闭连接
            NetworkEventArgs args(socket_, "Connection closed by peer", 0, 0);
            eventBus_.Publish("ConnectionClosed", args);
            Stop();
            break;
        }
        else {
            // 发生错误
            int error = WSAGetLastError();
            std::string errorMsg = "Network error: " + std::to_string(error);
            NetworkEventArgs args(socket_, errorMsg, 1, error);

            if (error == WSAEWOULDBLOCK || error == WSAETIMEDOUT) {
                if (++retryCount < MAX_RETRIES) {
                    std::this_thread::sleep_for(std::chrono::milliseconds(100));
                    continue;
                }
            }

            eventBus_.Publish("NetworkError", args);
            Stop();
            break;
        }
    }
}