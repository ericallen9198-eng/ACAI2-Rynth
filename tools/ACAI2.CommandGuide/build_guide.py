"""Build the user-facing, printable current-Rynth command guide. No game/Bridge calls."""
from pathlib import Path
from xml.sax.saxutils import escape
import argparse
from reportlab.pdfgen import canvas
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, KeepTogether
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.pagesizes import letter

parser=argparse.ArgumentParser()
parser.add_argument('--output',default='artifacts/command-guide/ACAI2-Rynth-Commands.pdf')
parser.add_argument('--fonts',default='/usr/share/fonts/truetype/dejavu')
args=parser.parse_args()
fontdir=Path(args.fonts)
for name,file in [('Guide','DejaVuSans.ttf'),('GuideBold','DejaVuSans-Bold.ttf'),('GuideMono','DejaVuSansMono.ttf')]:
    pdfmetrics.registerFont(TTFont(name,str(fontdir/file)))
pdfmetrics.registerFontFamily('Guide',normal='Guide',bold='GuideBold',italic='Guide',boldItalic='GuideBold')
styles=getSampleStyleSheet()
styles.add(ParagraphStyle('BodyG',fontName='Guide',fontSize=9,leading=12.5,spaceAfter=6,textColor=colors.HexColor('#26383f')))
styles.add(ParagraphStyle('SmallG',parent=styles['BodyG'],fontSize=8,leading=10.8,spaceAfter=4))
styles.add(ParagraphStyle('TitleG',fontName='GuideBold',fontSize=20,leading=24,textColor=colors.HexColor('#09665f'),spaceAfter=10))
styles.add(ParagraphStyle('HeadG',fontName='GuideBold',fontSize=11,leading=14,spaceBefore=8,spaceAfter=5,textColor=colors.HexColor('#09665f')))
styles.add(ParagraphStyle('CmdG',fontName='GuideMono',fontSize=8,leading=11,spaceAfter=0,textColor=colors.HexColor('#173e48')))
styles.add(ParagraphStyle('CellG',parent=styles['BodyG'],fontSize=8.4,leading=11,spaceAfter=0))
styles.add(ParagraphStyle('CellHeadG',parent=styles['CellG'],fontName='GuideBold',textColor=colors.white))
PAGE_W,PAGE_H=letter
WIDTH=PAGE_W-76
story=[]
def p(text,small=False):return Paragraph(text,styles['SmallG' if small else 'BodyG'])
def add(text,small=False):story.append(p(text,small))
def heading(text):story.append(Paragraph(text,styles['HeadG']))
def title(text,sub):story.extend([Paragraph(text,styles['TitleG']),p(sub)])
def table(rows,first=205,headers=('Command','What it does')):
    data=[[Paragraph(escape(t),styles['CellHeadG']) for t in headers]]
    for a,b in rows:
        data.append([Paragraph(escape(a).replace('\n','<br/>'),styles['CmdG']),Paragraph(b,styles['CellG'])])
    t=Table(data,colWidths=[first,WIDTH-first],hAlign='LEFT',repeatRows=1)
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#116b63')),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('TOPPADDING',(0,0),(-1,-1),5),('BOTTOMPADDING',(0,0),(-1,-1),5),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.HexColor('#f0f6f5'),colors.white]),('LINEBELOW',(0,0),(-1,0),.5,colors.HexColor('#116b63')),('LINEBELOW',(0,1),(-1,-1),.25,colors.HexColor('#dce5e4'))]))
    story.extend([t,Spacer(1,6)])
def nextpage():story.append(PageBreak())
def code(text):story.append(Paragraph(escape(text).replace('\n','<br/>'),styles['CmdG']));story.append(Spacer(1,6))

