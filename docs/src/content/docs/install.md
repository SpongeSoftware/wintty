---
title: Install
description: Install wintty on Windows.
---

wintty is distributed as a portable zip for Windows 11 (x64; arm64 is planned but not yet supported).

1. Download the zip for your architecture from the [Download](../download/) page.
2. Verify it against `SHA256SUMS.txt` from the same release.
3. Extract anywhere and run `Wintty.App.exe`.

Each release also ships SPDX and CycloneDX SBOMs and a winget manifest. Additional distribution channels (winget repository, others) will be announced later.

## Icons and symbols

Programs such as Neovim, `eza` and prompt themes draw file-type icons and Powerline separators using private-use characters that ordinary fonts do not contain. wintty draws those with an installed [Nerd Font](https://www.nerdfonts.com): it looks for a Mono-width variant (for example `FiraCode NFM`, `CaskaydiaCove NFM`, `JetBrainsMono NFM` or `Symbols Nerd Font Mono`) and falls back to blank boxes when none is installed. Install one and restart wintty.

## Colours

wintty honours `NO_COLOR`. If it is set in the environment wintty was started from, shells and prompt themes will turn colours off.
