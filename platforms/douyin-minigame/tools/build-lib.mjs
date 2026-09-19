import { stripTypeScriptTypes } from 'node:module';

const NAMED_IMPORT = /import\s*\{([\s\S]*?)\}\s*from\s*(['"])([^'"]+)\2\s*;?/g;

function commonJsBindings(bindings) {
  return bindings
    .split(',')
    .map((binding) => binding.trim())
    .filter(Boolean)
    .map((binding) => binding.replace(/\s+as\s+/, ': '))
    .join(', ');
}

export function runtimeImports(source) {
  const stripped = stripTypeScriptTypes(source, { mode: 'strip' });
  return [...stripped.matchAll(NAMED_IMPORT)].map((match) => match[3]);
}

export function transformToCommonJs(source, mapRequirePath = (specifier) => specifier) {
  const exported = [];
  let output = stripTypeScriptTypes(source, { mode: 'strip' });

  output = output.replace(NAMED_IMPORT, (_match, bindings, _quote, specifier) =>
    `const { ${commonJsBindings(bindings)} } = require('${mapRequirePath(specifier)}');`);

  output = output.replace(
    /\bexport\s+(const|let|var|async\s+function|function|class)\s+([A-Za-z_$][\w$]*)/g,
    (_match, declaration, name) => {
      exported.push(name);
      return `${declaration} ${name}`;
    },
  );

  if (/(?:^|\n)\s*(?:import|export)\s/m.test(output)) {
    throw new Error('Unsupported ESM syntax remains after CommonJS transform');
  }

  const assignments = [...new Set(exported)]
    .map((name) => `module.exports.${name} = ${name};`)
    .join('\n');
  return `${output.trim()}${assignments ? `\n\n${assignments}` : ''}\n`;
}
