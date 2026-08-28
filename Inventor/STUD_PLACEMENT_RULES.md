# Shear Stud Placement — Rules, Constraints & Gotchas

Extracted from the two drawing packages in [Inventor/](.):

| File | PLM doc | Rev / date | Content | Status |
|---|---|---|---|---|
| `DA1-GEH-U71-RZZ-DDRW-ST-0001_C11 1.pdf` (13 sheets) | **008N9536** | Rev 11 — 2026-07-15 | RB DP‑SC **General Notes & Standard Details**, sheets G100–G111. **This is the governing stud document.** | Deferred Verification / IFC‑H |
| `DA1-GEH-U71-RZZ-DDRW-CC-0003_C09 1.pdf` (40 sheets) | **009N0962** | Rev 8 — 2026-07-02 | RB DP‑SC **Level 1 (EL −29.0 m) & associated walls**, sheets S000–S606. Module-specific application. | Deferred Verification / IFC‑H |

Project: BWRX‑300 Reactor Building, DNNP‑1. GE Vernova Hitachi (GVH) proprietary, non-public, EAR99 / 10 CFR 810 export-controlled.

> **Both packages are on HOLD / "Deferred Verification"** — open items in the supporting documents listed in Table 1‑1 of G101. Numbers below can still move; re-check the sheet revision before committing geometry to fabrication.

---

## 0. Precedence, units and the "who wins" rules

| # | Rule | Source |
|---|---|---|
| 0.1 | If the **general-notes drawing conflicts with a module drawing**, the requirement indicated by the **seal stamp on the module drawing governs**. | G101 §1.2.1 |
| 0.2 | Notes must be read with the project specifications; conflicts go to GEH for resolution. | G101 §1.1 |
| 0.3 | **Elevations in metres, dimensions in millimetres.** Values in square brackets `[ ]` are the **original imperial reference dimensions used for procurement/fabrication**. | G101 §1.3.1 |
| 0.4 | **Stud spacing, diameter, and length-after-welding shall be as shown on the design drawings.** Any deviation must be reported in writing by the fabricator and approved by GEH. | G101 §1.6.2.4 |
| 0.5 | Module drawings defer stud configuration back to 008N9536 G103 Tables 1‑5, 1‑6, 1‑8 and G109. | CC‑0003 S100 note 4; S3xx/S4xx note 1 |

**Unit gotcha for any generator:** the drawings mix rounded and exact conversions of the same dimension. Treat these as *the same* nominal value, and prefer the exact one:

- 3″ → `76`, `76.2`, and once `74`
- 4″ → `101`, `101.6`, `102`
- 6″ → `152`, `152.4`
- 8″ → `203`, `203.2`, `204`, `208`
- 3/4″ dia → `19`, `19.05`, `19.1`
- 1/2″ dia → `12.7`, `13`
- 10.5″ → `268` (exact is 266.7)

---

## 1. Universal hard constraints (apply everywhere)

| # | Constraint | Value | Source |
|---|---|---|---|
| 1.1 | **Minimum stud spacing, 19.05 mm [3/4″] studs** | **76.2 mm [3″]** — per AISC 360 §I8.3e | T1‑6 n1, T1‑7 n2, G109 n1 |
| 1.2 | **Minimum stud spacing, 12.7 mm [1/2″] studs** | **50.8 mm [2″]** — per AISC 360 §I8.3e | G109 n1 |
| 1.3 | **Minimum edge distance** — edge of *stud base* to free edge of plate, concrete flow hole, or penetration-sleeve edge | **38.1 mm [1‑1/2″]** | T1‑6 n3, T1‑7 n5 |
| 1.4 | **Minimum distance, stud base → cover plate or sleeve** | **38.1 mm [1.5″]** | T1‑7 n10 |
| 1.5 | **Maximum stud-to-stud / stud-to-diaphragm spacing (floor & basemat faceplate arrays)** — to prevent faceplate buckling | **254 mm [10″]** | T1‑7 n2 |
| 1.6 | **Maximum stud spacing in wall connection zones** | **610 mm [24″]** | G105 n1, G106 n6, G108 n4 |
| 1.7 | **Minimum tie-bar spacing** | **101.6 mm [4″]** | G109 n2 |
| 1.8 | **Shifting studs is permitted** where needed to clear weld lines or interference — *provided min/max spacing is still met*, UNO | T1‑6 n2, T1‑7 n4, G110 n3 |
| 1.9 | When studs are shifted or skipped, **adjust stud quantity proportionally to the actual spacing** | G110 n3 |

