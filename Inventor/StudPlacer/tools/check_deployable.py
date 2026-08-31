#!/usr/bin/env python3
"""
check_deployable.py -- would a fresh clone of this repo actually run?

Born from a real failure: .gitignore had a blanket *.csv (sensible -- the repo
holds multi-hundred-MB model extracts) which silently swallowed rules/*.csv.
Everything worked locally, then the tool failed on the Inventor workstation with
"code tables not found" because those files had never been pushed. The blast
radius also covered parts/, ignored as a Python packaging convention.

This asserts every file the tool needs AT RUN TIME is committed and not ignored.
It is not a style check -- a miss here means a broken deployment.

Exit 0 = clean, 1 = something would not survive a clone.
"""
import os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
REPO = subprocess.run(["git", "rev-parse", "--show-toplevel"], cwd=ROOT,
                      capture_output=True, text=True)

if REPO.returncode != 0:
    print("not a git repo -- skipping deployability check")
    sys.exit(0)
REPO = REPO.stdout.strip()

# Everything Inventor reads at run time. Folders are checked recursively.
RUNTIME = [
    "ilogic/StudPlacer_Main.iLogicVb",
    "ilogic/StudPlacer_SelfTest.iLogicVb",
    "vb/StudRules.vb",
    "vb/StudArray.vb",
    "vb/StudPlacer.vb",
    "rules/table_1_6_walls.csv",
    "rules/table_1_7_floors.csv",
    "rules/global_constraints.csv",
]
# Not read by the engine, but a deployment without them is not usable.
SUPPORTING = [
    "README.md",
    "INVENTOR_SETUP.md",
    "parts/README.md",
    "samples/exclusions_example.csv",
    "samples/module_parameters_example.csv",
    "tools/set-install-path.ps1",
    "tools/set_install_path.py",
    "tools/check_rule_tables.py",
]

tracked = set(subprocess.run(["git", "ls-files"], cwd=REPO,
                             capture_output=True, text=True).stdout.split("\n"))

problems = []   # hard failures: the file can never be pushed as configured
warnings = []   # soft: just not committed yet
checked = 0


def is_ignored(repo_rel):
    """True if git would exclude this path.

    Deliberately uses -q, not -v: with -v, check-ignore exits 0 whenever ANY
    pattern matches, including a '!' negation, so a correctly un-ignored file
    reads as ignored. -q gives the real answer; -v is only for the message.
    """
    q = subprocess.run(["git", "check-ignore", "-q", repo_rel],
                       cwd=REPO, capture_output=True, text=True)
    return q.returncode == 0


def matching_rule(repo_rel):
    v = subprocess.run(["git", "check-ignore", "-v", repo_rel],
                       cwd=REPO, capture_output=True, text=True)
    if v.returncode == 0 and v.stdout.strip():
        return v.stdout.strip().split("\t")[0]
    return "(unknown rule)"


def check(rel, kind):
    global checked
    checked += 1
    abs_path = os.path.join(ROOT, rel)
    repo_rel = os.path.relpath(abs_path, REPO).replace(os.sep, "/")

    if not os.path.exists(abs_path):
        problems.append(f"{kind}: {rel} does not exist on disk")
        return
    if repo_rel in tracked:
        return

    if is_ignored(repo_rel):
        problems.append(
            f"{kind}: {rel} is IGNORED by {matching_rule(repo_rel)} -- it can "
            f"never be pushed. Add a '!' negation for it in .gitignore.")
    else:
        warnings.append(
            f"{kind}: {rel} is not committed yet -- 'git add' it, or the "
            f"Inventor workstation will not get it.")


for rel in RUNTIME:
    check(rel, "RUNTIME")
for rel in SUPPORTING:
    check(rel, "SUPPORTING")

print(f"deployability: {checked} files checked, "
      f"{len(problems)} problem(s), {len(warnings)} uncommitted")
for p in problems:
    print("  PROBLEM  " + p)
for w in warnings:
    print("  TODO     " + w)
sys.exit(1 if problems else 0)
