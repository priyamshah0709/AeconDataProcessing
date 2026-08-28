' =============================================================================
'  StudArray.vb  --  array generation, clearance filtering, density make-up and
'                    code validation.  Pure geometry/logic, no Inventor API.
'
'  Loaded into an iLogic rule with:   AddVbFile "StudArray.vb"
'  Requires StudRules.vb.
'
'  TWO COORDINATE SYSTEMS, ON PURPOSE
'  ----------------------------------
'  (Long, Trans)  Channel coordinates. Long runs ALONG the diaphragm channel,
'                 Trans runs ACROSS it measured from the channel's own datum
'                 diaphragm. S_l is a Long pitch, S_t is a Trans pitch, and the
'                 305 mm density window of T1-7 n7/n8 is a Long window.
'                 For a radial floor Long is the RADIUS -- which is why this is
'                 kept separate from anything Cartesian.
'
'  (U, V)         Faceplate surface coordinates, used only for keep-out testing
'                 against flow holes, sleeves and tie plates.
'                   FLAT      u = Y, v = Z
'                   CURVED_*  u = arc length from the datum angle, v = Z
'                   ANNULAR   u = X, v = Y  (plan view -- the floor IS flat)
'
'  MODULE COORDINATE CONVENTIONS  (all millimetres)
'  ------------------------------
'  FLAT           X = faceplate normal, studs grow along +X into the concrete.
'                 Faceplate inner face at X = 0. Y = transverse, Z = longitudinal.
'  CURVED_OUTER   Faceplate at radius R about global Z; concrete INSIDE, so the
'                 studs point toward the axis.
'  CURVED_INNER   Concrete OUTSIDE, so the studs point away from the axis.
'                 Angle sweeps from DatumDeg by -SpanDeg (Studly convention).
'  ANNULAR        Flat ring floor with RADIAL diaphragms; studs point +Z (or -Z
'                 for a bottom faceplate). Cavity width w = r * pitch therefore
'                 GROWS with radius, so the Table 1-7 band is re-resolved at
'                 every radial station.
' =============================================================================
Imports System
Imports System.Collections.Generic
Imports System.Globalization

Public Class StudPoint
    Public Channel As Integer          ' index of the diaphragm cavity
    Public Line As Integer             ' stud line within the cavity (1..n); 0 = make-up
    Public Station As Integer          ' index along the channel; 0 = make-up
    Public LongMm As Double            ' along the channel (radius, for ANNULAR)
    Public TransMm As Double           ' across the channel, from its datum diaphragm
    Public Xmm As Double
    Public Ymm As Double
    Public Zmm As Double
    Public DirX As Double
    Public DirY As Double
    Public DirZ As Double
    Public Umm As Double               ' surface coordinate, for keep-out tests only
    Public Vmm As Double
    Public DiaMm As Double
    Public LenMm As Double
    Public RollDeg As Double = 0.0
    Public Source As String = ""       ' rule provenance
End Class

Public Class ExclusionZone
    Public Kind As String = "CIRCLE"   ' CIRCLE | RECT
    Public U As Double
    Public V As Double
    Public R As Double = 0.0
    Public W As Double = 0.0
    Public H As Double = 0.0
    Public AngleDeg As Double = 0.0
    Public Extra As Double = 0.0       ' extra clearance beyond the global 38.1 mm
    Public Label As String = ""

    ''' <summary>
    ''' Signed distance from a query point to the zone boundary. Negative = inside.
    '''
    ''' The parameters are pu/pv, NOT u/v: VB identifiers are case-insensitive, so
    ''' parameters named u and v would shadow the U and V fields and every
    ''' difference would silently evaluate to zero -- which reads as "the stud is
    ''' dead centre in the keep-out zone" and deletes the entire array.
    ''' </summary>
    Public Function SignedDistance(ByVal pu As Double, ByVal pv As Double) As Double
        If Kind.ToUpperInvariant() = "CIRCLE" Then
            Dim du As Double = pu - U
            Dim dv As Double = pv - V
            Return Math.Sqrt(du * du + dv * dv) - R
        End If
        ' Rotate the query point into the rectangle's own frame.
        Dim a As Double = AngleDeg * Math.PI / 180.0
        Dim ca As Double = Math.Cos(-a)
        Dim sa As Double = Math.Sin(-a)
        Dim lu As Double = (pu - U) * ca - (pv - V) * sa
        Dim lv As Double = (pu - U) * sa + (pv - V) * ca
        Dim qu As Double = Math.Abs(lu) - W / 2.0
        Dim qv As Double = Math.Abs(lv) - H / 2.0
        Dim outU As Double = Math.Max(qu, 0.0)
        Dim outV As Double = Math.Max(qv, 0.0)
        Return Math.Sqrt(outU * outU + outV * outV) + Math.Min(Math.Max(qu, qv), 0.0)
    End Function
End Class

Public Class Violation
    Public Code As String = ""
    Public Detail As String = ""
    Public Umm As Double = 0.0
    Public Vmm As Double = 0.0
    Public Sub New(ByVal c As String, ByVal d As String, ByVal u As Double, ByVal v As Double)
        Code = c : Detail = d : Umm = u : Vmm = v
    End Sub
End Class