> ⚠ **1.5 vs 1.6 are different scopes.** 254 mm is the *array* ceiling on floor/basemat faceplates (Table 1‑7 note 2). 610 mm is the *connection-zone* ceiling at wall-to-wall / wall-to-floor joints. Walls have their own transverse ceiling from Table 1‑6 (381–432 mm). Don't apply one globally.

---

## 2. Table 1‑6 (G103) — Wall faceplate stud arrays

Stud anchor material is **ASTM A108 Type B** and stud geometry is **6‑3/16″ [157.2 mm] long × 3/4″ [19.1 mm] dia** for *all three* wall types.

| Component | Elevation range | t<sub>sc</sub> | FP/diaphragm grade | Max W<sub>sc</sub> (diaph. spacing @ CL) | # stud **columns** | Max longitudinal S<sub>l</sub> (or stud→plate) | Transversal S<sub>t</sub> | Max transversal (or stud→diaphragm) |
|---|---|---|---|---|---|---|---|---|
| **SCCV Wall** | −35.2 m → −25.5 m | 48″ / 1220 | ASME SA537 Cl. 2 | 24″ / **609.6** | **2** | 4.0″ / **101.6** | **W<sub>sc</sub>/3** | 16″ / **406.4** |
| **RB Outer Wall** | −35.2 m → −23.9 m | 36″ / 915 | ASTM A572 Gr 65 | 18.0″ / **457.2** | **1** | 3.0″ / **76.2** | **W<sub>sc</sub>/2** | 15″ / **381.0** |
| **RPV Pedestal Wall** | −34 m → −31.1 m | 48″ / 1220 | ASTM A572 Gr 50 | 24″ / **609.6** | **2** | 4.0″ / **101.6** | **W<sub>sc</sub>/3** | 17″ / **431.8** |
| **RPV Pedestal Wall** | −31.1 m → −8.0 m | 48″ / 1220 | ASTM A572 Gr 50 | 24″ / **609.6** | **2** | 8″ / **203.2** | **W<sub>sc</sub>/3** | 17″ / **431.8** |

**Table 1‑6 notes:**
1. Min stud spacing 76.2 mm [3″] per AISC 360 §I8.3e.
2. Shifting permitted to avoid weld lines / interference if min-max met, UNO.
3. Min stud-base edge → free plate edge / flow hole / sleeve edge = **38.1 mm [1‑1/2″]**.
4. **Penetration sleeve inside a shaft wall:** maintain the required stud density *outside* the sleeve region, and **add studs** so the transversal spacing between sleeve and adjacent diaphragm never exceeds the tabulated max transversal spacing.

> ⚠ **Drawing typo (Rev 11, revision-clouded):** the second RPV Pedestal band reads `From EL. -31.1m [-114.6'] to EL -8.0m [-26.2']`. The imperial value `[-114.6']` is wrong (−114.6 ft = −34 m); it should read `[-102']`. Use the **metric** −31.1 m.

### 2.1 Detail 3 / G103 — Typical *wall* faceplate stud arrangement

- **Cross-section:** stud columns sit between diaphragms spaced W<sub>sc</sub>; transverse pitch = S<sub>t</sub>; the number of columns is set by Table 1‑6 (1 for RB outer wall, 2 for SCCV & pedestal).
- **Elevation (vertical / longitudinal):** rows at pitch S<sub>l</sub>.
- **Note 1 — landing plate present at a horizontal splice:** spacing to the embedded/penetrating steel plate ≤ **S<sub>l</sub>**. For **basemat OSW modules**, up to **86.9 mm [3‑7/16″]** from top stud to landing plate is acceptable **at Splice A**.
- **Note 2 — no landing plate at the horizontal splice:** spacing to the splice line ≤ **S<sub>l</sub>/2**. For **pedestal wall modules**, up to **76.2 mm [3″]** from studs to the splice line is acceptable **at Splice B**.

