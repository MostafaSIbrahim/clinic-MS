import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { marked } from 'file:///C:/Users/lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/marked/lib/marked.esm.js';

const dir = path.dirname(fileURLToPath(import.meta.url));
const stem = process.argv[2] || 'UIUX-Implementation-Guide';
if (!/^[A-Za-z0-9-]+$/.test(stem)) throw Error('Invalid guide filename');
const md = fs.readFileSync(path.join(dir, stem + '.md'), 'utf8');
if ((md.match(/^```/gm) || []).length % 2) throw Error('Unbalanced code fences');
let html = marked.parse(md);
const toc = [];
let index = 0;
html = html.replace(/<h2>(.*?)<\/h2>/g, (_, title) => {
  const id = `section-${++index}`;
  toc.push(`<li><a href="#${id}">${title}</a></li>`);
  return `<h2 id="${id}">${title}</h2>`;
});
html = html.replace(/<table>/g, '<div class="table-scroll"><table>')
  .replace(/<\/table>/g, '</table></div>');
const output = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Safya Clinic — UI/UX Implementation Guide</title>
<style>
:root{color-scheme:light;--purple:#5b21b6;--line:#ddd6ee}
*{box-sizing:border-box}body{margin:0;background:#f6f5fb;color:#202332;font:16px/1.65 system-ui,Segoe UI,sans-serif}
.shell{max-width:1460px;margin:auto;display:grid;grid-template-columns:275px minmax(0,1fr);gap:32px;padding:32px}
aside{position:sticky;top:24px;align-self:start;max-height:calc(100vh - 48px);overflow:auto;font-size:14px}
aside strong{color:var(--purple)}aside ol{padding-left:20px}aside li{margin:10px 0}a{color:var(--purple);text-underline-offset:3px}
main{min-width:0;background:white;border:1px solid var(--line);border-radius:16px;padding:42px}
h1{font-size:2rem;line-height:1.2;color:#38156d}h2{font-size:1.55rem;border-top:2px solid var(--line);padding-top:30px;margin-top:42px;scroll-margin-top:20px}h3{font-size:1.15rem;color:#38156d;margin-top:28px}p,li{overflow-wrap:anywhere}li{margin-bottom:8px}
pre{position:relative;background:#151a2a;color:#f3f4ff;border-radius:10px;padding:22px;overflow:auto;line-height:1.55;font-size:13px;tab-size:4}code{font-family:Consolas,monospace}p code,li code,td code{background:#f0ecf7;padding:2px 4px;border-radius:4px}
.table-scroll{overflow-x:auto}table{width:100%;border-collapse:collapse;font-size:14px;margin:18px 0}th,td{text-align:left;padding:11px;border:1px solid var(--line);vertical-align:top}th{background:#eee8fa}tr:nth-child(even){background:#faf9fd}
.tools{display:flex;flex-wrap:wrap;gap:10px;margin:20px 0}button,.tools a{border:1px solid var(--purple);background:white;color:var(--purple);border-radius:7px;padding:9px 13px;font:inherit;cursor:pointer;text-decoration:none}button:focus-visible,a:focus-visible{outline:3px solid #0e7490;outline-offset:3px}.copy{font-size:12px;margin-bottom:0;padding:5px 9px}
@media(max-width:1000px){.shell{display:block;padding:16px}aside{position:static;max-height:none;margin-bottom:24px}aside ol{columns:2}main{padding:24px}}
@media(max-width:600px){aside ol{columns:1}main{padding:18px}h1{font-size:1.6rem}pre{padding:14px}}
@media print{body{background:white;font-size:10pt}.shell{display:block;padding:0}aside,.tools,.copy{display:none}main{border:0;padding:0}h2{break-before:page}h2,h3{break-after:avoid}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f4f4f4;color:#111;border:1px solid #ddd;font-size:8pt}table{font-size:9pt}.table-scroll{overflow:visible}a{color:#222}}
</style></head><body><div class="shell"><aside><strong>Safya Clinic · Implementation guide</strong><ol>${toc.join('')}</ol></aside><main>
<div class="tools"><button onclick="window.print()">Print / Save as PDF</button><a href="${stem}.md" download>Download Markdown</a></div>
${html}
</main></div><script>
document.querySelectorAll('pre').forEach(pre=>{const b=document.createElement('button');b.className='copy';b.textContent='Copy code';b.onclick=async()=>{try{await navigator.clipboard.writeText(pre.querySelector('code').textContent);b.textContent='Copied';setTimeout(()=>b.textContent='Copy code',1500)}catch{b.textContent='Select code and copy manually'}};pre.before(b)});
</script></body></html>`;
fs.writeFileSync(path.join(dir, stem + '.html'), output);
console.log(JSON.stringify({words:md.split(/\s+/).length, sections:toc.length, codeBlocks:(md.match(/^```\w/gm)||[]).length,htmlBytes:Buffer.byteLength(output)}));
