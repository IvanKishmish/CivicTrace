import http from 'node:http';
import {readFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
const root=fileURLToPath(new URL('.',import.meta.url));
const seed = fileURLToPath(new URL('../../data/procurements.seed.json', import.meta.url));
const mime={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json; charset=utf-8'};
const server=http.createServer(async(req,res)=>{
  try {
    const url=new URL(req.url,'http://localhost');
    const pathname=decodeURIComponent(url.pathname);
    const file = pathname === '/procurements.seed.json' ? seed : path.resolve(root,'.'+(pathname==='/'?'/index.html':pathname));
    if((file !== seed && !file.startsWith(root)) || !mime[path.extname(file)]) {res.writeHead(404);res.end('Not found');return;}
    const content=await readFile(file);
    res.writeHead(200,{'Content-Type':mime[path.extname(file)],'Cache-Control':'no-store','X-Content-Type-Options':'nosniff'});res.end(content);
  }catch {res.writeHead(404);res.end('Not found');}
});
server.listen(4173,'127.0.0.1',()=>console.log('CivicTrace: http://127.0.0.1:4173'));