# PAGE 1
TITLE='ACAI2 desktop command guide'
title(TITLE,'Current Rynth modules • November 14, 2026 • Print-friendly US Letter')
add('<b>Versions:</b> Combat 0.1.7 • Threads 0.1.5 • Keyfer adapter 0.3.1 • existing Bridge 0.17.',True)
add('A complete reference to the current modules’ <b>in-game commands</b>, with short instructions. These are the standalone Rynth modules; older Decal command menus are not interchangeable.')
heading('Everyday quick reference')
table([
('/acai2combat setup','Open Combat appearance and display-mode controls.'),
('/acai2combat normal','Show the full combat statistics meter.'),
('/acai2combat compact','Show only the DPS digits in a small HUD.'),
('/acai2combat ultra','Show transparent DPS digits on the floating-text layer.'),
('/acai2threads','Open the Thread journal and Gem Maker.'),
('/acai2r keyfer status','Check Keyfer’s adapter/Bridge connection on Keyfer’s client.'),
('/a !arena Toe vs Samm','Ask Keyfer to host a for-fun Allegiance exhibition.'),
('/ra settings save','Related RynthAi command: save its active settings profile.')])
heading('Move the digits without opening the full meter')
code('/acai2combat ultra\n/acai2combat unlock')
add('Drag the digits, then enter <b>/acai2combat lock</b> for click-through. Wait about <b>3 seconds</b> before closing the game. Settings save automatically per character.')
add('<b>Normal and Compact share the meter position.</b> Ultra-compact has its own saved digit position. The modes switch between displays; the current build does not show the full meter and ultra digits simultaneously.',True)
heading('Where things save')
add('ACAI2 saves its own preferences separately from RynthAi. <b>/ra settings save</b> saves RynthAi; it is not an ACAI2 “save everything” command. Keep the same character and installed module folder when checking a saved layout.',True)
add('To keep this PDF: download it, choose <b>Save as → Desktop</b>, and name it <b>ACAI2 Commands.pdf</b>. Open with Edge or your PDF reader; use <b>Ctrl+P</b> to print.',True)

# PAGE 2
nextpage();title('Combat — all typed commands','Type these directly into the game’s chat input; no /a prefix is needed.')
table([
('/acai2combat\n/acai2combat show','Show the currently selected HUD mode. Does not force Normal mode.'),
('/acai2combat setup','Show the HUD and open Setup.'),
('/acai2combat hide','Hide the meter/digits. Floating combat text and tracking continue; use Setup to disable floating text.'),
('/acai2combat normal','Select the full statistics meter.'),
('/acai2combat compact','Select the small digits-only HUD.'),
('/acai2combat ultra','Select transparent digits on the existing float layer.'),
('/acai2combat unlock','Unlock <b>ultra digits</b> for dragging.'),
('/acai2combat lock','Lock <b>ultra digits</b> and enable click-through.'),
('/acai2combat preview','Toggle the synthetic effects preview; it stops after 30 seconds. Does not change actual damage totals.'),
('/acai2combat new','Reset session totals and the current pack DPS sample. Keeps saved Thread history; switches the meter’s totals to Session scope.'),
('/acai2combat entered','Manually mark entry for an existing tracked Thread when needed. Does not move the character.'),
('/acai2combat outside','Manually pause tracked inside time and finish the current pack.'),
('/acai2combat status','Print version, queue/drop information, Kinetic repeat diagnostics and power-read status.'),
('/acai2combat export','Write a JSON summary of the session and runs. The reported path identifies the export.')])
heading('Panel-only controls and useful behavior')
add('<b>Setup:</b> Normal/Compact/Ultra-compact mode; meter lock; ultra click-through lock; digit size; transparency; colors; anchors; module overrides; and <b>Buffs scale / Debuffs scale / Skills scale</b>. Each group scales its own text and graphics. Kinetic cues use Skills; ordinary damage, crits and healing retain their existing controls.',True)
add('To drag <b>Normal or Compact</b>, turn off <b>Lock meter position</b> in Setup. The slash lock/unlock commands above affect Ultra only. <b>Details</b> holds source/mob breakdowns, big hits, deaths and saved Thread runs.',True)
add('<b>DPS:</b> all modes share per-pack outgoing DPS. Default pack cutoff is 4 seconds without observed combat; the final number holds until the next pack. <b>DOWN on the right</b> means a recognized mob kill, not extra damage. Reckless ON/OFF is based on your recent outgoing tagged hits.',True)

