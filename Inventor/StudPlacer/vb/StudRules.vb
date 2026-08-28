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
' NOTE: System.IO is deliberately NOT imported here.
' iLogic compiles AddVbFile sources in the same compilation as the rule, with its
' own global imports -- which include both System.IO and Inventor. The Inventor
' API has its own File and Path types, so a bare File.Exists or Path.Combine in
' this file fails inside Inventor with
'   "'File' is ambiguous, imported from the namespaces or types 'System.IO, Inventor'"
' even though it compiles fine on its own. Always write System.IO.File /
' System.IO.Path in full. Leaving the import out means the test build fails too,
' rather than passing something Inventor will reject.

'------------------------------------------------------------------------------
' Minimal RFC-4180-ish CSV reader.  Handles quoted fields and skips '#' comments.
'------------------------------------------------------------------------------
Public Class CsvTable
    Public Header As List(Of String) = New List(Of String)
    Public Rows As List(Of String()) = New List(Of String())

    Public Shared Function Load(ByVal path As String) As CsvTable
        If Not System.IO.File.Exists(path) Then
            Throw New Exception("Rule table not found: " & path)
        End If
        Dim t As New CsvTable()
        For Each raw As String In System.IO.File.ReadAllLines(path)
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

    Public Shared ReadOnly RequiredFiles As String() =
        New String() {"table_1_6_walls.csv", "table_1_7_floors.csv", "global_constraints.csv"}

    ''' <summary>Does this folder hold all three code tables?</summary>
    Private Shared Function HasAllTables(ByVal dir As String) As Boolean
        For Each f As String In RequiredFiles
            If Not System.IO.File.Exists(System.IO.Path.Combine(dir, f)) Then Return False
        Next
        Return True
    End Function

    Private Shared Function ParentOf(ByVal dir As String) As String
        Try
            Dim p As String = System.IO.Path.GetDirectoryName(dir.TrimEnd("\"c, "/"c))
            If p Is Nothing Then Return ""
            Return p
        Catch ex As Exception
            Return ""
        End Try
    End Function

    Private Shared Sub SearchDir(ByVal dir As String, ByVal depth As Integer, ByVal hits As List(Of String))
        If hits.Count >= 5 Then Return
        If HasAllTables(dir) AndAlso Not hits.Contains(dir) Then hits.Add(dir)
        If depth <= 0 Then Return
        Try
            For Each sub_ As String In System.IO.Directory.GetDirectories(dir)
                SearchDir(sub_, depth - 1, hits)
            Next
        Catch ex As Exception
            ' Unreadable folder -- skip it, this is a best-effort hint.
        End Try
    End Sub

    ''' <summary>
    ''' Best-effort hunt for a folder that actually holds the code tables, used
    ''' only to make the not-found message actionable. Nothing is ever loaded
    ''' from a discovered path: the operator has to point STUD_RULES_DIR at it,
    ''' so the schedule always records a rules folder somebody chose.
    ''' </summary>
    Public Shared Function FindCandidates(ByVal configured As String) As List(Of String)
        Dim hits As New List(Of String)
        Try
            ' Walk up to the nearest ancestor that exists.
            Dim probe As String = configured
            Dim guard As Integer = 0
            While probe <> "" AndAlso Not System.IO.Directory.Exists(probe) AndAlso guard < 8
                probe = ParentOf(probe)
                guard += 1
            End While
            If probe = "" OrElse Not System.IO.Directory.Exists(probe) Then Return hits

            ' That folder and anything two levels inside it.
            SearchDir(probe, 2, hits)

            ' Then a few levels up, one level deep each -- catches the tables
            ' having landed beside the install rather than inside it.
            Dim up As String = probe
            For i As Integer = 1 To 3
                up = ParentOf(up)
                If up = "" OrElse Not System.IO.Directory.Exists(up) Then Exit For
                SearchDir(up, 1, hits)
            Next
        Catch ex As Exception
            ' Never let the hint search break the error report it is decorating.
        End Try
        Return hits
    End Function

    ''' <summary>
    ''' Load all three code tables from a folder.
    '''
    ''' Everything is checked BEFORE anything is parsed, so a half-copied install
    ''' reports the whole picture at once instead of one missing file per run.
    ''' </summary>
    Public Shared Function Load(ByVal rulesDir As String) As RuleTables
        Dim missing As New List(Of String)
        Dim dirExists As Boolean = System.IO.Directory.Exists(rulesDir)
        If dirExists Then
            For Each f As String In RequiredFiles
                If Not System.IO.File.Exists(System.IO.Path.Combine(rulesDir, f)) Then missing.Add(f)
            Next
        End If

        If (Not dirExists) OrElse missing.Count > 0 Then
            Dim sb As New System.Text.StringBuilder()
            sb.AppendLine("StudPlacer code tables not found.")
            sb.AppendLine()
            sb.AppendLine("Looked in:")
            sb.AppendLine("    " & rulesDir)
            sb.AppendLine()
            If Not dirExists Then
                sb.AppendLine("That folder does not exist.")
            Else
                sb.AppendLine("Folder exists, but these files are missing:")
                For Each f As String In missing
                    sb.AppendLine("    " & f)
                Next
            End If
            sb.AppendLine()
            sb.AppendLine("The install root needs BOTH of these folders side by side:")
            sb.AppendLine("    <root>\vb\       the engine   (this one resolved, or you would")
            sb.AppendLine("                      not have got this far -- AddVbFile found it)")
            sb.AppendLine("    <root>\rules\    the code tables")
            sb.AppendLine()
            sb.AppendLine("Copy the rules folder out of the StudPlacer package to:")
            sb.AppendLine("    " & rulesDir)
            sb.AppendLine()
            sb.AppendLine("It should end up containing exactly:")
            For Each f As String In RequiredFiles
                sb.AppendLine("    " & f)
            Next
            sb.AppendLine()
            sb.AppendLine("If the tables live somewhere else, set the STUD_RULES_DIR parameter")
            sb.AppendLine("on the assembly to point at them.")

            Dim found As List(Of String) = FindCandidates(rulesDir)
            If found.Count > 0 Then
                sb.AppendLine()
                sb.AppendLine("--------------------------------------------------------------")
                sb.AppendLine("Found the code tables already sitting here:")
                For Each c As String In found
                    sb.AppendLine("    " & c)
                Next
                sb.AppendLine()
                sb.AppendLine("Either move that folder to the location above, or set")
                sb.AppendLine("STUD_RULES_DIR on the assembly to that path.")
            End If
            Throw New Exception(sb.ToString())
        End If

        Dim rt As New RuleTables()
        rt.walls = CsvTable.Load(System.IO.Path.Combine(rulesDir, "table_1_6_walls.csv"))
        rt.floors = CsvTable.Load(System.IO.Path.Combine(rulesDir, "table_1_7_floors.csv"))
        rt.Constraints = StudConstraints.Load(System.IO.Path.Combine(rulesDir, "global_constraints.csv"))
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
