# Gen 4 Chinese name tables

Extracted from user-supplied GPLv3 PKHeX forks, preserving array indices and glyphs. Core is unchanged. NUL sentinel entries are excluded.

YYJOY: source ../PKHeX-Gen4-Wokann/ForChinese/Patch/PKHeX.Core/PKM/Strings/StringConverter4Util.cs; SHA-256 d64f93915104ab5c7cae09ba6c15214a3512167cac6e7d03399dd3b6fa2f5132; 6769 slots, 6763 non-terminator mappings.

Xzonn: source ../PKHeX-Gen4-Xzonn/PKHeX.Core/PKM/Strings/StringConverter4Util.cs; SHA-256 645c89c6ab98672cf39d6ce503efeecae74ddfa0ab826d5c5cd754c7f82d70a8; 6815 slots, 6812 non-terminator mappings.

Xzonn declares an inclusive end of 0x1C9B, but its array ends at 0x1C9A. The absent entry is deliberately not invented. Official international glyphs use the existing Core table; Chinese tables replace the overlapping Korean area.
