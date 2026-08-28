' =============================================================================
'  StudRules.vb  --  code-table loading and rule resolution
'  BWRX-300 RB DP-SC shear stud placement.  Source: 008N9536 Rev 11, sheet G103.
'
'  Loaded into an iLogic rule with:   AddVbFile "StudRules.vb"
'  No Inventor API is used in this file, so it is pure logic and easy to reason
'  about.  All lengths are MILLIMETRES here; conversion to Inventor's internal
'  centimetres happens only in StudPlacer.vb.
' =============================================================================
Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO

'------------------------------------------------------------------------------
' Minimal RFC-4180-ish CSV reader.  Handles quoted fields and skips '#' comments.
'------------------------------------------------------------------------------
Public Class CsvTable
    Public Header As List(Of String) = New List(Of String)
    Public Rows As List(Of String()) = New List(Of String())

    Public Shared Function Load(ByVal path As String) As CsvTable
        If Not File.Exists(path) Then
            Throw New Exception("Rule table not found: " & path)
        End If
        Dim t As New CsvTable()
        For Each raw As String In File.ReadAllLines(path)
            Dim line As String = raw.Trim()
            If line.Length = 0 Then Continue For
            If line.StartsWith("#") Then Continue For
            Dim fields As String() = SplitLine(line)
            If t.Header.Count = 0 Then
                For Each f As String In fields
                    t.Header.Add(f.Trim().ToUpperInvariant())
                Next
            Else
                t.Rows.Add(fields)
            End If
        Next
        Return t
    End Function

    Private Shared Function SplitLine(ByVal line As String) As String()
        Dim outp As New List(Of String)
        Dim sb As New System.Text.StringBuilder()
        Dim inQuotes As Boolean = False
        Dim i As Integer = 0
        While i < line.Length
            Dim c As Char = line(i)
            If inQuotes Then
                If c = """"c Then
                    If i + 1 < line.Length AndAlso line(i + 1) = """"c Then
                        sb.Append(""""c) : i += 1
                    Else
                        inQuotes = False
                    End If
                Else
                    sb.Append(c)
                End If
            Else
                If c = """"c Then
                    inQuotes = True
                ElseIf c = ","c Then
                    outp.Add(sb.ToString()) : sb.Length = 0
                Else
                    sb.Append(c)
                End If
            End If
            i += 1
        End While
        outp.Add(sb.ToString())
        Return outp.ToArray()
    End Function

    Public Function Col(ByVal row As String(), ByVal name As String) As String
        Dim idx As Integer = Header.IndexOf(name.ToUpperInvariant())
        If idx < 0 Then Throw New Exception("Column '" & name & "' missing from rule table.")
        If idx >= row.Length Then Return ""
        Return row(idx).Trim()
    End Function

    Public Function Num(ByVal row As String(), ByVal name As String) As Double
        Dim s As String = Col(row, name)
        If s.Length = 0 Then Return 0.0
        ' InvariantCulture on purpose: these files must parse identically on a
        ' workstation configured for a comma-decimal locale.
        Return Double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture)
    End Function

    Public Function Int32Of(ByVal row As String(), ByVal name As String) As Integer
        Return CInt(Math.Round(Num(row, name)))
    End Function
End Class

'------------------------------------------------------------------------------
' Global constraints (global_constraints.csv) keyed by name.
'------------------------------------------------------------------------------
Public Class StudConstraints
    Private map As Dictionary(Of String, Double) = New Dictionary(Of String, Double)
    Private src As Dictionary(Of String, String) = New Dictionary(Of String, String)

    Public Shared Function Load(ByVal path As String) As StudConstraints
        Dim t As CsvTable = CsvTable.Load(path)
        Dim c As New StudConstraints()
        For Each r As String() In t.Rows
            Dim k As String = t.Col(r, "KEY").ToUpperInvariant()
            c.map(k) = t.Num(r, "VALUE")
            c.src(k) = t.Col(r, "SOURCE")
        Next
        Return c
    End Function

    Public Function Value(ByVal key As String) As Double
        Dim k As String = key.ToUpperInvariant()
        If Not map.ContainsKey(k) Then
            Throw New Exception("Constraint '" & key & "' missing from global_constraints.csv")
        End If
        Return map(k)
    End Function

    Public Function Source(ByVal key As String) As String
        Dim k As String = key.ToUpperInvariant()
        If src.ContainsKey(k) Then Return src(k)
        Return ""
    End Function

    ' Minimum centre-to-centre spacing depends on stud diameter (AISC 360 I8.3e).
    Public Function MinSpacingFor(ByVal studDiaMm As Double) As Double
        If studDiaMm >= 15.0 Then Return Value("MIN_STUD_SPACING_19MM")
        Return Value("MIN_STUD_SPACING_12MM")
    End Function