---

## 3. Table 1‑7 (G103) — Basemat & interior floor stud arrays

Stud anchor material **ASTM A108 Type B**, **6.188″ [157.2 mm] long × 0.750″ [19.1 mm] dia** in every row. The array **degrades as the diaphragm spacing tightens** — this is the key behaviour to encode.

| Component | t<sub>sc</sub> | Grade | W<sub>sc</sub> | S<sub>t</sub> (transversal / diaphragm) | S<sub>l</sub> (longitudinal) | # stud **rows** n |
|---|---|---|---|---|---|---|
| **Inner Basemat** (containment) | 48″ / 1220 | SA537 Cl. 2 | 24″ / 609.6 | W<sub>sc</sub>/3 = **203.2** | 10.0″ / **254.0** | **2** |
| | | | 20″ / 508.0 | W<sub>sc</sub>/2 = **254.0** | 6.0″ / **152.4** | **1** |
| | | | **< 10″ / < 254.0** | — | — | **No studs** |
| **Outer Basemat** (non-containment) | 48″ / 1220 | A572 Gr 65 | 24″ / 609.6 | W<sub>sc</sub>/3 = **203.2** | 10.0″ / **254.0** | **2** |
| | | | 20″ / 508.0 | W<sub>sc</sub>/2 = **254.0** | 6.0″ / **152.4** | **1** |
| | | | **< 10″ / < 254.0** | — | — | **No studs** |
| **Interior Floor Slabs**, −29.0 m → −8.5 m | 24″ / 609.6 | A572 Gr 65 | 24″ / 609.6 | W<sub>sc</sub>/3 = **203.2** | 3.0″ / **76.2** | **2** |
| | | | 16″ / 406.4 | W<sub>sc</sub>/2 = **203.2** | 3.0″ / **76.2** | **1** |
| | | | **6″ / 152.4** | — | — | **No studs** |

**Table 1‑7 notes — the important behavioural rules:**

1. Three diaphragm spacings are tabulated to show the stud requirement at max allowed spacing and the reduced studs as spacing decreases.
2. Min stud spacing **76.2 mm [3″]** (3/4″ studs). **Max stud-to-stud or stud-to-diaphragm = 254 mm [10″]** to prevent faceplate buckling.
3. **Radially arranged diaphragms** (spacing varies along module length): transversal stud spacing may be **reduced between the middle rows** to keep **152.4 mm [6″] clearance** between diaphragm plates and the first stud rows.
4. Shifting permitted to avoid weld lines / interference if min-max met, UNO.
5. Min stud-base → free plate edge or concrete flow hole = **38.1 mm [1‑1/2″]**.
6. **Double stud rows in a diaphragm-cavity floor module:** longitudinal spacing in interior floor slabs may be stretched to **88.9 mm [3.5″]** c/c where needed to straddle thick/thin plate transition weld seams.
7. **Density make-up rule — double rows, W<sub>sc</sub> = 610 mm [24″]:** where a stud can't sit at the prescribed longitudinal spacing, add studs in the affected region to restore **≥ 8 studs per 305 mm [12″] of longitudinal diaphragm-channel length**.
8. **Density make-up rule — single row, W<sub>sc</sub> = 406 mm [16″]:** same idea, restore **≥ 4 studs per 305 mm [12″]**.
9. **Stud-to-sleeve spacing may be up to S<sub>l</sub>.** Table 1‑7 density requirements still apply everywhere outside the sleeve region.
10. Min distance from **stud base edge to cover plate or sleeve = 38.1 mm [1.5″]**.
11. Longitudinal stud spacing **S<sub>l</sub> = 122 mm [4.8″]** is acceptable between floor rings **C and B at the interior floor-slab ring splice, Level −29.0 m**.

### 3.1 Detail 2 / G103 — Typical *floor* faceplate stud arrangement

