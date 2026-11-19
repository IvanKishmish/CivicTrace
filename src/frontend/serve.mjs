import { createServer, request } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PORT = Number(process.env.PORT) || 4173;
const HOST = process.env.HOST || '127.0.0.1';

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png'
};

createServer(async (req, res) => {
  try {
    const reqUrl = new URL(req.url || '/', `http://${req.headers.host || 'localhost'}`);
    if (reqUrl.pathname.startsWith('/api/')) {
      const target = new URL(reqUrl.pathname + reqUrl.search, process.env.API_PROXY_TARGET || 'http://127.0.0.1:8080');
      const upstream = request(target, { method: req.method, headers: { ...req.headers, host: target.host } }, (response) => {
        res.writeHead(response.statusCode, response.headers);
        response.pipe(res);
      });
      upstream.setTimeout(30000, () => upstream.destroy(new Error('API timeout')));
      upstream.on('error', () => {
        if (!res.headersSent) res.writeHead(502, { 'Content-Type': 'application/problem+json' });
        res.end(JSON.stringify({ detail: 'API недоступний. Перевірте запуск Docker і повторіть запит.' }));
      });
      req.pipe(upstream);
      return;
    }
    let pathname = decodeURIComponent(reqUrl.pathname);
    if (pathname === '/') pathname = '/index.html';

    const safePath = path.normalize(pathname).replace(/^(\.\.(\/|\\|$))+/, '');
    const filePath = path.join(__dirname, safePath);
    if (!filePath.startsWith(__dirname)) { res.writeHead(403); res.end('Forbidden'); return; }

    const info = await stat(filePath).catch(() => null);
    if (!info || !info.isFile()) { res.writeHead(404); res.end('Not Found'); return; }

    const ext = path.extname(filePath).toLowerCase();
    res.writeHead(200, { 'Content-Type': MIME[ext] || 'application/octet-stream', 'Cache-Control': 'no-cache' });
    res.end(await readFile(filePath));
  } catch {
    res.writeHead(500);
    res.end('Internal Server Error');
  }
}).listen(PORT, HOST, () => console.log(`Listening at http://${HOST}:${PORT}/`));