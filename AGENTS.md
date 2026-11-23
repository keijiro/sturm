## Where the reasoning lives

Written reasoning in this codebase is split three ways. Put each piece in one place only.

- **Code comments** say why the code is shaped the way it is. They are the larger and
  authoritative body: file headers state what a thing is for, what was tried, and what
  was measured. If you change what a comment describes, update it in the same edit.
- **`Docs/`** holds what spans files or has no file: the specification, cross-cutting
  rules, format obligations, rejected designs. The `impl-*.md` notes are maps that point
  at the file holding each argument.
- **Commit messages** say why the change happened at all: what the tree was like before,
  designs refused that left no code behind, measurements that decided the change, outside
  bugs with ticket numbers.

Do not restate a comment in a doc or in a commit body.

## Style

Follow the surrounding code. Comment prose has a deliberate voice — match it rather than
flattening it to summaries.

Commit messages: a subject line in the imperative, no full stop, under 72 characters (aim
at 50); a blank line; then the reason, wrapped at 72 columns. Let the body follow the size
of the diff, not the effort — if the argument already lives in a comment the commit adds,
the body is just a few lines of what changed.
