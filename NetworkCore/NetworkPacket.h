#pragma once

#include <cstdint>
#include <string>
#include <array>
#include <vector>
#include <nlohmann/json.hpp>
#include <openssl/sha.h>

/**
 * @brief 网络数据包结构
 */
class NetworkPacket {
public:
    /**
     * @brief 构造函数，初始化数据包
     */
    NetworkPacket();

    /**
     * @brief 析构函数，清理资源
     */
    ~NetworkPacket();

    /**
     * @brief 设置包头
     * @param header 包头值
     * @return 返回当前对象的引用
     */
    NetworkPacket& SetHeader(uint32_t header);

    /**
     * @brief 获取包头
     * @return 返回包头值
     */
    uint32_t GetHeader() const;

    /**
     * @brief 设置包体
     * @tparam T 包体数据类型
     * @param key 包体键
     * @param value 包体值
     * @return 返回当前对象的引用
     */
    template<typename T>
    NetworkPacket& SetBody(const std::string& key, const T& value);

    /**
     * @brief 获取包体
     * @return 返回包体的 JSON 字符串
     */
    std::string GetBody() const;

    /**
     * @brief 设置请求标识符
     * @param requestIdentifier 请求标识符值
     * @return 返回当前对象的引用
     */
    NetworkPacket& SetRequestIdentifier(uint32_t requestIdentifier);

    /**
     * @brief 获取请求标识符
     * @return 返回请求标识符值
     */
    uint32_t GetRequestIdentifier() const;

    /**
     * @brief 生成 SHA256 哈希值
     * @param key 密钥
     * @param originalJson 原始 JSON 字符串
     * @return 返回生成的 SHA256 哈希值
     */
    std::string GenerateSHA256(const std::string& key, const std::string& originalJson) const;

    /**
     * @brief 验证 SHA256 哈希值
     * @param key 密钥
     * @param originalJson 原始 JSON 字符串
     * @param hash 要验证的哈希值
     * @return 返回验证结果，true 表示验证通过，false 表示验证失败
     */
    bool VerifySHA256(const std::string& key, const std::string& originalJson, const std::string& hash) const;

    /**
     * @brief 序列化数据包为 JSON 字符串
     * @return 返回 JSON 字符串
     */
    std::string ToJson() const;

    /**
     * @brief 从 JSON 字符串反序列化数据包
     * @param jsonStr JSON 字符串
     * @return 返回反序列化后的数据包对象
     */
    static NetworkPacket FromJson(const std::string& jsonStr);

    /**
     * @brief 序列化数据包为字节数组
     * @param packet 数据包对象
     * @param key 密钥
     * @return 返回序列化后的字节数组
     */
    static std::vector<uint8_t> Serialize(const NetworkPacket& packet, const std::string& key);

    /**
     * @brief 反序列化字节数组为数据包
     * @param data 字节数组
     * @param key 密钥
     * @return 返回反序列化后的数据包对象
     */
    static NetworkPacket Deserialize(const std::vector<uint8_t>& data, const std::string& key);

    /**
     * @brief 获取包体中的指定键的值
     * @param key 包体键
     * @return 返回键对应的 JSON 值
     */
    nlohmann::json GetBody(const std::string& key) const;

private:
    uint32_t header_;               ///< 包头
    nlohmann::json body_;           ///< 包体
    uint32_t requestIdentifier_;    ///< 请求标识符
    std::string hash_;              ///< SHA256 哈希值
};

/**
* @brief 设置包体
* @tparam T 包体数据类型
* @param key 包体键
* @param value 包体值
* @return 返回当前对象的引用
*/
template<typename T>
NetworkPacket& NetworkPacket::SetBody(const std::string& key, const T& value) {
    body_[key] = value;
    return *this;
}
#pragma once
