# 11 Splits

Status: todo

**Goal:** recursive split panes inside a tab.

**Scope:** `SplitNode` tree (horizontal/vertical, ratio), split/close/focus navigation/resize with keyboard and mouse dividers, zoom pane, equalize, per-pane working directory inheritance.

**Acceptance:** unit tests for tree operations (split, close collapses parent, focus traversal); UI handles 8 panes; resize doesn't break ConPTY sessions.

**Files:** `src/Wintty.App/Splits/*`, `tests/`.
