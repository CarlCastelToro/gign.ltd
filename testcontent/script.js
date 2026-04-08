const CHAR_SET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
const BASE = 62;
const CHAR_TO_VALUE = new Map();
for (let i = 0; i < CHAR_SET.length; i++) {
    CHAR_TO_VALUE.set(CHAR_SET[i], i);
}
class Safe62Encoder {
    constructor() {
        this.blocks = [];
        this.currentBlock = 0n;
        this.blockSize = 0;
        this.totalLength = 0;
    }
    /**
     * @param {Uint8Array} data 
     */
    addData(data) {
        for (const byte of data) {
            this.currentBlock = (this.currentBlock << 8n) | BigInt(byte);
            this.blockSize++;
            this.totalLength++;
            if (this.blockSize >= 256) {
                this._flushBlock();
            }
        }
    }
    /**
     * @private
     */
    _flushBlock() {
        if (this.blockSize === 0) return;
        let num = this.currentBlock;
        let encoded = '';        
        if (num === 0n) {
            encoded = CHAR_SET[0];
        } else {
            while (num > 0n) {
                const rem = Number(num % BigInt(BASE));
                encoded = CHAR_SET[rem] + encoded;
                num = num / BigInt(BASE);
            }
        }
        this.blocks.push({
            bytes: this.blockSize,
            encoded: encoded
        });        
        this.currentBlock = 0n;
        this.blockSize = 0;
    }
    /**
     * @returns {string}
     */
    finalize() {
        this._flushBlock();
        
        // 编码总长度（使用变长编码，支持任意大小）
        const lengthHeader = this._encodeLength(this.totalLength);
        
        // 拼接所有块
        let result = lengthHeader;
        for (const block of this.blocks) {
            // 为每个块添加块大小信息（用于解码）
            result += this._encodeBlockHeader(block.bytes) + block.encoded;
        }
        
        return result;
    }    
    /**
     * @private
     */
    _encodeLength(len) {
        const bytes = [];
        while (len > 0) {
            let byte = len & 0x7F;
            len >>= 7;
            if (len > 0) byte |= 0x80;
            bytes.push(byte);
        }
        if (bytes.length === 0) bytes.push(0);
        let result = '';
        for (const byte of bytes) {
            result += CHAR_SET[byte % BASE] + CHAR_SET[Math.floor(byte / BASE)];
        }
        return result;
    }    
    /**
     * @private
     */
    _encodeBlockHeader(bytes) {
        // 使用两个 Base62 字符表示 0-3843 字节
        return CHAR_SET[Math.floor(bytes / BASE)] + CHAR_SET[bytes % BASE];
    }
}
class Safe62Decoder {
    /**
     * @param {string} encoded 
     */
    constructor(encoded) {
        this.encoded = encoded;
        this.pos = 0;
        this.result = [];
    }    
    /**
     * @returns {Uint8Array}
     */
    decode() {
        const totalLength = this._decodeLength();        
        const output = [];
        let bytesDecoded = 0;
        while (bytesDecoded < totalLength && this.pos < this.encoded.length) {
            if (this.pos + 2 > this.encoded.length) break;            
            const blockBytes = this._decodeBlockHeader();
            const blockEncoded = this._readUntilNextBlock();            
            const blockData = this._decodeBlock(blockEncoded, blockBytes);
            output.push(...blockData);
            bytesDecoded += blockBytes;
        }
        return new Uint8Array(output.slice(0, totalLength));
    }    
    /**
     * @private
     */
    _decodeLength() {
        let result = 0;
        let shift = 0;        
        while (this.pos < this.encoded.length) {
            if (this.pos + 2 > this.encoded.length) break;            
            const high = CHAR_TO_VALUE.get(this.encoded[this.pos]);
            const low = CHAR_TO_VALUE.get(this.encoded[this.pos + 1]);
            if (high === undefined || low === undefined) {
                throw new Error('Invalid length encoding');
            }            
            const byte = high * BASE + low;
            this.pos += 2;            
            result |= (byte & 0x7F) << shift;
            shift += 7;            
            if ((byte & 0x80) === 0) break;
        }        
        return result;
    }    
    /**
     * @private
     */
    _decodeBlockHeader() {
        const high = CHAR_TO_VALUE.get(this.encoded[this.pos]);
        const low = CHAR_TO_VALUE.get(this.encoded[this.pos + 1]);
        if (high === undefined || low === undefined) {
            throw new Error('Invalid block header');
        }
        this.pos += 2;
        return high * BASE + low;
    }
    /**
     * @private
     */
    _readUntilNextBlock() {
        if (this.pos + 2 >= this.encoded.length) {
            const result = this.encoded.slice(this.pos);
            this.pos = this.encoded.length;
            return result;
        }
        let endPos = this.pos;
        while (endPos + 2 < this.encoded.length) {
            const h1 = CHAR_TO_VALUE.get(this.encoded[endPos]);
            const h2 = CHAR_TO_VALUE.get(this.encoded[endPos + 1]);
            if (h1 !== undefined && h2 !== undefined) {
                const possibleBytes = h1 * BASE + h2;
                if (possibleBytes >= 0 && possibleBytes <= 3843) {
                    break;
                }
            }
            endPos++;
        }        
        const result = this.encoded.slice(this.pos, endPos);
        this.pos = endPos;
        return result;
    }    
    /**
     * @private
     */
    _decodeBlock(encoded, expectedBytes) {
        let num = 0n;
        for (const c of encoded) {
            const idx = CHAR_TO_VALUE.get(c);
            if (idx === undefined) {
                throw new Error(`Invalid character: ${c}`);
            }
            num = num * BigInt(BASE) + BigInt(idx);
        }
        const bytes = [];
        let n = num;
        while (n > 0n) {
            bytes.unshift(Number(n & 0xFFn));
            n = n >> 8n;
        }
        while (bytes.length < expectedBytes) bytes.unshift(0);        
        return bytes;
    }
}
/**
 * @param {string | Uint8Array} input 
 * @returns {string}
 */
