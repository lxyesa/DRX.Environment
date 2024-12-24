#include "NetworkPacket.h"
#include <iostream>
#include <sstream>
#include <openssl/evp.h>
#include <openssl/hmac.h>
#include <openssl/sha.h>
#include "NetworkEventBus.h"

NetworkPacket::NetworkPacket() : header_(0), requestIdentifier_(0) {}

NetworkPacket::~NetworkPacket() = default;

NetworkPacket& NetworkPacket::SetHeader(uint32_t header) {
    header_ = header;
    return *this;
}

uint32_t NetworkPacket::GetHeader() const {
    return header_;
}

std::string NetworkPacket::GetBody() const {
    return body_.dump();
}

NetworkPacket& NetworkPacket::SetRequestIdentifier(uint32_t requestIdentifier) {
    requestIdentifier_ = requestIdentifier;
    return *this;
}

uint32_t NetworkPacket::GetRequestIdentifier() const {
    return requestIdentifier_;
}

std::string NetworkPacket::ToJson() const {
    nlohmann::json j;
    j["h"] = header_;
    j["b"] = body_;
    j["r_id"] = requestIdentifier_;
	j["hash"] = hash_;
    return j.dump();
}

NetworkPacket NetworkPacket::FromJson(const std::string& jsonStr) {
    NetworkPacket packet;
    nlohmann::json j = nlohmann::json::parse(jsonStr);
    packet.header_ = j.at("h").get<uint32_t>();
    packet.body_ = j.at("b");
    packet.requestIdentifier_ = j.at("r_id").get<uint32_t>();
    return packet;
}

std::vector<uint8_t> NetworkPacket::Serialize(const NetworkPacket& packet, const std::string& key) {
    NetworkPacket packetCopy = packet;
    std::string sha256Str = packetCopy.GenerateSHA256(key, packetCopy.ToJson());
	packetCopy.hash_ = sha256Str;
	std::string jsonStr = packetCopy.ToJson();

    const size_t length = jsonStr.length();
    std::vector<uint8_t> data(length);
    std::memcpy(data.data(), jsonStr.data(), length);

	return data;
}

NetworkPacket NetworkPacket::Deserialize(const std::vector<uint8_t>& data, const std::string& key) {
	if (data.empty()) {
		throw std::invalid_argument("Data cannot be empty");
	}

	std::string jsonStr(reinterpret_cast<const char*>(data.data()), data.size());

	// 校验数据包
	if (jsonStr.find("hash") == std::string::npos) {
		throw std::runtime_error("Hash not found in data");
	}

	std::string hash = nlohmann::json::parse(jsonStr).at("hash").get<std::string>();
	std::string originalJson = jsonStr;
	bool isValid = NetworkPacket().VerifySHA256(key, originalJson, hash);
	if (!isValid) {
		throw std::runtime_error("Data has been tampered");
	}

	NetworkPacket packet = FromJson(jsonStr);

	return packet;
}

nlohmann::json NetworkPacket::GetBody(const std::string& key) const {
    if (body_.contains(key)) {
        return body_.at(key);
    }
    else {
        throw std::out_of_range("Key not found in body");
    }
}

std::string NetworkPacket::GenerateSHA256(const std::string& key, const std::string& originalJson) const {
    // 解析 JSON 字符串
    nlohmann::json j = nlohmann::json::parse(originalJson);
    // 移除 hash 字段
    j.erase("hash");
    // 重新生成 JSON 字符串
    std::string jsonStrWithoutHash = j.dump();

    std::string combinedStr = jsonStrWithoutHash + key;

    unsigned char hash[EVP_MAX_MD_SIZE];
    unsigned int hashLen;

    EVP_MD_CTX* context = EVP_MD_CTX_new();
    if (context == nullptr) {
        throw std::runtime_error("Failed to create EVP_MD_CTX");
    }

    if (EVP_DigestInit_ex(context, EVP_sha256(), nullptr) != 1) {
        EVP_MD_CTX_free(context);
        throw std::runtime_error("Failed to initialize digest");
    }

    if (EVP_DigestUpdate(context, combinedStr.c_str(), combinedStr.size()) != 1) {
        EVP_MD_CTX_free(context);
        throw std::runtime_error("Failed to update digest");
    }

    if (EVP_DigestFinal_ex(context, hash, &hashLen) != 1) {
        EVP_MD_CTX_free(context);
        throw std::runtime_error("Failed to finalize digest");
    }

    EVP_MD_CTX_free(context);

    std::stringstream ss;
    for (unsigned int i = 0; i < hashLen; ++i) {
        ss << std::hex << std::setw(2) << std::setfill('0') << (int)hash[i];
    }
    return ss.str();
}

bool NetworkPacket::VerifySHA256(const std::string& key, const std::string& originalJson, const std::string& hash) const {
    std::string generatedHash = GenerateSHA256(key, originalJson);
    return generatedHash == hash;
}

// 显式实例化模板
template NetworkPacket& NetworkPacket::SetBody<int>(const std::string& key, const int& value);
template NetworkPacket& NetworkPacket::SetBody<const char*>(const std::string& key, const char* const& value);
