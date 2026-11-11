#!/usr/bin/env python3
"""Counts the Korean string literals in the game's code and sheets, per file, for the string table plan
(Docs/Strings.md, the research report's candidate 17). Prints a Markdown table, largest first.

    python3 DevTools/Strings/count-korean.py
"""
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[2]
HANGUL = re.compile(r'[가-힣]')
LITERAL = re.compile(r'\$?@?"(?:[^"\\\n]|\\.)*"')


def code_counts():
    for path in sorted((ROOT / 'Assets/_Project/Scripts').rglob('*.cs')):
        text = path.read_text(encoding='utf-8')
        # Comments are not shown to players.
        text = re.sub(r'//[^\n]*', '', text)
        n = sum(1 for m in LITERAL.finditer(text) if HANGUL.search(m.group(0)))
        if n:
            yield path.relative_to(ROOT).as_posix(), n


def sheet_counts():
    for path in sorted((ROOT / 'Assets/_Project/Data').rglob('*.csv')):
        n = sum(len(HANGUL.findall(line)) > 0 for line in path.read_text(encoding='utf-8').splitlines()[1:])
        if n:
            yield path.relative_to(ROOT).as_posix(), n


def main():
    code = sorted(code_counts(), key=lambda p: -p[1])
    sheets = sorted(sheet_counts(), key=lambda p: -p[1])
    print(f'코드의 한글 문자열 {sum(n for _, n in code)}개, 시트의 한글이 든 줄 {sum(n for _, n in sheets)}개\n')
    print('| 코드 파일 | 한글 문자열 |\n|---|---|')
    for name, n in code:
        print(f'| `{name}` | {n} |')
    print('\n| 시트 | 한글이 든 줄 |\n|---|---|')
    for name, n in sheets:
        print(f'| `{name}` | {n} |')


if __name__ == '__main__':
    main()