- **(1) Termination spacing:** ≤ **S<sub>l</sub>/2** when terminating at a **splice**; ≤ **S<sub>l</sub>** when terminating at a **cover plate**. (Min spacing per T1‑7 n10.)
- **(2)** Typical stud spacing at a **penetration sleeve** — min spacing per T1‑7 n10.
- **(3) Staggered stud configurations:** the distance between adjacent studs must be ≤ **S<sub>l</sub>**, measured as the **shortest straight-line path between stud centrelines** (not the axis-aligned pitch).
- **(4)** If transverse spacing between a **penetration sleeve** and a **diaphragm or wall faceplate exceeds 254 mm [10″]**, **add a stud in between**.
- Detail also annotates **MIN 76.2 mm [3″]**, **MAX 254 mm [10″] (typ.)** and **MAX W<sub>sc</sub>**.
- On the cross-section: *"Number of stud rows can be decreased as the diaphragm spacings decrease — see Table 1‑7 note 2"* and *"No studs are required if the diaphragm spacing is less than 10″, for inner- and outer-basemat."*

---

## 4. Table 1‑11 (G109) — SC wall studs & round tie bars (wing / stair / elevator / partition)

These are the **thin SC walls** — different stud sizes from the DP‑SC walls above. Faceplate 12.7 mm [0.500″], PJP weld size 9.5 mm [0.375″], tie bar F<sub>y</sub> 50 ksi (345 MPa) / F<sub>u</sub> 65 ksi (448 MPa) on every row.

| Component | Level | t<sub>sc</sub> | **Max stud spacing s** | Max tie-bar dia d<sub>tie</sub> | Tie-bar spacing S<sub>tl</sub> & S<sub>tt</sub> | Stud dia Ø<sub>st</sub> | Stud length l<sub>st</sub> |
|---|---|---|---|---|---|---|---|
| **Wing Walls** | −34 → 0.0 | 18″ / 460 | 4.5″ / **114.3** | 1.00″ / 25.4 | 9.0″ / **228.6** | 0.75″ / **19** | 6.0″ / **152.4** |
| **Stairwell Walls** | −34 → −21.0 | 18″ / 460 | 4.5″ / **114.3** | 1.00″ / 25.4 | 9.0″ / **228.6** | 0.75″ / **19** | 6.0″ / **152.4** |
| **Elevator Walls** | −34 → 0.0 | 15″ / 380 | 3.75″ / **95.3** | 0.75″ / 19.1 | 7.5″ / **190.5** | 0.5″ / **12.7** | 4.0″ / **101.6** |
| **Partition Walls** | −34 → −21 | 15″ / 380 | 3.75″ / **95.3** | 0.75″ / 19.1 | 7.5″ / **190.5** | 0.5″ / **12.7** | 4.0″ / **101.6** |

**G109 notes:**
1. Min stud spacing **76.2 mm [3″]** for 19.05 mm [3/4″] studs and **50.8 mm [2″]** for 12.7 mm [1/2″] studs (AISC 360 §I8.3e).
2. Min allowable spacing **between tie bars = 101.6 mm [4″]**.
3. Tie bars at uniform intervals longitudinally and transversely wherever practical; **tie-bar spacing may be locally reduced** within a wall module to clear adjacent structural elements.
4. **The first row of studs may be deleted**, provided the equivalent number of shear connectors is replaced **within a distance of 1 × t<sub>sc</sub> from the wall edge**, and min/max spacing for studs and ties is met. See Details J & K.

### 4.1 Periphery stud arrays (G109 Details J & K)

Ramp-in pattern from the wall edge before the standard array begins:

| Detail | Applies to | Edge offset (max) | Ramp-in pitch | Then |
|---|---|---|---|---|
| **J** | **Wing wall & stair wall** | **203 mm [8″] max** (both directions from edge) | 3 rows at **76 mm [3″]** | "BEGIN STANDARD ARRAY" at pitch S |
| **K** | **Elevator walls** | **191 mm [7‑1/2″] max** | 3 rows at **64 mm [2‑1/2″]** | "BEGIN STANDARD ARRAY" at pitch S |

Connection-region studs in these details are **101.6 mm [4″] or 152.4 mm [6″]** long.

---

## 5. Connection-zone stud densities (per unit length / unit height)

