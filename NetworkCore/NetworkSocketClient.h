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
 * @brief ������Ŀͻ�����,���ڽ����͹���TCP socket����
 */
class NetworkSocketClient {
public:

    /**
     * @brief ����һ���µ�����ͻ���ʵ��
     * @param serverIp ������IP��ַ
     * @param serverPort �������˿ں�
     * @throws std::runtime_error ���Windows Socket��ʼ��ʧ��
     */
    NetworkSocketClient(const std::string& serverIp, int serverPort);

    /**
     * @brief ��������,����socket��Դ���ر�Windows Socket
     */
    ~NetworkSocketClient();

    /**
     * @brief ���ӵ�ָ���ķ�����
     * @throws std::runtime_error �������Socketʧ�ܻ����ӷ�����ʧ��
     */
    void Connect();

    /**
	* @brief �������ݰ���������
	* @param packet Ҫ���͵����ݰ�
	* @param key ���ݰ�������Կ
	* @throws std::runtime_error �����������ʧ��
	* @note �÷������̰߳�ȫ��
    */
    void Send(const NetworkPacket packet, const std::string& key);

    /**
    * @brief ����������UDP�׽���
    * @throws std::runtime_error �������Socketʧ�ܻ����ӷ�����ʧ��
    */
    void ConnectUDP();

    /**
     * @brief ����¼�������
     * @param eventName �¼�����
     * @param callback �¼��ص�����
     */
    void AddEventListener(const std::string& eventName, NetworkEventBus::EventCallback callback);

private:
    std::string serverIp_;    ///< ������IP��ַ
    int serverPort_;          ///< �������˿ں�
    SOCKET socket_;           ///< Socket���
    bool isRunning_;          ///< �ͻ�����ѭ������״̬
    std::thread listenThread_; ///< �����߳�
    NetworkEventBus eventBus_; ///< �¼�����

    /**
     * @brief �����ͻ�����ѭ��
     * @note �����߳����������ݽ���ѭ��
     */
    void Start();

    /**
     * @brief ֹͣ�ͻ�����ѭ��
     * @note ��ֹ���ݽ��ղ��ȴ��߳̽���
     */
    void Stop();

    /**
     * @brief �ͻ�����ѭ��,�������������͵�����
     * @note �÷��������ڵ������߳���
     */
    void ListenLoop();
};