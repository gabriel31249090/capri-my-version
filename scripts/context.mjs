import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { runCli } from 'repomix';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const artifacts = path.join(root, '.artifacts', 'context');
const shared = ['AGENTS.md', 'docs/development-workflow.md'];
const profiles = {
  default: null,
  brain: [...shared, 'app/Brain.cs', 'app/Engine.cs', 'app/CapriProfile.cs', 'app/ToolPermission.cs', 'CAPRI/*.txt', 'CAPRI/Modelfile'],
  tools: [...shared, 'app/Hands.cs', 'app/Computer.cs', 'app/ToolPermission.cs', 'app/Capri.cs', 'app/Brain.cs', 'rules/*.yml', 'sgconfig.yml'],
  voice: [...shared, 'app/Voice.cs', 'app/Agent.cs', 'app/Ears.cs', 'app/Dictation.cs', 'app/CapriProfile.cs', 'app/ToolPermission.cs', 'CAPRI/*.txt'],
  web: [...shared, 'site/*.html', 'site/*.js', 'site/*.css', 'site/setup/*.html', 'site/release.json', 'worker/src/*.js', 'worker/migrations/*.sql', 'worker/wrangler.jsonc'],
};

function hashText(text) {
  return createHash('sha256').update(text).digest('hex');
}

async function hashFile(filename) {
  const hash = createHash('sha256');
  for await (const chunk of createReadStream(filename)) hash.update(chunk);
  return hash.digest('hex');
}

function parseArguments(args) {
  const options = { profile: 'default', measure: false, list: false, check: false };
  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];
    if (argument === '--profile') options.profile = args[++index];
    else if (argument === '--measure') options.measure = true;
    else if (argument === '--list') options.list = true;
    else if (argument === '--check') options.check = true;
    else throw new Error(`Unknown argument: ${argument}`);
  }
  if (!Object.hasOwn(profiles, options.profile)) {
    throw new Error(`Unknown profile. Choose: ${Object.keys(profiles).join(', ')}`);
  }
  return options;
}

async function readConfiguration(filename) {
  return JSON.parse(await readFile(path.join(root, filename), 'utf8'));
}

function verifyComparisonSettings(selected, baseline) {
  const comparable = (config) => ({
    ...config,
    include: undefined,
    output: { ...config.output, filePath: undefined },
  });
  if (JSON.stringify(comparable(selected)) !== JSON.stringify(comparable(baseline))) {
    throw new Error('Baseline and selected configs must differ only in include patterns and output path.');
  }
}

async function pack(profile, configName, config, include) {
  const outputPath = path.join(artifacts, `${profile}.xml`);
  const options = { config: path.join(root, configName), output: outputPath, quiet: true };
  if (include) options.include = include.join(',');
  const started = Date.now();
  const { packResult } = await runCli([root], root, options);
  if (!packResult || !Number.isFinite(packResult.totalTokens) || packResult.totalFiles === 0) {
    throw new Error(`Repomix produced no usable result for ${profile}.`);
  }
  const files = await Promise.all(Object.entries(packResult.fileTokenCounts)
    .sort(([left], [right]) => left.localeCompare(right))
    .map(async ([relativePath, tokens]) => {
      const filename = path.resolve(root, relativePath);
      if (!filename.startsWith(`${root}${path.sep}`)) throw new Error('Manifest path left the repository.');
      return { path: relativePath.replaceAll('\\', '/'), tokens, sha256: await hashFile(filename) };
    }));
  const manifest = {
    profile,
    generatedAt: new Date().toISOString(),
    encoding: config.tokenCount.encoding,
    compress: config.output.compress,
    configurationSha256: hashText(JSON.stringify({ ...config, include: include ?? config.include })),
    manifestSha256: hashText(JSON.stringify(files)),
    output: path.relative(root, outputPath).replaceAll('\\', '/'),
    outputSha256: await hashFile(outputPath),
    totalFiles: packResult.totalFiles,
    totalTokens: packResult.totalTokens,
    elapsedMs: Date.now() - started,
    files,
  };
  if (files.length !== manifest.totalFiles) throw new Error('Repomix file count does not match its token manifest.');
  const manifestPath = path.join(artifacts, `${profile}.manifest.json`);
  await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
  const { files: omittedFiles, ...summary } = manifest;
  return { ...summary, manifest: path.relative(root, manifestPath).replaceAll('\\', '/') };
}

async function checkManifest(profile) {
  const manifest = JSON.parse(await readFile(path.join(artifacts, `${profile}.manifest.json`), 'utf8'));
  const config = await readConfiguration(profile === 'baseline' ? 'repomix.baseline.config.json' : 'repomix.config.json');
  const effectiveInclude = profiles[profile] ?? config.include;
  if (hashText(JSON.stringify({ ...config, include: effectiveInclude })) !== manifest.configurationSha256) {
    throw new Error('Context configuration changed since packing. Generate the profile again.');
  }
  if (hashText(JSON.stringify(manifest.files)) !== manifest.manifestSha256) throw new Error('Manifest hash mismatch.');
  for (const file of manifest.files) {
    const filename = path.resolve(root, file.path);
    if (!filename.startsWith(`${root}${path.sep}`)) throw new Error('Manifest path left the repository.');
    if (await hashFile(filename) !== file.sha256) throw new Error(`Source changed since packing: ${file.path}`);
  }
  const outputPath = path.resolve(root, manifest.output);
  if (!outputPath.startsWith(`${artifacts}${path.sep}`)) throw new Error('Output path left the artifact directory.');
  if (await hashFile(outputPath) !== manifest.outputSha256) throw new Error('Packed output hash mismatch.');
  console.log(JSON.stringify({ status: 'pass', profile, checkedFiles: manifest.files.length }));
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  if (options.list) {
    console.log(Object.keys(profiles).join('\n'));
    return;
  }
  if (options.check) {
    await checkManifest(options.profile);
    return;
  }
  await mkdir(artifacts, { recursive: true });
  const selectedConfig = await readConfiguration('repomix.config.json');
  let baseline;
  if (options.measure) {
    const baselineConfig = await readConfiguration('repomix.baseline.config.json');
    verifyComparisonSettings(selectedConfig, baselineConfig);
    baseline = await pack('baseline', 'repomix.baseline.config.json', baselineConfig);
  }
  const selected = await pack(options.profile, 'repomix.config.json', selectedConfig, profiles[options.profile]);
  if (baseline) {
    const removedTokens = baseline.totalTokens - selected.totalTokens;
    const metrics = {
      generatedAt: new Date().toISOString(),
      repository: path.basename(root),
      repomixVersion: JSON.parse(await readFile(path.join(root, 'node_modules', 'repomix', 'package.json'), 'utf8')).version,
      profile: options.profile,
      tokenizer: selectedConfig.tokenCount.encoding,
      comparison: 'Same repository, tokenizer, output format and ignores; only include patterns differ.',
      baseline,
      selected,
      reduction: { tokens: removedTokens, percent: Number((removedTokens / baseline.totalTokens * 100).toFixed(2)) },
    };
    await writeFile(path.join(root, '.artifacts', 'token-metrics.json'), `${JSON.stringify(metrics, null, 2)}\n`);
    await writeFile(path.join(root, '.artifacts', `token-metrics.${options.profile}.json`), `${JSON.stringify(metrics, null, 2)}\n`);
    console.log(JSON.stringify(metrics, null, 2));
  } else console.log(JSON.stringify(selected, null, 2));
}

main().catch((error) => {
  console.error(`Context generation failed: ${error.message}`);
  process.exitCode = 1;
});
