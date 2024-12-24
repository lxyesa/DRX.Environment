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
     * @brief 订阅指定事件的回调函数。
     * @param eventName 要订阅的事件名称。
     * @param callback 事件发布时要调用的回调函数。
     */
    void Subscribe(const std::string& eventName, EventCallback callback);

    /**
     * @brief 发布具有给定参数的事件。
     * @param eventName 要发布的事件名称。
     * @param args 传递给事件回调函数的参数。
     */
    void Publish(const std::string& eventName, const NetworkEventArgs& args);

private:
    std::unordered_map<std::string, std::vector<EventCallback>> eventMap_;
    mutable std::shared_mutex eventMapMutex_;
};

