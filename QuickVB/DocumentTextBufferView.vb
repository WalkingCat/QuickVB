' Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports ConsoleGUI
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Text

Public Class DocumentTextBufferView
    Inherits TextBufferView

    Public Shadows Property Buffer As DocumentTextBuffer
        Get
            Return CType(MyBase.Buffer, DocumentTextBuffer)
        End Get
        Set(value As DocumentTextBuffer)
            MyBase.Buffer = value
        End Set
    End Property

    Sub LoadDocument(projectName As String, documentName As String)
        Dim workspace = App.TheWorkspace

        If workspace IsNot Nothing AndAlso Buffer.DocumentId IsNot Nothing Then
            workspace.UnregisterTextBuffer(Buffer.DocumentId, Buffer)
        End If

        Dim project = workspace.CurrentSolution.Projects.Where(Function(proj) proj.Name = projectName).Single()
        Dim document = project.Documents.Where(Function(doc) doc.Name = documentName).Single()
        Buffer.DocumentId = document.Id

        workspace.RegisterTextBuffer(document, Buffer)
    End Sub


    Dim _completionList As CompletionList = Nothing

    Public Sub MaybeTriggerCompletionList(triggerChar As Char?)
        Dim theScreen = Screen.GetScreen()

        If CompletionList.CheckIfActive(_completionList) Then _completionList.Deactivate(theScreen)

        Dim document = Buffer.Document

        _completionList = CompletionList.TryCreate(document, Me, theScreen, triggerChar)

        If _completionList IsNot Nothing Then _completionList.Activate(theScreen, Nothing)
    End Sub

    ReadOnly Property IsCompletionListActive As Boolean
        Get
            Return CompletionList.CheckIfActive(_completionList)
        End Get
    End Property



    Dim _activeDiagnostics As List(Of Diagnostic)

    Public Sub UpdateDiagnostics(allDiagnostics As IEnumerable(Of Diagnostic))
        Dim theScreen = CType(Screen.GetScreen(), MyScreen)

        If allDiagnostics Is Nothing Then
            theScreen.SetStatus(Nothing, False)

            Exit Sub
        End If

        Dim document = Buffer.Document
        Dim position = document.GetTextAsync.Result.GetPositionFromLineAndColumn(CursorRow, CursorColumn)
        Dim span = New TextSpan(position, 0)

        Dim tree = document.GetSyntaxTreeAsync.Result
        _activeDiagnostics = (From diagnostic In allDiagnostics
                              Where diagnostic.Location.IsInSource AndAlso
                                    diagnostic.Location.SourceTree.FilePath = tree.FilePath AndAlso
                                    diagnostic.Location.SourceSpan.IntersectsWith(position)
                             ).ToList()

        If _activeDiagnostics.Any() Then
            Dim descriptions = _activeDiagnostics.Select(Function(diagnostic) diagnostic.GetMessage())

            Dim description As String
            If descriptions.Count = 1 Then
                description = descriptions.First
            Else
                Dim moreDiagnostics = String.Format(" (+ {0} more...)", _activeDiagnostics.Count - 1)
                description = descriptions.First + moreDiagnostics
            End If

            theScreen.SetStatus(description, True)
        Else
            theScreen.SetStatus(Nothing, False)
        End If
    End Sub

End Class