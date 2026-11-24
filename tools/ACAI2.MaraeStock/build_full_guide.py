"""Render the full player tutorial with the user-supplied A2 branding.

Usage: python tools/ACAI2.MaraeStock/build_full_guide.py --output <file.pdf>
Requires reportlab. The Markdown remains suitable for sharing on Discord.
"""
from pathlib import Path
import argparse
import html
import re
from reportlab.lib import colors
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak,
    Image,
)
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

ROOT = Path(__file__).resolve().parents[2]
NAVY = colors.HexColor('#10102C')
PURPLE = colors.HexColor('#7430C8')
PINK = colors.HexColor('#C51B85')
CYAN = colors.HexColor('#20DAF1')
INK = colors.HexColor('#24243E')


def register_fonts():
    """Use embedded fonts for consistent rendering and copyable commands."""
    candidates = [Path('/usr/share/fonts/truetype/dejavu'), ROOT / 'artifacts/fonts']
    folder = next((p for p in candidates if (p / 'DejaVuSans.ttf').exists()), None)
    if folder is None:
        raise SystemExit('Install fonts-dejavu-core, or put DejaVuSans fonts in artifacts/fonts.')
    for name, filename in [('Guide', 'DejaVuSans.ttf'), ('GuideBold', 'DejaVuSans-Bold.ttf'),
                           ('GuideMono', 'DejaVuSansMono.ttf')]:
        pdfmetrics.registerFont(TTFont(name, str(folder / filename)))
    pdfmetrics.registerFontFamily('Guide', normal='Guide', bold='GuideBold', italic='Guide', boldItalic='GuideBold')


def markup(text):
    text = html.escape(text, quote=True)
    text = re.sub(r'\[([^\]]+)\]\((https://[^\s)]+)\)',
                  r'<link href="\2" color="#7430C8"><u>\1</u></link>', text)
    text = re.sub(r'\*\*(.*?)\*\*', r'<b>\1</b>', text)
    text = re.sub(r'`(.*?)`', r'<font name="GuideMono" size="9">\1</font>', text)
    return text


