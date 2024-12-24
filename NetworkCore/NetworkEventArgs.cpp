#include "NetworkEventArgs.h"

NetworkEventArgs::NetworkEventArgs(SOCKET socket,
	const std::string& message,
	DWORD64 statecode,
	DWORD64 eCause,
	const std::string& pPack)
	: socket_(socket), message_(message), stateCode_(statecode),
	errorCauses_(eCause), packetJson_(pPack){ }

SOCKET NetworkEventArgs::GetSocket() const {
	return socket_;
}

const std::string& NetworkEventArgs::GetMessage() const {
    return message_;
}

const NetworkPacket NetworkEventArgs::GetPacket() const {
	if (packetJson_.empty()) {
		return NetworkPacket();
	}
	return NetworkPacket::FromJson(packetJson_);
}