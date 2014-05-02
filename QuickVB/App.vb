' Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports ConsoleGUI
Imports System.Reflection
Imports System.ComponentModel.Composition.Hosting
Imports System.IO

Module App
    Public TheWorkspace As QuickVBWorkspace

    Public CompositionContainer As CompositionContainer

    Dim TheScreen As MyScreen

    ReadOnly untitledText As String = File.ReadAllText("TestProject/Untitled.vb")

    Public Sub NewUntitledWorkspace()
        TheWorkspace = New QuickVBWorkspace()

        Dim projectId = TheWorkspace.CreateProject("Untitled", "Untitled.exe")
        Dim documentId = TheWorkspace.CreateDocument(projectId, "Untitled.vb")
    End Sub

    Public Sub LoadSelfWorkspace()
        TheWorkspace = New QuickVBWorkspace()

        Dim appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        Dim slnFolder = Path.GetFullPath(Path.Combine(appPath, "..\..\.."))
        Do
            Dim slnPath = Path.Combine(slnFolder, "QuickVB.sln")
            If File.Exists(slnPath) Then
                TheWorkspace.LoadExistingSolution(slnPath)
                Exit Sub
            End If

            Dim newSlnFolder = Path.GetFullPath(Path.Combine(slnFolder, ".."))
            If newSlnFolder = slnFolder Then
                Exit Sub
            End If
            slnFolder = newSlnFolder
        Loop
    End Sub


    Sub Main()
        LoadComponents()

        TheScreen = New MyScreen

        TheScreen.Post(
            Sub()
                NewUntitledWorkspace()
                TheScreen.ViewDocument("Untitled", "Untitled.vb", untitledText)
            End Sub)

        ConsoleGUI.Screen.NavigateTo(TheScreen)
    End Sub

    Public Sub LoadComponents(ParamArray attributedParts As Object())
        Dim compositionManager As New RoslynCompositionManager()
        compositionManager.Add(Assembly.Load("QuickVB"))
        compositionManager.Compose(attributedParts)

        CompositionContainer = compositionManager.Container
    End Sub
End Module
