// Parse authored inline JS after substituting Razor output with inert placeholders.
// This is syntax verification, not a replacement for the real browser tests.
const fs = require('fs'), path = require('path'), vm = require('vm');
let scripts = 0, files = 0;
function razorExpressions(source) {
    let result = '', i = 0;
    while (i < source.length) {
        const direct = source.slice(i, i + 2) === '@(';
        const match = direct ? ['@(', 'Inline'] : source.slice(i).match(/^@(Url\.(Action|Content)|Html\.Raw|Json\.Encode)\s*\(/);
        if (match) {
            let j = i + match[0].length, depth = 1, quoted = false, escaped = false;
            for (; j < source.length && depth; j++) {
                const c = source[j];
                if (quoted) { if (escaped) escaped = false; else if (c === '\\') escaped = true; else if (c === '"') quoted = false; }
                else if (c === '"') quoted = true;
                else if (c === '(') depth++;
                else if (c === ')') depth--;
            }
            if (depth) throw Error('Unclosed Razor expression');
            const helper = match[1];
            result += helper.startsWith('Url.') ? '/test-placeholder' : helper === 'Inline' ? 'false' : '[]'; i = j; continue;
        }
        result += source[i++];
    }
    return result.replace(/@Model\.\w+/g, '1')
        .replace(/@DateTime\.\w+(?:\.\w+)*/g, '2026')
        .replace(/@(?:ViewBag|TempData)\.\w+/g, '0');
}
function scan(dir) {
    for (const item of fs.readdirSync(dir, { withFileTypes: true })) {
        const file = path.join(dir, item.name);
        if (item.isDirectory()) { scan(file); continue; }
        if (!file.endsWith('.cshtml')) continue;
        const html = fs.readFileSync(file, 'utf8'), fragments = [];
        for (const match of html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)) {
            if (/\bsrc\s*=/.test(match[1])) continue;
            const js = razorExpressions(match[2]);
            try { new vm.Script(js, { filename: file }); }
            catch (error) { console.error(file + ': ' + error.stack); process.exitCode = 1; }
            fragments.push(js); scripts++;
        }
        if (fragments.length) {
            try { new vm.Script(fragments.join('\n;\n'), { filename: file }); }
            catch (error) { console.error(file + ' combined scope: ' + error.stack); process.exitCode = 1; }
            files++;
        }
    }
}
scan('QLKhachSan/Views');
if (!process.exitCode) console.log('PASS: ' + scripts + ' inline scripts across ' + files + ' views; no syntax errors or duplicate lexical declarations.');
