const fs = require('fs'), path = require('path'), assert = require('assert');
const root = path.resolve('QLKhachSan');
const project = fs.readFileSync(path.join(root, 'QLKhachSan.csproj'), 'utf8');
let checked = 0;
for (const match of project.matchAll(/<(?:Compile|Content) Include="([^"]+)"/g)) {
    if (/[*?]/.test(match[1])) continue;
    assert(fs.existsSync(path.join(root, match[1].replace(/\\/g, '/'))), 'Missing project file: ' + match[1]); checked++;
}
for (const folder of ['Controllers', 'Models', 'Filters']) {
    for (const name of fs.readdirSync(path.join(root, folder)).filter(n => n.endsWith('.cs'))) {
        assert(project.includes('Compile Include="' + folder + '\\' + name + '"'), 'C# source omitted: ' + name);
    }
}
for (const [folder, glob] of [['database','*.sql'],['tests','*'],['docs','*.md']]) {
    assert(project.includes('None Include="..\\' + folder + '\\' + glob + '"'), 'Support folder not linked: ' + folder);
    assert(fs.readdirSync(folder).length > 0, 'Empty support folder: ' + folder);
}
console.log('PASS: ' + checked + ' Compile/Content files exist; all controller/model/filter C# sources included; SQL/tests/docs linked as non-public source items.');
