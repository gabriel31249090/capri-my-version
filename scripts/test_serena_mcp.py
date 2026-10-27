"""Exercise the actual stdio MCP and C# LSP; edit only an isolated fixture."""
from __future__ import annotations

import asyncio
import json
import os
import subprocess
from pathlib import Path

import yaml
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

ROOT = Path(__file__).resolve().parents[1]
ARTIFACTS = ROOT / '.artifacts'
ARTIFACTS.mkdir(exist_ok=True)


def result_text(result) -> str:
    text = '\n'.join(getattr(block, 'text', '') for block in result.content)
    if getattr(result, 'is_error', False) or text.startswith('Error executing tool'):
        raise AssertionError(text)
    return text


async def call(session, name, arguments):
    result = await session.call_tool(name, arguments, read_timeout_seconds=240)
    return result_text(result)


async def inspect_project() -> dict:
    params = StdioServerParameters(
        command='powershell.exe',
        args=['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(ROOT / 'scripts/Start-Serena.ps1')],
        cwd=ROOT,
    )
    with (ARTIFACTS / 'serena-mcp.log').open('w', encoding='utf-8') as log:
        async with stdio_client(params, errlog=log) as streams:
            async with ClientSession(*streams) as session:
                initialized = await session.initialize()
                listing = await session.list_tools()
                tools = {tool.name for tool in listing.tools}
                expected = set(yaml.safe_load((ROOT / '.serena/project.yml').read_text())['fixed_tools'])
                assert tools == expected, f'Unexpected tool set: {tools ^ expected}'
                overview = await call(session, 'get_symbols_overview', {'relative_path': 'app/Brain.cs', 'depth': 1})
                assert 'Brain' in overview, overview
                symbol = await call(session, 'find_symbol', {
                    'name_path_pattern': 'Brain/PickModel', 'relative_path': 'app/Brain.cs',
                    'include_body': False, 'max_answer_chars': 6000,
                })
                assert 'PickModel' in symbol, symbol
                references = await call(session, 'find_referencing_symbols', {
                    'name_path': 'CapriProfile/Persona', 'relative_path': 'app/CapriProfile.cs',
                    'max_answer_chars': 6000,
                })
                assert 'Brain' in references, references
                return {
                    'server': initialized.server_info.model_dump(), 'tool_count': len(tools),
                    'tools': sorted(tools), 'symbol_lookup': 'PASS', 'references': 'PASS',
                    'overview_characters': len(overview), 'symbol_characters': len(symbol),
                }


async def edit_probe() -> dict:
    fixture = ARTIFACTS / 'serena-edit-probe'
    fixture.mkdir(exist_ok=True)
    config_dir = fixture / '.serena'
    config_dir.mkdir(exist_ok=True)
    (config_dir / 'project.yml').write_text(yaml.safe_dump({
        'project_name': 'capri-serena-edit-probe', 'language_servers': ['csharp'],
        'encoding': 'utf-8', 'read_only': False, 'agent_interface': 'tools',
        'fixed_tools': ['find_symbol', 'replace_symbol_body'],
        'ignore_all_files_in_gitignore': False, 'ignored_paths': ['bin/**', 'obj/**'],
    }), encoding='utf-8')
    project = fixture / 'Probe.csproj'
    project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>', encoding='utf-8')
    source = fixture / 'Probe.cs'
    source.write_text('public static class Probe { public static int Value() { return 1; } }\n', encoding='utf-8')
    restored = subprocess.run([str(ROOT / '.tools/dotnet/dotnet.exe'), 'restore', str(project), '--nologo'],
                              cwd=fixture, capture_output=True, text=True)
    assert restored.returncode == 0, restored.stdout + restored.stderr
    env = dict(os.environ)
    env['SERENA_HOME'] = str(ROOT / '.tools/s')
    params = StdioServerParameters(
        command=str(ROOT / '.tools/venv/Scripts/serena.exe'), env=env, cwd=fixture,
        args=['start-mcp-server', '--context', 'codex', '--agent-interface', 'tools',
              '--project', str(fixture), '--add-mode', 'no-onboarding', '--enable-web-dashboard', 'false',
              '--open-web-dashboard', 'false', '--enable-gui-log-window', 'false'],
    )
    with (ARTIFACTS / 'serena-edit-probe.log').open('w', encoding='utf-8') as log:
        async with stdio_client(params, errlog=log) as streams:
            async with ClientSession(*streams) as session:
                await session.initialize()
                before = await call(session, 'find_symbol', {
                    'name_path_pattern': 'Probe/Value', 'relative_path': 'Probe.cs', 'include_body': True,
                })
                assert 'return 1' in before, before
                await call(session, 'replace_symbol_body', {
                    'name_path': 'Probe/Value', 'relative_path': 'Probe.cs',
                    'body': 'public static int Value() { return 2; }',
                })
                assert 'return 2' in source.read_text(encoding='utf-8')
    built = subprocess.run([str(ROOT / '.tools/dotnet/dotnet.exe'), 'build', str(project), '--no-restore', '--nologo'],
                           cwd=fixture, capture_output=True, text=True)
    assert built.returncode == 0, built.stdout + built.stderr
    return {'symbolic_edit': 'PASS', 'edited_fixture_build': 'PASS', 'path': str(fixture.relative_to(ROOT))}


async def main():
    result = {'project': await inspect_project(), 'isolated_edit': await edit_probe()}
    (ARTIFACTS / 'serena-mcp-test.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    asyncio.run(main())
