'use strict';
/**
 * 上体宿舍电量 · 腾讯云函数 SCF 版 v2.0
 *
 * 一个函数干两件事（靠事件类型自动判断）：
 *   1) 定时触发器（event.Type === 'Timer'）→ 查电量 + PushPlus 推送   [10:00 / 20:00]
 *   2) 公众号消息回调（HTTP 事件，来自「函数URL」）→ 被动回复电费         [你发「查电费」]
 *
 * 运行环境：Node.js 18.15 / 16.13，零 npm 依赖
 * 入口：index.main_handler
 *
 * 环境变量：
 *   PUSHPLUS_TOKEN      定时推送用（PushPlus token）
 *   PUSHPLUS_TOPIC      选填，PushPlus 群组编码（室友一起收）
 *   WECHAT_TOKEN        订阅号「服务器配置」里填的 Token（问答用，必须与网页上一致）
 *   ROOM_AREA_ID / ROOM_ARCHITECTURE_ID / ROOM_FLOOR_ID / ROOM_ROOM_ID
 *   ROOM_DISPLAY_NAME / LOW_THRESHOLD
 *
 * 定时触发器 cron（北京时间）：0 0 10,20 * * * *   → 10:00 和 20:00
 */

const https = require('https');
const dns = require('dns');
const crypto = require('crypto');

const SCHOOL_HOST = 'axinfu.sus.edu.cn';
const SCHOOL_PORT = 8082;
const SCHOOL_PATH = '/electric/getData/getReserveAM';
const PUSH_HOST = 'www.pushplus.plus';
const PUSH_PATH = '/send';

// 宿舍编号请通过环境变量配置，仓库里不包含任何真实房间信息
const cfg = {
  areaId: process.env.ROOM_AREA_ID || '',
  architectureId: process.env.ROOM_ARCHITECTURE_ID || '',
  floorId: process.env.ROOM_FLOOR_ID || '',
  roomId: process.env.ROOM_ROOM_ID || '',
  displayName: process.env.ROOM_DISPLAY_NAME || '我的宿舍',
  lowThreshold: Number(process.env.LOW_THRESHOLD || 20),
};
const PUSH_TOKEN = process.env.PUSHPLUS_TOKEN || '';
const PUSH_TOPIC = process.env.PUSHPLUS_TOPIC || '';
const WECHAT_TOKEN = process.env.WECHAT_TOKEN || '';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ---------------------------------------------------------------- 基础工具
function httpsRequest(o) {
  return new Promise((resolve, reject) => {
    const body = o.body || '';
    const headers = Object.assign({ 'Content-Length': Buffer.byteLength(body) }, o.headers || {});
    const opts = {
      host: o.host,
      port: o.port,
      path: o.path,
      method: o.method || 'GET',
      headers: headers,
      timeout: o.timeoutMs || 15000,
      rejectUnauthorized: true,
    };
    if (o.servername) opts.servername = o.servername;
    if (o.hostHeader) headers.Host = o.hostHeader;
    const req = https.request(opts, (res) => {
      const chunks = [];
      res.on('data', (c) => chunks.push(c));
      res.on('end', () => resolve({ status: res.statusCode, text: Buffer.concat(chunks).toString('utf8') }));
    });
    req.on('timeout', () => req.destroy(new Error('TIMEOUT:' + (o.timeoutMs || 15000) + 'ms')));
    req.on('error', (e) => reject(e));
    if (body) req.write(body);
    req.end();
  });
}

async function resolveIPv4(host) {
  try {
    const r = await dns.promises.lookup(host, { family: 4 });
    return r.address;
  } catch (e) {
    console.log('[dns] 解析失败 ' + e.message);
    return null;
  }
}

function beijingNow() {
  const d = new Date(Date.now() + 8 * 3600 * 1000);
  const p = (n) => String(n).padStart(2, '0');
  return {
    date: d.getUTCFullYear() + '-' + p(d.getUTCMonth() + 1) + '-' + p(d.getUTCDate()),
    time: p(d.getUTCHours()) + ':' + p(d.getUTCMinutes()),
  };
}

