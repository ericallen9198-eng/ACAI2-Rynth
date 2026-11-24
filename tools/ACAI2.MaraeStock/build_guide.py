"""Build the novice guide and PDF from the same short instructions."""
from pathlib import Path
import argparse,json,html
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.styles import getSampleStyleSheet,ParagraphStyle
from reportlab.lib.pagesizes import letter
from reportlab.platypus import SimpleDocTemplate,Paragraph,Spacer,Table,TableStyle,KeepTogether
root=Path(__file__).resolve().parents[2]
spec=json.loads((root/'packaging/marae-stock/guide.json').read_text())
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--docs',type=Path);args=p.parse_args()
args.output.mkdir(parents=True,exist_ok=True)
lines=[spec['title'],f"v{spec['version']}",'',spec['intro'],'','BEFORE YOU START']+['- '+s for s in spec['before']]+['','INSTALL AND START']
lines += [f'{i}. {title}: {body}' for i,(title,body) in enumerate(spec['steps'],1)]
lines += ['','PICK YOUR OPTIONS']+[f'{label}: {body}' for label,body in spec['options']]+['','QUICK HELP']+['- '+s for s in spec['tips']]+['','CREDIT',spec['credit'],'','No earlier treasure-hunting installation, Lua setup, nav copying or RynthAi DLL replacement is needed.']
text='\n'.join(lines)+'\n';(args.output/'README.txt').write_text(text,encoding='utf-8')
if args.docs:
 md=['# '+spec['title'],f"Version {spec['version']}",'',spec['intro'],'','## Before you start','']+['- '+s for s in spec['before']]+['','## Install and start','']
 md += [f'{i}. **{title}.** {body}' for i,(title,body) in enumerate(spec['steps'],1)]
 md += ['','## Pick your options','','| Option | What it does |','| --- | --- |']+[f'| {label} | {body} |' for label,body in spec['options']]+['','## Quick help','']+['- '+s for s in spec['tips']]+['','## Credit','',spec['credit'],'','No earlier treasure-hunting installation, Lua setup, nav copying or RynthAi DLL replacement is needed.','', 'Tested with unmodified RynthSuite 2026.11.23.3 and genuine Rynth SDK APIs. Live server behavior still needs a first-run check.']
 args.docs.write_text('\n'.join(md)+'\n',encoding='utf-8')
styles=getSampleStyleSheet()
styles.add(ParagraphStyle('BodyM',fontName='Helvetica',fontSize=10.5,leading=13.2,spaceAfter=4,textColor=colors.HexColor('#23323b')))
styles.add(ParagraphStyle('HeadM',fontName='Helvetica-Bold',fontSize=10.5,leading=13,spaceBefore=7,spaceAfter=5,textColor=colors.HexColor('#08766f')))
styles.add(ParagraphStyle('SmallM',parent=styles['BodyM'],fontSize=9.2,leading=11.3))
def para(t,style='BodyM'):return Paragraph(html.escape(t),styles[style])
story=[Paragraph('MARAE LASSEL',ParagraphStyle('TitleM',fontName='Helvetica-Bold',fontSize=23,leading=27,textColor=colors.HexColor('#073b44'))),para('QUICK START  |  v'+spec['version']),para(spec['intro']),para('Nav paths & original mission logic: Soot (xSoot / SootAC).','SmallM')]
story.append(para('Before you start','HeadM'));story += [para(s) for s in spec['before']]
story.append(para('Install and start','HeadM'))
for i,(title,body) in enumerate(spec['steps'],1):story.append(Paragraph(f'<b>{i}. {html.escape(title)}.</b> '+html.escape(body),styles['BodyM']))
story.append(para('Pick your options','HeadM'))
table=Table([[para(label),para(body)] for label,body in spec['options']],colWidths=[107,421],hAlign='LEFT')
table.setStyle(TableStyle([('VALIGN',(0,0),(-1,-1),'TOP'),('BACKGROUND',(0,0),(0,-1),colors.HexColor('#e8f4f2')),('LINEBELOW',(0,0),(-1,-1),.35,colors.HexColor('#d9e5e5')),('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),4),('BOTTOMPADDING',(0,0),(-1,-1),3)]));story.append(table)
story.append(para('Quick help','HeadM'));story += [para(s,'SmallM') for s in spec['tips']]
story += [Spacer(1,3),para(spec['credit'],'SmallM')]
def footer(canvas,doc):
 canvas.setStrokeColor(colors.HexColor('#08766f'));canvas.line(42,30,570,30);canvas.setFont('Helvetica',7);canvas.setFillColor(colors.HexColor('#52656b'));canvas.drawString(42,19,'DreamWeave allegiance guide | Normal RynthAi DLL | /acai2marae opens the app');canvas.drawRightString(570,19,str(doc.page))
pdf=args.output/'Marae-Quick-Start.pdf'
doc=SimpleDocTemplate(str(pdf),pagesize=letter,rightMargin=42,leftMargin=42,topMargin=31,bottomMargin=39,title='Marae Lassel Quick Start',author='ACAI2 - original paths and mission logic by Soot')
doc.build(story,onFirstPage=footer,onLaterPages=footer)
assert doc.page==1, 'Keep the novice guide to one readable page.'
print('Generated README.txt and Marae-Quick-Start.pdf from the shared novice guide.')
