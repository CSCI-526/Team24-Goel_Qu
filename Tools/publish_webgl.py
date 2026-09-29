#!/usr/bin/env python3
"""Publish a completed Unity WebGL build to the repository's gh-pages branch."""

import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tempfile


def git(directory, *args, capture=False):
    result = subprocess.run(
        ["git", "-C", str(directory), *args], check=True, text=True,
        stdout=subprocess.PIPE if capture else None,
    )
    return result.stdout.strip() if capture else None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("build", nargs="?", default="Builds/WebGL")
    args = parser.parse_args()
    project = Path(__file__).resolve().parent.parent
    build = Path(args.build).resolve()
    if not (build / "index.html").is_file() or not (build / "Build").is_dir():
        parser.error("Expected a completed Unity WebGL build with index.html and Build/.")
    info = build / "build-info.json"
    if not info.is_file():
        parser.error("No successful build marker. Run Ball Game > Build WebGL before publishing.")
    files = [p for p in build.rglob("*") if p.is_file()]
    if any(p.is_symlink() for p in build.rglob("*")):
        parser.error("The build must not contain symbolic links.")
    if any(".git" in p.relative_to(build).parts for p in files):
        parser.error("The build must not contain a Git repository.")
    if any(p.stat().st_size >= 100 * 1024 * 1024 for p in files):
        parser.error("A build file exceeds GitHub's 100 MiB Git file limit.")
    if sum(p.stat().st_size for p in files) >= 1024 * 1024 * 1024:
        parser.error("The build exceeds GitHub Pages' 1 GiB site limit.")

    remote = git(project, "remote", "get-url", "origin", capture=True)
    source = json.loads(info.read_text()).get("sourceCommit", "")
    message = "Publish WebGL playtest" + (" from " + source[:12] if source else "")

    # A separate checkout keeps the Unity source branch and any edits untouched.
    with tempfile.TemporaryDirectory(prefix="ballgame-pages-") as folder:
        checkout = Path(folder)
        git(checkout, "init", "--quiet")
        git(checkout, "remote", "add", "origin", remote)
        for key in ("user.name", "user.email"):
            value = git(project, "config", "--get", key, capture=True)
            git(checkout, "config", key, value)
        existing = git(checkout, "ls-remote", "--heads", "origin", "gh-pages", capture=True)
        if existing:
            git(checkout, "fetch", "--depth=1", "origin", "gh-pages")
            git(checkout, "checkout", "--quiet", "-b", "gh-pages", "FETCH_HEAD")
        else:
            git(checkout, "checkout", "--quiet", "--orphan", "gh-pages")
        for path in checkout.iterdir():
            if path.name != ".git":
                shutil.rmtree(path) if path.is_dir() else path.unlink()
        shutil.copytree(build, checkout, dirs_exist_ok=True, ignore=shutil.ignore_patterns(".DS_Store"))
        (checkout / ".nojekyll").touch()
        git(checkout, "add", "--all")
        if not git(checkout, "status", "--porcelain", capture=True):
            print("The published branch already contains this build.")
            return
        git(checkout, "commit", "-m", message)
        git(checkout, "push", "origin", "HEAD:gh-pages")
    print("Build pushed to gh-pages. GitHub Pages must use gh-pages / (root).")


if __name__ == "__main__":
    main()
