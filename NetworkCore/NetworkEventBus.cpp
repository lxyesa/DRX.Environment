#include "NetworkEventBus.h"

void NetworkEventBus::Subscribe(const std::string& eventName, EventCallback callback) {
    std::unique_lock lock(eventMapMutex_);
    eventMap_[eventName].emplace_back(std::move(callback));
}

void NetworkEventBus::Publish(const std::string& eventName, const NetworkEventArgs& args) {
    std::shared_lock lock(eventMapMutex_);
    auto it = eventMap_.find(eventName);
    if (it != eventMap_.end()) {
        for (const auto& callback : it->second) {
            callback(args);
        }
    }
}