Outside the tabulated arrays, connection details specify **minimum stud counts per metre**, not a pitch. All are **3/4″ [19.1 mm] dia** unless noted.

### 5.1 Wall-to-wall & wall-to-floor (G105, G106)

| Location | Stud | Minimum count |
|---|---|---|
| RB exterior / SCCV / RB wall **faceplate, inside connection zone** | 19 × 152 mm [3/4″ × 6″] | **20 per 1 m** per faceplate; 76 mm [3″] min spacing between stud columns and between stud columns & diaphragm plate |
| **Shaft-wall faceplate outside joint zone** | 19.05 × 152.4 [3/4″ × 6″] | **30 per 1 m** |
| **Doubler plate outside joint zone** (elevator wall) | 19 × 101.6 [3/4″ × 4″] | **30 per 1 m** |
| Thickened stiffening diaphragm plate, **RB ext. wall** | 19 × 102 [3/4″ × 4″] | **15 per 823 mm [32‑3/8″]** per face |
| Thickened stiffening diaphragm plate, **SCCV wall** | 19.1 × 101.6 [3/4″ × 4″] | **24 per 1097 mm** per face |
| SC wall FP **outside** joint zone (G106 Type‑1/2) | 19 × 152 [3/4″ × 6″] | **40 per 1 m** (also a **32 per 1 m** case) |
| SC wall FP **inside** joint zone | 19 × 152 [3/4″ × 6″] | **12 per 1 m** |
| Stiffener plate **outside** joint zone | 19 × 152 [3/4″ × 6″] | **30 per 1 m** (wing/stair) · **33 per 1 m** (elevator) |
| Stiffener plate **inside** joint zone | 19 × 102 [3/4″ × 4″] | **8 per 1 m** |

### 5.2 SC wall-to-wall connections (G110 / G111)

| Face | Stud | Min count per 1 m unit height |
|---|---|---|
| Stiffener plate **outside** joint zone — stairwell / wing | 19.1 × 101.6 or × 152.4 | **30** |
| End stiffener plate **outside** joint zone — stairwell | 19.1 × 101.6 [4″] | **30** |
| End stiffener plate **outside** joint zone — wing wall | 19.1 × 152.4 [6″] | **20** |
| Stiffener plate **outside** joint zone — elevator | 19.1 × 152.4 [6″] | **20** |
| Inner / top / bottom stiffener **inside** joint zone — stairwell & wing | 19.1 × 101.6 [4″] | **8** |
| Inner / top / bottom / end stiffener **inside** joint zone — elevator | 19.1 × 101.6 [4″] | **6** |
| Elevator wall stiffener plate outside joint zone (G110 A) | 19.1 × 152.4 [6″] | **20** |
| SCCV wall FP **inside** joint zone, between two doubler plates (horiz.) & two diaphragms (vert.) — Type‑3 | 19.1 × 152.4 [6″] | **30** |
| SCCV wall FP **outside** joint zone above airlock — Type‑3 | 19.1 × 152.4 [6″] | **min 4 rows × min 25 columns**, min spacing 76.2 mm [3″] |

**G110 note 3 (governs all of §5.2):** *Studs minimum spacing is 76.2 mm [3″]. Shift studs as needed to avoid interference; maintain min/max spacing. Adjust stud quantity proportionally to actual spacing.*

### 5.3 The proportional-adjustment rule (appears on G105, G106, G110, G111)

> If a wall (vertical) splice exists **between two concrete flow holes** and the spacing between the holes deviates from the specified unit length (1 m), **adjust the number of shear studs between the flow holes proportionally**, by the ratio of actual spacing to the specified unit length.

Also: *max recommended spacing between the wall faceplate and the first diaphragm plate is **305 mm [12″]*** (G106 n5).

---

## 6. Omission, relocation and make-up rules (the exception logic)

