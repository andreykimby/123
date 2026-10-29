#!/usr/bin/env python3
"""Собирает брошюру: book.pdf (A5, постранично) и book-print.pdf (A4, 2 страницы на листе, готово к двусторонней печати).

Нужны: pandoc, chromium (Playwright), pypdf.
"""
import os
import subprocess
import sys
from pathlib import Path

from pypdf import PageObject, PdfReader, PdfWriter, Transformation

HERE = Path(__file__).resolve().parent
CHROME = os.environ.get("CHROME", "/opt/pw-browsers/chromium")

A4_W, A4_H = 595.276, 841.890
A5_W, A5_H = 419.528, 595.276


def build_html():
    body = subprocess.run(
        ["pandoc", "-f", "markdown", "-t", "html5", str(HERE / "book.md")],
        check=True, capture_output=True, text=True,
    ).stdout
    cover = (
        '<section class="cover"><div><h1>Skarynagram</h1>'
        '<div class="sub">HashtagPrinter<br>Руководство по настройке и работе</div></div>'
        '<div class="foot">Установка · Авторизация · Настройка · Клиентское окно</div></section>'
    )
    notes = '<section class="notes"><h2>Заметки</h2><div class="lines"></div></section>'
    html = (
        '<!doctype html><html lang="ru"><head><meta charset="utf-8">'
        '<title>Skarynagram — руководство</title>'
        '<link rel="stylesheet" href="book.css"></head><body>'
        f"{cover}{body}{notes}</body></html>"
    )
    out = HERE / "book.html"
    out.write_text(html, encoding="utf-8")
    return out


def render_pdf(html, pdf):
    subprocess.run(
        [CHROME, "--headless", "--no-sandbox", "--disable-gpu", "--no-pdf-header-footer",
         "--allow-file-access-from-files", f"--print-to-pdf={pdf}", html.as_uri()],
        check=True, capture_output=True,
    )


def pad_to_four(reader):
    pages = list(reader.pages)
    n = len(pages)
    total = (n + 3) // 4 * 4
    # недостающие страницы — пустые, в конце (внутренняя сторона задней обложки)
    return pages + [PageObject.create_blank_page(width=A5_W, height=A5_H) for _ in range(total - n)]


def place(sheet, page, slot_top, cw):
    """Кладёт A5-страницу повёрнутой на 90° в верхнюю/нижнюю половину портретного A4."""
    y0 = A4_H - A5_W if slot_top else 0
    if cw:  # верх страницы -> вправо
        t = Transformation().rotate(-90).translate(0, A5_W)
    else:   # верх страницы -> влево
        t = Transformation().rotate(90).translate(A5_H, 0)
    t = t.translate(0, y0)
    sheet.merge_transformed_page(page, t)


def impose(pages):
    n = len(pages)
    w = PdfWriter()
    for k in range(n // 4):
        front = PageObject.create_blank_page(width=A4_W, height=A4_H)
        back = PageObject.create_blank_page(width=A4_W, height=A4_H)
        place(front, pages[n - 1 - 2 * k], True, True)
        place(front, pages[2 * k], False, True)
        place(back, pages[n - 2 - 2 * k], True, False)
        place(back, pages[2 * k + 1], False, False)
        w.add_page(front)
        w.add_page(back)
    return w


def main():
    html = build_html()
    book = HERE / "book.pdf"
    render_pdf(html, book)
    pages = pad_to_four(PdfReader(str(book)))
    imposed = impose(pages)
    with open(HERE / "book-print.pdf", "wb") as f:
        imposed.write(f)
    print(f"страниц в книге: {len(pages)}, листов A4: {len(pages)//4}")


if __name__ == "__main__":
    sys.exit(main())