End Class

'------------------------------------------------------------------------------
' The resolved code requirement for one module.
'------------------------------------------------------------------------------
Public Class StudSpec
    Public Component As String = ""
    Public Family As String = ""          ' WALL | FLOOR
    Public NoStuds As Boolean = False
    Public StudDiaMm As Double = 19.1
    Public StudLenMm As Double = 157.2
    Public StudMaterial As String = ""
    Public PlateGrade As String = ""
    Public TscMm As Double = 0.0
    ' Count of stud lines between two adjacent diaphragms.
    ' Table 1-6 calls them "columns", Table 1-7 calls them "rows" -- same thing.
    Public LinesPerCavity As Integer = 0
    Public SlMaxMm As Double = 0.0        ' max spacing along the channel
    Public StDivisor As Integer = 0       ' = LinesPerCavity + 1
    Public StMaxMm As Double = 0.0        ' tabulated transverse ceiling, 0 = use global
    Public WscMaxMm As Double = 0.0       ' max permitted diaphragm spacing
    Public Source As String = ""

    Public Function StNominal(ByVal wscMm As Double) As Double
        If StDivisor <= 0 Then Return 0.0
        Return wscMm / CDbl(StDivisor)
    End Function
End Class

'------------------------------------------------------------------------------
' Table 1-6 / Table 1-7 lookup.
'------------------------------------------------------------------------------
Public Class RuleTables
    Private walls As CsvTable
    Private floors As CsvTable
    Public Constraints As StudConstraints

    Public Shared Function Load(ByVal rulesDir As String) As RuleTables
        Dim rt As New RuleTables()
        rt.walls = CsvTable.Load(Path.Combine(rulesDir, "table_1_6_walls.csv"))
        rt.floors = CsvTable.Load(Path.Combine(rulesDir, "table_1_7_floors.csv"))
        rt.Constraints = StudConstraints.Load(Path.Combine(rulesDir, "global_constraints.csv"))
        Return rt
    End Function

    Public Function IsWallComponent(ByVal component As String) As Boolean
        Dim c As String = component.Trim().ToUpperInvariant()
        For Each r As String() In walls.Rows
            If walls.Col(r, "COMPONENT").ToUpperInvariant() = c Then Return True
        Next
        Return False
    End Function

    ''' <summary>
    ''' Table 1-6.  Selects by component then by elevation band.  Elevations are
    ''' metres, negative below grade, so the band test is FROM &lt;= elev &lt;= TO
    ''' with FROM the more-negative end.
    ''' </summary>
    Public Function ResolveWall(ByVal component As String, ByVal elevM As Double) As StudSpec
        Dim c As String = component.Trim().ToUpperInvariant()
        Dim best As String() = Nothing
        For Each r As String() In walls.Rows
            If walls.Col(r, "COMPONENT").ToUpperInvariant() <> c Then Continue For
            Dim lo As Double = walls.Num(r, "ELEV_FROM_M")
            Dim hi As Double = walls.Num(r, "ELEV_TO_M")
            If lo > hi Then
                Dim tmp As Double = lo : lo = hi : hi = tmp
            End If
            ' Half-open at the top so a boundary elevation lands in the lower band,
            ' matching how the drawing stacks "-34 to -31.1" then "-31.1 to -8.0".
            If elevM >= lo AndAlso (elevM < hi OrElse Math.Abs(elevM - hi) < 0.0000001) Then
                best = r
                Exit For
            End If
        Next
        If best Is Nothing Then
            Throw New Exception("No Table 1-6 band for component '" & component &
                                "' at elevation " & elevM.ToString("0.###", CultureInfo.InvariantCulture) & " m.")
        End If

        Dim s As New StudSpec()
        s.Component = c
        s.Family = "WALL"
        s.PlateGrade = walls.Col(best, "PLATE_GRADE")
        s.TscMm = walls.Num(best, "T_SC_MM")
        s.WscMaxMm = walls.Num(best, "W_SC_MAX_MM")
        s.StudMaterial = walls.Col(best, "STUD_MATERIAL")
        s.StudLenMm = walls.Num(best, "STUD_LEN_MM")
        s.StudDiaMm = walls.Num(best, "STUD_DIA_MM")
        s.LinesPerCavity = walls.Int32Of(best, "STUD_COLUMNS")
        s.SlMaxMm = walls.Num(best, "S_L_MAX_MM")
        s.StDivisor = walls.Int32Of(best, "S_T_DIVISOR")
        s.StMaxMm = walls.Num(best, "S_T_MAX_MM")
        s.Source = walls.Col(best, "SOURCE")
        s.NoStuds = (s.LinesPerCavity <= 0)
        Return s
    End Function

    ''' <summary>
    ''' Table 1-7.  Selects by component then by the ACTUAL diaphragm spacing.
    ''' This is what makes the array degrade (2 rows -> 1 row -> no studs) as the
    ''' channel narrows, which is the whole point of the table.
    ''' </summary>
    Public Function ResolveFloor(ByVal component As String, ByVal wscMm As Double) As StudSpec
        Dim c As String = component.Trim().ToUpperInvariant()
        Dim best As String() = Nothing
        For Each r As String() In floors.Rows
            If floors.Col(r, "COMPONENT").ToUpperInvariant() <> c Then Continue For
            Dim wmax As Double = floors.Num(r, "W_SC_MAX_MM")
            Dim cmp As String = floors.Col(r, "CMP").ToUpperInvariant()
            Dim hit As Boolean = False
            If cmp = "LT" Then
                hit = (wscMm < wmax)
            Else
                hit = (wscMm <= wmax + 0.0001)
            End If
            If hit Then
                best = r
                Exit For   ' rows are ordered ascending by W_SC_MAX_MM
            End If
        Next
        If best Is Nothing Then
            Throw New Exception("No Table 1-7 band for component '" & component &
                                "' at W_sc = " & wscMm.ToString("0.#", CultureInfo.InvariantCulture) &
                                " mm. W_sc likely exceeds the maximum permitted diaphragm spacing.")
        End If

        Dim s As New StudSpec()
        s.Component = c
        s.Family = "FLOOR"
        s.PlateGrade = floors.Col(best, "PLATE_GRADE")
        s.TscMm = floors.Num(best, "T_SC_MM")
        s.StudMaterial = floors.Col(best, "STUD_MATERIAL")
        s.StudLenMm = floors.Num(best, "STUD_LEN_MM")
        s.StudDiaMm = floors.Num(best, "STUD_DIA_MM")
        s.LinesPerCavity = floors.Int32Of(best, "STUD_ROWS")
        s.SlMaxMm = floors.Num(best, "S_L_MAX_MM")
        s.StDivisor = floors.Int32Of(best, "S_T_DIVISOR")
        s.StMaxMm = 0.0        ' floors use the global 254 mm ceiling
        s.WscMaxMm = MaxWscFor(c)
        s.Source = floors.Col(best, "SOURCE")
        s.NoStuds = (s.LinesPerCavity <= 0)
        Return s
    End Function

    Public Function MaxWscFor(ByVal component As String) As Double
        Dim c As String = component.Trim().ToUpperInvariant()
        Dim m As Double = 0.0
        For Each r As String() In floors.Rows
            If floors.Col(r, "COMPONENT").ToUpperInvariant() = c Then
                m = Math.Max(m, floors.Num(r, "W_SC_MAX_MM"))
            End If
        Next
        Return m
    End Function

    ''' <summary>Dispatch on component family.</summary>
    Public Function Resolve(ByVal component As String, ByVal elevM As Double, ByVal wscMm As Double) As StudSpec
        If IsWallComponent(component) Then
            Return ResolveWall(component, elevM)
        End If
        Return ResolveFloor(component, wscMm)
    End Function
End Class