| Trigger | Rule | Source |
|---|---|---|
| **Gripper hole** (≈305 mm [12″] from plate edge) | Shift stud rows **locally** to give **152 mm [6″] clearance** to the gripper | G105 n2 |
| **Gripper holes in diaphragms** | Max Ø 3/4″, **max 4 per diaphragm**, min **2.25″** from edge or concrete flow hole. Studs **may be relocated** to maintain min spacing and specified stud density. | G101 §1.14.5 |
| **Stud on a stiffener conflicts with an architectural opening** (e.g. door) | The stud anchor on the stiffener **can be omitted** | G106 n3 |
| **Concrete flow hole falls under a door opening** | Flow hole may be omitted, and **two 19 mm × 102 mm [3/4″ × 4″] studs added on the centreline of the omitted hole**, under/on the stiffener plate | G106 n10; CC‑0003 S3xx n9 |
| **Flow hole conflicts with a tie-rod** | Flow hole may be relocated within **± 6″**; stud count between adjacent flow holes adjusted proportionally | G106 n11 |
| **Standard wall stud anchors overlap tie plates** | **Skip** the conflicting studs | G107 n5 |
| **Tie-plate in SCCV wall** | Tie plate shall be placed **between two stud columns** | G107 n7 |
| **Tie-plate in RB exterior wall** | Studs overlapping the tie plate **shall be removed** | G107 n7 |
| **Corbel at pedestal entry** | Headed studs shall be **centre-located between diaphragm plate and tie-plate** | CC‑0003 S514 n4 |
| **Faceplate openings / weld access windows / patch plates** | Diaphragms restored with patch plates + CJP; backing bars permitted and may remain | G101 §1.14.4 |

---

## 7. Penetration / sleeve special rules

### 7.1 General (G103)
- Stud-to-sleeve spacing may be up to **S<sub>l</sub>**; density requirements apply everywhere outside the sleeve region (T1‑7 n9).
- Min stud base → sleeve edge = **38.1 mm [1.5″]** (T1‑6 n3, T1‑7 n10).
- If transverse spacing sleeve↔diaphragm or sleeve↔wall faceplate **> 254 mm [10″]**, add a stud between (Detail 2 note 4).
- Shaft walls: maintain the required density outside the sleeve region and add studs so the transversal spacing sleeve↔adjacent diaphragm never exceeds Table 1‑6's max transversal spacing (T1‑6 n4).

### 7.2 Airlock / CRD port in the SCCV wall (CC‑0003 sheets S505, S508 — thickened faceplate)
These are explicit, generator-ready rules:

1. Stud spacing shown is typical for the **thickened faceplate around the airlock**. Stud material, length and diameter per **Table 1‑6, G103**.
2. **Transverse stud spacing S<sub>t</sub>, and the spacing from the diaphragm centreline to the next stud, shall be kept between 76 mm [3″] and 143 mm [5‑5/8″].**
3. Studs shall be kept **at least 76 mm [3″] radially away from the airlock sleeve centreline**.
4. In regions between the airlock sleeve edge and the first diaphragm plate, where **S<sub>variable</sub>** = centre-to-centre distance between diaphragms, or diaphragm→sleeve:

   | S<sub>variable</sub> | Studs |
   |---|---|
   | ≤ 143 mm [5‑5/8″] | **No stud** |
   | > 143 mm and ≤ 286 mm [11‑1/4″] | **1 row** |
   | > 286 mm and ≤ 429 mm [16‑7/8″] | **2 rows** |
   | > 429 mm and ≤ 572 mm [22‑1/2″] | **3 rows** |

5. **60.3 mm [2‑3/8″] clear distance** from the edge of the airlock to adjacent shear studs.
6. Edge-of-stud to plate edge ≤ 38 mm annotated as `≤38` on the thickened-faceplate stud configuration.

---

## 8. Small-stud (1/2″) applications

| Application | Stud | Min spacing | Max spacing | Source |
|---|---|---|---|---|
| **One row all around a door cover plate** (SC walls) | 12.7 mm [1/2″] dia × 101.6 mm [4″] | **50.8 mm [2″]** | **304.8 mm [12″]** | CC‑0003 S301–S404 note 2 |
| **Floor cover plates / chase & CRD-hatch cover plates** | 12.7 mm [1/2″] × 101 mm [4″], **2 rows** | **51 mm [2″]** | **268 mm [10.5″]** | CC‑0003 S601–S606, S603.1 |
| **Flange / filler plates at floor openings** | 12.7 mm [1/2″] × 101 mm [4″] | — | **2 per side of plate (typ.)** | CC‑0003 S601, S602, S604, S606 |
| Min clearance **tie rods ↔ door cover-plate shear studs** | — | **25.4 mm [1″]** | — | CC‑0003 S404 note 5 |