// ---------------------------------------------------------------- 查电费
async function queryOnce(timeoutMs) {
  if (!cfg.areaId || !cfg.architectureId || !cfg.floorId || !cfg.roomId) {
    throw new Error(
      '未配置宿舍编号：请在环境变量里设置 ROOM_AREA_ID / ROOM_ARCHITECTURE_ID / ROOM_FLOOR_ID / ROOM_ROOM_ID（可在桌面版程序里用 --dump-levels 查看对应编号）'
    );
  }
  const body =
    'areaId=' + encodeURIComponent(cfg.areaId) +
    '&architectureId=' + encodeURIComponent(cfg.architectureId) +
    '&floorId=' + encodeURIComponent(cfg.floorId) +
    '&roomId=' + encodeURIComponent(cfg.roomId);
  const headers = {
    'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
    Accept: 'application/json, text/javascript, */*; q=0.01',
    'X-Requested-With': 'XMLHttpRequest',
    'User-Agent':
      'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148 MicroMessenger/8.0.40',
  };

  const ip = await resolveIPv4(SCHOOL_HOST);
  const attempts = [];
  if (ip) attempts.push({ label: 'ip-direct', host: ip, servername: SCHOOL_HOST, hostHeader: SCHOOL_HOST + ':' + SCHOOL_PORT });
  attempts.push({ label: 'hostname', host: SCHOOL_HOST, servername: SCHOOL_HOST });

  let lastErr = null;
  for (const a of attempts) {
    try {
      const res = await httpsRequest({
        host: a.host,
        port: SCHOOL_PORT,
        path: SCHOOL_PATH,
        method: 'POST',
        body: body,
        headers: headers,
        servername: a.servername,
        hostHeader: a.hostHeader,
        timeoutMs: timeoutMs || 20000,
      });
      if (res.status !== 200) throw new Error('HTTP ' + res.status + ' ' + res.text.slice(0, 100));
      let json;
      try {
        json = JSON.parse(res.text);
      } catch (e) {
        throw new Error('非 JSON：' + res.text.slice(0, 100));
      }
      if (json.respCode !== '00') throw new Error('respCode=' + json.respCode + ' ' + (json.respDesc || ''));
      const d = json.data;
      if (!d) throw new Error('data 为空（房间 id 可能无效）');
      if (d.resultInfo && d.resultInfo.result === '3') throw new Error('有订单正在处理');
      const power = parseFloat(d.remainPower);
      if (!isFinite(power)) throw new Error('无法解析电量：' + d.remainPower);
      const unit = d.remainName === 'du' ? '度' : d.remainName === 'yuan' ? '元' : String(d.remainName || '');
      return { power: power, unit: unit, via: a.label };
    } catch (e) {
      lastErr = e;
      console.log('[query] ' + a.label + ' 失败：' + e.message + (e.code ? ' code=' + e.code : ''));
    }
  }
  throw lastErr || new Error('未知查询错误');
}

async function queryWithRetry(n, timeoutMs) {
  let last;
  for (let i = 1; i <= n; i++) {
    try {
      return await queryOnce(timeoutMs);
    } catch (e) {
      last = e;
      console.log('[query] 第 ' + i + ' 轮失败：' + e.message);
      if (i < n) await sleep(1200 * i);
    }
  }
  throw last;
}

function buildMessage(r, t) {
  const pretty = r.power.toFixed(2) + ' ' + r.unit;
  const low = r.unit === '度' && r.power <= cfg.lowThreshold;
  const lines = [
    '⚡ 上体宿舍电量',
    '📍 ' + cfg.displayName,
    '🔋 剩余 ' + pretty,
    '🕗 ' + t.date + ' ' + t.time,
  ];
  if (low) lines.push('', '⚠️ 电量低于 ' + cfg.lowThreshold + ' ' + r.unit + '，建议尽快充值');
  lines.push('', '（数据源：校园安心付，以实际电表为准）');
  return lines.join('\n');
}

async function pushPlus(title, content) {
  const payload = { token: PUSH_TOKEN, title: title, content: content, template: 'txt' };
  if (PUSH_TOPIC) payload.topic = PUSH_TOPIC;
  const res = await httpsRequest({
    host: PUSH_HOST,
    port: 443,
    path: PUSH_PATH,
    method: 'POST',
    body: JSON.stringify(payload),
    headers: { 'Content-Type': 'application/json' },
    servername: PUSH_HOST,
    timeoutMs: 15000,
  });
  console.log('[push] HTTP ' + res.status + ' ' + res.text.slice(0, 200));
  if (res.status !== 200) throw new Error('推送 HTTP ' + res.status);
  let j = null;
  try {
    j = JSON.parse(res.text);
  } catch (e) {}
  if (j && j.code !== 200) throw new Error('推送失败 code=' + j.code + ' msg=' + j.msg);
}

// ---------------------------------------------------------------- 定时任务
async function runScheduledPush() {
  const t = beijingNow();
  console.log('[timer] v2.0 ' + t.date + ' ' + t.time + ' 定时推送 ' + cfg.displayName);
  const r = await queryWithRetry(3, 20000);
  const message = buildMessage(r, t);
  console.log('----- 正文 -----\n' + message + '\n----------------');
  if (!PUSH_TOKEN) throw new Error('未配置 PUSHPLUS_TOKEN');
  await pushPlus('上体宿舍电量', message);
  return 'ok: ' + r.power.toFixed(2) + ' ' + r.unit;
}

