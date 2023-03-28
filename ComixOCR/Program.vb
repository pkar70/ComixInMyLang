Imports System
Imports System.Collections.ObjectModel
Imports System.IO
Imports Windows.ApplicationModel.DataTransfer

Module Program
    Sub Main(args As String())
        Console.WriteLine("Comix to OCR")

        If args.Count <> 1 Then
            Console.WriteLine("There should be one (and only one) parameter - cbz or cbr filename")
            Return
        End If

        Dim filename As String = args(0).ToLowerInvariant

        If Not IO.File.Exists(filename) Then
            Console.WriteLine($"Seems like file '{args(0)}' doesnt exist")
            Return
        End If

        If filename.ToLowerInvariant.EndsWith(".cbz") OrElse filename.ToLowerInvariant.EndsWith(".cbr") Then
            ZrobOCRcalosci(filename).Wait()
        Else
            Console.WriteLine($"Ale to nie .cbr/.cbz file!")
        End If
    End Sub

    Private mlTeksty As Collection(Of JedenText) = Nothing
    Private msCurrFile As String = ""


    Private Async Function ZrobOCRcalosci(filename As String) As Task

        Dim oStream As Stream = IO.File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)
        If oStream Is Nothing Then Return

        Dim oArchive = SharpCompress.Readers.ReaderFactory.Open(oStream)
        While oArchive.MoveToNextEntry
            If oArchive.Entry.IsDirectory Then Continue While

            'Dim oNew As JedenText = New JedenText
            'oNew.sFileName = oArchive.Entry.Key
            'oNew.sOriginal = vbCrLf & vbCrLf & vbCrLf & "File: " & oArchive.Entry.Key & vbCrLf & vbCrLf
            'If mlTeksty Is Nothing Then mlTeksty = New Collection(Of JedenText)
            'mlTeksty.Add(oNew)
            msCurrFile = oArchive.Entry.Key  'oFile.Name

            Try

                Console.WriteLine(oArchive.Entry.Key)
                If Console.IsOutputRedirected Then Console.Error.WriteLine(oArchive.Entry.Key)

                Dim oStreamPicek = oArchive.OpenEntryStream()
                Dim oMemStream As MemoryStream = New MemoryStream
                oStreamPicek.CopyTo(oMemStream)

                oMemStream.Seek(0, SeekOrigin.Begin)
                Dim oDecoder As Windows.Graphics.Imaging.BitmapDecoder
                Dim oMemRA = oMemStream.AsRandomAccessStream
                oDecoder = Await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(oMemRA)
                Dim moSoftBmp = Await oDecoder.GetSoftwareBitmapAsync()

                oStreamPicek.Dispose()
                oMemRA.Dispose()
                oMemStream.Dispose()

                Await ZrobOCR(moSoftBmp)
                moSoftBmp.Dispose()

            Catch ex As Exception
                Exit While
            End Try

        End While

        Dim sTemp As String = ""
        For Each oItem In mlTeksty
            sTemp = sTemp & vbCrLf & oItem.sOriginal
        Next

        'Dim oClip As New DataPackage()
        'oClip.SetText(sTemp)

        'Clipboard.SetContent(oClip)

        Console.Write(sTemp)

        'Console.WriteLine(vbCrLf & vbCrLf & " - ale takze w clipboard")

    End Function


    Private Async Function ZrobOCR(moSoftBmp As Windows.Graphics.Imaging.SoftwareBitmap) As Task(Of Boolean)
        If mlTeksty Is Nothing Then mlTeksty = New Collection(Of JedenText)

        Dim rOCR As Windows.Media.Ocr.OcrResult = Await Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages().RecognizeAsync(moSoftBmp)

        If rOCR.Lines.Count < 1 Then Return False

        Dim oNew As JedenText = New JedenText
        oNew.sFileName = msCurrFile
        oNew.sOriginal = vbCrLf & vbCrLf & vbCrLf & vbCrLf & vbCrLf & msCurrFile & vbCrLf & vbCrLf
        mlTeksty.Add(oNew)

        For Each oLine As Windows.Media.Ocr.OcrLine In rOCR.Lines
            oNew = New JedenText
            oNew.sFileName = msCurrFile
            oNew.sOriginal = oLine.Text.ToLower
            mlTeksty.Add(oNew)
        Next

        Return True
    End Function

End Module

Public Class JedenText
    Public Property sFileName As String
    Public Property sOriginal As String
    Public Property sTranslation As String

End Class