function safe62EncodeStream(input) {
    const encoder = new Safe62Encoder();    
    if (typeof input === 'string') {
        const encoder_stream = new TextEncoder();
        const chunkSize = 1024;         
        for (let i = 0; i < input.length; i += chunkSize) {
            const chunk = input.slice(i, i + chunkSize);
            const encoded = encoder_stream.encode(chunk);
            encoder.addData(encoded);
        }
    } else if (input instanceof Uint8Array) {
        const chunkSize = 1024;
        for (let i = 0; i < input.length; i += chunkSize) {
            const chunk = input.slice(i, i + chunkSize);
            encoder.addData(chunk);
        }
    } else {
        throw new Error('Only string or Uint8Array');
    }    
    return encoder.finalize();
}
/**
 * @param {string} encoded 
 * @returns {Uint8Array}
 */
function safe62DecodeStream(encoded) {
    if (typeof encoded !== 'string' || encoded.length === 0) {
        return new Uint8Array();
    }    
    const decoder = new Safe62Decoder(encoded);
    return decoder.decode();
}
/**
 * @param {string} encoded 
 * @returns {string}
 */
function safe62DecodeToStringStream(encoded) {
    return new TextDecoder().decode(safe62DecodeStream(encoded));
}
/**
 * @param {string | Uint8Array} input 
 * @returns {string}
 */
