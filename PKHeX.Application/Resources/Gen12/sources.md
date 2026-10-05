# Gen 1/2 translated name tables

- `charmap_cn.asm`: user-provided shared US-based translation table; invalid 啰 removed earlier.
- `KoreanChinese.txt`: UTF-8 transcription of user-provided `韩版汉化码表.txt` (UTF-16 LE). All 2,330 pairs preserved, with no additions or normalization. Four-digit hex means two bytes in written order, not a little-endian integer.
- `CknGb2312.txt`: all 7,445 assigned GB2312 double-byte characters (6,763 Hanzi and 682 other characters), generated with Python's strict `gb2312` decoder. Cross-checked against the user's `CKNSaveConverter-main/convert.py` using AST literal parsing only (never executed). For assigned GB2312 codes, use the converter's character labels where present, including A1A4=·, A1AA=— and A3B0–A3B9=ASCII digits. The converter omits A3AB, retained as standard fullwidth ＋. Its private-use placeholders, non-GB2312 extensions and truncated GB18030-looking keys 8135/8431 are not evidence of supported GB2312 glyphs and are excluded. Prefixes A1–F7 reserve both Hanzi and non-Hanzi pages; original English single-byte codes outside this range remain available. A CKN-specific ROM/save fixture was not supplied.

Selecting a table changes interpretation only. No region inference or implicit transcoding. Unknown double-byte entries are rejected for edits; unchanged buffers are preserved.
