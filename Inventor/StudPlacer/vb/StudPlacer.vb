' =============================================================================
'  StudPlacer.vb  --  Inventor placement and CSV export.
'
'  Loaded into an iLogic rule with:   AddVbFile "StudPlacer.vb"
'  Requires StudRules.vb and StudArray.vb.
'
'  UNITS: everything upstream is in millimetres.  Inventor's API internal length
'  unit is CENTIMETRES, so every coordinate is divided by 10 exactly once, here.
'
'  STUD PART REQUIREMENT: the stud .ipt must be modelled with its axis along its
'  own +Z and the weld face (base) at the part origin.  Orientation is then just
'  "point local +Z along the face normal".
' =============================================================================
Imports System
Imports System.Collections.Generic
Imports System.Globalization
' NOTE: System.IO is deliberately NOT imported here.
' iLogic compiles AddVbFile sources in the same compilation as the rule, with its
' own global imports -- which include both System.IO and Inventor. The Inventor
' API has its own File and Path types, so a bare File.Exists or Path.Combine in
' this file fails inside Inventor with
'   "'File' is ambiguous, imported from the namespaces or types 'System.IO, Inventor'"
' even though it compiles fine on its own. Always write System.IO.File /
' System.IO.Path in full. Leaving the import out means the test build fails too,
' rather than passing something Inventor will reject.
Imports System.Text
Imports Inventor