# PAGE 3
nextpage();title('Threads — commands and Gem Maker','The Thread app has its own journal, preferences and attended preparation workflow.')
table([
('/acai2threads\n/acai2threads show','Show the Thread panel.'),
('/acai2threads setup','Show the same Thread panel. Use its Settings button for position lock/background.'),
('/acai2threads hide','Hide the panel; the journal/prep controller remain active.'),
('/acai2threads status','Print the plugin version, preparation state and current notice.'),
('/acai2threads stop','Stop a pending/active gem batch. An outstanding request may still finish; uncertain batches stay stopped for review.'),
('/acai2threads export','Write local recipe/outcome/run records to a JSON export. The panel reports the path.')])
heading('Buttons in the panel — not slash commands')
table([
('Gem Maker → START BATCH','Begin the displayed manual or random plan. Pauses RynthAi automatically using its existing interfaces.'),
('Gem Maker → STOP BATCH','Same purpose as the typed stop command.'),
('Current Run → Mark entered / outside','Manual run-timing corrections; do not move the character.'),
('Link latest completed press','Attach a press to the current run manually. The link is labeled as user-supplied, not verified gem identity.'),
('History / Show live run','Review an earlier run or return to the live view.'),
('Easy / Tough / Nightmare','Rate the linked recipe/run. Link a press first.'),
('Research → Goal','Select lum, xp, loot, easy, hard or the shown salvage goals. Recommendations use observed evidence.'),
('Settings → Export local records','Same export workflow; Settings also controls position lock and background.')],first=205,headers=('Panel control','Short instruction'))
heading('Make random gems')
add('1. Stand in the local vendor/press area in peace mode. Open <b>Gem Maker</b>.<br/>2. Enable <b>Random recipe per gem</b>; choose blank level, fixed scarab and gem count.<br/>3. Enable <b>Buy missing supplies</b> to draw from recognized vendor stock plus carried components; off uses only your pack.<br/>4. Set the purchase cap and optional <b>Auto-withdraw notes when needed</b>, then start.')
add('Each gem gets fresh component choices and all nine doses: scarab + herb + powder + reagent + talisman + four of one taper color. Recipes are frozen before action and saved with confirmed outcomes. Repeats are possible; no outcome is guaranteed. Manual choices remain saved. Every result is kept.',True)
add('<b>Important:</b> entered/outside, linking, rating and Research goals above are <b>panel actions</b>. The current chat handler does not expose /acai2threads entered, link, rate or goal commands.',True)

