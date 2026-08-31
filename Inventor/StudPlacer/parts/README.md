# parts/

The stud library. Not generated — you model these once in Inventor.

## `Stud_19x157.ipt` — the 3/4" x 6-3/16" shear stud

Referenced by `STUD_PART_PATH`, and the only part the Table 1-6 / 1-7 arrays use.

1. New Part, millimetres.
2. Sketch on the **XY plane**, circle centred on the **origin**, Ø **19.1 mm**.
3. Extrude **157.2 mm** in **+Z**.
4. Save here as `Stud_19x157.ipt`.

Two things matter and nothing else does:

- **the stud axis runs along the part's own +Z**
- **the weld face (base) sits at the part origin**

Placement then reduces to "point local +Z along the faceplate normal", which is
exactly what `StudPlacer.BuildMatrix` does. Model the head, the fillet, whatever
the supplier drawing shows — just leave the axis and the base where they are.

## Not needed yet

A 12.7 x 101.6 mm stud will be required for the cover-plate rows (door surrounds,
floor cover plates) when that scope lands. See the "What is NOT implemented yet"
section of ../README.md.
