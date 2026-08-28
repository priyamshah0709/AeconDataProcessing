#!/usr/bin/env python3
"""
set_install_path.py -- repoint the iLogic rules at a new StudPlacer install root.

The absolute install path appears several times across ilogic/*.iLogicVb, in the
AddVbFile directives and in the STUD_RULES_DIR / STUD_PART_PATH defaults.
AddVbFile is a pre-compile directive so its argument must be a literal string --
it cannot read a parameter -- which is why the path is repeated rather than
derived. This keeps every copy in step.

Usage:
    python3 tools/set_install_path.py "C:\\Inventor Project\\Inventor\\StudPlacer"

Passing the ilogic subfolder works too; it is stripped automatically.
Re-running with the same root is a no-op.
"""
import os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ILOGIC = os.path.join(HERE, "..", "ilogic")
ANCHOR = re.compile(r'AddVbFile\s+"(?P<root>.*)\\vb\\StudRules\.vb"')


def main(argv):
    if len(argv) != 2:
        print(__doc__)
        return 2

    new_root = argv[1].strip().strip('"').rstrip("\\/")

    # People naturally copy the folder they are looking at in Explorer, which is
    # usually ilogic\ (that is where the rules live). Accept it.
    #
    # Split on backslash explicitly: os.path.basename follows the HOST os, so on
    # macOS or Linux it treats a Windows path as one long filename and this check
    # silently never fires.
    parts = new_root.replace("/", "\\").split("\\")
    tail = parts[-1].lower() if parts else ""
    if len(parts) > 1 and tail in ("ilogic", "vb", "rules", "parts", "samples", "tools", "tests"):
        stripped = "\\".join(parts[:-1])
        print(f'note: "{new_root}"')
        print(f'      looks like the {tail}\\ subfolder; using its parent as the install root:')
        print(f'      "{stripped}"')
        new_root = stripped

    files = sorted(f for f in os.listdir(ILOGIC) if f.endswith(".iLogicVb"))
    if not files:
        print("no .iLogicVb files found in " + os.path.abspath(ILOGIC))
        return 1

    old_root = None
    for f in files:
        text = open(os.path.join(ILOGIC, f), encoding="utf-8").read()
        m = ANCHOR.search(text)
        if m:
            old_root = m.group("root")
            break
    if old_root is None:
        print("could not find the AddVbFile anchor; has the rule header been edited?")
        return 1

    total = 0
    if old_root == new_root:
        print(f'already set to "{new_root}" -- no rewrite needed.')
        print("Verifying the layout anyway; run this any time to check an install.")
        files = []

    for f in files:
        path = os.path.join(ILOGIC, f)
        text = open(path, encoding="utf-8").read()
        n = text.count(old_root)
        if n:
            open(path, "w", encoding="utf-8").write(text.replace(old_root, new_root))
            total += n
        print(f"  {f}: {n} occurrence(s)")

    if files:
        print()
        print(f'  from: "{old_root}"')
        print(f'    to: "{new_root}"')
        print(f"  {total} path(s) updated across {len(files)} rule file(s).")

    print()
    print("Expected layout under that root:")
    missing = 0
    on_windows = os.sep == "\\"
    for sub in ("ilogic\\StudPlacer_Main.iLogicVb", "ilogic\\StudPlacer_SelfTest.iLogicVb",
                "vb\\StudRules.vb", "vb\\StudArray.vb", "vb\\StudPlacer.vb",
                "rules\\global_constraints.csv", "rules\\table_1_6_walls.csv",
                "rules\\table_1_7_floors.csv", "parts\\Stud_19x157.ipt"):
        full = new_root + "\\" + sub
        if not on_windows:
            print("  " + full)          # cannot check a Windows path from here
        elif os.path.exists(full):
            print("  OK      " + full)
        else:
            print("  MISSING " + full)
            missing += 1
    if on_windows and missing:
        print()
        print(f"{missing} item(s) not found. The rules will fail until they exist.")
        print(r"parts\Stud_19x157.ipt is the one you create yourself -- INVENTOR_SETUP.md step 2.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
