export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    const path = url.pathname;
    const segments = path.split("/").filter(Boolean);

    // 获取来访者 IP
    const ip = request.headers.get("CF-Connecting-IP") || "unknown";

    // CORS 头
    const corsHeaders = {
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type",
    };

    // 处理 OPTIONS 请求
    if (request.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: corsHeaders });
    }

    // 路由：/stats - 显示统计信息
    if (segments[0] === "stats") {
      // 检查是否为新访客
      const visitorKey = `visitor:${ip}`;
      const existingVisitor = await env.gign_kv_namespace.get(visitorKey);
      let isNewVisitor = false;

      if (existingVisitor === null) {
        // 新访客，记录 IP
        isNewVisitor = true;
        await env.gign_kv_namespace.put(
          visitorKey,
          JSON.stringify({
            ip: ip,
            firstVisit: new Date().toISOString(),
            visits: 1,
          }),
        );

        // 更新总来访者计数
        const countKey = "total_visitors";
        let countData = await env.gign_kv_namespace.get(countKey, "json");
        if (!countData) {
          countData = { count: 0, lastUpdated: new Date().toISOString() };
        }
        countData.count += 1;
        countData.lastUpdated = new Date().toISOString();
        await env.gign_kv_namespace.put(countKey, JSON.stringify(countData));
      } else {
        // 老访客，更新访问次数
        const visitorData = JSON.parse(existingVisitor);
        visitorData.visits = (visitorData.visits || 0) + 1;
        visitorData.lastVisit = new Date().toISOString();
        await env.gign_kv_namespace.put(
          visitorKey,
          JSON.stringify(visitorData),
        );
      }

      // 获取总来访者数
      const countKey = "total_visitors";
      let countData = await env.gign_kv_namespace.get(countKey, "json");
      const totalCount = countData ? countData.count : 0;

      // 返回统计信息
      if (segments[1] === "count") {
        // /stats/count - 只返回数字
        return new Response(totalCount.toString(), {
          status: 200,
          headers: {
            "Content-Type": "text/plain; charset=utf-8",
            ...corsHeaders,
          },
        });
      }

      if (segments[1] === "json") {
        // /stats/json - 返回 JSON 格式
        return new Response(
          JSON.stringify(
            {
              totalVisitors: totalCount,
              currentIP: ip,
              isNewVisitor: isNewVisitor,
              lastUpdated: countData ? countData.lastUpdated : null,
            },
            null,
            2,
          ),
          {
            status: 200,
            headers: {
              "Content-Type": "application/json; charset=utf-8",
              ...corsHeaders,
            },
          },
        );
      }

      // /stats - 返回 HTML 页面
      const html = `<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>访问统计 | KV Storage</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            padding: 20px;
        }
        .container {
            background: white;
            border-radius: 20px;
            padding: 40px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            text-align: center;
            max-width: 500px;
            width: 100%;
        }
        h1 {
            color: #333;
            margin-bottom: 30px;
            font-size: 28px;
        }
        .counter {
            font-size: 72px;
            font-weight: bold;
            color: #667eea;
            margin: 20px 0;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.1);
        }
        .label {
            color: #666;
            font-size: 18px;
            margin-bottom: 30px;
        }
        .info {
            background: #f5f5f5;
            border-radius: 10px;
            padding: 20px;
            margin-top: 20px;
        }
        .info p {
            color: #555;
            margin: 10px 0;
            font-size: 14px;
        }
        .badge {
            display: inline-block;
            padding: 5px 15px;
            border-radius: 20px;
            font-size: 14px;
            font-weight: bold;
            margin-top: 10px;
        }
        .badge.new {
            background: #4CAF50;
            color: white;
        }
        .badge.returning {
            background: #2196F3;
            color: white;
        }
        .api-info {
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #eee;
        }
        .api-info h3 {
            color: #333;
            margin-bottom: 15px;
        }
        .api-info code {
            background: #f0f0f0;
            padding: 3px 8px;
            border-radius: 4px;
            font-size: 13px;
        }
        .api-info ul {
            list-style: none;
            text-align: left;
        }
        .api-info li {
            margin: 8px 0;
            color: #666;
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>访问统计</h1>
        <div class="counter">${totalCount}</div>
        <div class="label">累计独立访客</div>
        
        <div class="info">
            <p>您的 IP: <strong>${ip}</strong></p>
            <p>
                <span class="badge ${isNewVisitor ? "new" : "returning"}">
                    ${isNewVisitor ? "新访客" : "老朋友"}
                </span>
            </p>
        </div>

        <div class="api-info">
            <h3>API 接口</h3>
            <ul>
                <li><code>GET /stats</code> - 显示此页面</li>
                <li><code>GET /stats/json</code> - 返回 JSON 数据</li>
                <li><code>GET /stats/count</code> - 只返回数字</li>
            </ul>
        </div>
    </div>
</body>
</html>`;

      return new Response(html, {
        status: 200,
        headers: { "Content-Type": "text/html; charset=utf-8", ...corsHeaders },
      });
    }

    // 路由：/create/{键名}
    if (segments[0] === "create" && segments.length === 2) {
      const key = decodeURIComponent(segments[1]);
      const existing = await env.gign_kv_namespace.get(key);
      if (existing !== null) {
        return new Response(`键 "${key}" 已存在`, {
          status: 409,
          headers: corsHeaders,
        });
      }
      await env.gign_kv_namespace.put(key, "");
      return new Response(`已创建键: ${key}`, {
        status: 201,
        headers: corsHeaders,
      });
    }

    // 路由：/delete/{键名}
    if (segments[0] === "delete" && segments.length === 2) {
      const key = decodeURIComponent(segments[1]);
      const existing = await env.gign_kv_namespace.get(key);
      if (existing === null) {
        return new Response(`键 "${key}" 不存在`, {
          status: 404,
          headers: corsHeaders,
        });
      }
      await env.gign_kv_namespace.delete(key);
      return new Response(`已删除键: ${key}`, {
        status: 200,
        headers: corsHeaders,
      });
    }

    // 路由：/write/{键名}/{内容}
    if (segments[0] === "write" && segments.length >= 3) {
      const key = decodeURIComponent(segments[1]);
      const content = decodeURIComponent(segments.slice(2).join("/"));
      await env.gign_kv_namespace.put(key, content);
      return new Response(`已写入键 "${key}": ${content}`, {
        status: 200,
        headers: corsHeaders,
      });
    }

    // 路由：/read/{键名}
    if (segments[0] === "read" && segments.length === 2) {
      const key = decodeURIComponent(segments[1]);
      const content = await env.gign_kv_namespace.get(key);
      if (content === null) {
        return new Response(`键 "${key}" 不存在`, {
          status: 404,
          headers: corsHeaders,
        });
      }
      return new Response(content, { status: 200, headers: corsHeaders });
    }

    // 在 worker.js 的路由判断区域添加：
    if (segments[0] === "list") {
      const list = await env.gign_kv_namespace.list();
      const keys = list.keys.map((k) => k.name);
      return new Response(JSON.stringify(keys, null, 2), {
        status: 200,
        headers: { "Content-Type": "application/json", ...corsHeaders },
      });
    }

    // 其他路由返回 404
    return new Response("未找到", { status: 404, headers: corsHeaders });
  },
};