Public Class ModuleInput
    ' identity
    Public ModuleId As String = ""
    Public Component As String = ""
    Public Geometry As String = "FLAT"          ' FLAT | CURVED_INNER | CURVED_OUTER | ANNULAR
    Public ElevM As Double = 0.0

    ' geometry -- FLAT
    Public PlateWidthMm As Double = 0.0
    Public PlateLengthMm As Double = 0.0
    Public WscMm As Double = 0.0
    Public DatumOffsetMm As Double = 0.0

    ' geometry -- CURVED
    Public RadiusMm As Double = 0.0
    Public SpanDeg As Double = 0.0
    Public DatumDeg As Double = 0.0
    Public DiaphPitchDeg As Double = 0.0

    ' geometry -- ANNULAR
    Public RInnerMm As Double = 0.0
    Public ROuterMm As Double = 0.0

    ' behaviour
    Public DatumAlign As String = "DIAPHRAGM"   ' DIAPHRAGM | STUD
    Public TermStart As String = "SPLICE"       ' LANDING_PLATE | COVER_PLATE | SPLICE | FREE_EDGE
    Public TermEnd As String = "SPLICE"
    Public SpliceIdStart As String = ""         ' A | B | ""
    Public SpliceIdEnd As String = ""
    Public TransverseMode As String = "EVEN"    ' EVEN | RADIAL_CLEARANCE
    Public StudDirSign As Double = 1.0
    Public FaceOffsetMm As Double = 0.0
    Public DensityMakeup As Boolean = True
End Class

Public Class StudArrayResult
    Public Points As List(Of StudPoint) = New List(Of StudPoint)
    Public Violations As List(Of Violation) = New List(Of Violation)
    Public Notes As List(Of String) = New List(Of String)
    Public Spec As StudSpec = Nothing
    Public MadeUp As Integer = 0
    Public Excluded As Integer = 0
End Class

