"""Extract the genuine parser-only enums/DTO from pinned RynthSuite; no fake SDK."""
import sys
from pathlib import Path
root,output=map(Path,sys.argv[1:])
s=(root/'Plugins/RynthCore.Plugin.RynthAi/LegacyUi/LegacyUiSettings.cs').read_text()
s=s[s.index('public enum MetaConditionType'):]
s=s[:s.index('\n}',s.index('public sealed class MetaRule'))+2]
output.parent.mkdir(parents=True,exist_ok=True)
output.write_text('// Unmodified definitions extracted from RynthSuite LegacyUiSettings.cs (MIT).\nusing System.Text.Json.Serialization;\nnamespace RynthCore.Plugin.RynthAi.LegacyUi;\n'+s)
