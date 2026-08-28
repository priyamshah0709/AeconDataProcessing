' =============================================================================
'  InventorStubs.vb  --  TEST SCAFFOLDING ONLY. Never deployed.
'
'  Minimal stand-ins for the Inventor interop types that StudPlacer.vb touches,
'  so the file can be compiled and type-checked on a machine without Inventor.
'  Member names and signatures mirror the real API; the bodies just record what
'  was asked for so the tests can assert on it.
' =============================================================================
Option Strict Off
Option Explicit On

Imports System
Imports System.Collections.Generic

Namespace Inventor

    Public Class Point
        Public Property X As Double
        Public Property Y As Double
        Public Property Z As Double
    End Class

    Public Class Vector
        Public Property X As Double
        Public Property Y As Double
        Public Property Z As Double
    End Class

    Public Class Matrix
        Public Origin As Point = New Point()
        Public AxisX As Vector = New Vector()
        Public AxisY As Vector = New Vector()
        Public AxisZ As Vector = New Vector()

        Public Sub SetCoordinateSystem(ByVal o As Point, ByVal xa As Vector, ByVal ya As Vector, ByVal za As Vector)
            Origin = o : AxisX = xa : AxisY = ya : AxisZ = za
        End Sub
    End Class

    Public Class TransientGeometry
        Public Function CreateMatrix() As Matrix
            Return New Matrix()
        End Function
        Public Function CreatePoint(ByVal x As Double, ByVal y As Double, ByVal z As Double) As Point
            Dim p As New Point() : p.X = x : p.Y = y : p.Z = z : Return p
        End Function
        Public Function CreateVector(ByVal x As Double, ByVal y As Double, ByVal z As Double) As Vector
            Dim v As New Vector() : v.X = x : v.Y = y : v.Z = z : Return v
        End Function
    End Class

    Public Class ComponentOccurrence
        Public Property Name As String = ""
        Public Property Grounded As Boolean = False
        Public Property Placement As Matrix = Nothing
        Public Property SourcePath As String = ""
        Friend Owner As ComponentOccurrences = Nothing
        Public Sub Delete()
            If Owner IsNot Nothing Then Owner.Remove(Me)
        End Sub
    End Class

    ''' <summary>1-based Item(), like the real collection.</summary>
    Public Class ComponentOccurrences
        Private items As List(Of ComponentOccurrence) = New List(Of ComponentOccurrence)
        Public ReadOnly Property Count As Integer
            Get
                Return items.Count
            End Get
        End Property
        Public Function Item(ByVal index As Integer) As ComponentOccurrence
            Return items(index - 1)
        End Function
        Public Function Add(ByVal path As String, ByVal position As Matrix) As ComponentOccurrence
            Dim o As New ComponentOccurrence()
            o.SourcePath = path : o.Placement = position : o.Owner = Me
            items.Add(o)
            Return o
        End Function
        Friend Sub Remove(ByVal o As ComponentOccurrence)
            items.Remove(o)
        End Sub
        Public Function All() As List(Of ComponentOccurrence)
            Return items
        End Function
    End Class


    ' ---- document / parameter side of the API -------------------------------
    ' Faithful on the point that matters: the real Inventor.Document interface
    ' exposes DocumentType and UnitsOfMeasure but NOT ComponentDefinition. Only
    ' the concrete document classes have that. Getting this wrong in the stub
    ' would let a rule compile here and fail inside Inventor.

    Public Enum DocumentTypeEnum
        kUnknownDocumentObject = 0
        kAssemblyDocumentObject = 12291
        kPartDocumentObject = 12290
        kDrawingDocumentObject = 12292
    End Enum

    Public Enum UnitsTypeEnum
        kDatabaseLengthUnits = 0
        kMillimeterLengthUnits = 1
        kCentimeterLengthUnits = 2
        kInchLengthUnits = 3
    End Enum

    Public Class UnitsOfMeasure
        Public Function ConvertUnits(ByVal value As Double,
                                     ByVal fromUnits As UnitsTypeEnum,
                                     ByVal toUnits As UnitsTypeEnum) As Double
            ' Only the conversion the rule actually performs: database (cm) -> mm.
            If fromUnits = UnitsTypeEnum.kDatabaseLengthUnits AndAlso
               toUnits = UnitsTypeEnum.kMillimeterLengthUnits Then
                Return value * 10.0
            End If
            Return value
        End Function
    End Class

    Public Class Parameter
        Public Property Name As String = ""
        Public Property Value As Object = Nothing
        Public Property Units As String = "ul"
        Public Property Expression As String = ""
    End Class

    Public Class UserParameters
        Friend Store As Dictionary(Of String, Parameter) =
            New Dictionary(Of String, Parameter)(StringComparer.OrdinalIgnoreCase)
        Public Function Item(ByVal name As String) As Parameter
            If Not Store.ContainsKey(name) Then Throw New ArgumentException("no such parameter: " & name)
            Return Store(name)
        End Function
        Public ReadOnly Property Count As Integer
            Get
                Return Store.Count
            End Get
        End Property
        Public Function Add(ByVal name As String, ByVal value As Object, ByVal units As String) As Parameter
            Dim p As New Parameter()
            p.Name = name : p.Value = value : p.Units = units
            Store(name) = p
            Return p
        End Function
    End Class

    Public Class Parameters
        Public ReadOnly UserParameters As New UserParameters()
        Public Function Item(ByVal name As String) As Parameter
            Return UserParameters.Item(name)
        End Function
    End Class

    Public Class Document
        Public Property DocumentType As DocumentTypeEnum = DocumentTypeEnum.kUnknownDocumentObject
        Public ReadOnly UnitsOfMeasure As New UnitsOfMeasure()
        Public Property FullFileName As String = ""
    End Class

    Public Class AssemblyComponentDefinition
        Public ReadOnly Occurrences As New ComponentOccurrences()
        Public ReadOnly Parameters As New Parameters()
    End Class

    Public Class AssemblyDocument
        Inherits Document
        Public ReadOnly ComponentDefinition As New AssemblyComponentDefinition()
        Public Sub New()
            MyBase.New()
            DocumentType = DocumentTypeEnum.kAssemblyDocumentObject
        End Sub
    End Class

    Public Class Transaction
        Public Ended As Boolean = False
        Public Aborted As Boolean = False
        Public Sub [End]()
            Ended = True
        End Sub
        Public Sub Abort()
            Aborted = True
        End Sub
    End Class

    Public Class TransactionManager
        Public LastName As String = ""
        Public Function StartTransaction(ByVal target As Object, ByVal name As String) As Transaction
            LastName = name
            Return New Transaction()
        End Function
    End Class

    Public Class UserInterfaceManager
        Public Property UserInteractionDisabled As Boolean = False
    End Class

    Public Class Application
        Public ReadOnly TransientGeometry As New TransientGeometry()
        Public ReadOnly TransactionManager As New TransactionManager()
        Public ReadOnly UserInterfaceManager As New UserInterfaceManager()
        Public Property ScreenUpdating As Boolean = True
    End Class

End Namespace