Public Class StudArrayBuilder
    Private tables As RuleTables
    Private cons As StudConstraints
    Private inp As ModuleInput
    Private res As StudArrayResult
    Private geo As String

    ' Per-channel datum:
    '   FLAT     -> Y of the cavity's lower diaphragm
    '   CURVED   -> angle (rad) of the cavity's leading diaphragm; sweep is negative
    '   ANNULAR  -> angle (rad) of the cavity's lower diaphragm
    Private channelBase As Dictionary(Of Integer, Double) = New Dictionary(Of Integer, Double)

    Public Sub New(ByVal ruleTables As RuleTables, ByVal moduleInput As ModuleInput)
        tables = ruleTables
        cons = ruleTables.Constraints
        inp = moduleInput
        geo = inp.Geometry.Trim().ToUpperInvariant()
        res = New StudArrayResult()
    End Sub

    ' ---------------------------------------------------------------- helpers
    Private Function MinSpacing(ByVal dia As Double) As Double
        Return cons.MinSpacingFor(dia)
    End Function

    ''' <summary>
    ''' The code measures from the EDGE of the stud base, so convert to a
    ''' centre-to-boundary distance before comparing against geometry.
    ''' </summary>
    Private Function EdgeClear(ByVal dia As Double) As Double
        Return cons.Value("MIN_EDGE_CLEARANCE") + dia / 2.0
    End Function

    Private Function PitchRad() As Double
        Return inp.DiaphPitchDeg * Math.PI / 180.0
    End Function

    ''' <summary>Cavity width across the channel at a given along-channel position.</summary>
    Private Function CavityWidthAt(ByVal longMm As Double) As Double
        If geo = "FLAT" Then Return inp.WscMm
        If geo = "ANNULAR" Then Return longMm * PitchRad()
        Return inp.RadiusMm * PitchRad()          ' curved: faceplate arc length
    End Function

    ''' <summary>
    ''' Build a stud from channel coordinates.  This is the single place where
    ''' (channel, along, across) becomes 3D, so make-up studs and array studs
    ''' can never disagree about where the surface is.
    ''' </summary>
    Private Function MakePoint(ByVal ch As Integer, ByVal longMm As Double, ByVal transMm As Double,
                               ByVal spec As StudSpec) As StudPoint
        Dim p As New StudPoint()
        p.Channel = ch
        p.LongMm = longMm
        p.TransMm = transMm
        p.DiaMm = spec.StudDiaMm
        p.LenMm = spec.StudLenMm
        p.Source = spec.Source

        Dim base_ As Double = 0.0
        If channelBase.ContainsKey(ch) Then base_ = channelBase(ch)

        If geo = "FLAT" Then
            p.Xmm = inp.FaceOffsetMm
            p.Ymm = base_ + transMm
            p.Zmm = longMm
            p.DirX = inp.StudDirSign : p.DirY = 0.0 : p.DirZ = 0.0
            p.RollDeg = 0.0
            p.Umm = p.Ymm : p.Vmm = p.Zmm

        ElseIf geo = "ANNULAR" Then
            Dim r As Double = longMm
            Dim ang As Double = base_ + (transMm / Math.Max(r, 0.000001))
            p.Xmm = r * Math.Cos(ang)
            p.Ymm = r * Math.Sin(ang)
            p.Zmm = inp.FaceOffsetMm
            p.DirX = 0.0 : p.DirY = 0.0 : p.DirZ = inp.StudDirSign
            p.RollDeg = ang * 180.0 / Math.PI
            p.Umm = p.Xmm : p.Vmm = p.Ymm

        Else    ' CURVED_INNER / CURVED_OUTER -- sweep runs negative from the datum
            Dim datum As Double = inp.DatumDeg * Math.PI / 180.0
            Dim ang As Double = base_ - (transMm / inp.RadiusMm)
            p.Xmm = inp.RadiusMm * Math.Cos(ang)
            p.Ymm = inp.RadiusMm * Math.Sin(ang)
            p.Zmm = longMm
            Dim sgn As Double = If(geo = "CURVED_INNER", 1.0, -1.0) * inp.StudDirSign
            p.DirX = sgn * Math.Cos(ang) : p.DirY = sgn * Math.Sin(ang) : p.DirZ = 0.0
            p.RollDeg = ang * 180.0 / Math.PI
            p.Umm = (datum - ang) * inp.RadiusMm
            p.Vmm = longMm
        End If
        Return p
    End Function

    ''' <summary>
    ''' Maximum distance from a channel-end boundary to the first stud station.
    ''' Wall (Detail 3): landing plate -> S_l, bare splice -> S_l/2.
    ''' Floor (Detail 2): cover plate  -> S_l, bare splice -> S_l/2.
    ''' Splice A and Splice B carry explicit dimensions from the same details.
    '''
    ''' hardMin returns the smallest legal distance at that boundary. The 38.1 mm
    ''' stud-base clearance of T1-6 n3 / T1-7 n5 is worded against a FREE EDGE,
    ''' a flow hole or a sleeve, and T1-7 n10 extends it to a cover plate. It is
    ''' NOT applied at a splice line or a landing plate, where the faceplate runs
    ''' continuously through the joint. Applying it there would make the interior
    ''' floor array infeasible, since S_l/2 = 38.1 mm is itself below the
    ''' centre-to-boundary equivalent of that rule.
    ''' </summary>
    Private Function TerminationAllowance(ByVal kind As String, ByVal spliceId As String,
                                          ByVal sl As Double, ByVal dia As Double,
                                          ByRef why As String, ByRef hardMin As Double) As Double
        Dim k As String = kind.Trim().ToUpperInvariant()
        Dim sid As String = spliceId.Trim().ToUpperInvariant()
        Dim a As Double
        hardMin = 0.0

        If k = "LANDING_PLATE" Then
            a = sl
            why = "LANDING_PLATE -> <= S_l (G103 Detail 3 n1)"
            If sid = "A" Then
                a = cons.Value("SPLICE_A_LANDING_PLATE_MAX")
                why = "Splice A landing plate -> " & a.ToString("0.#", CultureInfo.InvariantCulture) &
                      " mm (" & cons.Source("SPLICE_A_LANDING_PLATE_MAX") & ")"
            End If

        ElseIf k = "COVER_PLATE" Then
            a = sl
            hardMin = EdgeClear(dia)          ' T1-7 n10, stud base to cover plate
            why = "COVER_PLATE -> <= S_l (G103 Detail 2 n1), min " &
                  hardMin.ToString("0.##", CultureInfo.InvariantCulture) & " mm (T1-7 n10)"

        ElseIf k = "SPLICE" Then
            a = sl / 2.0
            why = "SPLICE (no landing plate) -> <= S_l/2 (G103 Detail 2 n1 / Detail 3 n2)"
            If sid = "B" Then
                a = cons.Value("SPLICE_B_SPLICE_LINE_MAX")
                why = "Splice B -> " & a.ToString("0.#", CultureInfo.InvariantCulture) &
                      " mm (" & cons.Source("SPLICE_B_SPLICE_LINE_MAX") & ")"
            End If

        Else
            a = sl
            hardMin = EdgeClear(dia)          ' T1-6 n3, stud base to a free plate edge
            why = "FREE_EDGE -> min edge clearance " &
                  hardMin.ToString("0.##", CultureInfo.InvariantCulture) & " mm (G103 T1-6 n3)"
        End If

        If a < hardMin Then
            res.Notes.Add("Termination allowance at " & k & " raised from " &
                          a.ToString("0.##", CultureInfo.InvariantCulture) & " to " &
                          hardMin.ToString("0.##", CultureInfo.InvariantCulture) &
                          " mm to hold the 38.1 mm stud-base edge clearance.")
            a = hardMin
            why = why & " [raised to the edge clearance]"
        End If
        Return a
    End Function

    ''' <summary>
    ''' Uniform stations along a channel.
    '''
    ''' The pitch must sit in [minSp, slMax] and the two end distances must sit
    ''' in [c, a]. That second bound matters: for interior floor slabs S_l is
    ''' 76.2 mm and the AISC minimum spacing is ALSO 76.2 mm, so there is no
    ''' slack at all -- naively spreading the remainder into the pitch drives it
    ''' below the minimum and quietly produces a non-compliant array.
    '''
    ''' Strategy: take the fewest gaps that keep the pitch at or under S_l, run
    ''' the pitch as long as legal (fewest studs), and split whatever is left
    ''' evenly between the two ends. Widen the gap count until that is feasible.
    ''' </summary>
    Private Function Stations(ByVal length As Double,
                              ByVal a0 As Double, ByVal a1 As Double,
                              ByVal c0 As Double, ByVal c1 As Double,
                              ByVal slMax As Double, ByVal minSp As Double) As List(Of Double)
        Dim list As New List(Of Double)
        If slMax <= 0.0 Then Return list

        If length < c0 + c1 - 0.001 Then
            res.Violations.Add(New Violation("CHANNEL_TOO_SHORT",
                "Channel length " & length.ToString("0.#", CultureInfo.InvariantCulture) &
                " mm cannot hold the minimum end clearances (" &
                c0.ToString("0.#", CultureInfo.InvariantCulture) & " + " &
                c1.ToString("0.#", CultureInfo.InvariantCulture) & " mm).", 0, 0))
            Return list
        End If
        If length <= a0 + a1 + 0.001 Then
            ' Short channel: one row, centred within its allowances.
            list.Add(Math.Min(Math.Max(length / 2.0, c0), a0))
            Return list
        End If

        Dim n As Integer = CInt(Math.Ceiling((length - a0 - a1) / slMax - 0.000001))
        If n < 1 Then n = 1
        Dim cap As Integer = n + 1000

        While n <= cap
            Dim pMax As Double = Math.Min(slMax, (length - c0 - c1) / CDbl(n))
            Dim pMin As Double = Math.Max(minSp, (length - a0 - a1) / CDbl(n))
            If pMax >= pMin - 0.000001 Then
                Dim p As Double = pMax                          ' longest legal pitch = fewest studs
                Dim leftover As Double = length - CDbl(n) * p
                Dim d0 As Double = leftover / 2.0
                Dim d1 As Double = leftover - d0
                If d0 > a0 Then
                    d0 = a0 : d1 = leftover - d0
                ElseIf d1 > a1 Then
                    d1 = a1 : d0 = leftover - d1
                End If
                If d0 < c0 Then
                    d0 = c0 : d1 = leftover - d0
                ElseIf d1 < c1 Then
                    d1 = c1 : d0 = leftover - d1
                End If
                For i As Integer = 0 To n
                    list.Add(d0 + CDbl(i) * p)
                Next
                Return list
            End If
            n += 1
        End While

        ' No gap count satisfies both the pitch window and the end allowances.
        res.Violations.Add(New Violation("NO_FEASIBLE_PITCH",
            "Channel length " & length.ToString("0.#", CultureInfo.InvariantCulture) &
            " mm admits no station pitch between " & minSp.ToString("0.#", CultureInfo.InvariantCulture) &
            " and " & slMax.ToString("0.#", CultureInfo.InvariantCulture) &
            " mm with end allowances " & a0.ToString("0.#", CultureInfo.InvariantCulture) & " / " &
            a1.ToString("0.#", CultureInfo.InvariantCulture) & " mm.", 0, 0))
        Return list
    End Function

    ''' <summary>
    ''' Across-channel offsets of the stud lines in a cavity of width w.
    ''' EVEN reproduces the tabulated S_t = W_sc/(n+1).  RADIAL_CLEARANCE
    ''' implements T1-7 n3: hold 152.4 mm off each diaphragm, squeeze the middle.
    ''' </summary>
    Private Function CavityLines(ByVal w As Double, ByVal n As Integer) As List(Of Double)
        Dim list As New List(Of Double)
        If n <= 0 OrElse w <= 0.0 Then Return list
        Dim even As Double = w / CDbl(n + 1)

        If inp.TransverseMode.Trim().ToUpperInvariant() = "RADIAL_CLEARANCE" Then
            Dim clear As Double = cons.Value("RADIAL_FIRST_ROW_CLEARANCE")
            If even < clear AndAlso w > 2.0 * clear Then
                If n = 1 Then
                    list.Add(w / 2.0)
                Else
                    Dim span As Double = w - 2.0 * clear
                    Dim step_ As Double = span / CDbl(n - 1)
                    For k As Integer = 0 To n - 1
                        list.Add(clear + CDbl(k) * step_)
                    Next
                End If
                Return list
            End If
        End If

        For k As Integer = 1 To n
            list.Add(CDbl(k) * even)
        Next
        Return list
    End Function

    ' ------------------------------------------------------------ array build
    Public Function Build(ByVal zones As List(Of ExclusionZone)) As StudArrayResult
        Select Case geo
            Case "FLAT"
                BuildFlat()
            Case "CURVED_INNER", "CURVED_OUTER"
                BuildCurved(geo = "CURVED_INNER")
            Case "ANNULAR"
                BuildAnnular()
            Case Else
                Throw New Exception("Unknown STUD_GEOMETRY '" & inp.Geometry & "'.")
        End Select

        If zones IsNot Nothing AndAlso zones.Count > 0 Then ApplyExclusions(zones)
        If inp.DensityMakeup AndAlso res.Spec IsNot Nothing AndAlso res.Spec.Family = "FLOOR" Then
            DensityMakeup(zones)
        End If
        Validate()
        Return res
    End Function

    Private Sub BuildFlat()
        If inp.WscMm <= 0.0 Then Throw New Exception("STUD_WSC_MM must be > 0 for FLAT geometry.")
        If inp.PlateWidthMm <= 0.0 Then Throw New Exception("STUD_PLATE_WIDTH_MM must be > 0.")
        If inp.PlateLengthMm <= 0.0 Then Throw New Exception("STUD_PLATE_LENGTH_MM must be > 0.")

        Dim spec As StudSpec = tables.Resolve(inp.Component, inp.ElevM, inp.WscMm)
        res.Spec = spec
        CheckWsc(spec, inp.WscMm)
        If spec.NoStuds Then
            res.Notes.Add("Table band resolves to NO STUDS for W_sc = " &
                          inp.WscMm.ToString("0.#", CultureInfo.InvariantCulture) & " mm. Nothing placed.")
            Return
        End If

        Dim whyA As String = "" : Dim whyB As String = ""
        Dim c0 As Double = 0.0 : Dim c1 As Double = 0.0
        Dim a0 As Double = TerminationAllowance(inp.TermStart, inp.SpliceIdStart, spec.SlMaxMm, spec.StudDiaMm, whyA, c0)
        Dim a1 As Double = TerminationAllowance(inp.TermEnd, inp.SpliceIdEnd, spec.SlMaxMm, spec.StudDiaMm, whyB, c1)
        res.Notes.Add("Start termination: " & whyA)
        res.Notes.Add("End termination: " & whyB)

        Dim sts As List(Of Double) = Stations(inp.PlateLengthMm, a0, a1, c0, c1,
                                              spec.SlMaxMm, MinSpacing(spec.StudDiaMm))
        Dim offsets As List(Of Double) = CavityLines(inp.WscMm, spec.LinesPerCavity)

        ' Diaphragm positions across the plate width.
        Dim y0 As Double = inp.DatumOffsetMm
        If inp.DatumAlign.Trim().ToUpperInvariant() = "STUD" Then
            ' The datum lands on a stud line, so step back half a cavity to the diaphragm.
            y0 = inp.DatumOffsetMm - inp.WscMm / 2.0
        End If
        While y0 > 0.0
            y0 -= inp.WscMm
        End While

        Dim dias As New List(Of Double)
        Dim y As Double = y0
        While y <= inp.PlateWidthMm + 0.001
            dias.Add(y)
            y += inp.WscMm
        End While

        For d As Integer = 0 To dias.Count - 2
            Dim ch As Integer = d + 1
            channelBase(ch) = dias(d)
            Dim li As Integer = 0
            For Each off As Double In offsets
                li += 1
                Dim yy As Double = dias(d) + off
                If yy < 0.0 OrElse yy > inp.PlateWidthMm Then Continue For
                Dim si As Integer = 0
                For Each zz As Double In sts
                    si += 1
                    Dim p As StudPoint = MakePoint(ch, zz, off, spec)
                    p.Line = li : p.Station = si
                    res.Points.Add(p)
                Next
            Next
        Next
    End Sub

    Private Sub BuildCurved(ByVal inner As Boolean)
        If inp.DiaphPitchDeg <= 0.0 Then Throw New Exception("STUD_DIAPH_PITCH_DEG must be > 0 for CURVED geometry.")
        If inp.RadiusMm <= 0.0 Then Throw New Exception("STUD_RADIUS_MM must be > 0 for CURVED geometry.")
        If inp.SpanDeg <= 0.0 Then Throw New Exception("STUD_SPAN_DEG must be > 0 for CURVED geometry.")
        If inp.PlateLengthMm <= 0.0 Then Throw New Exception("STUD_PLATE_LENGTH_MM must be > 0.")

        Dim spec As StudSpec = tables.Resolve(inp.Component, inp.ElevM, inp.WscMm)
        res.Spec = spec

        ' W_sc is measured at the MEMBER CENTRELINE, not at the faceplate.
        Dim pitch As Double = PitchRad()
        Dim rCl As Double
        If inner Then
            rCl = inp.RadiusMm + spec.TscMm / 2.0
        Else
            rCl = inp.RadiusMm - spec.TscMm / 2.0
        End If
        Dim wscGeom As Double = rCl * pitch
        res.Notes.Add("W_sc computed from geometry at the member centreline (r = " &
                      rCl.ToString("0.#", CultureInfo.InvariantCulture) & " mm, pitch = " &
                      inp.DiaphPitchDeg.ToString("0.###", CultureInfo.InvariantCulture) & " deg) = " &
                      wscGeom.ToString("0.#", CultureInfo.InvariantCulture) & " mm.")
        CheckWsc(spec, wscGeom)
        If spec.NoStuds Then Return

        Dim whyA As String = "" : Dim whyB As String = ""
        Dim c0 As Double = 0.0 : Dim c1 As Double = 0.0
        Dim a0 As Double = TerminationAllowance(inp.TermStart, inp.SpliceIdStart, spec.SlMaxMm, spec.StudDiaMm, whyA, c0)
        Dim a1 As Double = TerminationAllowance(inp.TermEnd, inp.SpliceIdEnd, spec.SlMaxMm, spec.StudDiaMm, whyB, c1)
        res.Notes.Add("Start termination: " & whyA)
        res.Notes.Add("End termination: " & whyB)
        Dim sts As List(Of Double) = Stations(inp.PlateLengthMm, a0, a1, c0, c1,
                                              spec.SlMaxMm, MinSpacing(spec.StudDiaMm))

        ' Stud lines are laid out on the FACEPLATE arc, so divide that, not the
        ' centreline cavity.
        Dim wFace As Double = inp.RadiusMm * pitch
        Dim offsets As List(Of Double) = CavityLines(wFace, spec.LinesPerCavity)

        Dim datum As Double = inp.DatumDeg * Math.PI / 180.0
        Dim span As Double = inp.SpanDeg * Math.PI / 180.0
        Dim start As Double
        If inp.DatumAlign.Trim().ToUpperInvariant() = "STUD" Then
            start = Math.Floor(datum / pitch) * pitch - pitch / 2.0
        Else
            start = Math.Floor(datum / pitch) * pitch
        End If
        While start > datum
            start -= pitch
        End While

        Dim ch As Integer = 0
        Dim a As Double = start
        While a > datum - span + 0.0000001
            ch += 1
            channelBase(ch) = a
            Dim li As Integer = 0
            For Each off As Double In offsets
                li += 1
                Dim ang As Double = a - off / inp.RadiusMm
                If ang > datum + 0.0000001 OrElse ang < datum - span - 0.0000001 Then Continue For
                Dim si As Integer = 0
                For Each zz As Double In sts
                    si += 1
                    Dim p As StudPoint = MakePoint(ch, zz, off, spec)
                    p.Line = li : p.Station = si
                    res.Points.Add(p)
                Next
            Next
            a -= pitch
        End While
    End Sub

    ''' <summary>
    ''' Radial floor / basemat.  The cavity widens with radius, so the Table 1-7
    ''' band is re-resolved at EVERY radial station -- that is what produces the
    ''' 2 rows -> 1 row -> no studs transition as you move inboard.
    ''' </summary>
    Private Sub BuildAnnular()
        Dim pitch As Double = PitchRad()
        If pitch <= 0.0 Then Throw New Exception("STUD_DIAPH_PITCH_DEG must be > 0 for ANNULAR geometry.")
        If inp.RInnerMm < 0.0 OrElse inp.ROuterMm <= inp.RInnerMm Then
            Throw New Exception("ANNULAR geometry needs 0 <= STUD_R_INNER_MM < STUD_R_OUTER_MM.")
        End If
        If inp.SpanDeg <= 0.0 Then Throw New Exception("STUD_SPAN_DEG must be > 0 for ANNULAR geometry.")

        ' Resolve once at the outer radius so the result carries a representative
        ' spec (stud size, material) and so S_l is known for the station walk.
        Dim wOuter As Double = inp.ROuterMm * pitch
        Dim maxW As Double = tables.MaxWscFor(inp.Component)
        If wOuter > maxW + 0.001 Then
            res.Violations.Add(New Violation("WSC_EXCEEDS_TABLE_MAX",
                "At the outer radius the cavity is " & wOuter.ToString("0.#", CultureInfo.InvariantCulture) &
                " mm, above the maximum tabulated diaphragm spacing " &
                maxW.ToString("0.#", CultureInfo.InvariantCulture) &
                " mm. Additional diaphragms are required outboard.", inp.ROuterMm, 0))
        End If
        Dim specOuter As StudSpec = tables.ResolveFloor(inp.Component, Math.Min(wOuter, maxW))
        res.Spec = specOuter
        Dim sl As Double = specOuter.SlMaxMm
        If sl <= 0.0 Then
            res.Notes.Add("Outer band resolves to NO STUDS; nothing placed.")
            Return
        End If

        Dim whyA As String = "" : Dim whyB As String = ""
        Dim c0 As Double = 0.0 : Dim c1 As Double = 0.0
        Dim a0 As Double = TerminationAllowance(inp.TermStart, inp.SpliceIdStart, sl, specOuter.StudDiaMm, whyA, c0)
        Dim a1 As Double = TerminationAllowance(inp.TermEnd, inp.SpliceIdEnd, sl, specOuter.StudDiaMm, whyB, c1)
        res.Notes.Add("Inner-radius termination: " & whyA)
        res.Notes.Add("Outer-radius termination: " & whyB)

        Dim sts As List(Of Double) = Stations(inp.ROuterMm - inp.RInnerMm, a0, a1, c0, c1,
                                              sl, MinSpacing(specOuter.StudDiaMm))

        Dim nCh As Integer = CInt(Math.Round(inp.SpanDeg / inp.DiaphPitchDeg))
        If nCh < 1 Then nCh = 1
        Dim datum As Double = inp.DatumDeg * Math.PI / 180.0

        For ch As Integer = 1 To nCh
            channelBase(ch) = datum - CDbl(ch) * pitch
            Dim si As Integer = 0
            For Each s As Double In sts
                si += 1
                Dim r As Double = inp.RInnerMm + s
                Dim wHere As Double = r * pitch
                Dim spec As StudSpec = Nothing
                Dim ok As Boolean = True
                Try
                    spec = tables.ResolveFloor(inp.Component, wHere)
                Catch ex As Exception
                    ok = False
                End Try
                If Not ok Then
                    res.Violations.Add(New Violation("WSC_EXCEEDS_TABLE_MAX",
                        "At r = " & r.ToString("0.#", CultureInfo.InvariantCulture) & " mm the cavity is " &
                        wHere.ToString("0.#", CultureInfo.InvariantCulture) &
                        " mm, above the maximum tabulated diaphragm spacing.", r, 0))
                    Continue For
                End If
                If spec.NoStuds Then Continue For

                Dim offs As List(Of Double) = CavityLines(wHere, spec.LinesPerCavity)
                Dim li As Integer = 0
                For Each off As Double In offs
                    li += 1
                    Dim p As StudPoint = MakePoint(ch, r, off, spec)
                    p.Line = li : p.Station = si
                    res.Points.Add(p)
                Next
            Next
        Next
    End Sub

    Private Sub CheckWsc(ByVal spec As StudSpec, ByVal wsc As Double)
        If spec.WscMaxMm > 0.0 AndAlso wsc > spec.WscMaxMm + 0.001 Then
            res.Violations.Add(New Violation("WSC_EXCEEDS_TABLE_MAX",
                "W_sc = " & wsc.ToString("0.#", CultureInfo.InvariantCulture) &
                " mm exceeds the tabulated maximum " &
                spec.WscMaxMm.ToString("0.#", CultureInfo.InvariantCulture) &
                " mm for " & spec.Component & " (" & spec.Source & ").", 0, 0))
        End If
    End Sub

    ' ------------------------------------------------------------- exclusions
    Private Sub ApplyExclusions(ByVal zones As List(Of ExclusionZone))
        Dim kept As New List(Of StudPoint)
        For Each p As StudPoint In res.Points
            Dim need As Double = EdgeClear(p.DiaMm)
            Dim drop As Boolean = False
            For Each z As ExclusionZone In zones
                If z.SignedDistance(p.Umm, p.Vmm) < need + z.Extra - 0.0001 Then
                    drop = True
                    Exit For
                End If
            Next
            If drop Then
                res.Excluded += 1
            Else
                kept.Add(p)
            End If
        Next
        res.Points = kept
    End Sub

    ' -------------------------------------------------------- density make-up
    ''' <summary>
    ''' Table 1-7 notes 7 and 8.  After exclusions, every 305 mm ALONG-CHANNEL
    ''' window must still carry 8 studs (double row) or 4 studs (single row).
    ''' Deficits are back-filled at the position with the largest clearance to
    ''' everything already placed.
    ''' </summary>
    Private Sub DensityMakeup(ByVal zones As List(Of ExclusionZone))
        Dim spec As StudSpec = res.Spec
        If spec Is Nothing OrElse spec.LinesPerCavity <= 0 Then Return
        Dim window As Double = cons.Value("DENSITY_WINDOW")
        Dim required As Integer
        If spec.LinesPerCavity >= 2 Then
            required = CInt(cons.Value("DENSITY_MIN_STUDS_DOUBLE_ROW"))
        Else
            required = CInt(cons.Value("DENSITY_MIN_STUDS_SINGLE_ROW"))
        End If
        Dim minSp As Double = MinSpacing(spec.StudDiaMm)
        Dim noteRef As String = "G103 T1-7 n" & If(spec.LinesPerCavity >= 2, "7", "8")

        Dim byCh As New Dictionary(Of Integer, List(Of StudPoint))
        For Each p As StudPoint In res.Points
            If Not byCh.ContainsKey(p.Channel) Then byCh(p.Channel) = New List(Of StudPoint)
            byCh(p.Channel).Add(p)
        Next

        For Each kv As KeyValuePair(Of Integer, List(Of StudPoint)) In byCh
            Dim pts As List(Of StudPoint) = kv.Value
            If pts.Count = 0 Then Continue For
            Dim lo As Double = Double.MaxValue
            Dim hi As Double = Double.MinValue
            For Each p As StudPoint In pts
                lo = Math.Min(lo, p.LongMm) : hi = Math.Max(hi, p.LongMm)
            Next

            Dim s As Double = lo
            While s < hi - 0.001
                Dim sHi As Double = s + window
                Dim inWin As Integer = 0
                For Each p As StudPoint In pts
                    If p.LongMm >= s - 0.001 AndAlso p.LongMm <= sHi + 0.001 Then inWin += 1
                Next
                Dim deficit As Integer = required - inWin
                If deficit > 0 Then
                    Dim added As Integer = GreedyFill(pts, zones, kv.Key, s, sHi, minSp, spec, deficit)
                    If added < deficit Then
                        res.Violations.Add(New Violation("DENSITY_MAKEUP_DEFICIT",
                            "Channel " & kv.Key & ", along-channel window " &
                            s.ToString("0", CultureInfo.InvariantCulture) & ".." &
                            sHi.ToString("0", CultureInfo.InvariantCulture) & " mm holds " &
                            (inWin + added) & " studs, code requires " & required &
                            " (" & noteRef & ").", 0, s))
                    End If
                End If
                s += window
            End While
        Next
    End Sub

    Private Function GreedyFill(ByVal pts As List(Of StudPoint), ByVal zones As List(Of ExclusionZone),
                                ByVal channel As Integer, ByVal sLo As Double, ByVal sHi As Double,
                                ByVal minSp As Double, ByVal spec As StudSpec,
                                ByVal want As Integer) As Integer
        Dim need As Double = EdgeClear(spec.StudDiaMm)
        Dim added As Integer = 0
        Dim grid As Double = 5.0

        ' Only studs near the window can affect a candidate's spacing.  Without
        ' this prefilter the scan is O(candidates x every stud in the module),
        ' which is minutes on a full basemat ring.
        Dim margin As Double = minSp + 1.0
        Dim local As New List(Of StudPoint)
        For Each p As StudPoint In res.Points
            If p.Channel = channel AndAlso p.LongMm >= sLo - margin AndAlso p.LongMm <= sHi + margin Then
                local.Add(p)
            End If
        Next

        For attempt As Integer = 1 To want
            Dim bestS As Double = 0.0, bestT As Double = 0.0
            Dim bestScore As Double = -1.0

            Dim s As Double = sLo
            While s <= sHi + 0.001
                Dim wHere As Double = CavityWidthAt(s)
                Dim tLo As Double = minSp                      ' never closer to a diaphragm than the min spacing
                Dim tHi As Double = wHere - minSp
                Dim tt As Double = tLo
                While tt <= tHi + 0.001
                    Dim cand As StudPoint = MakePoint(channel, s, tt, spec)
                    Dim ok As Boolean = True
                    If zones IsNot Nothing Then
                        For Each z As ExclusionZone In zones
                            If z.SignedDistance(cand.Umm, cand.Vmm) < need + z.Extra Then
                                ok = False
                                Exit For
                            End If
                        Next
                    End If
                    If ok Then
                        Dim nearest As Double = Double.MaxValue
                        For Each p As StudPoint In local
                            Dim dx As Double = p.Xmm - cand.Xmm
                            Dim dy As Double = p.Ymm - cand.Ymm
                            Dim dz As Double = p.Zmm - cand.Zmm
                            Dim d As Double = Math.Sqrt(dx * dx + dy * dy + dz * dz)
                            If d < nearest Then nearest = d
                        Next
                        If nearest >= minSp AndAlso nearest > bestScore Then
                            bestScore = nearest : bestS = s : bestT = tt
                        End If
                    End If
                    tt += grid
                End While
                s += grid
            End While

            If bestScore < 0.0 Then Exit For

            Dim np As StudPoint = MakePoint(channel, bestS, bestT, spec)
            np.Line = 0 : np.Station = 0
            np.Source = "MAKE-UP per G103 T1-7 n" & If(spec.LinesPerCavity >= 2, "7", "8")
            res.Points.Add(np)
            pts.Add(np)
            local.Add(np)
            added += 1
            res.MadeUp += 1
        Next
        Return added
    End Function

    ' -------------------------------------------------------------- validation
    Private Sub Validate()
        Dim spec As StudSpec = res.Spec
        If spec Is Nothing OrElse res.Points.Count = 0 Then Return
        Dim minSp As Double = MinSpacing(spec.StudDiaMm)

        Dim maxTrans As Double
        If spec.StMaxMm > 0.0 Then
            maxTrans = spec.StMaxMm
        Else
            maxTrans = cons.Value("MAX_SPACING_FLOOR_ARRAY")
        End If

        ' 1. True 3D nearest-neighbour minimum spacing.  This also satisfies
        '    Detail 2 note 3, which measures staggered arrays along the shortest
        '    straight line between stud centrelines rather than along the array
        '    axes.  Bucketed so it stays linear at thousands of studs.
        Dim cell As Double = Math.Max(minSp, 1.0)
        Dim buckets As New Dictionary(Of String, List(Of Integer))
        For i As Integer = 0 To res.Points.Count - 1
            Dim k As String = BucketKey(res.Points(i), cell)
            If Not buckets.ContainsKey(k) Then buckets(k) = New List(Of Integer)
            buckets(k).Add(i)
        Next

        Dim reported As Integer = 0
        For i As Integer = 0 To res.Points.Count - 1
            Dim p As StudPoint = res.Points(i)
            Dim ci As Integer = CInt(Math.Floor(p.Xmm / cell))
            Dim cj As Integer = CInt(Math.Floor(p.Ymm / cell))
            Dim ck As Integer = CInt(Math.Floor(p.Zmm / cell))
            Dim nearest As Double = Double.MaxValue
            For di As Integer = -1 To 1
                For dj As Integer = -1 To 1
                    For dk As Integer = -1 To 1
                        Dim k As String = (ci + di).ToString() & ":" & (cj + dj).ToString() & ":" & (ck + dk).ToString()
                        If Not buckets.ContainsKey(k) Then Continue For
                        For Each j As Integer In buckets(k)
                            If j = i Then Continue For
                            Dim dx As Double = res.Points(j).Xmm - p.Xmm
                            Dim dy As Double = res.Points(j).Ymm - p.Ymm
                            Dim dz As Double = res.Points(j).Zmm - p.Zmm
                            Dim d As Double = Math.Sqrt(dx * dx + dy * dy + dz * dz)
                            If d < nearest Then nearest = d
                        Next
                    Next
                Next
            Next
            If nearest < minSp - 0.05 AndAlso reported < 50 Then
                reported += 1
                res.Violations.Add(New Violation("MIN_SPACING",
                    "Stud centres " & nearest.ToString("0.##", CultureInfo.InvariantCulture) &
                    " mm apart, minimum is " & minSp.ToString("0.##", CultureInfo.InvariantCulture) &
                    " mm for a " & spec.StudDiaMm.ToString("0.#", CultureInfo.InvariantCulture) &
                    " mm stud (AISC 360 I8.3e).", p.Umm, p.Vmm))
            End If
        Next
        If reported >= 50 Then res.Notes.Add("MIN_SPACING violations truncated at 50 entries.")

        ' 2. Along-channel pitch inside each stud line.
        Dim byLine As New Dictionary(Of String, List(Of Double))
        For Each p As StudPoint In res.Points
            If p.Line = 0 Then Continue For          ' make-up studs sit off-array by design
            Dim k As String = p.Channel & "/" & p.Line
            If Not byLine.ContainsKey(k) Then byLine(k) = New List(Of Double)
            byLine(k).Add(p.LongMm)
        Next
        For Each kv As KeyValuePair(Of String, List(Of Double)) In byLine
            Dim l As List(Of Double) = kv.Value
            l.Sort()
            For i As Integer = 1 To l.Count - 1
                Dim d As Double = l(i) - l(i - 1)
                If d > spec.SlMaxMm + 0.05 Then
                    res.Violations.Add(New Violation("MAX_SPACING_LONGITUDINAL",
                        "Line " & kv.Key & ": pitch " & d.ToString("0.##", CultureInfo.InvariantCulture) &
                        " mm exceeds S_l = " & spec.SlMaxMm.ToString("0.#", CultureInfo.InvariantCulture) &
                        " mm (" & spec.Source & ").", 0, l(i)))
                End If
            Next
        Next

        ' 3. Across-channel spacing, measured from the STUDS THAT WERE ACTUALLY
        '    PLACED rather than from the tabulated divisor.  That keeps it honest
        '    where the cavity width varies with radius, and where
        '    RADIAL_CLEARANCE has deliberately shifted the lines off even pitch.
        '    Both end gaps are included: T1-6 and T1-7 govern stud-to-diaphragm
        '    spacing as well as stud-to-stud.
        Dim byStation As New Dictionary(Of String, List(Of StudPoint))
        For Each p As StudPoint In res.Points
            If p.Line = 0 Then Continue For
            Dim k As String = p.Channel & "/" & p.Station
            If Not byStation.ContainsKey(k) Then byStation(k) = New List(Of StudPoint)
            byStation(k).Add(p)
        Next

        Dim tMin As Integer = 0
        Dim tMax As Integer = 0
        For Each kv2 As KeyValuePair(Of String, List(Of StudPoint)) In byStation
            Dim g As List(Of StudPoint) = kv2.Value
            If g.Count = 0 Then Continue For
            g.Sort(Function(x, y) x.TransMm.CompareTo(y.TransMm))
            Dim w As Double = CavityWidthAt(g(0).LongMm)

            Dim gaps As New List(Of Double)
            gaps.Add(g(0).TransMm)                                  ' diaphragm -> first stud
            For i As Integer = 1 To g.Count - 1
                gaps.Add(g(i).TransMm - g(i - 1).TransMm)           ' stud -> stud
            Next
            gaps.Add(w - g(g.Count - 1).TransMm)                    ' last stud -> diaphragm

            For Each gp As Double In gaps
                If gp < minSp - 0.05 AndAlso tMin < 20 Then
                    tMin += 1
                    res.Violations.Add(New Violation("MIN_SPACING_TRANSVERSE",
                        "Channel/station " & kv2.Key & ": across-channel gap " &
                        gp.ToString("0.##", CultureInfo.InvariantCulture) & " mm is below the " &
                        minSp.ToString("0.#", CultureInfo.InvariantCulture) &
                        " mm minimum (stud-to-stud and stud-to-diaphragm both govern).",
                        g(0).Umm, g(0).Vmm))
                End If
                If gp > maxTrans + 0.05 AndAlso tMax < 20 Then
                    tMax += 1
                    res.Violations.Add(New Violation("MAX_SPACING_TRANSVERSE",
                        "Channel/station " & kv2.Key & ": across-channel gap " &
                        gp.ToString("0.##", CultureInfo.InvariantCulture) &
                        " mm exceeds the maximum transversal spacing " &
                        maxTrans.ToString("0.#", CultureInfo.InvariantCulture) & " mm.",
                        g(0).Umm, g(0).Vmm))
                End If
            Next
        Next
        If tMin >= 20 Then res.Notes.Add("MIN_SPACING_TRANSVERSE violations truncated at 20 entries.")
        If tMax >= 20 Then res.Notes.Add("MAX_SPACING_TRANSVERSE violations truncated at 20 entries.")
    End Sub

    Private Function BucketKey(ByVal p As StudPoint, ByVal cell As Double) As String
        Return CInt(Math.Floor(p.Xmm / cell)).ToString() & ":" &
               CInt(Math.Floor(p.Ymm / cell)).ToString() & ":" &
               CInt(Math.Floor(p.Zmm / cell)).ToString()
    End Function
End Class
