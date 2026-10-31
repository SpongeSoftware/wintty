#!/usr/bin/env python3
"""Generate winget manifests (version, installer, defaultLocale) for a release."""
import argparse
import pathlib

ARCHES = {"x64": "x64", "arm64": "arm64"}
ID = "wintty.wintty"


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--version", required=True)
    p.add_argument("--repo", required=True, help="owner/name")
    p.add_argument("--sums", required=True)
    p.add_argument("--out", required=True)
    a = p.parse_args()

    sums = {}
    for line in pathlib.Path(a.sums).read_text().splitlines():
        digest, name = line.split(None, 1)
        sums[name.strip().lstrip("*")] = digest.upper()

    out = pathlib.Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    owner, _, name = a.repo.partition("/")
    base = f"https://github.com/{a.repo}/releases/download/v{a.version}"

    (out / f"{ID}.yaml").write_text(
        f"PackageIdentifier: {ID}\nPackageVersion: {a.version}\n"
        "DefaultLocale: en-US\nManifestType: version\nManifestVersion: 1.6.0\n"
    )
    (out / f"{ID}.locale.en-US.yaml").write_text(
        f"PackageIdentifier: {ID}\nPackageVersion: {a.version}\nPackageLocale: en-US\n"
        f"Publisher: wintty contributors\nPackageName: wintty\nLicense: MIT\n"
        f"ShortDescription: Windows terminal emulator built on libghostty-vt\n"
        f"PackageUrl: https://github.com/{a.repo}\n"
        "ManifestType: defaultLocale\nManifestVersion: 1.6.0\n"
    )
    installers = ""
    for arch in ARCHES:
        fname = f"wintty-{a.version}-win-{arch}.zip"
        installers += (
            f"- Architecture: {arch}\n  InstallerType: zip\n  NestedInstallerType: portable\n"
            f"  NestedInstallerFiles:\n  - RelativeFilePath: Wintty.App.exe\n    PortableCommandAlias: wintty\n"
            f"  InstallerUrl: {base}/{fname}\n  InstallerSha256: {sums.get(fname, '')}\n"
        )
    (out / f"{ID}.installer.yaml").write_text(
        f"PackageIdentifier: {ID}\nPackageVersion: {a.version}\nInstallers:\n{installers}"
        "ManifestType: installer\nManifestVersion: 1.6.0\n"
    )


if __name__ == "__main__":
    main()
