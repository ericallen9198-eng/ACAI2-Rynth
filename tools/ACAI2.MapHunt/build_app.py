"""Keep the native app on exactly the tested AF workflow, changing only a player-facing helper message."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
s=(root/'metas/maphunt/MetaFiles/MapHunt.af').read_text()
s=s.replace('MapHunt helper or route was not confirmed. Start the Lua helper and check maphunt.nav in NavProfiles, then Resume.','The MapHunt app or route was not confirmed. Open the app and check maphunt.nav in NavProfiles, then Resume.')
(root/'metas/maphunt-app/MapHunt App.af').write_text(s)
print('Built app resource from the verified MapHunt AF; state logic unchanged.')
