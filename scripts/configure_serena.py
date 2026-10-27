"""Keep project-local Serena server exposure aligned with the client allowlist."""
import sys
from pathlib import Path

import yaml

root, settings_path = map(Path, sys.argv[1:])
project = yaml.safe_load((root / '.serena/project.yml').read_text(encoding='utf-8'))
settings = yaml.safe_load(settings_path.read_text(encoding='utf-8'))
# In Serena's switchable `codex` context, project-only restrictions affect active
# tools but not tools/list. Apply the same fixed set to its repository-local home.
settings['fixed_tools'] = project['fixed_tools']
settings['excluded_tools'] = []
settings['included_optional_tools'] = []
settings['trusted_project_path_patterns'] = [str(root.resolve())]
settings_path.write_text(yaml.safe_dump(settings, sort_keys=False), encoding='utf-8')
