' =============================================================================
'  Program.vb  --  regression suite for the StudPlacer engine.
'
'  Compiles and runs ../vb/*.vb, the same files that Inventor loads via
'  AddVbFile. Assertions are black-box through the public API, so nothing here
'  depends on the engine's internals.
'
'  Run:  dotnet run --project tests    (exit 0 = pass, 1 = fail)
' =============================================================================
Option Strict Off
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
' System.IO is not imported: the Inventor stubs declare colliding File/Path
' types on purpose, mirroring iLogic. Always qualify in full.
Imports Inventor

Module StudPlacerTests

    Dim Passed As Integer = 0
    Dim Failed As Integer = 0
    Dim CurrentGroup As String = ""

    Sub Grp(ByVal name As String)
        CurrentGroup = name
        Console.WriteLine()
        Console.WriteLine("-- " & name)
    End Sub

    Sub Ok(ByVal name As String, ByVal condition As Boolean)
        If condition Then
            Passed += 1
        Else
            Failed += 1
            Console.WriteLine("   FAIL  " & name)
        End If
    End Sub

    Sub Eq(ByVal name As String, ByVal actual As Double, ByVal expected As Double, Optional ByVal tol As Double = 0.02)
        If Math.Abs(actual - expected) < tol Then
            Passed += 1
        Else
            Failed += 1
            Console.WriteLine("   FAIL  " & name & ": expected " &
                              expected.ToString("0.####", CultureInfo.InvariantCulture) & ", got " &
                              actual.ToString("0.####", CultureInfo.InvariantCulture))
        End If
    End Sub

    Sub EqS(ByVal name As String, ByVal actual As String, ByVal expected As String)
        If String.Equals(actual, expected, StringComparison.Ordinal) Then
            Passed += 1
        Else
            Failed += 1
            Console.WriteLine("   FAIL  " & name & ": expected '" & expected & "', got '" & actual & "'")
        End If
    End Sub

    ''' <summary>Walk up from the binary looking for the rules folder.</summary>
    Function FindRulesDir() As String
        Dim d As String = AppContext.BaseDirectory
        For i As Integer = 0 To 8
            Dim cand As String = System.IO.Path.Combine(d, "rules")
            If System.IO.File.Exists(System.IO.Path.Combine(cand, "global_constraints.csv")) Then Return cand
            Dim parent As System.IO.DirectoryInfo = System.IO.Directory.GetParent(d.TrimEnd(System.IO.Path.DirectorySeparatorChar))
            If parent Is Nothing Then Exit For
            d = parent.FullName
        Next
        Throw New Exception("Could not locate the rules folder from " & AppContext.BaseDirectory)
    End Function

    Function MinPitchOf(ByVal r As StudArrayResult) As Double
        Dim byLine As New Dictionary(Of String, List(Of Double))
        For Each p As StudPoint In r.Points
            If p.Line = 0 Then Continue For
            Dim k As String = p.Channel & "/" & p.Line
            If Not byLine.ContainsKey(k) Then byLine(k) = New List(Of Double)
            byLine(k).Add(p.LongMm)
        Next
        Dim mn As Double = Double.MaxValue
        For Each kv As KeyValuePair(Of String, List(Of Double)) In byLine
            kv.Value.Sort()
            For i As Integer = 1 To kv.Value.Count - 1
                mn = Math.Min(mn, kv.Value(i) - kv.Value(i - 1))
            Next
        Next
        If mn = Double.MaxValue Then Return 0.0
        Return mn
    End Function

    Function MaxPitchOf(ByVal r As StudArrayResult) As Double
        Dim byLine As New Dictionary(Of String, List(Of Double))
        For Each p As StudPoint In r.Points
            If p.Line = 0 Then Continue For
            Dim k As String = p.Channel & "/" & p.Line
            If Not byLine.ContainsKey(k) Then byLine(k) = New List(Of Double)
            byLine(k).Add(p.LongMm)
        Next
        Dim mx As Double = 0.0
        For Each kv As KeyValuePair(Of String, List(Of Double)) In byLine
            kv.Value.Sort()
            For i As Integer = 1 To kv.Value.Count - 1
                mx = Math.Max(mx, kv.Value(i) - kv.Value(i - 1))
            Next
        Next
        Return mx
    End Function

    ''' <summary>Brute-force 3D nearest neighbour. Fine at test sizes.</summary>
    Function MinNeighbour(ByVal r As StudArrayResult) As Double
        Dim best As Double = Double.MaxValue
        For i As Integer = 0 To r.Points.Count - 1
            For j As Integer = i + 1 To r.Points.Count - 1
                Dim a As StudPoint = r.Points(i)
                Dim b As StudPoint = r.Points(j)
                Dim dx As Double = a.Xmm - b.Xmm
                Dim dy As Double = a.Ymm - b.Ymm
                Dim dz As Double = a.Zmm - b.Zmm
                best = Math.Min(best, Math.Sqrt(dx * dx + dy * dy + dz * dz))
            Next
        Next
        If best = Double.MaxValue Then Return 0.0
        Return best
    End Function

    Function CountLines(ByVal r As StudArrayResult) As Integer
        Dim seen As New HashSet(Of String)
        For Each p As StudPoint In r.Points
            If p.Line > 0 Then seen.Add(p.Channel & "/" & p.Line)
        Next
        Return seen.Count
    End Function

    Function MinRadiusOfLine(ByVal r As StudArrayResult, ByVal line As Integer) As Double
        Dim mn As Double = Double.MaxValue
        For Each p As StudPoint In r.Points
            If p.Line = line Then mn = Math.Min(mn, Math.Sqrt(p.Xmm * p.Xmm + p.Ymm * p.Ymm))
        Next
        If mn = Double.MaxValue Then Return 0.0
        Return mn
    End Function

    Function ViolationCodes(ByVal r As StudArrayResult) As String
        Dim s As New List(Of String)
        For Each v As Violation In r.Violations
            s.Add(v.Code)
        Next
        Return String.Join(",", s.ToArray())
    End Function

    Sub Main()
        Dim rulesDir As String = FindRulesDir()
        Console.WriteLine("StudPlacer engine regression suite")
        Console.WriteLine("rules: " & rulesDir)

        Dim t As RuleTables = RuleTables.Load(rulesDir)

        '--------------------------------------------------------------------
        Grp("Table 1-6 -- DP-SC wall bands")
        Dim s As StudSpec

        s = t.ResolveWall("SCCV_WALL", -30.0)
        Eq("SCCV columns", s.LinesPerCavity, 2)
        Eq("SCCV S_l", s.SlMaxMm, 101.6)
        Eq("SCCV S_t divisor", s.StDivisor, 3)
        Eq("SCCV S_t derived @ Wsc 609.6", s.StNominal(609.6), 203.2)
        Eq("SCCV S_t ceiling", s.StMaxMm, 406.4)
        Ok("SCCV derived S_t within ceiling", s.StNominal(609.6) <= s.StMaxMm)
        Eq("SCCV stud dia", s.StudDiaMm, 19.1)
        Eq("SCCV stud length", s.StudLenMm, 157.2)
        EqS("SCCV stud material", s.StudMaterial, "ASTM A108 Type B")
        Eq("SCCV t_sc", s.TscMm, 1220.0)

        s = t.ResolveWall("RB_OUTER_WALL", -30.0)
        Eq("RB Outer columns", s.LinesPerCavity, 1)
        Eq("RB Outer S_l", s.SlMaxMm, 76.2)
        Eq("RB Outer S_t derived @ Wsc 457.2", s.StNominal(457.2), 228.6)
        Eq("RB Outer S_t ceiling", s.StMaxMm, 381.0)

        Eq("RPV lower band S_l", t.ResolveWall("RPV_PEDESTAL_WALL", -33.0).SlMaxMm, 101.6)
        Eq("RPV upper band S_l", t.ResolveWall("RPV_PEDESTAL_WALL", -20.0).SlMaxMm, 203.2)
        Eq("RPV band boundary -31.1 resolves low", t.ResolveWall("RPV_PEDESTAL_WALL", -31.1).SlMaxMm, 101.6)
        Eq("RPV S_t derived @ Wsc 609.6", t.ResolveWall("RPV_PEDESTAL_WALL", -20.0).StNominal(609.6), 203.2)
        Eq("RPV S_t ceiling", t.ResolveWall("RPV_PEDESTAL_WALL", -20.0).StMaxMm, 431.8)

        Dim threw As Boolean = False
        Try
            t.ResolveWall("SCCV_WALL", 5.0)
        Catch ex As Exception
            threw = True
        End Try
        Ok("out-of-band elevation raises", threw)

        '--------------------------------------------------------------------
        Grp("Table 1-7 -- basemat and interior floor bands")
        s = t.ResolveFloor("INNER_BASEMAT", 609.6)
        Eq("Inner Basemat rows @609.6", s.LinesPerCavity, 2)
        Eq("Inner Basemat S_l @609.6", s.SlMaxMm, 254.0)
        Eq("Inner Basemat S_t derived @609.6", s.StNominal(609.6), 203.2)
        s = t.ResolveFloor("INNER_BASEMAT", 508.0)
        Eq("Inner Basemat rows @508", s.LinesPerCavity, 1)
        Eq("Inner Basemat S_l @508", s.SlMaxMm, 152.4)
        Eq("Inner Basemat S_t derived @508", s.StNominal(508.0), 254.0)
        Ok("Inner Basemat no studs @200", t.ResolveFloor("INNER_BASEMAT", 200.0).NoStuds)
        Ok("Inner Basemat no studs @253.9 (LT boundary)", t.ResolveFloor("INNER_BASEMAT", 253.9).NoStuds)
        Eq("Inner Basemat 1 row @254.0 (LT boundary)", t.ResolveFloor("INNER_BASEMAT", 254.0).LinesPerCavity, 1)

        s = t.ResolveFloor("OUTER_BASEMAT", 609.6)
        Eq("Outer Basemat rows @609.6", s.LinesPerCavity, 2)
        Eq("Outer Basemat S_t derived @609.6", s.StNominal(609.6), 203.2)
        EqS("Outer Basemat grade", s.PlateGrade, "ASTM A572 Grade 65")

        s = t.ResolveFloor("INTERIOR_FLOOR", 609.6)
        Eq("Interior Floor rows @609.6", s.LinesPerCavity, 2)
        Eq("Interior Floor S_l", s.SlMaxMm, 76.2)
        Eq("Interior Floor S_t derived @609.6", s.StNominal(609.6), 203.2)
        s = t.ResolveFloor("INTERIOR_FLOOR", 406.4)
        Eq("Interior Floor rows @406.4", s.LinesPerCavity, 1)
        Eq("Interior Floor S_t derived @406.4", s.StNominal(406.4), 203.2)
        Ok("Interior Floor no studs @152.4", t.ResolveFloor("INTERIOR_FLOOR", 152.4).NoStuds)
        Eq("Interior Floor max Wsc", t.MaxWscFor("INTERIOR_FLOOR"), 609.6)

        threw = False
        Try
            t.ResolveFloor("INTERIOR_FLOOR", 700.0)
        Catch ex As Exception
            threw = True
        End Try
        Ok("Wsc above table max raises", threw)

        '--------------------------------------------------------------------
        Grp("Global constraints")
        Eq("min spacing 3/4in", t.Constraints.MinSpacingFor(19.1), 76.2)
        Eq("min spacing 1/2in", t.Constraints.MinSpacingFor(12.7), 50.8)
        Eq("edge clearance", t.Constraints.Value("MIN_EDGE_CLEARANCE"), 38.1)
        Eq("floor array max spacing", t.Constraints.Value("MAX_SPACING_FLOOR_ARRAY"), 254.0)
        Eq("density window", t.Constraints.Value("DENSITY_WINDOW"), 305.0)
        Eq("density double row", t.Constraints.Value("DENSITY_MIN_STUDS_DOUBLE_ROW"), 8.0)
        Eq("density single row", t.Constraints.Value("DENSITY_MIN_STUDS_SINGLE_ROW"), 4.0)
        threw = False
        Try
            t.Constraints.Value("NOT_A_KEY")
        Catch ex As Exception
            threw = True
        End Try
        Ok("unknown constraint key raises", threw)

        '--------------------------------------------------------------------
        Grp("Flat SCCV wall, end to end")
        Dim m As New ModuleInput()
        m.ModuleId = "TEST-SCCV"
        m.Component = "SCCV_WALL"
        m.Geometry = "FLAT"
        m.ElevM = -30.0
        m.WscMm = 609.6
        m.PlateWidthMm = 609.6 * 4.0
        m.PlateLengthMm = 2000.0
        m.TermStart = "LANDING_PLATE"
        m.TermEnd = "SPLICE"
        m.DensityMakeup = False
        Dim r As StudArrayResult = New StudArrayBuilder(t, m).Build(New List(Of ExclusionZone))

        Eq("stud lines (4 cavities x 2)", CountLines(r), 8)
        Ok("studs generated", r.Points.Count > 100)
        EqS("no violations", ViolationCodes(r), "")
        Eq("pitch runs at S_l", MaxPitchOf(r), 101.6)
        Ok("pitch never below min spacing", MinPitchOf(r) >= 76.2 - 0.01)
        Ok("nearest neighbour >= 76.2", MinNeighbour(r) >= 76.2 - 0.05)
        Ok("count is lines x stations", r.Points.Count Mod 8 = 0)

        ' Every stud must sit on the faceplate and point along +X.
        Dim allOnFace As Boolean = True
        Dim allDirX As Boolean = True
        For Each p As StudPoint In r.Points
            If Math.Abs(p.Xmm) > 0.0001 Then allOnFace = False
            If Math.Abs(p.DirX - 1.0) > 0.0001 OrElse Math.Abs(p.DirY) > 0.0001 Then allDirX = False
        Next
        Ok("all studs on the faceplate plane", allOnFace)
        Ok("all studs normal to the faceplate", allDirX)

        ' First transverse offset must equal the derived S_t off the diaphragm.
        Dim firstY As Double = Double.MaxValue
        For Each p As StudPoint In r.Points
            firstY = Math.Min(firstY, p.Ymm)
        Next
        Eq("first stud column at S_t off the diaphragm", firstY, 203.2)

        '--------------------------------------------------------------------
        Grp("Flat wall termination allowances")
        Dim sts0 As Double = Double.MaxValue
        Dim stsN As Double = Double.MinValue
        For Each p As StudPoint In r.Points
            sts0 = Math.Min(sts0, p.Zmm)
            stsN = Math.Max(stsN, p.Zmm)
        Next
        Ok("landing plate end within S_l", sts0 <= 101.6 + 0.01)
        Ok("splice end within S_l/2", (2000.0 - stsN) <= 50.8 + 0.01)

        ' Same wall, but both ends bare splices -> tighter end distances.
        Dim m2 As ModuleInput = New ModuleInput()
        m2.Component = "SCCV_WALL" : m2.Geometry = "FLAT" : m2.ElevM = -30.0
        m2.WscMm = 609.6 : m2.PlateWidthMm = 609.6 : m2.PlateLengthMm = 2000.0
        m2.TermStart = "SPLICE" : m2.TermEnd = "SPLICE" : m2.DensityMakeup = False
        Dim r2 As StudArrayResult = New StudArrayBuilder(t, m2).Build(New List(Of ExclusionZone))
        Dim z0 As Double = Double.MaxValue
        For Each p As StudPoint In r2.Points
            z0 = Math.Min(z0, p.Zmm)
        Next
        Ok("bare splice end within S_l/2", z0 <= 50.8 + 0.01)
        EqS("bare-splice wall has no violations", ViolationCodes(r2), "")

        '--------------------------------------------------------------------
        Grp("Interior floor -- zero pitch slack")
        Dim mf As New ModuleInput()
        mf.Component = "INTERIOR_FLOOR"
        mf.Geometry = "FLAT"
        mf.WscMm = 609.6
        mf.PlateWidthMm = 609.6 * 2.0
        mf.PlateLengthMm = 5000.0
        mf.TermStart = "SPLICE"
        mf.TermEnd = "SPLICE"
        mf.DensityMakeup = False
        Dim rf0 As StudArrayResult = New StudArrayBuilder(t, mf).Build(New List(Of ExclusionZone))
        EqS("interior floor no violations", ViolationCodes(rf0), "")
        Eq("interior floor pitch is exactly S_l", MaxPitchOf(rf0), 76.2)
        Ok("interior floor pitch not below AISC minimum", MinPitchOf(rf0) >= 76.2 - 0.01)
        Ok("interior floor nearest neighbour >= 76.2", MinNeighbour(rf0) >= 76.2 - 0.05)

        '--------------------------------------------------------------------
        Grp("Curved SCCV wall")
        Dim mc As New ModuleInput()
        mc.Component = "SCCV_WALL"
        mc.Geometry = "CURVED_OUTER"
        mc.ElevM = -30.0
        mc.RadiusMm = 9945.0
        mc.SpanDeg = 10.0
        mc.DatumDeg = 0.0
        mc.DiaphPitchDeg = 3.5
        mc.PlateLengthMm = 3000.0
        mc.WscMm = 609.6
        mc.TermStart = "SPLICE"
        mc.TermEnd = "SPLICE"
        mc.DensityMakeup = False
        Dim rc As StudArrayResult = New StudArrayBuilder(t, mc).Build(New List(Of ExclusionZone))
        Ok("curved wall generates studs", rc.Points.Count > 0)
        Ok("curved wall nearest neighbour >= 76.2", MinNeighbour(rc) >= 76.2 - 0.05)

        Dim onRadius As Boolean = True
        Dim inward As Boolean = True
        For Each p As StudPoint In rc.Points
            Dim rr As Double = Math.Sqrt(p.Xmm * p.Xmm + p.Ymm * p.Ymm)
            If Math.Abs(rr - 9945.0) > 0.001 Then onRadius = False
            ' CURVED_OUTER: concrete is inside, so the stud axis points at the axis.
            Dim dot As Double = (p.Xmm * p.DirX + p.Ymm * p.DirY) / rr
            If dot > -0.999 Then inward = False
        Next
        Ok("curved studs lie on the faceplate radius", onRadius)
        Ok("CURVED_OUTER studs point toward the axis", inward)

        ' 3.5 deg at the member centreline r = 9945 - 610 = 9335 gives 570.3 mm,
        ' inside the 609.6 mm maximum, so this must not raise a Wsc violation.
        Ok("curved Wsc note recorded", rc.Notes.Count > 0)
        Ok("curved wall has no Wsc violation", ViolationCodes(rc).IndexOf("WSC_EXCEEDS") < 0)

        '--------------------------------------------------------------------
        Grp("Radial interior floor ring -- band degradation")
        Dim ma As New ModuleInput()
        ma.Component = "INTERIOR_FLOOR"
        ma.Geometry = "ANNULAR"
        ma.RInnerMm = 3000.0
        ma.ROuterMm = 17000.0
        ma.DiaphPitchDeg = 2.0
        ma.SpanDeg = 2.0
        ma.DatumDeg = 0.0
        ma.TermStart = "SPLICE"
        ma.TermEnd = "SPLICE"
        ma.DensityMakeup = False
        Dim ra As StudArrayResult = New StudArrayBuilder(t, ma).Build(New List(Of ExclusionZone))

        Ok("ring generates studs", ra.Points.Count > 200)
        EqS("ring has no violations", ViolationCodes(ra), "")
        Eq("ring radial pitch is exactly S_l", MaxPitchOf(ra), 76.2)
        Ok("ring radial pitch not below minimum", MinPitchOf(ra) >= 76.2 - 0.01)
        Ok("ring nearest neighbour >= 76.2", MinNeighbour(ra) >= 76.2 - 0.05)
        Ok("ring has a 2-row region", MinRadiusOfLine(ra, 2) > 0.0)
        Ok("2nd row only outboard of r=11640", MinRadiusOfLine(ra, 2) > 11640.0)
        Ok("innermost stud outboard of r=4366", MinRadiusOfLine(ra, 1) > 4366.0)
        Dim flatZ As Boolean = True
        For Each p As StudPoint In ra.Points
            If Math.Abs(p.Zmm) > 0.0001 OrElse Math.Abs(p.DirZ - 1.0) > 0.0001 Then flatZ = False
        Next
        Ok("ring studs are vertical on the floor plane", flatZ)

        ' Outer cavity above the table maximum must be reported, not silently placed.
        Dim mb As New ModuleInput()
        mb.Component = "INTERIOR_FLOOR" : mb.Geometry = "ANNULAR"
        mb.RInnerMm = 3000.0 : mb.ROuterMm = 20000.0
        mb.DiaphPitchDeg = 2.0 : mb.SpanDeg = 2.0
        mb.TermStart = "SPLICE" : mb.TermEnd = "SPLICE" : mb.DensityMakeup = False
        Dim rb As StudArrayResult = New StudArrayBuilder(t, mb).Build(New List(Of ExclusionZone))
        Ok("over-wide outer cavity is flagged", ViolationCodes(rb).IndexOf("WSC_EXCEEDS_TABLE_MAX") >= 0)


        '--------------------------------------------------------------------
        Grp("Geolocated module (origin offset)")
        Dim mg As New ModuleInput()
        mg.Component = "OUTER_BASEMAT" : mg.Geometry = "ANNULAR"
        mg.RInnerMm = 6000.0 : mg.ROuterMm = 14000.0
        mg.DiaphPitchDeg = 2.0 : mg.SpanDeg = 2.0 : mg.DatumDeg = 0.0
        mg.TermStart = "SPLICE" : mg.TermEnd = "SPLICE" : mg.DensityMakeup = False
        Dim rNoOff As StudArrayResult = New StudArrayBuilder(t, mg).Build(New List(Of ExclusionZone))

        mg.OriginXMm = 250000.0 : mg.OriginYMm = -180000.0 : mg.OriginZMm = -34000.0
        Dim rOff As StudArrayResult = New StudArrayBuilder(t, mg).Build(New List(Of ExclusionZone))

        Eq("offset does not change stud count", rOff.Points.Count, rNoOff.Points.Count)
        Ok("offset produces no new violations", rOff.Violations.Count = 0)
        Dim shifted As Boolean = True
        Dim localKept As Boolean = True
        For i As Integer = 0 To rNoOff.Points.Count - 1
            Dim a As StudPoint = rNoOff.Points(i)
            Dim b As StudPoint = rOff.Points(i)
            If Math.Abs((b.Xmm - a.Xmm) - 250000.0) > 0.001 Then shifted = False
            If Math.Abs((b.Ymm - a.Ymm) - (-180000.0)) > 0.001 Then shifted = False
            If Math.Abs((b.Zmm - a.Zmm) - (-34000.0)) > 0.001 Then shifted = False
            ' Keep-outs are authored module-locally, so u/v must NOT move.
            If Math.Abs(b.Umm - a.Umm) > 0.001 OrElse Math.Abs(b.Vmm - a.Vmm) > 0.001 Then localKept = False
        Next
        Ok("every stud shifts by exactly the offset", shifted)
        Ok("keep-out coordinates stay module-local", localKept)
        ' Radii must still be measured from the ring centre, not the assembly origin.
        Dim rMin As Double = Double.MaxValue
        For Each pt As StudPoint In rOff.Points
            Dim dx As Double = pt.Xmm - 250000.0
            Dim dy As Double = pt.Ymm - (-180000.0)
            rMin = Math.Min(rMin, Math.Sqrt(dx * dx + dy * dy))
        Next
        Ok("radii still measured from the ring centre", rMin >= 6000.0 - 0.01)

        '--------------------------------------------------------------------
        Grp("Exclusion zones")
        Dim zones As New List(Of ExclusionZone)
        Dim hole As New ExclusionZone()
        hole.Kind = "CIRCLE" : hole.U = 304.8 : hole.V = 1000.0 : hole.R = 183.0
        hole.Label = "flow hole"
        zones.Add(hole)

        Dim mExc As New ModuleInput()
        mExc.Component = "SCCV_WALL" : mExc.Geometry = "FLAT" : mExc.ElevM = -30.0
        mExc.WscMm = 609.6 : mExc.PlateWidthMm = 609.6 : mExc.PlateLengthMm = 2000.0
        mExc.TermStart = "SPLICE" : mExc.TermEnd = "SPLICE" : mExc.DensityMakeup = False
        Dim rExc As StudArrayResult = New StudArrayBuilder(t, mExc).Build(zones)
        Ok("exclusion removed studs", rExc.Excluded > 0)

        Dim needClear As Double = 38.1 + 19.1 / 2.0
        Dim allClear As Boolean = True
        For Each p As StudPoint In rExc.Points
            Dim d As Double = Math.Sqrt((p.Umm - hole.U) ^ 2 + (p.Vmm - hole.V) ^ 2) - hole.R
            If d < needClear - 0.001 Then allClear = False
        Next
        Ok("survivors clear the hole by 38.1 mm at the stud base edge", allClear)

        ' Rectangular zone, rotated, must also cut.
        Dim tiePlate As New ExclusionZone()
        tiePlate.Kind = "RECT" : tiePlate.U = 304.8 : tiePlate.V = 600.0
        tiePlate.W = 31.8 : tiePlate.H = 400.0 : tiePlate.AngleDeg = 30.0
        Eq("rect SDF at centre is negative", Math.Sign(tiePlate.SignedDistance(304.8, 600.0)), -1)
        Ok("rect SDF far away is positive", tiePlate.SignedDistance(304.8, 2000.0) > 0.0)
        Eq("circle SDF on the boundary is zero", hole.SignedDistance(304.8 + 183.0, 1000.0), 0.0, 0.001)

        '--------------------------------------------------------------------
        Grp("Density make-up (T1-7 n7)")
        Dim bigHole As New ExclusionZone()
        bigHole.Kind = "CIRCLE" : bigHole.U = 304.8 : bigHole.V = 1000.0 : bigHole.R = 150.0
        Dim zones2 As New List(Of ExclusionZone)
        zones2.Add(bigHole)

        Dim mD As New ModuleInput()
        mD.Component = "INTERIOR_FLOOR" : mD.Geometry = "FLAT"
        mD.WscMm = 609.6 : mD.PlateWidthMm = 609.6 : mD.PlateLengthMm = 2000.0
        mD.TermStart = "SPLICE" : mD.TermEnd = "SPLICE"
        mD.DensityMakeup = True
        Dim rD As StudArrayResult = New StudArrayBuilder(t, mD).Build(zones2)
        Ok("make-up pass ran", rD.Excluded > 0)
        Ok("make-up studs are tagged", rD.MadeUp = 0 OrElse rD.Points.Exists(Function(p) p.Source.StartsWith("MAKE-UP")))

        Dim mkClear As Boolean = True
        For Each p As StudPoint In rD.Points
            Dim d As Double = Math.Sqrt((p.Umm - bigHole.U) ^ 2 + (p.Vmm - bigHole.V) ^ 2) - bigHole.R
            If d < needClear - 0.001 Then mkClear = False
        Next
        Ok("make-up studs still respect the hole clearance", mkClear)
        Ok("make-up studs respect min spacing", MinNeighbour(rD) >= 76.2 - 0.05)

        '--------------------------------------------------------------------
        Grp("Inventor placement (stubbed API)")
        Dim inv As New Inventor.Application()
        Dim asmDoc As New Inventor.AssemblyDocument()
        Dim studPath As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StudPlacerTestStud.ipt")
        System.IO.File.WriteAllText(studPath, "stub")

        Dim msg As String = ""
        Dim placed As Integer = StudPlacer.Place(inv, asmDoc, studPath, r.Points, 100000, msg)
        Eq("occurrence count matches stud count", placed, r.Points.Count)
        Eq("occurrences in the assembly", asmDoc.ComponentDefinition.Occurrences.Count, r.Points.Count)

        Dim occ1 As Inventor.ComponentOccurrence = asmDoc.ComponentDefinition.Occurrences.Item(1)
        Ok("occurrence named with the STUD_ prefix", occ1.Name.StartsWith("STUD_"))
        Ok("occurrence grounded", occ1.Grounded)

        ' Inventor's internal length unit is centimetres: mm must be divided by 10 exactly once.
        Dim p1 As StudPoint = r.Points(0)
        Eq("matrix origin X in cm", occ1.Placement.Origin.X, p1.Xmm / 10.0, 0.000001)
        Eq("matrix origin Y in cm", occ1.Placement.Origin.Y, p1.Ymm / 10.0, 0.000001)
        Eq("matrix origin Z in cm", occ1.Placement.Origin.Z, p1.Zmm / 10.0, 0.000001)

        ' Local +Z must be the stud axis, and the frame must be right-handed orthonormal.
        Dim ax As Inventor.Vector = occ1.Placement.AxisX
        Dim ay As Inventor.Vector = occ1.Placement.AxisY
        Dim az As Inventor.Vector = occ1.Placement.AxisZ
        Eq("local Z is the stud direction X", az.X, p1.DirX, 0.000001)
        Eq("local Z is the stud direction Y", az.Y, p1.DirY, 0.000001)
        Eq("local Z is the stud direction Z", az.Z, p1.DirZ, 0.000001)
        Eq("axisX unit length", Math.Sqrt(ax.X ^ 2 + ax.Y ^ 2 + ax.Z ^ 2), 1.0, 0.000001)
        Eq("axisY unit length", Math.Sqrt(ay.X ^ 2 + ay.Y ^ 2 + ay.Z ^ 2), 1.0, 0.000001)
        Eq("axisZ unit length", Math.Sqrt(az.X ^ 2 + az.Y ^ 2 + az.Z ^ 2), 1.0, 0.000001)
        Eq("axisX . axisY orthogonal", ax.X * ay.X + ax.Y * ay.Y + ax.Z * ay.Z, 0.0, 0.000001)
        Eq("axisX . axisZ orthogonal", ax.X * az.X + ax.Y * az.Y + ax.Z * az.Z, 0.0, 0.000001)
        Eq("axisY . axisZ orthogonal", ay.X * az.X + ay.Y * az.Y + ay.Z * az.Z, 0.0, 0.000001)
        Dim crX As Double = ax.Y * ay.Z - ax.Z * ay.Y
        Dim crY As Double = ax.Z * ay.X - ax.X * ay.Z
        Dim crZ As Double = ax.X * ay.Y - ax.Y * ay.X
        Eq("frame is right-handed", crX * az.X + crY * az.Y + crZ * az.Z, 1.0, 0.000001)

        ' Re-running must replace, not duplicate.
        Dim placed2 As Integer = StudPlacer.Place(inv, asmDoc, studPath, r.Points, 100000, msg)
        Eq("re-run is idempotent", asmDoc.ComponentDefinition.Occurrences.Count, r.Points.Count)

        ' UI state must be restored.
        Ok("screen updating restored", inv.ScreenUpdating)
        Ok("user interaction restored", Not inv.UserInterfaceManager.UserInteractionDisabled)

        ' Guard must refuse and say so rather than hanging Inventor.
        Dim msg2 As String = ""
        Dim placed3 As Integer = StudPlacer.Place(inv, asmDoc, studPath, r.Points, 10, msg2)
        Eq("occurrence guard blocks placement", placed3, 0)
        Ok("guard explains itself", msg2.IndexOf("guard") >= 0)

        threw = False
        Try
            StudPlacer.Place(inv, asmDoc, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "does_not_exist.ipt"), r.Points, 100, msg)
        Catch ex As Exception
            threw = True
        End Try
        Ok("missing stud part raises", threw)


        ' A basemat needs studs on both faceplates. Two runs with distinct
        ' prefixes must coexist; sharing one prefix wipes the first face.
        Dim asmBoth As New Inventor.AssemblyDocument()
        StudPlacer.Place(inv, asmBoth, studPath, r.Points, 100000, msg, "STUD_BOT_")
        StudPlacer.Place(inv, asmBoth, studPath, rf0.Points, 100000, msg, "STUD_TOP_")
        Eq("both faces coexist under distinct prefixes",
           asmBoth.ComponentDefinition.Occurrences.Count, r.Points.Count + rf0.Points.Count)
        Dim bot As Integer = 0, top As Integer = 0
        For i As Integer = 1 To asmBoth.ComponentDefinition.Occurrences.Count
            Dim nm As String = asmBoth.ComponentDefinition.Occurrences.Item(i).Name
            If nm.StartsWith("STUD_BOT_") Then bot += 1
            If nm.StartsWith("STUD_TOP_") Then top += 1
        Next
        Eq("bottom face intact", bot, r.Points.Count)
        Eq("top face intact", top, rf0.Points.Count)
        ' Re-running one face must replace only that face.
        StudPlacer.Place(inv, asmBoth, studPath, r.Points, 100000, msg, "STUD_BOT_")
        Eq("re-running one face leaves the other alone",
           asmBoth.ComponentDefinition.Occurrences.Count, r.Points.Count + rf0.Points.Count)

        ' Curved studs must produce valid frames too.
        Dim asm2 As New Inventor.AssemblyDocument()
        StudPlacer.Place(inv, asm2, studPath, rc.Points, 100000, msg)
        Dim occC As Inventor.ComponentOccurrence = asm2.ComponentDefinition.Occurrences.Item(1)
        Dim cax As Inventor.Vector = occC.Placement.AxisX
        Dim caz As Inventor.Vector = occC.Placement.AxisZ
        Eq("curved axisX unit length", Math.Sqrt(cax.X ^ 2 + cax.Y ^ 2 + cax.Z ^ 2), 1.0, 0.000001)
        Eq("curved axisX . axisZ orthogonal", cax.X * caz.X + cax.Y * caz.Y + cax.Z * caz.Z, 0.0, 0.000001)

        '--------------------------------------------------------------------
        Grp("CSV export")
        Dim outDir As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "studplacer_test")
        System.IO.Directory.CreateDirectory(outDir)
        Dim schedPath As String = System.IO.Path.Combine(outDir, "sched.csv")
        Dim compatPath As String = System.IO.Path.Combine(outDir, "compat.csv")
        StudPlacer.ExportSchedule(schedPath, m, r, "008N9536 Rev 11")
        StudPlacer.ExportStudlyCompat(compatPath, m, r, "PN-123", "SCCV wall")

        Dim sched As String = System.IO.File.ReadAllText(schedPath)
        Ok("schedule has a header block", sched.IndexOf("HEADER,MODULE_ID,TEST-SCCV") >= 0)
        Ok("schedule reports the rule source", sched.IndexOf("HEADER,RULE_SOURCE") >= 0)
        Ok("schedule reports the derived S_t", sched.IndexOf("HEADER,S_T_NOMINAL_MM,203.2") >= 0)
        Ok("schedule reports the total", sched.IndexOf("HEADER,TOTAL_STUDS," & r.Points.Count) >= 0)
        Ok("schedule has the column header", sched.IndexOf("INDEX,CHANNEL,LINE,STATION") >= 0)
        Dim schedRows As Integer = 0
        For Each ln As String In System.IO.File.ReadAllLines(schedPath)
            If ln.Length > 0 AndAlso Char.IsDigit(ln(0)) AndAlso ln.IndexOf(",") > 0 Then schedRows += 1
        Next
        Eq("schedule row per stud", schedRows, r.Points.Count)

        Dim compat As String = System.IO.File.ReadAllText(compatPath)
        Ok("legacy CSV keeps the Studly header", compat.StartsWith("Type,Index,Field,Value"))
        Ok("legacy CSV Assembly_Type 1 for flat", compat.IndexOf("Assembly_Type,,,1") >= 0)
        Ok("legacy CSV total matches", compat.IndexOf("Total_Studs,,," & r.Points.Count) >= 0)
        Dim rollLines As Integer = 0
        For Each ln As String In System.IO.File.ReadAllLines(compatPath)
            If ln.IndexOf(",Roll,") >= 0 Then rollLines += 1
        Next
        Eq("legacy CSV one Roll row per stud", rollLines, r.Points.Count)

        ' Round-trip the exclusion sample through the loader.
        Dim sampleExcl As String = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(rulesDir), "samples", "exclusions_example.csv")
        If System.IO.File.Exists(sampleExcl) Then
            Dim loaded As List(Of ExclusionZone) = StudPlacer.LoadExclusions(sampleExcl)
            Eq("sample exclusion file parses", loaded.Count, 5)
            EqS("first zone kind", loaded(0).Kind, "CIRCLE")
            Eq("airlock extra clearance parsed", loaded(4).Extra, 22.2)
        Else
            Console.WriteLine("   (skipped sample exclusion round-trip; file not found)")
        End If
        Eq("missing exclusion file yields empty list",
           StudPlacer.LoadExclusions(System.IO.Path.Combine(outDir, "nope.csv")).Count, 0)

        '--------------------------------------------------------------------
        Grp("Locale safety")
        ' A comma-decimal locale must not change how the rule tables parse.
        Dim prior As System.Globalization.CultureInfo = System.Threading.Thread.CurrentThread.CurrentCulture
        Try
            System.Threading.Thread.CurrentThread.CurrentCulture = New System.Globalization.CultureInfo("de-DE")
            Dim t2 As RuleTables = RuleTables.Load(rulesDir)
            Eq("SCCV S_l under de-DE", t2.ResolveWall("SCCV_WALL", -30.0).SlMaxMm, 101.6)
            Eq("edge clearance under de-DE", t2.Constraints.Value("MIN_EDGE_CLEARANCE"), 38.1)
        Finally
            System.Threading.Thread.CurrentThread.CurrentCulture = prior
        End Try


        '--------------------------------------------------------------------
        Grp("Missing-install diagnostics")
        Dim badDir As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "studplacer_no_such_rules")
        If System.IO.Directory.Exists(badDir) Then System.IO.Directory.Delete(badDir, True)
        Dim msgMissing As String = ""
        Try
            RuleTables.Load(badDir)
        Catch ex As Exception
            msgMissing = ex.Message
        End Try
        Ok("missing rules dir raises", msgMissing.Length > 0)
        Ok("message names the folder searched", msgMissing.IndexOf(badDir) >= 0)
        Ok("message says the folder does not exist", msgMissing.IndexOf("does not exist") >= 0)
        Ok("message names all three tables",
           msgMissing.IndexOf("table_1_6_walls.csv") >= 0 AndAlso
           msgMissing.IndexOf("table_1_7_floors.csv") >= 0 AndAlso
           msgMissing.IndexOf("global_constraints.csv") >= 0)
        Ok("message points at STUD_RULES_DIR", msgMissing.IndexOf("STUD_RULES_DIR") >= 0)

        ' Folder present but a table missing -- the half-copied install case.
        Dim partialDir As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "studplacer_partial_rules")
        If System.IO.Directory.Exists(partialDir) Then System.IO.Directory.Delete(partialDir, True)
        System.IO.Directory.CreateDirectory(partialDir)
        System.IO.File.Copy(System.IO.Path.Combine(rulesDir, "global_constraints.csv"),
                            System.IO.Path.Combine(partialDir, "global_constraints.csv"), True)
        Dim msgPartial As String = ""
        Try
            RuleTables.Load(partialDir)
        Catch ex As Exception
            msgPartial = ex.Message
        End Try
        Ok("partial install raises", msgPartial.Length > 0)
        Ok("partial message says the folder exists", msgPartial.IndexOf("Folder exists") >= 0)
        Ok("partial message lists only what is missing",
           msgPartial.IndexOf("table_1_6_walls.csv") >= 0 AndAlso
           msgPartial.IndexOf("Folder exists, but these files are missing:") >= 0)
        System.IO.Directory.Delete(partialDir, True)


        ' Simulate the real-world slip: tables sitting beside the install rather
        ' than in <root>\rules. The message must point at where they actually are.
        Dim fakeRoot As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "studplacer_findtest")
        If System.IO.Directory.Exists(fakeRoot) Then System.IO.Directory.Delete(fakeRoot, True)
        Dim strayDir As String = System.IO.Path.Combine(fakeRoot, "package", "rules")
        System.IO.Directory.CreateDirectory(strayDir)
        For Each f As String In RuleTables.RequiredFiles
            System.IO.File.Copy(System.IO.Path.Combine(rulesDir, f), System.IO.Path.Combine(strayDir, f), True)
        Next
        Dim wantedDir As String = System.IO.Path.Combine(fakeRoot, "rules")

        Dim cands As List(Of String) = RuleTables.FindCandidates(wantedDir)
        Ok("discovery finds a misplaced rules folder", cands.Contains(strayDir))

        Dim msgFound As String = ""
        Try
            RuleTables.Load(wantedDir)
        Catch ex As Exception
            msgFound = ex.Message
        End Try
        Ok("message reports where the tables actually are", msgFound.IndexOf(strayDir) >= 0)
        Ok("message offers the STUD_RULES_DIR route", msgFound.IndexOf("STUD_RULES_DIR on the assembly to that path") >= 0)

        ' Nothing nearby -> no misleading suggestion block.
        Dim lonely As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "studplacer_lonely", "rules")
        Dim msgLonely As String = ""
        Try
            RuleTables.Load(lonely)
        Catch ex As Exception
            msgLonely = ex.Message
        End Try
        Ok("no suggestion when nothing is found", msgLonely.IndexOf("Found the code tables") < 0)
        Ok("discovery never throws on a bogus path", RuleTables.FindCandidates("Z:\no\such\place").Count >= 0)

        ' Path walk: build a real tree, ask for a path one level too deep, and
        ' check the message pinpoints the break AND lists the real contents.
        Dim walkRoot As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "studplacer_walk")
        If System.IO.Directory.Exists(walkRoot) Then System.IO.Directory.Delete(walkRoot, True)
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(walkRoot, "Inventor", "StudPlacer", "vb"))
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(walkRoot, "Inventor", "StudPlacer", "ilogic"))
        Dim askedFor As String = System.IO.Path.Combine(walkRoot, "Inventor", "StudPlacer", "rules")
        Dim walk As String = RuleTables.DescribePath(askedFor)
        Ok("walk marks the existing ancestors", walk.IndexOf("exists   ") >= 0)
        Ok("walk pinpoints where it breaks", walk.IndexOf("stops here") >= 0)
        Ok("walk names the missing segment", walk.IndexOf(askedFor) >= 0)
        Ok("walk lists what is actually there", walk.IndexOf("[dir]  vb") >= 0 AndAlso walk.IndexOf("[dir]  ilogic") >= 0)
        Dim walkMsg As String = ""
        Try
            RuleTables.Load(askedFor)
        Catch ex As Exception
            walkMsg = ex.Message
        End Try
        Ok("load error embeds the path walk", walkMsg.IndexOf("Walking that path one folder at a time") >= 0)

        Console.WriteLine("   path-walk diagnostic:")
        For Each ln As String In walk.Replace(vbCr, "").Split(CChar(vbLf))
            If ln.Trim() <> "" Then Console.WriteLine("     | " & ln)
        Next

        ' Hidden-extension trap: Explorer shows "x.csv", the file is "x.csv.txt".
        Dim trapDir As String = System.IO.Path.Combine(walkRoot, "traprules")
        System.IO.Directory.CreateDirectory(trapDir)
        For Each f As String In RuleTables.RequiredFiles
            System.IO.File.Copy(System.IO.Path.Combine(rulesDir, f), System.IO.Path.Combine(trapDir, f), True)
        Next
        System.IO.File.Move(System.IO.Path.Combine(trapDir, "table_1_6_walls.csv"),
                            System.IO.Path.Combine(trapDir, "table_1_6_walls.csv.txt"))
        Dim trapMsg As String = ""
        Try
            RuleTables.Load(trapDir)
        Catch ex As Exception
            trapMsg = ex.Message
        End Try
        Ok("extension trap is named", trapMsg.IndexOf("table_1_6_walls.csv.txt") >= 0)
        Ok("extension trap is explained", trapMsg.IndexOf("hides known file extensions") >= 0)
        System.IO.Directory.Delete(walkRoot, True)

        Console.WriteLine("   message when the tables are found elsewhere:")
        For Each ln As String In msgFound.Replace(vbCr, "").Split(CChar(vbLf))
            Console.WriteLine("     | " & ln)
        Next
        System.IO.Directory.Delete(fakeRoot, True)


        '--------------------------------------------------------------------
        Console.WriteLine()
        Console.WriteLine("=====================================================")
        Console.WriteLine(" " & Passed & " passed, " & Failed & " failed")
        Console.WriteLine("=====================================================")
        If Failed > 0 Then
            System.Environment.Exit(1)
        End If
    End Sub

End Module
