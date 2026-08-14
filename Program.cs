using Ratatui;

using var term = new Terminal();
term.Clear();

using var para = new Paragraph("Hello from Ratatui.cs!")
    .Title("YT Music TUI");

term.Draw(para, new Rect(2, 1, 44, 6));