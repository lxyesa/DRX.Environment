#pragma once

#include <WinSock2.h>
#include "NetworkPacket.h"
#include <string>
#include <memory>
#include <vector>
#include <unordered_map>

/**
 * @brief 网络事件参数类,用于传递事件相关信息
 */
class NetworkEventArgs {
public:
    /**
    * @brief 构造一个新的网络事件参数实例
    * @param socket 事件发生的Socket对象
    * @param message 事件相关的消息描述
    * @param statecode 事件状态码: 0-成功, 1-失败, 2-警告, 3-未知
    * @param eCause 事件错误原因(具体请阅读文档中的“错误码定义”，错误码的定义不是一个硬性要求，但为了与服务端开发人员设置的错误码保持一致，建议使用)
    * @param pJson 事件相关的数据包JSON字符串(可选)
    */
    NetworkEventArgs(SOCKET socket = INVALID_SOCKET, const std::string& message = "", DWORD64 statecode = 3, DWORD64 eCause = 0, const std::string& pJson = "");
	SOCKET GetSocket() const;
	const std::string& GetMessage() const;
	const NetworkPacket GetPacket() const;

private:
	SOCKET socket_;          ///< 事件发生的Socket对象
	std::string message_;    ///< 事件相关的消息描述
	DWORD64 stateCode_;      ///< 事件状态码: 0-成功, 1-失败, 2-警告, 3-未知
	DWORD64 errorCauses_;    ///< 事件错误原因
	std::string packetJson_; ///< 事件相关的数据包JSON字符串
};