# PAGE 4
nextpage();title('Keyfer — local operator commands','Use these on the configured Keyfer character’s client. The Bridge must be running.')
table([
('/acai2r status','Show adapter/session status.'),
('/acai2r export','Export a local observation snapshot when available. This is not a save-all command.'),
('/acai2r ui','Legacy placeholder: reports Combat UI is excluded from the Keyfer package. Use /acai2combat setup for the standalone meter.'),
('/acai2r keyfer\n/acai2r keyfer status','Show Keyfer controls and Bridge/connection status.'),
('/acai2r keyfer reload','Reload keyfer.json and reconnect/restart the scoped responder as needed.'),
('/acai2r keyfer quiet on\n/acai2r keyfer quiet off','Enable/disable local public quiet mode. Private replies can continue.'),
('/acai2r keyfer emotes on\n/acai2r keyfer emotes off','Enable/disable conversational emotes. Existing public-only/cooldown rules still apply.'),
('/acai2r keyfer treasure','Show local treasure-service status.'),
('/acai2r keyfer standings','Show observed treasure standings through the treasure service.')],first=217)
heading('Chat with Keyfer')
add('Tell <b>Keyfer Sutherland</b> normally, or address him in Allegiance/General: <b>“Keyfer, who is leading the wave leaderboard?”</b> Short follow-ups such as <b>“tell me more”</b> can continue a recent conversation. Wiki lookups and treasure questions use normal conversation, not extra slash commands.',True)
heading('Leaderboard topics Keyfer recognizes')
add('Ask an addressed question about any of these topics. The <b>/top</b> commands themselves belong to <b>DreamWeave</b>, not ACAI2.',True)
table([
('/top dps  •  /top defense','DPS trial and survival trial.'),
('/top wave  •  /top speed','Wave gauntlet and current speed trial.'),
('/top speed winners','Recent speed-trial season winners.'),
('/top level  •  /top bank','Character level and bank standings.'),
('/top cap  •  /top stamp','Lifetime class ability points and quest stamps.'),
('/top thread  •  /top knot','Thread-Guide and best Knot.'),
('/top deaths  •  /top title','Death counts and titles.'),
('/top 1v1  •  /top 2v2  •  /top ffa','PvP ladders.'),
('/top tugak  •  /top bg','Additional PvP ladder categories.')],first=245)

# PAGE 5
nextpage();title('Keyfer — arena and chat controls','These are messages sent to Keyfer through chat, not local /acai2r commands.')
table([
('!arena\n!arena help','Show arena usage.'),
('!arena <First Name> vs <Second Name>','Request a public exhibition in an enabled public channel. Multiple-word names need no special quoting.'),
('!arena preview <First Name> vs <Second Name>','Request private results. A normal match request sent by tell is also private.'),
('!arena status','Show channel enablement and whether a public exhibition is active.'),
('!arena stats\n!arena stats <name>','Show your arena record, or a named player’s record.'),
('!arena build <name>','Show purchased abilities loaded from that player’s published sheet.'),
('!arena refresh <name>','Reload the named player’s published sheet/build.'),
('!arena cancel','Cancel pending public commentary. Restricted to Samm or Toe; does not remove the General cooldown.'),
('!sheet <published-sheet-URL>\n!arena sheet <published-sheet-URL>','Register your own published character sheet. Requires a supported char.acdreamweave.com HTTPS sheet URL.'),
('!duel <First Name> vs <Second Name>','Alias for the arena command family. “Keyfer, arena …” is another addressed form.')],first=245)
heading('Ready-to-type examples')
code('/a !arena Lawful Waffle vs Samm\n/t Keyfer Sutherland, !arena preview Toe vs Samm\n/t Keyfer Sutherland, !arena build Kewne')
add('Arena is a <b>for-fun fictional exhibition</b>, not real combat simulation. General hosting follows the existing Bridge configuration; <b>!arena status</b> reports it. General cooldowns and short anti-spam/request throttles still apply. Allegiance does not use the General between-match cooldown.',True)
heading('Natural-language quiet controls')
add('<b>“Keyfer, quiet down”</b> and <b>“Keyfer, talk again”</b> are the simplest forms. For <b>Samm/Toe</b>, these control <b>General replies</b> while allowing Allegiance/tells. On Keyfer’s client, the explicit <b>/acai2r keyfer quiet on/off</b> controls public quiet mode separately.',True)
add('Other accepted quiet phrases after addressing Keyfer: <i>be quiet, hush, stop chiming in, stop jumping in, chill, chill out, quit talking, stop talking, give it a rest, pipe down</i>. Resume phrases: <i>resume, wake up, speak up, talk again, you can talk again, start talking again, quit being quiet, stop being quiet</i>. Existing owner/exclusion/channel permissions apply; non-owner behavior can differ.',True)