function safe62EncodeFast(input) {
    let bytes;
    if (typeof input === 'string') {
        bytes = new TextEncoder().encode(input);
    } else if (input instanceof Uint8Array) {
        bytes = input;
    } else {
        throw new Error('Only string or Uint8Array');
    }
    const CHUNK_SIZE = 256;
    const chunks = [];    
    for (let i = 0; i < bytes.length; i += CHUNK_SIZE) {
        const chunk = bytes.slice(i, Math.min(i + CHUNK_SIZE, bytes.length));
        let num = 0n;
        for (const b of chunk) {
            num = (num << 8n) | BigInt(b);
        }
        let encoded = '';
        if (num === 0n) {
            encoded = CHAR_SET[0];
        } else {
            let temp = num;
            while (temp > 0n) {
                const rem = Number(temp % BigInt(BASE));
                encoded = CHAR_SET[rem] + encoded;
                temp = temp / BigInt(BASE);
            }
        }        
        chunks.push({
            len: chunk.length,
            encoded: encoded
        });
    }
    const totalLen = bytes.length;
    let lengthEncoded = '';
    let tempLen = totalLen;
    while (tempLen > 0) {
        const rem = tempLen % BASE;
        lengthEncoded = CHAR_SET[rem] + lengthEncoded;
        tempLen = Math.floor(tempLen / BASE);
    }
    if (lengthEncoded === '') lengthEncoded = CHAR_SET[0];
    const result = 'L' + lengthEncoded + ':' + 
                   chunks.map(c => String.fromCharCode(c.len) + c.encoded).join('');
    
    return result;
}
/**
 * @param {string} encoded 
 * @returns {Uint8Array}
 */
function safe62DecodeFast(encoded) {
    if (!encoded.startsWith('L')) {
        return safe62DecodeLegacy(encoded);
    }
    const colonIdx = encoded.indexOf(':');
    if (colonIdx === -1) return new Uint8Array();    
    const lengthStr = encoded.slice(1, colonIdx);
    let totalLen = 0;
    for (const c of lengthStr) {
        const val = CHAR_TO_VALUE.get(c);
        if (val === undefined) return new Uint8Array();
        totalLen = totalLen * BASE + val;
    }    
    const body = encoded.slice(colonIdx + 1);
    const result = [];
    let pos = 0;
    let decodedCount = 0;    
    while (pos < body.length && decodedCount < totalLen) {
        if (pos + 1 > body.length) break;
        const chunkLen = body.charCodeAt(pos);
        pos++;
        let chunkEncoded = '';
        let endPos = pos;
        while (endPos < body.length) {
            if (endPos > pos && body.charCodeAt(endPos) < 256) {
                break;
            }
            endPos++;
        }        
        chunkEncoded = body.slice(pos, endPos);
        pos = endPos;
        let num = 0n;
        for (const c of chunkEncoded) {
            const idx = CHAR_TO_VALUE.get(c);
            if (idx === undefined) {
                throw new Error(`Invalid character: ${c}`);
            }
            num = num * BigInt(BASE) + BigInt(idx);
        }
        const bytes = [];
        let n = num;
        while (n > 0n) {
            bytes.unshift(Number(n & 0xFFn));
            n = n >> 8n;
        }
        while (bytes.length < chunkLen) bytes.unshift(0);        
        result.push(...bytes);
        decodedCount += chunkLen;
    }    
    return new Uint8Array(result.slice(0, totalLen));
}
function safe62DecodeLegacy(encoded) {
    if (typeof encoded !== 'string' || encoded.length < 2) {
        return new Uint8Array();
    }
    const len1 = CHAR_TO_VALUE.get(encoded[0]);
    const len2 = CHAR_TO_VALUE.get(encoded[1]);
    if (len1 === undefined || len2 === undefined) {
        return new Uint8Array();
    }
    const originalLength = len1 * BASE + len2;
    const body = encoded.slice(2);
    let num = 0n;
    for (const c of body) {
        const idx = CHAR_TO_VALUE.get(c);
        if (idx === undefined) throw new Error('Include invalid character');
        num = num * BigInt(BASE) + BigInt(idx);
    }
    const bytes = [];
    let n = num;
    while (n > 0n) {
        bytes.unshift(Number(n & 0xFFn));
        n = n >> 8n;
    }
    while (bytes.length < originalLength) bytes.unshift(0);
    return new Uint8Array(bytes.slice(-originalLength));
}
module.exports = {
    safe62Encode: safe62EncodeFast,
    safe62Decode: safe62DecodeFast,
    safe62DecodeToString: (encoded) => new TextDecoder().decode(safe62DecodeFast(encoded)),
    safe62EncodeStream,
    safe62DecodeStream,
    safe62DecodeToStringStream,
    safe62EncodeLegacy: safe62EncodeStream,
    safe62DecodeLegacy
};