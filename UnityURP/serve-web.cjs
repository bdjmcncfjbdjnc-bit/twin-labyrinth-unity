const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');

const variant = process.argv.includes('--rules') ? 'Blackout-Web' :
  process.argv.includes('--haunt') ? 'Haunt-Web' : process.argv.includes('--maze') ? 'Maze-Web' : 'Web';
const root = path.resolve(__dirname, 'Builds', variant);
const port = Number(process.env.TWIN_PREVIEW_PORT || 8787);
const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.wasm': 'application/wasm',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.json': 'application/json',
};

if (!fs.existsSync(path.join(root, 'index.html'))) {
  throw new Error('Unity Web build missing. Build the Web target before starting the preview.');
}

http.createServer((request, response) => {
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    response.writeHead(405, { Allow: 'GET, HEAD' }).end();
    return;
  }
  let pathname;
  try {
    pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
  } catch {
    response.writeHead(400).end();
    return;
  }
  const resource = pathname === '/' ? '/index.html' : pathname;
  const target = path.resolve(root, '.' + resource);
  if (!/^\/(?:index\.html|Rocketbox-Notices\.txt|(?:Build|TemplateData)\/[a-zA-Z0-9_.-]+)$/.test(resource) ||
      !target.startsWith(root + path.sep)) {
    response.writeHead(404).end();
    return;
  }

  fs.stat(target, (error, stat) => {
    if (error || !stat.isFile()) {
      response.writeHead(404).end();
      return;
    }
    const compressed = /\.(br|gz)$/.exec(target);
    const uncompressed = compressed ? target.slice(0, -compressed[0].length) : target;
    response.setHeader('Content-Type', types[path.extname(uncompressed)] || 'application/octet-stream');
    response.setHeader('Content-Length', stat.size);
    if (compressed) response.setHeader('Content-Encoding', compressed[1] === 'br' ? 'br' : 'gzip');
    if (request.method === 'HEAD') {
      response.end();
      return;
    }
    const stream = fs.createReadStream(target);
    stream.on('error', () => response.destroy());
    stream.pipe(response);
  });
}).listen(port, '127.0.0.1', () => {
  console.log(`Twin Labyrinth Web preview at http://127.0.0.1:${port}/`);
});
