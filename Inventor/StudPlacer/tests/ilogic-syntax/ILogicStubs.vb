' =============================================================================
'  ILogicStubs.vb  --  TEST SCAFFOLDING ONLY. Never deployed.
'
'  Stand-ins for the globals iLogic injects into every rule, so the rule files
'  in ../../ilogic can be compiled and type-checked off Windows.
' =============================================================================
Option Strict Off
Option Explicit On

Imports System
Imports System.IO
Imports Inventor

Public Class ThisDocStub
    Public Property Document As Inventor.Document = New Inventor.AssemblyDocument()
    Public Property Path As String = System.IO.Path.GetTempPath()
    Public Function FileName(ByVal includeExtension As Boolean) As String
        Return "STUB-MODULE"
    End Function
End Class

Public Module ILogicGlobals
    Public ReadOnly ThisApplication As New Inventor.Application()
    Public ReadOnly ThisDoc As New ThisDocStub()

    ''' <summary>iLogic's parameter accessor. Not used by these rules, but present
    ''' so the compile check matches what a rule author can reach for.</summary>
    Public Function Parameter(ByVal name As String) As Object
        Return Nothing
    End Function
End Module
