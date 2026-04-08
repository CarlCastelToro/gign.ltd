// Base62 标准字符集
const CHAR_SET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
const BASE = 62;
const CHAR_TO_VALUE = new Map();
for (let i = 0; i < CHAR_SET.length; i++) CHAR_TO_VALUE.set(CHAR_SET[i], i);

// 编码器
class Safe62Encoder {
  constructor() {
    this.blocks = [];
    this.currentBlock = 0n;
    this.blockSize = 0;
    this.totalLength = 0;
  }

  addData(data) {
    for (const b of data) {
      this.currentBlock = (this.currentBlock << 8n) | BigInt(b);
      this.blockSize++;
      this.totalLength++;
      if (this.blockSize >= 256) this._flush();
    }
  }

  _flush() {
    if (!this.blockSize) return;
    let num = this.currentBlock, enc = "";
    if (num === 0n) enc = CHAR_SET[0];
    else while (num > 0n) {
      const r = Number(num % BigInt(BASE));
      enc = CHAR_SET[r] + enc;
      num /= BigInt(BASE);
    }
    this.blocks.push({ s: this.blockSize, d: enc });
    this.currentBlock = 0n;
    this.blockSize = 0;
  }

  _len(n) {
    const buf = [];
    if (n === 0) buf.push(0);
    else while (n > 0) {
      let b = n & 0x7F;
      n >>= 7;
      if (n > 0) b |= 0x80;
      buf.push(b);
    }
    return buf.map(b => CHAR_SET[Math.floor(b / BASE)] + CHAR_SET[b % BASE]).join("");
  }

  _hdr(s) {
    return CHAR_SET[Math.floor(s / BASE)] + CHAR_SET[s % BASE];
  }

  end() {
    this._flush();
    let out = this._len(this.totalLength);
    for (const b of this.blocks) out += this._hdr(b.s) + b.d;
    return out;
  }
}

// 解码器
class Safe62Decoder {
  constructor(s) { this.s = s; this.p = 0; }
  decode() {
    const total = this._dlen();
    const out = [];
    let dec = 0;
    while (dec < total && this.p < this.s.length) {
      const size = this._dhdr();
      const data = this._dblk();
      out.push(...this._ddata(data, size));
      dec += size;
    }
    return new Uint8Array(out.slice(0, total));
  }

  _dlen() {
    let r = 0, sh = 0;
    while (this.p < this.s.length) {
      const h = CHAR_TO_VALUE.get(this.s[this.p]);
      const l = CHAR_TO_VALUE.get(this.s[this.p+1]);
      if (h === undefined || l === undefined) break;
      const b = h * BASE + l;
      this.p += 2;
      r |= (b & 0x7F) << sh;
      sh +=7;
      if (!(b & 0x80)) break;
    }
    return r;
  }

  _dhdr() {
    const h = CHAR_TO_VALUE.get(this.s[this.p]);
    const l = CHAR_TO_VALUE.get(this.s[this.p+1]);
    this.p +=2;
    return h * BASE + l;
  }

  _dblk() {
    const st = this.p;
    while (this.p < this.s.length && CHAR_TO_VALUE.has(this.s[this.p])) this.p++;
    return this.s.slice(st, this.p);
  }

  _ddata(enc, exp) {
    let n = 0n;
    for (const c of enc) n = n * BigInt(BASE) + BigInt(CHAR_TO_VALUE.get(c));
    const b = [];
    while (n > 0n) {
      b.unshift(Number(n & 0xFFn));
      n >>= 8n;
    }
    while (b.length < exp) b.unshift(0);
    return b;
  }
}

// 快捷方法
function encode(input) {
  const e = new Safe62Encoder();
  e.addData(typeof input === "string" ? new TextEncoder().encode(input) : input);
  return e.end();
}

function decode(str) {
  return new Safe62Decoder(str).decode();
}

// Worker 入口（极简调用）
export default {
  async fetch(request) {
    const params = new URL(request.url).searchParams;
    const en1 = params.get("b62encode");
    const de1 = params.get("b62decode");
    const en = params.get("e");
    const de = params.get("d");

    const corsHeaders = {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET,HEAD,OPTIONS',
      'Access-Control-Allow-Headers': '*'
    };
    try {
      if (en) return new Response(encode(en), { headers: corsHeaders });
      if (de) return new Response(new TextDecoder().decode(decode(de)), { headers: corsHeaders });
      return new Response("", { headers: corsHeaders });
    } catch {
      return new Response("", { headers: corsHeaders });
    }
  }
};