---

## 9. Stud material, welding and QA

| Item | Requirement | Source |
|---|---|---|
| **Stud material** | **ASTM A108 Type B**, as permitted by ASME BPVC §III Div. 2 (2021) Subsec. CC, App. D2‑I Table D2‑I‑2.2, meeting strength requirements of CC‑2711 / Table CC‑2623.2‑1 | G101 §1.6.2.3; T1‑6, T1‑7 |
| Stud procurement/fabrication spec | **007N8061** — BWRX‑300 Procurement & Fabrication of Seismic Category 1A Structural Steel | G101 §1.6.2.1 |
| **Stud welding material — RB** | AWS D1.1 Clause 7 and §9.7 | G101 §1.10.5 |
| **Stud welding material — SCCV** | ASME BPVC §III Div. 2, Subsec. CC‑2620 | G101 §1.10.5 |
| **Stud welding procedure qualification — RB** | AWS D1.1 §9.7 (**pre-production bend test**) | G101 §1.10.6 |
| **Stud welding procedure qualification — SCCV** | ASME §III Div. 2, CC‑4534 & CC‑4543.5 (**production bend test of one stud per 100**) | G101 §1.10.6 |
| **Certification of welded studs — RB** | AWS D1.1 §9.3 (both sides) and ANSI/AISC N690 §NA4.6 | G101 §1.11.2 |
| **Certification of welded studs — SCCV** | ASME BPVC §III Div. 2 Subsec. CC‑2130 | G101 §1.11.2 |
| Weld sequencing | **Weld before pouring concrete** into the floor DP‑SC panel module | G106 n9, G107 n2, G108 n3 |

---

## 10. Related geometry constraints that drive stud layout

| Item | Value | Source |
|---|---|---|
| **Tolerance on horizontal spacing between diaphragm plates** | ≤ **3 mm [1/8″]** | G101 §1.13.2 |
| **Diaphragm plate out-of-flatness** | ≤ diaphragm depth ÷ 150 (AWS D1.1 §7.22.6) | G101 §1.13.3 |
| **Fabricator may move a module vertical splice** | Only if: thickened diaphragms stay aligned with wall faceplates; penetration azimuths unchanged; **revised splice does not exceed max W<sub>sc</sub> of Table 1‑6**; vertical seam weld lines offset/staggered | CC‑0003 S100 note 3 |
| Concrete flow hole (typ. wall/floor) | Ø 732 mm [2′‑4‑7/8″] and Ø 366 mm [1′‑2‑3/8″] appear as typicals | G104, S6xx |
| Stiffener plate flow-hole re-entrant corner radius | 2 × plate thickness or **16 mm [5/8″]**, whichever greater; **min 13 mm [1/2″]** | G106 n8 |
| Max spacing wall faceplate → first diaphragm plate | **305 mm [12″]** recommended | G106 n5 |
| Max spacing between cover plates (Type‑3) | **203.2 mm [8″]** | G108 |

---

## 11. Open conflicts / things to confirm before automating