Public Class StudPlacer

    ''' <summary>
    ''' Default occurrence-name prefix. Made overridable because a basemat has a
    ''' faceplate on BOTH faces and each needs its own run: the place routine
    ''' clears everything carrying the prefix first, so two faces sharing one
    ''' prefix means the second run deletes the first face's studs.
    ''' Give each face its own (STUD_BOT_, STUD_TOP_) and both survive.
    ''' </summary>
    Public Const OccurrencePrefix As String = "STUD_"

    ''' <summary>Right-handed coordinate system with local +Z along (dx,dy,dz).</summary>
    Private Shared Function BuildMatrix(ByVal tg As TransientGeometry,
                                        ByVal xmm As Double, ByVal ymm As Double, ByVal zmm As Double,
                                        ByVal dx As Double, ByVal dy As Double, ByVal dz As Double) As Inventor.Matrix
        Dim L As Double = Math.Sqrt(dx * dx + dy * dy + dz * dz)
        If L < 0.000000001 Then
            dx = 0.0 : dy = 0.0 : dz = 1.0 : L = 1.0
        End If
        dx /= L : dy /= L : dz /= L

        ' Helper vector deliberately not parallel to the stud axis.
        Dim hx As Double, hy As Double, hz As Double
        If Math.Abs(dz) < 0.9 Then
            hx = 0.0 : hy = 0.0 : hz = 1.0
        Else
            hx = 1.0 : hy = 0.0 : hz = 0.0
        End If

        ' xAxis = normalize(h x d)
        Dim ax As Double = hy * dz - hz * dy
        Dim ay As Double = hz * dx - hx * dz
        Dim az As Double = hx * dy - hy * dx
        Dim aL As Double = Math.Sqrt(ax * ax + ay * ay + az * az)
        ax /= aL : ay /= aL : az /= aL

        ' yAxis = d x xAxis
        Dim bx As Double = dy * az - dz * ay
        Dim by As Double = dz * ax - dx * az
        Dim bz As Double = dx * ay - dy * ax

        Dim m As Inventor.Matrix = tg.CreateMatrix()
        m.SetCoordinateSystem(tg.CreatePoint(xmm / 10.0, ymm / 10.0, zmm / 10.0),
                              tg.CreateVector(ax, ay, az),
                              tg.CreateVector(bx, by, bz),
                              tg.CreateVector(dx, dy, dz))
        Return m
    End Function

    ''' <summary>Remove studs placed by a previous run so the rule is re-runnable.</summary>
    Public Shared Function ClearExisting(ByVal asmDef As AssemblyComponentDefinition,
                                        Optional ByVal prefix As String = OccurrencePrefix) As Integer
        Dim n As Integer = 0
        If String.IsNullOrEmpty(prefix) Then prefix = OccurrencePrefix
        For i As Integer = asmDef.Occurrences.Count To 1 Step -1
            Dim occ As ComponentOccurrence = asmDef.Occurrences.Item(i)
            If occ.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
                occ.Delete()
                n += 1
            End If
        Next
        Return n
    End Function

    ''' <summary>
    ''' Place one occurrence per stud.  Wrapped in a transaction with the UI
    ''' pinned down, which is the difference between "a few seconds" and "several
    ''' minutes" at these counts.
    ''' </summary>
    Public Shared Function Place(ByVal inv As Inventor.Application,
                                 ByVal asmDoc As AssemblyDocument,
                                 ByVal studPartPath As String,
                                 ByVal pts As List(Of StudPoint),
                                 ByVal maxOccurrences As Integer,
                                 ByRef message As String,
                                 Optional ByVal prefix As String = OccurrencePrefix) As Integer
        If Not System.IO.File.Exists(studPartPath) Then
            Throw New Exception("Stud part not found: " & studPartPath)
        End If
        If pts.Count > maxOccurrences Then
            message = "Placement skipped: " & pts.Count & " studs exceeds the " & maxOccurrences &
                      " occurrence guard. The CSV was still written. Raise STUD_MAX_OCCURRENCES " &
                      "or split the module if you really want this many occurrences."
            Return 0
        End If

        Dim asmDef As AssemblyComponentDefinition = asmDoc.ComponentDefinition
        Dim tg As TransientGeometry = inv.TransientGeometry
        Dim placed As Integer = 0

        Dim priorScreen As Boolean = inv.ScreenUpdating
        Dim priorInteract As Boolean = inv.UserInterfaceManager.UserInteractionDisabled
        Dim tx As Inventor.Transaction = Nothing
        Try
            inv.ScreenUpdating = False
            inv.UserInterfaceManager.UserInteractionDisabled = True
            tx = inv.TransactionManager.StartTransaction(asmDoc, "Place shear studs")

            If String.IsNullOrEmpty(prefix) Then prefix = OccurrencePrefix
            ClearExisting(asmDef, prefix)

            For Each p As StudPoint In pts
                Dim m As Inventor.Matrix = BuildMatrix(tg, p.Xmm, p.Ymm, p.Zmm, p.DirX, p.DirY, p.DirZ)
                Dim occ As ComponentOccurrence = asmDef.Occurrences.Add(studPartPath, m)
                occ.Name = prefix & p.Channel.ToString("00") & "_" &
                           p.Line.ToString("0") & "_" & p.Station.ToString("000")
                ' Grounded so nothing drifts: these are positioned by rule, not by constraints.
                occ.Grounded = True
                placed += 1
            Next

            tx.End()
            tx = Nothing
        Finally
            If tx IsNot Nothing Then tx.Abort()
            inv.UserInterfaceManager.UserInteractionDisabled = priorInteract
            inv.ScreenUpdating = priorScreen
        End Try

        message = "Placed " & placed & " stud occurrences."
        Return placed
    End Function

    ' ------------------------------------------------------------------ CSV
    Private Shared Function Q(ByVal s As String) As String
        If s Is Nothing Then Return """"""
        Dim needs As Boolean = s.Contains(",") OrElse s.Contains("""") OrElse s.Contains(vbLf)
        Dim t As String = s.Replace("""", """""")
        If needs Then Return """" & t & """"
        Return t
    End Function

    Private Shared Function N(ByVal d As Double) As String
        Return d.ToString("0.###", CultureInfo.InvariantCulture)
    End Function

    ''' <summary>
    ''' Rule-annotated stud schedule: every stud carries the code clause that put
    ''' it there.  This is the audit artefact for a Deferred Verification package.
    ''' </summary>
    Public Shared Sub ExportSchedule(ByVal path As String,
                                     ByVal inp As ModuleInput,
                                     ByVal r As StudArrayResult,
                                     ByVal rulesRev As String)
        Dim sb As New StringBuilder()
        sb.AppendLine("# BWRX-300 RB DP-SC shear stud schedule")
        sb.AppendLine("# Generated by StudPlacer from " & rulesRev)
        sb.AppendLine("# All coordinates in millimetres, module local.")
        sb.AppendLine("SECTION,KEY,VALUE")
        sb.AppendLine("HEADER,MODULE_ID," & Q(inp.ModuleId))
        sb.AppendLine("HEADER,COMPONENT," & Q(inp.Component))
        sb.AppendLine("HEADER,GEOMETRY," & Q(inp.Geometry))
        sb.AppendLine("HEADER,ELEVATION_M," & N(inp.ElevM))
        If r.Spec IsNot Nothing Then
            sb.AppendLine("HEADER,RULE_SOURCE," & Q(r.Spec.Source))
            sb.AppendLine("HEADER,STUD_DIA_MM," & N(r.Spec.StudDiaMm))
            sb.AppendLine("HEADER,STUD_LENGTH_MM," & N(r.Spec.StudLenMm))
            sb.AppendLine("HEADER,STUD_MATERIAL," & Q(r.Spec.StudMaterial))
            sb.AppendLine("HEADER,LINES_PER_CAVITY," & r.Spec.LinesPerCavity.ToString())
            sb.AppendLine("HEADER,S_L_MAX_MM," & N(r.Spec.SlMaxMm))
            sb.AppendLine("HEADER,S_T_DIVISOR," & r.Spec.StDivisor.ToString())
            sb.AppendLine("HEADER,S_T_NOMINAL_MM," & N(r.Spec.StNominal(inp.WscMm)))
        End If
        sb.AppendLine("HEADER,W_SC_MM," & N(inp.WscMm))
        sb.AppendLine("HEADER,TOTAL_STUDS," & r.Points.Count.ToString())
        sb.AppendLine("HEADER,EXCLUDED," & r.Excluded.ToString())
        sb.AppendLine("HEADER,MAKEUP_ADDED," & r.MadeUp.ToString())
        sb.AppendLine("HEADER,VIOLATIONS," & r.Violations.Count.ToString())

        For Each note As String In r.Notes
            sb.AppendLine("NOTE,," & Q(note))
        Next
        For Each v As Violation In r.Violations
            sb.AppendLine("VIOLATION," & Q(v.Code) & "," & Q(v.Detail & " @ u=" & N(v.Umm) & " v=" & N(v.Vmm)))
        Next

        sb.AppendLine()
        sb.AppendLine("INDEX,CHANNEL,LINE,STATION,X_MM,Y_MM,Z_MM,DIR_X,DIR_Y,DIR_Z,U_MM,V_MM,DIA_MM,LEN_MM,ROLL_DEG,RULE")
        Dim i As Integer = 0
        For Each p As StudPoint In r.Points
            i += 1
            sb.AppendLine(String.Join(",", New String() {
                i.ToString(), p.Channel.ToString(), p.Line.ToString(), p.Station.ToString(),
                N(p.Xmm), N(p.Ymm), N(p.Zmm),
                N(p.DirX), N(p.DirY), N(p.DirZ),
                N(p.Umm), N(p.Vmm), N(p.DiaMm), N(p.LenMm), N(p.RollDeg), Q(p.Source)}))
        Next
        System.IO.File.WriteAllText(path, sb.ToString())
    End Sub

    ''' <summary>
    ''' Legacy Studly.py CSV contract, kept so anything already consuming that
    ''' format keeps working.  Assembly_Type: 1 flat, 2 curved inner,
    ''' 3 curved outer, 4 common floor, 5 basemat.  Roll is degrees x 10.
    ''' </summary>
    Public Shared Sub ExportStudlyCompat(ByVal path As String,
                                         ByVal inp As ModuleInput,
                                         ByVal r As StudArrayResult,
                                         ByVal partNumber As String,
                                         ByVal partDescription As String)
        Dim atype As Integer = 0
        Select Case inp.Geometry.Trim().ToUpperInvariant()
            Case "FLAT" : atype = 1
            Case "CURVED_INNER" : atype = 2
            Case "CURVED_OUTER" : atype = 3
            Case "ANNULAR"
                If inp.Component.ToUpperInvariant().Contains("BASEMAT") Then
                    atype = 5
                Else
                    atype = 4
                End If
        End Select

        Dim sb As New StringBuilder()
        sb.AppendLine("Type,Index,Field,Value")
        sb.AppendLine("Part_Name,,," & Q(partDescription))
        sb.AppendLine("Part_Number,,," & Q(partNumber))
        sb.AppendLine("Assembly_Type,,," & atype.ToString())
        sb.AppendLine("Total_Studs,,," & r.Points.Count.ToString())
        Dim i As Integer = 0
        For Each p As StudPoint In r.Points
            i += 1
            sb.AppendLine("Stud," & i & ",X_Pos," & CInt(Math.Round(p.Xmm)).ToString())
            sb.AppendLine("Stud," & i & ",Y_Pos," & CInt(Math.Round(p.Ymm)).ToString())
            sb.AppendLine("Stud," & i & ",Z_Pos," & CInt(Math.Round(p.Zmm)).ToString())
            sb.AppendLine("Stud," & i & ",Roll," & CInt(Math.Round(p.RollDeg * 10.0)).ToString())
        Next
        System.IO.File.WriteAllText(path, sb.ToString())
    End Sub

    ''' <summary>Load per-module exclusion zones (flow holes, sleeves, tie plates).</summary>
    Public Shared Function LoadExclusions(ByVal path As String) As List(Of ExclusionZone)
        Dim list As New List(Of ExclusionZone)
        If String.IsNullOrEmpty(path) OrElse Not System.IO.File.Exists(path) Then Return list
        Dim t As CsvTable = CsvTable.Load(path)
        For Each row As String() In t.Rows
            Dim z As New ExclusionZone()
            z.Kind = t.Col(row, "KIND").ToUpperInvariant()
            z.U = t.Num(row, "U_MM")
            z.V = t.Num(row, "V_MM")
            z.R = t.Num(row, "R_MM")
            z.W = t.Num(row, "W_MM")
            z.H = t.Num(row, "H_MM")
            z.AngleDeg = t.Num(row, "ANGLE_DEG")
            z.Extra = t.Num(row, "EXTRA_CLEAR_MM")
            z.Label = t.Col(row, "LABEL")
            list.Add(z)
        Next
        Return list
    End Function
End Class
