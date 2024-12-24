#pragma once

#include <WinSock2.h>
#include "NetworkPacket.h"
#include <string>
#include <memory>
#include <vector>
#include <unordered_map>

/**
 * @brief �����¼�������,���ڴ����¼������Ϣ
 */
class NetworkEventArgs {
public:
    /**
    * @brief ����һ���µ������¼�����ʵ��
    * @param socket �¼�������Socket����
    * @param message �¼���ص���Ϣ����
    * @param statecode �¼�״̬��: 0-�ɹ�, 1-ʧ��, 2-����, 3-δ֪
    * @param eCause �¼�����ԭ��(�������Ķ��ĵ��еġ������붨�塱��������Ķ��岻��һ��Ӳ��Ҫ�󣬵�Ϊ�������˿�����Ա���õĴ����뱣��һ�£�����ʹ��)
    * @param pJson �¼���ص����ݰ�JSON�ַ���(��ѡ)
    */
    NetworkEventArgs(SOCKET socket = INVALID_SOCKET, const std::string& message = "", DWORD64 statecode = 3, DWORD64 eCause = 0, const std::string& pJson = "");
	SOCKET GetSocket() const;
	const std::string& GetMessage() const;
	const NetworkPacket GetPacket() const;

private:
	SOCKET socket_;          ///< �¼�������Socket����
	std::string message_;    ///< �¼���ص���Ϣ����
	DWORD64 stateCode_;      ///< �¼�״̬��: 0-�ɹ�, 1-ʧ��, 2-����, 3-δ֪
	DWORD64 errorCauses_;    ///< �¼�����ԭ��
	std::string packetJson_; ///< �¼���ص����ݰ�JSON�ַ���
};