# PAGE 6
nextpage();title('Saving, related commands and help','Rynth/DreamWeave commands below are separate from ACAI2.')
table([
('/ra settings save','RynthAi: immediately save the active settings profile, including ranges and supported profile-backed UI preferences.'),
('/ra settings savechar Samm','RynthAi: save current settings as a named profile. Replace Samm with your chosen profile name.'),
('/ra settings loadchar Samm','RynthAi: load that named profile. Current RynthAi may create missing names from current settings.'),
('/ra help','RynthAi’s own help; use for its macro/navigation/combat commands.'),
('/rv plates numbers off\n/rv plates numbers on','RynthVision: hide/restore its floating combat numbers. Turning them off avoids duplicate numbers alongside ACAI2.'),
('/rv plates gains on\n/rv plates gains off','RynthVision: keep/hide its floating XP/Luminance gains independently of combat numbers.'),
('/b w n 2','DreamWeave bank: withdraw two 250,000-pyreal trade notes. This actually withdraws notes; it is not an ACAI2 setting.')],first=217)
heading('Your settings and local files')
table([
('%LOCALAPPDATA%\\ACAI2-Combat','Per-character appearance in ui.json; compact Thread/run records and exports under the character’s hash-named folder.'),
('%LOCALAPPDATA%\\ACAI2-Threads','Per-character ui.json (including manual/random plan), prep.json checkpoint, recipe/run journal and exports.'),
('%LOCALAPPDATA%\\ACAI2-Rynth','Keyfer adapter files, including keyfer.json. The existing Bridge retains its own configuration, memories and service data.')],first=220,headers=('Folder (paste into File Explorer)','What to find'))
add('Combat/Threads preferences save automatically. After moving/changing controls, allow about <b>3 seconds</b> before closing. Use the same character to check the same preferences. RynthAi has separate profile autosaving; its engine/global and RynthVision UI controls can have their own persistence.',True)
heading('If something does not respond')
add('Use the module’s <b>status</b> command, check it is enabled in RynthCore, and confirm the installed version. Keyfer additionally needs the configured character and Bridge connection. A closed panel does not necessarily mean its background tracking stopped. There is no universal ACAI2 save/reset/stop command.',True)
heading('Verification and source references')
add('ACAI2 commands checked against repository revision <b>e4ecf24</b>; player arena syntax checked against the existing Bridge v0.17 reference. These pages document current in-game commands and panel controls, not HTTP endpoints, developer CLI flags or retired Decal modules.',True)
add('<link href="https://github.com/ericallen9198-eng/ACAI2-Rynth/tree/e4ecf247765cea822f7cd66986214267d92f4037" color="#09665f">ACAI2-Rynth source and setup guides</link> • <link href="https://github.com/tombohar/RynthSuite" color="#09665f">RynthSuite source/help</link>. Rynth helper syntax was checked against its public main branch; installed builds may differ.',True)
add('Fonts: embedded DejaVu Sans subsets. <link href="https://dejavu-fonts.github.io/License.html" color="#09665f">DejaVu font license</link>. This guide contains no API keys, private logs or personal configuration.',True)

out=Path(args.output);out.parent.mkdir(parents=True,exist_ok=True)
def page(canvas,doc):
    canvas.saveState();canvas.setStrokeColor(colors.HexColor('#cbdad7'));canvas.setLineWidth(.5);canvas.line(38,35,PAGE_W-38,35)
    canvas.setFont('Guide',7.3);canvas.setFillColor(colors.HexColor('#50656b'))
    canvas.drawString(38,23,'ACAI2 • Rynth command guide • Nov 14, 2026')
    canvas.drawRightString(PAGE_W-38,23,f'Page {doc.page}')
    canvas.restoreState()
doc=SimpleDocTemplate(str(out),pagesize=letter,rightMargin=38,leftMargin=38,topMargin=36,bottomMargin=45,title=TITLE,author='ACAI2 project',subject='Current in-game command reference: Combat, Threads and Keyfer')
doc.build(story,onFirstPage=page,onLaterPages=page)
print(out)
