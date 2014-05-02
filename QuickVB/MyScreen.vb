' Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports ConsoleGUI
Imports System.ComponentModel.Composition.Hosting
Imports System.Threading
Imports Microsoft.CodeAnalysis

Public Class MyScreen
    Inherits Screen

    Dim WithEvents DocumentBuffer As DocumentTextBuffer
    Dim WithEvents DocumentBufferView As DocumentTextBufferView
    Dim WithEvents DocumentPane As Pane

    Dim WithEvents DiagnosticsBuffer As TextBuffer
    Dim WithEvents ErrorBufferView As TextBufferView
    Dim WithEvents DiagnosticsPane As Pane

    Dim MainMenu As Menu

    Dim SystemMenuItem As MenuItem

    Dim StatusTextLabel As Label
    Dim StatusPositionLabel As Label

    Dim _activeBufferView As TextBufferView
    Property ActiveBufferView As TextBufferView
        Get
            Return _activeBufferView
        End Get
        Set(value As TextBufferView)
            _activeBufferView = value
            If TypeOf _activeBufferView Is DocumentTextBufferView Then
                _activeDocumentBufferView = CType(_activeBufferView, DocumentTextBufferView)
            End If
        End Set
    End Property

    Dim _activeDocumentBufferView As DocumentTextBufferView
    ReadOnly Property ActiveDocumentBufferView As DocumentTextBufferView
        Get
            Return _activeDocumentBufferView
        End Get
    End Property


    Dim RoslynEnabled As Boolean = False


    Public Sub New()
        Console.Title = "QuickVB"

        DocumentBuffer = New DocumentTextBuffer
        DiagnosticsBuffer = New TextBuffer

        DocumentBufferView = New DocumentTextBufferView
        DocumentBufferView.Buffer = DocumentBuffer
        DocumentBufferView.BackgroundColor = ConsoleColor.DarkBlue
        DocumentBufferView.ForegroundColor = ConsoleColor.Gray

        DocumentPane = New Pane
        DocumentPane.Left = 0
        DocumentPane.Top = 1
        DocumentPane.Width = 80
        DocumentPane.Height = 21
        DocumentPane.PaddingBottom = 2
        DocumentPane.BackgroundColor = ConsoleColor.DarkBlue
        DocumentPane.ForegroundColor = ConsoleColor.Gray
        DocumentPane.Title = "Loading..."
        DocumentPane.Controls.Add(DocumentBufferView)

        ErrorBufferView = New TextBufferView
        ErrorBufferView.Buffer = DiagnosticsBuffer
        ErrorBufferView.Left = 1
        ErrorBufferView.Top = 17
        ErrorBufferView.Width = 78
        ErrorBufferView.Height = 5
        ErrorBufferView.ScrollBars = False
        ErrorBufferView.BackgroundColor = ConsoleColor.DarkBlue
        ErrorBufferView.ForegroundColor = ConsoleColor.Gray

        DiagnosticsPane = New Pane
        DiagnosticsPane.Left = 0
        DiagnosticsPane.Top = 21
        DiagnosticsPane.Width = 80
        DiagnosticsPane.Height = 4
        DiagnosticsPane.PaddingBottom = 1
        DiagnosticsPane.BackgroundColor = ConsoleColor.DarkBlue
        DiagnosticsPane.ForegroundColor = ConsoleColor.Gray
        DiagnosticsPane.Title = "Output"
        DiagnosticsPane.Controls.Add(ErrorBufferView)

        Dim SystemMenu As Menu = New Menu
        SystemMenu.DropDownMenu = True

        Dim FileMenu As Menu = New Menu
        FileMenu.DropDownMenu = True
        FileMenu.SetMenuItems(New MenuItem() {
            New MenuItem(Name:="&New Program"),
            New MenuItem(Name:="&Open Program..."),
            New MenuItem(Name:="Open &Self", Action:=Sub()
                                                         App.LoadSelfWorkspace()
                                                         SetStatus(Nothing, False)
                                                         ViewDocument(DocumentBufferView, DocumentPane, "QuickVB", "MyScreen.vb")
                                                     End Sub),
            New MenuItem(Name:="Save &As..."),
            New MenuItem(Separator:=True),
            New MenuItem(Name:="&Print..."),
            New MenuItem(Separator:=True),
            New MenuItem(Name:="E&xit", Action:=Sub() Environment.Exit(0))})


        Dim EditMenu As Menu = New Menu
        EditMenu.DropDownMenu = True
        EditMenu.SetMenuItems(New MenuItem() {
            New MenuItem(Name:="Cu&t       Shift+Del"),
            New MenuItem(Name:="&Copy       Ctrl+Ins"),
            New MenuItem(Name:="&Paste     Shift+Ins")})


        Dim ViewMenu As Menu = New Menu
        ViewMenu.DropDownMenu = True
        ViewMenu.SetMenuItems(New MenuItem() {
            New MenuItem(Name:="&SUBs...            F2"),
            New MenuItem(Name:="O&utput Screen      F4"),
            New MenuItem(Name:="Included &Lines"),
            New MenuItem(Separator:=True),
            New MenuItem(Name:="Switch to..."),
            New MenuItem(Name:="QuickVB\MyScreen.vb", Action:=Sub() ViewDocument(DocumentBufferView, DocumentPane, "QuickVB", "MyScreen.vb")),
            New MenuItem(Name:="ConsoleGUI\Screen.cs", Action:=Sub() ViewDocument(DocumentBufferView, DocumentPane, "ConsoleGUI", "Screen.cs"))})


        Dim SearchMenu As Menu = New Menu
        SearchMenu.DropDownMenu = True
        SearchMenu.SetMenuItems(New MenuItem() {
            New MenuItem(Name:="&Find..."),
            New MenuItem(Name:="&Change...")})


        Dim RunMenu As Menu = New Menu
        RunMenu.DropDownMenu = True
        RunMenu.SetMenuItems(New MenuItem() {
            New MenuItem(Name:="&Start   F5", Action:=Sub()
                                                          RunProgram()
                                                      End Sub),
            New MenuItem(Name:="&Build", Action:=Sub()
                                                     BuildProgram()
                                                 End Sub)
        })


        Dim DebugMenu As Menu = New Menu
        DebugMenu.DropDownMenu = True
        DebugMenu.SetMenuItems(New MenuItem() {
            New MenuItem(Name:="&Add Watch..."),
            New MenuItem(Name:="&Instant Watch...   Shift+F9"),
            New MenuItem(Name:="&Delete Watch..."),
            New MenuItem(Separator:=True),
            New MenuItem(Name:="Toggle &Breakpoint        F9"),
            New MenuItem(Name:="&Clear All Breakpoints")})


        Dim OptionsMenu As Menu = New Menu
        OptionsMenu.DropDownMenu = True
        OptionsMenu.SetMenuItems(New MenuItem() {
            New MenuItem(Name:="&Display..."),
            New MenuItem(Name:="Set &Paths..."),
            New MenuItem(Name:="&Full Menus"),
            New MenuItem(Separator:=True),
            New MenuItem(Name:="Enable &Roslyn", Action:=AddressOf EnableRoslyn)})

        Dim HelpMenu As Menu = New Menu
        HelpMenu.DropDownMenu = True
        HelpMenu.SetMenuItems(New MenuItem() {
            New MenuItem(Name:="&Alex Turner", Action:=Sub()
                                                       End Sub),
            New MenuItem(Name:="&Ian Halliday", Action:=Sub()
                                                        End Sub),
            New MenuItem(Name:="and &Roslyn!", Action:=Sub()
                                                       End Sub)})



        MainMenu = New Menu
        MainMenu.Left = 0
        MainMenu.Top = 0
        MainMenu.Width = Me.Width
        MainMenu.Height = 1

        SystemMenuItem = New MenuItem(Name:="&≡", DropDownMenu:=SystemMenu, Visible:=False)

        MainMenu.SetMenuItems(New MenuItem() {
            SystemMenuItem,
            New MenuItem(Name:="&File", DropDownMenu:=FileMenu),
            New MenuItem(Name:="&Edit", DropDownMenu:=EditMenu),
            New MenuItem(Name:="&View", DropDownMenu:=ViewMenu),
            New MenuItem(Name:="&Search", DropDownMenu:=SearchMenu),
            New MenuItem(Name:="&Run", DropDownMenu:=RunMenu),
            New MenuItem(Name:="&Debug", DropDownMenu:=DebugMenu),
            New MenuItem(Name:="&Options", DropDownMenu:=OptionsMenu),
            New MenuItem(Name:="&Help", AnchorRight:=True, DropDownMenu:=HelpMenu)})

        Me.BackgroundColor = ConsoleColor.DarkBlue

        StatusTextLabel = New Label
        StatusTextLabel.Top = 24
        StatusTextLabel.Left = 0
        StatusTextLabel.Width = 80
        StatusTextLabel.PaddingLeft = 1
        StatusTextLabel.PaddingRight = 1
        StatusTextLabel.BackgroundColor = ConsoleColor.DarkCyan
        StatusTextLabel.ForegroundColor = ConsoleColor.White
        StatusTextLabel.Text = "<F6=Window> <F5=Run>"

        StatusPositionLabel = New Label
        StatusPositionLabel.Top = 24
        StatusPositionLabel.Left = 80 - 18
        StatusPositionLabel.Width = 18
        StatusPositionLabel.PaddingRight = 1
        StatusPositionLabel.BackgroundColor = ConsoleColor.DarkCyan
        StatusPositionLabel.ForegroundColor = ConsoleColor.Black
        StatusPositionLabel.Text = " "

        Me.Controls.Add(DiagnosticsPane)
        Me.Controls.Add(DocumentPane)
        Me.Controls.Add(MainMenu)
        Me.Controls.Add(StatusTextLabel)
        Me.Controls.Add(StatusPositionLabel)

        Me.ActiveControl = DocumentBufferView
        ActiveBufferView = DocumentBufferView
    End Sub


    Private Sub MyScreen_BeforeRender(sender As Object, e As EventArgs) Handles Me.BeforeRender
        Dim oldPositionText = StatusPositionLabel.Text
        StatusPositionLabel.Text = "│       " + String.Format("{0:D5}:{1:D3}", ActiveBufferView.CursorRow + 1, ActiveBufferView.CursorColumn + 1)
        If StatusPositionLabel.Text <> oldPositionText Then Me.Invalidate()
    End Sub

    Private Sub MyScreen_BeforeKeyDown(sender As Object, e As KeyEvent) Handles Me.BeforeKeyDown
        If (e.ControlKeyState And (ControlKeyState.LEFT_CTRL_PRESSED Or ControlKeyState.RIGHT_CTRL_PRESSED)) <> 0 Then
            Select Case e.VirtualKey
                Case VirtualKey.VK_SPACE
                    ActiveDocumentBufferView.MaybeTriggerCompletionList(Nothing)

                    e.Handled = True
            End Select
        End If

        If e.Handled Then Exit Sub

        Select Case e.VirtualKey
            Case VirtualKey.VK_F5
                RunProgram()
            Case VirtualKey.VK_F6
                SwitchPane()
            Case VirtualKey.VK_MENU, VirtualKey.VK_F10
                If Not MainMenu.IsActive() And MainMenu IsNot Me.ActiveControl Then
                    MainMenu.Activate(Me, Me.ActiveControl)
                End If
        End Select
    End Sub

    Private Sub MyScreen_AfterKeyDown(sender As Object, e As KeyEvent) Handles Me.AfterKeyDown
        If Not ActiveDocumentBufferView.IsCompletionListActive Then
            Dim document = ActiveDocumentBufferView.Buffer.Document
            Dim text = document.GetTextAsync.Result
            If ActiveDocumentBufferView.CursorRow >= text.Lines.Count Then Exit Sub
            Dim position = text.GetPositionFromLineAndColumn(ActiveDocumentBufferView.CursorRow, ActiveDocumentBufferView.CursorColumn - 1)

            If e.Character <> Nothing Then
                ActiveDocumentBufferView.MaybeTriggerCompletionList(e.Character)
            End If
        End If
    End Sub

    Private Sub SwitchPane()
        ActiveBufferView = If(ActiveBufferView Is DocumentBufferView, ErrorBufferView, DocumentBufferView)
        Me.ActiveControl = ActiveBufferView
        DocumentBufferView.ScrollBarsVisible = ActiveBufferView Is DocumentBufferView
        Me.Invalidate()
    End Sub

    Private Sub ViewDocument(bufferView As DocumentTextBufferView, pane As Pane, projectName As String, documentName As String, Optional newText As String = Nothing)
        Try
            bufferView.LoadDocument(projectName, documentName)
        Catch ex As Exception
            SetStatus("NOTE: Please Open Self on the File menu first.", True)
            Exit Sub
        End Try

        If documentName.EndsWith(".vb") Then
            Console.Title = "QuickVB"
            Me.Theme = ControlTheme.Basic
            DocumentBufferView.ForegroundColor = ConsoleColor.Gray
            StatusPositionLabel.Visible = True
            SystemMenuItem.Visible = False
            MainMenu.Layout()
        Else
            Console.Title = "QuickVB - Anders mode"
            Me.Theme = ControlTheme.CSharp
            DocumentBufferView.ForegroundColor = ConsoleColor.Yellow
            StatusPositionLabel.Visible = False
            SystemMenuItem.Visible = True
            MainMenu.Layout()
        End If

        SetStatus(Nothing, False)

        pane.Title = documentName

        If newText IsNot Nothing Then
            bufferView.Buffer.Text = newText
            OnLineCommit(bufferView.Buffer)
        End If
    End Sub

    Public Sub SetStatus(message As String, highlight As Boolean)
        Dim defaultStatus As String
        Dim background As ConsoleColor
        Dim foreground As ConsoleColor

        If ActiveDocumentBufferView.Buffer.Document IsNot Nothing AndAlso ActiveDocumentBufferView.Buffer.Document.Name.EndsWith(".vb") Then
            defaultStatus = "<F6=Window> <F5=Run>"
            background = ConsoleColor.DarkCyan
            foreground = ConsoleColor.White
        Else
            defaultStatus = "F6 Window  F5 Run  F10 Menu"
            background = ConsoleColor.Gray
            foreground = ConsoleColor.Black
        End If

        If message IsNot Nothing Then
            StatusTextLabel.Text = message
        Else
            StatusTextLabel.Text = defaultStatus
        End If

        If Not highlight Then
            StatusTextLabel.BackgroundColor = background
            StatusTextLabel.ForegroundColor = foreground
        Else
            StatusTextLabel.BackgroundColor = foreground
            StatusTextLabel.ForegroundColor = background
        End If
    End Sub

    Public Sub ViewDocument(projectName As String, documentName As String, Optional newText As String = Nothing)
        ViewDocument(Me.DocumentBufferView, Me.DocumentPane, projectName, documentName, newText)
    End Sub


    ' Compilation:

    Private _diagnostics As IReadOnlyList(Of Diagnostic)

    Private Sub RunProgram()
        If Not RoslynEnabled Then
            SetStatus("NOTE: Please enable Roslyn on the Options menu first.", True)
            Exit Sub
        End If

        App.TheWorkspace.RunProgram(Me)
    End Sub

    Private Sub BuildProgram()
        If Not RoslynEnabled Then
            SetStatus("NOTE: Please enable Roslyn on the Options menu first.", True)
            Exit Sub
        End If

        Dim diagnostics = App.TheWorkspace.BuildProgram()
        UpdateDiagnostics(diagnostics)
    End Sub

    Private Sub UpdateDiagnostics(diagnostics As IEnumerable(Of Diagnostic), Optional cancel As CancellationToken = Nothing)
        If diagnostics IsNot Nothing AndAlso diagnostics.Any Then
            Dim diagnosticsString = String.Join(vbNewLine, diagnostics)
            DiagnosticsBuffer.Text = diagnosticsString
            DiagnosticsPane.Title = "Diagnostics"
        Else
            DiagnosticsBuffer.Text = ""
            DiagnosticsPane.Title = "Output"
        End If

        If diagnostics IsNot Nothing Then
            _diagnostics = diagnostics.ToList.AsReadOnly
            ActiveDocumentBufferView.Buffer.Colorize(_diagnostics, cancel)
        End If

        Screen.GetScreen.Invalidate()
    End Sub


    ' Colorization/Formatting:

    Private _lineDirty As Boolean = False

    Private Sub DocumentBuffer_BufferChanged(sender As Object, e As EventArgs) Handles DocumentBuffer.BufferChanged
        If Not RoslynEnabled Then Exit Sub

        Dim buffer = CType(sender, DocumentTextBuffer)

        _lineDirty = True

        OnCharacterChanged(buffer)
    End Sub

    Private Sub DocumentBufferView_RowChanged(sender As Object, e As RowChangedEventArgs) Handles DocumentBufferView.RowChanged
        If Not RoslynEnabled Then Exit Sub

        Dim bufferView = CType(sender, DocumentTextBufferView)

        If _lineDirty Then
            _lineDirty = False

            OnLineCommit(bufferView.Buffer)
        End If
    End Sub

    Private Sub DocumentBufferView_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles DocumentBufferView.SelectionChanged
        If Not RoslynEnabled Then Exit Sub

        Dim bufferView = CType(sender, DocumentTextBufferView)

        bufferView.UpdateDiagnostics(_diagnostics)
    End Sub

    Private Sub OnCharacterChanged(buffer As DocumentTextBuffer)
        If Not RoslynEnabled Then Exit Sub

        ' TODO: Do this conversion in a background task?
        App.TheWorkspace.ChangedDocumentTextInternal(buffer.DocumentId, buffer.Text)

        buffer.Colorize()
    End Sub

    Private Sub OnLineCommit(buffer As DocumentTextBuffer)
        If Not RoslynEnabled Then Exit Sub

        Dim workspace = App.TheWorkspace

        buffer.Format()
        workspace.BackgroundCompile(
            Sub(diagnostics, cancel)
                UpdateDiagnostics(diagnostics)
            End Sub)
    End Sub

    Private Sub RefreshDocument(buffer As DocumentTextBuffer)
        OnCharacterChanged(buffer)
        OnLineCommit(buffer)
    End Sub

    Private Sub EnableRoslyn()
        If RoslynEnabled Then Exit Sub

        SetStatus(Nothing, False)

        RoslynEnabled = True
        RefreshDocument(ActiveDocumentBufferView.Buffer)
    End Sub
End Class