1. **Which "maximum spacing" applies where.** 254 mm [10″] (T1‑7 n2, floor/basemat array) vs 381–432 mm (T1‑6 max transversal, walls) vs 610 mm [24″] (G105/G106/G108 connection zones). Scope each by region rather than picking one global max.
2. **RPV Pedestal Wall elevation typo** in Table 1‑6 (`[-114.6']` should be `[-102']`) — §2 above.
3. **Table 1‑6 has no explicit longitudinal *minimum*** — only the maximum. The 76.2 mm [3″] AISC floor from note 1 is the min.
4. **`S` vs `S_l` vs `S_t` symbol usage is inconsistent** between G103 Details 2/3, G109 Detail A, and Table 1‑11 (which uses `s` for max stud spacing and `S_tl`/`S_tt` for **tie-bar** spacing, not stud spacing). Map symbols per-sheet, not globally.
5. **Interior floor slabs Table 1‑7 covers −29.0 m to −8.5 m**, but Table 1‑5 lists interior floor slabs at El −29.0 m and Table 1‑8 lists them at EL 0.0 m. Confirm which elevation band a given module falls in before picking a row.
6. **Interior floor slabs have zero pitch slack.** Table 1‑7 gives `S_l = 76.2 mm [3"]` for interior floor slabs, which is *exactly* the AISC 360 §I8.3e minimum spacing (§1.1). The longitudinal pitch therefore has no tolerance band at all — it must land on 76.2 mm, not "at or below". Any layout routine that spreads a remainder into the pitch produces a non‑compliant array. Note 6's 88.9 mm [3.5″] allowance for straddling a weld seam is the only sanctioned departure upward.
7. **Scope of the 38.1 mm edge clearance.** Notes T1‑6 n3 and T1‑7 n5 word it against a *free edge* of a plate, a concrete flow hole, or a penetration sleeve edge; T1‑7 n10 extends it to a cover plate or sleeve. It is **not** worded against a splice line or a landing plate, where the faceplate runs continuously through the joint. This matters: at a bare splice Detail 2/3 allows the stud centre within `S_l/2`, which for interior floors is 38.1 mm — already inside the centre‑to‑boundary equivalent of the edge rule. Reading the edge rule as applying at splices makes the interior floor array infeasible. Confirm the intent with the responsible engineer.
8. **Both drawings are HOLD / Deferred Verification.** Table 1‑6, 1‑7, 1‑8 and Detail 2 all carry Rev‑11 revision clouds — these numbers changed in the current revision.

---

## 12. Implementation status — [StudPlacer/](StudPlacer/)

These rules are implemented as an iLogic tool in [StudPlacer/](StudPlacer/), driven by
the code tables in [StudPlacer/rules/](StudPlacer/rules/) so a drawing revision is a CSV
edit rather than a code change.

- [StudPlacer/README.md](StudPlacer/README.md) — design, scope and caveats
- [StudPlacer/INVENTOR_SETUP.md](StudPlacer/INVENTOR_SETUP.md) — deploying and running it
- `StudPlacer/tests/run-tests.sh` — 141 rule‑table lint checks, both iLogic rules
  compile‑checked, 132 engine assertions against the compiled VB. **Re‑run after any
  edit to the rule CSVs.**

**Built** — Tables 1‑6 and 1‑7:

- [x] Region model: wall (T1‑6) vs floor/basemat (T1‑7), flat / curved / radial geometry
- [x] Array driver, including the `W_sc` band degradation 2 rows → 1 row → **no studs**
- [x] `S_t` **derived** as `W_sc/(n+1)` rather than looked up, so it stays right at
      untabulated diaphragm spacings; the printed column becomes a ceiling check
- [x] Radial floors re‑resolve the band at every radial station (the cavity widens with `r`)
- [x] Boundary rule: `≤ S_l` at a landing/cover plate, `≤ S_l/2` at a bare splice
- [x] Clearance filter against flow holes, sleeves, tie plates and gripper holes
- [x] Stagger check on the **shortest straight‑line** centre‑to‑centre distance
- [x] Density make‑up: ≥8 studs/305 mm (double row) / ≥4 (single row) with greedy back‑fill
- [x] Proportional count adjustment via the exclusion + make‑up passes
- [x] Per‑region stud diameter and length after welding
- [x] Rule‑annotated CSV schedule — every stud cites the clause that placed it

**Not built yet** — deliberate scope line:

- [ ] Table 1‑11 SC thin walls and the Detail J/K periphery ramp‑in arrays
- [ ] Connection‑zone per‑metre densities (20/30/32/33/40 studs per metre)
- [ ] 1/2″ cover‑plate rows (door surrounds, floor cover plates)
- [ ] Airlock / CRD port `S_variable` row‑count lookup (§7.2) — constants are loaded,
      nothing consumes them
- [ ] Reading diaphragm positions and openings from the Inventor solid rather than
      from parameters and an exclusion file