// ---------------------------------------------------------------- 公众号问答
function sha1(str) {
  return crypto.createHash('sha1').update(str, 'utf8').digest('hex');
}

function checkSignature(params) {
  if (!WECHAT_TOKEN) return true; // 没配 token 时跳过校验（便于测试）
  const signature = params.signature || '';
  const timestamp = params.timestamp || '';
  const nonce = params.nonce || '';
  if (!signature) return false;
  const raw = [WECHAT_TOKEN, timestamp, nonce].sort().join('');
  return sha1(raw) === signature;
}

function xmlField(xml, name) {
  const m = new RegExp('<' + name + '>([\\s\\S]*?)<\\/' + name + '>').exec(xml || '');
  if (!m) return '';
  return m[1].replace(/^<!\[CDATA\[/, '').replace(/\]\]>$/, '');
}

function textXml(to, from, content) {
  return (
    '<xml>' +
    '<ToUserName><![CDATA[' + to + ']]></ToUserName>' +
    '<FromUserName><![CDATA[' + from + ']]></FromUserName>' +
    '<CreateTime>' + Math.floor(Date.now() / 1000) + '</CreateTime>' +
    '<MsgType><![CDATA[text]]></MsgType>' +
    '<Content><![CDATA[' + content + ']]></Content>' +
    '</xml>'
  );
}

const KEYWORDS = ['查电费', '查电量', '电量', '电费', '剩多少电', '还有多少电'];

async function handleWechat(event) {
  const method = (event.httpMethod || 'GET').toUpperCase();
  const query = event.queryStringParameters || {};
  const body = event.isBase64Encoded ? Buffer.from(event.body || '', 'base64').toString('utf8') : event.body || '';

  console.log('[wx] ' + method + ' 收到微信请求');

  if (!checkSignature(query)) {
    console.log('[wx] 签名校验失败');
    return 'invalid signature';
  }

  // GET：服务器配置时的 URL 校验
  if (method === 'GET') {
    const echo = query.echostr || '';
    console.log('[wx] 校验通过，返回 echostr');
    return echo;
  }

  // POST：用户发来的消息
  const fromUser = xmlField(body, 'FromUserName');
  const toUser = xmlField(body, 'ToUserName');
  const msgType = xmlField(body, 'MsgType');
  const content = (xmlField(body, 'Content') || '').trim();
  console.log('[wx] type=' + msgType + ' content="' + content + '" from=' + fromUser);

  if (!fromUser || !toUser) {
    console.log('[wx] 解析失败，原样返回 success');
    return 'success';
  }

  if (msgType !== 'text') {
    return textXml(fromUser, toUser, '发送「查电费」即可查询当前剩余电量～');
  }

  const hit = KEYWORDS.some((k) => content.includes(k));
  if (!hit) {
    return textXml(fromUser, toUser, '发送「查电费」即可查询当前剩余电量。\n当前查询：' + cfg.displayName);
  }

  const t = beijingNow();
  try {
    // 微信要求 5 秒内回复，所以只试 1 次、超时压到 4 秒
    const r = await queryWithRetry(1, 4000);
    const pretty = r.power.toFixed(2) + ' ' + r.unit;
    const low = r.unit === '度' && r.power <= cfg.lowThreshold;
    let reply =
      '⚡ 剩余 ' + pretty + '\n' +
      '📍 ' + cfg.displayName + '\n' +
      '🕗 ' + t.date + ' ' + t.time;
    if (low) reply += '\n\n⚠️ 电量偏低（低于 ' + cfg.lowThreshold + ' ' + r.unit + '），建议尽快充值';
    reply += '\n\n（数据源：校园安心付，以实际电表为准）';
    console.log('[wx] 回复：' + pretty);
    return textXml(fromUser, toUser, reply);
  } catch (e) {
    console.log('[wx] 查询失败：' + e.message);
    return textXml(fromUser, toUser, '❌ 查询失败：' + e.message + '\n请稍后再试一次。');
  }
}

// ---------------------------------------------------------------- 入口
exports.main_handler = async (event, context) => {
  const ev = event || {};
  const isTimer = ev.Type === 'Timer' || ev.type === 'Timer' || Boolean(ev.TriggerName);
  const isHttp = Boolean(ev.httpMethod);

  if (isHttp) {
    return await handleWechat(ev);
  }
  // 定时触发 或 控制台手动测试（空事件）都走推送
  return await runScheduledPush();
};
