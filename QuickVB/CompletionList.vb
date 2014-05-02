' Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports ConsoleGUI
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Recommendations
Imports Microsoft.CodeAnalysis.Text

Public Class CompletionList
    Inherits Menu

    Dim _originalDocument As Document
    Dim _originalReplaceSpan As TextSpan
    Dim _bufferView As TextBufferView
    Dim _screen As Screen
    Dim _items As IList(Of String)

    Dim _completionCharacter As Char?

    Private Sub New(document As Document, replaceSpan As TextSpan, buffer As TextBufferView, screen As Screen, items As IList(Of String))
        _originalDocument = document
        _originalReplaceSpan = replaceSpan
        _bufferView = buffer
        _screen = screen
        _items = items

        Me.DropDownMenu = True
        Me.AllowNoSelection = True
    End Sub

    Private Shared CommitCharacters As Char() = {"."c, "("c, ControlChars.Tab, ControlChars.Cr}
    Private Shared Function IsCommitCharacter(character As Char) As Boolean
        Return CommitCharacters.Contains(character)
    End Function

    Public Shared Function TryCreate(document As Document, buffer As TextBufferView, screen As Screen, triggerChar As Char?) As CompletionList
        Dim text = document.GetTextAsync.Result
        Dim position = text.GetPositionFromLineAndColumn(buffer.CursorRow, buffer.CursorColumn)

        If position = 0 OrElse triggerChar = ControlChars.Back Then Return Nothing

        Dim token = document.GetSyntaxRootAsync.Result.FindToken(position)
        Dim replaceSpan = token.Span

        Dim symbols = Recommender.GetRecommendedSymbolsAtPosition(document.GetSemanticModelAsync.Result, replaceSpan.Start, document.Project.Solution.Workspace)

        If Not symbols.Any Then Return Nothing

        Dim items = symbols.Select(Function(symbol) symbol.Name).Distinct.ToList

        Dim completionList As New CompletionList(document, replaceSpan, buffer, screen, items)

        completionList.UpdateItems(document)

        Return completionList
    End Function

    Public Sub UpdateItems(document As Document)
        Dim text = document.GetTextAsync.Result
        Dim position = text.GetPositionFromLineAndColumn(_bufferView.CursorRow, _bufferView.CursorColumn)

        Dim filterLength = position - _originalReplaceSpan.Start
        If filterLength < 0 Then
            Me.Hide()
            Exit Sub
        End If
        Dim filterSpan As New TextSpan(_originalReplaceSpan.Start, filterLength)


        Dim filter = text.GetSubText(filterSpan).ToString().ToLower

        If (filterLength = 0 AndAlso (position <= 1 OrElse Char.IsWhiteSpace(text(position - 2)))) OrElse
           (filterLength > 0 AndAlso filter.All(Function(ch) Char.IsDigit(ch))) Then
            Me.Hide()
            Exit Sub
        End If

        Dim filterPredicate = Function(item As String) item.ToLower.IndexOf(filter, StringComparison.InvariantCultureIgnoreCase) >= 0
        Dim filteredItems = From item In _items
                            Where filterPredicate(item)
                            Order By item.ToLower.StartsWith(filter) Descending, item

        If Not filteredItems.Any() Then
            Me.Hide()
            Exit Sub
        End If

        Dim menuItems As New List(Of MenuItem(Of String))
        Dim limitedMenuItems = filteredItems.Take(8).ToList
        For Each item In limitedMenuItems.Take(7)
            Dim menuItem = New MenuItem(Of String)(item, item,
                Sub()
                    Dim replaceWithText = item

                    Dim newText = _originalDocument.GetTextAsync.Result.Replace(_originalReplaceSpan, replaceWithText)
                    Dim newSolution = document.Project.Solution.WithDocumentText(document.Id, newText)
                    newSolution.Workspace.TryApplyChanges(newSolution)

                    _bufferView.MoveCursor(_bufferView.CursorRow, _bufferView.CursorColumn + (replaceWithText.Length - filterSpan.Length))

                    Me.Hide()
                End Sub)
            menuItems.Add(menuItem)
        Next
        If limitedMenuItems.Count > 7 Then
            Dim ellipsisMenuItem = New MenuItem(Of String)("...", Nothing)
            menuItems.Add(ellipsisMenuItem)
        End If

        Me.Top = _bufferView.ScreenTop + _bufferView.CursorRow - _bufferView.WindowTop + 1
        Me.Left = _bufferView.ScreenLeft + text.Lines.GetLinePosition(filterSpan.Start).Character - _bufferView.WindowLeft + 1

        Me.SetMenuItems(menuItems.ToArray())
        If filter <> "" And filterPredicate(menuItems.First.Value) Then Me.ActivateMenuItem(_screen, menuItems.First)
        Me.Activate(_screen, Nothing)
    End Sub

    Public Overrides Sub OnBeforeKeyDown(sender As Object, e As KeyEvent)
        MyBase.OnBeforeKeyDown(sender, e)

        If e.Handled Then Exit Sub

        If IsCommitCharacter(e.Character) Then
            If Me.ActiveMenuItem IsNot Nothing Then
                Commit(e.Character)
                If e.Character = ControlChars.Tab Then
                    e.Handled = True
                End If
            Else
                Hide()
            End If
        ElseIf e.Character = " "c Then
            Hide()
        End If
    End Sub

    Public Overrides Sub OnAfterKeyDown(sender As Object, e As KeyEvent)
        MyBase.OnAfterKeyDown(sender, e)

        Dim item As String
        If Me.ActiveMenuItem IsNot Nothing Then
            item = CType(Me.ActiveMenuItem, MenuItem(Of String)).Value
        Else
            item = _items.FirstOrDefault
        End If

        If item Is Nothing Then Exit Sub

        Dim document = App.TheWorkspace.CurrentSolution.GetDocument(_originalDocument.Id)

        Dim text = document.GetTextAsync.Result
        Dim position = text.GetPositionFromLineAndColumn(_bufferView.CursorRow, _bufferView.CursorColumn)

        If position < _originalReplaceSpan.Start Then
            Hide()
            Exit Sub
        End If

        If Char.IsLetterOrDigit(e.Character) Or e.Character = ControlChars.Back Then
            UpdateItems(document)
        End If
    End Sub

    Public Sub Commit(Optional completionCharacter As Char? = Nothing)
        _completionCharacter = completionCharacter

        Me.SelectMenuItem()
    End Sub

    Public Sub Hide()
        Me.Deactivate(_screen)
    End Sub

    Public Shared Function CheckIfActive(list As CompletionList) As Boolean
        Return (list IsNot Nothing AndAlso list.IsActive)
    End Function
End Class
