#pragma once

#include <cstdint>
#include <string>
#include <array>
#include <vector>
#include <nlohmann/json.hpp>
#include <openssl/sha.h>

/**
 * @brief �������ݰ��ṹ
 */
class NetworkPacket {
public:
    /**
     * @brief ���캯������ʼ�����ݰ�
     */
    NetworkPacket();

    /**
     * @brief ����������������Դ
     */
    ~NetworkPacket();

    /**
     * @brief ���ð�ͷ
     * @param header ��ͷֵ
     * @return ���ص�ǰ���������
     */
    NetworkPacket& SetHeader(uint32_t header);

    /**
     * @brief ��ȡ��ͷ
     * @return ���ذ�ͷֵ
     */
    uint32_t GetHeader() const;

    /**
     * @brief ���ð���
     * @tparam T ������������
     * @param key �����
     * @param value ����ֵ
     * @return ���ص�ǰ���������
     */
    template<typename T>
    NetworkPacket& SetBody(const std::string& key, const T& value);

    /**
     * @brief ��ȡ����
     * @return ���ذ���� JSON �ַ���
     */
    std::string GetBody() const;

    /**
     * @brief ���������ʶ��
     * @param requestIdentifier �����ʶ��ֵ
     * @return ���ص�ǰ���������
     */
    NetworkPacket& SetRequestIdentifier(uint32_t requestIdentifier);

    /**
     * @brief ��ȡ�����ʶ��
     * @return ���������ʶ��ֵ
     */
    uint32_t GetRequestIdentifier() const;

    /**
     * @brief ���� SHA256 ��ϣֵ
     * @param key ��Կ
     * @param originalJson ԭʼ JSON �ַ���
     * @return �������ɵ� SHA256 ��ϣֵ
     */
    std::string GenerateSHA256(const std::string& key, const std::string& originalJson) const;

    /**
     * @brief ��֤ SHA256 ��ϣֵ
     * @param key ��Կ
     * @param originalJson ԭʼ JSON �ַ���
     * @param hash Ҫ��֤�Ĺ�ϣֵ
     * @return ������֤�����true ��ʾ��֤ͨ����false ��ʾ��֤ʧ��
     */
    bool VerifySHA256(const std::string& key, const std::string& originalJson, const std::string& hash) const;

    /**
     * @brief ���л����ݰ�Ϊ JSON �ַ���
     * @return ���� JSON �ַ���
     */
    std::string ToJson() const;

    /**
     * @brief �� JSON �ַ��������л����ݰ�
     * @param jsonStr JSON �ַ���
     * @return ���ط����л�������ݰ�����
     */
    static NetworkPacket FromJson(const std::string& jsonStr);

    /**
     * @brief ���л����ݰ�Ϊ�ֽ�����
     * @param packet ���ݰ�����
     * @param key ��Կ
     * @return �������л�����ֽ�����
     */
    static std::vector<uint8_t> Serialize(const NetworkPacket& packet, const std::string& key);

    /**
     * @brief �����л��ֽ�����Ϊ���ݰ�
     * @param data �ֽ�����
     * @param key ��Կ
     * @return ���ط����л�������ݰ�����
     */
    static NetworkPacket Deserialize(const std::vector<uint8_t>& data, const std::string& key);

    /**
     * @brief ��ȡ�����е�ָ������ֵ
     * @param key �����
     * @return ���ؼ���Ӧ�� JSON ֵ
     */
    nlohmann::json GetBody(const std::string& key) const;

private:
    uint32_t header_;               ///< ��ͷ
    nlohmann::json body_;           ///< ����
    uint32_t requestIdentifier_;    ///< �����ʶ��
    std::string hash_;              ///< SHA256 ��ϣֵ
};

/**
* @brief ���ð���
* @tparam T ������������
* @param key �����
* @param value ����ֵ
* @return ���ص�ǰ���������
*/
template<typename T>
NetworkPacket& NetworkPacket::SetBody(const std::string& key, const T& value) {
    body_[key] = value;
    return *this;
}
#pragma once