def build(output):
    register_fonts()
    styles = {
        'body': ParagraphStyle('body', fontName='Guide', fontSize=10.1, leading=13.6, textColor=INK, spaceAfter=6),
        'title': ParagraphStyle('title', fontName='GuideBold', fontSize=23.5, leading=28, textColor=NAVY, spaceAfter=15),
        'head': ParagraphStyle('head', fontName='GuideBold', fontSize=12.2, leading=16, textColor=PURPLE, spaceBefore=6, spaceAfter=4, keepWithNext=True),
        'list': ParagraphStyle('list', fontName='Guide', fontSize=10.1, leading=13.6, textColor=INK, leftIndent=15, firstLineIndent=-15, spaceAfter=5),
        'small': ParagraphStyle('small', fontName='Guide', fontSize=9, leading=12.5, textColor=INK, spaceAfter=5),
        'code': ParagraphStyle('code', fontName='GuideMono', fontSize=8.6, leading=12.5, textColor=NAVY, splitLongWords=True),
    }
    def p(text, style='body'):
        return Paragraph(markup(text), styles[style])
    def box(text, code=False):
        # Long diagnostics wrap at argument boundaries. The Markdown source
        # retains one-line commands for copying into chat.
        if code:
            safe = html.escape(text)
            if len(text) > 100:
                safe = safe.replace('getvar[ml_zone],', 'getvar[ml_zone],<br/>')
                safe = safe.replace('getvar[mm_watch_active],', 'getvar[mm_watch_active],<br/>')
            para = Paragraph(safe, styles['code'])
        else:
            para = p(text, 'small')
        t = Table([[para]], colWidths=[516])
        t.setStyle(TableStyle([
            ('BACKGROUND', (0,0), (-1,-1), colors.HexColor('#F2EDFF' if code else '#EDF9FD')),
            ('LINEBEFORE', (0,0), (0,-1), 3, PURPLE if code else colors.HexColor('#00BBD8')),
            ('LEFTPADDING', (0,0), (-1,-1), 11), ('RIGHTPADDING',(0,0),(-1,-1),11),
            ('TOPPADDING',(0,0),(-1,-1),9),('BOTTOMPADDING',(0,0),(-1,-1),9),
        ]))
        return [t, Spacer(1,9)]
    source = (ROOT / 'docs/MARAE-FULL-GUIDE.md').read_text(encoding='utf-8')
    pages = source.split('<!-- page -->')
    story = []
    for page_num, page in enumerate(pages):
        if page_num:
            story.append(PageBreak())
        else:
            # The original supplied artwork is embedded unmodified.
            logo = Image(str(ROOT / 'docs/assets/a2-logo.png'), width=142, height=142)
            logo.hAlign = 'CENTER'
            story += [logo, Spacer(1,10)]
        lines = page.strip().splitlines()
        i = 0
        while i < len(lines):
            line = lines[i].strip()
            if not line:
                i += 1
                continue
            if line.startswith('```'):
                block = []; i += 1
                while i < len(lines) and not lines[i].startswith('```'):
                    block.append(lines[i]); i += 1
                story += box('\n'.join(block), True); i += 1
                continue
            if line.startswith('|'):
                rows = []
                while i < len(lines) and lines[i].strip().startswith('|'):
                    cells = [x.strip() for x in lines[i].strip().strip('|').split('|')]
                    if not all(re.fullmatch(r'[-: ]+', c) for c in cells):
                        rows.append([p(c, 'small') for c in cells])
                    i += 1
                widths = [110,406] if len(rows[0]) == 2 else [172,172,172]
                table = Table(rows, colWidths=widths, repeatRows=1)
                table.setStyle(TableStyle([
                    ('VALIGN',(0,0),(-1,-1),'TOP'),
                    ('BACKGROUND',(0,0),(-1,0),colors.HexColor('#E9DDFD')),
                    ('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.HexColor('#F8F5FF'),colors.HexColor('#EFFBFE')]),
                    ('LINEBELOW',(0,0),(-1,0),1.3,PURPLE),
                    ('LEFTPADDING',(0,0),(-1,-1),9),('RIGHTPADDING',(0,0),(-1,-1),9),
                    ('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),6),
                ]))
                story += [table, Spacer(1,10)]
                continue
            if line.startswith('# '):
                story.append(p(line[2:], 'title'))
            elif line.startswith('## '):
                story.append(p(line[3:], 'head'))
            elif line.startswith('> '):
                story += box(line[2:])
            elif line.startswith('- '):
                story.append(p('•  ' + line[2:], 'list'))
            elif re.match(r'^\d+\. ', line):
                story.append(p(line, 'list'))
            else:
                block = [line]
                while i + 1 < len(lines) and lines[i+1].strip() and not re.match(r'^(#|\||>|```|- |\d+\. )',lines[i+1]):
                    i += 1; block.append(lines[i].strip())
                story.append(p(' '.join(block)))
            i += 1
    def chrome(canvas, doc):
        canvas.saveState()
        canvas.setFillColor(NAVY); canvas.rect(0,750,612,42,fill=1,stroke=0)
        canvas.setFillColor(CYAN); canvas.setFont('GuideBold',9)
        canvas.drawString(48,766,'ACAI2  /  DREAMWEAVE')
        canvas.setFillColor(colors.white); canvas.setFont('Guide',8)
        canvas.drawRightString(564,766,'MARAE LASSEL • PLAYER GUIDE')
        canvas.setFillColor(PINK); canvas.rect(0,746,153,4,fill=1,stroke=0)
        canvas.setFillColor(PURPLE); canvas.rect(153,746,306,4,fill=1,stroke=0)
        canvas.setFillColor(CYAN); canvas.rect(459,746,153,4,fill=1,stroke=0)
        canvas.setStrokeColor(PURPLE);canvas.setLineWidth(.6);canvas.line(48,36,564,36)
        canvas.setFillColor(INK);canvas.setFont('Guide',7)
        canvas.drawString(48,23,'v1.17.0  •  Routes & original mission logic: Soot (xSoot / SootAC)')
        canvas.setFont('GuideBold',8);canvas.setFillColor(PURPLE)
        canvas.drawRightString(564,23,f'{doc.page:02}')
        canvas.restoreState()
    output.parent.mkdir(parents=True,exist_ok=True)
    doc = SimpleDocTemplate(str(output), pagesize=letter, leftMargin=48,rightMargin=48,
                            topMargin=63,bottomMargin=50,
                            title='Marae Lassel — Complete Player Guide v1.17.0',
                            author='ACAI2; original mission paths and logic by Soot',
                            subject='DreamWeave tasks, hunting, treasure maps, installation and recovery')
    doc.build(story,onFirstPage=chrome,onLaterPages=chrome)
    if doc.page != len(pages):
        raise SystemExit(f'Expected {len(pages)} planned pages, got {doc.page}; fix overflow before release.')
    print(f'Built {output} ({doc.page} pages)')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, type=Path)
    build(parser.parse_args().output)
