# CryptoBridge
> 加密与哈希脚本桥接层

## Classes
| 类名 | 简介 |
|------|------|
| `CryptoBridge` | 静态类，提供 AES 加解密、哈希、Base64、UUID 等加密工具 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `aesEncrypt(plainText)` | `plainText:string` | `string` | 默认密钥 AES 加密(Base64) |
| `aesDecrypt(base64Cipher)` | `base64Cipher:string` | `string` | 默认密钥 AES 解密 |
| `aesEncryptWithKey(text, keyB64, ivB64)` | `plainText:string, keyBase64:string, ivBase64:string` | `string` | 自定义密钥 AES 加密 |
| `aesDecryptWithKey(cipher, keyB64, ivB64)` | `base64Cipher:string, keyBase64:string, ivBase64:string` | `string` | 自定义密钥 AES 解密 |
| `generateAesKey()` | — | `{key:string, iv:string}` | 生成 AES-256 密钥对(Base64) |
| `sha256(input)` | `input:string` | `string` | SHA-256 哈希(hex) |
| `md5(input)` | `input:string` | `string` | MD5 哈希(hex) |
| `hmacSha256(input, keyB64)` | `input:string, keyBase64:string` | `string` | HMAC-SHA256 签名(Base64) |
| `base64Encode(input)` | `input:string` | `string` | Base64 编码 |
| `base64Decode(base64)` | `base64:string` | `string` | Base64 解码 |
| `randomBytes(length?)` | `length:int` | `string` | 随机字节(Base64) |
| `uuid()` | — | `string` | 生成 UUID v4 |

## Usage
```typescript
const encrypted = Crypto.aesEncrypt("hello");
const decrypted = Crypto.aesDecrypt(encrypted);
const hash = Crypto.sha256("password");
const id = Crypto.uuid();
const keys = Crypto.generateAesKey();
```
