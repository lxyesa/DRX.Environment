# PacketAPI 文档

## PacketTypes 类

`PacketTypes` 类定义了网络数据包的各种类型常量，用于在数据通信中标识不同类型的包。

### 常量

- **Command**
  - **类型**: `string`
  - **值**: `"command"`
  - **描述**: 表示一个命令类型的数据包。

- **CommandResponse**
  - **类型**: `string`
  - **值**: `"command_response"`
  - **描述**: 表示一个命令响应类型的数据包。

- **Response**
  - **类型**: `string`
  - **值**: `"response"`
  - **描述**: 表示一个通用响应类型的数据包。

- **Message**
  - **类型**: `string`
  - **值**: `"message"`
  - **描述**: 表示一个消息类型的数据包。

## PacketBodyKey 类

`PacketBodyKey` 类定义了数据包主体中常用的键值，便于在处理数据包时统一使用这些键。

### 静态只读字段

#### 命令类型的数据包键值

- **Command**
  - **类型**: `string`
  - **值**: `"command"`
  - **描述**: 用于标识命令的键。

- **CommandArgs**
  - **类型**: `string`
  - **值**: `"command_args"`
  - **描述**: 用于传递命令参数的键。

- **CommandResponse**
  - **类型**: `string`
  - **值**: `"command_result"`
  - **描述**: 用于传递命令执行结果的键。

#### 其他类型的数据包键值

- **Message**
  - **类型**: `string`
  - **值**: `"message"`
  - **描述**: 用于传递消息内容的键。

- **Response**
  - **类型**: `string`
  - **值**: `"response"`
  - **描述**: 用于传递通用响应内容的键。

