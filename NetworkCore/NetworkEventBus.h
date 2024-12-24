#pragma once

#include <functional>
#include <unordered_map>
#include <vector>
#include <string>
#include <memory>
#include <shared_mutex>
#include "NetworkEventArgs.h"

class NetworkEventBus {
public:
    using EventCallback = std::function<void(const NetworkEventArgs&)>;

    /**
     * @brief ����ָ���¼��Ļص�������
     * @param eventName Ҫ���ĵ��¼����ơ�
     * @param callback �¼�����ʱҪ���õĻص�������
     */
    void Subscribe(const std::string& eventName, EventCallback callback);

    /**
     * @brief �������и����������¼���
     * @param eventName Ҫ�������¼����ơ�
     * @param args ���ݸ��¼��ص������Ĳ�����
     */
    void Publish(const std::string& eventName, const NetworkEventArgs& args);

private:
    std::unordered_map<std::string, std::vector<EventCallback>> eventMap_;
    mutable std::shared_mutex eventMapMutex_;
};

